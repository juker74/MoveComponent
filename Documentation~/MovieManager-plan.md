# Movie manager and chapter runtime implementation plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development to implement this plan task-by-task. The user has approved execution; do not ask for another design approval.

**Goal:** Provide a GF MovieComponent → IMovieManager → MovieManager module managing multiple chapter graphs and one current execution.

**Architecture:** Move the existing single execution state machine into a GF module, add catalog/cache ownership there, retain thin legacy adapters, then add a standard GF component for Unity and business integration.

**Tech Stack:** Unity 2022.3.62f3, existing GameFrameX 2.5.4, C#, current graph/catalog JSON codecs and NUnit EditMode.

**Spec:** MovieManager-design.md (same directory).

## Global constraints

- Approved scope: multiple cached chapter graphs, one current execution; no new dependencies or automatic chapter progression.
- Preserve all current activation ordering, UI ownership/pause leases, graph-loop values and late callback guards.
- Keep plugin files and existing script identities unchanged. Do not edit PackageCache or unrelated host-project changes.
- GF manager namespace `GameFrameX.Movie.Runtime`, public parameterless construction, `GameFrameworkModule` lifecycle; modern component never duplicates manager Update.
- Existing standalone driver/host/FSM remain compatible adapters. Only one execution implementation exists.
- The controller alone runs Unity and publishes Git changes. Implementers do not run concurrent Unity instances, commit, push, tag or spawn subagents.

## Task 1: GF manager contract, execution migration and multi-chapter cache

**Files:** Create Contracts/IMovieManager.cs and metadata; create Implementation/MovieManager.cs and state partial with metadata as needed; replace VideoFlowDriver implementation with a thin compatibility adapter while preserving its file/meta and public signatures; update VideoFlowStates partial; update FlowEvent, runtime/test assembly references; create Tests/Editor/VideoFlow/MovieManagerTests.cs and metadata. Paths are under Runtime/VideoFlow unless stated.

**Interfaces:** Produce exactly IMovieManager and immutable MovieChapterInfo as defined in the spec. Retain the existing `IVideoFlowDriver`, player/UI/behavior contracts and public compatibility constructor. New module is the only owner of execution state and the module-wide activation sequence.

- [ ] First write a runnable NUnit regression which uses reflection until the new type exists, configures real loader/factory with recording playback, loads catalog A/B, plays A, loads B during playback, and asserts A remains active while both chapter IDs are cached. If IMovieManager/MovieManager is absent, assert an explicit missing-feature failure rather than causing compilation failure. Signal RED_READY and wait for controller's confirmed NUnit red before production edits.
- [ ] Implement the contract and migrate the original Driver/States implementation into MovieManager. Preserve old Load/Start/Tick/Dispose through a legacy driver adapter using internal module methods; do not route a standalone adapter through GF global registration.
- [ ] Implement strict catalog loading and immutable metadata. Cache compiled graphs independently from the selected/executing graph. Validate loader results, behavior support and executable chapter entry before mutation. Target failures must retain current run/cache.
- [ ] Implement chapter playback, same-chapter replay, Stop and the three unload scopes according to the spec. Retain monotonic activation IDs and identify chapter in events. Preserve deferred event cleanup and existing queue stop priority. No data or session from one chapter may leak into another.
- [ ] Add meaningful typed tests with literal fixtures and independent expectations for: A running while B preloads; bad catalog/graph/entry or uncached Play preserves A; cache replacement/duplicate identity rejection; A→B cleanup ordering; same-chapter replay resets values and sessions; unloading inactive A leaves B running; unloading current B closes video/UI and retains A; all-unload retains catalog; catalog-unload empties all; observer-triggered unload; stale UI callback rejection; GF Update/Shutdown advancing/releasing once. Test helpers live in test files and do not add production-only test APIs.
- [ ] Report changed files, public behavior checks, exact red observation request and any unresolved concerns. Controller runs targeted tests followed by task review.

**Representative expected workflow:**

```csharp
IMovieManager movie = new MovieManager();
movie.SetDependencies(new VideoFlowLoader(), new VideoFlowBehaviorFactory(playback, ui));
Assert.That(movie.LoadCatalog(catalogJson).Success, Is.True);
Assert.That(movie.LoadChapter("a", graphA).Success, Is.True);
Assert.That(movie.PlayChapter("a").Success, Is.True);
Assert.That(movie.LoadChapter("b", graphB).Success, Is.True);
Assert.That(movie.CurrentChapterId, Is.EqualTo("a"));
Assert.That(movie.LoadedChapterIds, Is.EqualTo(new[] { "a", "b" }));
movie.UnloadChapter("a");
Assert.That(movie.Status, Is.EqualTo(FlowStatus.Empty));
Assert.That(movie.LoadedChapterIds, Is.EqualTo(new[] { "b" }));
```

## Task 2: standard GF MovieComponent and integration verification

**Files:** Create Runtime/Integrations/GameFrameX/Movie directory, metadata and MovieComponent.cs; put component into existing runtime assembly via an asmref if appropriate to avoid an assembly cycle, otherwise use a dedicated minimal assembly referencing manager/contracts/core GF. Update Unity integration test references and add MovieComponent tests. Do not change plugin or existing Host metadata.

**Interfaces:** Consume Task 1 IMovieManager unchanged. Component forwards every operation/query/event without exposing a Driver or concrete Manager. Register and retrieve the manager using real GF lifecycle. Serialized scene inputs are catalog, chapter graph bindings, UI service, video target, Clip bindings and Play On Start.

- [ ] Add failing integration tests for actual GF registration and component-owned dependency configuration, then configure serialized bindings and call Play() to start the catalog default chapter. Root test runner controls execution.
- [ ] Implement standard GF Awake registration, service setup at a lifecycle-safe point and complete API forwarding. Parameterless Play() validates configured catalog/bindings before replacing a current run; partial/bad configuration must report a clear failure. Allow explicit LoadChapter for late resource loading.
- [ ] Ensure module Update is the sole update source, and disabling/destroying the component closes its video/UI safely. Test component disable, destruction, GF shutdown order, reentrant Play during stop and duplicate component ownership. Preserve externally provided RenderTexture.
- [ ] Run complete five-assembly Unity regression, inspect actual test XML/log, then independent task/final review. Fix material findings and add only tests that protect them.

## Task 3: documentation, release and target-project integration

**Files:** README.md, Documentation~/VideoFlow.md, ChapterCatalog.md, Validation.md, dependency/validation records, installation examples, CHANGELOG.md and package.json; host project manifest/lock only for package revision update.

- [ ] Document the new business entry, exact unload scopes, catalog loading versus graph loading, resource binding, event chapter identity, GF update ownership and migration from old standalone entry. Existing APIs are compatibility paths, not an alternate implementation.
- [ ] Record precise local test evidence and package structural validation; preserve prior historical results. Version the reviewed compatible addition as 1.2.0.
- [ ] Integrate verified feature into /Users/user/MoveComponent, publish private Git v1.2.0 without changing old tags, verify remote Git UPM in the isolated project, and update current GameFrameX.Unity to that Git tag.
- [ ] Validate actual target package version, all assemblies/resources, old graph GUID and new component/module types; remove temporary validation helper. Record post-publication evidence in a docs-only follow-up and report entry points, test totals and limitations.
