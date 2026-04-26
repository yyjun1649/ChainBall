# Schema — `SpecRelic` (Spec class: `SpecRelic`)

> 유물(Relic)은 패시브 효과. 스펠 조합이 전술이라면, 유물은 전략으로서 런 전체에 영향.
> Slay the Spire의 Relic과 동일한 역할.

| Sheet       | Key column    | Generated class                    | JSON                       |
|-------------|---------------|------------------------------------|----------------------------|
| `SpecRelic` | `id` (string) | `SpecData.SpecRelic` (`partial`)   | `Json/SpecRelic.json`      |

---

## Columns

| Field           | Type             | Required | Description                                                              | Example          |
|-----------------|------------------|----------|--------------------------------------------------------------------------|------------------|
| `id`            | `string`         | ✅       | 고유 키.                                                                 | `armor_pierce`   |
| `nameKey`       | `string`         | ✅       | 로컬라이즈.                                                              | `relic.armor_pierce.name` |
| `descKey`       | `string`         | ✅       | 로컬라이즈.                                                              | `relic.armor_pierce.desc` |
| `rarity`        | `enum:eRarity`   | ✅       | `COMMON / UNCOMMON / RARE`                                               | `COMMON`         |
| `category`      | `enum:eRelicCategory` | ✅  | 분류. 시너지 / 디스플레이용. 아래 enum 참조.                              | `DAMAGE`         |
| `hook`          | `enum:eRelicHook` | ✅      | 적용 시점. 아래 enum 참조.                                                | `ON_HIT`         |
| `condition`     | `enum:eRelicCondition` | ✅ | 조건부 발동. 아래 enum 참조. 무조건은 `NONE`.                             | `NONE`           |
| `condParam1`    | `int`            | ✅       | 조건 파라미터 1 (HP threshold percent, row distance, … 컨텍스트 의존).   | `0`              |
| `condParam2`    | `int`            | ✅       | 조건 파라미터 2.                                                         | `0`              |
| `damageDelta`   | `int`            | ✅       | 데미지 가산. 철갑탄 장전 = `+1`.                                         | `0`              |
| `damageMul`     | `float`          | ✅       | 데미지 배수. 궤적 잔상 적용 시 `2.0` (단, hook 조건 만족시).             | `1.0`            |
| `bounceDelta`   | `int`            | ✅       | 모든 발사체 바운스 +N. 탄성 코팅 = `+2`.                                 | `0`              |
| `extraCast`     | `int`            | ✅       | 턴당 시전 +N. 추가 탄약 = `+1`.                                          | `0`              |
| `extraCastDamageDelta` | `int`     | ✅       | 추가 시전된 발사체에 적용되는 데미지 가산 (보통 음수). 추가 탄약 = `-1`.| `0`              |
| `chance`        | `float`          | ✅       | 발동 확률 [0, 1]. 무한 반사 = `0.5`. `1.0` = 항상.                       | `1.0`            |
| `triggerEffectId` | `int`          | ⭕       | 발동 시 적용할 Effect의 `id` (비교적 복잡한 효과 위임). 없으면 `0`. (SpecEffect.id 가 int 라 동기) | `1001`           |
| `usesPerRun`    | `int`            | ✅       | 런 당 사용 횟수 제한. 최후의 발악 = `1`. `0` = 무제한.                   | `0`              |
| `synergyAxis`   | `string`         | ⭕       | 시너지 라벨. UI 분류용. 표 8.7 참조. 없으면 `""`.                        | `damage_bounce`  |
| `vfxKey`        | `string`         | ⭕       | 획득 / 발동 VFX.                                                         | `vfx/relic/get`  |

---

## `eRelicCategory` (enum)

| Value         | Meaning                                          |
|---------------|--------------------------------------------------|
| `DAMAGE`      | 데미지 증폭                                       |
| `BOUNCE`      | 바운스 / 궤적                                     |
| `MULTI_SHOT`  | 멀티샷 / 분산                                     |
| `SURVIVAL`    | 생존 / HP                                         |
| `ECONOMY`     | 골드 / 상점                                       |
| `UTILITY`     | 슬롯 확장, 선택지 증가 등                         |
| `SYNERGY`     | 빌드 정의급 (8.7) — 다중 축 결합                  |

---

## `eRelicHook` (enum)

유물 효과가 *언제* 발화하는지.

