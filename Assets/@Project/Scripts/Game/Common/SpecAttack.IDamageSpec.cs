using System.Collections.Generic;
using SpecData;

namespace SpecData
{
    public partial class SpecAttack : IDamageSpec
    {
        float IDamageSpec.range => range;
        int IDamageSpec.hitInstance => hitInstance;
        eDamageType IDamageSpec.damageType => damageType;
        eAttackType IDamageSpec.attackType => attackType;
        float IDamageSpec.baseDamage => baseDamage;
        float IDamageSpec.basePercent => basePercent;
        List<int> IDamageSpec.effects => effects;
    }
}
