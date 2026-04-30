using System;
using UnityEngine;

// Phase 2 placeholder — HP only. Phase 8 promotes Player to a full UnitController-derived
// entity (Survival relics need EffectHost integration).
public class Player : MonoBehaviour
{
    [SerializeField] private int _maxHp = 30;

    public int MaxHp { get; private set; }
    public int Hp    { get; private set; }
    public bool IsAlive => Hp > 0;

    public event Action<int /*delta*/, int /*hpAfter*/> OnHpChanged;
    public event Action OnDefeat;

    private void Awake()
    {
        MaxHp = _maxHp;
        Hp = MaxHp;
    }

    public void TakeDanger(int amount)
    {
        if (!IsAlive || amount <= 0) return;

        Hp = Mathf.Max(0, Hp - amount);
        OnHpChanged?.Invoke(-amount, Hp);

        if (Hp == 0) OnDefeat?.Invoke();
    }

    public void Heal(int amount)
    {
        if (!IsAlive || amount <= 0) return;

        Hp = Mathf.Min(MaxHp, Hp + amount);
        OnHpChanged?.Invoke(amount, Hp);
    }

    public void SetPosition(Vector3 pos)
    {
        transform.position = pos;
    }
}
