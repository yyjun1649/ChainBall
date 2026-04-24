---
name: handler-resource
description: How to load assets in this Unity project via `Handlers.Resource` (Addressables wrapper). Use whenever the user or the code touches prefab loading, popup loading, ScriptableObject lookup, sprite atlas lookup, asset caching, "load asset / prefab / SO / sprite", or mentions Addressables at all. Always prefer this over direct `Addressables.LoadAssetAsync` or `Resources.Load`.
paths: Assets/@Project/**/*.cs, Assets/@Library/**/*.cs
---

# Handler — Resource

All asset loading in this project goes through `Library.Handlers.Resource` — a `MonoBehaviour` Handler wrapping Unity Addressables with per-category caches.

## Entry point

```csharp
using Library;

// always like this — never cache the reference in a field
var prefab = Handlers.Resource.GetPrefab("Enemy_Skeleton");
var sprite = Handlers.Resource.GetSprite("icon_gold");
```

Source: `Assets/@Library/Script/Handler/ResourceHandler.cs` (+ partials in `ResourceHandler.Partial/`).

## Public API

### Prefab (`ResourceHandler.Prefab.cs`)

```csharp
GameObject GetPrefab(string prefabName);            // sync
T          GetPrefab<T>(string prefabName);         // sync, casts to component
UniTask<GameObject> GetPrefabAsync(string prefabName);
UniTask<T>          GetPrefabAsync<T>(string prefabName);

void ClearCache();                                   // release ALL cached prefabs
void ClearPrefab(string prefabName, GameObject instance);
```

### Popup (`ResourceHandler.Popup.cs`)

```csharp
PopupBase GetPopup(string popupName, out bool isNew);        // sync
UniTask<(PopupBase popup, bool isNew)> GetPopupAsync(string popupName);
void ClearPopupCache();                                       // destroys all cached popup instances + releases their Addressable handles
```

Typically you call `Handlers.UI.Show<TPopup>()` / `Handlers.UI.GetPopup<TPopup>()` instead — UIHandler wraps `ResourceHandler.GetPopup` and adds stack/layer bookkeeping. Use `Handlers.Resource.GetPopup(...)` directly only if you need the `isNew` signal for custom factory logic.

Internally, each loaded popup records its `AsyncOperationHandle<GameObject>` alongside the instantiated `PopupBase`. `ClearPopupCache()` destroys every cached instance and releases every tracked handle — call it at scene transitions (typically in the outgoing `BaseScene.OnSceneDestroy`).

### ScriptableObject (`ResourceHandler.Scriptable.cs`)

```csharp
T GetScriptableObject<T>(string scriptableName) where T : ScriptableObject;
void ClearCacheScriptable();
```

### Sprite Atlas (`ResourceHandler.ATLAS.cs`)

```csharp
Sprite GetSprite(string spriteName);   // returns null if not found — always null-check
```

Requires `Handlers.Initialize()` to have completed (it loads all sprite atlases via `LoadSpriteAtlasAsync`).

### Mesh / Material / Shader

`GetMesh`, `GetMaterial`, `GetShader` exist but their backing `LoadXxxAsync` isn't wired into `InitializeAddressable()` yet. Assume these return `null` in the current build unless you've manually loaded them.

## Sync vs Async — which to use

- **Sync getters** (`GetPrefab`, `GetScriptableObject`) internally call `AsyncOperationHandle.WaitForCompletion()`. That blocks the main thread until the asset is on disk-cached or downloaded. Safe at boot (splash / scene `Start`) and for tiny assets. **Avoid on hot paths, during gameplay, or for anything bundled remotely.**
- **Async getters** (`GetPrefabAsync`, `GetPopupAsync`) return `UniTask`. Use them during loading screens, scene-transition fades, or anywhere the frame budget matters.

If a piece of code already runs inside a `UniTask`-returning method, prefer the async variant by default.

## Cache lifetime

Caches are **not** released automatically. Every `GetPrefab` / `GetPopup` / `GetScriptableObject` holds its Addressable handle until one of the `Clear*` methods is called. Rule of thumb:

- Per-scene popups: call `Handlers.Resource.ClearPopupCache()` in the outgoing scene's `OnSceneDestroy` (or rely on a manual pass during scene transition).
- Stage-specific prefabs: call `ClearPrefab(name, instance)` when the specific instance is destroyed, or `ClearCache()` at major scene boundaries.
- ScriptableObject tables: usually long-lived; clear only on game-wide reset.

Leaking handles on mobile = memory pressure → OOM on low-end devices. This is not optional.

## Hard don'ts

- ❌ `Resources.Load<T>(...)` / `Resources.LoadAsync<T>(...)` — the project does not use the legacy `Resources` folder.
- ❌ `Addressables.LoadAssetAsync<T>(...)` direct calls — bypasses the cache and leaks handles.
- ❌ `private ResourceHandler _rh = Handlers.Resource;` — caching a Handler reference. Call `Handlers.Resource.X()` each time.
- ❌ Calling atlas-dependent getters (`GetSprite`) before `Handlers.Initialize()` has awaited.

## Known rough edges

- `Handlers.Initialize()` currently calls `await _resourceHandler.InitializeAddressable()` twice (copy-paste duplicate in `Handlers.cs`). The second call is a fast no-op thanks to the `IsInitialize` guard, but it prints a red `Debug.LogError`. Leave it unless explicitly asked to fix — it's out of scope for normal asset-loading changes.
- `InitializeAddressable()` only loads sprite atlases today (`LoadBigTextureAsync` is commented out). If you write code that depends on preloaded BigTextures/Meshes/Materials, flag that to the user before proceeding.
