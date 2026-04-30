using System.Collections.Generic;
using Library;
using SpecData;
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

    private UnitController _target;
    private int _targetVersion;
    private readonly float _turnRate;

    private const float BouncePauseDuration = 0f;
    private const float BezierRecalcInterval = 0.25f;
    private const float BezierMinDuration = 0.4f;
    private const float BezierControlScale = 0.3f;
    private const float RetargetCooldown = 0.05f;
    private const float BezierSuppressDuration = 0.4f;

    private BounceMovement _bounceMovement;
    private float _bouncePauseUntil;
    private float _lastRetargetAttempt;
    private float _bezierSuppressUntil;

    private Vector2 _bezP0, _bezP1, _bezP2;
    private float _bezT;
    private float _bezDuration;
    private float _lastBezierRecalcTime;
    private bool _bezierActive;

    public HomingBehavior(UnitController target, float turnRate)
    {
        _target = target;
        _targetVersion = target != null ? target.Version : -1;
        _turnRate = turnRate;
    }

    public void OnAttach(IHitInstance hit)
    {
        hit.OnTickFrame += Steer;

        var moving = hit as MovingHit;
        if (moving != null && moving.Movement is BounceMovement bm)
        {
            _bounceMovement = bm;
            _bounceMovement.OnBounced += HandleBounced;
        }

        _bouncePauseUntil = 0f;
        _bezierActive = false;
        _lastRetargetAttempt = -999f;
        _bezierSuppressUntil = 0f;
    }

    public void OnDetach(IHitInstance hit)
    {
        hit.OnTickFrame -= Steer;
        if (_bounceMovement != null)
        {
            _bounceMovement.OnBounced -= HandleBounced;
            _bounceMovement = null;
        }
    }

    private void HandleBounced()
    {
        _bouncePauseUntil = Time.time + BouncePauseDuration;
        // Invalidate current bezier — fresh one will be built when pause expires.
        _bezierActive = false;
    }

    private void Steer(IHitInstance hit, float dt)
    {
        if (hit.Snapshot == null || !hit.Snapshot.IsAttackerAlive()) return;
        if (Time.time < _bouncePauseUntil)
        {
            return;
        }

        var moving = hit as MovingHit;
        if (moving == null || moving.Movement == null) return;

        // Reacquire when target is dead/null/recycled. Version mismatch covers the case where
        // the target died and was re-pooled into a fresh unit (e.g. brick respawn) — IsAlive is
        // true again but the reference now points at a different logical unit.
        bool targetLost = _target == null
                          || !_target.IsAlive
                          || _target.Version != _targetVersion;
        if (targetLost)
        {
            if (Time.time - _lastRetargetAttempt < RetargetCooldown) return;
            _lastRetargetAttempt = Time.time;

            var newTarget = FindNearestEnemy(hit.Snapshot.Attacker, moving.transform.position);

            if (newTarget == null) return;
            _target = newTarget;
            _targetVersion = newTarget.Version;
            _bezierActive = false;
            _lastBezierRecalcTime = -999f;
            _bezierSuppressUntil = Time.time + BezierSuppressDuration;
        }

        Vector2 current = moving.Movement.GetVelocityDirection();
        if (current.sqrMagnitude < 0.0001f) return;

        Vector2 selfPos = moving.transform.position;
        Vector2 targetPos = _target.transform.position;

        // Build / refresh bezier. While suppressed (right after retarget) skip — direct toTarget homing
        // gives the turnRate cap full authority so U-turns actually swing instead of getting damped by
        // a forward-biased bezier tangent.
        bool bezierSuppressed = Time.time < _bezierSuppressUntil;
        bool needsRecalc = !bezierSuppressed
                          && (!_bezierActive
                              || _bezT >= 1f
                              || Time.time - _lastBezierRecalcTime >= BezierRecalcInterval);
        if (needsRecalc) RebuildBezier(selfPos, targetPos, current);
        if (bezierSuppressed) _bezierActive = false;

        Vector2 desired;
        if (_bezierActive)
        {
            _bezT += dt / _bezDuration;
            float t = Mathf.Clamp01(_bezT);
            // Quadratic bezier tangent: 2(1-t)(P1-P0) + 2t(P2-P1)
            Vector2 tangent = 2f * (1f - t) * (_bezP1 - _bezP0) + 2f * t * (_bezP2 - _bezP1);
            if (tangent.sqrMagnitude < 0.0001f) tangent = (targetPos - selfPos);
            desired = tangent.normalized;
        }
        else
        {
            Vector2 toTarget = targetPos - selfPos;
            if (toTarget.sqrMagnitude < 0.0001f) return;
            desired = toTarget.normalized;
        }

        float angle = Vector2.SignedAngle(current, desired);
        float maxDelta = _turnRate * dt;
        float clamped = Mathf.Clamp(angle, -maxDelta, maxDelta);

        Vector3 next3 = Quaternion.AngleAxis(clamped, Vector3.forward) * (Vector3)current;
        Vector2 next = new Vector2(next3.x, next3.y);
        moving.Movement.SetVelocityDirection(next);
    }

    private void RebuildBezier(Vector2 selfPos, Vector2 targetPos, Vector2 currentDir)
    {
        _bezP0 = selfPos;
        _bezP2 = targetPos;
        float dist = Vector2.Distance(selfPos, targetPos);
        // P1 extends current heading so curve start is tangent to current velocity.
        _bezP1 = selfPos + currentDir.normalized * (dist * BezierControlScale);
        // Use distance as a proxy for travel time; speed is ~unit/sec scale here.
        _bezDuration = Mathf.Max(BezierMinDuration, dist * BezierControlScale * 2f);
        _bezT = 0f;
        _lastBezierRecalcTime = Time.time;
        _bezierActive = true;
    }

    // Reacquire on target death: closest alive hostile unit (per attacker's EnemyLayer).
    // Routes through UnitSpawnHandler's active-unit list — O(active) instead of FindObjectsByType scene scan.
    private static UnitController FindNearestEnemy(UnitController attacker, Vector3 origin)
    {
        if (attacker == null) return null;
        var spawn = GameManager.Spawn;
        if (spawn == null) return null;
        return spawn.GetNearestEnemy(origin, attacker);
    }
}

