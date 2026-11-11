using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using GameFrameX.Movie.Runtime;
using GameFrameX.NodeComposer;
using GameFrameX.Runtime;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.Video;
using Object = UnityEngine.Object;

namespace Game.VideoFlow.Tests
{
    public class MovieComponentTests
    {
        const string Catalog = "{\"schemaVersion\":1,\"startChapterId\":\"b\",\"chapters\":[{\"chapterId\":\"a\",\"name\":\"Alpha\",\"graphKey\":\"chapters/a.json\",\"entryNodeId\":\"v\"},{\"chapterId\":\"b\",\"name\":\"Beta\",\"graphKey\":\"chapters/b.json\",\"entryNodeId\":\"v\"}]}";
        const string GraphA = "{\"schemaVersion\":1,\"id\":\"graph-a\",\"name\":\"Alpha\",\"nodes\":[{\"id\":\"v\",\"typeId\":\"media.video\",\"title\":\"Video\",\"x\":0,\"y\":0,\"properties\":{\"source\":\"alpha\"}}],\"edges\":[]}";
        const string GraphB = "{\"schemaVersion\":1,\"id\":\"graph-b\",\"name\":\"Beta\",\"nodes\":[{\"id\":\"v\",\"typeId\":\"media.video\",\"title\":\"Video\",\"x\":0,\"y\":0,\"properties\":{\"source\":\"beta\"}}],\"edges\":[]}";
        const BindingFlags PrivateInstance = BindingFlags.Instance | BindingFlags.NonPublic;

        [SetUp] public void SetUp() { ResetFramework(); }
        [TearDown] public void TearDown() { ResetFramework(); }
        [UnityTearDown]
        public IEnumerator RestoreEditMode()
        {
            if (Application.isPlaying) yield return new ExitPlayMode();
            var bootstrap = GameObject.Find("Movie Test Manual Bootstrap");
            if (bootstrap != null) Object.DestroyImmediate(bootstrap);
        }

        [Test]
        public void PlayOnStartFailureRaisesDiagnosticWithCurrentContextWithoutLogging()
        {
            using (var scene = new Scene())
            {
                AssertSuccess(scene.Component.LoadCatalog(Catalog));
                AssertSuccess(scene.Component.LoadChapter("a", UiGraph));
                AssertSuccess(scene.Component.PlayChapter("a"));
                Set(scene.Component, "catalogJson", null);
                Set(scene.Component, "playOnStart", true);
                var diagnostics = new List<FlowEvent>();
                var logs = new List<string>();
                scene.Component.EventRaised += e => { if (e.Kind == FlowEventKind.Diagnostic) diagnostics.Add(e); };
                Application.LogCallback logged = (message, stack, kind) => logs.Add(message);
                bool ignored = LogAssert.ignoreFailingMessages;
                LogAssert.ignoreFailingMessages = true;
                Application.logMessageReceived += logged;
                try
                {
                    Invoke(scene.Component, "Start");
                    Assert.That(diagnostics.Count, Is.EqualTo(1));
                    Assert.That(diagnostics[0].ErrorCode, Is.EqualTo("Catalog"));
                    Assert.That(diagnostics[0].ErrorMessage, Is.Not.Empty);
                    Assert.That(diagnostics[0].Phase, Is.EqualTo(FlowPhase.Interacting));
                    Assert.That(diagnostics[0].ChapterId, Is.EqualTo("a"));
                    Assert.That(diagnostics[0].NodeId, Is.EqualTo("v"));
                    Assert.That(logs, Is.Empty);
                }
                finally
                {
                    Application.logMessageReceived -= logged;
                    LogAssert.ignoreFailingMessages = ignored;
                }
            }
        }

