# Architecture — RogueLikeTemplate

> This is an **initial draft**. Keep it current: every design change must land here **before** the code
> change (use `/arch-update`). Imported by `CLAUDE.md`, so anything written here is part of the
> project contract.

---

## 1. High-Level Shape

```
┌─────────────────────────────────────────────────────────────┐
│                         Unity Runtime                        │
│                                                              │
│   ┌──────────────────────────────────────────────────────┐   │
│   │         Handlers (SingletonBehaviour<Handlers>)      │   │
│   │         — static accessors: Handlers.Resource,       │   │
│   │           Handlers.UI, Handlers.Scene, …             │   │
│   │                                                      │   │
│   │   ┌──────────────┐ ┌──────────────┐ ┌──────────────┐ │   │
│   │   │Resource-     │ │UIHandler     │ │SceneHandler  │ │   │
│   │   │Handler       │ │(child MB)    │ │(child MB)    │ │   │
│   │   │(child MB)    │ │              │ │              │ │   │
│   │   └──────────────┘ └──────────────┘ └──────────────┘ │   │
│   │   ┌──────────────┐ ┌──────────────┐ ┌──────────────┐ │   │
│   │   │EventHandler  │ │SoundHandler  │ │TimeHandler   │ │   │
│   │   └──────────────┘ └──────────────┘ └──────────────┘ │   │
│   │   ┌──────────────┐ ┌──────────────┐                   │   │
│   │   │PoolHandler   │ │FeelHandler   │                   │   │
│   │   └──────────────┘ └──────────────┘                   │   │
│   └──────────────────────────────────────────────────────┘   │
│                                                              │
│   ScriptableObject Data ──▶ consumed by Handlers / UI        │
│   Addressables         ──▶ async load (UniTask)              │
│   UI: PopupBase + Handlers.UI.Show<TPopup>()                 │
└─────────────────────────────────────────────────────────────┘
```

"child MB" = MonoBehaviour on a child GameObject of the `Handlers` object. Each Handler is resolved once via `GetComponentInChildren<T>()` in `Handlers.Awake`.

---

## 2. Handlers + Handler Pattern

### Scope: what belongs under `Handlers`

`Library.Handlers` is the **project's system layer** — infrastructure concerns that every feature reaches for but that no single feature owns. The concerns currently living here are:

- `ResourceHandler` — Addressable-backed asset cache (prefab / popup / SO / sprite / mesh / material / shader).
- `UIHandler` — popup stack + ESC dismiss + sort order.
- `SceneHandler` — scene transition with single-flight load gate; owns the `LoadingCanvas` shown during transitions (see §2.5).
- `EventHandler` — cross-feature event bus (`IEvent` subscribers).
- `SoundHandler` — BGM / SFX / ambience routing.
- `TimeHandler` — server-synced clock + tick/minute/day callbacks.
- `PoolHandler` — typed object pool registry (`PoolMonoBehaviour<T>` instances keyed by `Type` + int id).
- `FeelHandler` — Feel (MMF_Player) integration: preset catalog, global playback control, pooled in-game FX path (see §10).

**Gameplay, feature, or scene-local code does not go under `Handlers`.** If a system is only used by one feature or one scene, keep it in that feature/scene. Promote to a Handler only when a concern is genuinely cross-cutting.

### Rules

- A **Handler** is a `MonoBehaviour` placed as a child GameObject under the `Handlers` object, and owns one system concern.
- `Handlers.Awake` wires each Handler via `GetComponentInChildren<XxxHandler>()`. Handlers never self-register.
- Access is always through static accessors: `Handlers.Resource.DoThing()`, `Handlers.UI.Show<TPopup>()`.
- **Do not cache Handler references** in fields. Handlers can be swapped / reset during scene flow; caching leads to stale references.

### Real Skeleton (source of truth: `Assets/@Library/Script/Handler/Handlers.cs`)

```csharp
namespace Library
{
    public class Handlers : SingletonBehaviour<Handlers>
    {
        public static ResourceHandler Resource => Instance._resourceHandler;
        public static UIHandler       UI       => Instance._uiHandler;
        public static SceneHandler    Scene    => Instance._sceneHandler;
        public static EventHandler    Event    => Instance._eventHandler;
        public static SoundHandler    Sound    => Instance._soundHandler;
        public static TimeHandler     Time     => Instance._timeHandler;

        private ResourceHandler _resourceHandler;
        private UIHandler       _uiHandler;
        // …

        protected override void Awake()
        {
            base.Awake();
            _resourceHandler = GetComponentInChildren<ResourceHandler>();
            _uiHandler       = GetComponentInChildren<UIHandler>();
            // …
        }

        public async UniTask Initialize(CancellationToken cancellationToken = default)
        {
            await _resourceHandler.InitializeAddressable(cancellationToken);
            _timeHandler.StartTimeCoroutine();
        }
    }
}
```

