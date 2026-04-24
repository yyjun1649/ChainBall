public interface IEffect
{
    string Id { get; }
    void OnAttach(UnitData unit);
    void OnDetach(UnitData unit);
}

public interface ITickEffect : IEffect
{
    void OnTick(float deltaTime);
}

public interface IDurationEffect : IEffect
{
    float RemainingTime { get; }
    bool IsExpired { get; }
}

public interface IStackableEffect : IEffect
{
    int Stacks { get; }
    int MaxStacks { get; }
    void OnStack();
}

public interface ISourcedEffect : IEffect
{
    object Source { get; }
}

public class StatModifierEffect : IEffect, ISourcedEffect
{
    private readonly string _effectName;
    private readonly eStatType _stat;
    private readonly float _value;
    private readonly eModifierType _modifierType;
    private readonly object _source;
    private TStatModifier<eStatType> _modifier;

    public string Id => _effectName;
    public object Source => _source;

    public StatModifierEffect(string effectName, eStatType stat, float value, eModifierType modifierType, object source = null)
    {
        _effectName = effectName;
        _stat = stat;
        _value = value;
        _modifierType = modifierType;
        _source = source;
    }

    public void OnAttach(UnitData unit)
    {
        _modifier = TStatModifier<eStatType>.MakeModifier(_effectName, _modifierType, _stat, _value, true, this);
        unit.Stats.AddModifier(_modifier);
        unit.Stats.CalculateStat();
    }

    public void OnDetach(UnitData unit)
    {
        unit.Stats.RemoveBySource(this);
        unit.Stats.CalculateStat();
        _modifier = null;
    }
}