| Value             | Meaning                                                           |
|-------------------|-------------------------------------------------------------------|
| `PASSIVE_GLOBAL`  | 전역 stat 수정 (모든 발사체 데미지 +1 등). 매 발사 시 자동 적용.   |
| `ON_HIT`          | 벽돌 충돌 직후.                                                    |
| `ON_KILL`         | 벽돌 파괴 시.                                                     |
| `ON_BOUNCE`       | 벽 / 벽돌 바운스 시.                                               |
| `ON_BOUNCE_EXHAUSTED` | 발사체 바운스 소진 시 (무한 반사).                            |
| `ON_PROJECTILE_DESPAWN` | 발사체 소멸 시 (잔탄 소진).                                |
| `ON_TURN_END`     | 턴 종료 시.                                                        |
| `ON_BATTLE_END`   | 전투 종료 시 (응급 수리, 전투 명상).                                |
| `ON_BATTLE_START` | 전투 시작 시.                                                      |
| `ON_DAMAGE_TAKEN` | 플레이어 피해 받을 때 (보복 사격).                                  |
| `ON_FATAL`        | 치명타 피해 직전 (최후의 발악).                                     |
| `ON_BRICK_DESTROY` | 벽돌 파괴 시 (파편화 — 확률 분기).                                |
| `ON_SHOP_OPEN`    | 상점 진입 시 (감정사 가격 할인).                                    |

---

## `eRelicCondition` (enum)

발동 *조건* — 자주 등장하는 분기를 enum으로 표준화.

| Value                | Description                                                              |
|----------------------|--------------------------------------------------------------------------|
| `NONE`               | 조건 없음 (항상 적용).                                                    |
| `BRICK_HP_EQ`        | 충돌 벽돌 HP가 `condParam1` 일 때 (약점 포착: HP 1 → +2).                |
| `BRICK_HP_LT_PCT`    | 충돌 벽돌 HP가 최대치의 `condParam1`% 이하일 때 (처형자: 25%).            |
| `WITHIN_DANGER_ROW`  | 충돌 벽돌이 위험 라인 `condParam1` 줄 이내일 때 (근접 사격).              |
| `WALL_BOUNCE_LR`     | 좌우 벽 반사일 때 (벽면 강화).                                            |
| `SAME_BRICK_HITS_GE` | 같은 벽돌에 한 턴 내 `condParam1` 회 이상 적중 시 (누적 타격).            |
| `PLAYER_HP_LT_PCT`   | 플레이어 HP `condParam1`% 이하일 때 (위기 집중).                          |
| `BRICKS_KILLED_THIS_CAST_GE` | 이번 시전에 파괴한 벽돌 수 ≥ `condParam1` (속사 / 잔탄 소진).      |
| `NO_DAMAGE_TAKEN_PREV_BATTLE` | 직전 전투에서 피해 받지 않음 (전투 명상).                       |
| `IN_ELITE_BATTLE`    | 엘리트 전투 중 (약탈자).                                                  |

---

## 시너지 유물 (8.7) 모델링

시너지 유물은 단일 hook + condition으로 표현이 어려운 경우가 있다. 이 경우:

- **stat-delta 컬럼으로 표현 가능** → 그대로 사용 (대부분).
- **표현 불가** → `triggerEffectId` 로 Effect를 호출하거나, 새 hook / condition enum을 추가 (이건
  ARCHITECTURE.md / Combat.md에 사양 추가 후 진행).

예시:
- **연쇄 반응기** (오버킬 데미지 전이 시 전이된 벽돌에도 바운스 판정 발생) → 단순 stat-delta로 표현 불가.
  Effect `OVERKILL_TRANSFER` 의 동작을 확장하는 방식으로 처리. v0.1 schema에는 hook=`ON_KILL` +
  condition=`NONE` + `triggerEffectId`로 임시 표현하고, 구체 동작은 `Systems/Combat.md` 에서 설계.

---

## Authoring checklist

```
[ ] id 는 snake_case 고유키
[ ] hook + condition + condParam 의 조합이 GDD 표 8.x 의 효과를 정확히 표현하는지
[ ] PASSIVE_GLOBAL 은 stat-delta 컬럼만 사용 (chance / cond 모두 default)
[ ] usesPerRun 가 0 외의 값이면 RunState 가 카운터 추적
[ ] synergyAxis 는 8.7 표의 라벨과 일치
```
