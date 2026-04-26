# Systems — Combat

> 본 문서는 **턴 진행, 시전 시퀀스 평가, 데미지 파이프라인, 발사체 수명**의 *규칙*을 정의한다.
> 숫자는 들어가지 않는다 — 숫자는 SpecData에 있다.
>
> 이 문서가 모호하면 GDD 7.x / 8.x의 효과를 코드로 구현할 수 없다. 변경 시 `/arch-update` 흐름.

---

## 1. Turn lifecycle

한 턴은 다음 단계로 진행된다 (GDD §2 Core Loop의 정형화):

```
┌────────────────────────────────────────────────────────────┐
│  TurnPhase.AIM         플레이어가 각도 / 시전 회수 결정     │
│           │                                                 │
│           ▼                                                 │
│  TurnPhase.CAST        Weapon의 castsPerTurn 만큼 시퀀스 발사│
│           │            (각 시전마다 슬롯 평가 → Projectile  │
│           │             생성 → 발사 → 충돌/반사/Effect)     │
│           ▼                                                 │
│  TurnPhase.SETTLE      모든 발사체 소멸까지 대기            │
│           │            (시간 가속 옵션은 시각적 속도만)     │
│           ▼                                                 │
│  TurnPhase.RESOLVE     Brick HP=0 → 파괴, 드롭 처리         │
│           │            On_Battle_End 류 hook은 여기 아님    │
│           ▼                                                 │
│  TurnPhase.ENEMY       모든 벽돌 1줄 하강 + 상단 새 줄 생성 │
│           │            (스폰 벽돌 추가 생성도 여기서)       │
│           ▼                                                 │
│  TurnPhase.DAMAGE      위험 라인 도달 벽돌 → 플레이어 피해  │
│           │            (Survival relic hook 발화)           │
│           ▼                                                 │
│  TurnPhase.UPKEEP      쿨다운 -1, 마나 회복, On_Turn_End    │
│           │            상태이상 tick (화상 등)              │
│           ▼                                                 │
│  TurnPhase.AIM (next turn)                                  │
└────────────────────────────────────────────────────────────┘
```

- Phase 사이의 전환은 명시적이며, 한 phase 안에서 다음 phase의 hook이 발화하지 않는다.
- 패배 판정(HP ≤ 0)은 `TurnPhase.DAMAGE` 직후 검사. 승리 판정(잔여 벽돌 = 0 AND 웨이브 종료)은
  `TurnPhase.RESOLVE` 직후.

---

## 2. Weapon cast — 슬롯 시퀀스 평가 모델

이 부분이 GDD에서 가장 모호한 핵심이다. 다음 모델로 박아둔다.

> **코드 매핑 (확정)**: 슬롯 평가의 결과는 **`SpellSequence` 인스턴스 한 개**로 빌드되어, 내부적으로
> `HitLauncher.Launch(...)` 를 호출하여 `HitInstance` 풀에서 `MovingHit` (또는 INSTANT/AURA) 를 꺼내고
> `HitSnapshot` 을 잠근 뒤 `IHitBehavior` 들을 부착한다. 자세한 흐름은 §2.6.

### 2.1 슬롯 구조 (참조)

```
[Trigger?] [Projectile] [Modifier]* [Effect]*
```

- `Trigger` 슬롯은 0개 또는 1개. 없으면 **무조건 발화** 모드.
- `Projectile` 슬롯은 정확히 1개. 비어 있을 수 없다 (Weapon schema에서 강제).
- `Modifier` 슬롯은 0~N개 (Weapon의 `slotShape`에 따라).
- `Effect` 슬롯은 0~N개.

무기의 `slotShape` (예: `TRIGGER/PROJECTILE/MODIFIER/MODIFIER/EFFECT`)는 **카테고리 제약**일 뿐,
실제 평가 순서는 **슬롯 인덱스 오름차순**으로 동일하다.

### 2.2 평가 단계 (한 시전 = `castsPerTurn` 중 1회)

