# Schema — `SpecCharacter` (Spec class: `SpecCharacter`)

> 캐릭터는 등장 스펠 풀과 패시브를 결정한다. 지팡이는 공용 장비라 본 spec에 포함되지 않는다.

| Sheet           | Key column    | Generated class                          | JSON                        |
|-----------------|---------------|------------------------------------------|-----------------------------|
| `SpecCharacter` | `id` (string) | `SpecData.SpecCharacter` (`partial`)     | `Json/SpecCharacter.json`   |

> 주의: 이미 `Generated/SpecCharacter.g.cs` 가 존재한다 (현 더미). 본 schema는 ChainBall 도메인을 반영하는
> 새 정의이며, 기존과의 정합화는 구현 단계에서 결정.

---

## Columns

| Field              | Type             | Required | Description                                                           | Example          |
|--------------------|------------------|----------|-----------------------------------------------------------------------|------------------|
| `id`               | `string`         | ✅       | 고유 키.                                                              | `gunslinger`     |
| `nameKey`          | `string`         | ✅       | 로컬라이즈.                                                           | `char.gunslinger.name` |
| `descKey`          | `string`         | ✅       | 로컬라이즈.                                                           | `char.gunslinger.desc` |
| `passiveId`        | `string`         | ✅       | 캐릭터 패시브 효과 (`TRelic.id` 와 동일 형식, 또는 별도 테이블 — v0.1은 `TRelic` 재사용). | `passive_precision_shot` |
| `commonPool`       | `string[]`       | ✅       | 공용 스펠 풀 사용 여부 + 제외 / 포함 리스트. 보통 `["*"]` (전체 사용).  | `*`              |
| `exclusivePool0`   | `string[]`       | ✅       | Phase 0 (초기) 전용 스펠 `id` 리스트. `/` 구분.                       | `proj_double_shot/proj_cannonball/mod_amp/...` |
| `exclusivePool1`   | `string[]`       | ⭕       | Phase 1 (Trajectory) 전용 풀. `/` 구분. 비어있으면 미해금.            | `proj_curve_ball/...` |
| `exclusivePool2`   | `string[]`       | ⭕       | Phase 2 (Multi-shot) 전용 풀.                                          | `proj_burst_shot/...` |
| `unlockPhase1`     | `enum:eUnlockCondition` | ✅ | Phase 1 해금 조건. 아래 enum 참조.                                | `FIRST_CLEAR`    |
| `unlockPhase1Param`| `int`            | ✅       | 해금 조건 파라미터. `FIRST_CLEAR` 면 `0`, `ELITE_KILLS_GE` 면 N.      | `0`              |
| `unlockPhase2`     | `enum:eUnlockCondition` | ✅ | Phase 2 해금 조건.                                                | `ELITE_KILLS_GE` |
| `unlockPhase2Param`| `int`            | ✅       |                                                                       | `5`              |
| `startWeaponId`    | `string`         | ✅       | 시작 무기 (`TWeapon.id`).                                             | `weapon_apprentice` |
| `startHp`          | `int`            | ✅       | 시작 HP.                                                              | `30`             |
| `iconKey`          | `string`         | ⭕       | UI 아이콘 Resource 키.                                                 | `ui/char/gunslinger` |

---

## `eUnlockCondition` (enum)

| Value             | Description                                                              |
|-------------------|--------------------------------------------------------------------------|
| `ALWAYS`          | 시작부터 해금.                                                            |
| `FIRST_CLEAR`     | 해당 캐릭터로 첫 1런 클리어.                                              |
| `ELITE_KILLS_GE`  | 누적 엘리트 처치 ≥ `param`.                                              |
| `BOSS_KILLS_GE`   | 누적 보스 처치 ≥ `param`.                                                |
| `RUN_COUNT_GE`    | 누적 런 수 ≥ `param`.                                                    |

---

## 스펠 풀 구성

캐릭터의 가능한 스펠 풀 = `commonPool` + `exclusivePool0` + (해금된 phase의 풀) … 의 합집합.

- `commonPool = ["*"]` 는 공용 풀(`Common`/`Uncommon`/`Rare` 모든 스펠)을 다 포함한다는 약속.
- 캐릭터가 일부 공용 스펠을 *제외*해야 하면 `["*", "-spell_id"]` 형태(부정 표기) 검토 — v0.1은 단순화하여 `["*"]` 만 허용.
- Phase별 풀은 누적 (Phase 2 해금되면 Phase 0 + 1 + 2 전부 사용 가능).

---

## Authoring checklist

```
[ ] id 는 snake_case 고유키
[ ] passiveId 가 TRelic 또는 전용 패시브 테이블에 존재
[ ] startWeaponId 가 TWeapon 에 존재
[ ] exclusivePool* 의 모든 id가 THitInstance(kind=MOVING) / TModifier / TTrigger / TEffect 중 어디든 존재
[ ] unlockPhaseN 조건이 명확한 트래킹 데이터(저장)와 매핑됨
```
