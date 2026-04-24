using Library;
using UnityEngine;

[RequireComponent(typeof(Collider2D))]
public class MovingHit : HitInstance<MovingHit>
{
    private ProjectileMovement _movement;
    public ProjectileMovement Movement => _movement;

    private Collider2D _collider;

    private void Awake()
    {
        _collider = GetComponent<Collider2D>();
        _collider.isTrigger = true;
    }

    protected override void OnSpawn()
    {
        _movement = GetComponent<ProjectileMovement>();
        if (_movement != null)
        {
            if (_movement is StraightMovement straight)
            {
                straight.Setup(Snapshot.Speed, Snapshot.LifeTime);
            }
            _movement.Initialize(this, Snapshot.Direction);
        }

        if (Snapshot.HitCount >= 0)
        {
            AddBehavior(new PenetrateBehavior(Snapshot.HitCount - 1));
        }
    }

    protected override void Tick(float deltaTime)
    {
        _movement?.Tick(deltaTime);

        if (Snapshot != null && Snapshot.LifeTime > 0f && Age >= Snapshot.LifeTime)
        {
            Despawn();
        }
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (!IsAlive) return;
        if (Snapshot == null || !Snapshot.IsAttackerAlive())
        {
            Despawn();
            return;
        }

        if (!MappingHelperManager.Instance.Unit.TryGet(other, out var unit)) return;
        if (unit.MyLayer == Snapshot.Attacker.MyLayer) return;
        if (!unit.IsAlive) return;

        RaiseHit(unit);
    }
}