        [Test]
        public void ThrowingComponentSubscriberIsObservableAndHealthyListenersStillReceiveLifecycle()
        {
            using (var scene = new Scene())
            {
                AssertSuccess(scene.Component.LoadCatalog(Catalog));
                AssertSuccess(scene.Component.LoadChapter("a", UiGraph));
                var failure = new InvalidOperationException("component observer failed");
                var events = new List<FlowEvent>();
                scene.Component.EventRaised += e => { if (e.Kind == FlowEventKind.NodeEntered) throw failure; };
                scene.Component.EventRaised += events.Add;
                AssertSuccess(scene.Component.PlayChapter("a"));
                Assert.That(events.Count(e => e.Kind == FlowEventKind.NodeEntered), Is.EqualTo(1));
                var diagnostics = events.Where(e => e.Kind == FlowEventKind.Diagnostic).ToArray();
                Assert.That(diagnostics.Length, Is.EqualTo(1));
                Assert.That(diagnostics[0].ErrorCode, Is.EqualTo("ObserverException"));
                Assert.That(diagnostics[0].ErrorMessage, Does.Contain(failure.Message));
                Assert.That(diagnostics[0].Exception, Is.SameAs(failure));
                Assert.That(diagnostics[0].RelatedEventKind, Is.EqualTo(FlowEventKind.NodeEntered));
                Assert.That(diagnostics[0].GraphId, Is.EqualTo("ui-graph"));
                Assert.That(diagnostics[0].ChapterId, Is.EqualTo("a"));
                Assert.That(diagnostics[0].NodeId, Is.EqualTo("v"));
                Assert.That(diagnostics[0].ActivationId, Is.EqualTo(scene.Component.ActiveNodes.Single().ActivationId));
                Assert.That(scene.Component.Status, Is.EqualTo(FlowStatus.Running));
            }
        }

        [Test]
        public void RegisteredComponentConfiguredPlayStartsCatalogDefaultAndCachesBothGraphs()
        {
            using (var scene = new Scene())
            {
                Assert.That(GameEntry.GetComponent<MovieComponent>(), Is.SameAs(scene.Component));
                Assert.That(typeof(MovieComponent).GetCustomAttribute<GameFrameXManualStartupAttribute>(), Is.Not.Null);
                AssertSuccess(scene.Component.Play());
                var manager = GameFrameworkEntry.GetModule<IMovieManager>();
                Assert.That(manager.CurrentChapterId, Is.EqualTo("b"));
                Assert.That(manager.Status, Is.EqualTo(FlowStatus.Running));
                Assert.That(manager.LoadedChapterIds, Is.EqualTo(new[] { "a", "b" }));
                Assert.That(scene.Players.Length, Is.EqualTo(1));
                Assert.That(scene.Players[0].clip, Is.EqualTo(scene.Clip));
            }
        }

        [TestCase("catalog")]
        [TestCase("late-json")]
        [TestCase("unknown-id")]
        [TestCase("duplicate-id")]
        [TestCase("duplicate-graph")]
        [TestCase("entry")]
        [TestCase("missing-start")]
        [TestCase("missing-json")]
        [TestCase("ui")]
        [TestCase("clip")]
        public void InvalidSceneConfigurationPreservesCurrentSessionAndBothCaches(string failure)
        {
            using (var scene = new Scene())
            {
                AssertSuccess(scene.Component.Play());
                var player = scene.Players.Single(); var activation = scene.Component.CurrentVideo.ActivationId;
                switch (failure)
                {
                    case "catalog": Set(scene.Component, "catalogJson", scene.Asset("{}")); break;
                    case "late-json": scene.Graphs("a", GraphA, "b", "{}"); break;
                    case "unknown-id": scene.Graphs("a", GraphA, "unknown", GraphB); break;
                    case "duplicate-id": scene.Graphs("a", GraphA, "a", GraphB); break;
                    case "duplicate-graph": scene.Graphs("a", GraphA, "b", GraphB.Replace("graph-b", "graph-a")); break;
                    case "entry": scene.Graphs("a", GraphA, "b", GraphB.Replace("\"id\":\"v\"", "\"id\":\"other\"")); break;
                    case "missing-start": scene.Graphs("a", GraphA); break;
                    case "missing-json": Set(scene.Component, "chapterGraphs", new[] { new MovieComponent.ChapterGraphBinding { chapterId = "b" } }); break;
                    case "ui": Set(scene.Component, "uiService", null); break;
                    case "clip": Set(scene.Component, "clips", new[] { new MovieComponent.ClipBinding { sourceOrGuid = "alpha" } }); break;
                }
                var result = scene.Component.Play();
                Assert.That(result.Success, Is.False); Assert.That(result.Message, Is.Not.Empty);
                Assert.That(scene.Component.Status, Is.EqualTo(FlowStatus.Running));
                Assert.That(scene.Component.CurrentChapterId, Is.EqualTo("b"));
                Assert.That(scene.Component.CurrentVideo.ActivationId, Is.EqualTo(activation));
                Assert.That(scene.Component.LoadedChapterIds, Is.EqualTo(new[] { "a", "b" }));
                Assert.That(scene.Players.Single(), Is.SameAs(player));
            }
        }

