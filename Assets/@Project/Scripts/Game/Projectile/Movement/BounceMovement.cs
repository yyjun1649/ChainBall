using Library;
using UnityEngine;

public class BounceMovement : ProjectileMovement
{
    private enum Mode { Bounce, AbsorbToPlayer }

    [SerializeField] private float _speed    = 15f;
    [SerializeField] private float _lifeTime = 8f;
    [SerializeField] private float _absorbSpeedMultiplier = 1.5f;
    [SerializeField] private float _skin     = 0.01f;

    private Rect         _bounds;
    private bool         _hasBounds;
    private float        _killLineY;
    private BallSession  _session;
    private float        _radius;
    private Collider2D   _selfCollider;

    private Vector2 _dir;
    private float   _elapsed;
    private Mode    _mode;
    private Vector2 _absorbTarget;
    private bool    _killLineReported;
    private int     _pierceLeft;

    // Pierced-recently ignore list — without this, a brick wider than a single
    // step would be hit again next frame while the ball is still overlapping it,
    // either consuming an extra pierce charge or reflecting from inside the brick.
    private struct PierceMark { public Collider2D Collider; public int FramesLeft; }
    private readonly PierceMark[] _piercedRecently = new PierceMark[4];
    private const int _pierceIgnoreFrames = 3;

    // Single shared buffer — TickBounce is called from Unity's main-thread update
    // loop, one MovingHit at a time, so reuse is safe.
    private static readonly RaycastHit2D[] _hitBuffer = new RaycastHit2D[8];

    public void SetupFromSnapshot(float speed, float lifeTime)
    {
        if (speed > 0f)    _speed    = speed;
        if (lifeTime > 0f) _lifeTime = lifeTime;
    }

    public void AttachContext(Rect bounds, float killLineY, BallSession session)
    {
        _bounds    = bounds;
        _hasBounds = bounds.width > 0f && bounds.height > 0f;
        _killLineY = killLineY;
        _session   = session;
        _mode      = Mode.Bounce;
        _killLineReported = false;
    }

    public void EnterAbsorbMode(Vector2 playerPos)
    {
        _mode = Mode.AbsorbToPlayer;
        _absorbTarget = playerPos;
    }

    // ChainBall pierce semantics: each unit hit consumes 1 charge, deals damage,
    // and skips reflection so the ball passes through. Wall hits always reflect.
    public void SetPierceCount(int count)
    {
        _pierceLeft = Mathf.Max(0, count);
    }

    public override void Initialize(MovingHit projectile, Vector3 direction)
    {
        base.Initialize(projectile, direction);
        _dir = ((Vector2)direction).normalized;
        if (_dir.sqrMagnitude < 0.0001f) _dir = Vector2.up;
        _elapsed = 0f;
        // Pool reuse safety: clear context so a no-AttachContext path (e.g. SpellSequence)
        // doesn't inherit bounds/session from a prior cast that did attach.
        _bounds = default;
        _hasBounds = false;
        _killLineY = 0f;
        _session = null;
        _killLineReported = false;
        _mode = Mode.Bounce;
        _pierceLeft = 0;
        for (int i = 0; i < _piercedRecently.Length; i++) _piercedRecently[i] = default;

        _selfCollider = projectile.Collider;
        if (projectile.TryGetComponent<CircleCollider2D>(out var circle))
        {
            float scale = Mathf.Max(transform.lossyScale.x, transform.lossyScale.y);
            _radius = circle.radius * scale;
        }
        else
        {
            _radius = 0.25f;
        }

        SetRotationFromDirection(_dir);
    }

    public override void Tick(float deltaTime)
    {
        _elapsed += deltaTime;
        if (_elapsed >= _lifeTime)
        {
            _projectile.Release();
            return;
        }

        if (_mode == Mode.Bounce)
        {
            TickBounce(deltaTime);
        }
        else
        {
            TickAbsorb(deltaTime);
        }
    }

    private bool IsPiercedRecently(Collider2D c)
    {
        for (int i = 0; i < _piercedRecently.Length; i++)
            if (_piercedRecently[i].Collider == c) return true;
        return false;
    }

    private void MarkPierced(Collider2D c)
    {
        int slot = -1;
        int oldestFramesLeft = int.MaxValue;
        for (int i = 0; i < _piercedRecently.Length; i++)
        {
            if (_piercedRecently[i].Collider == null) { slot = i; break; }
            if (_piercedRecently[i].FramesLeft < oldestFramesLeft)
            {
                oldestFramesLeft = _piercedRecently[i].FramesLeft;
                slot = i;
            }
        }
        _piercedRecently[slot] = new PierceMark { Collider = c, FramesLeft = _pierceIgnoreFrames };
    }

