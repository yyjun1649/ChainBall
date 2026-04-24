public interface IOnTakeDamage
{
    void OnTakeDamage(DamageInfo info, UnitController @from);
}

public interface IOnKill
{
    void OnKill(UnitData unitData);
}

public interface IOnDealDamage
{
    void OnDeal(DamageInfo info, UnitData @to);
}