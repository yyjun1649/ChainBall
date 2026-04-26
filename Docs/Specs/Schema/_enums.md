# Schema — `#enum` Sheet (Master Reference)

> `Spec.xlsx` 의 `#enum` 시트에 들어갈 모든 enum 정의의 **단일 권위 문서**.
> 각 enum의 `key, value` 쌍을 그대로 시트에 붙여넣을 수 있도록 정수값까지 확정해둔다.
>
> Claude는 xlsx를 직접 편집하지 않는다 → 디자이너가 본 표를 보고 시트를 채우고
> `Tools > SpecData > Rebuild All` 실행 후 `Generated/Enums.g.cs` 가 본 표와 일치하는지 확인.

---

## `#enum` 시트 컬럼 레이아웃 (재확인)

`Docs/Specs/README.md` §2 참조. 한 enum은 2개 컬럼 페어로 표현:

| Row | Col A             | Col B             | (Col C 선택) |
|-----|-------------------|-------------------|--------------|
| 1   | `#Menu` 또는 주석 | (무시)            | `#desc`      |
| 2   | `eXxx`            | `value:eXxx`      | `#desc`      |
| 3+  | enum key (식별자) | 정수 (long)       | 설명 텍스트  |

`EnumParser` 는 row 2에서 `eXxx` + `value:eXxx` 페어를 자동 인식 → row 3 이상에서 `key, value` 추출.

---

## 정수값 배정 원칙

- **0 = NONE / 기본값** (해당 enum에 NONE 의미가 있는 경우).
- **1부터 의미 순서대로 증가** — 디자이너 가독성 우선, 카테고리 점프 없음.
- 추가 enum 값은 **마지막에 append** (기존 값 재배정 금지 — JSON/시트 데이터 깨짐).

---

## 1. `eHitInstanceKind`

`THitInstance.kind` 컬럼. 코드 풀(`MovingHit/InstantHit/AuraHit`) 분기. `Schema/hit_instance.md` §`eHitInstanceKind`.

| key       | value | desc |
|-----------|-------|------|
| `MOVING`  | 1     | 투사체 (`MovingHit` 풀) |
| `INSTANT` | 2     | 즉발 범위 (`InstantHit` 풀) |
| `AURA`    | 3     | 지속 영역 (`AuraHit` 풀) |

> NONE 값 없음 — 모든 HitInstance 행은 반드시 kind를 가진다.

---

## 2. `eProjectileMotion`

`THitInstance.motion` 컬럼. `kind=MOVING` 전용. 그 외 kind는 `STRAIGHT` 으로 채워두고 무시.

| key        | value | desc |
|------------|-------|------|
| `STRAIGHT` | 0     | 직선. 반사 없음 (기본값) |
| `REFLECT`  | 1     | 표준 반사 (`bounceCount` 사용) |
| `HOMING`   | 2     | 가장 가까운 벽돌로 유도 |
| `CURVE`    | 3     | 발사 후 1회 꺾임 |
| `STATIC`   | 4     | 발사 위치에 고정 |
| `FALLING`  | 5     | 충돌 시 정지·낙하 (헤비볼) |

---

## 3. `eRarity`

여러 테이블 공통 (`THitInstance`, `TModifier`, `TTrigger`, `TEffect`, `TRelic`, `TWeapon`).

| key        | value | desc |
|------------|-------|------|
| `COMMON`   | 1     |  |
| `UNCOMMON` | 2     |  |
| `RARE`     | 3     |  |

> v0.1은 3티어. EPIC / LEGENDARY 추가 시 `4 / 5` 로 append.

---

## 4. `eElement`

`TModifier.element`, `TEffect.element`, `TTrigger.elementMatch`, `TWave.pattern` 의 `S(F)` 등.

| key     | value | desc |
|---------|-------|------|
| `NONE`  | 0     | 무속성 (기본값) |
| `FIRE`  | 1     | 화염. SHIELD(F) 와 상성 |
| `ICE`   | 2     | 빙결. SHIELD(I) 와 상성 |
| `SHOCK` | 3     | 전격. SHIELD(E) 와 상성 |

> `Schema/wave.md` 의 `S(F)/S(I)/S(E)` 표기와 일치.

---

## 5. `eModifierBehavior`

`TModifier.behavior`. `Schema/modifier.md` §`eModifierBehavior`.

| key             | value | desc |
|-----------------|-------|------|
| `NONE`          | 0     | stat-delta 만 적용 (기본값) |
| `SPLIT`         | 1     | 첫 충돌 시 `behaviorParam1` 갈래로 복제 |
| `PIERCE_ON_HIT` | 2     | 충돌 후 직진 계속 (무한 관통) |
| `CHAIN`         | 3     | 인접 벽돌로 자동 유도 (`behaviorParam1` 회) |
| `CLONE_AT_FIRE` | 4     | 발사 시 동일 발사체 1개 추가 (`behaviorParamF` 도) |
| `FREEZE_ROW`    | 5     | 충돌 벽돌 다음 턴 하강 정지 |

---

