using System.Collections.Generic;
using Library;
using UnityEngine;

public class HitSnapshot : DisposeObject<HitSnapshot>
{
    public UnitController Attacker;
    public int AttackerVersion;
    public UnitController Target;

    public float BaseDamage;
    public float Percent = 1f;
    public eDamageType DamageType;
    public eAttackType AttackType;

    public float CritChance;
    public float CritMultiplier;
    public bool IsCritical;

    public float Speed;
    public float LifeTime;
    public int HitCount = 1;

    public Vector3 Origin;
    public Vector3 Direction = Vector3.right;

    public readonly Dictionary<string, object> Extra = new Dictionary<string, object>();

    public bool IsAttackerAlive()
    {
        if (Attacker == null) return false;
        if (Attacker.Version != AttackerVersion) return false;
        return true;
    }

    protected override void Reset()
    {
        Attacker = null; AttackerVersion = 0;
        Target = null;
        BaseDamage = 0f; Percent = 1f;
        DamageType = eDamageType.Melee;
        AttackType = eAttackType.Normal;
        CritChance = 0f; CritMultiplier = 0f; IsCritical = false;
        Speed = 0f; LifeTime = 0f; HitCount = 1;
        Origin = Vector3.zero; Direction = Vector3.right;
        Extra.Clear();
    }
}
