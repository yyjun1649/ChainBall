using UnityEngine;

[RequireComponent(typeof(SpriteRenderer))]
public class ShaderGauge : MonoBehaviour
{
    private static readonly int FillProp = Shader.PropertyToID("_FillAmount");
    private MaterialPropertyBlock mpb;
    private SpriteRenderer sr;

    [Range(0f, 1f)]
    public float fillAmount = 1f;

    void Awake()
    {
        sr = GetComponent<SpriteRenderer>();
        mpb = new MaterialPropertyBlock();
    }

    public void SetGauge(float amount)
    {
        fillAmount = amount;
        
        sr.GetPropertyBlock(mpb);
        mpb.SetFloat(FillProp, fillAmount);
        sr.SetPropertyBlock(mpb);
    }
}