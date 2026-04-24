---
description: Create a new ScriptableObject data type (and instructions for the .asset).
argument-hint: <DataName>
---

Create a new ScriptableObject data class named `$ARGUMENTS` for this project.

## Location

`Assets/@Project/Scripts/Data/$ARGUMENTS.cs`

## Template Checklist

1. `public class $ARGUMENTS : ScriptableObject`
2. `[CreateAssetMenu(fileName = "$ARGUMENTS", menuName = "Data/$ARGUMENTS")]`
3. Fields:
   - `[SerializeField] private` with a public read-only property — never raw public fields.
   - Use simple serializable types; avoid runtime-only references.
4. Treat instances as **immutable at runtime**. Do not add `Set*` methods; data is authored in the
   Editor.
5. If this data is part of a table, prefer a typed wrapper SO (e.g., `$ARGUMENTSTable` holding
   `IReadOnlyList<$ARGUMENTS>`).

## Do NOT

- Do not create the `.asset` file or any `.meta` file directly.
- Do not register a runtime mutation API.

## Output

- The `.cs` content.
- Step-by-step instructions for the user to create the asset in Unity
  (`Create ▸ Data ▸ $ARGUMENTS`).
- Suggested Addressables label if the asset will be loaded via Addressables.
