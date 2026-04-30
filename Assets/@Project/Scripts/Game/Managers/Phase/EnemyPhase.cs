using System.Collections;
using UnityEngine;

public class EnemyPhase : PhaseBase
{
    // Debug pattern used until SpecWave wiring lands (Phase 9). Length must equal FieldHandler.COLS.
    [SerializeField] private string[] _debugPattern = { "N", "N", "N", ".", ".", "N", "N", "N" };

    public override IEnumerator Execute()
    {
        var field = GameManager.Field;
        if (field == null) yield break;

        field.ShiftAllDown();
        field.AddRowFromPattern(_debugPattern);
        yield break;
    }
}
