---
name: combat-effect
description: How to design and implement IEffect / EffectHost / Reactive and Status effects in RogueLikeTemplate. Use whenever the user touches IEffect implementations, UnitEffects event hooks, EffectFactory, StatModifierEffect, DoT / Frozen / LifeSteal / TeleportOnHit, or any effect that reacts to events or expires over time. Enforces the OnAttach/OnDetach lifecycle contract from UnitCombatDesign.md §5.2.
paths: Assets/@Project/Scripts/Game/Unit/Effect/**/*.cs
---

# Combat — Effect (L3)

반응(Reactive) + 상태(Status) 효과 레이어. 값 계산은 `combat-stat`, 공격 형태는 `combat-hit`. **Phase 2 적용됨** — 유지보수 가이드.

## Source of truth

- **설계 문서**: `UnitCombatDesign.md` §5 전체 — §5.2 IEffect 라이프사이클, §5.3 EffectHost, §5.5 반응형 샘플, §5.6 상태형 샘플.
- **실제 소스**:
  - `Assets/@Project/Scripts/Game/Unit/Effect/Effect.cs` — `IEffect`, `ITickEffect`, `IDurationEffect`, `IStackableEffect`, `ISourcedEffect`, `StatModifierEffect`
  - `Assets/@Project/Scripts/Game/Unit/Effect/UnitEffects.cs` — EffectHost 역할, Before/After 훅
  - `Assets/@Project/Scripts/Game/Unit/Effect/EffectFactory.cs` — Spec → IEffect 팩토리
  - `Assets/@Project/Scripts/Game/Unit/Effect/AdditionalEffects.cs`, `MoreEffects.cs`, `CriticalEffect.cs` — 레거시(주석 처리된 과거 구현). 되살릴 때는 현행 `IEffect` 계약 기준으로.

## Non-negotiable rules

1. **세 종류를 섞지 않는다.** Stat Modifier(값 계산 — `combat-stat`), Reactive Effect(이벤트 반응), Status Effect(지속 시간). Status가 Stat을 **도구로** 쓰는 건 OK (FrozenEffect가 MoveSpeed Modifier를 Add).
2. **`IEffect` 계약은 `OnAttach(UnitData)` / `OnDetach(UnitData)` 두 개뿐.** `Apply/Remove/ClearMemory` 시그니처는 설계가 금지. `Id` 프로퍼티 필수.
3. **OnDetach에서 이벤트/Modifier 반드시 해제.** OnAttach의 모든 `+=` / `AddModifier`에 대응하는 `-=` / `RemoveBySource`가 OnDetach에 있어야 한다. 누락 시 유령 구독자.
4. **`EffectHost`(=`UnitEffects`) 훅은 Before/After 구분.** `BeforeDealDamage`는 수치 수정/취소, `AfterDealDamage`는 흡혈·가시·킬 연쇄. 한 훅에 다 몰면 순서 버그.
5. **Reactive Effect는 `Time.time` / `Time.deltaTime`만 신뢰.** `UnitData.CalculateStat` 같은 외부 리셋에 의존 금지. (Phase 1.4로 CalculateStat이 Effect를 건드리지 않도록 분리됨)
6. **IDurationEffect 만료는 `EffectHost.Tick(dt)`이 감시.** 효과 자신이 `unit.Effects.Remove(this)` 호출하지 않음.
7. **중복 방지는 `Id` 기준.** 같은 `Id`로 `Add` 요청 시 `IStackableEffect`면 `OnStack()`만 호출, 아니면 조용히 무시. 덧붙이지 않는다.
8. **`ISourcedEffect.Source`** — Augment/Item/Skill 인스턴스를 `source`로 실어 보내면 `EffectHost.RemoveBySource(src)` 한 번으로 해당 출처 Effect 일괄 제거.

## Entry points / 대표 API

### IEffect 계약

