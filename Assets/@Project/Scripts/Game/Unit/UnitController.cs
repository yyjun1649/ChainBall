using System;
using Cysharp.Text;
using Library;
using Sigtrap.Relays;
using UnityEngine;

[PoolAddress("UnitController_{0}")]
public class UnitController : PoolMonoBehaviour<UnitController>
{
    [SerializeField] private UnitFsmHandler _unitFsmHandler;
    [SerializeField] private Collider2D _collider;

    private UnitData _unitData;

    public int Version { get; private set; }
    public UnitData Data => _unitData;
    public float MaxHp { get; private set; }
    public float CurrentHp { get; private set; }
    public LayerMask EnemyLayer { get; set; }
    public LayerMask MyLayer { get; set; }
    public bool IsAlive { get; private set; }

    public UnitFsmHandler UnitFsmHandler => _unitFsmHandler;
    public Collider2D Collider => _collider;

    #region Action

    public Relay<DamageInfo, UnitController> OnTakeDamage { get; private set; } = new();
    public Relay<DamageInfo, UnitController> OnDealDamage { get; private set; } = new();
    public Relay<bool> OnDeath { get; private set; } = new();

    // Lifecycle hook — fired exactly once per pooled life, on return to pool. Used by
    // UnitSpawnHandler to drop the unit from its active-unit list.
    public event Action<UnitController> OnReleased;

    public void ClearAction()
    {
        OnTakeDamage.RemoveAll();
        OnDealDamage.RemoveAll();
        OnDeath.RemoveAll();
    }

    #endregion

    public void Initialize(UnitData unitData, LayerMask myLayer, LayerMask enemyLayer)
    {
        Version++;
        _unitData = unitData;

        SetLayer(enemyLayer, myLayer);

        MaxHp = _unitData.Stats.GetStatValue(eStatType.Health);
        CurrentHp = MaxHp;

        ClearAction();

        MappingHelperManager.Instance.Unit.Register(_collider, this);

        gameObject.SetActive(true);

        SetCollider(true);

        _unitFsmHandler.Initialize(this);

        IsAlive = true;
    }

    public void StartGame()
    {
        _unitFsmHandler.Change(eStateType.Idle);
    }

    public void SetPosition(Vector3 pos)
    {
        transform.position = pos;
    }

    public float DealDamage(UnitController @to, float value, eDamageType damageType, eAttackType attackType, float percent = 1f)
    {
        if (to == null) return 0f;

        using (var ctx = DamageInfo.Get())
        {
            ctx.Attacker = this;
            ctx.AttackerVersion = Version;
            ctx.Target = to;
            ctx.TargetVersion = to.Version;
            ctx.BaseDamage = value;
            ctx.Percent = percent;
            ctx.DamageType = damageType;
            ctx.AttackType = attackType;

            DamagePipeline.Process(ctx);

            if (ctx.Canceled || ctx.IsDodged) return 0f;

            OnDealDamage.Dispatch(ctx, to);

            if (!to.IsAlive)
            {
                _unitData.Effects.RaiseKill(this, to);
            }

            return ctx.Final;
        }
    }

    public void ApplyDamageToHealth(DamageInfo ctx)
    {
        if (!IsAlive) return;

        CurrentHp -= ctx.Final;

        OnTakeDamage.Dispatch(ctx, ctx.Attacker);

        if (CurrentHp <= 0)
        {
            Death();
        }
    }

    public bool Death(bool isForce = false)
    {
        if (!IsAlive) return false;

        if (!isForce && CurrentHp > 0)
        {
            return false;
        }

        _unitFsmHandler.Change(eStateType.Death);

        SetCollider(false);

        OnDeath.Dispatch(isForce);

        IsAlive = false;

        if (isForce)
        {
            Release();
        }

        return true;
    }

    public override void OnRelease()
    {
        MappingHelperManager.Instance.Unit.Unregister(_collider);
        Version++;
        OnReleased?.Invoke(this);
        base.OnRelease();
    }

    private void SetLayer(LayerMask enemyLayer, LayerMask myLayer)
    {
        EnemyLayer = enemyLayer;
        MyLayer = myLayer;
        gameObject.layer = MaskToLayerIndex(myLayer);
    }

    private static int MaskToLayerIndex(LayerMask mask)
    {
        int v = mask.value;
        if (v == 0) return 0;
        int idx = 0;
        while ((v & 1) == 0) { v >>= 1; idx++; }
        return idx;
    }

    private void SetCollider(bool isOn)
    {
        _collider.enabled = isOn;
    }
}
