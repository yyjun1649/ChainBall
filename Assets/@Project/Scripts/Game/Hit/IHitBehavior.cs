using UnityEngine;

public interface IHitBehavior
{
    int Priority { get; }
    void OnAttach(IHitInstance hit);
    void OnDetach(IHitInstance hit);
}

// ─── Velocity-based Behaviors (Priority 10-30) ──────────────────────────────
// These read / modify the projectile's velocity. Safe to combine.

public class PenetrateBehavior : IHitBehavior
{
    public int Priority => 10;

    private int _remaining;

    public PenetrateBehavior(int count)
    {
        _remaining = count;
    }

    public void OnAttach(IHitInstance hit) { hit.OnHit += OnHitTarget; }
    public void OnDetach(IHitInstance hit) { hit.OnHit -= OnHitTarget; }

    private void OnHitTarget(IHitInstance hit, UnitController target)
    {
        if (_remaining > 0)
        {
            _remaining--;
            return;
        }

        hit.Despawn();
    }
}

public class HomingBehavior : IHitBehavior
{
    public int Priority => 20;

    private readonly UnitController _target;
    private readonly float _turnRate;

    public HomingBehavior(UnitController target, float turnRate)
    {
        _target = target;
        _turnRate = turnRate;
    }

    public void OnAttach(IHitInstance hit) { hit.OnTickFrame += Steer; }
    public void OnDetach(IHitInstance hit) { hit.OnTickFrame -= Steer; }

    private void Steer(IHitInstance hit, float dt)
    {
        if (_target == null || !_target.IsAlive) return;
        // Velocity steering requires access to the movement component.
        // Concrete implementation belongs in the Movement module (e.g. StraightMovement)
        // or in a HomingMovement variant. This scaffold is a no-op placeholder.
        _ = _turnRate;
    }
}

// ─── Position-overriding Behaviors (Priority 40+) ───────────────────────────
// These WRITE transform.position directly. Must not be combined with HomingBehavior.

// Placeholder — concrete implementations (Orbit, Falling) share a forbidden-combination
// guard with HomingBehavior. See combat-hit skill for the compatibility matrix.

// ─── Collision-reaction Behaviors (Priority 50) ─────────────────────────────

public class BounceBehavior : IHitBehavior
{
    public int Priority => 50;

    private int _remaining;

    public BounceBehavior(int count)
    {
        _remaining = count;
    }

    public void OnAttach(IHitInstance hit) { /* wire to Movement collision callback */ }
    public void OnDetach(IHitInstance hit) { /* unwire */ }

    // Concrete bounce reflection is implemented by the movement component
    // when it detects a wall collider; it should decrement _remaining and
    // Despawn when it hits zero.
    public bool TryConsume()
    {
        if (_remaining <= 0) return false;
        _remaining--;
        return true;
    }
}

// ─── Despawn-time Behaviors ─────────────────────────────────────────────────

public class SplitOnDespawnBehavior : IHitBehavior
{
    public int Priority => 60;

    private readonly int _childCount;
    private readonly float _spreadDegrees;
    private readonly System.Action<IHitInstance, HitSnapshot, int> _spawnChild;

    public SplitOnDespawnBehavior(int childCount, float spreadDegrees, System.Action<IHitInstance, HitSnapshot, int> spawnChild)
    {
        _childCount = childCount;
        _spreadDegrees = spreadDegrees;
        _spawnChild = spawnChild;
    }

    public void OnAttach(IHitInstance hit) { hit.OnDespawn += OnDespawn; }
    public void OnDetach(IHitInstance hit) { hit.OnDespawn -= OnDespawn; }

    private void OnDespawn(IHitInstance hit)
    {
        if (_spawnChild == null) return;
        if (hit.Snapshot == null) return;

        for (int i = 0; i < _childCount; i++)
        {
            _spawnChild(hit, hit.Snapshot, i);
        }
        _ = _spreadDegrees;
    }
}