```csharp
public interface IEffect
{
    string Id { get; }
    void OnAttach(UnitData unit);
    void OnDetach(UnitData unit);
}

public interface ITickEffect : IEffect      { void OnTick(float deltaTime); }
public interface IDurationEffect : IEffect  { float RemainingTime { get; } bool IsExpired { get; } }
public interface IStackableEffect : IEffect { int Stacks { get; } int MaxStacks { get; } void OnStack(); }
public interface ISourcedEffect : IEffect   { object Source { get; } }
```

### EffectHost (UnitEffects)

```csharp
public class UnitEffects : UnitDataModule
{
    public Relay<DamageInfo, UnitController, UnitController> OnBeforeDealDamage;
    public Relay<DamageInfo, UnitController, UnitController> OnAfterDealDamage;
    public Relay<DamageInfo, UnitController, UnitController> OnBeforeTakeDamage;
    public Relay<DamageInfo, UnitController, UnitController> OnAfterTakeDamage;
    public Relay<DamageInfo, UnitController, UnitController> OnBeforeHeal / OnAfterHeal / ...
    public Relay<HitSnapshot> OnFireHit;
    public Relay<UnitController, UnitController> OnKill;
    public Relay OnDeath;

    public void Add(IEffect e);           // 중복 Id면 IStackable.OnStack() 또는 무시
    public void Remove(IEffect e);
    public void RemoveBySource(object source);
    public void Tick(float deltaTime);    // ITickEffect + IDurationEffect 만료 처리
    public void Clear();                  // 전체 OnDetach 후 제거

    public void RaiseBefore{Deal,Take}Damage(...) / RaiseAfter{...}(...);
    public void RaiseOnFireHit(HitSnapshot snap);
    public void RaiseKill / RaiseDeath;
}
```

### Reactive 샘플 — 맞으면 순간이동

```csharp
public class TeleportOnHitEffect : IEffect, ISourcedEffect
{
    readonly float _distance, _cooldown;
    float _nextAvailable;
    UnitData _unit;
    public object Source { get; }
    public string Id => $"Teleport_{_distance}";

    public TeleportOnHitEffect(object source, float distance, float cooldown)
    { Source = source; _distance = distance; _cooldown = cooldown; }

    public void OnAttach(UnitData unit)
    {
        _unit = unit;
        unit.Effects.OnBeforeTakeDamage.AddListener(OnBeforeTakeDamage);
    }

    public void OnDetach(UnitData unit)
    {
        unit.Effects.OnBeforeTakeDamage.RemoveListener(OnBeforeTakeDamage);
    }

    void OnBeforeTakeDamage(DamageInfo ctx, UnitController from, UnitController to)
    {
        if (Time.time < _nextAvailable || from == null) return;
        var tr = to.transform;
        var away = (tr.position - from.transform.position).normalized;
        tr.position += away * _distance;
        _nextAvailable = Time.time + _cooldown;
        // ctx.Canceled = true; // 완전 회피로 만들려면
    }
}
```

### Status 샘플 — 빙결 (IDurationEffect + ITickEffect)

```csharp
public class FrozenEffect : IDurationEffect, ITickEffect, ISourcedEffect
{
    public string Id => "Frozen";
    public float RemainingTime { get; private set; }
    public bool IsExpired => RemainingTime <= 0f;
    public object Source { get; }

    UnitData _unit;
    TStatModifier<eStatType> _moveMod, _atkMod;

    public FrozenEffect(object source, float duration)
    { Source = source; RemainingTime = duration; }

    public void OnAttach(UnitData unit)
    {
        _unit = unit;
        _moveMod = TStatModifier<eStatType>.MakeModifier("Frozen_Move", eModifierType.PercentAdd, eStatType.MoveSpeed, -1f, false, this);
        _atkMod  = TStatModifier<eStatType>.MakeModifier("Frozen_Atk",  eModifierType.PercentAdd, eStatType.AttackSpeed, -1f, false, this);
        unit.Stats.AddModifier(_moveMod);
        unit.Stats.AddModifier(_atkMod);
        unit.Stats.CalculateStat();
    }

    public void OnDetach(UnitData unit)
    {
        unit.Stats.RemoveBySource(this);
        unit.Stats.CalculateStat();
    }

    public void OnTick(float dt) { RemainingTime -= dt; }
}
```

### EffectFactory 등록

