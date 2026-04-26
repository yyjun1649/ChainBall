# Schema — `SpecWeapon` (Spec class: `SpecWeapon`)

> 무기는 캐릭터와 독립된 공용 장비. 슬롯 수와 시전 정책으로 차별화된다.
> 슬롯에 어떤 스펠을 꽂는지는 런타임 빌드(저장 데이터)이며, 본 spec에는 **빈 무기 정의**만 들어간다.
>
> v0.1의 무기는 모두 **지팡이 형태**이지만, 식별자는 일반화된 `Weapon` 을 쓴다 (캐릭터 추가 시 다른 무기 카테고리 대비).

| Sheet        | Key column    | Generated class                       | JSON                       |
|--------------|---------------|---------------------------------------|----------------------------|
| `SpecWeapon` | `id` (string) | `SpecData.SpecWeapon` (`partial`)     | `Json/SpecWeapon.json`     |

---

## Columns

| Field             | Type             | Required | Description                                                            | Example              |
|-------------------|------------------|----------|------------------------------------------------------------------------|----------------------|
| `id`              | `string`         | ✅       | 고유 키.                                                               | `weapon_apprentice`  |
| `nameKey`         | `string`         | ✅       | 로컬라이즈.                                                            | `weapon.apprentice.name` |
| `descKey`         | `string`         | ✅       | 로컬라이즈.                                                            | `weapon.apprentice.desc` |
| `rarity`          | `enum:eRarity`   | ✅       | `COMMON / UNCOMMON / RARE`.                                            | `COMMON`             |
| `slotCount`       | `int`            | ✅       | 슬롯 수. 수습 = `4`, 연발 = `3`, 대마도 = `5`.                         | `4`                  |
| `castsPerTurn`    | `int`            | ✅       | 턴당 시전 횟수. 연발 = `2`.                                            | `1`                  |
| `cooldownTurns`   | `int`            | ✅       | 시전 후 N턴 쿨다운. 대마도 = `1`. `0` = 없음.                          | `0`                  |
| `slotShape`       | `enum[]:eSlotKind` | ✅     | 슬롯별 허용 카테고리 (배열). 길이 = `slotCount`.                       | `TRIGGER/PROJECTILE/MODIFIER/EFFECT` |
| `multiAngle`      | `bool`           | ✅       | `castsPerTurn > 1` 일 때 각 시전을 다른 각도로 조준 가능 여부.         | `false`              |
| `iconKey`         | `string`         | ⭕       | UI 아이콘 Resource 키.                                                  | `ui/weapon/apprentice` |

---

## `eSlotKind` (enum)

각 슬롯이 받을 수 있는 스펠 카테고리.

| Value         | Accepts                                      |
|---------------|----------------------------------------------|
| `ANY`         | 모든 카테고리 허용.                            |
| `TRIGGER`     | Trigger 카테고리만.                           |
| `PROJECTILE`  | Projectile 카테고리만 (= `THitInstance.kind=MOVING`). |
| `MODIFIER`    | Modifier 카테고리만.                           |
| `EFFECT`      | Effect 카테고리만.                            |

`slotShape` 는 슬롯 순서를 강제한다. 예: `TRIGGER/PROJECTILE/MODIFIER/MODIFIER/EFFECT` 는
GDD 6.2 의 표준 시퀀스. v0.1에서는 모든 무기가 이 표준을 따라도 되지만, 향후 기믹 무기
(예: "PROJECTILE 슬롯 2개")를 위해 컬럼으로 분리한다.

---

## Slot constraint validation

런타임에서 슬롯에 스펠을 꽂을 때:

1. 해당 슬롯의 `eSlotKind` 와 스펠의 카테고리(`ProjectileSpec` / `ModifierSpec` / `TriggerSpec` / `EffectSpec`)가
   호환되는지 검증.
2. **첫 PROJECTILE 슬롯은 비어 있을 수 없다** (무기당 최소 1개 발사체 필수, GDD 7.1).
3. Modifier 슬롯이 비어 있어도 시퀀스는 유효 (Projectile만 발사).
4. Trigger 슬롯이 비어 있으면 → 시퀀스 끝에 Effect들이 자동 발화 (Trigger.md 참조).

자세한 시퀀스 평가 규칙은 `Docs/Systems/Weapon.md` 와 `Docs/Systems/Combat.md`.

---

## Authoring checklist

```
[ ] id 는 snake_case 고유키
[ ] slotShape 의 길이가 slotCount 와 일치
[ ] castsPerTurn > 1 인 무기가 multiAngle=true 면 UI 가 다중 조준 지원해야 함
[ ] cooldownTurns > 0 인 무기는 빈 턴 보상 메커니즘 검토 (GDD 6.3)
```