        [Test]
        public void LateGraphLoadingUsesSceneServicesAndOnlyGfUpdateAdvancesExecution()
        {
            using (var scene = new Scene())
            {
                IMovieManager component = scene.Component;
                AssertSuccess(component.LoadCatalog(Catalog));
                AssertSuccess(component.LoadChapter("a", GraphA));
                AssertSuccess(component.PlayChapter("a"));
                var player = scene.Players.Single(); var activation = component.CurrentVideo.ActivationId;
                AssertSuccess(component.LoadChapter("b", GraphB));
                Assert.That(component.CurrentChapterId, Is.EqualTo("a"));
                Assert.That(component.CurrentVideo.ActivationId, Is.EqualTo(activation));
                Assert.That(scene.Players.Single(), Is.SameAs(player));
                Assert.That(component.LoadedChapterIds, Is.EqualTo(new[] { "a", "b" }));
                Assert.That(typeof(MovieComponent).GetMethod("Update", PrivateInstance), Is.Null);
                // A UI entry stays open: each GF update must emit exactly one node update.
                component.UnloadCatalog();
                AssertSuccess(component.LoadCatalog(Catalog));
                AssertSuccess(component.LoadChapter("b", UiGraph));
                AssertSuccess(component.PlayChapter("b"));
                int updates = 0;
                component.EventRaised += e => { if (e.Kind == FlowEventKind.NodeUpdated) updates++; };
                GameFrameworkEntry.Update(.1f, .2f);
                Assert.That(updates, Is.EqualTo(1));
                Assert.That(component.Status, Is.EqualTo(FlowStatus.Running));
                GameFrameworkEntry.Update(.1f, .2f);
                Assert.That(updates, Is.EqualTo(2));
            }
        }

        [UnityTest]
        public IEnumerator LaterBaseAwakeKeepsRegisteredManagerAndCurrentPlayback()
        {
            // A real scene Base selects manual startup and prevents the package auto host during entry.
            new GameObject("Movie Test Manual Bootstrap").AddComponent<BaseComponent>();
            yield return new EnterPlayMode();
            Object.DestroyImmediate(GameObject.Find("Movie Test Manual Bootstrap"));
            ResetFramework();
            using (var scene = new Scene())
            {
                AssertSuccess(scene.Component.Play());
                var manager = GameFrameworkEntry.GetModule<IMovieManager>();
                var player = scene.Players.Single(); var activation = scene.Component.CurrentVideo.ActivationId;
                var baseOwner = new GameObject("Late Base");
                try
                {
                    var component = baseOwner.AddComponent<BaseComponent>();
                    Assert.That(GameEntry.GetComponent<BaseComponent>(), Is.SameAs(component));
                    Assert.That(GameFrameworkEntry.GetModule<IMovieManager>(), Is.SameAs(manager));
                    Assert.That(scene.Component.CurrentVideo.ActivationId, Is.EqualTo(activation));
                    Assert.That(scene.Players.Single(), Is.SameAs(player));
                }
                finally { Object.DestroyImmediate(baseOwner); }
            }
            yield return new ExitPlayMode();
        }