    private void TickPierceMarks()
    {
        for (int i = 0; i < _piercedRecently.Length; i++)
        {
            if (_piercedRecently[i].Collider == null) continue;
            var m = _piercedRecently[i];
            m.FramesLeft--;
            if (m.FramesLeft <= 0) m.Collider = null;
            _piercedRecently[i] = m;
        }
    }

    private void TickBounce(float deltaTime)
    {
        Vector2 pos  = transform.position;
        float   step = _speed * deltaTime;
        TickPierceMarks();

        // Sweep: detect collider hits along the next-step path. Uses attacker's EnemyLayer
        // as the mask, so the ball never hits itself or the player. Continuous detection
        // eliminates corner / fast-ball tunneling that trigger overlap can't catch.
        LayerMask mask = _projectile.Snapshot != null && _projectile.Snapshot.Attacker != null
            ? _projectile.Snapshot.Attacker.EnemyLayer
            : (LayerMask)~0;

        // CircleCastNonAlloc — pierce can consume multiple bricks in a single
        // step, so we need every hit along the path, not just the first one.
        int count = Physics2D.CircleCastNonAlloc(pos, _radius, _dir, _hitBuffer, step, mask);
        // Insertion-sort by distance ascending — Unity's NonAlloc variant doesn't
        // guarantee ordering. Buffer is small (8) so n^2 cost is negligible.
        for (int i = 1; i < count; i++)
        {
            var v = _hitBuffer[i];
            int j = i - 1;
            while (j >= 0 && _hitBuffer[j].distance > v.distance)
            {
                _hitBuffer[j + 1] = _hitBuffer[j];
                j--;
            }
            _hitBuffer[j + 1] = v;
        }

        bool reflected = false;
        for (int i = 0; i < count; i++)
        {
            var hit = _hitBuffer[i];
            if (hit.collider == null || hit.collider == _selfCollider) continue;
            if (IsPiercedRecently(hit.collider)) continue;

            bool isUnit = MappingHelperManager.Instance.Unit.TryGet(hit.collider, out var unit)
                          && unit.IsAlive
                          && _projectile.Snapshot?.Attacker != null
                          && unit.MyLayer != _projectile.Snapshot.Attacker.MyLayer;

            if (isUnit) _projectile.NotifyHitFromMovement(unit);

            // Pierce: pass through the unit, mark it ignored for the next few
            // frames (so a still-overlapping brick doesn't consume more charges),
            // and keep scanning for the next hit. Walls always reflect.
            if (isUnit && _pierceLeft > 0)
            {
                _pierceLeft--;
                MarkPierced(hit.collider);
                continue;
            }

            pos = hit.point + hit.normal * (_radius + _skin);
            _dir = Vector2.Reflect(_dir, hit.normal);
            reflected = true;
            break;
        }

        if (!reflected) pos += _dir * step;

        // Wall reflection (Rect — prototype walls are not colliders).
        // Skipped entirely when bounds were never attached (degenerate Rect),
        // otherwise the default-(0,0,0,0) ceiling reflects the ball straight
        // down on the first frame.
        if (_hasBounds)
        {
            if (pos.x < _bounds.xMin)      { pos.x = _bounds.xMin; _dir = Vector2.Reflect(_dir, Vector2.right); }
            else if (pos.x > _bounds.xMax) { pos.x = _bounds.xMax; _dir = Vector2.Reflect(_dir, Vector2.left); }
            if (pos.y > _bounds.yMax)      { pos.y = _bounds.yMax; _dir = Vector2.Reflect(_dir, Vector2.down); }
        }

        transform.position = pos;

        SetRotationFromDirection(_dir);

        if (_hasBounds && !_killLineReported && pos.y <= _killLineY)
        {
            _killLineReported = true;
            _session?.OnBallReachedKillLine(_projectile, pos);
        }
    }

    private void TickAbsorb(float deltaTime)
    {
        Vector2 pos = transform.position;
        Vector2 toTarget = _absorbTarget - pos;
        float distSq = toTarget.sqrMagnitude;

        float step = _speed * _absorbSpeedMultiplier * deltaTime;
        if (distSq <= step * step + 0.0025f)
        {
            _projectile.Release();
            return;
        }

        _dir = toTarget.normalized;
        transform.position = pos + _dir * step;
    }
}
