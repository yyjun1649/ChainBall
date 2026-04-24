---
description: Review the pending changes for mobile performance regressions.
---

Audit the current working-tree changes against mobile-performance guardrails. Assume Unity 6 + URP,
target iOS/Android.

## Scope

Only the files modified in this session / working tree. Use `git status` and `git diff` to scope.

## Checks

### GC / allocation (hot paths = Update, FixedUpdate, per-frame callbacks)

- Any `new` inside a hot path? — flag unless reuse via pool or struct.
- `string` concatenation or `$""` interpolation on hot paths? — recommend `ZString`.
- Standard LINQ on hot paths? — recommend `ZLinq`.
- `foreach` over `List<T>`: OK, but no `IEnumerable<T>` abstraction on hot paths (boxing).
- `GetComponent` / `Find*` / `FindObjectOfType` in Update? — cache in `Awake` / `OnEnable`.

### Draw calls / UI

- New Canvas without a nested `CanvasGroup` strategy for invalidation?
- Text changes that force Canvas rebuild every frame? — suggest `TextMeshPro` static batching.
- SpriteAtlas membership? — confirm new sprites are atlas-bound.

### Textures / memory

- New texture imports: check max size per platform override; ASTC on mobile.
- Mipmaps on UI (should usually be off).

### Async / lifecycle

- Any `IEnumerator` coroutine added? — MUST convert to `UniTask`.
- Missing `CancellationToken` on long-lived async?
- Addressables handles released?

### Architecture rule compliance

- New **system-level** singleton (cross-cutting infra) outside `Handlers`? — REJECT, route it through a Handler instead (see `ARCHITECTURE.md` §2). Feature/scene-local singletons are a design smell but not a hard reject — ask the author about scope.
- Handler reference cached in a field? — REJECT.
- Runtime mutation of a `ScriptableObject`? — REJECT (treat SOs as immutable at runtime).
- New `async void` outside Unity lifecycle methods? — REJECT.
- New `UniTask` without a `CancellationToken` tied to its owner's lifecycle? — REJECT (see `CLAUDE.md` §✍️ UniTask lifecycle).

## Output

- Verdict: ✅ clean / ⚠️ issues.
- A bulleted list of findings with file:line references.
- Concrete fix suggestions for each finding.
