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
    private float        _killLineY;
    private BallSession  _session;
    private float        _radius;
    private Collider2D   _selfCollider;

    private Vector2 _dir;
    private float   _elapsed;
    private Mode    _mode;
    private Vector2 _absorbTarget;
    private bool    _killLineReported;

    public void SetupFromSnapshot(float speed, float lifeTime)
    {
        if (speed > 0f)    _speed    = speed;
        if (lifeTime > 0f) _lifeTime = lifeTime;
    }

    public void AttachContext(Rect bounds, float killLineY, BallSession session)
    {
        _bounds    = bounds;
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

    public override void Initialize(MovingHit projectile, Vector3 direction)
    {
        base.Initialize(projectile, direction);
        _dir = ((Vector2)direction).normalized;
        if (_dir.sqrMagnitude < 0.0001f) _dir = Vector2.up;
        _elapsed = 0f;

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

    private void TickBounce(float deltaTime)
    {
        Vector2 pos  = transform.position;
        float   step = _speed * deltaTime;

        // Sweep: detect collider hits along the next-step path. Uses attacker's EnemyLayer
        // as the mask, so the ball never hits itself or the player. Continuous detection
        // eliminates corner / fast-ball tunneling that trigger overlap can't catch.
        LayerMask mask = _projectile.Snapshot != null && _projectile.Snapshot.Attacker != null
            ? _projectile.Snapshot.Attacker.EnemyLayer
            : (LayerMask)~0;

        var hit = Physics2D.CircleCast(pos, _radius, _dir, step, mask);
        if (hit.collider != null && hit.collider != _selfCollider)
        {
            // Damage path: route to MovingHit's pipeline.
            if (MappingHelperManager.Instance.Unit.TryGet(hit.collider, out var unit)
                && unit.IsAlive
                && unit.MyLayer != _projectile.Snapshot.Attacker.MyLayer)
            {
                _projectile.NotifyHitFromMovement(unit);
            }
            
            pos = hit.point + hit.normal * (_radius + _skin);
            _dir = Vector2.Reflect(_dir, hit.normal);
        }
        else
        {
            pos += _dir * step;
        }

        // Wall reflection (Rect — prototype walls are not colliders).
        if (pos.x < _bounds.xMin) { pos.x = _bounds.xMin; _dir = Vector2.Reflect(_dir, Vector2.right); }
        else if (pos.x > _bounds.xMax) { pos.x = _bounds.xMax; _dir = Vector2.Reflect(_dir, Vector2.left); }
        if (pos.y > _bounds.yMax) { pos.y = _bounds.yMax; _dir = Vector2.Reflect(_dir, Vector2.down); }

        transform.position = pos;

        SetRotationFromDirection(_dir);

        if (!_killLineReported && pos.y <= _killLineY)
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