// ─── OnHit-fanout Behaviors (Priority 15) ───────────────────────────────────

// Fans damage out from the just-hit unit to N nearest other enemies on each OnHit fire.
// Routes through attacker.DealDamage(...) so the full 6-layer pipeline (crit roll, resist,
// Effects hooks, OnDealDamage / OnTakeDamage relays) runs per chain target. The fire-time
// HitSnapshot is the source of truth for attacker — no re-read at hit time.
public class LightningChainBehavior : IHitBehavior
{
    public int Priority => 15;

    private readonly int _chainCount;
    private readonly float _damage;
    private readonly List<UnitController> _scratch = new List<UnitController>();

    public LightningChainBehavior(int chainCount, float damage)
    {
        _chainCount = Mathf.Max(0, chainCount);
        _damage = damage;
    }

    public void OnAttach(IHitInstance hit) { hit.OnHit += OnHitTarget; }
    public void OnDetach(IHitInstance hit) { hit.OnHit -= OnHitTarget; }

    private void OnHitTarget(IHitInstance hit, UnitController justHit)
    {
        if (_chainCount <= 0) return;
        if (hit.Snapshot == null || !hit.Snapshot.IsAttackerAlive()) return;

        var attacker = hit.Snapshot.Attacker;
        var spawn = GameManager.Spawn;
        if (spawn == null) return;

        Vector3 origin = justHit != null ? justHit.transform.position : hit.Snapshot.Origin;
        spawn.GetNearestEnemies(origin, attacker, justHit, _chainCount, _scratch);

        for (int i = 0; i < _scratch.Count; i++)
        {
            var t = _scratch[i];
            if (t == null || !t.IsAlive) continue;
            attacker.DealDamage(t, _damage, hit.Snapshot.DamageType, hit.Snapshot.AttackType);
        }
        _scratch.Clear();
    }
}

// Schedules N delayed damage pulses to whichever target this hit strikes.
// Each OnHit fires a fresh queue of N pulses spaced `interval` apart, with the
// first pulse `interval` seconds after impact (not immediate). Pulses fire on
// OnTickFrame; per-pulse alive + Version + attacker checks gate damage. Damage
// is applied via attacker.DealDamage so the full pipeline (crit/resist/Effects)
// runs. Several OnHits can stack their queues (e.g. with PenetrateBehavior).
public class BonusDamageBehavior : IHitBehavior
{
    public int Priority => 15;