```
┌─ Step A. Resolve sequence ───────────────────────────────┐
│  1. Weapon.slots[] 를 인덱스 순회                         │
│  2. Projectile 슬롯의 SpecHitInstance(kind=MOVING)        │
│     → SnapshotPatch (HitSnapshot 빌드용 변형 누적기)      │
│  3. Modifier 슬롯들을 순서대로 SnapshotPatch + behaviors  │
│     리스트에 누적 (stat-delta / 곱셈 / IHitBehavior 추가) │
│  4. Trigger 슬롯이 있으면 → Trigger 컨텍스트 등록         │
│     없으면 → "always" Trigger 컨텍스트로 처리             │
│  5. Effect 슬롯의 SpecEffect.id 들을 EffectList에 수집    │
└──────────────────────────────────────────────────────────┘
                        │
                        ▼
┌─ Step B. Spawn HitInstance(s) ───────────────────────────┐
│  6. multiShot 만큼 HitLauncher.FireProjectile(...) 호출    │
│     (spreadAngle 으로 각 사본 발사각 계산)                │
│     → MovingHit 풀에서 인스턴스 dequeue                   │
│     → HitSnapshotBuilder.Build → SnapshotPatch 적용       │
│     → 모든 IHitBehavior 부착                              │
│  7. 각 HitInstance 에 Trigger + EffectList 컨텍스트 부여   │
│     (TriggerWatcher 가 OnHit/OnDespawn 등에 구독)         │
└──────────────────────────────────────────────────────────┘
                        │
                        ▼
┌─ Step C. Simulate ───────────────────────────────────────┐
│  8. HitInstance.Update / Movement 가 비행·반사·충돌 처리  │
│  9. 충돌·바운스·소멸 등 이벤트마다 TriggerWatcher 평가    │
│  10. Trigger 발화 시 → EffectFactory.Create(SpecEffect)   │
│      → IEffect 적용 (컨텍스트 = 이벤트)                   │
│  11. HitInstance 소멸 시 OnDespawn 발화 → 매칭 Trigger 평가│
│  12. DamagePipeline.Process(ctx) 는 RaiseHit 안에서 자동  │
└──────────────────────────────────────────────────────────┘
```

### 2.3 SnapshotPatch 의 stat 합성 규칙

Modifier가 N개 있을 때, 슬롯 순서대로 누적 적용:

```
damage   = base + Σ damageDelta  ; then  damage *= Π damageMul ; then max(damage, max(damageMin))
bounce   = base + Σ bounceDelta
pierce   = base + Σ pierceDelta
hitWidth = base * Π hitWidthMul
speed    = base * Π speedMul
element  = last non-NONE element (slot 순서로 후행 우선)
behavior = list (모든 비-NONE behavior 순서대로 적용)
```

- 합산 → 곱셈 순서 고정. 이걸 바꾸면 디자이너 직관이 깨진다.
- `damageMin` 은 모든 Modifier가 제시한 값 중 최댓값을 채택한다 (가장 보수적인 하한).
- `hitWidthMul` 등 곱셈 0배는 금지 (Modifier schema에서 검증).

### 2.4 Trigger 평가 시점

각 이벤트마다 Trigger 종류별 매칭 검사:

| Event during Step C            | Triggers that fire                                  |
|--------------------------------|-----------------------------------------------------|
| 발사체 → 벽돌 충돌              | `BRICK_HIT`, `NTH_BRICK_HIT`, `ELEMENT_MATCH`, `CONSECUTIVE_HIT` |
| 발사체 → 벽돌 충돌 + 파괴       | `BRICK_HIT` → `BRICK_KILL` (순서 고정)              |
| 발사체 → 벽 반사                | `WALL_BOUNCE`                                       |
| 발사체 바운스 카운터 = 0 시점   | `FULL_BOUNCE`                                       |
| 발사체 소멸                     | `PROJECTILE_DESPAWN`                                |
| 한 줄 모든 벽돌 파괴            | `LINE_CLEAR` (해당 행이 비워진 즉시)                |
| 시전 시작 시 위험 라인 근접 검사| `DANGER_PROXIMITY` (한 번만 평가, Step A에서)       |

- 하나의 이벤트에 여러 트리거 종류가 매칭하면 **schema에 등록된 순서**(Trigger 슬롯에 꽂힌 1개만)로
  평가. v0.1은 슬롯당 Trigger 1개라 충돌 없음.
