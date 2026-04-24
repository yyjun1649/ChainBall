---
name: handler-scene
description: How to transition between Unity scenes in this project via `Handlers.Scene` and `BaseScene`. Use whenever the user or the code mentions scene change, scene loading, transitioning between scenes, `SceneManager.LoadScene`, `LoadSceneAsync`, or writing a new scene root. Always prefer `Handlers.Scene.ChangeSceneAsync` over direct `SceneManager` calls.
paths: Assets/@Project/**/*.cs, Assets/@Library/**/*.cs
---

# Handler — Scene

Scene loading in this project is owned by `Library.Handlers.Scene` (source: `Assets/@Library/Script/Handler/SceneHandler.cs`). Every scene root inherits `BaseScene` (source: `Assets/@Library/Script/Scene/BaseScene.cs`).

## Entry point

```csharp
using System.Threading;
using Cysharp.Threading.Tasks;
using Library;

// await a scene change — the common pattern
await Handlers.Scene.ChangeSceneAsync("Stage_01", cancellationToken);

// if you don't have a token, pass CancellationToken.None (default)
await Handlers.Scene.ChangeSceneAsync("Stage_01");

// current scene access
var scene = Handlers.Scene.CurrentScene;  // the BaseScene that called RegisterScene
```

## Public API

```csharp
public class SceneHandler : MonoBehaviour
{
    public BaseScene CurrentScene { get; private set; }

    public UniTask ChangeSceneAsync(string sceneName, CancellationToken cancellationToken = default);
    public void RegisterScene(BaseScene scene);  // called by BaseScene.Awake
}
```

## Critical caveats

### Single-flight load gate

`SceneHandler` ignores `ChangeSceneAsync` if a load is already in progress (`_isLoading == true`). The second call returns immediately without awaiting the in-flight load — so the caller will not "wait" for the first load via the second call. Don't fire two scene changes back to back and expect both to sequence.

If you need sequencing, await the first `ChangeSceneAsync` before invoking the second.

### Cancellation semantics

`cancellationToken` is forwarded into `SceneManager.LoadSceneAsync(...).ToUniTask(cancellationToken: ...)`. Cancellation raises `OperationCanceledException` on the awaiter — make sure upstream code is ready to catch it. The `_isLoading` flag is released in a `finally` block so a cancelled load will not permanently jam the handler.

Unity's scene load itself cannot be aborted mid-flight; cancellation is honored at the UniTask awaiter boundary. The scene may finish loading anyway.

## BaseScene contract

Every scene's root GameObject must carry a `BaseScene` subclass:

```csharp
public class StageScene : BaseScene
{
    protected override void OnSceneLoaded()
    {
        // called from Start() — scene is live, all MonoBehaviours have Awoken
        Handlers.UI.Show<HudPopup>();
    }

    protected override void OnSceneDestroy()
    {
        // called from OnDestroy() — scene is tearing down
        Handlers.UI.CloseAllPopup();
        Handlers.Resource.ClearPopupCache();
    }
}
```

`BaseScene.Awake` automatically calls `Handlers.Scene.RegisterScene(this)`, so `Handlers.Scene.CurrentScene` is live from the first `Start()` pass onward. You never call `RegisterScene` yourself.

**`OnSceneLoaded` / `OnSceneDestroy` are abstract** — you must implement them even if the body is empty.

## Cleanup conventions on scene transition

`SceneHandler.ChangeSceneAsync` does not clean up UI or Addressable caches for you. Typical pattern in the outgoing scene's `OnSceneDestroy`:

```csharp
protected override void OnSceneDestroy()
{
    Handlers.UI.CloseAllPopup();            // pop the popup stack
    Handlers.Resource.ClearPopupCache();    // release popup Addressable handles + destroy instances
}
```

Leaking popup/prefab handles across scenes is the #1 memory-pressure bug on mobile in this project's pattern.

## Hard don'ts

- ❌ `SceneManager.LoadScene(...)` / `SceneManager.LoadSceneAsync(...)` direct — bypasses the load gate and `CurrentScene` tracking.
- ❌ Firing two `ChangeSceneAsync` calls without awaiting the first — second one silently no-ops.
- ❌ Scene roots that don't inherit `BaseScene` — `Handlers.Scene.CurrentScene` will be stale after the transition.
- ❌ Caching the handler: `private SceneHandler _s = Handlers.Scene;` — always call via `Handlers.Scene`.
- ❌ Coroutines for scene loading — this project uses UniTask.

## Checklist when adding/changing scenes

- [ ] Scene root has a `BaseScene` subclass with `OnSceneLoaded` and `OnSceneDestroy` implemented.
- [ ] Transition uses `await Handlers.Scene.ChangeSceneAsync(name, cancellationToken)`.
- [ ] Caller owns a `CancellationToken` tied to its lifecycle (see `CLAUDE.md` §✍️ UniTask lifecycle).
- [ ] Outgoing scene's `OnSceneDestroy` calls `Handlers.UI.CloseAllPopup()` + `Handlers.Resource.ClearPopupCache()`.
- [ ] No direct `SceneManager` calls.
