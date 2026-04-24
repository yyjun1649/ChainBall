
using Cysharp.Text;
using Library;
using TMPro;
using UnityEngine;

public class DamageText : PoolMonoBehaviour<DamageText>
{
    protected internal override string AddressFormat => ZString.Format("DamageText_{0}", poolObjectId);
    
    [SerializeField] private TextMeshProUGUI _text;
    [SerializeField] private Animator _animator;

    public void Initialize(DamageInfo damageInfo, Vector3 pos)
    {
        transform.localScale = Vector3.one * 0.01f;
        
        SetColor(damageInfo.DamageType);
        SetSize(damageInfo.Value);
        SetText(damageInfo.Value);

        transform.position = pos;
        
        gameObject.SetActive(true);
        
        SetAnimation(damageInfo.CriticalType);
    }
    
    public void SetAnimation(eCriticalType criticalType)
    {
        _animator.Play(criticalType.ToString());
    }

    public void SetColor(eDamageType damageType)
    {
        _text.color = damageType == eDamageType.Melee ? Color.red 
            : damageType == eDamageType.Magic ? Color.blue 
            : Color.green;
    }

    public void SetSize(float value)
    {
        var size = Vector3.one;

        // size *= (1 + (value / 3000f));
        
        transform.localScale = size;
    }

    public void SetText(float value)
    {
        _text.SetText(value.ToString("N0"));
    }

    public void OnEnd()
    {
        Release();
    }

    public void Reset()
    {
        _text = GetComponentInChildren<TextMeshProUGUI>();
        _animator = GetComponent<Animator>();
    }

    public static void Show(DamageInfo damageInfo, Vector3 pos)
    {
        var damageText = Handlers.Pool.Get<DamageText>(damageInfo.GetId());

        damageText.Initialize(damageInfo,pos);
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void RegisterOnDamageFinalized()
    {
        DamagePipeline.OnFinalized -= OnDamageFinalized;
        DamagePipeline.OnFinalized += OnDamageFinalized;
    }

    private static void OnDamageFinalized(DamageInfo ctx)
    {
        if (ctx.Canceled || ctx.IsDodged) return;
        if (ctx.Target == null) return;
        if (ctx.Final <= 0f) return;
        Show(ctx, ctx.Target.transform.position);
    }
}