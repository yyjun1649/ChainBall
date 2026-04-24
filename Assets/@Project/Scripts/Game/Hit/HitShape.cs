using System.Collections.Generic;
using Library;
using UnityEngine;

public abstract class HitShape
{
    public abstract void Query(Vector3 origin, int enemyLayer, List<UnitController> results);

    protected static void CollectUnits(Collider2D[] cols, List<UnitController> results)
    {
        results.Clear();
        for (int i = 0; i < cols.Length; i++)
        {
            if (MappingHelperManager.Instance.Unit.TryGet(cols[i], out var unit) && unit.IsAlive)
            {
                results.Add(unit);
            }
        }
    }
}

public class CircleShape : HitShape
{
    public float Radius;

    public override void Query(Vector3 origin, int enemyLayer, List<UnitController> results)
    {
        int mask = 1 << enemyLayer;
        var hits = Physics2D.OverlapCircleAll(origin, Radius, mask);
        CollectUnits(hits, results);
    }
}

public class ConeShape : HitShape
{
    public float Radius;
    public float HalfAngleDegrees;
    public Vector3 Direction = Vector3.right;

    public override void Query(Vector3 origin, int enemyLayer, List<UnitController> results)
    {
        int mask = 1 << enemyLayer;
        var hits = Physics2D.OverlapCircleAll(origin, Radius, mask);
        results.Clear();

        Vector2 forward = ((Vector2)Direction).normalized;

        for (int i = 0; i < hits.Length; i++)
        {
            if (!MappingHelperManager.Instance.Unit.TryGet(hits[i], out var unit)) continue;
            if (!unit.IsAlive) continue;

            Vector2 dir = ((Vector2)hits[i].transform.position - (Vector2)origin);
            if (dir.sqrMagnitude < 0.0001f)
            {
                results.Add(unit);
                continue;
            }

            if (Vector2.Angle(forward, dir.normalized) > HalfAngleDegrees) continue;

            results.Add(unit);
        }
    }
}

public class BoxShape : HitShape
{
    public Vector2 Size;
    public float AngleDegrees;

    public override void Query(Vector3 origin, int enemyLayer, List<UnitController> results)
    {
        int mask = 1 << enemyLayer;
        var hits = Physics2D.OverlapBoxAll(origin, Size, AngleDegrees, mask);
        CollectUnits(hits, results);
    }
}

public class LineShape : HitShape
{
    public Vector3 Direction = Vector3.right;
    public float Length;
    public float Width;

    public override void Query(Vector3 origin, int enemyLayer, List<UnitController> results)
    {
        int mask = 1 << enemyLayer;

        var normalized = ((Vector2)Direction).normalized;
        Vector3 end = origin + (Vector3)(normalized * Length);
        Vector3 center = (origin + end) * 0.5f;
        Vector2 size = new Vector2(Length, Width);
        float angleDeg = Mathf.Atan2(normalized.y, normalized.x) * Mathf.Rad2Deg;

        var hits = Physics2D.OverlapBoxAll(center, size, angleDeg, mask);
        CollectUnits(hits, results);
    }
}
