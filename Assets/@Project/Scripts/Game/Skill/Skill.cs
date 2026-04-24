using SpecData;

public abstract class Skill : DamageActionBase<SpecSkill>
{
    public abstract void Use(UnitController from, UnitController target);
}
