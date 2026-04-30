using System.Collections.Generic;
using Library;
using UnityEngine;

// 8 columns × 15 rows brick grid. Row 0 = spawn area (top), row 14 = danger line (bottom).
// Owns grid state, descent, danger-line collection. Spawn composition is delegated to
// UnitSpawnHandler (resolved via GameManager.Spawn).
public class FieldHandler : GameHandler
{
    public const int COLS = 8;
    public const int ROWS = 15;
    public const int DANGER_ROW = ROWS - 1; // 14

    [SerializeField] private Vector2 _cellSize = Vector2.one;
    [SerializeField] private Vector2 _origin   = Vector2.zero; // world pos of cell (0, 0)

    private readonly UnitController[,] _cells = new UnitController[COLS, ROWS];
    private readonly List<UnitController> _crossedDanger = new();

    public IReadOnlyList<UnitController> CrossedDanger => _crossedDanger;

    public UnitController this[int col, int row] => InBounds(col, row) ? _cells[col, row] : null;

    public bool InBounds(int col, int row) => col >= 0 && col < COLS && row >= 0 && row < ROWS;

    public bool IsEmpty()
    {
        for (int r = 0; r < ROWS; r++)
        for (int c = 0; c < COLS; c++)
            if (_cells[c, r] != null) return false;
        return true;
    }

    public Vector3 GridToWorld(int col, int row)
    {
        // row 0 (spawn) at top, row 14 (danger) at bottom.
        return new Vector3(
            _origin.x + col * _cellSize.x,
            _origin.y - row * _cellSize.y,
            0f);
    }

    // UPKEEP phase: tick every alive brick's effects with turn-unit dt.
    public void TickAllEffects(float deltaTime)
    {
        for (int r = 0; r < ROWS; r++)
        for (int c = 0; c < COLS; c++)
        {
            var b = _cells[c, r];
            if (b == null || !b.IsAlive) continue;
            b.Data.Effects.Tick(deltaTime);
        }
    }

    // ENEMY phase: shift all alive bricks down by 1 row. Bricks crossing DANGER_ROW are
    // accumulated in _crossedDanger for the DAMAGE phase to process.
    // FREEZE check: TODO Phase 6 — `unit.Data.Effects.HasEffect<FreezeEffect>` once it lands.
    public void ShiftAllDown()
    {
        for (int r = ROWS - 1; r >= 0; r--)
        for (int c = 0; c < COLS; c++)
        {
            var b = _cells[c, r];
            if (b == null) continue;

            // TODO Phase 6: if (b.Data.Effects.HasEffect<FreezeEffect>()) continue;

            int nextRow = r + 1;
            if (nextRow >= ROWS)
            {
                _crossedDanger.Add(b);
                _cells[c, r] = null;
                continue;
            }

            _cells[c, r] = null;
            _cells[c, nextRow] = b;
            b.SetPosition(GridToWorld(c, nextRow));
        }
    }

    // ENEMY phase: spawn one row at row 0 from a parsed pattern.
    public void AddRowFromPattern(string[] pattern)
    {
        if (pattern == null || pattern.Length != COLS)
        {
            Debug.LogError($"[FieldHandler] pattern length must be {COLS}, got {pattern?.Length ?? 0}");
            return;
        }

        for (int c = 0; c < COLS; c++)
        {
            // Row 0 must be empty before AddRow (caller is expected to ShiftAllDown first).
            if (_cells[c, 0] != null)
            {
                Debug.LogError($"[FieldHandler] AddRowFromPattern: row 0 col {c} not empty");
                continue;
            }

            var parsed = BrickPatternParser.Parse(pattern[c]);
            if (parsed.isEmpty) continue;

            var unit = GameManager.Spawn.Spawn(parsed, new Vector2Int(c, 0));
            if (unit == null) continue;

            _cells[c, 0] = unit;
            unit.SetPosition(GridToWorld(c, 0));

            var captured = unit;
            unit.OnDeath.AddListener(_ => OnBrickDeath(captured));
        }
    }

    private void OnBrickDeath(UnitController unit)
    {
        for (int r = 0; r < ROWS; r++)
        for (int c = 0; c < COLS; c++)
        {
            if (_cells[c, r] != unit) continue;
            _cells[c, r] = null;
            unit.Release();
            return;
        }
        // Already crossed the danger line (cleared by ShiftAllDown) — still release the unit.
        unit.Release();
    }

    // DAMAGE phase clears the buffer after consuming it.
    public void ClearCrossedDanger() => _crossedDanger.Clear();

    public void RemoveAt(int col, int row)
    {
        if (!InBounds(col, row)) return;
        _cells[col, row] = null;
    }

    public override void Clear()
    {
        for (int r = 0; r < ROWS; r++)
        for (int c = 0; c < COLS; c++)
        {
            var b = _cells[c, r];
            if (b == null) continue;
            if (b.IsAlive) b.Death(true);
            _cells[c, r] = null;
        }
        _crossedDanger.Clear();
    }
}