        [TestCase(FlowEventKind.NodeEntered)]
        [TestCase(FlowEventKind.NodeUpdated)]
        public void UnloadingInactiveChapterFromCurrentChapterEventKeepsCurrentSession(FlowEventKind trigger)
        {
            using (var scene = new Scene())
            {
                AssertSuccess(scene.Component.LoadCatalog(Catalog));
                AssertSuccess(scene.Component.LoadChapter("a", UiGraph));
                AssertSuccess(scene.Component.LoadChapter("b", GraphB));
                FlowUiRequest opened = null; int closed = 0; bool requested = false;
                scene.Ui.Opened += request => opened = request;
                scene.Ui.Closed += id => closed++;
                scene.Component.EventRaised += e =>
                {
                    if (e.Kind != trigger || requested) return;
                    requested = true; scene.Component.UnloadChapter("b");
                };
                AssertSuccess(scene.Component.PlayChapter("a"));
                GameFrameworkEntry.Update(.1f, .2f);
                Assert.That(requested, Is.True);
                Assert.That(scene.Component.Status, Is.EqualTo(FlowStatus.Running));
                Assert.That(scene.Component.CurrentChapterId, Is.EqualTo("a"));
                Assert.That(scene.Component.LoadedChapterIds, Is.EqualTo(new[] { "a" }));
                Assert.That(opened, Is.Not.Null); Assert.That(closed, Is.Zero);
                Assert.That(scene.Ui.Complete(opened.ActivationId), Is.True);
            }
        }

        [TestCase("stop")]
        [TestCase("chapter")]
        [TestCase("all")]
        [TestCase("catalog")]
        public void CleanupFromReplayStopEventReachesManagerAndCancelsRemainingPlaySteps(string cleanup)
        {
            using (var scene = new Scene())
            {
                AssertSuccess(scene.Component.Play());
                bool requested = false;
                scene.Component.EventRaised += e =>
                {
                    if (e.Kind != FlowEventKind.FlowStopped || requested) return;
                    requested = true;
                    switch (cleanup)
                    {
                        case "chapter": scene.Component.UnloadChapter("b"); break;
                        case "all": scene.Component.UnloadAllChapters(); break;
                        case "catalog": scene.Component.UnloadCatalog(); break;
                        default: scene.Component.Stop(); break;
                    }
                };
                Assert.That(scene.Component.Play().Success, Is.False);
                Assert.That(requested, Is.True);
                Assert.That(scene.Players, Is.Empty);
                Assert.That(scene.Component.Status, Is.EqualTo(FlowStatus.Empty));
                Assert.That(scene.Component.LoadedChapterIds, Is.Empty);
                Assert.That(scene.Component.Chapters.Count, Is.EqualTo(cleanup == "catalog" ? 0 : 2));
            }
        }

        [TestCase(FlowEventKind.FlowStopped)]
        [TestCase(FlowEventKind.NodeExited)]
        public void ConfiguredPlayHonorsPendingTargetUnloadDuringOldChapterCleanup(FlowEventKind trigger)
        {
            using (var scene = new Scene())
            {
                AssertSuccess(scene.Component.Play());
                AssertSuccess(scene.Component.PlayChapter("a"));
                Assert.That(scene.Component.CurrentChapterId, Is.EqualTo("a"));
                bool requested = false;
                scene.Component.EventRaised += e =>
                {
                    if (e.Kind != trigger || requested) return;
                    requested = true;
                    scene.Component.UnloadChapter("b");
                };
                var result = scene.Component.Play();
                Assert.That(requested, Is.True);
                Assert.That(result.Success, Is.False);
                Assert.That(result.Issues[0].Code, Is.EqualTo("FlowState"));
                Assert.That(scene.Component.Status, Is.EqualTo(FlowStatus.Empty));
                Assert.That(scene.Component.CurrentChapterId, Is.Null);
                Assert.That(scene.Component.CurrentVideo, Is.Null);
                Assert.That(scene.Component.ActiveNodes, Is.Empty);
                Assert.That(scene.Component.LoadedChapterIds, Does.Not.Contain("b"));
                Assert.That(scene.Players, Is.Empty);
            }
        }