### When to add a new Handler

- A new, independent global concern that spans scenes or systems.
- You would otherwise be tempted to create a second singleton — don't; add a Handler instead.
- Use `/new-handler`. The workflow is: create `XxxHandler : MonoBehaviour`, attach it to a child GameObject of the `Handlers` object in the scene/prefab (Editor step), then add the field, static accessor, and `GetComponentInChildren<XxxHandler>()` line in `Handlers.cs`.

---

## 2.5 Scene Flow & Transition Loading

### Scene graph

```
SplashScene → TitleScene → LobbyScene ⇄ GameScene
  (entry)     (init)        (meta)      (play)
```

- **SplashScene** — app entry. `Handlers` 루트를 `DontDestroyOnLoad`로 승격시키고 즉시 `TitleScene`으로 전환. 다른 부팅 로직은 넣지 않는다.
- **TitleScene** — 시스템/데이터 초기화 게이트. `await Handlers.Initialize()` → `UserData` 로드/생성 → `LobbyScene` 진입. 실패 가능한 부팅 로직은 전부 여기서 끝낸다. 로비는 "데이터가 준비된 상태"만 전제한다.
- **LobbyScene** — 비-전투 메타 게임 (선택, 상점, 설정). 시스템 초기화 재실행 금지.
- **GameScene** — 실제 전투 루프. 전투용 `CancellationToken`은 `OnSceneDestroy`에서 일괄 취소한다.

모든 씬 전환은 항상 `Handlers.Scene.ChangeSceneAsync(...)` 한 경로로 수행한다. `SceneManager.LoadScene` 직접 호출 금지.

### Transition loading — `LoadingCanvas` owned by `SceneHandler`

로딩 화면은 **팝업이 아니다**. 의미적으로 "씬 전환의 일부"이므로 `SceneHandler`가 Canvas를 직접 소유한다.

- 소유: `SceneHandler` GameObject의 자식으로 `LoadingCanvas`를 배치. `SceneHandler`는 `[SerializeField] Canvas _loadingCanvas`로 참조.
- **`PopupBase` 상속 금지**. `UIHandler`를 거치지 않는다. ESC / `CloseAllPopup` 영향 없음.
- 활성 토글은 `SceneHandler` 내부 로직에서만 수행된다. 외부에서 직접 `SetActive` 호출 금지.

### Sort order 규약 (reservation)

| Layer              | `sortingOrder` | 비고 |
|--------------------|----------------|------|
| Gameplay / HUD     | ≤ 0            | 모든 씬 내부 Canvas는 이 범위 |
| **LoadingCanvas**  | **0 (fixed)**  | 프리팹에 박아두고 런타임 수정 금지 |
| Popups (incl. error) | 1 ~ N         | `UIHandler._layerIndex++`로 증가 |

결과: 로딩은 게임플레이를 덮지만, 에러 팝업(그리고 그 외 모든 `PopupBase`)은 항상 로딩 **위**에 표시된다.

### `ChangeSceneAsync` API

```csharp
public async UniTask ChangeSceneAsync(
    string sceneName,
    bool  useLoading  = false,   // LoadingCanvas 표시 여부
    float minDuration = 0f,      // 최소 노출 시간 (초). 0이면 로드 완료 즉시 종료
    CancellationToken ct = default)
```

3가지 전환 모드가 파라미터 조합으로 표현된다:

| 모드 | `useLoading` | `minDuration` |
|---|---|---|
| 즉시 이동 | `false` | `0` |
| 로딩만 경유 | `true` | `0` |
| 최소 시간 보장 | `true` | `> 0` |

### Rules

- 로딩 Canvas는 언제나 풀스크린 raycast block을 가져야 한다 (전환 중 입력 차단).
- 취소(`CancellationToken`)가 발생해도 `_loadingCanvas`는 반드시 비활성 상태로 복원되어야 한다 — `try/finally`에서 보장.
- 진행률 UI가 필요해지면 `_loadingCanvas` 자식에 `LoadingView` 컴포넌트를 추가하고 `SceneHandler`가 참조해 업데이트한다 (새 Handler를 만들지 않는다).

