using Library;
using UnityEngine;

public class AreaDotSkill : Skill
{
    public override void Use(UnitController from, UnitController target)
    {
        if (_spec.hitInstance == 0) return;

        Vector3 origin = from.transform.position;
        var shape = new CircleShape { Radius = _spec.range };

        var interval = _spec.arg0;
        var duration = _spec.arg0;

        var instance = Handlers.Pool.Get<AuraHit>(_spec.hitInstance);
        instance.TickInterval = interval;

        HitLauncher.Launch(
            from, _spec, instance, shape, origin, Vector3.right, target,
            lifeTimeOverride: duration);
    }
}
