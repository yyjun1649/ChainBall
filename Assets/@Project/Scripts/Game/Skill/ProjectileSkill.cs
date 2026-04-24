using UnityEngine;

public class ProjectileSkill : Skill
{
    public override void Use(UnitController from, UnitController target)
    {
        if (_spec.hitInstance == 0) return;

        Vector3 origin = from.transform.position;

        Vector3 direction = Vector3.right;
        if (target != null)
        {
            direction = (target.transform.position - origin).normalized;
        }

        for (int i = 0; i < _spec.useCount; i++)
        {
            HitLauncher.FireProjectile(from, _spec, origin, direction, target);
        }
    }
}
