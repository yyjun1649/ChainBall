using System.Collections;
using UnityEngine;

public class UpKeepPhase : PhaseBase
{
    [SerializeField] private float _turnDeltaTime = 1f;

    public override IEnumerator Execute()
    {
        GameManager.Field?.TickAllEffects(_turnDeltaTime);
        yield break;
    }
}
