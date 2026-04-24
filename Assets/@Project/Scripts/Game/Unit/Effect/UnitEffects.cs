using System;
using System.Collections.Generic;
using Sigtrap.Relays;

[Serializable]
public class UnitEffects : UnitDataModule
{
    public Relay<DamageInfo, UnitController, UnitController> OnBeforeDealDamage = new();
    public Relay<DamageInfo, UnitController, UnitController> OnAfterDealDamage = new();
    public Relay<DamageInfo, UnitController, UnitController> OnBeforeTakeDamage = new();
    public Relay<DamageInfo, UnitController, UnitController> OnAfterTakeDamage = new();
    public Relay<DamageInfo, UnitController, UnitController> OnBeforeHeal = new();
    public Relay<DamageInfo, UnitController, UnitController> OnAfterHeal = new();
    public Relay<DamageInfo, UnitController, UnitController> OnBeforeTakeHeal = new();
    public Relay<DamageInfo, UnitController, UnitController> OnAfterTakeHeal = new();
    public Relay<HitSnapshot> OnFireHit = new();
    public Relay<UnitController, UnitController> OnKill = new();
    public Relay OnDeath = new();

    private readonly List<IEffect> _active = new();

    public IReadOnlyList<IEffect> Active => _active;

    public void Add(IEffect e)
    {
        if (e == null) return;

        var existing = _active.Find(x => x.Id == e.Id);
        if (existing != null)
        {
            if (existing is IStackableEffect s) s.OnStack();
            return;
        }

        _active.Add(e);
        e.OnAttach(_unitData);
    }

    public void Remove(IEffect e)
    {
        if (e == null) return;
        if (!_active.Remove(e)) return;
        e.OnDetach(_unitData);
    }

    public void RemoveBySource(object source)
    {
        if (source == null) return;

        for (int i = _active.Count - 1; i >= 0; i--)
        {
            if (_active[i] is ISourcedEffect se && ReferenceEquals(se.Source, source))
            {
                var e = _active[i];
                _active.RemoveAt(i);
                e.OnDetach(_unitData);
            }
        }
    }

    public void Tick(float deltaTime)
    {
        for (int i = _active.Count - 1; i >= 0; i--)
        {
            var e = _active[i];
            if (e is ITickEffect t) t.OnTick(deltaTime);
            if (e is IDurationEffect d && d.IsExpired)
            {
                _active.RemoveAt(i);
                e.OnDetach(_unitData);
            }
        }
    }

    public void Clear()
    {
        for (int i = _active.Count - 1; i >= 0; i--)
        {
            var e = _active[i];
            _active.RemoveAt(i);
            e.OnDetach(_unitData);
        }

        OnBeforeDealDamage.RemoveAll();
        OnAfterDealDamage.RemoveAll();
        OnBeforeTakeDamage.RemoveAll();
        OnAfterTakeDamage.RemoveAll();
        OnBeforeHeal.RemoveAll();
        OnAfterHeal.RemoveAll();
        OnBeforeTakeHeal.RemoveAll();
        OnAfterTakeHeal.RemoveAll();
        OnFireHit.RemoveAll();
        OnKill.RemoveAll();
        OnDeath.RemoveAll();
    }

    public void RaiseBeforeDealDamage(DamageInfo info, UnitController from, UnitController to) => OnBeforeDealDamage?.Dispatch(info, from, to);
    public void RaiseAfterDealDamage(DamageInfo info, UnitController from, UnitController to) => OnAfterDealDamage?.Dispatch(info, from, to);
    public void RaiseBeforeTakeDamage(DamageInfo info, UnitController from, UnitController to) => OnBeforeTakeDamage?.Dispatch(info, from, to);
    public void RaiseAfterTakeDamage(DamageInfo info, UnitController from, UnitController to) => OnAfterTakeDamage?.Dispatch(info, from, to);
    public void RaiseBeforeHeal(DamageInfo info, UnitController from, UnitController to) => OnBeforeHeal?.Dispatch(info, from, to);
    public void RaiseAfterHeal(DamageInfo info, UnitController from, UnitController to) => OnAfterHeal?.Dispatch(info, from, to);
    public void RaiseBeforeTakeHeal(DamageInfo info, UnitController from, UnitController to) => OnBeforeTakeHeal?.Dispatch(info, from, to);
    public void RaiseAfterTakeHeal(DamageInfo info, UnitController from, UnitController to) => OnAfterTakeHeal?.Dispatch(info, from, to);
    public void RaiseOnFireHit(HitSnapshot snap) => OnFireHit?.Dispatch(snap);
    public void RaiseKill(UnitController from, UnitController to) => OnKill?.Dispatch(from, to);
    public void RaiseDeath() => OnDeath?.Dispatch();
}
