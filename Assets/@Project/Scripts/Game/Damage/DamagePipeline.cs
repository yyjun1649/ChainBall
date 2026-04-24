using System;
using Library;
using UnityEngine;

public static class DamagePipeline
{
    public static event Action<DamageInfo> OnFinalized;

    public static void Process(DamageInfo ctx)
    {
        if (!IsValidTarget(ctx))
        {
            ctx.Canceled = true;
            Finalize(ctx);
            return;
        }

        Stage_CalculateBase(ctx);

        ctx.Attacker?.Data.Effects.RaiseBeforeDealDamage(ctx, ctx.Attacker, ctx.Target);
        
        if (ctx.Canceled)
        {
            Finalize(ctx); return;
        }

        Stage_RollDodge(ctx);
        
        if (ctx.IsDodged)
        {
            Finalize(ctx); return;
        }
        
        Stage_RollCritical(ctx);
        Stage_ApplyResistance(ctx);

        ctx.Target.Data.Effects.RaiseBeforeTakeDamage(ctx, ctx.Attacker, ctx.Target);
        
        if (ctx.Canceled)
        {
            Finalize(ctx); return;
        }

        Stage_ApplyToHealth(ctx);

        ctx.Attacker?.Data.Effects.RaiseAfterDealDamage(ctx, ctx.Attacker, ctx.Target);
        ctx.Target.Data.Effects.RaiseAfterTakeDamage(ctx, ctx.Attacker, ctx.Target);

        Finalize(ctx);
    }

    private static bool IsValidTarget(DamageInfo ctx)
    {
        if (ctx.Target == null || !ctx.Target.IsAlive) return false;
        if (ctx.TargetVersion != 0 && ctx.Target.Version != ctx.TargetVersion) return false;
        if (ctx.Attacker != null && ctx.Attacker.Version != ctx.AttackerVersion) return false;
        return true;
    }

    private static void Stage_CalculateBase(DamageInfo ctx)
    {
        if (ctx.Attacker == null)
        {
            ctx.PreMitigation = ctx.BaseDamage;
            return;
        }

        var stats = ctx.Attacker.Data.Stats;
        var statType = ctx.DamageType == eDamageType.Melee ? eStatType.MeleeDamage : eStatType.MagicDamage;
        ctx.PreMitigation = ctx.BaseDamage + stats.GetStatValue(statType) * ctx.Percent;
    }

    private static void Stage_RollDodge(DamageInfo ctx)
    {
        // Placeholder — no Dodge stat defined yet.
    }

    private static void Stage_RollCritical(DamageInfo ctx)
    {
        if (ctx.Attacker == null) return;

        if (ctx.CriticalType == eCriticalType.Critical)
        {
            // Pre-confirmed at fire time (HitSnapshot path) — apply pre-supplied multiplier.
            if (ctx.CritMultiplier > 0f)
            {
                ctx.PreMitigation *= ctx.CritMultiplier;
            }
            return;
        }

        var stats = ctx.Attacker.Data.Stats;
        if (UtilLibrary.GetChance(stats.GetStatValue(eStatType.CriticalChance)))
        {
            ctx.CriticalType = eCriticalType.Critical;
            ctx.CritMultiplier = 1f + stats.GetStatValue(eStatType.CriticalDamage);
            ctx.PreMitigation *= ctx.CritMultiplier;
        }
    }

    private static void Stage_ApplyResistance(DamageInfo ctx)
    {
        if (ctx.Target == null)
        {
            ctx.Final = ctx.PreMitigation;
            return;
        }

        var defense = ctx.Target.Data.Stats.GetStatValue(eStatType.Defense);
        ctx.Final = Mathf.Max(0f, ctx.PreMitigation - defense);
    }

    private static void Stage_ApplyToHealth(DamageInfo ctx)
    {
        if (ctx.Target == null) return;
        if (ctx.Final <= 0f) return;
        ctx.Target.ApplyDamageToHealth(ctx);
    }

    private static void Finalize(DamageInfo ctx) => OnFinalized?.Invoke(ctx);
}
