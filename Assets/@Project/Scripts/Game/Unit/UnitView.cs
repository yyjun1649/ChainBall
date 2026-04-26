using Cysharp.Text;
using Library;
using UnityEngine;

public class UnitView : PoolMonoBehaviour<UnitView>
{
    protected internal override string AddressFormat => ZString.Format("UnitView_{0}", poolObjectId);

    [SerializeField] private Animator _animator;
    [SerializeField] private SpriteRenderer _model;

    private UnitController _unitController;
    public UnitController Controller => _unitController;

    public Transform TrHead;

    [SerializeField] private Vector3 _offset;

    private Transform _controllerTr;
    private int _controllerVersion;
    private bool _followTarget;

    public void Initialize(UnitController unitController)
    {
        _unitController = unitController;
        _controllerTr = unitController.transform;
        _controllerVersion = unitController.Version;
        _followTarget = true;

        transform.position = _controllerTr.position + _offset;

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
        _unitController.OnDealDamage.AddListener(OnDealDamage);
        _unitController.OnDeath.AddListener(OnDeath);
    }

    private void UnregisterHandler()
    {
        if (_unitController == null) return;
        _unitController.OnTakeDamage.RemoveListener(OnTakeDamage);
        _unitController.OnDealDamage.RemoveListener(OnDealDamage);
        _unitController.OnDeath.RemoveListener(OnDeath);
    }

    private void OnTakeDamage(DamageInfo damageInfo, UnitController from) { }
    private void OnDealDamage(DamageInfo damageInfo, UnitController to) { }

    private void OnDeath(bool force)
    {
        _followTarget = false;

        if (force)
        {
            UnregisterHandler();
            ReleaseSelf();
            return;
        }

        if (_animator != null)
        {
            _animator.speed = 1f;
            _animator.Play("Death");
        }
    }

    // Called from an Animation Event at the end of the Death clip.
    public void ReleaseOnDeathAnimation()
    {
        UnregisterHandler();
        ReleaseSelf();
    }

    private void ReleaseSelf()
    {
        Release();
    }
}
