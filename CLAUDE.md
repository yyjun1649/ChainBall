# RogueLikeTemplate — Claude Code Project Guide

> This file is auto-loaded at the start of every Claude Code session and re-injected after `/compact`.
> Rules here are non-negotiable. See `@ARCHITECTURE.md` for deeper design context.

@ARCHITECTURE.md

---

## 🗣️ Response Language

- **Always reply to the user in Korean (한국어).**
- Code, identifiers, file paths, commit messages, and committed documentation stay in **English**.
- Inline code comments stay in **English** unless the user explicitly asks for Korean.
- When quoting error messages, tool output, or command syntax, keep them verbatim (do not translate).

---

## ⛔ Hard Prohibitions (Unity Danger Files)

Claude MUST NOT directly edit, create, or delete any of the following. If a change requires touching them, stop and ask the user to perform the action inside the Unity Editor instead.

| Target | Reason |
|---|---|
| `*.meta` | GUID file — corrupting it breaks every reference in the project |
| `*.unity` (scenes) | YAML structure; manual edits can render the scene unrecoverable |
| `*.prefab` | YAML structure; field tweaks must go through the Editor |
| `*.asset` (ScriptableObject instances, etc.) | GUID / FileID chains; use the Editor or an editor script |
| `Library/`, `Temp/`, `Logs/`, `obj/` | Unity-regenerated caches. Never hand-edit |
| `Assets/AddressableAssetsData/**/*.bin*` | Addressables build artifacts |
| `UserSettings/` | Per-user Editor state; not committed |

### ⚠️ Requires explicit user approval before modifying

- `ProjectSettings/**` — build, physics, graphics; project-wide blast radius
- `Packages/manifest.json`, `Packages/packages-lock.json` — dependency graph
- Any **new** `*.asmdef` / `*.asmref`, or structural changes to existing ones — assembly boundaries affect compile time and reference topology
- `.gitignore`, `.mcp.json`, `.claude/settings.json` — shared team configuration

---

## 🧭 Project Overview

- **Unity**: `6000.0.43f1` (Unity 6)
- **Render Pipeline**: Universal RP 17.0.4
- **Target Platforms**: Mobile (iOS / Android)
- **Input**: Unity Input System 1.13.1
- **Asset Streaming**: Addressables 2.3.16 (with unity-addressable-importer)
- **Core Libraries**:
  - UniTask — async
  - ZString — zero-alloc string formatting
  - ZLinq — zero-alloc LINQ
  - Newtonsoft.Json — JSON serialization
  - ProCamera2D, EnhancedScroller v2, Coffee UI Effects
- **Folder Layout**:
  - `Assets/@Project/` — gameplay logic, data, scenes, UI
  - `Assets/@Library/` — in-house reusable scripts, utilities, shaders
  - `Assets/@ThirdParty/` — external / store assets
  - `Assets/Plugins/`, `Assets/NuGet/` — binary dependencies
  - `Assets/@Project/Scripts/{Common, Data, Define, Game, UI}` for code classification

---

## 🏗️ Architecture Rules (Strict)

1. **`Handlers` is the home for *system-level* concerns.**
   - `Library.Handlers` (`SingletonBehaviour<Handlers>`, in `Assets/@Library/Script/Handler/Handlers.cs`) owns the cross-cutting, project-wide systems: asset loading (`ResourceHandler`), UI popups (`UIHandler`), scene transitions (`SceneHandler`), event bus (`EventHandler`), audio (`SoundHandler`), time/clock (`TimeHandler`).
   - Each system-level Handler is a `MonoBehaviour` attached to a **child GameObject** of the `Handlers` object, wired up in `Handlers.Awake` via `GetComponentInChildren<XxxHandler>()`.
   - Access system Handlers exclusively via their static accessors: `Handlers.Resource`, `Handlers.UI`, `Handlers.Scene`, `Handlers.Event`, `Handlers.Sound`, `Handlers.Time`.
