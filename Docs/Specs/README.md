# Specs — SpecData Pipeline (ChainBall)

> **이 문서는 데이터 흐름과 계약을 설명한다. 실제 숫자는 xlsx에 있고, JSON과 `.g.cs`는 자동 생성물이다.**
> Claude는 xlsx를 직접 편집하지 않는다 (Excel 바이너리). 대신 `Schema/*.md` 의 컬럼 정의를 보고
> 디자이너에게 변경 요청을 내거나, 사용자가 Editor에서 Rebuild를 돌린 후의 JSON / `.g.cs` 결과를 참조한다.

---

## 1. Pipeline

```
Assets/@Project/Scripts/SpecData/Xlsx/Spec.xlsx        (designer source, version-controlled)
                       │
                       ▼  Tools > SpecData > Rebuild All        (Editor menu, user-triggered)
                       │
   ┌───────────────────┼───────────────────────────────────┐
   ▼                                                       ▼
Assets/@Project/Scripts/SpecData/Generated/Spec*.g.cs   Assets/@Project/Scripts/SpecData/Json/Spec*.json
(compiled into Assembly-CSharp)                         (Addressable: "SpecData/Spec*")
                                                                │
                                                                ▼  [Runtime, BeforeSceneLoad]
                                                       SpecDataManager.SpecXxx.Get(key)
```

| Stage     | Path                                                       | Owner               | Editable by Claude?    |
|-----------|------------------------------------------------------------|---------------------|------------------------|
| Source    | `Assets/@Project/Scripts/SpecData/Xlsx/Spec.xlsx`          | Designer            | ❌ (Excel binary)      |
| Codegen   | `Assets/@Project/Scripts/SpecData/Generated/*.g.cs`        | Pipeline (auto)     | ❌ (regenerated)       |
| Data      | `Assets/@Project/Scripts/SpecData/Json/*.json`             | Pipeline (auto)     | ❌ (regenerated)       |
| Extension | `Assets/@Project/Scripts/SpecData/Partial/Spec*.cs`        | Hand-written        | ✅                     |
| Wiring    | `Assets/@Project/Scripts/SpecData/Partial/SpecDataManager.Tables.cs` | Hand-written | ✅           |

Pipeline implementation lives in `Assets/@Library/Script/SpecData/`. Settings asset:
`Assets/@Project/Scripts/Data/SpecData/SpecDataSettings.asset`.

Deeper engine guide: [`@Library/Script/SpecData/README.md`](../../Assets/@Library/Script/SpecData/README.md).

---

## 2. Spreadsheet conventions

### Sheet name prefix

| Prefix     | Role                       | Output                                                                |
|------------|----------------------------|-----------------------------------------------------------------------|
| `#Menu`    | Designer index             | Ignored                                                               |
| `#enum`    | Enum source                | `Generated/Enums.g.cs`                                                |
| `#` (other)| Meta sheet                 | Ignored                                                               |
| `T*`       | Data table                 | `Generated/T*.g.cs` (class `Spec*`) + `Json/T*.json`                  |

### Data table row layout (1-based)

| Row  | Content                                                                  |
|------|--------------------------------------------------------------------------|
| 1    | `#Menu` / Korean comment — ignored                                       |
| 2    | **Field name** (column header). `#`-prefixed → dev-only, skipped         |
| 3    | **Field type** (see below). `#`-prefixed → dev-only, skipped             |
| 4+   | Data rows. First cell = `IGNORE_ROW` → skip                              |

### Supported field types

```
int, long, float, double, bool, string
int[], string[], …                    (arrays — cell-internal delimiter '/')
enum:eXxx                             (enum — defined on the #enum sheet)
enum[]:eXxx                           (enum array)
```

Array delimiter is `/` (defined in `RowParser.ARRAY_DELIM`).

### `#enum` sheet
Row 2 lists enum column pairs: `[eXxx, value:eXxx, (#desc), eYyy, value:eYyy, …]`.
Row 3+ holds the actual `key, value` rows. Enum types are emitted to `Generated/Enums.g.cs`.

---

## 3. Runtime usage

```csharp
using SpecData;

// Single lookup (key is the table's id column)
if (SpecDataManager.SpecAttack.TryGet("magic_ball", out var atk))
    Debug.Log($"{atk.id} dmg={atk.baseDamage}");

// Iterate all
foreach (var skill in SpecDataManager.SpecSkill.All)
    if (skill.cooldown <= 0f) { /* … */ }
```

- Tables are loaded once at `BeforeSceneLoad` (`SpecDataManager.LoadAll()`) via Addressable
  key `SpecData/Spec*`.
- All loaded specs are **immutable**. Do not write to fields outside dev tools.
- Every spec class is `partial`. Add helper methods (computed properties, predicates) under
  `SpecData/Partial/Spec{Name}.cs`. **Do not edit `Generated/*.g.cs`.**

