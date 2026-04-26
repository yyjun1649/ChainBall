using System.Collections.Generic;
using Library;
using SpecData;
using UnityEngine;

public static class HitLauncher
{
    // Core: caller supplies an already-obtained HitInstance. Launch fills in snapshot + behaviors.
    public static IHitInstance Launch(
        UnitController attacker,
        IDamageSpec damageSpec,
        IHitInstance instance,
        HitShape shape,
        Vector3 origin,
        Vector3 direction,
        UnitController target = null,
        IReadOnlyList<IHitBehavior> behaviors = null,
        float lifeTimeOverride = -1f)
    {
        if (instance == null) return null;

        SpecHitInstance hitSpec = null;
        if (damageSpec != null && damageSpec.hitInstance != 0)
        {
            SpecDataManager.Instance.SpecHitInstance.TryGet(damageSpec.hitInstance, out hitSpec);
        }

        var snap = HitSnapshotBuilder.Build(attacker, damageSpec, hitSpec, origin, direction, target);
        if (lifeTimeOverride >= 0f) snap.LifeTime = lifeTimeOverride;

        instance.Initialize(snap, shape);

        if (behaviors != null)
        {
            for (int i = 0; i < behaviors.Count; i++)
            {
                instance.AddBehavior(behaviors[i]);
            }
        }

        return instance;
    }

    // Shortcut for the common Projectile (MovingHit) case.
    // Pulls the instance from Handlers.Pool keyed by damageSpec.hitInstance, then calls Launch.
    public static MovingHit FireProjectile(
        UnitController attacker,
        IDamageSpec damageSpec,
        Vector3 origin,
        Vector3 direction,
        UnitController target = null)
    {
        if (damageSpec == null || damageSpec.hitInstance == 0) return null;

        var instance = Handlers.Pool.Get<MovingHit>(damageSpec.hitInstance);
        Launch(attacker, damageSpec, instance, null, origin, direction, target);
        return instance;
    }
}
