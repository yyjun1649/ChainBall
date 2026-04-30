using System;
using Library;
using UnityEngine;

public class GameManager : SingletonBehaviour<GameManager>
{
    public static FieldHandler     Field => Instance._field;
    public static PhaseHandler     Phase => Instance._phase;
    public static UnitSpawnHandler Spawn => Instance._spawn;

    [SerializeField] private Player _player;
    public Player Player => _player;

    private GameHandler[]    _handlers;
    private FieldHandler     _field;
    private PhaseHandler     _phase;
    private UnitSpawnHandler _spawn;

    public int  TurnCount       { get; private set; }
    public bool IsBattleRunning { get; private set; }

    public event Action<int> OnTurnStarted;
    public event Action<int> OnTurnEnded;

    protected override void Awake()
    {
        _handlers = GetComponentsInChildren<GameHandler>();
        _field    = GetComponentInChildren<FieldHandler>();
        _phase    = GetComponentInChildren<PhaseHandler>();
        _spawn    = GetComponentInChildren<UnitSpawnHandler>();

        base.Awake();
    }

    public void Initialize()
    {
        foreach (var gameHandler in _handlers) gameHandler.Initialize();
        foreach (var gameHandler in _handlers) gameHandler.LateInitialize();
    }

    public void StartBattle()
    {
        TurnCount       = 0;
        IsBattleRunning = true;
        foreach (var h in _handlers) h.StartGame();

        StepTurn();
    }

    public void StepTurn()
    {
        if (!IsBattleRunning) return;
        if (_phase == null || _phase.IsRunning) return;

        TurnCount++;
        OnTurnStarted?.Invoke(TurnCount);
        _phase.RunTurn(() =>
        {
            OnTurnEnded?.Invoke(TurnCount);
            if (IsBattleRunning) StepTurn();
        });
    }

    public void Clear()
    {
        IsBattleRunning = false;
        foreach (var gameHandler in _handlers) gameHandler.Clear();
    }

    public void FailGame()
    {
    }

    public void Reset()
    {
    }
}
