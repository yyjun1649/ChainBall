using System;

public interface IHitInstance
{
    HitSnapshot Snapshot { get; }
    HitShape Shape { get; set; }
    float Age { get; }
    bool IsAlive { get; }
    UnitController Attacker { get; }

    event Action<IHitInstance, UnitController> OnHit;
    event Action<IHitInstance> OnDespawn;
    event Action<IHitInstance, float> OnTickFrame;

    void Initialize(HitSnapshot snapshot, HitShape shape = null);
    void AddBehavior(IHitBehavior behavior);
    void Despawn();
}
