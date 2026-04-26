using System;
using UnityEngine;

// 8 columns × 15 rows grid. Row 0 = spawn area (top), row 14 = danger line (bottom).
// Stores UnitController directly — type/element live on UnitData (MVC: Controller is a pure
// event publisher, Data owns state). Grid position is implicit in array index.
public class BrickField : MonoBehaviour
{
    public const int COLS = 8;
    public const int ROWS = 15;
    public const int DANGER_ROW = ROWS - 1; // 14

    [SerializeField] private Vector2 _cellSize = Vector2.one;
    [SerializeField] private Vector2 _origin   = Vector2.zero; // world pos of cell (0, 0)

    private readonly UnitController[,] _cells = new UnitController[COLS, ROWS];

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

    // Spawn one row at row 0 from a parsed pattern. Caller (TurnRunner) provides the spawn factory.
    public void AddRowFromPattern(string[] pattern, Func<BrickPatternParser.ParsedCell, Vector2Int, UnitController> spawn)
    {
        if (pattern == null || pattern.Length != COLS)
        {
            Debug.LogError($"[BrickField] pattern length must be {COLS}, got {pattern?.Length ?? 0}");
            return;
        }

        for (int c = 0; c < COLS; c++)
        {
            // Row 0 must be empty before AddRow (caller is expected to ShiftAllDown first).
            if (_cells[c, 0] != null)
            {
                Debug.LogError($"[BrickField] AddRowFromPattern: row 0 col {c} not empty");
                continue;
            }

            var parsed = BrickPatternParser.Parse(pattern[c]);
            if (parsed.isEmpty) continue;

            var unit = spawn(parsed, new Vector2Int(c, 0));
            if (unit == null) continue;

            _cells[c, 0] = unit;
            unit.SetPosition(GridToWorld(c, 0));
        }
    }

    // Shift all alive bricks down by 1 row. Bricks crossing DANGER_ROW boundary are returned
    // to the caller (TurnRunner.DAMAGE phase) for danger-line damage processing + removal.
    // FREEZE check: TODO Phase 6 — `unit.Data.Effects.HasEffect<FreezeEffect>` once FreezeEffect lands.
    public void ShiftAllDown(Action<UnitController> onCrossDanger)
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
                onCrossDanger?.Invoke(b);
                _cells[c, r] = null;
                continue;
            }

            _cells[c, r] = null;
            _cells[c, nextRow] = b;
            b.SetPosition(GridToWorld(c, nextRow));
        }
    }

    public void RemoveAt(int col, int row)
    {
        if (!InBounds(col, row)) return;
        _cells[col, row] = null;
    }
}
