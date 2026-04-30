using System;
using System.Collections.Generic;
using Cysharp.Text;
using Library;
using SpecData;
using UnityEngine;

namespace Game.Spell
{
    // Authority: Docs/Roadmap.md §Phase 3, Docs/Systems/Spell.md §2~§7.
    // One cast → one SpellSequence.
    //   - Initialize(Projectile spec, Modifiers, Trigger, Effects) once per cast.
    //   - Use(from, origin, dir) fires multiShot projectiles, each with the modifier-
    //     patched HitSnapshot, the bounce/pierce/PIERCE_ON_HIT behaviors, and a
    //     TriggerWatcher that turns Trigger events into Effect emissions.
    //   - Dispose() releases all attached watchers (idempotent; watchers self-dispose
    //     on HitInstance despawn so this is a safety net for sequences that never
    //     fully resolve).
    //
    // Pooled via DisposeObject<T> — `SpellSequence.Get()` / `seq.Dispose()`.
    public sealed class SpellSequence : DisposeObject<SpellSequence>
    {
        public const int MAX_PROJECTILES_PER_CAST = 64;

        private SpecHitInstance _hitInstance;
        private IReadOnlyList<SpecModifier> _modifiers;
        private SpecTrigger _trigger;
        private IReadOnlyList<SpecEffect> _effects;
        private readonly List<TriggerWatcher> _activeWatchers = new();

        public IReadOnlyList<TriggerWatcher> ActiveWatchers => _activeWatchers;

        public void Initialize(
            SpecHitInstance projectile,
            IReadOnlyList<SpecModifier> modifiers,
            SpecTrigger trigger,
            IReadOnlyList<SpecEffect> effects)
        {
            _hitInstance = projectile;
            _modifiers = modifiers;
            _trigger = trigger;
            _effects = effects;
        }

        // onFired: optional per-spawn hook so callers can attach context that
        // SpellSequence doesn't own (e.g. BounceMovement.AttachContext bounds).
        public int Use(UnitController from, Vector3 origin, Vector3 direction, Action<MovingHit> onFired = null)
        {
            if (_hitInstance == null || from == null) return 0;

            var patch = SnapshotPatch.Get().Apply(_modifiers);
            // Hoist: spec adapter is constant for the whole cast.
            var damageSpec = HitInstanceDamageSpec.Get();
            damageSpec.SetSpec(_hitInstance);

            try
            {
                int multi = Mathf.Max(1, _hitInstance.multiShot);
                for (int i = 0; i < patch.Behaviors.Count; i++)
                {
                    if (patch.Behaviors[i].Kind == eModifierBehavior.CLONE_AT_FIRE)
                        multi += Mathf.Max(1, patch.Behaviors[i].Param1);
                }

                if (multi > MAX_PROJECTILES_PER_CAST)
                {
                    Debug.LogWarning(ZString.Format(
                        "[SpellSequence] multiShot {0} exceeds cap {1}; clamping.",
                        multi, MAX_PROJECTILES_PER_CAST));
                    multi = MAX_PROJECTILES_PER_CAST;
                }

                float spread = _hitInstance.spreadAngle;
                int fired = 0;
                for (int i = 0; i < multi; i++)
                {
                    Vector3 dir = ApplySpread(direction, spread, i, multi);
                    if (FireOne(from, origin, dir, patch, damageSpec, onFired)) fired++;
                }
                return fired;
            }
            finally
            {
                patch.Dispose();
                damageSpec.Dispose();
            }
        }

        protected override void Reset()
        {
            // Don't dispose watchers here — they own their own lifetime via the
            // HitInstance's OnDespawn (TriggerWatcher.HandleDespawn → Dispose).
            // Disposing them now would unsubscribe before trigger events can fire.
            // We just relinquish the bookkeeping list so the sequence can be reused.
            _activeWatchers.Clear();
            _hitInstance = null;
            _modifiers = null;
            _trigger = null;
            _effects = null;
        }

        private bool FireOne(UnitController from, Vector3 origin, Vector3 dir, SnapshotPatch patch, IDamageSpec damageSpec, Action<MovingHit> onFired)
        {
            var instance = Handlers.Pool.Get<MovingHit>(_hitInstance.id);
            if (instance == null) return false;

            var snap = HitSnapshotBuilder.Build(from, damageSpec, _hitInstance, origin, dir, target: null);
            patch.ApplyTo(snap, _hitInstance);

            instance.Initialize(snap, shape: null);

            int bounce = Mathf.Max(0, _hitInstance.bounceCount + patch.BounceDelta);
            int pierce = Mathf.Max(0, _hitInstance.pierceCount + patch.PierceDelta);
            if (bounce > 0) instance.AddBehavior(new BounceBehavior(bounce));

            // Pierce semantics differ by movement: BounceMovement passes through
            // bricks (skips reflection), straight projectiles use the despawn-on-N
            // PenetrateBehavior path.
            if (pierce > 0) ApplyPierce(instance, pierce);

            for (int i = 0; i < patch.Behaviors.Count; i++)
            {
                var entry = patch.Behaviors[i];
                if (entry.Kind == eModifierBehavior.PIERCE_ON_HIT)
                {
                    ApplyPierce(instance, int.MaxValue);
                }
                // SPLIT / CHAIN / FREEZE_ROW — Phase 5.
            }

            if (_trigger != null && _effects != null && _effects.Count > 0)
            {
                var watcher = TriggerWatcher.Get();
                watcher.Attach(_trigger, _effects, instance);
                _activeWatchers.Add(watcher);
            }

            onFired?.Invoke(instance);
            return true;
        }

        private static void ApplyPierce(MovingHit instance, int count)
        {
            if (instance.Movement is BounceMovement bounce)
                bounce.SetPierceCount(count);
            else
                instance.AddBehavior(new PenetrateBehavior(count));
        }

        private static Vector3 ApplySpread(Vector3 baseDir, float spreadDegrees, int index, int total)
        {
            if (total <= 1 || spreadDegrees <= 0f) return baseDir;
            float t = index / (float)(total - 1);
            float angle = Mathf.Lerp(-spreadDegrees * 0.5f, spreadDegrees * 0.5f, t);
            return Quaternion.Euler(0f, 0f, angle) * baseDir;
        }

        // SpecHitInstance lacks damageType / attackType (v0.1 schema decision, Roadmap §Phase 1)
        // and exposes effects as int[] rather than List<int>. Adapter bridges both gaps.
        // Pooled to avoid per-cast alloc; SetSpec rebinds for each Use.
        private sealed class HitInstanceDamageSpec : DisposeObject<HitInstanceDamageSpec>, IDamageSpec
        {
            private static readonly List<int> _empty = new List<int>();
            private SpecHitInstance _spec;

            public void SetSpec(SpecHitInstance spec) { _spec = spec; }

            protected override void Reset() { _spec = null; }

            public float       range       => _spec.range;
            public int         hitInstance => _spec.id;
            public eDamageType damageType  => default;
            public eAttackType attackType  => default;
            public float       baseDamage  => _spec.baseDamage;
            public float       basePercent => _spec.basePercent <= 0f ? 1f : _spec.basePercent;
            // Effect bundle for the cast comes via SpellSequence._effects → TriggerWatcher,
            // not via IDamageSpec.effects. Empty here is intentional.
            public List<int>   effects     => _empty;
        }
    }
}
