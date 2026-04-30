// SRDebugger panel for verifying Phase 3 (SpellSequence/SnapshotPatch/TriggerWatcher)
// and Phase 4 (Weapon runtime).
//
// Usage in Editor / dev build:
//   1. Run the GameScene (PlayerProxyUnit must be in the scene).
//   2. Open the SRDebugger overlay (default trigger: 3-finger tap or pin).
//   3. Go to "Options" → "Spell" category.
//   4. Tweak the projectile/modifier/trigger knobs, then press a Cast button.
//
// Requires Addressable HitInstance_{HitInstanceId} to be registered for the
// MovingHit pool to resolve. Tunes a fresh SpecHitInstance per cast — does NOT
// read from the live SpecData tables, so it works even before designer rows land.

using System.Collections.Generic;
using System.ComponentModel;
using Cysharp.Text;
using Game.Spell;
using SpecData;
using UnityEngine;

public partial class SROptions
{
    private const string CategorySpell = "Spell";

    // ─── Projectile knobs ───────────────────────────────────────────────────

    private int   _spellHitInstanceId = 1;
    private float _spellBaseDamage    = 1f;
    private int   _spellMultiShot     = 1;
    private float _spellSpreadAngle   = 0f;
    private int   _spellBounceCount   = 0;
    private int   _spellPierceCount   = 0;
    private float _spellAimAngleDeg   = 90f;
    private float _spellMoveSpeed     = 10f;
    private float _spellLifeTime      = 5f;

    [Category(CategorySpell)] public int HitInstanceId
    {
        get => _spellHitInstanceId;
        set { _spellHitInstanceId = Mathf.Max(1, value); OnPropertyChanged(nameof(HitInstanceId)); }
    }

    [Category(CategorySpell)] public float BaseDamage
    {
        get => _spellBaseDamage;
        set { _spellBaseDamage = value; OnPropertyChanged(nameof(BaseDamage)); }
    }

    [Category(CategorySpell)] public int MultiShot
    {
        get => _spellMultiShot;
        set { _spellMultiShot = Mathf.Clamp(value, 1, 64); OnPropertyChanged(nameof(MultiShot)); }
    }

    [Category(CategorySpell), NumberRange(0, 180)] public float SpreadAngle
    {
        get => _spellSpreadAngle;
        set { _spellSpreadAngle = value; OnPropertyChanged(nameof(SpreadAngle)); }
    }

    [Category(CategorySpell)] public int BounceCount
    {
        get => _spellBounceCount;
        set { _spellBounceCount = Mathf.Max(0, value); OnPropertyChanged(nameof(BounceCount)); }
    }

    [Category(CategorySpell)] public int PierceCount
    {
        get => _spellPierceCount;
        set { _spellPierceCount = Mathf.Max(0, value); OnPropertyChanged(nameof(PierceCount)); }
    }

    [Category(CategorySpell), NumberRange(0, 360)] public float AimAngleDeg
    {
        get => _spellAimAngleDeg;
        set { _spellAimAngleDeg = value; OnPropertyChanged(nameof(AimAngleDeg)); }
    }

    [Category(CategorySpell)] public float MoveSpeed
    {
        get => _spellMoveSpeed;
        set { _spellMoveSpeed = value; OnPropertyChanged(nameof(MoveSpeed)); }
    }

    [Category(CategorySpell)] public float LifeTime
    {
        get => _spellLifeTime;
        set { _spellLifeTime = value; OnPropertyChanged(nameof(LifeTime)); }
    }

    // ─── Modifier toggles ───────────────────────────────────────────────────

    private bool _modHeavy;
    private bool _modLight;
    private bool _modDamageUp;
    private bool _modInfinitePierce;

    [Category(CategorySpell), DisplayName("Mod: Heavy (+2 dmg, -2 bounce)")]
    public bool ModHeavy { get => _modHeavy; set { _modHeavy = value; OnPropertyChanged(nameof(ModHeavy)); } }

    [Category(CategorySpell), DisplayName("Mod: Light (-1 dmg, +4 bounce)")]
    public bool ModLight { get => _modLight; set { _modLight = value; OnPropertyChanged(nameof(ModLight)); } }

