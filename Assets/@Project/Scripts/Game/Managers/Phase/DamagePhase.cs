using System.Collections;
using UnityEngine;

public class DamagePhase : PhaseBase
{
    [SerializeField] private int _baseDangerDamage = 1;

    public override IEnumerator Execute()
    {
        var field = GameManager.Field;
        var player = GameManager.Instance.Player;
        if (field == null) yield break;

        var crossed = field.CrossedDanger;
        if (crossed.Count > 0)
        {
            if (player != null)
                player.TakeDanger(_baseDangerDamage * crossed.Count);

            for (int i = 0; i < crossed.Count; i++)
            {
                var b = crossed[i];
                if (b != null && b.IsAlive) b.Death(true);
            }
        }
        field.ClearCrossedDanger();
        yield break;
    }
}