---

## 4. Adding a new table

```
[ ] Add T{Name} sheet to Spec.xlsx (follow row layout above)
[ ] Define column types correctly in row 3 (use enum:eXxx for enums)
[ ] (Editor) Tools > SpecData > Rebuild All
[ ] Verify Generated/T{Name}.g.cs has expected fields
[ ] Add Table<TKey, Spec{Name}> property + LoadAddressable line in
    SpecData/Partial/SpecDataManager.Tables.cs
[ ] Add Schema/{name}.md describing columns and allowed values
[ ] (Optional) Add SpecData/Partial/Spec{Name}.cs for helper methods
```

---

## 5. Spec tables for ChainBall

ChainBall은 코드의 6-레이어 컨트랙트(`UnitCombatDesign.md`) 위에 GDD 도메인을 *얹는다*. 다음이 권위.

| Table          | Schema doc                                       | Key column   | Purpose                                                 | 코드 매핑 |
|----------------|--------------------------------------------------|--------------|---------------------------------------------------------|-----------|
| `THitInstance` | [`Schema/hit_instance.md`](Schema/hit_instance.md) | `id` (int)    | 공격 인스턴스 (MOVING / INSTANT / AURA 통합)             | `SpecHitInstance` (코드 더미를 본 schema로 확장) |
| `TModifier`    | [`Schema/modifier.md`](Schema/modifier.md)       | `id` (string) | 발사체 변형 — 가속, 분열, 관통, …                          | HitSnapshot 패치 + `IHitBehavior` 부착 |
| `TTrigger`     | [`Schema/trigger.md`](Schema/trigger.md)         | `id` (string) | 발동 조건 — 온히트, N번째 충돌, 라인 클리어, …             | `HitInstance.OnHit / OnDespawn / OnTickFrame` 구독 |
| `TEffect`      | [`Schema/effect.md`](Schema/effect.md)           | `id` (int)    | 효과 — 폭발, 화상, 연쇄 번개, …                           | `SpecEffect` + `IEffect` + `EffectFactory` (확장) |
| `TRelic`       | [`Schema/relic.md`](Schema/relic.md)             | `id` (string) | 유물 — 철갑탄 장전, 처형자, 탄약 공장, …                   | `IEffect` 묶음 (Augment 패턴, UnitCombatDesign §5.4) |
| `TWeapon`      | [`Schema/weapon.md`](Schema/weapon.md)           | `id` (string) | 무기 — 슬롯 수, 시전 정책                                  | (신규) `Weapon` 런타임 + `SpellSequence` 평가기 |
| `TCharacter`   | [`Schema/character.md`](Schema/character.md)     | `id` (string) | 캐릭터 — 패시브, 전용 스펠 풀                              | `SpecCharacter` (확장) |
| `TWave`        | [`Schema/wave.md`](Schema/wave.md)               | `id` (int)    | 웨이브 줄 생성 패턴 (보스 패턴 enum 분기 포함)              | (신규) `SpecWave` + `BrickPatternParser` + `BossPatternRunner` |

### 코드 spec과의 정합화 결정 (확정)

| 코드 spec        | 결정                                                                                       |
|------------------|--------------------------------------------------------------------------------------------|
| `SpecHitInstance`| **확장**. 본 `THitInstance` schema가 권위. 종류 enum(`kind`) 추가, ChainBall 도메인 필드 흡수. |
| `SpecAttack`     | v0.1에서 ChainBall은 사용하지 않음 (캐릭터 자율 공격 모듈이 없으므로). 코드는 유지하되 표는 빈 채로. |
| `SpecSkill`      | **유지**. *자율 시전* (캐릭터 패시브/자동 발화) 용도로 남김. Weapon 시전은 SpellSequence 사용. |
| `SpecEffect`     | **확장**. 본 `TEffect` schema가 권위. 도메인 효과(폭발, 화상, 연쇄 번개 등)에 맞는 IEffect 구현체 추가. |
| `SpecCharacter`  | **확장**. ChainBall 도메인 필드(passiveId, exclusivePool*, unlockPhase 등) 추가.            |
| `SpecLocalize`   | 그대로 사용.                                                                                |

---

## 6. Hard rules

- **Numbers go through xlsx, not code.** No `const float DAMAGE = 1f;` for tunable values.
- **Do not hand-edit `Json/*.json`.** It is overwritten on rebuild.
- **Do not edit `Generated/*.g.cs`.** Same reason.
- **No new ScriptableObject for tunable data.** See `ARCHITECTURE.md` §3.
- **Schema doc and xlsx must agree.** A column added to the xlsx without updating
  `Schema/*.md` is a bug. A column in `Schema/*.md` not in the xlsx is a bug.