    [Category(CategorySpell), DisplayName("Mod: Damage Up (+1 dmg)")]
    public bool ModDamageUp { get => _modDamageUp; set { _modDamageUp = value; OnPropertyChanged(nameof(ModDamageUp)); } }

    [Category(CategorySpell), DisplayName("Mod: Pierce-on-hit (∞)")]
    public bool ModInfinitePierce { get => _modInfinitePierce; set { _modInfinitePierce = value; OnPropertyChanged(nameof(ModInfinitePierce)); } }

    // ─── Trigger / Effect ───────────────────────────────────────────────────

    private bool _trigOnHit;

    [Category(CategorySpell), DisplayName("Trigger: BRICK_HIT → Effect (warn-only until Phase 6)")]
    public bool TrigOnHit { get => _trigOnHit; set { _trigOnHit = value; OnPropertyChanged(nameof(TrigOnHit)); } }

    // ─── Weapon (Phase 4) ──────────────────────────────────────────────────

    private int _weaponCastsPerTurn = 1;
    private int _weaponCooldownTurns = 0;

    // ─── Bounce field bounds ───────────────────────────────────────────────
    // Defaults match CastPhase prototype: Rect(-4, 0, 8, 12), killLine y=0.

    private bool  _bounceWalls    = true;
    private float _boundsXMin     = -4f;
    private float _boundsXMax     =  4f;
    private float _boundsYMax     = 12f;
    private float _boundsKillLine =  0f;

    [Category(CategorySpell), DisplayName("Bounce: enable wall reflection")]
    public bool BounceWalls { get => _bounceWalls; set { _bounceWalls = value; OnPropertyChanged(nameof(BounceWalls)); } }

    [Category(CategorySpell), DisplayName("Bounce: xMin")]
    public float BoundsXMin { get => _boundsXMin; set { _boundsXMin = value; OnPropertyChanged(nameof(BoundsXMin)); } }

    [Category(CategorySpell), DisplayName("Bounce: xMax")]
    public float BoundsXMax { get => _boundsXMax; set { _boundsXMax = value; OnPropertyChanged(nameof(BoundsXMax)); } }

    [Category(CategorySpell), DisplayName("Bounce: yMax (ceiling)")]
    public float BoundsYMax { get => _boundsYMax; set { _boundsYMax = value; OnPropertyChanged(nameof(BoundsYMax)); } }

    [Category(CategorySpell), DisplayName("Bounce: killLine y")]
    public float BoundsKillLine { get => _boundsKillLine; set { _boundsKillLine = value; OnPropertyChanged(nameof(BoundsKillLine)); } }

    [Category(CategorySpell), DisplayName("Weapon: castsPerTurn")]
    public int WeaponCastsPerTurn
    {
        get => _weaponCastsPerTurn;
        set { _weaponCastsPerTurn = Mathf.Clamp(value, 1, 8); OnPropertyChanged(nameof(WeaponCastsPerTurn)); }
    }

    [Category(CategorySpell), DisplayName("Weapon: cooldownTurns")]
    public int WeaponCooldownTurns
    {
        get => _weaponCooldownTurns;
        set { _weaponCooldownTurns = Mathf.Max(0, value); OnPropertyChanged(nameof(WeaponCooldownTurns)); }
    }

    // ─── Action buttons ─────────────────────────────────────────────────────

    [Category(CategorySpell)]
    public void CastDirect()
    {
        if (!TryResolveAttacker(out var attacker, out var origin)) return;

        var (proj, mods, trig, effs) = BuildSpecBundle();
        Vector3 direction = UtilCode.AngleToVector(_spellAimAngleDeg);

        var sequence = SpellSequence.Get();
        sequence.Initialize(proj, mods, trig, effs);
        int fired = sequence.Use(attacker, origin, direction, MaybeAttachBounceContext);
        sequence.Dispose();
        Debug.Log(ZString.Format(
            "[SROptions.Spell] CastDirect fired {0} projectile(s) at {1:0.#}°.",
            fired, _spellAimAngleDeg));
    }

