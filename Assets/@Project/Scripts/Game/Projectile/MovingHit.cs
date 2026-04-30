using Library;
using UnityEngine;

[RequireComponent(typeof(Collider2D))]
public class MovingHit : HitInstance<MovingHit>
{
    [SerializeField]private Collider2D _collider;
    [SerializeField] private ProjectileMovement _movement;
    
    public ProjectileMovement Movement => _movement;
    public Collider2D Collider => _collider;

    protected override void OnSpawn()
    {
        if (_movement is StraightMovement straight)
        {
            straight.Setup(Snapshot.Speed, Snapshot.LifeTime);
        }
        else if (_movement is BounceMovement bounce)
        {
            bounce.SetupFromSnapshot(Snapshot.Speed, Snapshot.LifeTime);
        }
        _movement.Initialize(this, Snapshot.Direction);

        // BounceMovement is hit-and-keep-going by design; PenetrateBehavior would force
        // a despawn on first hit, so skip the auto-attach for it.
        if (_movement is not BounceMovement && Snapshot.HitCount >= 0)
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

    // Hit hook for movement components that drive their own hit detection
    // (e.g. BounceMovement's sweep cast). Trigger-overlap path (OnTriggerEnter2D) is
    // intentionally absent — ChainBall projectiles are bounce-only.
    public void NotifyHitFromMovement(UnitController unit)
    {
        if (!IsAlive) return;
        if (Snapshot == null || !Snapshot.IsAttackerAlive())
        {
            Despawn();
            return;
        }
        if (unit == null || !unit.IsAlive) return;
        if (unit.MyLayer == Snapshot.Attacker.MyLayer) return;

        RaiseHit(unit);
    }
}
