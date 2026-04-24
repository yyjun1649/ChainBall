using System.Collections.Generic;
using UnityEngine;

public class InstantHit : HitInstance<InstantHit>
{
    private static readonly List<UnitController> _tempResults = new List<UnitController>();

    protected override void OnSpawn()
    {
        DoQuery();
    }

    protected override void Tick(float deltaTime)
    {
        if (Snapshot == null)
        {
            Despawn();
            return;
        }

        if (Snapshot.LifeTime > 0f)
        {
            if (IsAlive && Age < Snapshot.LifeTime)
            {
                DoQuery();
            }

            if (Age >= Snapshot.LifeTime)
            {
                Despawn();
            }
        }
        else
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
}