    [Category(CategorySpell), DisplayName("Cast: Homing")]
    public void CastHoming()
    {
        if (!TryResolveAttacker(out var attacker, out var origin)) return;

        var target = FindNearestEnemy(attacker, origin);
        if (target == null)
        {
            Debug.LogWarning("[SROptions.Spell] CastHoming: no enemy found.");
            return;
        }

        var (proj, mods, trig, effs) = BuildSpecBundle();
        Vector3 direction = UtilCode.AngleToVector(_spellAimAngleDeg);

        var sequence = SpellSequence.Get();
        sequence.Initialize(proj, mods, trig, effs);
        int fired = sequence.Use(attacker, origin, direction, hit =>
        {
            MaybeAttachBounceContext(hit);
            hit?.AddBehavior(new HomingBehavior(target, turnRate: 360f));
        });
        sequence.Dispose();
        Debug.Log(ZString.Format(
            "[SROptions.Spell] CastHoming fired {0} projectile(s) toward {1}.",
            fired, target.name));
    }

    [Category(CategorySpell), DisplayName("Cast: LightningChain")]
    public void CastLightningChain()
    {
        if (!TryResolveAttacker(out var attacker, out var origin)) return;

        var (proj, mods, trig, effs) = BuildSpecBundle();
        Vector3 direction = UtilCode.AngleToVector(_spellAimAngleDeg);

        var sequence = SpellSequence.Get();
        sequence.Initialize(proj, mods, trig, effs);
        int fired = sequence.Use(attacker, origin, direction, hit =>
        {
            MaybeAttachBounceContext(hit);
            hit?.AddBehavior(new LightningChainBehavior(chainCount: 3, damage: 5f));
        });
        sequence.Dispose();
        Debug.Log(ZString.Format(
            "[SROptions.Spell] CastLightningChain fired {0} projectile(s) at {1:0.#}°.",
            fired, _spellAimAngleDeg));
    }

    [Category(CategorySpell), DisplayName("Cast: SpawnOnHit")]
    public void CastSpawnOnHit()
    {
        if (!TryResolveAttacker(out var attacker, out var origin)) return;

        var (proj, mods, trig, effs) = BuildSpecBundle();
        Vector3 direction = UtilCode.AngleToVector(_spellAimAngleDeg);

        var sequence = SpellSequence.Get();
        sequence.Initialize(proj, mods, trig, effs);
        int fired = sequence.Use(attacker, origin, direction, hit =>
        {
            MaybeAttachBounceContext(hit);
            hit?.AddBehavior(new SpawnOnHitBehavior(count: 3, hitInstanceId: 1, spreadAngleDeg: 60f));
        });
        sequence.Dispose();
        Debug.Log(ZString.Format(
            "[SROptions.Spell] CastSpawnOnHit fired {0} projectile(s) at {1:0.#}°.",
            fired, _spellAimAngleDeg));
    }

    [Category(CategorySpell), DisplayName("Cast: BonusDamage")]
    public void CastBonusDamage()
    {
        if (!TryResolveAttacker(out var attacker, out var origin)) return;

        var (proj, mods, trig, effs) = BuildSpecBundle();
        Vector3 direction = UtilCode.AngleToVector(_spellAimAngleDeg);

        var sequence = SpellSequence.Get();
        sequence.Initialize(proj, mods, trig, effs);
        int fired = sequence.Use(attacker, origin, direction, hit =>
        {
            MaybeAttachBounceContext(hit);
            hit?.AddBehavior(new BonusDamageBehavior(count: 3, damage: 2f, interval: 0.3f));
        });
        sequence.Dispose();
        Debug.Log(ZString.Format(
            "[SROptions.Spell] CastBonusDamage fired {0} projectile(s) at {1:0.#}°.",
            fired, _spellAimAngleDeg));
    }

