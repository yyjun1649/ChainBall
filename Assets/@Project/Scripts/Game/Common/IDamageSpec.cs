using System.Collections.Generic;

namespace SpecData
{
    public interface IDamageSpec
    {
        float range { get; }
        int hitInstance { get; }
        eDamageType damageType { get; }
        eAttackType attackType { get; }
        float baseDamage { get; }
        float basePercent { get; }
        List<int> effects { get; }
    }
}
