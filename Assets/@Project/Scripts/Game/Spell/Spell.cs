using SpecData;

namespace Game.Spell
{
    // Authority: Docs/Systems/Weapon.md §1, Docs/Systems/Spell.md §1.
    // Runtime spell = (category, single spec). Each Spell holds exactly one of
    // the four spec types corresponding to its category. Use the static factory
    // methods; the constructor is private to keep the discriminated-union
    // invariant ("category matches the populated field").
    public sealed class Spell
    {
        public eSlotKind Category { get; }
        public SpecHitInstance Projectile { get; }
        public SpecModifier    Modifier   { get; }
        public SpecTrigger     Trigger    { get; }
        public SpecEffect      Effect     { get; }

        private Spell(eSlotKind category,
                      SpecHitInstance projectile = null,
                      SpecModifier    modifier   = null,
                      SpecTrigger     trigger    = null,
                      SpecEffect      effect     = null)
        {
            Category   = category;
            Projectile = projectile;
            Modifier   = modifier;
            Trigger    = trigger;
            Effect     = effect;
        }

        public static Spell OfProjectile(SpecHitInstance proj) => new Spell(eSlotKind.PROJECTILE, projectile: proj);
        public static Spell OfModifier(SpecModifier mod)       => new Spell(eSlotKind.MODIFIER,   modifier: mod);
        public static Spell OfTrigger(SpecTrigger trig)        => new Spell(eSlotKind.TRIGGER,    trigger: trig);
        public static Spell OfEffect(SpecEffect eff)           => new Spell(eSlotKind.EFFECT,     effect: eff);

        public bool FitsSlot(eSlotKind slotKind) => slotKind == eSlotKind.ANY || slotKind == Category;
    }
}
