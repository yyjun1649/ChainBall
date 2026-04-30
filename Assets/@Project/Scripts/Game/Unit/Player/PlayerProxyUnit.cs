using UnityEngine;

// Phase 2 prototype shim — gives the ball-cast pipeline a non-null UnitController to use
// as `attacker` until Phase 8 promotes Player to a full UnitController-derived entity.
// Lives alongside Player on the same GameObject; remove once Player itself becomes a UnitController.
public class PlayerProxyUnit : UnitController
{
    // Layers the ball's sweep cast (BounceMovement) should hit — typically the brick / enemy layer.
    [SerializeField] private LayerMask _enemyLayer;

    private void Start()
    {
        var data = new UnitData();
        data.InitializeBare("PlayerProxy");

        LayerMask my = 1 << UnitLayer.UserLayer;
        Initialize(data, my, _enemyLayer);
    }
}
