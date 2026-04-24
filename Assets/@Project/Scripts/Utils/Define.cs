
public enum eAttackType
{
    Normal,
}

public enum eStatType
{
    None,
        
    Health = 1,
        
    MeleeDamage=2,
    MagicDamage=3,

    AttackSpeed=4,
    MoveSpeed=5,

    CriticalChance=6,
    CriticalDamage=7,

    LifeSteal=8,

    Defense=9,

    HealRating=10,
        
    Range=11,
}
    
public enum eModifierType
{
    Flat,
    PercentAdd,
    PercentMul,
}
    
    
public enum eEffectType
{
    StatModifier,
}
    
public enum eDamageType
{
    Melee,
    Magic,
    Heal,
}
    
public enum eCriticalType
{
    Normal,
    Critical,
}
    
public enum eVFXType
{
    None,
}

public enum eAttackModuleType
{
    Collider,
    Melee,
    Projectile,
    Spear,
}
    
public enum eItemType
{
    Active,
    Passive,
}

public enum UnitAction
{
    Idle,
    Move,
    Attack,
    Hit,
    Death,
}