        [TestCase(false)]
        [TestCase(true)]
        public void DisableOrDestroyFromStopEventCannotRevivePlayback(bool destroy)
        {
            using (var scene = new Scene())
            {
                AssertSuccess(scene.Component.Play());
                var manager = GameFrameworkEntry.GetModule<IMovieManager>();
                bool requested = false;
                scene.Component.EventRaised += e =>
                {
                    if (e.Kind != FlowEventKind.FlowStopped || requested) return;
                    requested = true;
                    if (destroy) Object.DestroyImmediate(scene.Component);
                    else { scene.Component.enabled = false; Invoke(scene.Component, "OnDisable"); }
                };
                Assert.That(scene.Component.Play().Success, Is.False);
                Assert.That(requested, Is.True);
                Assert.That(manager.Status, Is.EqualTo(FlowStatus.Empty));
                Assert.That(scene.Players, Is.Empty);
            }
        }

        [Test]
        public void ReentrantPlayFailsObservablyAndThrowingObserverDoesNotBlockOtherObservers()
        {
            using (var scene = new Scene())
            {
                AssertSuccess(scene.Component.Play());
                GraphResult<bool> nested = null; int stopped = 0;
                scene.Component.EventRaised += e => { throw new InvalidOperationException("Observer failure"); };
                scene.Component.EventRaised += e =>
                {
                    if (e.Kind != FlowEventKind.FlowStopped) return;
                    stopped++; nested = scene.Component.Play();
                };
                AssertSuccess(scene.Component.Play());
                Assert.That(stopped, Is.EqualTo(1));
                Assert.That(nested, Is.Not.Null); Assert.That(nested.Success, Is.False);
                Assert.That(nested.Issues[0].Code, Is.EqualTo("FlowState"));
                Assert.That(scene.Component.Status, Is.EqualTo(FlowStatus.Running));
                Assert.That(scene.Players.Length, Is.EqualTo(1));
                scene.Component.Stop();
                Assert.That(scene.Players, Is.Empty);
            }
        }

        [Test]
        public void DisableClosesUiAndVideoAndDestroyPreservesExternalTexture()
        {
            using (var scene = new Scene())
            {
                var target = new RenderTexture(32, 18, 0);
                try
                {
                    Set(scene.Component, "videoTarget", target);
                    AssertSuccess(scene.Component.Play());
                    Assert.That(scene.Players.Single().targetTexture, Is.SameAs(target));
                    scene.Component.enabled = false; Invoke(scene.Component, "OnDisable");
                    Assert.That(scene.Players, Is.Empty);
                    Assert.That(scene.Component.Status, Is.EqualTo(FlowStatus.Stopped));
                    scene.Component.enabled = true;
                    scene.Component.UnloadCatalog();
                    AssertSuccess(scene.Component.LoadCatalog(Catalog));
                    AssertSuccess(scene.Component.LoadChapter("b", UiGraph));
                    FlowUiRequest opened = null; int closed = 0;
                    scene.Ui.Opened += request => opened = request;
                    scene.Ui.Closed += id => closed++;
                    AssertSuccess(scene.Component.PlayChapter("b"));
                    Assert.That(opened, Is.Not.Null);
                    scene.Component.enabled = false; Invoke(scene.Component, "OnDisable");
                    Assert.That(closed, Is.EqualTo(1));
                    Assert.That(scene.Ui.Complete(opened.ActivationId), Is.False);
                    Object.DestroyImmediate(scene.Component);
                    Assert.That(target != null, Is.True);
                }
                finally { Object.DestroyImmediate(target); }
            }
        }

