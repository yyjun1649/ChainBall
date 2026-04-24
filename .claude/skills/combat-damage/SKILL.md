---
name: combat-damage
description: How to deal and take damage in RogueLikeTemplate via DamageInfo and the 8-stage DamagePipeline. Use whenever the user touches DealDamage, ApplyDamageToHealth, DamageInfo, damage calculation, critical / dodge / resistance logic, DamagePipeline, DamageText, or the UnitController damage path. Enforces the pipeline order from UnitCombatDesign.md §6.3 and the "crit is attack-type agnostic" rule from §6.1.
paths: Assets/@Project/Scripts/Game/Damage/**/*.cs, Assets/@Project/Scripts/Game/Unit/UnitController.cs, Assets/@Project/Scripts/Game/Unit/Effect/UnitEffects.cs
---

# Combat — Damage Pipeline (L4)

데미지가 어떻게 흘러가는가를 관장하는 레이어. Stat 읽기 + HitInstance 결과 수신 + 8단계 훅. **Phase 3 적용됨** — 유지보수 가이드.

## Source of truth

- **설계 문서**: `UnitCombatDesign.md` §6 — §6.1 안티패턴, §6.2 DamageContext 필드, §6.3 8-stage 순서, §6.4 근접/원거리 공유.
- **실제 소스**:
  - `Assets/@Project/Scripts/Game/Damage/DamageInfo.cs` — DamageContext 역할, Attacker/Target + Version, Base/PreMitigation/Final, Canceled 등 전 필드
  - `Assets/@Project/Scripts/Game/Damage/DamagePipeline.cs` — static `Process(ctx)` 8단계, `OnFinalized` 이벤트
  - `Assets/@Project/Scripts/Game/Damage/DamageText.cs` — `RuntimeInitializeOnLoadMethod`로 `OnFinalized` 자동 구독
  - `Assets/@Project/Scripts/Game/Unit/UnitController.cs` — `DealDamage`는 Pipeline 호출만, `ApplyDamageToHealth(ctx)`가 HP 차감

## Non-negotiable rules

1. **크리티컬은 공격 타입 무관.** `if (damageType == Melee && attackType == Normal)` 같은 분기 금지. `Stage_RollCritical`이 모든 공격에서 동일. 특정 타입 비활성화는 Effect 또는 `ctx.Canceled` 플래그.
2. **8단계 순서 고정.** `ValidTarget → CalculateBase → BeforeDealDamage 훅 → Dodge → Critical → Resistance → BeforeTakeDamage 훅 → ApplyToHealth → After 훅`. `Canceled = true`면 해당 단계 이후 skip → `Finalize`.
3. **HP 차감은 `Stage_ApplyToHealth`에서만.** 실제 호출은 `ctx.Target.ApplyDamageToHealth(ctx)`. 외부에서 `CurrentHp -= ...` 금지.
4. **UI는 `Finalize` 이벤트 구독자.** 전투 로직에서 `DamageText.Show` 직접 호출 금지. `DamagePipeline.OnFinalized += ...` (현재 `DamageText`가 RuntimeInitialize로 자동 구독).
5. **Attacker/Target Version 진입 시 확정.** `ctx.AttackerVersion = Attacker.Version`, `ctx.TargetVersion = Target.Version`. Pipeline 내부 `IsValidTarget`이 불일치 시 `Canceled`로 drop.
6. **DamageInfo는 풀링.** `DamageInfo.Get()` 사용, `using` 또는 명시적 `Dispose()`. 직접 `new` 금지.
7. **공격자 없는 데미지 허용.** `ctx.Attacker == null` (환경 피해, DoT 만료)는 합법. `BeforeDealDamage` 훅 스킵, 나머지 동일.
8. **Pre-confirmed crit 보존.** `HitSnapshot.IsCritical == true`로 들어온 경우 `Stage_RollCritical`이 재판정하지 않고 전달된 `CritMultiplier`만 `PreMitigation`에 곱함.

## Entry points / 대표 API

### DamageInfo

```csharp
public class DamageInfo : DisposeObject<DamageInfo>
{
    public UnitController Attacker; public int AttackerVersion;
    public UnitController Target;   public int TargetVersion;

    public float BaseDamage;
    public float Percent = 1f;
    public float PreMitigation;
    public float Final;
    public float CritMultiplier;

    public eDamageType   DamageType;
    public eAttackType   AttackType;
    public eCriticalType CriticalType;

    public bool IsDodged;
    public bool IsBlocked;
    public bool Canceled;

    public readonly List<string> AppliedEffects;

    // Legacy alias — `Value` reads `Final`.
    public float Value { get => Final; set => Final = value; }
}
```

### DamagePipeline