2. **Add a new system Handler when a concern is genuinely cross-cutting** (spans scenes, shared by many features, has no natural feature owner). Use `/new-handler` — it adds the field, static accessor, and `GetComponentInChildren` line. Gameplay/feature code that belongs to a single system or scene stays in that system/scene; do **not** promote feature logic to a Handler just to expose it globally.
3. **Do not cache Handler references in fields.** Avoid `private UIHandler _ui = Handlers.UI;`. Call `Handlers.Xxx.Method()` at the point of use — this preserves lifecycle safety across scene loads.
4. **Tunable gameplay data goes through SpecData** (`Assets/@Project/Scripts/SpecData/`). xlsx → JSON + `Spec*.g.cs`, accessed at runtime via `SpecDataManager.Spec*`. ScriptableObject is reserved for asset-reference catalogs only (e.g. `FeelPresetTable`, `SpecDataSettings`). See `ARCHITECTURE.md` §3.
5. **UI uses the `PopupBase` + `Handlers.UI`** pattern (see the `handler-ui-popup` skill). Every popup inherits `PopupBase` and is opened via `Handlers.UI.Show<TPopup>()`.

---

## ✍️ Coding Conventions

- **Async**: `UniTask` is the default. Coroutines are acceptable only where they already exist (`TimeHandler`) or when a Unity API genuinely requires one — new async work should use `UniTask`.
- **UniTask lifecycle (mandatory)**: every non-trivial `UniTask` takes a `CancellationToken`; tie it to the owner's lifecycle:
  - MonoBehaviour: `this.GetCancellationTokenOnDestroy()`.
  - `PopupBase`: scope a `CancellationTokenSource` to `Show()` and cancel it in `Hide()`.
  - Scene-wide work: scope to the owning `BaseScene` and cancel in `OnSceneDestroy`.
  - Fire-and-forget via `.Forget()` is only acceptable if the task is bounded AND its exceptions are handled inside the task body. Never use `async void` in our code (except Unity lifecycle methods).
- **Reactive**: prefer R3-style Observables. If the R3 package is not yet installed, confirm with the user before adding it.
- **String formatting**: use `ZString` on hot paths (Update / per-frame / alloc-sensitive).
- **LINQ**: `ZLinq` on hot paths; standard LINQ acceptable off hot paths.
- **JSON**: `Newtonsoft.Json`.
- **Naming**: C# standard — PascalCase (public), camelCase (locals), `_camelCase` (private fields).
- **GC discipline**: minimize `new` on hot paths, no `string` concatenation, avoid boxing.

---

## 🔧 Modification Principles

- **Minimize changes to existing code.** Refactors require user approval.
- **Design changes follow: update `ARCHITECTURE.md` → user confirms → implement.** Use `/arch-update`.
- One task, one purpose. Don't bundle unrelated cleanups or refactors.
- **Comments are minimal** — one line only, and only when the WHY is non-obvious.
- For bug fixes, match the scope exactly. Don't widen the blast radius.

---

## 🚀 Frequently Used Commands

```bash
# Unity project state
git status
git diff

# CLI build (placeholder — update once the build script is defined)
# "Unity.exe" -batchmode -quit -projectPath . -executeMethod BuildScript.Build -logFile -

# Test runner (placeholder)
# "Unity.exe" -batchmode -runTests -projectPath . -testResults ./test-results.xml -logFile -
```

> Fill in real build-script paths and methods once they exist.

---

## 📌 Session Checklist

Before starting work:
- [ ] Does the request touch prohibited files (`*.meta`, `*.unity`, `*.prefab`)?
- [ ] Does it touch `ProjectSettings`, `manifest.json`, or `asmdef`? — ask first
- [ ] New Handler? → use `/new-handler`
- [ ] New tunable data? → add `T*` sheet to `SpecData/Xlsx/Spec.xlsx` (not a new SO). See `Docs/Specs/`.
- [ ] Design change? → run `/arch-update` first

After finishing:
- [ ] Run a mobile-performance review (`/review-mobile`)
- [ ] Confirm no `.meta` files were created by hand (Unity generates those)