        [TestCase(false)]
        [TestCase(true)]
        public void FrameworkShutdownAndComponentDestroyAreSafeInEitherOrder(bool shutdownFirst)
        {
            using (var scene = new Scene())
            {
                AssertSuccess(scene.Component.Play());
                var manager = GameFrameworkEntry.GetModule<IMovieManager>();
                if (shutdownFirst)
                {
                    GameFrameworkEntry.Shutdown();
                    Assert.That(scene.Component.Play().Success, Is.False);
                }
                Object.DestroyImmediate(scene.Component);
                if (!shutdownFirst) GameFrameworkEntry.Shutdown();
                GameFrameworkEntry.Shutdown();
                Assert.That(manager.Status, Is.EqualTo(FlowStatus.Empty));
                Assert.That(scene.Players, Is.Empty);
                var modules = typeof(GameFrameworkEntry).GetField("s_GameFrameworkModules", BindingFlags.Static | BindingFlags.NonPublic).GetValue(null);
                Assert.That((int)modules.GetType().GetProperty("Count").GetValue(modules), Is.Zero);
            }
        }

        [Test]
        public void DuplicateComponentCannotReconfigureOrCleanUpFirstOwner()
        {
            using (var scene = new Scene())
            {
                AssertSuccess(scene.Component.Play());
                var player = scene.Players.Single(); var activation = scene.Component.CurrentVideo.ActivationId;
                var other = new GameObject("Duplicate Movie");
                try
                {
                    var duplicate = other.AddComponent<MovieComponent>();
                    Invoke(duplicate, "Awake");
                    Assert.That(duplicate.enabled, Is.False);
                    Assert.That(duplicate.Play().Success, Is.False);
                    duplicate.Stop(); duplicate.UnloadCatalog();
                    Object.DestroyImmediate(duplicate);
                    Assert.That(GameEntry.GetComponent<MovieComponent>(), Is.SameAs(scene.Component));
                    Assert.That(scene.Component.CurrentVideo.ActivationId, Is.EqualTo(activation));
                    Assert.That(scene.Component.Status, Is.EqualTo(FlowStatus.Running));
                    Assert.That(scene.Players.Single(), Is.SameAs(player));
                }
                finally { Object.DestroyImmediate(other); }
            }
        }

        [Test]
        public void PlayOnStartUsesSerializedConfiguration()
        {
            using (var scene = new Scene())
            {
                Set(scene.Component, "playOnStart", true);
                Invoke(scene.Component, "Start");
                Assert.That(scene.Component.CurrentChapterId, Is.EqualTo("b"));
                Assert.That(scene.Component.LoadedChapterIds, Is.EqualTo(new[] { "a", "b" }));
                Assert.That(scene.Players.Length, Is.EqualTo(1));
            }
        }

        [Test]
        public void ExplicitDependenciesAndSnapshotQueriesForwardToRegisteredModule()
        {
            using (var scene = new Scene())
            {
                Set(scene.Component, "uiService", null);
                IMovieManager movie = scene.Component;
                movie.SetDependencies(new VideoFlowLoader(), new VideoFlowBehaviorFactory(new UnityVideoPlaybackService(scene.Owner.transform), scene.Ui));
                AssertSuccess(movie.LoadCatalog(Catalog));
                var graph = UiGraph.Replace("}],\"edges\"", "},{\"id\":\"score\",\"typeId\":\"common.number\",\"title\":\"Score\",\"x\":0,\"y\":0,\"properties\":{\"value\":7}}],\"edges\"");
                AssertSuccess(movie.LoadChapter("a", graph));
                FlowEvent entered = null;
                movie.EventRaised += e => { if (e.Kind == FlowEventKind.NodeEntered) entered = e; };
                AssertSuccess(movie.PlayChapter("a"));
                Assert.That(movie.Phase, Is.EqualTo(FlowPhase.Interacting));
                Assert.That(movie.CurrentNodeId, Is.EqualTo("v"));
                Assert.That(movie.StartChapterId, Is.EqualTo("b"));
                Assert.That(movie.Chapters.Select(c => c.Name), Is.EqualTo(new[] { "Alpha", "Beta" }));
                Assert.That(movie.ActiveNodes.Select(n => n.NodeId), Is.EqualTo(new[] { "v" }));
                Assert.That(movie.Values["score"], Is.EqualTo(7d));
                Assert.That(movie.CurrentVideo, Is.Null);
                Assert.That(entered.ChapterId, Is.EqualTo("a"));
                movie.UnloadAllChapters();
                Assert.That(movie.Status, Is.EqualTo(FlowStatus.Empty));
                Assert.That(movie.ActiveNodes, Is.Empty); Assert.That(movie.Values, Is.Empty);
                Assert.That(movie.Chapters.Count, Is.EqualTo(2));
                Assert.That(movie.StartChapterId, Is.EqualTo("b"));
                movie.UnloadCatalog();
                Assert.That(movie.Chapters, Is.Empty); Assert.That(movie.StartChapterId, Is.Null);
            }
        }

