using System;
using System.Collections.Generic;
using Library;
using UnityEngine;

public abstract class HitInstance<T> : PoolMonoBehaviour<T>, IHitInstance
    where T : HitInstance<T>
{
    protected internal override string AddressFormat => "HitInstance_{0}";

    public HitSnapshot Snapshot { get; protected set; }
    public HitShape Shape { get; set; }
    public float Age { get; protected set; }
    public bool IsAlive { get; protected set; }

    public UnitController Attacker => Snapshot?.Attacker;

    public event Action<IHitInstance, UnitController> OnHit;
    public event Action<IHitInstance> OnDespawn;
    public event Action<IHitInstance, float> OnTickFrame;

    private readonly List<IHitBehavior> _behaviors = new List<IHitBehavior>();

    public virtual void Initialize(HitSnapshot snapshot, HitShape shape = null)
    {
        Snapshot = snapshot;
        Shape = shape;
        Age = 0f;
        IsAlive = true;

        if (snapshot != null)
        {
            transform.position = snapshot.Origin;
        }

        OnSpawn();
    }

    public void AddBehavior(IHitBehavior behavior)
    {
        if (behavior == null) return;

        int i = _behaviors.Count;
        while (i > 0 && _behaviors[i - 1].Priority > behavior.Priority) i--;
        _behaviors.Insert(i, behavior);

        if (IsAlive) behavior.OnAttach(this);
    }

    protected abstract void OnSpawn();
    protected abstract void Tick(float deltaTime);

    private void Update()
    {
        if (!IsAlive) return;

        var dt = Time.deltaTime;
        Age += dt;

        Tick(dt);

        OnTickFrame?.Invoke(this, dt);
    }

    protected void RaiseHit(UnitController target)
    {
        if (!IsAlive) return;
        if (target == null || !target.IsAlive) return;
        if (Snapshot == null || !Snapshot.IsAttackerAlive()) return;

        ApplyDamage(target);

        if (!IsAlive) return;

        OnHit?.Invoke(this, target);
    }

    private void ApplyDamage(UnitController target)
    {
        using (var ctx = DamageInfo.Get())
        {
            ctx.Attacker = Snapshot.Attacker;
            ctx.AttackerVersion = Snapshot.AttackerVersion;
            ctx.Target = target;
            ctx.TargetVersion = target.Version;
            ctx.BaseDamage = Snapshot.BaseDamage;
            ctx.Percent = Snapshot.Percent;
            ctx.DamageType = Snapshot.DamageType;
            ctx.AttackType = Snapshot.AttackType;

            if (Snapshot.IsCritical)
            {
                ctx.CriticalType = eCriticalType.Critical;
                ctx.CritMultiplier = Snapshot.CritMultiplier;
            }

            DamagePipeline.Process(ctx);

            if (ctx.Canceled || ctx.IsDodged) return;
            if (ctx.Attacker == null) return;

            ctx.Attacker.OnDealDamage.Dispatch(ctx, target);

            if (!target.IsAlive)
            {
                ctx.Attacker.Data.Effects.RaiseKill(ctx.Attacker, target);
            }
        }
    }

    public void Despawn()
    {
        if (!IsAlive) return;
        Release();
    }

    // Called by PoolMonoBehaviour.Release → ObjectPoolBase.Release path.
    // Single cleanup point covers both explicit Despawn() and direct Release() callers.
    public override void OnRelease()
    {
        if (IsAlive)
        {
            IsAlive = false;
            OnDespawn?.Invoke(this);
        }

        for (int i = _behaviors.Count - 1; i >= 0; i--)
        {
            _behaviors[i].OnDetach(this);
        }
        _behaviors.Clear();

        OnHit = null;
        OnDespawn = null;
        OnTickFrame = null;

        Snapshot?.Dispose();
        Snapshot = null;
        Shape = null;

        base.OnRelease();
    }
}