    private readonly int _count;
    private readonly float _damage;
    private readonly float _interval;

    private struct PendingPulse
    {
        public UnitController target;
        public int targetVersion;
        public float fireAtTime;
    }

    private readonly List<PendingPulse> _pulses = new List<PendingPulse>();

    public BonusDamageBehavior(int count, float damage, float interval)
    {
        _count = Mathf.Max(0, count);
        _damage = damage;
        _interval = Mathf.Max(0f, interval);
    }

    public void OnAttach(IHitInstance hit)
    {
        hit.OnHit += OnHitTarget;
        hit.OnTickFrame += Tick;
    }

    public void OnDetach(IHitInstance hit)
    {
        hit.OnHit -= OnHitTarget;
        hit.OnTickFrame -= Tick;
        _pulses.Clear();
    }

    private void OnHitTarget(IHitInstance hit, UnitController target)
    {
        if (_count <= 0 || target == null) return;
        float now = Time.time;
        int version = target.Version;
        for (int i = 0; i < _count; i++)
        {
            _pulses.Add(new PendingPulse
            {
                target = target,
                targetVersion = version,
                fireAtTime = now + _interval * (i + 1),
            });
        }
    }

    private void Tick(IHitInstance hit, float dt)
    {
        if (_pulses.Count == 0) return;
        float now = Time.time;
        var snapshot = hit.Snapshot;

        for (int i = _pulses.Count - 1; i >= 0; i--)
        {
            var p = _pulses[i];
            if (now < p.fireAtTime) continue;

            _pulses.RemoveAt(i);

            if (snapshot == null || !snapshot.IsAttackerAlive()) continue;
            if (p.target == null || !p.target.IsAlive || p.target.Version != p.targetVersion) continue;

            snapshot.Attacker.DealDamage(p.target, _damage, snapshot.DamageType, snapshot.AttackType);
        }
    }
}

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

// Each OnHit fan-out spawns N child projectiles in a fan around the parent's
// current heading. Children use a separate hitInstanceId (their own SpecHitInstance,
// fetched fresh) but inherit attacker / damageType / attackType from the parent
// Snapshot. Children deliberately do NOT carry SpawnOnHitBehavior — depth is fixed
// at 1, so cascade is impossible regardless of the child spec's contents.
//
// Immediate re-hit prevention (the just-hit unit sits at spawn position): combined
// strategy — (a) spawn position is offset along the child's direction by SkinDistance,
// (b) for BounceMovement children, the just-hit unit's Collider2D is seeded into
// BounceMovement._piercedRecently for IgnoreFrames frames, so the sweep cast skips
// it during the first few ticks even with overlap.
public class SpawnOnHitBehavior : IHitBehavior
{
    public int Priority => 15;

    private const float SkinDistance = 0.4f;
    private const int   IgnoreFrames = 5;

    private readonly int _count;
    private readonly int _hitInstanceId;
    private readonly float _spreadAngleDeg;

    public SpawnOnHitBehavior(int count, int hitInstanceId, float spreadAngleDeg)
    {
        _count = Mathf.Max(0, count);
        _hitInstanceId = hitInstanceId;
        _spreadAngleDeg = spreadAngleDeg;
    }

    public void OnAttach(IHitInstance hit) { hit.OnHit += OnHitTarget; }
    public void OnDetach(IHitInstance hit) { hit.OnHit -= OnHitTarget; }

