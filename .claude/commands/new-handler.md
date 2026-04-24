---
description: Scaffold a new Handler under the `Handlers` root (no duplicate singletons).
argument-hint: <HandlerName>
---

Create a new Handler named `$ARGUMENTS` following this project's `Handlers` + Handler pattern (see `ARCHITECTURE.md` §2).

## Requirements

1. **Do not introduce a new singleton.** The Handler is a `MonoBehaviour` attached to a child GameObject of the `Handlers` root; `Handlers.cs` resolves it via `GetComponentInChildren`.
2. **Do not cache the Handler** in fields of other classes. Callers go through `Handlers.$ARGUMENTS` every time.
3. Place the file at `Assets/@Library/Script/Handler/$ARGUMENTS.cs` (alongside the other Handlers). Do not create `.meta` — Unity generates those.
4. Use `UniTask` for any async APIs. No coroutines.
5. If the Handler owns Addressable handles or other disposables, expose a `ClearCache()` / `Dispose()` and document when callers should invoke it.

## Steps

1. Read `ARCHITECTURE.md` §2 to confirm the pattern hasn't drifted.
2. Open `Assets/@Library/Script/Handler/Handlers.cs` and verify the `Awake` wiring site (where `GetComponentInChildren<XxxHandler>()` calls live).
3. Create `$ARGUMENTS.cs` with:
   - `public class $ARGUMENTS : MonoBehaviour` inside `namespace Library`.
   - Minimal API surface — add methods only as needed.
   - Public init method `Initialize()` or `InitializeAsync(CancellationToken)` if it requires setup before use. If so, add an `await` or call in `Handlers.Initialize()`.
4. In `Handlers.cs`, show the user the diff **before applying**:
   - Add `private $ARGUMENTS _$0argumentsCamel;` field.
   - Add `public static $ARGUMENTS $0argumentsTrimmed => Instance._$0argumentsCamel;` accessor (drop the `Handler` suffix from the accessor name if the class name ends in `Handler`, e.g. `InventoryHandler` → `Handlers.Inventory`).
   - Add `_$0argumentsCamel = GetComponentInChildren<$ARGUMENTS>();` in `Awake`.
5. Do not create `.meta` or `.prefab` files. Instruct the user to:
   - Open Unity once so the `.meta` for the new script is generated.
   - Attach the new component to a **child GameObject** of the `Handlers` GameObject in the scene/prefab.

## Output

- The new file content.
- The `Handlers.cs` diff (field + accessor + `GetComponentInChildren` line).
- A short note: "After saving, open Unity so (a) the .meta is generated and (b) the `$ARGUMENTS` component is attached as a child of the `Handlers` GameObject."
