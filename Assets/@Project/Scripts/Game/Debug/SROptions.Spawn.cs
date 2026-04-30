// SRDebugger panel for spawning bricks on demand (verification helper).
//
// Usage in Editor / dev build:
//   1. Run the GameScene.
//   2. Open the SRDebugger overlay.
//   3. Go to "Options" -> "Spawn" category.
//   4. Pick a BrickType / column, then press a Spawn button.
//
// Routes through FieldHandler.AddRowFromPattern so the new brick is registered
// in the grid (_cells) exactly as a wave-driven spawn would be. ShiftAllDown is
// invoked first so row 0 is guaranteed empty before pattern application.

using System.ComponentModel;
using Cysharp.Text;
using SpecData;
using UnityEngine;

public partial class SROptions
{
    private const string CategorySpawn = "Spawn";

    // ─── Knobs ─────────────────────────────────────────────────────────────

    private eBrickType _spawnBrickType = eBrickType.NORMAL;
    private eElement   _spawnShieldElement = eElement.FIRE;
    private int        _spawnHpOverride = 0; // 0 = use spec default
    private int        _spawnColumn = 0;
    private int        _spawnBossId = 1;

    [Category(CategorySpawn), DisplayName("Brick: type")]
    public eBrickType BrickType
    {
        get => _spawnBrickType;
        set { _spawnBrickType = value; OnPropertyChanged(nameof(BrickType)); }
    }

    [Category(CategorySpawn), DisplayName("Brick: shield element (when type=SHIELD)")]
    public eElement ShieldElement
    {
        get => _spawnShieldElement;
        set { _spawnShieldElement = value; OnPropertyChanged(nameof(ShieldElement)); }
    }

    [Category(CategorySpawn), DisplayName("Brick: HP override (0 = spec default, NORMAL only)")]
    public int HpOverride
    {
        get => _spawnHpOverride;
        set { _spawnHpOverride = Mathf.Max(0, value); OnPropertyChanged(nameof(HpOverride)); }
    }

    [Category(CategorySpawn), DisplayName("Brick: column (0..7)")]
    public int SpawnColumn
    {
        get => _spawnColumn;
        set { _spawnColumn = Mathf.Clamp(value, 0, FieldHandler.COLS - 1); OnPropertyChanged(nameof(SpawnColumn)); }
    }

    [Category(CategorySpawn), DisplayName("Boss: id (when type=BOSS)")]
    public int BossId
    {
        get => _spawnBossId;
        set { _spawnBossId = Mathf.Max(1, value); OnPropertyChanged(nameof(BossId)); }
    }

    // ─── Action buttons ────────────────────────────────────────────────────

    [Category(CategorySpawn), DisplayName("Spawn: Brick (single column)")]
    public void SpawnBrickSingle()
    {
        if (!TryGetField(out var field)) return;

        string token = BuildCellToken();
        if (token == null) return;

        var pattern = new string[FieldHandler.COLS];
        for (int c = 0; c < FieldHandler.COLS; c++) pattern[c] = ".";
        pattern[_spawnColumn] = token;

        field.ShiftAllDown();
        field.AddRowFromPattern(pattern);
        Debug.Log(ZString.Format("[SROptions.Spawn] single brick '{0}' at column {1}.", token, _spawnColumn));
    }

    [Category(CategorySpawn), DisplayName("Spawn: Brick (filled row)")]
    public void SpawnBrickRow()
    {
        if (!TryGetField(out var field)) return;

        string token = BuildCellToken();
        if (token == null) return;

        var pattern = new string[FieldHandler.COLS];
        for (int c = 0; c < FieldHandler.COLS; c++) pattern[c] = token;

        field.ShiftAllDown();
        field.AddRowFromPattern(pattern);
        Debug.Log(ZString.Format("[SROptions.Spawn] filled row with '{0}'.", token));
    }

    [Category(CategorySpawn), DisplayName("Spawn: reset options")]
    public void ResetSpawnOptions()
    {
        _spawnBrickType = eBrickType.NORMAL;
        _spawnShieldElement = eElement.FIRE;
        _spawnHpOverride = 0;
        _spawnColumn = 0;
        _spawnBossId = 1;
        Debug.Log("[SROptions.Spawn] reset to defaults.");
    }

    // ─── Internals ─────────────────────────────────────────────────────────

    private static bool TryGetField(out FieldHandler field)
    {
        field = GameManager.Field;
        if (field == null)
        {
            Debug.LogWarning("[SROptions.Spawn] GameManager.Field is null — open GameScene first.");
            return false;
        }
        return true;
    }

    // Builds a BrickPatternParser cell token from current panel selections.
    // Mirrors the notation in BrickPatternParser (N / N(hp) / S(F|I|E) / E / P / R / B[id]).
    private string BuildCellToken()
    {
        switch (_spawnBrickType)
        {
            case eBrickType.NORMAL:
                return _spawnHpOverride > 0 ? ZString.Format("N({0})", _spawnHpOverride) : "N";
            case eBrickType.SHIELD:
            {
                string tag = _spawnShieldElement switch
                {
                    eElement.FIRE  => "F",
                    eElement.ICE   => "I",
                    eElement.SHOCK => "E",
                    _              => null,
                };
                if (tag == null)
                {
                    Debug.LogWarning(ZString.Format("[SROptions.Spawn] unsupported shield element {0}.", _spawnShieldElement));
                    return null;
                }
                return ZString.Format("S({0})", tag);
            }
            case eBrickType.EXPLOSIVE: return "E";
            case eBrickType.SPAWNER:   return "P";
            case eBrickType.REFLECTOR: return "R";
            case eBrickType.BOSS:      return ZString.Format("B[{0}]", _spawnBossId);
            default:
                Debug.LogWarning(ZString.Format("[SROptions.Spawn] unhandled brick type {0}.", _spawnBrickType));
                return null;
        }
    }
}