---

## 3. Data Layer — ScriptableObject

- All static / designer-tunable data lives in `Assets/@Project/Scripts/Data` as `ScriptableObject` types.
- One `.cs` file per data **type**; `.asset` instances are created in the Editor (not by Claude).
- Prefer typed collection SOs (e.g., `EnemyTable`) over loose `List<T>` on the manager.
- Runtime mutation of SO instances is forbidden outside of dev tools — treat them as immutable.
- Use `/new-so-data` to scaffold a new type.

---

## 4. UI — `PopupBase` + `Handlers.UI`

All modal/popup UI flows through two points: the `PopupBase` abstract class and the `UIHandler` accessed via `Handlers.UI`. Source files:
- `Assets/@Library/Script/UI/PopupBase.cs`
- `Assets/@Library/Script/UI/IPopup.cs`
- `Assets/@Library/Script/Handler/UIHandler.cs`

### Contract

- Every popup inherits `PopupBase` (`MonoBehaviour`) and implements `HandleEvent(eEventType)`.
- A popup is opened with `Handlers.UI.Show<TPopup>()` — the handler resolves the Addressable (key == class name, or explicit override), parents it, wires show/hide callbacks, and pushes it onto the popup stack if `IsOnStack == true`.
- ESC auto-closes the top `IsOnStack` popup. Sort order is managed by `UIHandler._layerIndex` — never write to `Canvas.sortingOrder` directly. Popup sortingOrder는 `1 ~ N` 범위를 사용하며, `0`은 `SceneHandler`의 `LoadingCanvas`를 위해 예약되어 있다 (§2.5 참조).
- `PopupBase.Show()` auto-subscribes the popup to `Handlers.Event`; `Hide()` unsubscribes. Popup code does not call `Subscribe`/`Unsubscribe` itself.
- For data passing, add a `Configure(...)` method on the subclass and call it **before** `Show()`.
- If the popup prefab has a single `MMF_Player` anywhere in its hierarchy, `PopupBase` auto-wires it to the Show/Hide lifecycle: plays on `Show()`, stops on `Hide()`. Popups are **not** registered with `Handlers.Feel` — their lifecycle is fully owned by Show/Hide, so global Pause/Stop broadcasts do not apply to popup animations. Popups that need multiple players manage them directly (see §10).

### Deep dive

See the `handler-ui-popup` skill (`.claude/skills/handler-ui-popup/SKILL.md`) for the full API, hard don'ts, and a new-popup checklist. That skill is the authoritative reference for popup work.

---

## 5. Async & Reactive

- **Async**: `UniTask` is the default for new async work. Coroutines are not banned outright — `TimeHandler` uses them deliberately for server-time ticking — but every new async operation goes through `UniTask` unless a Unity API forces otherwise.
- **Reactive**: prefer `R3`-style Observables for event streams (input, state changes).
  - R3 is not yet in `Packages/manifest.json`. Adding it requires user approval (see CLAUDE.md §⚠).

### Lifecycle rules for `UniTask` (mandatory)

Leaked async work causes the worst class of bugs in this project — UI that keeps ticking after a popup is closed, state mutations after a scene has unloaded. Every new `UniTask` must satisfy all of the following:

1. **Takes a `CancellationToken`** as a parameter (or creates one scoped to a known owner).
2. **Token is tied to the owner's lifecycle:**
   - `MonoBehaviour`: `this.GetCancellationTokenOnDestroy()`.
   - `PopupBase`: create a `CancellationTokenSource` in `Show()`, cancel + dispose in `Hide()`.
   - Scene work: scope to the owning `BaseScene`, cancel in `OnSceneDestroy`.
3. **No `async void`** in project code (Unity lifecycle methods on `MonoBehaviour` are the only exception — and even there, prefer wrapping with `.Forget()` or a dedicated entry point).
4. **Fire-and-forget (`.Forget()`) is allowed only when:**
   - The task is bounded (not a loop), OR
   - The task body catches its own exceptions, AND
   - The caller accepts that the result is thrown away.
5. **Inside the task**, re-check `CancellationToken` after every `await` that spans frames; do not mutate MonoBehaviour state after cancellation.

The `/review-mobile` skill checks these points on every changeset.

---

## 6. Addressables

- All gameplay assets (prefabs, sprites, audio, SOs for content) load via Addressables.
- Label conventions: `stage/*`, `ui/*`, `audio/sfx/*`, `audio/bgm/*` (refine as needed).
- Release handles explicitly; prefer `AsyncOperationHandle` disposal helpers in a central loader.

