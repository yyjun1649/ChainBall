---
name: combat-ability
description: Pattern reference for Augments, Items, StatBoxes, Abilities as Effect source tags in RogueLikeTemplate. These layers are REMOVED from current code but this skill defines the contract for when they are re-introduced. Use whenever the user asks about roguelike upgrades, augment / item / weapon / ability systems, or anything that applies a bundle of effects to a unit. Enforces the "ability = source tag that issues effects" pattern from UnitCombatDesign.md §5.4.
paths: Assets/@Project/Scripts/Game/Unit/**/*.cs
---

# Combat — Ability / Augment / Item (L6, **현재 제거됨**)

로그라이크 업그레이드·장비·증강은 **Effect 묶음을 발행하는 출처 태그**. 자체 로직 클래스가 아님.

**현재 상태**: Augment / Item / StatBox 전부 제거됨 (Phase 2.5 + 7 사용자 요청). 이 Skill은 **재도입 시의 설계 규약**을 담는다.

## Source of truth

- **설계 문서**: `UnitCombatDesign.md` §5.4 — Ability/Augment/Item = Effect의 출처, §5.5 반응형 샘플, §11 명명 규약.
- **관련 레이어**: `combat-effect` (계약), `combat-stat` (Source 기반 Modifier), `combat-hit` (Behavior 공급 Effect).

## Non-negotiable rules (재도입 시)

1. **Ability/Augment/Item은 Effect 묶음을 발행한다.** 자체적으로 전투 로직을 실행하지 않는다. "공격 시 확률로 얼음 화살 추가"는 `IEffect` 1개로 구현, 그 Effect를 발행하는 Augment/Item이 출처 태그.
2. **Apply는 대칭적인 Revoke를 가진다.** `Apply(UnitData)`가 있으면 반드시 `Revoke(UnitData)`. 일회성 호출만 존재하면 안 됨.
3. **Effect에 `Source = this` 를 실어보낸다.** `ISourcedEffect` 구현. `EffectHost.RemoveBySource(augment)` 한 번으로 Augment가 준 Effect 일괄 제거.
4. **새 기능 추가 = 기존 코드 수정 없이.** 신규 `IEffect` 파생 클래스 1개 + `EffectFactory.Initialize`에 등록 1줄 + Spec 시트 1행. `UnitController`/`DamagePipeline`/`HitInstance` 수정 필요하면 레이어 경계가 잘못된 것.
5. **스펙은 `SpecEffect` 단위 조합.** `SpecAugment.effects: int[]`가 `SpecEffect` 키 참조. 한 SpecEffect는 여러 Augment/Item이 공유 가능.
6. **`PooledDisposable` 기반.** `UnitAugment`, `UnitItem` 등 Pool 반납.
7. **증분 반영 원칙.** Augment 추가 시 = 그 Augment의 Effect만 `unit.Effects.Add`, Augment 제거 시 = `unit.Effects.RemoveBySource(augment)`. `UnitData.CalculateStat` 전체 리빌드 유발 금지.

## 재도입 시 목표 코드

```csharp
public class UnitAugment : PooledDisposable
{
    public int Id;
    public SpecAugment SpecAugment { get; private set; }
    readonly List<IEffect> _issued = new();

    public void Set(int id)
    {
        Id = id;
        SpecAugment = SpecAugment.GetDictionary()[Id];
    }

    public void Apply(UnitData unit)
    {
        foreach (var effectName in SpecAugment.effects)
        {
            var spec = SpecEffect.GetDictionary()[effectName].SetParam();
            var e = EffectFactory.Create(spec, effectName.ToString(), source: this);
            _issued.Add(e);
            unit.Effects.Add(e);
        }
    }

    public void Revoke(UnitData unit)
    {
        unit.Effects.RemoveBySource(this);
        _issued.Clear();
    }

    protected override void Reset()
    {
        Id = -1;
        SpecAugment = null;
        _issued.Clear();
    }
}
```

### 컨테이너 (UnitAugments)

