using Cysharp.Text;
using Library;
using UnityEngine;

public class UnitView : PoolMonoBehaviour<UnitView>
{
    protected internal override string AddressFormat => ZString.Format("UnitView_{0}",poolObjectId);
    
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
        _unitController.OnActionChanged.AddListener(OnActionChanged);
        _unitController.OnMoveDirectionChanged.AddListener(OnMoveDirectionChanged);
        _unitController.OnFlip.AddListener(HandleFlip);
        _unitController.OnTakeDamage.AddListener(OnTakeDamage);
        _unitController.OnDealDamage.AddListener(OnDealDamage);
        _unitController.OnTakeHeal.AddListener(OnTakeHeal);
        _unitController.OnHeal.AddListener(OnHeal);
        _unitController.OnDeath.AddListener(OnDeath);
    }

    private void UnregisterHandler()
    {
        if (_unitController == null) return;
        _unitController.OnActionChanged.RemoveListener(OnActionChanged);
        _unitController.OnMoveDirectionChanged.RemoveListener(OnMoveDirectionChanged);
        _unitController.OnFlip.RemoveListener(HandleFlip);
        _unitController.OnTakeDamage.RemoveListener(OnTakeDamage);
        _unitController.OnDealDamage.RemoveListener(OnDealDamage);
        _unitController.OnTakeHeal.RemoveListener(OnTakeHeal);
        _unitController.OnHeal.RemoveListener(OnHeal);
        _unitController.OnDeath.RemoveListener(OnDeath);
    }

    // Map semantic action -> this view's Animator convention.
    private void OnActionChanged(UnitAction action)
    {
        switch (action)
        {
            case UnitAction.Idle:
                _animator.SetBool("Move", false);
                _animator.speed = 0.1f;
                break;
            case UnitAction.Move:
                _animator.SetBool("Move", true);
                _animator.speed = 0.5f;
                break;
            case UnitAction.Attack:
                _animator.Play("Attack");
                break;
            case UnitAction.Hit:
                _animator.Play("Hit");
                break;
            case UnitAction.Death:
                _animator.speed = 1f;
                _animator.Play("Death");
                break;
        }
    }

    private void OnMoveDirectionChanged(Vector2 dir)
    {
        int animX = dir.x > 0.25f ? 1 : dir.x < -0.25f ? -1 : 0;
        int animY = dir.y > 0.25f ? 1 : dir.y < -0.25f ? -1 : 0;

        _animator.SetInteger("MoveX", Mathf.Abs(animX));
        _animator.SetInteger("MoveY", animY);
    }

    private void HandleFlip(bool flip) => _model.flipX = flip;

    private void OnTakeDamage(DamageInfo damageInfo, UnitController from) { }
    private void OnDealDamage(DamageInfo damageInfo, UnitController to) { }
    private void OnTakeHeal(DamageInfo damageInfo, UnitController from) { }
    private void OnHeal(DamageInfo damageInfo, UnitController to) { }

    private void OnDeath(bool force)
    {
        _followTarget = false;

        if (force)
        {
            UnregisterHandler();
            ReleaseSelf();
            return;
        }

        // Non-force: play Death animation. Animation event will call ReleaseOnDeathAnimation().
        _animator.speed = 1f;
        _animator.Play("Death");
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
