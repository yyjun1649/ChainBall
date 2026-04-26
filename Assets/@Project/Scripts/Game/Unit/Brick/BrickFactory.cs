using System;
using Library;
using UnityEngine;

// Bridges BrickPatternParser output to UnitController + UnitView instances pulled from
// Handlers.Pool. type/element are written to UnitData (MVC: Controller stays a pure event
// publisher; Data owns state).
//
// Pool model:
//   Controller: ONE prefab shared by all brick types — Addressable "UnitController_{_brickPrefabId}".
//   View      : ONE prefab per type (5 visual variants) + per-boss prefabs for BOSS type.
//                Each registered as "UnitView_{viewId}".
//
// HP defaults per type are coded here for v0.1; promote to SpecBrick when that table arrives.
public class BrickFactory : MonoBehaviour
{
    [Header("Controller")]
    [SerializeField] private int       _brickPrefabId = 1; // → Addressable "UnitController_1"
    [SerializeField] private LayerMask _myLayer;
    [SerializeField] private LayerMask _enemyLayer;

    [Header("View — id by eBrickType (length must equal enum count)")]
    [Tooltip("Index = (int)eBrickType. Order: NORMAL, SHIELD, EXPLOSIVE, SPAWNER, REFLECTOR, BOSS.\n" +
             "BOSS entry is ignored — bossId-specific mapping below is used.")]
    [SerializeField] private int[] _viewIdByType = { 1, 2, 3, 4, 5, 0 };

    [Header("View — boss id → view id")]
    [SerializeField] private BossViewEntry[] _bossViewMap;

    [Serializable]
    public struct BossViewEntry
    {
        public int bossId;
        public int viewId;
    }

    public UnitController Spawn(BrickPatternParser.ParsedCell parsed, Vector2Int gridPos)
    {
        // 1) Controller
        var unit = Handlers.Pool.Get<UnitController>(_brickPrefabId);

        var data = new UnitData();
        data.InitializeBare($"brick_{parsed.type}");
        data.AddModule(new BrickMeta
        {
            type    = parsed.type,
            element = parsed.element,
            bossId  = parsed.bossId,
        });

        int hp = parsed.hp > 0 ? parsed.hp : DefaultHp(parsed.type);
        data.Stats.AddModifier(TStatModifier<eStatType>.MakeModifier(
            "Brick_Health", eModifierType.Flat, eStatType.Health, hp, false, "Brick"));
        data.CalculateStat();

        unit.Initialize(data, _myLayer, _enemyLayer);

        // 2) View — pure event subscriber. Reads data.brickType / data.element via controller.Data if needed.
        int viewId = ResolveViewId(parsed.type, parsed.bossId);
        if (viewId > 0)
        {
            var view = Handlers.Pool.Get<UnitView>(viewId);
            view.Initialize(unit);
        }

        return unit;
    }

    private int ResolveViewId(eBrickType type, int bossId)
    {
        if (type == eBrickType.BOSS)
        {
            if (_bossViewMap != null)
            {
                for (int i = 0; i < _bossViewMap.Length; i++)
                {
                    if (_bossViewMap[i].bossId == bossId) return _bossViewMap[i].viewId;
                }
            }
            Debug.LogError($"[BrickFactory] no view mapping for BOSS bossId={bossId}");
            return 0;
        }

        int idx = (int)type;
        if (_viewIdByType == null || idx < 0 || idx >= _viewIdByType.Length)
        {
            Debug.LogError($"[BrickFactory] _viewIdByType not configured for type={type}");
            return 0;
        }
        return _viewIdByType[idx];
    }

    static int DefaultHp(eBrickType type) => type switch
    {
        eBrickType.NORMAL    => 1,
        eBrickType.SHIELD    => 1,
        eBrickType.EXPLOSIVE => 1,
        eBrickType.SPAWNER   => 2,
        eBrickType.REFLECTOR => 99, // unkillable in v0.1
        eBrickType.BOSS      => 50, // placeholder; boss spec arrives Phase 9
        _ => 1,
    };
}
