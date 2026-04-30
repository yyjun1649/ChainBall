using System;
using System.Collections.Generic;
using Library;
using SpecData;

public class UnitData : PooledDisposable
{
    public string unitName = String.Empty;
    public TStatContainer<eStatType> Stats { get; private set; }
    public UnitEffects Effects { get; private set; }

    public SpecCharacter SpecCharacter { get; private set; }

    // Optional per-domain metadata. Brick uses BrickMeta; Player / future units add their own.
    // Keeps UnitData generic — no domain-specific fields leak into the base type.
    private readonly Dictionary<Type, UnitDataModule> _modules = new();

    public T GetModule<T>() where T : UnitDataModule
        => _modules.TryGetValue(typeof(T), out var m) ? (T)m : null;

    public void AddModule(UnitDataModule module)
    {
        if (module == null) return;
        _modules[module.GetType()] = module;
    }

    public void Initialize(string name)
    {
        unitName = name;

        SpecCharacter ??= SpecDataManager.Instance.SpecCharacter.Get(unitName);
        Stats ??= new TStatContainer<eStatType>();
        Effects ??= new UnitEffects();

        Effects.Initialize(this, CalculateStat);

        RegisterBaseStats();
        CalculateStat();
    }

    // Brick / Player paths don't have a SpecCharacter row. Caller is responsible for
    // populating Stats afterward (e.g. UnitSpawnHandler adds Base_Health from cell notation).
    public void InitializeBare(string name)
    {
        unitName = name;

        Stats ??= new TStatContainer<eStatType>();
        Effects ??= new UnitEffects();

        Effects.Initialize(this, CalculateStat);
    }

    // TODO Phase 8 — ChainBall Player redesign.
    // Old SpecCharacter dummy carried 11 stat fields (health, meleeDamage, AttackSpeed, …);
    // new schema only exposes startHp. Other stats (damage etc.) are weapon-driven in ChainBall.
    // Phase 8 decides: keep TStatContainer for Brick HP / Player HP only, or replace entirely.
    private void RegisterBaseStats()
    {
        var s = SpecCharacter;
        const string baseSource = "Base";
        Stats.AddModifier(TStatModifier<eStatType>.MakeModifier(
            "Base_Health", eModifierType.Flat, eStatType.Health, s.startHp, false, baseSource));
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
        _modules.Clear();
    }
}