```csharp
public static class DamagePipeline
{
    public static event Action<DamageInfo> OnFinalized;

    public static void Process(DamageInfo ctx)
    {
        if (!IsValidTarget(ctx)) { ctx.Canceled = true; Finalize(ctx); return; }

        Stage_CalculateBase(ctx);

        ctx.Attacker?.Data.Effects.RaiseBeforeDealDamage(ctx, ctx.Attacker, ctx.Target);
        if (ctx.Canceled) { Finalize(ctx); return; }

        Stage_RollDodge(ctx);
        if (ctx.IsDodged) { Finalize(ctx); return; }

        Stage_RollCritical(ctx);   // 공격 타입 무관. Pre-confirmed Critical은 재판정 스킵.
        Stage_ApplyResistance(ctx);

        ctx.Target.Data.Effects.RaiseBeforeTakeDamage(ctx, ctx.Attacker, ctx.Target);
        if (ctx.Canceled) { Finalize(ctx); return; }

        Stage_ApplyToHealth(ctx);   // HP 차감은 여기서만

        ctx.Attacker?.Data.Effects.RaiseAfterDealDamage(ctx, ctx.Attacker, ctx.Target);
        ctx.Target.Data.Effects.RaiseAfterTakeDamage(ctx, ctx.Attacker, ctx.Target);

        Finalize(ctx);
    }
}
```

### 호출 패턴

```csharp
// 근접: AttackModule → HitLauncher → InstantHit → RaiseHit → DamagePipeline.Process
// 원거리: AttackModule → HitLauncher → Projectile (MovingHit) → OnTriggerEnter → RaiseHit → DamagePipeline.Process
// 직접 호출(제한적): UnitController.DealDamage 또한 내부에서 Process만 호출

using (var ctx = DamageInfo.Get())
{
    ctx.Attacker = attacker; ctx.AttackerVersion = attacker.Version;
    ctx.Target   = target;   ctx.TargetVersion   = target.Version;
    ctx.BaseDamage = spec.baseDamage;
    ctx.Percent    = spec.basePercent;
    ctx.DamageType = spec.damageType;
    ctx.AttackType = spec.attackType;
    DamagePipeline.Process(ctx);
}
```

### UI 구독 (자동)

```csharp
// DamageText.cs — RuntimeInitialize로 등록. 추가 UI도 같은 패턴.
[RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
private static void RegisterOnDamageFinalized()
{
    DamagePipeline.OnFinalized -= OnDamageFinalized;
    DamagePipeline.OnFinalized += OnDamageFinalized;
}
```

## Anti-patterns — 절대 다시 도입 금지

- ❌ `UnitController.DealDamage` 내부 크리/계산 인라인. 전부 Pipeline.
- ❌ `if (damageType == eDamageType.Melee && attackType == eAttackType.Normal)` 식 공격 타입 크리 게이트.
- ❌ `DamageText.Show(info, pos)`를 전투 로직에서 직접 호출. `OnFinalized` 구독자로만.
- ❌ `CurrentHp -= value`를 `Stage_ApplyToHealth` 외부에서 수행.
- ❌ `UnitController.TakeDamage(DamageInfo, UnitController)` 시그니처 부활. 제거된 메서드.
- ❌ `ctx.Attacker` 세팅 시 `ctx.AttackerVersion` 미세팅.
- ❌ `ctx`를 `using` / `Dispose()` 없이 leak.

## Checklist — 편집 전 확인

- [ ] Pipeline에 새 로직을 추가한다면 8단계 중 어느 단계인가?
- [ ] `ctx.Attacker` 세팅 시 `AttackerVersion`도 함께 채우는가?
- [ ] 크리 판정 분기에 공격 타입 조건이 섞이지 않는가?
- [ ] HP 차감 경로가 `Stage_ApplyToHealth` → `Target.ApplyDamageToHealth(ctx)`인가?
- [ ] `DamageText.Show` 호출이 전투 로직에서 일어나지 않는가?
- [ ] `DamageInfo.Get()` 사용 후 `Dispose()` 보장되는가 (`using`)?
- [ ] 새 저항/배율 스탯을 추가했다면 `Stage_ApplyResistance` / `Stage_CalculateBase`에 올바르게 포함됐는가?

## Sample usage — 새 "Dodge" 스탯 도입

```diff
 // Define/SpecEnums.cs
 public enum eStatType { ...,
+    DodgeChance,
 }
```

```diff
 // Damage/DamagePipeline.Stage_RollDodge
-private static void Stage_RollDodge(DamageInfo ctx) { }
+private static void Stage_RollDodge(DamageInfo ctx)
+{
+    if (ctx.Target == null) return;
+    var dodge = ctx.Target.Data.Stats.GetStatValue(eStatType.DodgeChance);
+    if (UtilLibrary.GetChance(dodge))
+    {
+        ctx.IsDodged = true;
+        ctx.Final = 0f;
+    }
+}
```

+ `UnitData.RegisterBaseStats()`에 Base_DodgeChance 추가 (→ `combat-stat`).

## 관련 Skill 경계

- Effect 훅(Before/After), Canceled 플래그 설정 → `combat-effect`
- Stat 값 읽기 (`GetStatValue`) → `combat-stat` (읽기 전용)
- 발사 시점 수치 확정 + Snapshot 빌드 → `combat-hit` (`OnFireHit` 훅, `HitSnapshotBuilder`)
- 공격자/피격자 Version 체크 → `combat-unit`
- UI(`DamageText`, 카메라 쉐이크) 반응 → `OnFinalized` 구독