- Trigger의 `cooldownTurn` / `maxFiresPerCast` 는 발화 후 카운터 갱신.

### 2.5 Effect 실행 컨텍스트

Trigger가 발화하면 EffectList 가 실행된다. 각 Effect는 다음 컨텍스트를 받는다:

```
EffectContext {
    Vector2 hitWorldPos      // 충돌 / 발화 위치
    Brick   targetBrick      // 충돌한 벽돌 (없으면 null)
    Projectile projectile    // 발화시킨 발사체
    int     bricksKilledThisCast  // 이번 시전에 누적 파괴 수
    eElement projectileElement
    // …
}
```

Effect는 컨텍스트만 보고 동작한다 — 글로벌 상태에 직접 의존하지 않음.

### 2.6 코드 매핑 — SpellSequence 와 6-레이어 컨트랙트

본 슬롯 평가는 코드 측 `SpellSequence` (신규, B-3 결정) 가 담당한다. 기존 `Skill` 은 **자율 시전**
(캐릭터 패시브, 자동 발화 어빌리티) 용도로 남는다 — Weapon 시전과는 분리된 경로.

```
[Player input: aim angle]
        │
        ▼
Weapon.Cast(angle)                                         ← 신규 런타임 클래스
        │
        ▼
SpellSequence.Build(weapon.slots) → SpellSequence.Use()    ← 신규. DamageActionBase의 사촌
        │   - SpecHitInstance (PROJECTILE 슬롯, kind=MOVING)
        │   - List<SpecModifier>
        │   - SpecTrigger? (선택)
        │   - List<SpecEffect>
        │
        ▼
HitLauncher.FireProjectile(attacker, damageSpec, origin, dir, target)
        │   - damageSpec.hitInstance == SpecHitInstance.id
        │   - HitSnapshotBuilder.Build → SnapshotPatch 적용
        │
        ▼
HitInstance (MovingHit)  ←  IHitBehavior[] 부착 (PenetrateBehavior, BounceBehavior, 등)
        │   - OnHit / OnDespawn / OnTickFrame 발화
        │
        ▼ (TriggerWatcher 가 매칭 시 발화)
EffectFactory.Create(SpecEffect[id])  →  IEffect 적용
        │
        ▼
DamagePipeline.Process(DamageInfo)  →  Brick.HP 차감
        │
        ▼
EffectHost.RaiseAfterDealDamage / RaiseAfterTakeDamage / RaiseKill
        │
        ▼ (Relic IEffect들이 hook 구독)
유물 효과 발화
```

**책임 분담**

| 시스템                | 담당                                                                   |
|-----------------------|------------------------------------------------------------------------|
| `Weapon` (런타임)     | 슬롯 보관, `castsPerTurn`/`cooldownTurns` 정책 적용, `SpellSequence` 빌드 |
| `SpellSequence` (런타임) | 슬롯 4종을 모아 한 번의 시전 단위로 평가. HitLauncher 호출. 풀링됨.    |
| `SnapshotPatch` (런타임) | Modifier 누적 결과를 HitSnapshot 필드에 적용하는 변형 함수 묶음.       |
| `TriggerWatcher` (런타임) | HitInstance 의 OnHit/OnDespawn/OnTickFrame 에 구독, SpecTrigger 조건 매칭 시 EffectList 발화. |
| `HitInstance` (기존)  | 비행 / 충돌 / 반사 / 수명 처리. `RaiseHit` 안에서 `DamagePipeline.Process` 호출. |
| `IHitBehavior` (기존) | Modifier가 추가하는 런타임 거동 (Penetrate/Bounce/Split/...).         |
| `EffectHost` (기존)   | Relic / Status Effect의 hook 발화 채널. ChainBall Trigger 와는 별 레이어. |

