using UnityEngine;

public class StraightMovement : ProjectileMovement
{
    [SerializeField] private float speed = 10f;
    [SerializeField] private float lifeTime = 5f;
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

        float angle = Mathf.Atan2(_direction.y, _direction.x) * Mathf.Rad2Deg;
        transform.rotation = Quaternion.Euler(0f, 0f, angle);
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
}