## 6. `eTriggerEvent`

`TTrigger.event`. `Schema/trigger.md` §`eTriggerEvent`.

| key                  | value | desc |
|----------------------|-------|------|
| `BRICK_HIT`          | 1     | 발사체가 벽돌 충돌 시마다 |
| `BRICK_KILL`         | 2     | 벽돌 파괴 시마다 |
| `WALL_BOUNCE`        | 3     | 좌/우/상단 벽 반사 시 |
| `PROJECTILE_DESPAWN` | 4     | 발사체 소멸 시 |
| `NTH_BRICK_HIT`      | 5     | `nthCount` 번째 충돌 시에만 |
| `ELEMENT_MATCH`      | 6     | 발사체↔벽돌 속성 일치 시 |
| `LINE_CLEAR`         | 7     | 한 줄 전체 파괴 시 |
| `DANGER_PROXIMITY`   | 8     | 위험 라인 근접 (`proximityRow`) |
| `CONSECUTIVE_HIT`    | 9     | 바운스 없이 N개 연속 적중 |
| `FULL_BOUNCE`        | 10    | 최대 바운스 소진 시 |

> NONE 값 없음 — Trigger 행은 반드시 event 지정.

---

## 7. `eEffectKind`

`TEffect.kind`. `Schema/effect.md` §`eEffectKind`.

| key                  | value | desc |
|----------------------|-------|------|
| `DAMAGE_DIRECT`      | 1     | 단일 직접 데미지 |
| `AOE_DAMAGE`         | 2     | 영역 데미지 (`radius`) |
| `LINE_DAMAGE`        | 3     | 가로 1줄 데미지 |
| `CHAIN_DAMAGE`       | 4     | 인접 `targetCount` 개로 전이 |
| `STATUS_BURN`        | 5     | 화상 (`duration`, `tickDamage`) |
| `STATUS_FREEZE`      | 6     | 동결 (`duration`) |
| `HEAL`               | 7     | 플레이어 HP 회복 |
| `GOLD_GAIN`          | 8     | 골드 추가 |
| `SPAWN_HIT_INSTANCE` | 9     | HitInstance 추가 생성 |
| `EXTRA_CAST`         | 10    | 다음 턴 시전 +N |
| `OVERKILL_TRANSFER`  | 11    | 초과 데미지 인접 전이 |
| `HALF_HP_REMOVE`     | 12    | 적중 벽돌 HP 절반 제거 |
| `GRAVITY_PULL`       | 13    | 주변 벽돌 1칸 중앙으로 당김 |
| `BURN_DETONATE`      | 14    | 화상 벽돌 적중 시 화상 소모 + damage |
| `EMPOWER_NEXT`       | 15    | 다음 턴 첫 발사체 데미지 +N |

---

## 8. `eRelicCategory`

`TRelic.category`. `Schema/relic.md` §`eRelicCategory`.

| key          | value | desc |
|--------------|-------|------|
| `DAMAGE`     | 1     | 데미지 증폭 |
| `BOUNCE`     | 2     | 바운스 / 궤적 |
| `MULTI_SHOT` | 3     | 멀티샷 / 분산 |
| `SURVIVAL`   | 4     | 생존 / HP |
| `ECONOMY`    | 5     | 골드 / 상점 |
| `UTILITY`    | 6     | 슬롯 확장, 선택지 증가 |
| `SYNERGY`    | 7     | 빌드 정의급 |

---

## 9. `eRelicHook`

`TRelic.hook`. `Schema/relic.md` §`eRelicHook`.

| key                     | value | desc |
|-------------------------|-------|------|
| `PASSIVE_GLOBAL`        | 1     | 전역 stat 수정 (자동) |
| `ON_HIT`                | 2     | 벽돌 충돌 직후 |
| `ON_KILL`               | 3     | 벽돌 파괴 시 |
| `ON_BOUNCE`             | 4     | 벽/벽돌 바운스 시 |
| `ON_BOUNCE_EXHAUSTED`   | 5     | 발사체 바운스 소진 시 |
| `ON_PROJECTILE_DESPAWN` | 6     | 발사체 소멸 시 |
| `ON_TURN_END`           | 7     | 턴 종료 시 |
| `ON_BATTLE_END`         | 8     | 전투 종료 시 |
| `ON_BATTLE_START`       | 9     | 전투 시작 시 |
| `ON_DAMAGE_TAKEN`       | 10    | 플레이어 피해 시 |
| `ON_FATAL`              | 11    | 치명타 직전 |
| `ON_BRICK_DESTROY`      | 12    | 벽돌 파괴 시 (확률 분기) |
| `ON_SHOP_OPEN`          | 13    | 상점 진입 시 |

---

## 10. `eRelicCondition`

`TRelic.condition`. `Schema/relic.md` §`eRelicCondition`.

