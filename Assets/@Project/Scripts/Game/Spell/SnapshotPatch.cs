using System.Collections.Generic;
using Library;
using SpecData;
using UnityEngine;

namespace Game.Spell
{
    // Shared HitSnapshot.Extra keys produced/consumed by the spell pipeline.
    // SnapshotPatch writes them; TriggerWatcher and downstream behaviors read them.
    public static class HitSnapshotKeys
    {
        public const string HitWidth = "hitWidth";
        public const string Element  = "element";
    }

    // Authority: Docs/Systems/Spell.md §3 / §7.1
    // Modifier accumulator → patches a base HitSnapshot at fire-time.
    // Order is fixed: sum (delta) → product (mul) → clamp by max(damageMin).
    // Pooled via DisposeObject<T> — `SnapshotPatch.Get()` / `patch.Dispose()`.
    public sealed class SnapshotPatch : DisposeObject<SnapshotPatch>
    {
        private int _damageDelta;
        private float _damageMul = 1f;
        private int _damageMinMax;
        private int _bounceDelta;
        private int _pierceDelta;
        private float _hitWidthMul = 1f;
        private float _speedMul = 1f;
        private eElement _element = eElement.NONE;
        private readonly List<BehaviorEntry> _behaviors = new();

        public int BounceDelta => _bounceDelta;
        public int PierceDelta => _pierceDelta;
        public float SpeedMul => _speedMul;
        public float HitWidthMul => _hitWidthMul;
        public eElement Element => _element;
        public IReadOnlyList<BehaviorEntry> Behaviors => _behaviors;

        public SnapshotPatch Apply(SpecModifier mod)
        {
            if (mod == null) return this;

            _damageDelta += mod.damageDelta;
            // 0 in *Mul fields is treated as identity (empty cell from xlsx → default(float) = 0).
            if (mod.damageMul > 0f) _damageMul *= mod.damageMul;
            if (mod.damageMin > _damageMinMax) _damageMinMax = mod.damageMin;
            _bounceDelta += mod.bounceDelta;
            _pierceDelta += mod.pierceDelta;
            if (mod.hitWidthMul > 0f) _hitWidthMul *= mod.hitWidthMul;
            if (mod.speedMul > 0f) _speedMul *= mod.speedMul;
            if (mod.element != eElement.NONE) _element = mod.element;
            if (mod.behavior != eModifierBehavior.NONE)
            {
                _behaviors.Add(new BehaviorEntry(
                    mod.behavior, mod.behaviorParam1, mod.behaviorParam2, mod.behaviorParamF));
            }
            return this;
        }

        public SnapshotPatch Apply(IReadOnlyList<SpecModifier> mods)
        {
            if (mods == null) return this;
            for (int i = 0; i < mods.Count; i++) Apply(mods[i]);
            return this;
        }

        public void ApplyTo(HitSnapshot snap, SpecHitInstance hitSpec = null)
        {
            if (snap == null) return;

            float damageBeforeMin = (snap.BaseDamage + _damageDelta) * _damageMul;
            snap.BaseDamage = Mathf.Max(_damageMinMax, damageBeforeMin);

            snap.Speed *= _speedMul;

            float baseHitWidth = hitSpec != null ? hitSpec.hitWidth : 1f;
            snap.Extra[HitSnapshotKeys.HitWidth] = baseHitWidth * _hitWidthMul;

            if (_element != eElement.NONE)
                snap.Extra[HitSnapshotKeys.Element] = _element;
        }

        protected override void Reset()
        {
            _damageDelta = 0;
            _damageMul = 1f;
            _damageMinMax = 0;
            _bounceDelta = 0;
            _pierceDelta = 0;
            _hitWidthMul = 1f;
            _speedMul = 1f;
            _element = eElement.NONE;
            _behaviors.Clear();
        }

        public readonly struct BehaviorEntry
        {
            public readonly eModifierBehavior Kind;
            public readonly int Param1;
            public readonly int Param2;
            public readonly float ParamF;

            public BehaviorEntry(eModifierBehavior kind, int p1, int p2, float pf)
            {
                Kind = kind;
                Param1 = p1;
                Param2 = p2;
                ParamF = pf;
            }
        }
    }
}
