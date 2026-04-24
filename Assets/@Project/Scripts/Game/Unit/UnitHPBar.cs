using Library;
using UnityEngine;

public class UnitHPBar : PoolMonoBehaviour<UnitHPBar>
{
    protected internal override string AddressFormat => "UnitHpBar";
    
    
    [SerializeField] private DynamicSlider _sliderHp;

    private UnitController _unitController;
    private Transform _controllerTr;
    private int _controllerVersion;
    private Vector3 _offset;
    private bool _followTarget;

    public void Initialize(UnitController unitController, Vector3 position)
    {
        _unitController = unitController;
        _controllerTr = unitController.transform;
        _controllerVersion = unitController.Version;
        _offset = position - _controllerTr.position;
        _followTarget = true;

        _sliderHp.Initialize(unitController.CurrentHp, unitController.MaxHp);

        transform.position = position;

        RegisterHandler();

        gameObject.SetActive(true);
    }

    private void LateUpdate()
    {
        if (!_followTarget) return;
        if (_unitController == null || _unitController.Version != _controllerVersion)
        {
            _followTarget = false;
            return;
        }

        transform.position = _controllerTr.position + _offset;
    }

    private void RegisterHandler()
    {
        _unitController.OnTakeDamage.AddListener(OnTakeDamage);
        _unitController.OnTakeHeal.AddListener(OnTakeHeal);
        _unitController.OnDeath.AddListener(OnDeath);
    }

    private void UnregisterHandler()
    {
        if (_unitController == null) return;
        _unitController.OnTakeDamage.RemoveListener(OnTakeDamage);
        _unitController.OnTakeHeal.RemoveListener(OnTakeHeal);
        _unitController.OnDeath.RemoveListener(OnDeath);
    }

    private void OnTakeDamage(DamageInfo damageInfo, UnitController from)
    {
        _sliderHp.SetValue(_unitController.CurrentHp / _unitController.MaxHp);
    }

    private void OnTakeHeal(DamageInfo damageInfo, UnitController from)
    {
        _sliderHp.SetValue(_unitController.CurrentHp / _unitController.MaxHp);
    }

    private void OnDeath(bool force)
    {
        _followTarget = false;
        UnregisterHandler();
        Release();
    }
}