    private void OnHitTarget(IHitInstance hit, UnitController justHit)
    {
        if (_count <= 0 || _hitInstanceId == 0) return;
        var snapshot = hit.Snapshot;
        if (snapshot == null || !snapshot.IsAttackerAlive()) return;

        var moving = hit as MovingHit;
        if (moving == null) return;

        Vector2 parentDir = moving.Movement != null
            ? moving.Movement.GetVelocityDirection()
            : (Vector2)snapshot.Direction;
        if (parentDir.sqrMagnitude < 0.0001f) parentDir = Vector2.up;

        Vector2 spawnBase = moving.transform.position;

        // Fetch child spec; fall back to a synthetic spec built from the parent
        // Snapshot when the SpecHitInstance table doesn't carry the id (debug
        // panel path in particular — Spec*.json holds an empty table). This keeps
        // the 6-layer contract intact: child still flows through HitLauncher with
        // a real IDamageSpec, just with numbers inherited from the parent.
        SpecHitInstance childSpec = null;
        SpecDataManager.Instance.SpecHitInstance.TryGet(_hitInstanceId, out childSpec);

        var childDamageSpec = ChildDamageSpec.Get();
        childDamageSpec.Bind(childSpec, snapshot, _hitInstanceId);

        try
        {
            var collider = justHit != null ? justHit.Collider : null;
            for (int i = 0; i < _count; i++)
            {
                float t = _count == 1 ? 0.5f : (float)i / (_count - 1);
                float angleOffset = Mathf.Lerp(-_spreadAngleDeg * 0.5f, _spreadAngleDeg * 0.5f, t);
                Vector3 childDir = Quaternion.Euler(0f, 0f, angleOffset) * (Vector3)parentDir;
                Vector3 spawnPos = (Vector3)spawnBase + childDir.normalized * SkinDistance;

                MovingHit child = HitLauncher.FireProjectile(snapshot.Attacker, childDamageSpec, spawnPos, childDir);

                // Snapshot Speed/LifeTime/HitCount come from SpecHitInstance lookup
                // inside HitLauncher.Launch. When that lookup misses, those fields
                // are zero and the child despawns instantly. Patch them from the
                // parent snapshot so the child actually flies.
                if (child != null && child.Snapshot != null && childSpec == null)
                {
                    if (child.Snapshot.Speed <= 0f)    child.Snapshot.Speed    = snapshot.Speed;
                    if (child.Snapshot.LifeTime <= 0f) child.Snapshot.LifeTime = snapshot.LifeTime;
                    if (child.Snapshot.HitCount <= 0)  child.Snapshot.HitCount = snapshot.HitCount;
                }

                if (child != null && child.Movement is BounceMovement bm)
                {
                    if (collider != null) bm.IgnoreColliderForFrames(collider, IgnoreFrames);
                    // Inherit wall-bounce context from parent so children reflect off the
                    // same Rect / killLine as the parent (SRDebugger or BallSession-driven).
                    if (moving.Movement is BounceMovement parentBm) bm.CopyBoundsFrom(parentBm);
                }
            }
        }
        finally
        {
            childDamageSpec.Dispose();
        }
    }

    // Lightweight IDamageSpec adapter: child's spec drives damage numbers when
    // available; otherwise falls back to the parent Snapshot. DamageType/AttackType
    // always come from the parent Snapshot (Phase 5 inheritance rule). Pooled via
    // DisposeObject so a fan-out of N children allocates zero adapters.
    private sealed class ChildDamageSpec : DisposeObject<ChildDamageSpec>, IDamageSpec
    {
        private static readonly List<int> _empty = new List<int>();
        private SpecHitInstance _spec;
        private HitSnapshot _parentSnap;
        private int _hitInstanceId;

        public void Bind(SpecHitInstance spec, HitSnapshot parentSnap, int hitInstanceId)
        {
            _spec = spec;
            _parentSnap = parentSnap;
            _hitInstanceId = hitInstanceId;
        }

        protected override void Reset() { _spec = null; _parentSnap = null; _hitInstanceId = 0; }

        public float       range       => _spec != null ? _spec.range : 0f;
        public int         hitInstance => _spec != null ? _spec.id : _hitInstanceId;
        public eDamageType damageType  => _parentSnap != null ? _parentSnap.DamageType : default;
        public eAttackType attackType  => _parentSnap != null ? _parentSnap.AttackType : default;
        public float       baseDamage  => _spec != null ? _spec.baseDamage : (_parentSnap != null ? _parentSnap.BaseDamage : 0f);
        public float       basePercent
        {
            get
            {
                if (_spec != null) return _spec.basePercent <= 0f ? 1f : _spec.basePercent;
                return _parentSnap != null && _parentSnap.Percent > 0f ? _parentSnap.Percent : 1f;
            }
        }
        public List<int>   effects     => _empty;
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
