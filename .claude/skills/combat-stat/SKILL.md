---
name: combat-stat
description: How to work with stat calculation in RogueLikeTemplate — TStatContainer, TStatModifier, Base stat registration, PercentAdd/PercentMul, Source-based removal. Use whenever the user touches stat values, modifiers, buffs, eStatType, eModifierType, or files under Assets/@Library/Script/Stat/ or Game/Unit/UnitData.cs. Enforces final = (ΣFlat) × (1 + ΣPercentAdd) × Π(1 + PercentMul) from UnitCombatDesign.md §4.
paths: Assets/@Library/Script/Stat/**/*.cs, Assets/@Project/Scripts/Game/Unit/UnitData.cs, Assets/@Project/Scripts/Define/SpecEnums.cs
---

# Combat — Stat (L2)

순수한 값 계산 레이어. 반응 로직은 `combat-effect`. **Phase 1 적용됨** — 유지보수 가이드.

## Source of truth

- **설계 문서**: `UnitCombatDesign.md` §4.
- **실제 소스**:
  - `Assets/@Library/Script/Stat/TStatContainer.cs` — 3종 공식 + `RemoveBySource`
  - `Assets/@Library/Script/Stat/TStatModifier.cs` — `Source` 필드
  - `Assets/@Project/Scripts/Game/Unit/UnitData.cs` — `RegisterBaseStats()` 1회
  - `Assets/@Project/Scripts/Define/SpecEnums.cs` — `eModifierType { Flat, PercentAdd, PercentMul }`

## Non-negotiable rules

1. **Stat 계산 공식 고정.** `final = (Σ Flat) × (1 + Σ PercentAdd) × Π (1 + PercentMul)`. PercentAdd는 합산 버프, PercentMul은 독립 승수(크리/속성).
2. **ModifierType은 세 종류.** `Flat` / `PercentAdd` / `PercentMul`. 새 축 필요시 enum 확장 + `CalculateStat` 갱신.
3. **Modifier는 `Source` 필드 필수.** 출처 기반 `RemoveBySource(src)`로 일괄 제거. 문자열 ID 매칭 금지.
4. **이벤트/반응 로직 금지.** `if (dmg) { ... }` 조건 반응은 `IEffect`의 일.
5. **Modifier 풀링.** `TStatModifier<T>.MakeModifier(...)`만. `new` 직접 생성 금지.
6. **`CalculateStat()`은 상태 전이 시점.** Augment/Item 추가, Effect Attach/Detach. 매 프레임 호출 금지.
7. **Base stat은 `Initialize()`의 `RegisterBaseStats()`에서 1회.** 매 CalculateStat 재등록 금지.

## Entry points / 대표 API

### TStatModifier

```csharp
public class TStatModifier<T> : PooledDisposable
{
    public string ModifierId { get; }
    public object Source { get; }
    public eModifierType ModifierType { get; }
    public T StatType { get; }
    public float Value { get; set; }

    public static TStatModifier<T> MakeModifier(
        string id, eModifierType type, T stat, float value,
        bool isStackable = false, object source = null);
}
```

### TStatContainer

```csharp
public class TStatContainer<T>
{
    public void Initialize();                                // 전체 Dispose + clear
    public void AddModifier(TStatModifier<T> m, bool calculate = false);
    public void RemoveModifier(TStatModifier<T> m, bool calculate = false);
    public void RemoveBySource(object source, bool calculate = false);
    public void CalculateStat();                             // 3종 공식 재계산
    public float GetStatValue(T type);                       // cache 읽기
}
```

### 사용 패턴

```csharp
var mod = TStatModifier<eStatType>.MakeModifier(
    id: "Augment_Damage_Major",
    type: eModifierType.PercentAdd,
    stat: eStatType.MeleeDamage,
    value: 0.3f,
    source: this);

unit.Stats.AddModifier(mod);
unit.Stats.CalculateStat();

// 해당 source의 모든 Modifier 일괄 제거
unit.Stats.RemoveBySource(this);
unit.Stats.CalculateStat();
```

## Anti-patterns — 절대 다시 도입 금지

- ❌ `eModifierType.Additive` / `eModifierType.Percent` 이름 부활. 삭제된 enum 값.
- ❌ `value *= (1 + m.Value)` 단일 루프로 Percent 병합. PercentAdd/PercentMul 구분 소실.
- ❌ 루프 돌면서 `list.Remove(mod)` — 증강 단위 일괄 제거 불가.
- ❌ `new TStatModifier<T>(...)` 직접 생성.
- ❌ `CalculateStat()` 매 프레임 호출.
- ❌ `UnitData.CalculateStat` 안에 Base stat 재등록 + `Effects.Clear()` 묶음 부활.
- ❌ `Source = null`로 Modifier 생성 (Base stat 외). Base는 문자열 `"Base"` 상수.

## Checklist — 편집 전 확인

- [ ] 새 Modifier에 의미있는 `Source`(Effect/Augment/Item 인스턴스)가 설정되는가?
- [ ] 새 `eModifierType` 분기가 `CalculateStat`에서 처리되는가?
- [ ] `CalculateStat()` 호출 지점이 상태 전이인가?
- [ ] 제거 경로가 `RemoveBySource(src)`인가?
- [ ] UI 갱신과 계산 재실행이 분리되어 있는가?
- [ ] Base stat 등록이 `RegisterBaseStats()` 안에만 있는가?

## Sample usage — 새 Stat 추가

`eStatType.Armor` 추가 가정:

```diff
 // Define/SpecEnums.cs
 public enum eStatType { None, Health = 1, ..., Range = 11,
+    Armor = 12,
 }
```

```diff
 // Unit/UnitData.cs RegisterBaseStats()
+Stats.AddModifier(TStatModifier<eStatType>.MakeModifier("Base_Armor",
+    eModifierType.Flat, eStatType.Armor, s.Armor, false, baseSource));
```

SpecCharacter의 `Armor` 필드 추가는 Spec sheet 작업(Unity Editor).

## 관련 Skill 경계

- `IEffect` 구현체가 Modifier Add/Remove → `combat-effect`
- `Augment` / `Item` 출처 클래스 → `combat-ability` (현재 제거됨)
- `DamagePipeline`이 `Stats.GetStatValue(...)` **읽기 전용** → `combat-damage`
- `HitSnapshotBuilder`가 발사 시점 Stat 복사 → `combat-hit`
