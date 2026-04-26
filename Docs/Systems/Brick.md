# Systems — Brick

> 벽돌의 종류, 속성, 라인 생성 규칙. 숫자(스폰 확률, HP 분포)는 SpecData 또는 Stage 데이터에 둔다 — 본 문서는 **규칙**만.

---

## 1. Brick model

`Brick` 은 **`UnitController` 파생** (UnitCombatDesign §3). `Stats` 에 HP, `Effects` 에 상태이상이 살고,
DamagePipeline 이 자연스럽게 통합된다.

```
Brick : UnitController {
    SpecBrick     spec       // 또는 인스턴스 생성 시 직접 type/element 주입
    eBrickType    type       // NORMAL / SHIELD / EXPLOSIVE / SPAWNER / REFLECTOR / BOSS
    eElement      element    // NONE / FIRE / ICE / SHOCK (실드 벽돌일 때만 의미)
    GridPos       pos        // (col, row)
    // Inherited: Data.Stats (HP), Data.Effects (status), Version, IsAlive, ...
}
```

### 상태이상 (BURN / FREEZE 등) — IEffect로 표현

GDD의 상태이상은 **`IEffect` 구현체**로 표현되고, `brick.Data.Effects.Add(effect)` 로 적용된다.
즉 별도 `StatusFlags` 비트 필드가 아니다. 기존 `EffectHost` 시스템(UnitCombatDesign §5)을 그대로 재사용.

| GDD 상태이상 | IEffect 구현체            | 인터페이스                               | 동작                                                             |
|--------------|----------------------------|------------------------------------------|------------------------------------------------------------------|
| 화상         | `BurnEffect`               | `IDurationEffect` + `ITickEffect`         | 매 턴 (`UPKEEP` 의 Tick 호출 시) `tickDamage` 적용. duration 만료 시 자동 제거. |
| 동결         | `FreezeEffect`             | `IDurationEffect` + `IStackableEffect` ? | `BrickField.ShiftAllDown` 가 `Effects.HasEffect<FreezeEffect>` 검사 후 해당 벽돌 건너뜀. |
| 향후         | (POISON, SHOCK, BLEED ...) | 동일                                      | 새 IEffect 1개씩 추가.                                           |

- `hp` 가 0 이 되면 즉시 파괴 처리 (Combat.md §3 참조).
- `Effects.Tick(dt)` 는 `TurnRunner.TurnPhase.UPKEEP` 에서 한 번 호출 — 턴 단위 시뮬레이션이라
  `dt = 1.0f` (1턴) 같은 형태로 넘기는 게 권장 (Phase 6에서 확정).
- `BURN` 의 데미지 적용은 `BurnEffect.OnTick` 안에서 `brick.DealDamage` 또는 `DamagePipeline.Process` 로 라우팅 — DamageText / Relic 훅 통과 필요.

---

## 2. Brick types (GDD §4.2)

| Type        | Behavior                                                                       |
|-------------|--------------------------------------------------------------------------------|
| `NORMAL`    | 표준 벽돌. 데미지 받음, HP 0 시 파괴.                                          |
| `SHIELD`    | `element != NONE`. 발사체 element와 일치하지 않으면 데미지 0.                  |
| `EXPLOSIVE` | 파괴 시 주변 8칸의 NORMAL 벽돌에 1 데미지 (체인 가능).                          |
| `SPAWNER`   | 매 `TurnPhase.ENEMY` 직후, 주변 빈 칸 1개에 NORMAL 벽돌 1개 추가 생성.        |
| `REFLECTOR` | 데미지 받지 않음. 발사체 무조건 반사 (관통 / 통과 무시).                       |
| `BOSS`      | 고HP, 특수 패턴. 보스 시트에서 별도 정의.                                       |

### Type별 상호작용 우선순위

발사체가 벽돌과 충돌할 때:

