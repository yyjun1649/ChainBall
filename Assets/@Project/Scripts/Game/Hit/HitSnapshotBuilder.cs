using Library;
using SpecData;
using UnityEngine;

public static class HitSnapshotBuilder
{
    public static HitSnapshot Build(
        UnitController attacker,
        IDamageSpec damageSpec,
        SpecHitInstance hitSpec,
        Vector3 origin,
        Vector3 direction,
        UnitController target = null)
    {
        var snap = HitSnapshot.Get();

        snap.Attacker = attacker;
        snap.AttackerVersion = attacker != null ? attacker.Version : 0;
        snap.Target = target;

        snap.BaseDamage = damageSpec.baseDamage;
        snap.Percent = damageSpec.basePercent;
        snap.DamageType = damageSpec.damageType;
        snap.AttackType = damageSpec.attackType;

        snap.Origin = origin;
        snap.Direction = direction;

        if (hitSpec != null)
        {
            snap.Speed = hitSpec.moveSpeed;
            snap.LifeTime = hitSpec.lifeTime;
            snap.HitCount = hitSpec.hitCount;
        }

        if (attacker != null)
        {
            var stats = attacker.Data.Stats;
            snap.CritChance = stats.GetStatValue(eStatType.CriticalChance);
            snap.CritMultiplier = 1f + stats.GetStatValue(eStatType.CriticalDamage);

            attacker.Data.Effects.RaiseOnFireHit(snap);
        }

        snap.IsCritical = UtilLibrary.GetChance(snap.CritChance);

        return snap;
    }
}
