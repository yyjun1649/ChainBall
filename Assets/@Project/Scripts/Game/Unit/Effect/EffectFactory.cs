using System;
using System.Collections.Generic;
using SpecData;
using UnityEngine;

public static class EffectFactory
{
    private static readonly Dictionary<eEffectType, Func<SpecEffect, string, float, IEffect>> _creators =
        new Dictionary<eEffectType, Func<SpecEffect, string, float, IEffect>>();

    public static void Initialize()
    {
        _creators[eEffectType.StatModifier] = (spec, effectName, source) => new StatModifierEffect(
            effectName,
            (eStatType)(int)spec.Params["statType"],
            spec.Params["statValue"],
            (eModifierType)(int)spec.Params["modifierType"],
            source
        );
    }

    public static IEffect Create(SpecEffect spec, string effectName, float source = 0f)
    {
        if (!_creators.TryGetValue(spec.effectType, out var creator))
        {
            Debug.LogError($"[{spec.effectType}] is not registered in EffectFactory.");
            return null;
        }

        return creator(spec, effectName, source);
    }
}
