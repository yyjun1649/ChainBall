using System.Collections.Generic;
using Library;

public class DamageInfo : DisposeObject<DamageInfo>
{
    public UnitController Attacker;
    public int AttackerVersion;
    public UnitController Target;
    public int TargetVersion;

    public eDamageType DamageType { get; set; }
    public eCriticalType CriticalType { get; set; }
    public eAttackType AttackType { get; set; }

    public float BaseDamage;
    public float PreMitigation;
    public float Final;
    public float CritMultiplier;

    public bool IsDodged;
    public bool IsBlocked;
    public bool Canceled;

    public float Percent = 1f;

    public readonly List<string> AppliedEffects = new List<string>();

    // Legacy alias — existing consumers (DamageText etc.) read Value.
    public float Value
    {
        get => Final;
        set => Final = value;
    }

    public void Set(eDamageType damageType, eCriticalType criticalType, eAttackType attackType, float value)
    {
        DamageType = damageType;
        CriticalType = criticalType;
        AttackType = attackType;
        Final = value;
    }

    // Pool key for DamageText. Critical / Normal / future variants get separate prefabs;
    // DamageType is conveyed via SetColor on a shared prefab, not via key.
    public int GetId()
    {
        return (int)CriticalType;
    }

    protected override void Reset()
    {
        Attacker = null; AttackerVersion = 0;
        Target = null; TargetVersion = 0;
        DamageType = eDamageType.Melee;
        CriticalType = eCriticalType.Normal;
        AttackType = eAttackType.Normal;
        BaseDamage = 0f; PreMitigation = 0f; Final = 0f;
        CritMultiplier = 0f;
        IsDodged = false; IsBlocked = false; Canceled = false;
        Percent = 1f;
        AppliedEffects.Clear();
    }
}
