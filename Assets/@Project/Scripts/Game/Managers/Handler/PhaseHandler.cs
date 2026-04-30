using System;
using System.Collections;
using UnityEngine;

public class PhaseHandler : GameHandler
{
    [SerializeField] private IdlePhase   _idle;
    [SerializeField] private UpKeepPhase _upkeep;
    [SerializeField] private CastPhase   _cast;
    [SerializeField] private EnemyPhase  _enemy;
    [SerializeField] private DamagePhase _damage;
    [SerializeField] private EndPhase    _end;

    public ePhaseType Current   { get; private set; } = ePhaseType.Idle;
    public bool       IsRunning { get; private set; }

    public event Action<ePhaseType> OnPhaseChanged;

    public override void Initialize()
    {
        base.Initialize();

        if (_idle   == null) Debug.LogError("[PhaseHandler] _idle slot is empty");
        if (_upkeep == null) Debug.LogError("[PhaseHandler] _upkeep slot is empty");
        if (_cast   == null) Debug.LogError("[PhaseHandler] _cast slot is empty");
        if (_enemy  == null) Debug.LogError("[PhaseHandler] _enemy slot is empty");
        if (_damage == null) Debug.LogError("[PhaseHandler] _damage slot is empty");
        if (_end    == null) Debug.LogError("[PhaseHandler] _end slot is empty");
    }

    public void RunTurn(Action onComplete)
    {
        if (IsRunning) return;
        StartCoroutine(RunTurnRoutine(onComplete));
    }

    private IEnumerator RunTurnRoutine(Action onComplete)
    {
        IsRunning = true;
        yield return Step(ePhaseType.UPKEEP, _upkeep);
        yield return Step(ePhaseType.ENEMY,  _enemy);
        yield return Step(ePhaseType.CAST,   _cast);
        yield return Step(ePhaseType.DAMAGE, _damage);
        yield return Step(ePhaseType.END,    _end);
        SetPhase(ePhaseType.Idle);
        IsRunning = false;
        onComplete?.Invoke();
    }

    private IEnumerator Step(ePhaseType type, PhaseBase phase)
    {
        SetPhase(type);
        if (phase == null) yield break;

        phase.Enter();
        yield return phase.Execute();
        phase.Exit();
    }

    private void SetPhase(ePhaseType p)
    {
        Current = p;
        OnPhaseChanged?.Invoke(p);
    }
}

public enum ePhaseType
{
    Idle,
    UPKEEP,
    CAST,
    ENEMY,
    DAMAGE,
    END,
}