        [Test]
        public void PlayFromModuleUpdateEventFailsWithoutUnloadingCurrentChapter()
        {
            using (var scene = new Scene())
            {
                AssertSuccess(scene.Component.LoadCatalog(Catalog));
                AssertSuccess(scene.Component.LoadChapter("a", UiGraph));
                AssertSuccess(scene.Component.LoadChapter("b", GraphB));
                FlowUiRequest opened = null; int closed = 0;
                scene.Ui.Opened += request => opened = request;
                scene.Ui.Closed += id => closed++;
                AssertSuccess(scene.Component.PlayChapter("a"));
                var activation = scene.Component.ActiveNodes.Single().ActivationId;
                GraphResult<bool> nested = null;
                scene.Component.EventRaised += e => { if (e.Kind == FlowEventKind.NodeUpdated) nested = scene.Component.Play(); };
                GameFrameworkEntry.Update(.1f, .2f);
                Assert.That(nested, Is.Not.Null); Assert.That(nested.Success, Is.False);
                Assert.That(scene.Component.Status, Is.EqualTo(FlowStatus.Running));
                Assert.That(scene.Component.CurrentChapterId, Is.EqualTo("a"));
                Assert.That(scene.Component.ActiveNodes.Single().ActivationId, Is.EqualTo(activation));
                Assert.That(scene.Component.LoadedChapterIds, Is.EqualTo(new[] { "a", "b" }));
                Assert.That(closed, Is.Zero);
                Assert.That(scene.Ui.Complete(opened.ActivationId), Is.True);
                Assert.That(nested.Issues[0].Code, Is.EqualTo("FlowState"));
            }
        }

        [Test]
        public void PlayFromUiOpenedDuringModuleRoutingFailsWithoutClosingNewUiSession()
        {
            using (var scene = new Scene())
            {
                AssertSuccess(scene.Component.LoadCatalog(Catalog));
                AssertSuccess(scene.Component.LoadChapter("a", UiChainGraph));
                AssertSuccess(scene.Component.LoadChapter("b", GraphB));
                var opened = new List<FlowUiRequest>(); int closed = 0; GraphResult<bool> nested = null;
                scene.Ui.Opened += request =>
                {
                    opened.Add(request);
                    if (opened.Count == 2) nested = scene.Component.Play();
                };
                scene.Ui.Closed += id => closed++;
                AssertSuccess(scene.Component.PlayChapter("a"));
                Assert.That(scene.Ui.Complete(opened[0].ActivationId), Is.True);
                GameFrameworkEntry.Update(.1f, .2f);
                GameFrameworkEntry.Update(.1f, .2f);
                Assert.That(opened.Count, Is.EqualTo(2));
                Assert.That(nested, Is.Not.Null); Assert.That(nested.Success, Is.False);
                Assert.That(scene.Component.Status, Is.EqualTo(FlowStatus.Running));
                Assert.That(scene.Component.CurrentNodeId, Is.EqualTo("next"));
                Assert.That(scene.Component.CurrentChapterId, Is.EqualTo("a"));
                Assert.That(scene.Component.LoadedChapterIds, Is.EqualTo(new[] { "a", "b" }));
                Assert.That(closed, Is.EqualTo(1));
                Assert.That(scene.Ui.Complete(opened[1].ActivationId), Is.True);
                Assert.That(nested.Issues[0].Code, Is.EqualTo("FlowState"));
            }
        }