> 주의: ChainBall의 **Trigger** (Weapon 슬롯의 발동 조건) 와 코드의 **EffectHost hook** (DamagePipeline
> 단계별 훅) 은 *다른 레이어*다. 전자는 `HitInstance` 단위 이벤트, 후자는 `UnitController` 단위 이벤트.
> Relic은 EffectHost hook을 쓰고, Wand의 Trigger 슬롯은 HitInstance 이벤트를 쓴다. 자세한 매핑은
> `Docs/Systems/Spell.md` §8 와 `Docs/Systems/Relic.md` §2.

---

## 3. Damage pipeline

벽돌이 데미지를 받는 단일 파이프라인:

```
DealDamageToBrick(brick, dmg, source) {
    1. ApplyRelicHooks(PASSIVE_GLOBAL, dmg, source)   // 철갑탄 장전 +1
    2. ApplyRelicHooks(ON_HIT, dmg, source)            // 약점 포착, 근접 사격, …
    3. ApplyShieldResistance(dmg, brick)               // 실드 벽돌 속성 검사
    4. brick.hp -= dmg
    5. if brick.hp <= 0:
         OnBrickKilled(brick, source, overkillAmount)
         ApplyRelicHooks(ON_KILL, …)
         FireTriggers(BRICK_KILL, …)
       else:
         FireTriggers(BRICK_HIT, …)
}
```

- **Shield**: `eBrickType.SHIELD` 인 벽돌은 발사체 element와 일치할 때만 정상 데미지, 그 외에는 0.
  v0.1 단순 모델 — 향후 부분 저항 도입 가능.
- **Overkill**: 효과 `OVERKILL_TRANSFER` 가 등록되어 있을 때만 초과 데미지가 인접으로 전이.
- **Relic hook 순서**: `PASSIVE_GLOBAL` (스탯) → `ON_HIT` (조건부 가산) → 적용. 같은 hook 내 여러
  유물은 등록 순서.

---

## 4. Projectile lifetime

```
HitInstance state (MovingHit, kind=MOVING) {
    bouncesLeft : int    // SpecHitInstance.bounceCount + Σ bounceDelta - bouncesUsed
    pierceLeft  : int
    passLeft    : int    // 데미지 없이 통과 가능 횟수
    isAlive     : bool   // HitInstance.IsAlive
}
```

`bouncesLeft` 는 코드 측 `BounceBehavior._remaining` 으로 흡수 가능. `pierceLeft` 는 `PenetrateBehavior`
와 매칭. 즉 Modifier가 추가하는 IHitBehavior 인스턴스가 이 카운터들의 *런타임 권위*가 된다.

소멸 조건 (`isAlive = false`):
1. `bouncesLeft < 0` AND 그다음 충돌이 발생.
2. 화면 하단(플레이어 라인 아래) 이탈.
3. `STATIC` motion + 시퀀스 종료.
4. `behavior=PIERCE_ON_HIT` 가 아닌 한, 벽돌 충돌 시 `pierceLeft <= 0` 이면 통과 불가 → 반사 또는 정지.

소멸 시 `PROJECTILE_DESPAWN` 이벤트 발화 → Trigger 매칭 → Effect 실행. **이 단계에서 새 발사체가
생성될 수 있다** (예: 리코셰).

### 무한 재귀 방지

- 한 시전(turn) 내에서 spawn 가능한 발사체 총수에 cap을 둔다 (`MAX_PROJECTILES_PER_CAST`, 코드 상수,
  v0.1 권장값 64).
- cap 초과 시 추가 spawn은 무시 + 디버그 경고.

---

## 5. Reflection / motion details

| Motion       | Wall hit         | Brick hit                        | Despawn                    |
|--------------|------------------|----------------------------------|----------------------------|
| `REFLECT`    | 반사 (입사=반사) | 데미지 + 반사                     | bounce 소진 / 하단 이탈    |
| `STRAIGHT`   | 반사             | 데미지 + 직진                     | 화면 이탈                   |
| `HOMING`     | 반사 후 재유도   | 데미지 + 가장 가까운 벽돌 재유도  | bounce 소진 / 화면 이탈    |
| `CURVE`      | 반사             | 데미지 + 반사 (꺾임 1회 적용 후) | bounce 소진                |
| `STATIC`     | n/a              | n/a (충돌 판정 위치에 고정)        | 시퀀스 종료                |
| `FALLING`    | 반사 후 중력 가속 | 데미지 + 정지·낙하                | 하단 이탈                   |

