using Library;

public class TStatModifier<T> : PooledDisposable
{
    public string ModifierId { get; private set; }
    public object Source { get; private set; }
    public eModifierType ModifierType { get; private set; }
    public T StatType { get; private set; }
    public float Value { get; set; }
    public bool IsStackable { get; private set; }

    public void Set(string name, eModifierType modifierType, T statType, float value, bool isStackable = false, object source = null)
    {
        ModifierId = name;
        Source = source;
        ModifierType = modifierType;
        StatType = statType;
        Value = value;
        IsStackable = isStackable;
    }

    protected override void Reset()
    {
        ModifierId = string.Empty;
        Source = null;
        ModifierType = eModifierType.Flat;
        Value = 0;
        IsStackable = false;
    }

    public static TStatModifier<T> MakeModifier(string id, eModifierType modifierType, T statType, float value, bool isStackable = false, object source = null)
    {
        var modifier = Get<TStatModifier<T>>();

        modifier.Set(id, modifierType, statType, value, isStackable, source);

        return modifier;
    }
}