        static readonly string UiGraph = "{\"schemaVersion\":1,\"id\":\"ui-graph\",\"name\":\"UI\",\"nodes\":[{\"id\":\"v\",\"typeId\":\"media.ui\",\"title\":\"Choice\",\"x\":0,\"y\":0,\"properties\":{\"uiType\":\"$UI$\",\"pauseVideo\":false}}],\"edges\":[]}"
            .Replace("$UI$", typeof(UnityTestMovieUI).FullName + ", " + typeof(UnityTestMovieUI).Assembly.GetName().Name);
        static readonly string UiChainGraph = "{\"schemaVersion\":1,\"id\":\"ui-chain\",\"name\":\"UI\",\"nodes\":[{\"id\":\"v\",\"typeId\":\"media.ui\",\"title\":\"First\",\"x\":0,\"y\":0,\"properties\":{\"uiType\":\"$UI$\",\"pauseVideo\":false}},{\"id\":\"next\",\"typeId\":\"media.ui\",\"title\":\"Next\",\"x\":0,\"y\":0,\"properties\":{\"uiType\":\"$UI$\",\"pauseVideo\":false}}],\"edges\":[{\"id\":\"continue\",\"sourceNodeId\":\"v\",\"sourcePortId\":\"completed\",\"targetNodeId\":\"next\",\"targetPortId\":\"enter\",\"order\":0}]}"
            .Replace("$UI$", typeof(UnityTestMovieUI).FullName + ", " + typeof(UnityTestMovieUI).Assembly.GetName().Name);

        static void AssertSuccess(GraphResult<bool> result) { Assert.That(result.Success, Is.True, result.Message); }
        static void Set(Component component, string field, object value)
        { component.GetType().GetField(field, PrivateInstance).SetValue(component, value); }
        static void Invoke(Component component, string method) { component.GetType().GetMethod(method, PrivateInstance).Invoke(component, null); }

        sealed class Scene : IDisposable
        {
            readonly List<TextAsset> assets = new List<TextAsset>();
            public readonly GameObject Owner;
            public readonly MovieComponent Component;
            public readonly FlowUiService Ui;
            public readonly VideoClip Clip;
            public VideoPlayer[] Players { get { return Owner.GetComponentsInChildren<VideoPlayer>(true); } }
            public Scene()
            {
                Owner = new GameObject("Movie Component Test");
                Ui = Owner.AddComponent<FlowUiService>();
                Component = Owner.AddComponent<MovieComponent>();
                Set(Component, "catalogJson", Asset(Catalog));
                Set(Component, "uiService", Ui);
                Clip = AssetDatabase.LoadAssetAtPath<VideoClip>("Packages/com.gameframex.unity.movecomponent/Tests/Editor/VideoNodesIntegration/Fixtures/preview.mp4");
                Assert.That(Clip, Is.Not.Null);
                Set(Component, "clips", new[] { new MovieComponent.ClipBinding { sourceOrGuid = "alpha", clip = Clip }, new MovieComponent.ClipBinding { sourceOrGuid = "beta", clip = Clip } });
                Graphs("a", GraphA, "b", GraphB);
                if (GameEntry.GetComponent<MovieComponent>() != Component) Invoke(Component, "Awake");
            }
            public TextAsset Asset(string json) { var asset = new TextAsset(json); assets.Add(asset); return asset; }
            public void Graphs(params string[] pairs)
            {
                var bindings = new List<MovieComponent.ChapterGraphBinding>();
                for (int i = 0; i < pairs.Length; i += 2)
                    bindings.Add(new MovieComponent.ChapterGraphBinding { chapterId = pairs[i], graphJson = Asset(pairs[i + 1]) });
                Set(Component, "chapterGraphs", bindings.ToArray());
            }
            public void Dispose()
            {
                Object.DestroyImmediate(Owner);
                foreach (var asset in assets) Object.DestroyImmediate(asset);
            }
        }

        static void ResetFramework()
        {
            GameFrameworkEntry.Shutdown();
            var registry = typeof(GameEntry).GetField("GameFrameworkComponents", BindingFlags.Static | BindingFlags.NonPublic).GetValue(null);
            registry.GetType().GetMethod("Clear").Invoke(registry, null);
        }
    }
}
