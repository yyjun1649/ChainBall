using System;
using System.Collections.Generic;
using SpecData;
using UnityEngine;

// TODO Phase 6 — ChainBall domain rebuild.
// SpecEffect schema migrated from {effectType + Params dict} to {kind + typed columns}.
// EffectFactory needs new dispatch table keyed on eEffectKind. See Roadmap §Phase 6.
public static class EffectFactory
{
    private static readonly Dictionary<eEffectKind, Func<SpecEffect, string, float, IEffect>> _creators =
        new Dictionary<eEffectKind, Func<SpecEffect, string, float, IEffect>>();

    public static void Initialize()
    {
        // Empty until Phase 6 — register eEffectKind creators (DAMAGE_DIRECT, AOE_DAMAGE,
        // STATUS_BURN, STATUS_FREEZE, …). Schema/effect.md §eEffectKind enumerates the 15 kinds.
    }

    public static IEffect Create(SpecEffect spec, string effectName, float source = 0f)
    {
        if (!_creators.TryGetValue(spec.kind, out var creator))
        {
            Debug.LogWarning($"[EffectFactory] kind={spec.kind} is not registered (Phase 6 pending).");
            return null;
        }

        return creator(spec, effectName, source);
    }
}
