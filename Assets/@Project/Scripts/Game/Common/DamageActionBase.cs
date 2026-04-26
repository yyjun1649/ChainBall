using System.Collections.Generic;
using SpecData;
using Library;

public abstract class DamageActionBase<TSpec> : PooledDisposable where TSpec : IDamageSpec
{
    protected TSpec _spec;
    protected readonly List<IEffect> _effects = new();

    public virtual void Initialize(TSpec spec)
    {
        _spec = spec;
        foreach (var effectId in _spec.effects)
        {
            if (effectId <= 0) continue;
            if (!SpecDataManager.Instance.SpecEffect.TryGet(effectId, out var effSpec)) continue;

            var eff = EffectFactory.Create(effSpec, effectId.ToString());
            if (eff != null) _effects.Add(eff);
        }
    }

    protected override void Reset()
    {
        _spec = default;
        _effects.Clear();
    }
}