```csharp
public static class EffectFactory
{
    public static void Initialize()
    {
        _creators[eEffectType.StatModifier] = (spec, name, source) => new StatModifierEffect(
            name,
            (eStatType)(int)spec.Params["statType"],
            spec.Params["statValue"],
            (eModifierType)(int)spec.Params["modifierType"],
            source);
    }
    public static IEffect Create(SpecEffect spec, string effectName, object source = null);
}
```

## Anti-patterns — 절대 다시 도입 금지

- ❌ `IEffect { Initialize, Apply, Remove, ClearMemory }` 시그니처. 삭제된 계약.
- ❌ `DummyEffect` 반환 — 미구현 타입은 `Debug.LogError` 후 `null` 반환, 예외 처리는 호출자.
- ❌ `StatModifierEffect`가 `OnAlwaysApply` 리스너로 AddModifier 호출. Stat/Effect 레이어 혼재. `OnAttach`에서 직접 Add / `OnDetach`에서 `RemoveBySource(this)`.
- ❌ 단일 `OnTakeDamage` / `OnDealDamage` 훅만 쓰기. Before/After 구분 필요.
- ❌ OnAttach의 `+=` 구독을 OnDetach에서 `-=` 안 하고 방치. 풀 재사용 시 listener 중첩.
- ❌ 같은 Id Effect를 중복으로 `_active`에 추가 (`Add`가 처리).
- ❌ Infinite-loop Effect에서 `OnComplete` 같은 종료 시그널 없이 Despawn 미연결.

## Checklist — 편집 전 확인

- [ ] 새 `IEffect` 구현체가 `OnAttach/OnDetach` 시그니처인가?
- [ ] OnAttach의 모든 `+=` / `AddModifier` 대응 `-=` / `RemoveBySource`가 OnDetach에 있는가?
- [ ] 값 변경 Effect라면 `combat-stat` 케이스 아닌가? (그쪽이 더 맞으면 StatModifier로)
- [ ] Duration이 있다면 `IDurationEffect` + `OnTick`에서 `RemainingTime` 차감?
- [ ] 중복 Attach 시 동작이 정의됐는가? (무시 / 스택 / 재적용)
- [ ] `ISourcedEffect.Source`에 외부 출처 인스턴스가 연결되는가?
- [ ] `EffectFactory.Create`에 새 `eEffectType`이 등록됐는가?

## Sample usage — 새 Reactive Effect "공격 시 20% 확률로 이중타격"

```csharp
public class DoubleStrikeEffect : IEffect, ISourcedEffect
{
    readonly float _chance;
    readonly float _multiplier;
    public object Source { get; }
    public string Id => $"DoubleStrike_{_chance}";

    public DoubleStrikeEffect(object source, float chance, float multiplier)
    { Source = source; _chance = chance; _multiplier = multiplier; }

    public void OnAttach(UnitData unit)
    {
        unit.Effects.OnAfterDealDamage.AddListener(OnAfterDealDamage);
    }
    public void OnDetach(UnitData unit)
    {
        unit.Effects.OnAfterDealDamage.RemoveListener(OnAfterDealDamage);
    }

    void OnAfterDealDamage(DamageInfo ctx, UnitController from, UnitController to)
    {
        if (ctx.Canceled) return;
        if (UtilLibrary.GetChance(_chance))
        {
            from.DealDamage(to, ctx.BaseDamage * _multiplier, ctx.DamageType, ctx.AttackType, ctx.Percent);
        }
    }
}
```

EffectFactory에 등록 + SpecEffect 시트에 `eEffectType.DoubleStrike`(새 enum) 추가.

## 관련 Skill 경계

- 수치 계산, ModifierType, RemoveBySource → `combat-stat`
- DamageContext 훅 단계 (Before/After) → `combat-damage`
- `OnFireHit(snap)` — 발사 시점 Snapshot 수정 → `combat-hit`
- Effect를 묶어 발행하는 출처 (Augment/Item) → `combat-ability` (현재 제거됨)
- Effect OnDetach + 유닛 재사용 → `combat-unit` (Version 패턴)
