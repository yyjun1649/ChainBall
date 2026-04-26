using System;
using UnityEngine;

// Turn loop driver. Phase 2 skeleton — CAST is empty, ENEMY uses a placeholder pattern,
// DAMAGE applies fixed danger-line damage to Player.
//
// Phase progression per turn:
//   UPKEEP  — tick all bricks' Effects (BurnEffect, etc.). dt = 1.0 (turn-unit).
//   CAST    — Weapon casts. (Phase 4)
//   ENEMY   — ShiftAllDown + AddRowFromPattern (next SpecWave line). (Phase 9 wires waves)
//   DAMAGE  — bricks that crossed danger line deal damage to Player.
//   END     — relic ON_TURN_END hooks. (Phase 7)
public enum TurnPhase
{
    Idle,
    UPKEEP,
    CAST,
    ENEMY,
    DAMAGE,
    END,
}

public class TurnRunner : MonoBehaviour
{
    [SerializeField] private BrickField _field;
    [SerializeField] private Player     _player;

    [SerializeField] private int   _baseDangerDamage   = 1;
    [SerializeField] private float _turnDeltaTime      = 1f; // dt for Effects.Tick

    public TurnPhase CurrentPhase { get; private set; } = TurnPhase.Idle;
    public int       TurnCount    { get; private set; }

    public event Action<TurnPhase> OnTurnPhaseChanged;

    public void StepTurn()
    {
        TurnCount++;

        SetPhase(TurnPhase.UPKEEP);
        DoUpkeep();

        SetPhase(TurnPhase.CAST);
        DoCast();

        SetPhase(TurnPhase.ENEMY);
        DoEnemy();

        SetPhase(TurnPhase.DAMAGE);
        DoDamage();

        SetPhase(TurnPhase.END);
        DoEnd();

        SetPhase(TurnPhase.Idle);
    }

    private void SetPhase(TurnPhase next)
    {
        CurrentPhase = next;
        OnTurnPhaseChanged?.Invoke(next);
    }

    private void DoUpkeep()
    {
        if (_field == null) return;
        for (int r = 0; r < BrickField.ROWS; r++)
        for (int c = 0; c < BrickField.COLS; c++)
        {
            var b = _field[c, r];
            if (b == null || !b.IsAlive) continue;
            b.Data.Effects.Tick(_turnDeltaTime);
        }
    }

    // TODO Phase 4 — Weapon.Cast(playerInputAngle).
    private void DoCast() { }

    private void DoEnemy()
    {
        if (_field == null) return;
        _field.ShiftAllDown(brick =>
        {
            // Crossed danger row → counted by DAMAGE phase via _crossed list.
            _crossed.Add(brick);
        });

        // TODO Phase 9 — pull next SpecWave line and call _field.AddRowFromPattern(...).
    }

    private readonly System.Collections.Generic.List<Brick> _crossed = new();

    private void DoDamage()
    {
        if (_player == null) { _crossed.Clear(); return; }
        if (_crossed.Count == 0) return;

        _player.TakeDanger(_baseDangerDamage * _crossed.Count);

        foreach (var b in _crossed)
        {
            b.Death(true); // force release back to pool
        }
        _crossed.Clear();
    }

    // TODO Phase 7 — relic ON_TURN_END hooks via Handlers.Event.
    private void DoEnd() { }

    private void Update()
    {
        // Debug input: spacebar = next turn.
        if (UnityEngine.Input.GetKeyDown(KeyCode.Space))
        {
            StepTurn();
        }
    }
}
