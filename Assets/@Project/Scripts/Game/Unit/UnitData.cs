using System;
using Library;
using SpecData;

public class UnitData : PooledDisposable
{
    public string unitName = String.Empty;
    public TStatContainer<eStatType> Stats { get; private set; }
    public UnitEffects Effects { get; private set; }

    public SpecCharacter SpecCharacter { get; private set; }

    public void Initialize(string name)
    {
        unitName = name;

        SpecCharacter ??= SpecCharacter.GetDictionary()[unitName];
        Stats ??= new TStatContainer<eStatType>();
        Effects ??= new UnitEffects();

        Effects.Initialize(this, CalculateStat);

        RegisterBaseStats();
        CalculateStat();
    }

    private void RegisterBaseStats()
    {
        var s = SpecCharacter;
        const string baseSource = "Base";
        Stats.AddModifier(TStatModifier<eStatType>.MakeModifier("Base_Health",         eModifierType.Flat, eStatType.Health,         s.health,         false, baseSource));
        Stats.AddModifier(TStatModifier<eStatType>.MakeModifier("Base_MeleeDamage",    eModifierType.Flat, eStatType.MeleeDamage,    s.meleeDamage,    false, baseSource));
        Stats.AddModifier(TStatModifier<eStatType>.MakeModifier("Base_MagicDamage",    eModifierType.Flat, eStatType.MagicDamage,    s.magicDamage,    false, baseSource));
        Stats.AddModifier(TStatModifier<eStatType>.MakeModifier("Base_AttackSpeed",    eModifierType.Flat, eStatType.AttackSpeed,    s.AttackSpeed,    false, baseSource));
        Stats.AddModifier(TStatModifier<eStatType>.MakeModifier("Base_MoveSpeed",      eModifierType.Flat, eStatType.MoveSpeed,      s.MoveSpeed,      false, baseSource));
        Stats.AddModifier(TStatModifier<eStatType>.MakeModifier("Base_CriticalChance", eModifierType.Flat, eStatType.CriticalChance, s.CriticalChance, false, baseSource));
        Stats.AddModifier(TStatModifier<eStatType>.MakeModifier("Base_CriticalDamage", eModifierType.Flat, eStatType.CriticalDamage, s.CriticalDamage, false, baseSource));
        Stats.AddModifier(TStatModifier<eStatType>.MakeModifier("Base_LifeSteal",      eModifierType.Flat, eStatType.LifeSteal,      s.LifeSteal,      false, baseSource));
        Stats.AddModifier(TStatModifier<eStatType>.MakeModifier("Base_Defense",        eModifierType.Flat, eStatType.Defense,        s.Defense,        false, baseSource));
        Stats.AddModifier(TStatModifier<eStatType>.MakeModifier("Base_HealRating",     eModifierType.Flat, eStatType.HealRating,     s.HealRate,       false, baseSource));
        Stats.AddModifier(TStatModifier<eStatType>.MakeModifier("Base_Range",          eModifierType.Flat, eStatType.Range,          s.Range,          false, baseSource));
    }

    public void CalculateStat()
    {
        Stats.CalculateStat();
    }

    protected override void Reset()
    {
        SpecCharacter = null;
        Effects?.Clear();
        Stats?.Initialize();
    }
}
