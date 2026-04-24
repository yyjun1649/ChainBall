using SpecData;
using Library;

public static class SkillFactory
{
    public static Skill Create(SpecSkill spec)
    {
        Skill skill;

        if (spec.hitInstance != 0)
        {
            skill = PooledDisposable.Get<ProjectileSkill>();
        }
        else
        {
            skill = PooledDisposable.Get<AroundSkill>();
        }

        skill.Initialize(spec);
        return skill;
    }
}
