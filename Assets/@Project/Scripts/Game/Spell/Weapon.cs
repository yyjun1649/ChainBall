using System;
using System.Collections.Generic;
using Cysharp.Text;
using SpecData;
using UnityEngine;

namespace Game.Spell
{
    // Authority: Docs/Systems/Weapon.md §1~§4, Roadmap §Phase 4.
    // Runtime weapon — owns a SpecWeapon + the equipped slot array. One Cast(...)
    // call corresponds to one TurnPhase.CAST: fires castsPerTurn SpellSequences
    // (or skips if cooldownLeft > 0), then applies cooldownTurns.
    public sealed class Weapon
    {
        private SpecWeapon _spec;
        private Spell[]    _slots;
        private int        _cooldownLeft;

        public SpecWeapon Spec => _spec;
        public IReadOnlyList<Spell> Slots => _slots;
        public int CooldownLeft => _cooldownLeft;
        public bool IsOnCooldown => _cooldownLeft > 0;

        // First PROJECTILE slot (per slotShape) must be filled. Other categories
        // are optional. (Weapon.md §2.)
        public bool CanCast
        {
            get
            {
                if (_spec == null || _slots == null) return false;
                int idx = FirstProjectileSlotIndex();
                if (idx < 0) return false;
                return _slots[idx]?.Projectile != null;
            }
        }

        public Weapon(SpecWeapon spec)
        {
            Initialize(spec);
        }

        public void Initialize(SpecWeapon spec)
        {
            _spec = spec;
            int count = spec != null ? Mathf.Max(0, spec.slotCount) : 0;
            _slots = new Spell[count];
            _cooldownLeft = 0;
        }

        public bool TryEquipSpell(int slotIndex, Spell spell)
        {
            if (_spec == null || _slots == null) return false;
            if (slotIndex < 0 || slotIndex >= _slots.Length) return false;
            if (spell == null)
            {
                _slots[slotIndex] = null;
                return true;
            }

            var allowed = SlotKindAt(slotIndex);
            if (!spell.FitsSlot(allowed))
            {
                Debug.LogWarning(ZString.Format(
                    "[Weapon] slot {0} expects {1}, got {2}.",
                    slotIndex, allowed, spell.Category));
                return false;
            }

            _slots[slotIndex] = spell;
            return true;
        }

        public Spell GetSlot(int slotIndex)
        {
            if (_slots == null || slotIndex < 0 || slotIndex >= _slots.Length) return null;
            return _slots[slotIndex];
        }

        // Performs a single TurnPhase.CAST. Fires castsPerTurn SpellSequences or
        // skips & ticks cooldown if currently locked. Returns count of sequences
        // actually launched (0 on cooldown / no projectile / null inputs).
        // aimDegrees: per-cast aim angles in degrees. If null/short, the last
        // available angle is reused. multiAngle handling is the caller's concern
        // — this method simply consumes the array.
        public int Cast(UnitController from, Vector3 origin, IReadOnlyList<float> aimDegrees, Action<MovingHit> onFired = null)
        {
            if (_spec == null || from == null) return 0;
            if (IsOnCooldown)
            {
                _cooldownLeft--;
                return 0;
            }
            if (!CanCast)
            {
                Debug.LogWarning(ZString.Format(
                    "[Weapon] {0} cannot cast — first PROJECTILE slot empty.", _spec?.id));
                return 0;
            }

            int castsPerTurn = Mathf.Max(1, _spec.castsPerTurn);
            int fired = 0;
            for (int i = 0; i < castsPerTurn; i++)
            {
                float angleDeg = ResolveAngle(aimDegrees, i);
                if (FireOnce(from, origin, angleDeg, onFired)) fired++;
            }

            if (fired > 0 && _spec.cooldownTurns > 0)
                _cooldownLeft = _spec.cooldownTurns;

            return fired;
        }

        // SwapTo — migrates compatible spells from current slots to a new SpecWeapon.
        // Spells that don't fit the new slotShape (or overflow slotCount) are
        // returned via overflow so the caller can shelve them in inventory.
        public void SwapTo(SpecWeapon newSpec, List<Spell> overflow = null)
        {
            if (newSpec == null) return;

            var oldSlots = _slots;
            int oldCount = oldSlots != null ? oldSlots.Length : 0;

            Initialize(newSpec);
            if (oldCount == 0) return;

            for (int i = 0; i < oldCount; i++)
            {
                var spell = oldSlots[i];
                if (spell == null) continue;

                if (!TryPlaceFirstFit(spell))
                    overflow?.Add(spell);
            }
        }

        private bool FireOnce(UnitController from, Vector3 origin, float aimDeg, Action<MovingHit> onFired)
        {
            var (proj, mods, trig, effs) = ExtractSlots();
            if (proj == null) return false;

            Vector3 dir = UtilCode.AngleToVector(aimDeg);

            var sequence = SpellSequence.Get();
            sequence.Initialize(proj, mods, trig, effs);
            int shots = sequence.Use(from, origin, dir, onFired);
            // Sequence returns to pool now. Watchers it created stay alive via
            // their own HitInstance subscriptions and self-dispose on despawn.
            sequence.Dispose();
            return shots > 0;
        }

        // Fresh lists per cast — TriggerWatchers hold the effect list across HitInstance
        // event lifetimes, so a static buffer would alias earlier casts and clear under us.
        private (SpecHitInstance, IReadOnlyList<SpecModifier>, SpecTrigger, IReadOnlyList<SpecEffect>) ExtractSlots()
        {
            SpecHitInstance proj = null;
            SpecTrigger trig = null;
            var mods = new List<SpecModifier>();
            var effs = new List<SpecEffect>();

            for (int i = 0; i < _slots.Length; i++)
            {
                var s = _slots[i];
                if (s == null) continue;

                switch (s.Category)
                {
                    case eSlotKind.PROJECTILE:
                        if (proj == null) proj = s.Projectile;
                        break;
                    case eSlotKind.TRIGGER:
                        if (trig == null) trig = s.Trigger;
                        break;
                    case eSlotKind.MODIFIER:
                        if (s.Modifier != null) mods.Add(s.Modifier);
                        break;
                    case eSlotKind.EFFECT:
                        if (s.Effect != null) effs.Add(s.Effect);
                        break;
                }
            }

            return (proj, mods, trig, effs);
        }

        private bool TryPlaceFirstFit(Spell spell)
        {
            for (int i = 0; i < _slots.Length; i++)
            {
                if (_slots[i] != null) continue;
                if (!spell.FitsSlot(SlotKindAt(i))) continue;
                _slots[i] = spell;
                return true;
            }
            return false;
        }

        private eSlotKind SlotKindAt(int slotIndex)
        {
            if (_spec?.slotShape == null || slotIndex >= _spec.slotShape.Length) return eSlotKind.ANY;
            return _spec.slotShape[slotIndex];
        }

        private int FirstProjectileSlotIndex()
        {
            if (_spec?.slotShape == null) return -1;
            for (int i = 0; i < _spec.slotShape.Length; i++)
            {
                var k = _spec.slotShape[i];
                if (k == eSlotKind.PROJECTILE || k == eSlotKind.ANY) return i;
            }
            return -1;
        }

        private static float ResolveAngle(IReadOnlyList<float> aimDegrees, int castIndex)
        {
            if (aimDegrees == null || aimDegrees.Count == 0) return 90f; // default: straight up
            if (castIndex < aimDegrees.Count) return aimDegrees[castIndex];
            return aimDegrees[aimDegrees.Count - 1];
        }
    }
}
