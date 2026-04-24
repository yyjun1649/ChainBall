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
            if (effectId < 0)
            {
                continue;
            }
            
            _effects.Add(EffectFactory.Create(SpecEffect.GetDictionary()[effectId].SetParam(), effectId.ToString()));
        }
    }

    protected override void Reset()
    {
        _spec = default;
        _effects.Clear();
    }
}
