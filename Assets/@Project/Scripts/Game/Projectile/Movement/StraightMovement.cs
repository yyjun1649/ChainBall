using UnityEngine;

public class StraightMovement : ProjectileMovement
{
    [SerializeField] private float speed = 10f;
    [SerializeField] private float lifeTime = 5f;
    // legacy: SpellSequence 미경유 경로용. HomingBehavior 와 동시 사용 금지.
    [SerializeField] private float rotateSpeed = 0f;

    private Transform _target;
    private bool _targetIsNull;

    public void Setup(float moveSpeed, float lifeTime, Transform target = null, float rotateSpeed = 0f)
    {
        this.speed = moveSpeed;
        this.lifeTime = lifeTime;
        this.rotateSpeed = rotateSpeed;
        _target = target;

        _targetIsNull = _target == null;
    }

    private Vector3 _direction;
    private float _elapsed;

    public override void Initialize(MovingHit projectile, Vector3 direction)
    {
        base.Initialize(projectile, direction);

        _direction = direction.normalized;
        _elapsed = 0f;

        SetRotationFromDirection(_direction);
    }

    public override void Tick(float deltaTime)
    {
        _elapsed += deltaTime;

        if (!_targetIsNull && rotateSpeed > 0f)
        {
            Vector3 toTarget = (_target.position - transform.position).normalized;
            float angle = Vector3.SignedAngle(transform.up, toTarget, Vector3.forward);
            float maxDelta = rotateSpeed * deltaTime;
            angle = Mathf.Clamp(angle, -maxDelta, maxDelta);
            transform.rotation = Quaternion.AngleAxis(angle, Vector3.forward) * transform.rotation;
        }

        transform.position += transform.up * (speed * deltaTime);

        if (_elapsed >= lifeTime)
        {
            _projectile.Release();
        }
    }

    public void SetTarget(Transform target)
    {
        _target = target;
    }

    public override Vector2 GetVelocityDirection() => _direction;

    public override void SetVelocityDirection(Vector2 direction)
    {
        if (direction.sqrMagnitude < 0.0001f) return;
        _direction = direction.normalized;
        SetRotationFromDirection(_direction);
    }
}
