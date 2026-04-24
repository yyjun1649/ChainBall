using UnityEngine;

public abstract class ProjectileMovement : MonoBehaviour
{
    protected MovingHit _projectile;

    public virtual void Initialize(MovingHit projectile, Vector3 direction)
    {
        _projectile = projectile;
    }

    public abstract void Tick(float deltaTime);
}
