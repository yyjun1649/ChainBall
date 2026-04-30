using UnityEngine;

public abstract class ProjectileMovement : MonoBehaviour
{
    protected MovingHit _projectile;

    public virtual void Initialize(MovingHit projectile, Vector3 direction)
    {
        _projectile = projectile;
    }

    public abstract void Tick(float deltaTime);

    // Velocity steering hooks — read/write the current heading without coupling
    // HomingBehavior to a specific Movement subclass.
    public abstract Vector2 GetVelocityDirection();
    public abstract void SetVelocityDirection(Vector2 direction);

    protected void SetRotationFromDirection(Vector2 dir)
    {
        transform.rotation = Quaternion.Euler(0f, 0f, UtilCode.VectorToAngleSigned(dir));
    }
}
