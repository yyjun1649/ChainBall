# Schema — `SpecModifier` (Spec class: `SpecModifier`)

> Modifier는 **바로 앞의 Projectile** (또는 앞선 Modifier가 적용된 결과)을 변형한다.
> 슬롯 내 순서가 의미를 가진다 — 평가 모델은 `Docs/Systems/Combat.md` §시전 시퀀스 평가.

| Sheet          | Key column    | Generated class                        | JSON                       |
|----------------|---------------|----------------------------------------|----------------------------|
| `SpecModifier` | `id` (string) | `SpecData.SpecModifier` (`partial`)    | `Json/SpecModifier.json`   |

---

## Columns

Modifier는 본질적으로 "스탯을 변형하거나 동작을 추가"하는 오퍼레이터다. 한 행에 모든 가능한 변형을
담는 대신, 명확한 stat-delta + 동작 enum 형태로 설계한다.

| Field             | Type            | Required | Description                                                              | Example      |
|-------------------|-----------------|----------|--------------------------------------------------------------------------|--------------|
| `id`              | `string`        | ✅       | 고유 키.                                                                 | `accelerate` |
| `nameKey`         | `string`        | ✅       | 로컬라이즈.                                                              | `mod.accelerate.name` |
| `descKey`         | `string`        | ✅       | 로컬라이즈.                                                              | `mod.accelerate.desc` |
| `rarity`          | `enum:eRarity`  | ✅       | `COMMON / UNCOMMON / RARE`                                               | `COMMON`     |
| `damageDelta`     | `int`           | ✅       | 데미지 가산 (음수 가능). `0` = 영향 없음. 강화탄 = `+2`.                 | `0`          |
| `damageMul`       | `float`         | ✅       | 데미지 배수. `1.0` = 영향 없음. 집중 = `3.0`.                            | `1.0`        |
| `damageMin`       | `int`           | ✅       | 데미지 하한 (음수 가산 후 클램프). 보통 `1`.                             | `1`          |
| `bounceDelta`     | `int`           | ✅       | 바운스 횟수 가산 (음수 가능). 가속 = `+3`.                               | `0`          |
| `pierceDelta`     | `int`           | ✅       | 관통 카운트 가산.                                                        | `0`          |
| `hitWidthMul`     | `float`         | ✅       | 충돌 판정 폭 배수. 대형화 = `2.0`, 미니화 = `0.5`.                       | `1.0`        |
| `speedMul`        | `float`         | ✅       | 속도 배수. 시간 감속 = `0.5`.                                            | `1.0`        |
| `behavior`        | `enum:eModifierBehavior` | ✅ | 단순 stat-delta로 표현 안 되는 동작. 아래 enum 참조.                | `NONE`       |
| `behaviorParam1`  | `int`           | ✅       | behavior 보조 파라미터 1. 분열 갈래 수 등.                               | `0`          |
| `behaviorParam2`  | `int`           | ✅       | behavior 보조 파라미터 2.                                                | `0`          |
| `behaviorParamF`  | `float`         | ✅       | behavior 보조 파라미터 (실수).                                            | `0`          |
| `element`         | `enum:eElement` | ✅       | 발사체에 부여할 속성. `NONE / FIRE / ICE / SHOCK`.                       | `NONE`       |
| `cooldownTurn`    | `int`           | ✅       | 적용 후 N턴 쿨다운. 보통 `0`.                                            | `0`          |

---

## `eModifierBehavior` (enum)

`NONE` 외에는 **순수 stat-delta로 표현 불가능한 동작**만 등록한다. Stat 변형은 위의 컬럼으로 표현.

| Value          | Meaning                                                                                  |
|----------------|------------------------------------------------------------------------------------------|
| `NONE`         | 추가 동작 없음 (stat-delta만 적용).                                                       |
| `SPLIT`        | 첫 벽돌 충돌 시 `behaviorParam1` 갈래로 복제. 각 사본 바운스 = `behaviorParam2`.          |
| `PIERCE_ON_HIT`| 벽돌 충돌 시 파괴 후 직진 계속 (관통). `pierceDelta` 와 별개로 무한 관통 모드.            |
| `CHAIN`        | 벽돌 충돌 시 인접 벽돌로 자동 유도, 최대 `behaviorParam1` 회.                             |
| `CLONE_AT_FIRE`| 발사 시 동일 발사체 1개 추가 (약간 다른 각도, `behaviorParamF` 도).                       |
| `FREEZE_ROW`   | 충돌 벽돌 다음 턴 하강 안 함 (시간 감속 보조 효과).                                       |

새 behavior를 추가하기 전에 먼저 stat-delta 컬럼으로 표현 가능한지 검토할 것.

---

## Stacking & evaluation

Modifier 슬롯에 여러 개가 배치되면 **선두에서 후미 순서로 누적 적용**된다:

```
Projectile(base) → Modifier[0] applied → Modifier[1] applied → … → Effect(s)
```

- `damageDelta` / `bounceDelta` / `pierceDelta` 는 **합산**.
- `damageMul` / `hitWidthMul` / `speedMul` 는 **곱셈** (`mul1 * mul2 * …`).
- `damageMin` 은 **최댓값**을 사용 (가장 보수적).
- `element` 는 **마지막 비-NONE 값**이 우선 (덮어쓰기). 다중 속성 동시 부여는 v0.1에서 지원하지 않음.
- `behavior` 가 NONE이 아닌 Modifier가 둘 이상이면 **순서대로 모두 적용** (예: SPLIT → CHAIN 가능).

자세한 평가 모델은 `Docs/Systems/Combat.md` 와 `Docs/Systems/Spell.md`.

---

## Authoring checklist

```
[ ] id 는 snake_case 고유키
[ ] 단순 변형이면 behavior=NONE 으로 두고 stat-delta 컬럼만 사용
[ ] behavior 사용 시 behaviorParam1/2/F 의미를 본 표에 추가했는지
[ ] damageMin 은 음수 데미지 변환 후 의미가 통하는지 (보통 1)
[ ] element 부여 시 발사체 속성과 실드 벡터의 상성 검토 (Brick.md 참조)
```
