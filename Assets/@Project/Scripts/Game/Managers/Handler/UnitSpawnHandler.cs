using System;
using System.Collections.Generic;
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

    private readonly List<UnitController> _activeUnits = new();

    public string CurrentWaveId  => _currentWaveId;
    public bool   HasNext        => _wave != null && _cursor < _wave.Length;
    public int    RemainingLines => _wave == null ? 0 : Mathf.Max(0, _wave.Length - _cursor);
    public IReadOnlyList<UnitController> ActiveUnits => _activeUnits;

    public override void Initialize() { }

    // Closest hostile alive unit (per attacker's EnemyLayer). Replaces FindObjectsByType scans
    // on hot paths like HomingBehavior retarget. self-skip + IsAlive recheck guards against
    // pooled-but-not-yet-released or dying instances still sitting in the active list.
    public UnitController GetNearestEnemy(Vector3 origin, UnitController self)
    {
        if (self == null) return null;
        int enemyMask = self.EnemyLayer.value;

        UnitController best = null;
        float bestSqr = float.PositiveInfinity;
        for (int i = 0; i < _activeUnits.Count; i++)
        {
            var u = _activeUnits[i];
            if (u == null || u == self || !u.IsAlive) continue;
            if ((enemyMask & (1 << u.gameObject.layer)) == 0) continue;
            float sqr = ((Vector2)(u.transform.position - origin)).sqrMagnitude;
            if (sqr < bestSqr) { bestSqr = sqr; best = u; }
        }
        return best;
    }

    // N closest hostile alive units (per attacker's EnemyLayer) excluding `self` and `exclude`.
    // Caller supplies `results` buffer (cleared on entry) so hot paths (chain behaviors) can
    // reuse a single list across hits with zero per-call alloc. O(active * count) selection
    // beats a full sort for the small-N case (chain count typically <= 8).
    public void GetNearestEnemies(Vector3 origin, UnitController self, UnitController exclude, int count, List<UnitController> results)
    {
        if (results == null) return;
        results.Clear();
        if (self == null || count <= 0) return;
        int enemyMask = self.EnemyLayer.value;

        // Parallel arrays: results[k] / bestSqr[k] kept ordered ascending by distance.
        // Stack-buffer for the common small-count path; heap fallback for large requests.
        Span<float> bestSqr = count <= 32 ? stackalloc float[count] : new float[count];
        for (int i = 0; i < count; i++) bestSqr[i] = float.PositiveInfinity;
        int filled = 0;

        for (int i = 0; i < _activeUnits.Count; i++)
        {
            var u = _activeUnits[i];
            if (u == null || u == self || u == exclude || !u.IsAlive) continue;
            if ((enemyMask & (1 << u.gameObject.layer)) == 0) continue;
            float sqr = ((Vector2)(u.transform.position - origin)).sqrMagnitude;

            if (sqr >= bestSqr[count - 1]) continue;

            int insertAt = filled; // default: append at the end of the filled range
            for (int k = 0; k < filled; k++)
            {
                if (sqr < bestSqr[k]) { insertAt = k; break; }
            }

            // Shift tail right by one slot (drops bestSqr[count-1] when full).
            int shiftEnd = filled < count ? filled : count - 1;
            for (int k = shiftEnd; k > insertAt; k--) bestSqr[k] = bestSqr[k - 1];
            bestSqr[insertAt] = sqr;

            if (filled < count)
            {
                results.Add(null);
                filled++;
            }
            for (int k = results.Count - 1; k > insertAt; k--) results[k] = results[k - 1];
            results[insertAt] = u;
        }
    }

    private void Register(UnitController unit)
    {
        if (unit == null) return;
        if (_activeUnits.Contains(unit)) return;
        _activeUnits.Add(unit);
        unit.OnReleased += HandleUnitReleased;
    }

    private void HandleUnitReleased(UnitController unit)
    {
        if (unit == null) return;
        unit.OnReleased -= HandleUnitReleased;
        _activeUnits.Remove(unit);
    }

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

        for (int i = 0; i < _activeUnits.Count; i++)
        {
            var u = _activeUnits[i];
            if (u != null) u.OnReleased -= HandleUnitReleased;
        }
        _activeUnits.Clear();
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

        Register(unit);

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