---

## 7. Object Pooling

Frequently spawned gameplay objects (bullets, effects, enemies, damage popups) use a pooled lifecycle
to avoid GC spikes and `Instantiate` hitches. Source files:

- `Assets/@Library/Script/Handler/PoolHandler.cs`
- `Assets/@Library/Script/ObjectPool/ObjectPoolBase.cs`
- `Assets/@Library/Script/ObjectPool/PoolMonoBehaviour.cs`

`PoolHandler` is a Handler (see §2) — accessed via `Handlers.Pool`, attached as a child GameObject
of the `Handlers` root. It is not a standalone singleton.

### Contract

- Every pooled type inherits `PoolMonoBehaviour<T>` (CRTP) and declares a virtual
  `protected internal abstract string AddressFormat { get; }`. The format is a composite Addressable
  key with `{0}` substituted by the int id — e.g. `"bullet_{0}"` → id `5` loads `bullet_5`.
- Prefab lookup goes through `Handlers.Resource.GetPrefab(address)`. **No `GameObject` or
  `AssetReferenceGameObject` serialized fields** in gameplay code; everything routes through
  `Handlers.Resource`.
- `Handlers.Pool.Get<T>(int id)` returns a live instance. `PoolHandler`:
  1. Resolves the pool for `T`, creating it if needed.
  2. On pool miss, loads the prefab via `Handlers.Resource.GetPrefab(ZString.Format(AddressFormat, id))`
     and instantiates one. (First-miss cost is sync; use `Prewarm` for hot assets.)
  3. Sets `poolObjectId = id` and `SourcePool = <this pool>` on the instance, then calls `OnGet()`.
- Each pooled instance carries a back-reference (`internal ObjectPoolBase<T> SourcePool`). Callers
  return instances via `obj.Release()` only — **no `Handlers.Pool.Release<T>(int, T)` overload**.
  This eliminates the risk of mismatched `(id, obj)` arguments.

### Rules

- **No string literal Addressable keys** for pooled prefabs in gameplay code. The only place the
  format lives is the derived type's `AddressFormat` override.
- **`AddressFormat` must contain `{0}`.** The pool validates this on first `Get`; misconfiguration
  raises `InvalidOperationException` immediately rather than silently collapsing all ids to one prefab.
- **Do not cache `ObjectPoolBase<T>` or `PoolHandler` references.** Call `Handlers.Pool.GetObject<T>()`
  at the use site (same discipline as other Handlers).
- **Designer-facing id stays int.** The meaning of a given id is documented on the pool type, not
  as a global enum. Collisions across types are impossible because pools are keyed by `Type`.
- **Addressable asset keys must match the format.** Data entries for `Bullet` with `AddressFormat =
  "bullet_{0}"` must register Addressables named `bullet_1`, `bullet_2`, …

### Minimal example

```csharp
public class Bullet : PoolMonoBehaviour<Bullet>
{
    protected internal override string AddressFormat => "bullet_{0}";

    public override void OnGet()
    {
        base.OnGet();
        // reset state for reuse
    }
}

// Somewhere in gameplay:
var b = Handlers.Pool.Get<Bullet>(5);   // loads "bullet_5" on first miss
b.transform.position = firePoint;

// Return:
b.Release();   // no id, no manager reference
```

### When NOT to pool

- One-shot UI (popups) — owned by `UIHandler`, not pooled here.
- Rare singletons (e.g., boss prefab spawned once per run) — direct Instantiate is fine.
- Assets with heavy per-frame state reset cost that cancels out the pooling win.

### Risks & guardrails

- **First-Get sync hitch**: `Handlers.Resource.GetPrefab` uses `Addressables.LoadAssetAsync(...).WaitForCompletion()`
  on cache miss. Use `Handlers.Pool.Prewarm<T>(id, count)` during scene load for hot assets.
- **Scene lifecycle**: `PoolHandler` follows the `Handlers` lifetime. Active pool instances persist
  across scene changes by design; release them manually where needed via `Handlers.Pool.ReleaseAll<T>()`.
- **Misuse: caller still passes id to `Release`**: `Handlers.Pool.Release<T>(int, T)` is intentionally
  not exposed. `obj.Release()` is the only sanctioned path.

### Workflow

1. New pooled type — inherit `PoolMonoBehaviour<T>`, override `AddressFormat`, override
   `OnGet`/`OnRelease` as needed.
