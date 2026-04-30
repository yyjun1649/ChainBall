using System.Linq;
using Cysharp.Text;
using Library;
using SpecData;
using UnityEngine;

// Owns wave cursor (SpecWave iteration) + per-cell unit composition.
// - Brick / Boss are defined in SpecEnemy (wave-spawned, throwaway).
// - Player / playable characters are defined in SpecCharacter (run-persistent, has unlock/pool/passive).
// Both schemas expose controllerId / viewId, which drive Pool.Get<UnitController> / Pool.Get<UnitView>.
// FieldHandler calls Spawn(parsed, ...) per cell during AddRowFromPattern;
// LoadWave / TrySpawnNextRow drives wave-driven row spawning.
public class UnitSpawnHandler : GameHandler
{
    [Header("Layers")]
    [SerializeField] private LayerMask _myLayer;
    [SerializeField] private LayerMask _enemyLayer;

    private SpecWave[] _wave;
    private int _cursor;
    private string _currentWaveId;

    public string CurrentWaveId  => _currentWaveId;
    public bool   HasNext        => _wave != null && _cursor < _wave.Length;
    public int    RemainingLines => _wave == null ? 0 : Mathf.Max(0, _wave.Length - _cursor);

    public override void Initialize() { }

    public void LoadWave(string waveId)
    {
        if (string.IsNullOrEmpty(waveId))
        {
            Debug.LogError("[UnitSpawnHandler] LoadWave: waveId is null/empty");
            return;
        }

        _currentWaveId = waveId;
        _wave = SpecDataManager.Instance.SpecWave.All
            .Where(w => w.waveId == waveId)
            .OrderBy(w => w.lineIndex)
            .ToArray();
        _cursor = 0;

        if (_wave.Length == 0)
            Debug.LogError($"[UnitSpawnHandler] LoadWave: no SpecWave rows for waveId='{waveId}'");
    }

    public bool TrySpawnNextRow()
    {
        if (!HasNext) return false;

        var field = GameManager.Field;
        if (field == null) { Debug.LogError("[UnitSpawnHandler] TrySpawnNextRow: GameManager.Field is null"); return false; }

        var line = _wave[_cursor++];
        field.AddRowFromPattern(line.pattern);
        return true;
    }

    public override void Clear()
    {
        _wave = null;
        _cursor = 0;
        _currentWaveId = null;
    }

    // Brick / Boss path — Field calls this per parsed cell. Routes through SpecEnemy.
    public UnitController Spawn(BrickPatternParser.ParsedCell parsed, Vector2Int gridPos)
    {
        string enemyId = ResolveEnemyId(parsed);
        var spec = SpecDataManager.Instance.SpecEnemy.Get(enemyId);
        if (spec == null)
        {
            Debug.LogError($"[UnitSpawnHandler] missing SpecEnemy '{enemyId}' (cell type={parsed.type})");
            return null;
        }

        var meta = new BrickMeta
        {
            type    = parsed.type,
            element = parsed.element,
            bossId  = parsed.bossId,
        };
        
        int hpOverride = parsed.hp > 0 ? parsed.hp : 0;
        
        return SpawnUnit(spec.controllerId, spec.viewId, spec.id, hpOverride > 0 ? hpOverride : spec.startHp, meta);
    }

    // Enemy path by id — scripted spawns / boss patterns that know the SpecEnemy id directly.
    public UnitController SpawnEnemy(string enemyId)
    {
        var spec = SpecDataManager.Instance.SpecEnemy.Get(enemyId);
        if (spec == null)
        {
            Debug.LogError($"[UnitSpawnHandler] missing SpecEnemy '{enemyId}'");
            return null;
        }
        return SpawnUnit(spec.controllerId, spec.viewId, spec.id, spec.startHp, null);
    }

    // Player / playable character path — id resolves to a SpecCharacter row.
    public UnitController SpawnCharacter(string characterId)
    {
        var spec = SpecDataManager.Instance.SpecCharacter.Get(characterId);
        if (spec == null)
        {
            Debug.LogError($"[UnitSpawnHandler] missing SpecCharacter '{characterId}'");
            return null;
        }
        return SpawnUnit(spec.controllerId, spec.viewId, spec.id, spec.startHp, null);
    }

    private UnitController SpawnUnit(int controllerId, int viewId, string id, int hp, UnitDataModule extraModule)
    {
        var unit = Handlers.Pool.Get<UnitController>(controllerId);

        var data = new UnitData();
        data.InitializeBare(id);
        if (extraModule != null) data.AddModule(extraModule);

        data.Stats.AddModifier(TStatModifier<eStatType>.MakeModifier(
            "Base_Health", eModifierType.Flat, eStatType.Health, hp, false, "Base"));
        
        data.CalculateStat();

        unit.Initialize(data, _myLayer, _enemyLayer);

        var view = Handlers.Pool.Get<UnitView>(viewId);
        view.Initialize(unit);

        return unit;
    }

    // Cell → SpecEnemy id mapping. SpecEnemy sheet rows must match this scheme:
    //   NORMAL    → "brick_NORMAL"
    //   SHIELD/F  → "brick_shield_FIRE"
    //   SHIELD/I  → "brick_shield_ICE"
    //   SHIELD/E  → "brick_shield_SHOCK"
    //   EXPLOSIVE → "brick_EXPLOSIVE"
    //   SPAWNER   → "brick_SPAWNER"
    //   REFLECTOR → "brick_REFLECTOR"
    //   BOSS      → "boss_{bossId}"
    private static string ResolveEnemyId(BrickPatternParser.ParsedCell parsed)
    {
        if (parsed.type == eBrickType.BOSS)
            return ZString.Format("boss_{0}", parsed.bossId);
        if (parsed.type == eBrickType.SHIELD)
            return ZString.Format("brick_shield_{0}", parsed.element);
        return ZString.Format("brick_{0}", parsed.type);
    }
}