```csharp
public class UnitAugments : UnitDataModule
{
    public List<UnitAugment> Augments = new();

    public void AddAugment(UnitAugment augment)
    {
        Augments.Add(augment);
        augment.Apply(_unitData);
        _unitData.Stats.CalculateStat();
    }

    public void RemoveAugment(UnitAugment augment)
    {
        if (!Augments.Remove(augment)) return;
        augment.Revoke(_unitData);
        _unitData.Stats.CalculateStat();
    }

    public void Clear()
    {
        foreach (var a in Augments) a.Revoke(_unitData);
        foreach (var a in Augments) a.Dispose();
        Augments.Clear();
    }
}
```

그리고 `UnitData`에 다시 `public UnitAugments Augments;` 필드 + `Initialize`에서 모듈 Initialize.

## Anti-patterns (재도입 시 피해야 할)

- ❌ `UnitAugments.Apply()` 일괄 적용 (모든 Augment의 모든 Effect를 한 번에 재등록). 개별 Add/Remove로 증분 반영.
- ❌ `_onCalculate.Dispatch()` 로 `UnitData.CalculateStat` 전체 리빌드 유발. `Stats.CalculateStat()`만 직접 호출.
- ❌ Effect 생성 시 `source = null`. 반드시 `this` (Augment/Item 인스턴스).
- ❌ `UnitAugment.Initialize`에서 Effects 리스트를 **생성 시 1회** 채우고 재사용. 같은 인스턴스가 여러 Unit에 적용되면 Effect 공유 문제. `Apply(unit)` 시점에 Factory로 발행.
- ❌ `EffectFactory.Create(spec, name)` 시그니처 (source 빠짐). 이전 코드에 있던 패턴 — 재도입 시 `source` 인자 필수.
- ❌ `DummyEffect` 폴백. 미구현 타입은 명시적 Error + null.

## Checklist — 재도입 시

- [ ] 새 Augment/Item/StatBox를 만들 때 기존 `IEffect` 구현 + Spec 시트 1행으로 해결되는가?
- [ ] 새 `IEffect` 구현이 필요하면 `combat-effect`의 `OnAttach/OnDetach` 계약 따르는가?
- [ ] `ISourcedEffect`를 구현해 `Source = <Augment instance>`인가?
- [ ] `Apply(unit)`에 대칭 `Revoke(unit)` 있는가?
- [ ] 같은 Augment 인스턴스가 여러 Unit에 적용되거나 재사용되어도 Effect가 공유되지 않는가?
- [ ] Augment 제거가 `EffectHost.RemoveBySource(augment)` 한 줄로 되는가?
- [ ] `UnitData.CalculateStat` 전체 재조립이 유발되지 않는가?

## 새 기능 추가 체크리스트 (설계 §부록 B 축약)

"공격 시 확률로 얼음 화살 하나 추가 발사" 요청:

1. 레이어 식별 — Reactive Effect (→ `combat-effect`)
2. 기존 IEffect 구현으로 표현 가능? 아니면 신규 파생.
3. 구독할 훅 — `OnFireHit` 또는 `OnAfterDealDamage`.
4. 출처 — Augment / Item.
5. 기존 `UnitController` / `DamagePipeline` / `HitInstance` 수정 없이 구현 가능? → 불가능이면 레이어 경계 재검토.
6. 풀링 — 새 Effect 클래스도 풀 등록.
7. OnDetach 이벤트 해제 누락 없는가?
8. `Source = this`로 발행자 기록했는가?

## 관련 Skill 경계

- 새 Effect 구현 (반응형/상태형/스탯 모디파이어 계약) → `combat-effect`
- Effect가 Stat을 바꾸는 방식 → `combat-stat`
- Augment가 새 공격 형태 제공 (예: "모든 공격 분열") → `combat-hit` (Behavior 추가하는 Effect)
- UnitData가 Augments/Items 허브 보유 → `combat-unit` (재도입 시 필드 추가)

이 Skill은 "어떤 출처가 어떤 효과를 한 묶음으로 발행하는가"만 관장. 효과 계약은 `combat-effect`, 수치는 `combat-stat`, 공격 형태는 `combat-hit`.