2. Register the matching Addressable keys for each `(type, id)` pair.
3. Optionally `Handlers.Pool.Prewarm<T>(id, count)` at scene load for hot assets.

---

## 8. Folder Conventions

```
Assets/
├── @Project/                 # Gameplay (this team owns it)
│   ├── Scene/                # Unity scenes
│   └── Scripts/
│       ├── Common/           # Cross-feature utilities, extensions
│       ├── Data/             # ScriptableObject types + generated asset folders
│       ├── Define/           # enums, const, static defines
│       ├── Game/             # Runtime gameplay code
│       └── UI/               # Gameplay popups (inherit PopupBase)
├── @Library/                 # In-house reusable code (shader, util, script)
├── @ThirdParty/              # External vendor drops
├── Plugins/                  # Native / binary plugins
└── NuGet/                    # NuGetForUnity packages
```

Keep domain code in `@Project`. Anything reusable across games graduates to `@Library`.

---

## 9. Performance Guardrails (Mobile)

- **GC**: zero-alloc on Update-level hot paths. Use `ZString`, `ZLinq`, object pooling.
- **Draw calls**: batch-aware UI (SpriteAtlas, shared materials). Watch Canvas rebuilds.
- **Textures**: ASTC (mobile), mipmaps only where needed, max size per category.
- **Scripts**: profile before optimizing; avoid `GetComponent` / `Find` in Update.
- Use `/review-mobile` to audit a changeset.

---

## 10. FeelHandler — Feel Integration

MoreMountains **Feel** (`MMF_Player` + feedbacks) is the project's one-shot animation / juice
system for popups, buttons, and in-game effects. All Feel usage routes through `Handlers.Feel`.
Source files (to be created):

- `Assets/@Library/Script/Handler/FeelHandler.cs`
- `Assets/@Project/Scripts/Data/Feel/FeelPresetTable.cs` (ScriptableObject)

`FeelHandler` is a Handler (§2) — child GameObject of `Handlers`, accessed via `Handlers.Feel`.
It is not a standalone singleton. The Feel asset lives under `Assets/@ThirdParty/Feel/`.

### Dependencies (user-approved Editor steps)