    [Category(CategorySpell)]
    public void CastViaWeapon()
    {
        if (!TryResolveAttacker(out var attacker, out var origin)) return;

        var (proj, mods, trig, effs) = BuildSpecBundle();
        if (proj == null) { Debug.LogWarning("[SROptions.Spell] no projectile."); return; }

        var weapon = BuildDebugWeapon(proj, mods, trig, effs);
        var angles = new List<float>();
        for (int i = 0; i < weapon.Spec.castsPerTurn; i++) angles.Add(_spellAimAngleDeg);

        int casts = weapon.Cast(attacker, origin, angles, MaybeAttachBounceContext);
        Debug.Log(ZString.Format(
            "[SROptions.Spell] CastViaWeapon: {0} sequence(s), cooldownLeft={1}.",
            casts, weapon.CooldownLeft));

        if (weapon.IsOnCooldown && _weaponCooldownTurns > 0)
        {
            int ticks = weapon.Cast(attacker, origin, angles, MaybeAttachBounceContext);
            Debug.Log(ZString.Format(
                "[SROptions.Spell] follow-up Cast (should skip): {0} sequence(s), cooldownLeft={1}.",
                ticks, weapon.CooldownLeft));
        }
    }

    [Category(CategorySpell)]
    public void ResetSpellOptions()
    {
        _spellHitInstanceId = 1; _spellBaseDamage = 1f; _spellMultiShot = 1;
        _spellSpreadAngle = 0f; _spellBounceCount = 0; _spellPierceCount = 0;
        _spellAimAngleDeg = 90f; _spellMoveSpeed = 10f; _spellLifeTime = 5f;
        _modHeavy = _modLight = _modDamageUp = _modInfinitePierce = false;
        _trigOnHit = false;
        _weaponCastsPerTurn = 1; _weaponCooldownTurns = 0;
        _bounceWalls = true;
        _boundsXMin = -4f; _boundsXMax = 4f; _boundsYMax = 12f; _boundsKillLine = 0f;
        Debug.Log("[SROptions.Spell] reset to defaults.");
    }

    // ─── Internals ──────────────────────────────────────────────────────────

    private bool TryResolveAttacker(out UnitController attacker, out Vector3 origin)
    {
        attacker = UnityEngine.Object.FindFirstObjectByType<PlayerProxyUnit>();
        if (attacker == null)
        {
            Debug.LogWarning("[SROptions.Spell] no PlayerProxyUnit in scene — open GameScene first.");
            origin = default;
            return false;
        }
        origin = attacker.transform.position;
        return true;
    }

    // Picks the closest alive non-attacker UnitController to `origin`. Used by the
    // homing debug buttons; intentionally simple (FindObjectsByType is fine for dev tools).
    private static UnitController FindNearestEnemy(UnitController attacker, Vector3 origin)
    {
        var all = UnityEngine.Object.FindObjectsByType<UnitController>(FindObjectsSortMode.None);
        UnitController best = null;
        float bestSqr = float.PositiveInfinity;
        for (int i = 0; i < all.Length; i++)
        {
            var u = all[i];
            if (u == null || u == attacker || !u.IsAlive) continue;
            float sqr = ((Vector2)(u.transform.position - origin)).sqrMagnitude;
            if (sqr < bestSqr) { bestSqr = sqr; best = u; }
        }
        return best;
    }

