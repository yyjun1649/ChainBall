using Library;
using UnityEngine;

public class AroundSkill : Skill
{
    public override void Use(UnitController from, UnitController target)
    {
        if (_spec.hitInstance == 0) return;

        Vector3 origin = from.transform.position;
        var shape = new CircleShape { Radius = _spec.range };

        for (int i = 0; i < _spec.useCount; i++)
        {
            var instance = Handlers.Pool.Get<InstantHit>(_spec.hitInstance);
            HitLauncher.Launch(from, _spec, instance, shape, origin, Vector3.right, target);
        }
    }
}