| key                            | value | desc |
|--------------------------------|-------|------|
| `NONE`                         | 0     | 조건 없음 (기본값) |
| `BRICK_HP_EQ`                  | 1     | 충돌 벽돌 HP = `condParam1` |
| `BRICK_HP_LT_PCT`              | 2     | 충돌 벽돌 HP ≤ `condParam1`% |
| `WITHIN_DANGER_ROW`            | 3     | 위험 라인 `condParam1` 줄 이내 |
| `WALL_BOUNCE_LR`               | 4     | 좌우 벽 반사 시 |
| `SAME_BRICK_HITS_GE`           | 5     | 같은 벽돌 한 턴 ≥ `condParam1` 적중 |
| `PLAYER_HP_LT_PCT`             | 6     | 플레이어 HP ≤ `condParam1`% |
| `BRICKS_KILLED_THIS_CAST_GE`   | 7     | 이번 시전 파괴 ≥ `condParam1` |
| `NO_DAMAGE_TAKEN_PREV_BATTLE`  | 8     | 직전 전투 무피해 |
| `IN_ELITE_BATTLE`              | 9     | 엘리트 전투 중 |

---

## 11. `eSlotKind`

`TWeapon.slotShape` 의 배열 원소. `Schema/weapon.md` §`eSlotKind`.

| key          | value | desc |
|--------------|-------|------|
| `ANY`        | 0     | 모든 카테고리 허용 |
| `TRIGGER`    | 1     | Trigger 만 |
| `PROJECTILE` | 2     | Projectile 만 (= `kind=MOVING`) |
| `MODIFIER`   | 3     | Modifier 만 |
| `EFFECT`     | 4     | Effect 만 |

---

## 12. `eUnlockCondition`

`TCharacter.unlockPhase1/2`. `Schema/character.md` §`eUnlockCondition`.

| key               | value | desc |
|-------------------|-------|------|
| `ALWAYS`          | 0     | 시작부터 해금 (기본값) |
| `FIRST_CLEAR`     | 1     | 해당 캐릭터 첫 클리어 |
| `ELITE_KILLS_GE`  | 2     | 누적 엘리트 처치 ≥ param |
| `BOSS_KILLS_GE`   | 3     | 누적 보스 처치 ≥ param |
| `RUN_COUNT_GE`    | 4     | 누적 런 ≥ param |

---

## 13. `eBossPattern`

`TWave.bossPattern`. `Schema/wave.md` §`eBossPattern`. 코드 (`BossPatternRunner`) 진입점 분기.

| key                 | value | desc |
|---------------------|-------|------|
| `NONE`              | 0     | 보스 줄 아님 (기본값) |
| `BOSS_01_OPENING`   | 101   | `Boss01.Opening(field)` |
| `BOSS_01_TELEGRAPH` | 102   | `Boss01.Telegraph(field)` |
| `BOSS_01_RAGE`      | 103   | `Boss01.Rage(field)` |

> 보스마다 `100 단위 블록` 으로 점프 (Boss02 = 201~, Boss03 = 301~). 보스 추가 시 같은 컨벤션 유지.
> 본 12종 enum 명에는 13번이 포함되지 않지만, Tasks.md / Roadmap §Phase 1 산출물의 12종에는
> `eBossPattern` 이 명시되어 있어 본 문서에는 포함. (총 12종 — `eHitInstanceKind, eProjectileMotion,
> eRarity, eElement, eModifierBehavior, eTriggerEvent, eEffectKind, eRelicCategory, eRelicHook,
> eRelicCondition, eSlotKind, eUnlockCondition, eBossPattern` = 13개? 재확인 ↓)

---

## 카운트 재확인

Tasks.md / Roadmap §Phase 1 의 enum 명단:

```
eHitInstanceKind, eProjectileMotion, eRarity, eElement, eModifierBehavior,
eTriggerEvent, eEffectKind, eRelicCategory, eRelicHook, eRelicCondition,
eSlotKind, eUnlockCondition, eBossPattern
```

= **13종** (Roadmap 에 "12종" 으로 적혀있으나 실제 명단은 13개). 본 문서는 13종 모두 정의.
Tasks.md / Roadmap 의 "12종" 표현은 별도 PR로 정정 권장.

---

## 디자이너 작업 절차

```
[ ] Spec.xlsx 의 #enum 시트 열기
[ ] 본 문서 §1~13 각 enum을 시트에 컬럼 페어로 추가
    - row 2: [eXxx, value:eXxx, #desc]
    - row 3+: [key, value, desc]
[ ] Tools > SpecData > Rebuild All 실행
[ ] Generated/Enums.g.cs 가 13종 enum 모두 포함하는지 확인
    (eItem 외에 eHitInstanceKind, eProjectileMotion, ..., eBossPattern 13개 추가)
[ ] Console 에 SpecDataValidator 빨간 로그 0
```

---

## Cross-references

- `Docs/Specs/README.md` §2 — `#enum` 시트 컨벤션
- `Assets/@Library/Script/SpecData/EnumParser.cs` — 파서 구현
- `Assets/@Project/Scripts/SpecData/Generated/Enums.g.cs` — 생성 결과
- 각 enum의 의미는 해당 schema 문서 (`Schema/{table}.md`) 참조
