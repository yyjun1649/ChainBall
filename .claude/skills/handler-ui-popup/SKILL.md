---
name: handler-ui-popup
description: How to create and show UI popups / modals / dialogs in this Unity project via `Handlers.UI` and `PopupBase`. Use whenever the user or the code mentions popups, modals, dialogs, opening/closing UI screens, ESC-to-close, popup stack, sort order, or a new class inheriting from `PopupBase`. Always prefer `Handlers.UI.Show<T>()` over instantiating UI prefabs directly.
paths: Assets/@Project/**/*.cs, Assets/@Library/**/*.cs
---

# Handler — UI Popup

All popup/modal UI in this project is owned by `Library.Handlers.UI` (source: `Assets/@Library/Script/Handler/UIHandler.cs`). Popups derive from `PopupBase` (source: `Assets/@Library/Script/UI/PopupBase.cs`).

## Entry points

```csharp
using Library;

// open immediately (most common)
Handlers.UI.Show<SettingsPopup>();

// get the instance without showing (rarely needed)
var popup = Handlers.UI.GetPopup<SettingsPopup>();
popup.SomeSetter(...);
popup.Show();

// close every open popup on the stack — for scene transitions
Handlers.UI.CloseAllPopup();
```

- `Show<T>()` and `GetPopup<T>()` use `typeof(T).ToString()` as the Addressable key by default. Pass an explicit `popupName` string only if the Addressable entry uses a different name.
- The popup prefab is resolved via `Handlers.Resource.GetPopup(name, out isNew)` — i.e. the Addressable key **must** match the class name (or the override).
- On first resolution, `UIHandler` parents the popup under `_trParent`, zeroes its local transform, and calls `popup.SetAction(OnShow, OnHide)` to wire stack bookkeeping.

## PopupBase contract

```csharp
public class SettingsPopup : PopupBase
{
    // set in the inspector (or leave the Reset() auto-assign to grab the root Canvas)
    // [SerializeField] protected Canvas _canvas;  // inherited

    public override void HandleEvent(eEventType eventType)
    {
        // react to game events you subscribed to via Handlers.Event
    }

    // optionally override Show()/Hide() — call base.Show()/base.Hide() FIRST
}
```

Required for any `PopupBase` subclass:

1. **Implement `HandleEvent(eEventType)`** — it's abstract. Return early if you don't care; don't throw.
2. **Assign `_canvas`** — the inspector field on `PopupBase`. `PopupBase.Reset()` auto-grabs `GetComponent<Canvas>()` in the editor; if your root GameObject has no Canvas, assign it manually.
3. **Set `IsOnStack`** — a public field on `PopupBase`.
   - `true` for modal dialogs that ESC should close in LIFO order (confirm dialogs, settings, inventory, shop).
   - `false` for non-modal overlays (HUDs, toasts, persistent sidebars) — they won't be pushed onto the stack and ESC won't close them.

## Auto behavior you must not duplicate

- **Event subscription**: `PopupBase.Show()` calls `Handlers.Event.Subscribe(this)`, `Hide()` calls `Unsubscribe(this)`. Don't call Subscribe/Unsubscribe yourself in popup code.
- **Sort order**: `UIHandler` increments `_layerIndex` on `OnShow` and calls `popup.SetSortIndex(_layerIndex)`, which writes to `_canvas.sortingOrder`. **Do not write to `sortingOrder` directly** — you'll desync the stack ordering.
- **ESC key**: `UIHandler.Update()` checks `Input.GetKeyDown(KeyCode.Escape)` and pops the top popup (only popups with `IsOnStack = true` are on the stack). Don't add a custom ESC handler that also calls `Hide()` — you'll double-pop.
- **Instance caching**: `UIHandler.GetPopup<T>` is idempotent. The same instance is reused every time; `Show()` just re-activates the GameObject. Don't `Destroy` popups yourself — use `Handlers.Resource.ClearPopupCache()` at major scene boundaries if memory pressure requires it.

## When the popup needs runtime data

There's no built-in `Show<T>(data)` overload. Pattern:

```csharp
var popup = Handlers.UI.GetPopup<ConfirmPopup>();
popup.Configure(message, onYes, onNo);
popup.Show();
```

`Configure` is a method you add on the subclass. Call it **before** `Show()` so the popup already has its state when `OnShow` fires.

## Override `Show` / `Hide` safely

If you need extra work on show/hide, override and call the base first so event subscribe/dispatch happens in the right order:

```csharp
public override void Show()
{
    base.Show();          // activates GO, dispatches OnShow, subscribes to Handlers.Event
    _refreshCoroutine = RefreshAsync().Forget();   // UniTask, not coroutine
}

public override void Hide()
{
    _cts?.Cancel();
    base.Hide();
}
```

## Hard don'ts

- ❌ `Instantiate(popupPrefab, parent)` — bypasses UIHandler, breaks stack and ESC.
- ❌ `popup.gameObject.SetActive(true)` directly — call `popup.Show()` so `OnShow` fires.
- ❌ Custom ESC handlers that also close popups.
- ❌ Writing to `_canvas.sortingOrder` in popup code.
- ❌ Subscribing to `Handlers.Event` manually from a `PopupBase` subclass (the base class already does it).
- ❌ `private UIHandler _ui = Handlers.UI;` — caching the handler.
- ❌ Coroutines for show/hide animations — use UniTask.

## Checklist when writing a new popup

- [ ] Class inherits `PopupBase`, is in a namespace consistent with your UI layer.
- [ ] `HandleEvent(eEventType)` implemented.
- [ ] `IsOnStack` set correctly for the popup's intent.
- [ ] Root prefab has a `Canvas`; `_canvas` wired.
- [ ] Addressable entry created with key == class name (or pass an override string to `Show<T>(name)`).
- [ ] No direct `Instantiate` / `SetActive` in calling code — everything goes through `Handlers.UI.Show<T>()`.