    private (SpecHitInstance, IReadOnlyList<SpecModifier>, SpecTrigger, IReadOnlyList<SpecEffect>) BuildSpecBundle()
    {
        var proj = new SpecHitInstance
        {
            id           = _spellHitInstanceId,
            kind         = eHitInstanceKind.MOVING,
            baseDamage   = _spellBaseDamage,
            basePercent  = 1f,
            moveSpeed    = _spellMoveSpeed,
            lifeTime     = _spellLifeTime,
            hitCount     = 1,
            bounceCount  = _spellBounceCount,
            pierceCount  = _spellPierceCount,
            multiShot    = _spellMultiShot,
            spreadAngle  = _spellSpreadAngle,
            hitWidth     = 1,
        };

        var mods = new List<SpecModifier>();
        if (_modHeavy)         mods.Add(MakeMod("dbg_heavy",  damageDelta: 2,  bounceDelta: -2, damageMin: 1));
        if (_modLight)         mods.Add(MakeMod("dbg_light",  damageDelta: -1, bounceDelta: 4,  damageMin: 1));
        if (_modDamageUp)      mods.Add(MakeMod("dbg_dmg_up", damageDelta: 1,  damageMin: 1));
        if (_modInfinitePierce) mods.Add(MakeBehaviorMod("dbg_pierce_inf", eModifierBehavior.PIERCE_ON_HIT));

        SpecTrigger trig = null;
        var effs = new List<SpecEffect>();
        if (_trigOnHit)
        {
            trig = new SpecTrigger { id = "dbg_on_hit", eventType = eTriggerEvent.BRICK_HIT, maxFiresPerCast = 0, cooldownTurn = 0 };
            effs.Add(new SpecEffect { id = 9001, kind = eEffectKind.DAMAGE_DIRECT, damage = 1 });
        }

        return (proj, mods, trig, effs);
    }

    private static SpecModifier MakeMod(string id, int damageDelta = 0, int bounceDelta = 0, int pierceDelta = 0, int damageMin = 0)
    {
        return new SpecModifier
        {
            id           = id,
            damageDelta  = damageDelta,
            damageMul    = 1f,
            damageMin    = damageMin,
            bounceDelta  = bounceDelta,
            pierceDelta  = pierceDelta,
            hitWidthMul  = 1f,
            speedMul     = 1f,
            behavior     = eModifierBehavior.NONE,
        };
    }

    private static SpecModifier MakeBehaviorMod(string id, eModifierBehavior behavior, int p1 = 0, int p2 = 0, float pf = 0f)
    {
        return new SpecModifier
        {
            id              = id,
            damageMul       = 1f,
            hitWidthMul     = 1f,
            speedMul        = 1f,
            behavior        = behavior,
            behaviorParam1  = p1,
            behaviorParam2  = p2,
            behaviorParamF  = pf,
        };
    }

    private Weapon BuildDebugWeapon(SpecHitInstance proj, IReadOnlyList<SpecModifier> mods, SpecTrigger trig, IReadOnlyList<SpecEffect> effs)
    {
        var slotShape = new List<eSlotKind> { eSlotKind.PROJECTILE };
        for (int i = 0; i < mods.Count; i++) slotShape.Add(eSlotKind.MODIFIER);
        if (trig != null) slotShape.Add(eSlotKind.TRIGGER);
        for (int i = 0; i < effs.Count; i++) slotShape.Add(eSlotKind.EFFECT);

        var spec = new SpecWeapon
        {
            id            = "dbg_weapon",
            slotCount     = slotShape.Count,
            castsPerTurn  = _weaponCastsPerTurn,
            cooldownTurns = _weaponCooldownTurns,
            slotShape     = slotShape.ToArray(),
        };

        var weapon = new Weapon(spec);
        int idx = 0;
        weapon.TryEquipSpell(idx++, Spell.OfProjectile(proj));
        for (int i = 0; i < mods.Count; i++) weapon.TryEquipSpell(idx++, Spell.OfModifier(mods[i]));
        if (trig != null) weapon.TryEquipSpell(idx++, Spell.OfTrigger(trig));
        for (int i = 0; i < effs.Count; i++) weapon.TryEquipSpell(idx++, Spell.OfEffect(effs[i]));
        return weapon;
    }

    // Per-spawn callback handed to SpellSequence/Weapon. Wires the BounceMovement
    // context with the panel's bounds so the projectile reflects off walls when
    // BounceWalls is on. No-op otherwise.
    private void MaybeAttachBounceContext(MovingHit hit)
    {
        if (hit == null || !_bounceWalls) return;
        if (hit.Movement is not BounceMovement bounce) return;

        float width  = Mathf.Max(0f, _boundsXMax - _boundsXMin);
        float height = Mathf.Max(0f, _boundsYMax - _boundsKillLine);
        var bounds = new Rect(_boundsXMin, _boundsKillLine, width, height);
        bounce.AttachContext(bounds, _boundsKillLine, session: null);
    }
}
