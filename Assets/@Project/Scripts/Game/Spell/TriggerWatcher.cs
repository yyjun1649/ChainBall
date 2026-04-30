using System.Collections.Generic;
using Cysharp.Text;
using Library;
using SpecData;
using UnityEngine;

namespace Game.Spell
{
    // Authority: Docs/Systems/Spell.md §4 + §8.
    // One per cast. Subscribes to a single HitInstance's events and fires the
    // SpecEffect bundle when SpecTrigger.eventType matches. Guards: cooldownTurn,
    // maxFiresPerCast.
    //
    // Currently supports HitInstance-bound events (BRICK_HIT / BRICK_KILL /
    // PROJECTILE_DESPAWN / NTH_BRICK_HIT / ELEMENT_MATCH / CONSECUTIVE_HIT).
    // Handlers.Event-bound events (WALL_BOUNCE / LINE_CLEAR / DANGER_PROXIMITY /
    // FULL_BOUNCE) need ChainBall channels which arrive in Phase 7.
    //
    // Pooled via DisposeObject<T> — `TriggerWatcher.Get()` / `watcher.Dispose()`
    // returns to the pool. Reset wires/unwires the HitInstance subscriptions.
    public sealed class TriggerWatcher : DisposeObject<TriggerWatcher>
    {
        private SpecTrigger _spec;
        private IReadOnlyList<SpecEffect> _effects;
        private IHitInstance _hit;
        private string _effectIdPrefix;
        private int _firesThisCast;
        private int _hitCounter;
        private int _cooldownLeft;

        public int FiresThisCast => _firesThisCast;

        public void Attach(SpecTrigger spec, IReadOnlyList<SpecEffect> effects, IHitInstance hit)
        {
            _spec = spec;
            _effects = effects;
            _hit = hit;
            _effectIdPrefix = spec != null
                ? ZString.Concat("trig_", spec.id, "_")
                : "trig__";

            if (hit == null) return;
            hit.OnHit += HandleHit;
            hit.OnDespawn += HandleDespawn;
        }

        // Turn-based cooldown tick — caller (TurnRunner / PhaseHandler) decides cadence.
        public void OnTurnAdvanced()
        {
            if (_cooldownLeft > 0) _cooldownLeft--;
        }

        protected override void Reset()
        {
            if (_hit != null)
            {
                _hit.OnHit -= HandleHit;
                _hit.OnDespawn -= HandleDespawn;
            }
            _hit = null;
            _spec = null;
            _effects = null;
            _effectIdPrefix = null;
            _firesThisCast = 0;
            _hitCounter = 0;
            _cooldownLeft = 0;
        }

        private void HandleHit(IHitInstance hit, UnitController target)
        {
            if (_spec == null) return;

            switch (_spec.eventType)
            {
                case eTriggerEvent.BRICK_HIT:
                    FireIfReady(target, hit);
                    break;

                case eTriggerEvent.BRICK_KILL:
                    if (target != null && !target.IsAlive) FireIfReady(target, hit);
                    break;

                case eTriggerEvent.NTH_BRICK_HIT:
                    _hitCounter++;
                    if (_spec.nthCount > 0 && _hitCounter >= _spec.nthCount)
                    {
                        _hitCounter = 0;
                        FireIfReady(target, hit);
                    }
                    break;

                case eTriggerEvent.ELEMENT_MATCH:
                    if (ElementMatches(hit, target)) FireIfReady(target, hit);
                    break;

                // Differs from NTH_BRICK_HIT: counter is NOT reset on fire.
                // Each subsequent hit past the threshold continues to fire (still
                // gated by maxFiresPerCast / cooldownTurn). Counter resets only on
                // despawn (Spell.md §8 "벽 반사 시 리셋" — wall-reflect reset is
                // pending the WALL_BOUNCE channel from Phase 7).
                case eTriggerEvent.CONSECUTIVE_HIT:
                    _hitCounter++;
                    if (_spec.nthCount > 0 && _hitCounter >= _spec.nthCount)
                        FireIfReady(target, hit);
                    break;

                // Phase 7 channels — silently ignore until ChainBall event bus exists.
                case eTriggerEvent.WALL_BOUNCE:
                case eTriggerEvent.LINE_CLEAR:
                case eTriggerEvent.DANGER_PROXIMITY:
                case eTriggerEvent.FULL_BOUNCE:
                    break;
            }
        }

        private void HandleDespawn(IHitInstance hit)
        {
            if (_spec == null) { Dispose(); return; }
            if (_spec.eventType == eTriggerEvent.PROJECTILE_DESPAWN)
                FireIfReady(target: null, hit);
            // Despawn is terminal for a HitInstance — return to pool to prevent
            // dangling subscriptions when the HitInstance is reused.
            Dispose();
        }

        private void FireIfReady(UnitController target, IHitInstance hit)
        {
            if (_spec.maxFiresPerCast > 0 && _firesThisCast >= _spec.maxFiresPerCast) return;
            if (_cooldownLeft > 0) return;

            FireEffects(target, hit);
            _firesThisCast++;
            _cooldownLeft = _spec.cooldownTurn;
        }

        private void FireEffects(UnitController target, IHitInstance hit)
        {
            if (_effects == null || _effects.Count == 0) return;

            var host = ResolveHost(target, hit);
            if (host == null) return;

            for (int i = 0; i < _effects.Count; i++)
            {
                var spec = _effects[i];
                if (spec == null) continue;

                var effect = EffectFactory.Create(spec, ZString.Concat(_effectIdPrefix, spec.id));
                if (effect == null) continue; // Phase 6 — creator not yet registered.
                host.Data.Effects.Add(effect);
            }
        }

        private static UnitController ResolveHost(UnitController target, IHitInstance hit)
        {
            if (target != null && target.IsAlive) return target;
            return hit?.Attacker;
        }

        private bool ElementMatches(IHitInstance hit, UnitController target)
        {
            if (hit?.Snapshot == null || target == null) return false;
            if (_spec.elementMatch == eElement.NONE) return true;

            if (!hit.Snapshot.Extra.TryGetValue(HitSnapshotKeys.Element, out var v)) return false;
            if (v is not eElement elem) return false;
            return elem == _spec.elementMatch;
        }
    }
}
