using SpecData;

// Brick-specific metadata module attached to UnitData of brick units.
// Read by DamagePipeline / future Shield/Reflector/Explosive IEffects.
//
// Lookup pattern:
//   var meta = unit.Data.GetModule<BrickMeta>();
//   if (meta != null) { ... } // null = not a brick
//
// Phase 6 may dissolve this into IEffects (ShieldGuardEffect carrying element, etc.) —
// at that point BrickMeta becomes redundant and is deleted.
public class BrickMeta : UnitDataModule
{
    public eBrickType type;
    public eElement   element;
    public int        bossId; // 0 unless type == BOSS
}