```
1. REFLECTOR? → 무조건 반사. 데미지 무시. Trigger BRICK_HIT 발화 안 함.
2. SHIELD?   → element 일치 검사
                 일치   → 정상 데미지 처리 (NORMAL과 동일)
                 불일치 → 데미지 0, 반사. Trigger BRICK_HIT 발화하지만 BRICK_KILL 은 발화 불가.
3. NORMAL/EXPLOSIVE/SPAWNER → 정상 데미지 처리.
4. BOSS → 정상 데미지 처리 + 보스별 추가 처리 (개별 사양).
```

`EXPLOSIVE` 의 연쇄 폭발은 `BRICK_KILL` 이벤트 처리 중 발화하는 부수효과로 모델링 (Combat.md §6).

---

## 3. Element 상성

```
SHIELD(FIRE)  ← 발사체 element FIRE  : 정상 데미지
SHIELD(FIRE)  ← 발사체 element NONE  : 0
SHIELD(FIRE)  ← 발사체 element ICE   : 0
NORMAL        ← 모든 element         : 정상 데미지 (element는 무관)
```

- v0.1은 단순 binary 모델. 향후 부분 저항 (50% 등) 도입 가능 — 도입 시 본 문서 갱신 필수.
- 발사체의 element는 `SpecHitInstance.Extra["element"]` (HitSnapshot 빌드 시 결정) + 적용된 Modifier 중 마지막 비-NONE element.
  Modifier 평가 순서는 Combat.md §2.3.

---

## 4. Line generation

매 `TurnPhase.ENEMY` 에 다음이 일어난다:

```
1. 모든 살아있는 벽돌을 1줄 아래로 이동
   - FREEZE 상태(brick.Data.Effects.HasEffect<FreezeEffect>)의 벽돌은 이번 턴 이동 건너뜀
2. 1행 (생성 영역)에 새 벽돌 줄 생성
   - 줄 패턴은 SpecWave 정의에 따름 (다음 §4.1)
3. SPAWNER 벽돌의 추가 생성 처리
4. 14행(위험 라인) 아래로 내려간 벽돌 → DAMAGE phase에서 플레이어 피해 처리 후 제거
```

### 4.1 SpecWave 데이터

웨이브 / 스테이지별 줄 생성 패턴은 `SpecWave` (테이블명 `TWave`) 에 정의된다. 자세한 컬럼은
`Docs/Specs/Schema/wave.md`. 핵심 필드:

```
TWave 시트
- waveId   : string         (예: "act1_normal_01")
- lineIndex: int             (해당 웨이브 내 N번째 줄, 0-based)
- pattern  : string[8]       (가로 8칸 — 각 셀에 brickTypeId 또는 ".")
```

런타임에서 `SpecDataManager.SpecWave.Where(w => w.waveId == X).OrderBy(lineIndex)` 으로 한 웨이브의
줄 시퀀스를 얻고, 매 `TurnPhase.ENEMY` 마다 다음 줄을 1행에 enqueue.

---

## 5. Damage at danger line

`TurnPhase.DAMAGE`:

```
foreach brick that crossed row 14 this turn:
    playerHP -= baseDangerDamage   // 기본 1, Survival relic으로 변동 가능
    if playerHP <= 0: defeat
```

- `baseDangerDamage` 는 SpecData에 두기 (예: `TBalance.dangerLineDamage = 1`).
- `방어막` relic은 hook=ON_DAMAGE_TAKEN 으로 받은 후 `damageDelta = -1, min 1` 적용.

---

## 6. Authoring guide

| Want to add               | Where                                                              |
|---------------------------|--------------------------------------------------------------------|
| 새 brick type             | `eBrickType` enum 추가 → 본 §2 표 갱신 → Combat.md §3 동작 추가  |
| 새 status (e.g. POISON)   | 새 `IEffect` 구현체 추가 (`PoisonEffect : IDurationEffect, ITickEffect`) → `EffectFactory` 등록 → 본 §1 표 추가 |
| 새 line pattern           | `TWave` 시트에 행 추가 (`Specs/Schema/wave.md`)                    |