- 반사각 계산은 standard 입사=반사. `randomAngle > 0` (스파크) 이면 반사 후 `±randomAngle°` 가산.
- `passThrough > 0` 인 Projectile은 벽돌과의 충돌 시 데미지를 줄 수 있는지 여부와 별개로 통과
  카운터를 소비하고 진행 (유령볼: 데미지 1 + 통과). `pierceCount` 와 의미가 다름:
  - `pierce`: 벽돌을 **파괴하고 직진**.
  - `passThrough`: 벽돌과 **충돌하지 않은 듯 통과** (데미지는 schema에 따라 적용 / 미적용).

---

## 6. Hooks & event bus

전투 중의 이벤트는 모두 `Handlers.Event` (CombatEvent 채널)을 통해 발화된다:

| Event                  | When                                 | Subscribers                       |
|------------------------|--------------------------------------|-----------------------------------|
| `CastStarted`          | TurnPhase.CAST 시작                   | UI, Relic(ON_BATTLE_*)            |
| `ProjectileSpawned`    | 발사체 인스턴스 생성 직후              | VFX, Relic                        |
| `BrickHit`             | 충돌 직후 (데미지 적용 전)             | Trigger evaluator, Relic(ON_HIT)  |
| `BrickKilled`          | hp ≤ 0 확정 후                        | Drop spawner, Relic(ON_KILL)      |
| `WallBounced`          | 벽 반사 직후                          | Trigger, Relic(ON_BOUNCE)         |
| `ProjectileDespawned`  | 소멸 처리 직후                         | Trigger, Relic                    |
| `LineCleared`          | 한 줄 비워졌을 때                      | Trigger, UI                       |
| `TurnEnded`            | TurnPhase.UPKEEP 끝                  | Relic(ON_TURN_END)                |
| `DamageTaken`          | TurnPhase.DAMAGE 적용                 | Relic(ON_DAMAGE_TAKEN), UI         |

이벤트 발화 순서는 **단일 스레드 동기**. 한 핸들러가 처리 중 다른 이벤트를 발화하면 큐에 enqueue,
현재 핸들러 종료 후 처리 (재진입 방지).

---

## 7. Open questions (구현 단계에서 정리)

1. ~~시간 감속 시뮬레이션 단위~~ → **해소**: GDD Modifier `시간 감속` 의 "발사체 속도 50% 감소"는
   `HitSnapshot.Speed` 에 단순 곱셈 패치 (Spell.md §7). 글로벌 슬로우모션은 v0.1 미사용 — 향후
   필요해지면 `Handlers.Time` 에 `SetTimeScale(float)` + `Handlers.Time.DeltaTime` 도입.
2. ~~Projectile pooling 분리~~ → **해소**: 코드의 `MovingHit / InstantHit / AuraHit` 3개 풀이 이미
   `PoolMonoBehaviour<T>` 의 separate pool로 동작. `AddressFormat = "HitInstance_{0}"` 가 SpecHitInstance.id
   를 직접 키로 사용 (kind와 무관하게 같은 키 공간).
3. ~~화상 / 동결 상태이상 데이터 모델~~ → **해소**: `IEffect` 구현체 (`BurnEffect`, `FreezeEffect`)
   로 표현하고 `brick.Data.Effects.Add(effect)` 적용. UnitCombatDesign §5.6 패턴 그대로 (Brick.md §1).
4. ~~Trigger castsPerTurn=2 시 카운터~~ → **해소**: 각 시전마다 리셋 (현행 모델 채택).
5. `EffectHost.RaiseOnFireHit` 가 ChainBall에서 의미 있게 사용되는가 — Player 캐릭터에 EffectHost 가
   존재한다면 Relic을 IEffect 묶음으로 이 host에 등록하는 모델로 자연스럽게 흡수. (Phase 7에서 결정)

---

## 변경 절차

본 문서의 규칙을 변경하려면 `/arch-update` 흐름. 코드는 본 문서를 따라 구현되며, 본 문서가 모호하면
구현하지 않는다 (먼저 문서를 박아둔다).
