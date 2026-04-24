using System.Collections.Generic;
using UnityEngine;

public class AuraHit : HitInstance<AuraHit>
{
    private static readonly List<UnitController> _tempResults = new List<UnitController>();

    public float TickInterval;
    public Transform FollowTarget;

    private float _tickTimer;

    protected override void OnSpawn()
    {
        _tickTimer = 0f;
        DoQuery();
    }

    protected override void Tick(float deltaTime)
    {
        if (Snapshot == null)
        {
            Despawn();
            return;
        }

        if (FollowTarget != null)
        {
            transform.position = FollowTarget.position;
        }

        _tickTimer += deltaTime;
        if (TickInterval > 0f && _tickTimer >= TickInterval)
        {
            _tickTimer -= TickInterval;
            DoQuery();
        }

        if (Snapshot.LifeTime > 0f && Age >= Snapshot.LifeTime)
        {
            Despawn();
        }
    }

    private void DoQuery()
    {
        if (!IsAlive) return;
        if (Shape == null || Snapshot == null || Snapshot.Attacker == null) return;

        Shape.Query(transform.position, Snapshot.Attacker.EnemyLayer, _tempResults);

        for (int i = 0; i < _tempResults.Count; i++)
        {
            if (!IsAlive) break;
            RaiseHit(_tempResults[i]);
        }
    }

    public override void OnRelease()
    {
        FollowTarget = null;
        TickInterval = 0f;
        base.OnRelease();
    }
}
