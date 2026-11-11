# GF Movie module design

Approved scope: MovieComponent → IMovieManager → MovieManager, with multiple loaded chapter graphs and one executing chapter. The user approved this design on 2026-11-11. This document resolves implementation details of that approved behavior.

## Responsibilities and layout

- `Runtime/VideoFlow/Contracts/IMovieManager.cs`: `GameFrameX.Movie.Runtime.IMovieManager`, an immutable `MovieChapterInfo` descriptor, and the complete public module contract. Existing graph/behavior/player/UI interfaces and snapshots remain reusable.
- `Runtime/VideoFlow/Implementation/MovieManager.cs` and its state partial: `MovieManager : GameFrameworkModule, IMovieManager`, a public parameterless GF module. Move the existing execution implementation here, retaining its single state machine, activation sequence, immutable graphs, queue ordering, UI ownership and value rules.
- `Runtime/Integrations/GameFrameX/Movie/MovieComponent.cs`: `GameFrameX.Movie.Runtime.MovieComponent : GameFrameworkComponent`; standard GF registration and `GetModule<IMovieManager>()`, serialized Unity configuration, service assembly, API/event forwarding. No public Driver/manager getter and no component Update loop.
- Existing `VideoFlowDriver`, `VideoFlowHost` and optional FSM adapter remain documented compatibility entry points for existing projects. The old driver becomes a thin adapter to a non-registered MovieManager execution implementation, not a second state machine. This preserves old source/scene identities and all existing tests. Modern MovieComponent exclusively uses the GF-registered module.
- No new packages, chapter completion nodes, save system, cross-chapter values, unlock system or concurrent chapter execution.

## Public contract

All members are main-thread only. Expected input/state failures return existing `GraphResult<bool>`; void cleanup methods are idempotent. State/query returns are immutable snapshots. Use existing `FlowStatus`, `FlowPhase`, `FlowActivation`, `VideoExecutionSnapshot` and `FlowEvent` types.

```csharp
namespace GameFrameX.Movie.Runtime
{
    public interface IMovieManager
    {
        FlowStatus Status { get; }
        FlowPhase Phase { get; }
        string CurrentChapterId { get; }
        string CurrentNodeId { get; }
        string StartChapterId { get; }
        IReadOnlyList<MovieChapterInfo> Chapters { get; }
        IReadOnlyList<string> LoadedChapterIds { get; }
        VideoExecutionSnapshot CurrentVideo { get; }
        IReadOnlyList<FlowActivation> ActiveNodes { get; }
        IReadOnlyDictionary<string, object> Values { get; }
        event Action<FlowEvent> EventRaised;
        void SetDependencies(IVideoFlowLoader loader, IFlowNodeBehaviorFactory behaviorFactory);
        GraphResult<bool> LoadCatalog(string catalogJson);
        GraphResult<bool> LoadChapter(string chapterId, string graphJson);
        GraphResult<bool> PlayChapter(string chapterId);
        void Stop();
        void UnloadChapter(string chapterId);
        void UnloadAllChapters();
        void UnloadCatalog();
    }
}
```

`MovieChapterInfo` has readonly `ChapterId`, `Name`, `GraphKey`, and `EntryNodeId`; it never exposes mutable chapter DTOs. Chapter order is catalog order. `LoadedChapterIds` is an immutable snapshot in catalog order. IDs use exact identity; malformed/unknown chapter IDs are rejected, never guessed from graph IDs or filenames.

## Catalog, cache and execution

1. `LoadCatalog` strictly decodes the existing chapter catalog JSON. Validate fully before replacing the old catalog. Replacing a valid catalog stops current execution and clears old graph caches before installing the new immutable descriptors. Invalid input preserves everything. No IO, implicit graph loading or automatic first/next playback occurs.
2. `LoadChapter` requires an existing catalog entry. Decode/validate using injected loader and the existing behavior support rules, validate the entry as executable, and reject graph IDs duplicated across different cached chapters. Loading another chapter is permitted during playback and cannot change the current graph, values, state or sessions. Replacing the currently selected chapter cache is rejected until explicitly unloaded. Failure never replaces a cached graph.
3. `PlayChapter` requires a cached, valid chapter. Validate target and entry before clearing old execution. Re-entering the same chapter also closes the old UI/video and starts a fresh activation. Each playback initializes current values from that chapter's defaults; graph-local video loops keep current values. All cached configurations remain immutable. The activation sequence is module-wide and is never reset by Stop, unload, catalog replacement or chapter replay, so old callback IDs cannot collide.
4. `Stop` closes active video/UI/logic and queued transitions, retaining catalog and caches. It retains the selected `CurrentChapterId` and graph for stopped-state inspection; `PlayChapter` always starts a new run.
5. `UnloadChapter` removes only that cache and retains its catalog entry. If selected/current, clean up and release graph/value/request references, clear CurrentChapterId, set Empty/Idle; other cached chapters remain. Unknown/already unloaded IDs are no-ops.
6. `UnloadAllChapters` stops and releases all cached/current execution data, sets Empty/Idle, retains ordered catalog metadata and StartChapterId. `UnloadCatalog` additionally clears that metadata. Keep dependency services and event subscriptions until GF Shutdown.
7. Lifecycle callbacks and behaviors can re-enter the module. Return observable state errors for load/play calls during an in-progress operation. Stop and void unload requests issued from events must be deferred safely and stop affected execution before further routing; do not silently discard a void cleanup call. Cleanup completion must not reactivate disposed sessions. Preserve existing exception isolation and late-request rejection.
8. Add an optional `ChapterId` to existing FlowEvent without breaking old constructor calls; existing node/phase/completion events identify the originating chapter. No redundant manager state machine or mirrored status fields.

## GF and Unity boundaries

The installed GF requires the interface namespace to start with `GameFrameX.` and creates registered managers using a parameterless constructor and a `GameFrameworkModule` cast. Its BaseComponent is the sole manager Update/Shutdown owner. MovieManager overrides protected Update/Shutdown; IMovieManager does not expose Tick/Dispose.

MovieComponent uses standard ImplementationComponentType/InterfaceComponentType registration, base.Awake readiness checks and module lookup. It configures an existing UnityVideoPlaybackService and IFlowUiService through VideoFlowBehaviorFactory. Resources are provided explicitly: serializable catalog TextAsset, chapter-ID to graph TextAsset bindings, playback target, Clip bindings, optional Play On Start. GraphKey remains a host resource key; the manager never reads Assets or disk paths. Provide the complete IMovieManager operations and snapshot/event forwarding on the component, plus parameterless Play() to load configured catalog/bindings then play the catalog start chapter. Do not expose mutable manager internals.

Initialization must work with GF Awake ordering, including a base component being created later. Avoid consuming the last module reference after GF shutdown. Component disable stops current execution; destroy unsubscribes and unloads its configured module resources without directly shutting down the global framework. A second MovieComponent must not reconfigure a live module owned by another component. Keep cleanup/reentrant Play safe.

## Verification and delivery

Use the isolated Unity 2022.3.62f3 validation project. Establish a fresh 107-test baseline. Observe a real NUnit failure for missing multi-chapter behavior before implementation, then verify typed public behavior tests and all five existing test assemblies. Cover cache independence, invalid target preservation, cleanup scope, replay, values, stale UI callbacks, event chapter identity, deferred cleanup, real GF module update and Unity component lifecycle. Preserve plugin bytes, prior GUIDs and existing package behavior. Update usage/migration docs and publish a reviewed minor package release after local verification, then update the original GameFrameX.Unity Git package and validate actual import as in prior releases.