- Feel asset import into `Assets/@ThirdParty/Feel/` (paid asset; CLAUDE.md §⛔ — user imports).
- `Packages/manifest.json`: DOTween (Feel's runtime dep) — CLAUDE.md §⚠, user approval.
- Child GameObject `FeelHandler` under the `Handlers` prefab (Editor step).

### Architecture: Hybrid (UI + Pooled)

The handler is intentionally thin. Two paths:

1. **UI path (self-owned `MMF_Player`)** — popups, buttons, screen accents:
   - The prefab carries its own `MMF_Player` component, authored by designers.
   - For popups: `PopupBase` auto-wires a single `GetComponentInChildren<MMF_Player>(includeInactive: true)`
     — plays on `Show()`, stops on `Hide()`. Popups are **not** registered with `Handlers.Feel`;
     their lifecycle is fully owned by Show/Hide and they do not participate in global
     Pause/Stop broadcasts. Popups that need multiple players manage them manually.
   - For buttons or other UI that outlives a single animation: call `player.PlayFeedbacks()`
     directly and `Handlers.Feel.Register(player)` / `Unregister(player)` in `OnEnable` /
     `OnDisable` to opt into global control (Pause/Stop/TimeScale).
2. **Pooled path (`Handlers.Feel.Play(key, target)`)** — high-frequency in-game FX
   (hit flash, crit burst, damage pop):
   - `FeelHandler` owns an internal pool: `Dictionary<MMF_Player /*source prefab*/, Queue<MMF_Player>>`.
     It does **not** go through `Handlers.Pool` (key shape mismatch: Feel presets are
     string-keyed, `PoolHandler` is int-keyed).
   - On first call for a key: look up prefab in `FeelPresetTable`, `Instantiate`, subscribe
     once to `MMF_Player.Events.OnComplete` to auto-release.
   - On subsequent calls: dequeue, reparent to `target`, `PlayFeedbacks()`.

### Preset catalog — `FeelPresetTable` (Inspector-assigned, not Addressables)

Presets are declared in a `FeelPresetTable` ScriptableObject:

```csharp
[CreateAssetMenu(menuName = "Data/Feel/FeelPresetTable")]
public class FeelPresetTable : ScriptableObject
{
    [Serializable] public struct Entry
    {
        public string     key;      // e.g. "fx/hit_crit"
        public MMF_Player prefab;   // direct reference, assigned in Inspector
    }
    public Entry[] entries;
}
```

- `FeelHandler` holds `[SerializeField] FeelPresetTable _presets;` and caches a
  `Dictionary<string, MMF_Player>` on `Initialize`.
- Keys are namespaced: `fx/*`, `ui/*`, `popup/*`.
- **Architectural exception (scoped)**: §6 ("all gameplay assets load via Addressables") and
  §7's "no `GameObject` serialized fields in gameplay code" rule do **not** apply to
  `FeelPresetTable` / `FeelHandler`. This exception is limited to the Feel preset catalog
  as system-layer designer-tunable data. Gameplay code must still route through
  `Handlers.Resource` and `Handlers.Pool`.

### Contract

- `Handlers.Feel.Play(string key, Transform target = null, Vector3? worldPos = null)` —
  pooled path. Non-null `target` parents to the transform; `worldPos` overrides position
  for world-space FX.
- `Handlers.Feel.Prewarm(string key, int count)` — pre-instantiate pooled instances.
- `Handlers.Feel.PauseAll()` / `ResumeAll()` / `StopAll()` — broadcasts to every
  registered `MMF_Player`: explicitly-registered UI players (buttons etc.) + all pooled
  instances. Popups are excluded by design (see §4).
- `Handlers.Feel.SetTimeScale(float scale)` — routes through Feel's global feedback speed
  (`MMF_Player.FeedbacksIntensity` / equivalent), **not** `UnityEngine.Time.timeScale`.
- `Handlers.Feel.Register(MMF_Player)` / `Unregister(MMF_Player)` — UI-path opt-in.

### Lifecycle rules (mandatory)

1. **Scene unload**: `SceneHandler.ChangeSceneAsync` calls `Handlers.Feel.StopAll()` before
   the `SceneManager.LoadSceneAsync` await. This prevents cross-scene coroutine leaks.
2. **Popup `Hide()`**: for an auto-wired player, `PopupBase` calls `player.StopFeedbacks()`
   before unsubscribing and disabling. No `Unregister` — popups are never registered.
3. **Pooled auto-release**: each pooled instance subscribes once to `MMF_Player.Events.OnComplete`
   and returns to its queue. **Infinite-loop feedbacks must not be used through the pooled
   path** — they will never complete and will leak pool entries. Use the UI path for loops.
4. **No `async void`** in project code. Any awaitable wrapper (`PlayAsync`) follows §5
   UniTask rules (CancellationToken scoped to the caller's owner).
5. **Do not cache `FeelHandler` or pooled `MMF_Player` references** beyond a single play —
   the pooled instance is returned to the pool on completion.

### Mobile guardrails (extends §9)

- **Pooled path is mandatory** for FX that can fire ≥10 times per second (per-hit damage
  pops, muzzle flashes). `/review-mobile` flags direct `Instantiate` of `MMF_Player`
  in gameplay code.
- **UI one-shot path** is permitted for popup/button accents at human cadence (< 5 Hz).
- Prefer non-allocating feedbacks (Position, Scale, Rotation, Color). Avoid string-formatting
  feedbacks on hot paths.
- Route Feel sound feedbacks through `Handlers.Sound` for consistent volume/mixer control
  — avoid Feel's built-in `AudioSource` Play where possible.

### When NOT to use FeelHandler

- Long-running procedural animation (locomotion, camera follow) — dedicated systems or Timeline.
- Simple one-liner tweens with no designer-facing knobs — a direct DOTween call from
  `Assets/@Library` is fine.

### Workflow

1. Author an `MMF_Player` prefab (e.g. under `Assets/@Project/Prefabs/Feel/`).
2. Add `{key, prefab}` entry to a `FeelPresetTable.asset` (or a dedicated per-feature table)
   in the Inspector.
3. **UI path**: drop the prefab under a popup root; `PopupBase` auto-wires it. For buttons,
   call `player.PlayFeedbacks()` + `Register` / `Unregister` in `OnEnable` / `OnDisable`.
4. **Pooled path**: `Handlers.Feel.Play("fx/hit_crit", target)` from gameplay. Optionally
   `Prewarm` hot presets at scene load.
5. Run `/review-mobile` to verify the pooled-vs-UI-path choice.

---

## 11. How to evolve this document

1. Run `/arch-update` with the proposed change.
2. Draft a patch to this file.
3. Get user confirmation.
4. Implement only after the doc change lands.

Never let code drift ahead of this document.
