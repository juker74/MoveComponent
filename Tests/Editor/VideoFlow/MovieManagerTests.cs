using System;
using System.Collections.Generic;
using System.Linq;
using GameFrameX.Movie.Runtime;
using GameFrameX.Runtime;
using GameFrameX.NodeComposer.Video;
using UnityEngine;
using NUnit.Framework;
using GameFrameX.NodeComposer;

namespace Game.VideoFlow.Tests
{
    public class MovieManagerTests
    {
        const string Catalog = "{\"schemaVersion\":1,\"startChapterId\":\"a\",\"chapters\":[{\"chapterId\":\"a\",\"name\":\"Alpha\",\"graphKey\":\"chapters/a.json\",\"entryNodeId\":\"v\"},{\"chapterId\":\"b\",\"name\":\"Beta\",\"graphKey\":\"chapters/b.json\",\"entryNodeId\":\"v\"}]}";
        const string GraphA = "{\"schemaVersion\":1,\"id\":\"graph-a\",\"name\":\"Alpha\",\"nodes\":[{\"id\":\"v\",\"typeId\":\"media.video\",\"title\":\"Video\",\"x\":0,\"y\":0,\"properties\":{\"source\":\"alpha.mp4\"}}],\"edges\":[]}";
        const string GraphB = "{\"schemaVersion\":1,\"id\":\"graph-b\",\"name\":\"Beta\",\"nodes\":[{\"id\":\"v\",\"typeId\":\"media.video\",\"title\":\"Video\",\"x\":0,\"y\":0,\"properties\":{\"source\":\"beta.mp4\"}}],\"edges\":[]}";

        [Test]
        public void LoadingChapterBWhileARunsKeepsAExecutionAndBothCachedChapters()
        {
            var playback = new RecordingPlayback();
            IMovieManager movie = Manager(playback);
            try
            {
                Assert.That(movie.LoadCatalog(Catalog).Success, Is.True);
                Assert.That(movie.LoadChapter("a", GraphA).Success, Is.True);
                Assert.That(movie.PlayChapter("a").Success, Is.True);
                var first = playback.Sessions.Single();
                var activation = movie.CurrentVideo.ActivationId;
                Assert.That(movie.LoadChapter("b", GraphB).Success, Is.True);
                Assert.That(movie.CurrentChapterId, Is.EqualTo("a"));
                Assert.That(movie.Status, Is.EqualTo(FlowStatus.Running));
                Assert.That(movie.LoadedChapterIds, Is.EqualTo(new[] { "a", "b" }));
                Assert.That(movie.CurrentVideo.ActivationId, Is.EqualTo(activation));
                Assert.That(playback.Sources, Is.EqualTo(new[] { "alpha.mp4" }));
                Assert.That(first.Disposed, Is.False);
            }
            finally { movie.UnloadCatalog(); }
        }

        [Test]
        public void TypedManagerPreloadDoesNotReplaceExecutionAndSnapshotsStayImmutable()
        {
            var playback = new RecordingPlayback(); var movie = Manager(playback);
            try
            {
                Assert.That(movie.LoadCatalog(Catalog).Success, Is.True);
                var chapters = movie.Chapters;
                Assert.That(chapters.Select(c => c.ChapterId), Is.EqualTo(new[] { "a", "b" }));
                Assert.That(chapters[0].Name, Is.EqualTo("Alpha"));
                Assert.That(chapters[0].GraphKey, Is.EqualTo("chapters/a.json"));
                Assert.That(chapters[0].EntryNodeId, Is.EqualTo("v"));
                Assert.That(movie.StartChapterId, Is.EqualTo("a"));
                Assert.That(movie.LoadChapter("a", GraphA).Success, Is.True);
                var loaded = movie.LoadedChapterIds;
                Assert.That(movie.PlayChapter("a").Success, Is.True);
                var activation = movie.CurrentVideo.ActivationId;
                Assert.That(movie.LoadChapter("b", GraphB).Success, Is.True);
                Assert.That(movie.CurrentChapterId, Is.EqualTo("a"));
                Assert.That(movie.Status, Is.EqualTo(FlowStatus.Running));
                Assert.That(movie.CurrentVideo.ActivationId, Is.EqualTo(activation));
                Assert.That(loaded, Is.EqualTo(new[] { "a" }));
                Assert.That(movie.LoadedChapterIds, Is.EqualTo(new[] { "a", "b" }));
                Assert.Throws<NotSupportedException>(() => ((IList<string>)movie.LoadedChapterIds).Add("c"));
                Assert.Throws<NotSupportedException>(() => ((IList<MovieChapterInfo>)chapters).Clear());
                Assert.That(playback.Sessions.Single().Disposed, Is.False);
            }
            finally { movie.UnloadCatalog(); }
        }

        [TestCase("catalog")]
        [TestCase("graph")]
        [TestCase("missing-entry")]
        [TestCase("data-entry")]
        [TestCase("uncached")]
        [TestCase("wrong-case")]
        [TestCase("malformed-id")]
        public void InvalidTargetPreservesRunningChapterAndCache(string failure)
        {
            var playback = new RecordingPlayback(); var movie = PlayingA(playback);
            try
            {
                var activation = movie.CurrentVideo.ActivationId;
                GraphResult<bool> result;
                switch (failure)
                {
                    case "catalog": result = movie.LoadCatalog(Catalog.Replace("\"Alpha\"", "null")); break;
                    case "graph": result = movie.LoadChapter("b", "{}"); break;
                    case "missing-entry": result = movie.LoadChapter("b", GraphB.Replace("\"id\":\"v\"", "\"id\":\"other\"")); break;
                    case "data-entry": result = movie.LoadChapter("b", GraphB.Replace("media.video", "common.number").Replace("\"source\":\"beta.mp4\"", "\"value\":3")); break;
                    case "wrong-case": result = movie.PlayChapter("A"); break;
                    case "malformed-id": result = movie.LoadChapter("../b", GraphB); break;
                    default: result = movie.PlayChapter("b"); break;
                }
                Assert.That(result.Success, Is.False);
                Assert.That(movie.CurrentChapterId, Is.EqualTo("a"));
                Assert.That(movie.CurrentVideo.ActivationId, Is.EqualTo(activation));
                Assert.That(movie.Status, Is.EqualTo(FlowStatus.Running));
                Assert.That(movie.LoadedChapterIds, Is.EqualTo(new[] { "a" }));
                Assert.That(playback.Sessions.Single().Disposed, Is.False);
            }
            finally { movie.UnloadCatalog(); }
        }

        [Test]
        public void CacheReplacementIsAtomicAndDuplicateGraphIdentityIsRejected()
        {
            var playback = new RecordingPlayback(); var movie = PlayingA(playback);
            try
            {
                Assert.That(movie.LoadChapter("a", GraphB).Success, Is.False);
                Assert.That(movie.LoadChapter("b", GraphA).Success, Is.False);
                Assert.That(movie.LoadChapter("b", GraphB).Success, Is.True);
                Assert.That(movie.LoadChapter("b", "{}").Success, Is.False);
                Assert.That(movie.LoadChapter("b", GraphB.Replace("beta.mp4", "replacement.mp4")).Success, Is.True);
                Assert.That(movie.PlayChapter("b").Success, Is.True);
                Assert.That(playback.Sources, Is.EqualTo(new[] { "alpha.mp4", "replacement.mp4" }));
                Assert.That(playback.Trace.Take(4), Is.EqualTo(new[] { "open:alpha.mp4", "stop:alpha.mp4", "dispose:alpha.mp4", "open:replacement.mp4" }));
            }
            finally { movie.UnloadCatalog(); }
        }

        [Test]
        public void SwitchingChapterClosesUiAndVideoBeforeOpeningTheNextVideo()
        {
            var playback = new RecordingPlayback(); var ui = new RecordingUi(playback.Trace); var movie = Manager(playback, ui);
            try
            {
                movie.LoadCatalog(Catalog); Assert.That(movie.LoadChapter("a", UiGraphA).Success, Is.True);
                movie.LoadChapter("b", GraphB); movie.PlayChapter("a"); Update(movie); Update(movie);
                Assert.That(ui.Sessions.Count, Is.EqualTo(1));
                Assert.That(playback.Sessions[0].Paused, Is.True);
                movie.PlayChapter("b");
                Assert.That(ui.Sessions[0].Disposed, Is.True);
                Assert.That(playback.Sessions[0].Disposed, Is.True);
                Assert.That(playback.Sessions[0].ResumeCount, Is.Zero);
                Assert.That(movie.CurrentChapterId, Is.EqualTo("b"));
                Assert.That(playback.Trace.IndexOf("dispose-ui"), Is.LessThan(playback.Trace.IndexOf("open:beta.mp4")));
                Assert.That(playback.Trace.IndexOf("dispose:alpha.mp4"), Is.LessThan(playback.Trace.IndexOf("open:beta.mp4")));
            }
            finally { movie.UnloadCatalog(); }
        }

        [Test]
        public void ReplayingSameChapterRestoresDefaultsAndCreatesFreshMonotonicActivations()
        {
            var playback = new RecordingPlayback(); var movie = Manager(playback);
            try
            {
                movie.LoadCatalog(Catalog); Assert.That(movie.LoadChapter("a", ValueGraphA).Success, Is.True);
                movie.PlayChapter("a"); var activation = movie.CurrentVideo.ActivationId;
                Assert.That(movie.Values["count"], Is.EqualTo(2f));
                Update(movie); Update(movie);
                Assert.That(movie.Values["count"], Is.EqualTo(1f));
                movie.PlayChapter("a");
                Assert.That(movie.Values["count"], Is.EqualTo(2f));
                Assert.That(movie.CurrentVideo.ActivationId, Is.GreaterThan(activation));
                Assert.That(playback.Sessions[0].Disposed, Is.True);
                Assert.That(playback.Sessions.Count, Is.EqualTo(2));
                movie.UnloadCatalog(); movie.LoadCatalog(Catalog); movie.LoadChapter("a", GraphA); movie.PlayChapter("a");
                Assert.That(movie.CurrentVideo.ActivationId, Is.GreaterThan(activation + 1));
                Assert.That(movie.Values, Is.Empty);
            }
            finally { movie.UnloadCatalog(); }
        }

        [Test]
        public void UnloadingInactiveChapterLeavesCurrentPlaybackAndUnloadingCurrentClearsOnlyIt()
        {
            var playback = new RecordingPlayback(); var ui = new RecordingUi(); var movie = Manager(playback, ui);
            try
            {
                movie.LoadCatalog(Catalog); movie.LoadChapter("a", GraphA); movie.LoadChapter("b", UiGraphB); movie.PlayChapter("b");
                Update(movie); Update(movie); var activation = movie.CurrentVideo.ActivationId;
                movie.UnloadChapter("a"); movie.UnloadChapter("unknown"); movie.UnloadChapter("a");
                Assert.That(movie.Status, Is.EqualTo(FlowStatus.Running));
                Assert.That(movie.CurrentVideo.ActivationId, Is.EqualTo(activation));
                Assert.That(playback.Sessions[0].Disposed, Is.False);
                movie.LoadChapter("a", GraphA); movie.UnloadChapter("b");
                Assert.That(movie.LoadedChapterIds, Is.EqualTo(new[] { "a" }));
                Assert.That(movie.Chapters.Count, Is.EqualTo(2));
                Assert.That(movie.CurrentChapterId, Is.Null);
                Assert.That(movie.CurrentVideo, Is.Null);
                Assert.That(movie.CurrentNodeId, Is.Null);
                Assert.That(movie.Values, Is.Empty);
                Assert.That(movie.ActiveNodes, Is.Empty);
                Assert.That(movie.Status, Is.EqualTo(FlowStatus.Empty));
                Assert.That(movie.Phase, Is.EqualTo(FlowPhase.Idle));
                Assert.That(playback.Sessions[0].Disposed, Is.True);
                Assert.That(ui.Sessions.Single().Disposed, Is.True);
            }
            finally { movie.UnloadCatalog(); }
        }

        [Test]
        public void StopRetainsSelectionWhileAllUnloadRetainsCatalogAndCatalogUnloadRemovesMetadata()
        {
            var playback = new RecordingPlayback(); var movie = PlayingA(playback);
            try
            {
                movie.LoadChapter("b", GraphB); var first = movie.CurrentVideo.ActivationId;
                movie.Stop(); movie.Stop();
                Assert.That(movie.Status, Is.EqualTo(FlowStatus.Stopped));
                Assert.That(movie.CurrentChapterId, Is.EqualTo("a"));
                Assert.That(movie.LoadedChapterIds, Is.EqualTo(new[] { "a", "b" }));
                movie.PlayChapter("a"); Assert.That(movie.CurrentVideo.ActivationId, Is.GreaterThan(first));
                movie.UnloadAllChapters(); movie.UnloadAllChapters();
                Assert.That(movie.Status, Is.EqualTo(FlowStatus.Empty));
                Assert.That(movie.LoadedChapterIds, Is.Empty);
                Assert.That(movie.Chapters.Select(c => c.ChapterId), Is.EqualTo(new[] { "a", "b" }));
                Assert.That(movie.StartChapterId, Is.EqualTo("a"));
                movie.UnloadCatalog(); movie.UnloadCatalog();
                Assert.That(movie.Chapters, Is.Empty); Assert.That(movie.StartChapterId, Is.Null);
                Assert.That(movie.LoadChapter("a", GraphA).Success, Is.False);
            }
            finally { movie.UnloadCatalog(); }
        }

        [Test]
        public void ReplacingValidCatalogClosesCurrentRunAndDropsOldCachesWithoutResettingActivations()
        {
            var playback = new RecordingPlayback(); var movie = PlayingA(playback);
            try
            {
                var id = movie.CurrentVideo.ActivationId;
                Assert.That(movie.LoadCatalog(Catalog.Replace("Alpha", "New Alpha")).Success, Is.True);
                Assert.That(movie.Chapters[0].Name, Is.EqualTo("New Alpha"));
                Assert.That(movie.LoadedChapterIds, Is.Empty); Assert.That(movie.CurrentChapterId, Is.Null);
                Assert.That(playback.Sessions[0].Disposed, Is.True);
                movie.LoadChapter("a", GraphA); movie.PlayChapter("a");
                Assert.That(movie.CurrentVideo.ActivationId, Is.GreaterThan(id));
            }
            finally { movie.UnloadCatalog(); }
        }

        [TestCase("Stop")]
        [TestCase("UnloadChapter")]
        [TestCase("UnloadAllChapters")]
        [TestCase("UnloadCatalog")]
        public void CleanupRequestedFromEnteringObserverPreventsBehaviorOpeningAndIsNotDiscarded(string cleanup)
        {
            var playback = new RecordingPlayback(); var movie = Manager(playback); var events = new List<FlowEvent>();
            try
            {
                movie.LoadCatalog(Catalog); movie.LoadChapter("a", GraphA); movie.LoadChapter("b", GraphB);
                movie.EventRaised += e =>
                {
                    events.Add(e);
                    if (e.Kind != FlowEventKind.NodeEntered) return;
                    if (cleanup == "Stop") movie.Stop();
                    else if (cleanup == "UnloadChapter") movie.UnloadChapter("a");
                    else if (cleanup == "UnloadAllChapters") movie.UnloadAllChapters();
                    else movie.UnloadCatalog();
                };
                movie.PlayChapter("a");
                Assert.That(playback.Sessions, Is.Empty); Assert.That(movie.ActiveNodes, Is.Empty);
                Assert.That(events.First(e => e.Kind == FlowEventKind.NodeEntered).ChapterId, Is.EqualTo("a"));
                Assert.That(events.First(e => e.Kind == FlowEventKind.FlowStopped).ChapterId, Is.EqualTo("a"));
                Assert.That(movie.Status, Is.EqualTo(cleanup == "Stop" ? FlowStatus.Stopped : FlowStatus.Empty));
                if (cleanup == "UnloadChapter") Assert.That(movie.LoadedChapterIds, Is.EqualTo(new[] { "b" }));
                if (cleanup == "UnloadAllChapters") Assert.That(movie.Chapters.Count, Is.EqualTo(2));
                if (cleanup == "UnloadCatalog") Assert.That(movie.Chapters, Is.Empty);
            }
            finally { movie.UnloadCatalog(); }
        }

        [Test]
        public void UnloadFromExitObserverStopsRemainingRouteAndReentrantLoadsReturnFailures()
        {
            var playback = new RecordingPlayback(); var movie = Manager(playback); var results = new List<bool>();
            try
            {
                movie.LoadCatalog(Catalog); movie.LoadChapter("a", RoutedGraphA); movie.PlayChapter("a");
                movie.EventRaised += e =>
                {
                    if (e.Kind != FlowEventKind.NodeExited) return;
                    results.Add(movie.LoadCatalog(Catalog).Success);
                    results.Add(movie.LoadChapter("b", GraphB).Success);
                    results.Add(movie.PlayChapter("a").Success);
                    movie.UnloadChapter("a");
                };
                playback.Sessions[0].Status = VideoPlaybackStatus.Completed; Update(movie); Update(movie);
                Assert.That(results, Is.EqualTo(new[] { false, false, false }));
                Assert.That(playback.Sources, Is.EqualTo(new[] { "alpha.mp4" }));
                Assert.That(movie.Status, Is.EqualTo(FlowStatus.Empty));
                Assert.That(movie.CurrentChapterId, Is.Null); Assert.That(movie.LoadedChapterIds, Is.Empty);
            }
            finally { movie.UnloadCatalog(); }
        }

        [Test]
        public void ValidationCallbacksCannotReenterAndThrownLoaderOrFactoryPreservesTheRun()
        {
            var playback = new RecordingPlayback(); var loader = new CallbackLoader();
            var factory = new CallbackFactory(new VideoFlowBehaviorFactory(playback, new RecordingUi()));
            var movie = new MovieManager(); movie.SetDependencies(loader, factory);
            try
            {
                movie.LoadCatalog(Catalog); movie.LoadChapter("a", GraphA); movie.PlayChapter("a");
                var activation = movie.CurrentVideo.ActivationId;
                loader.Callback = () => Assert.That(movie.PlayChapter("a").Success, Is.False);
                Assert.That(movie.LoadChapter("b", GraphB).Success, Is.True);
                loader.Throw = true;
                Assert.That(movie.LoadChapter("b", GraphB).Success, Is.False);
                loader.Throw = false; factory.Throw = true;
                Assert.That(movie.LoadChapter("b", GraphB).Success, Is.False);
                Assert.That(movie.CurrentVideo.ActivationId, Is.EqualTo(activation));
                Assert.That(movie.Status, Is.EqualTo(FlowStatus.Running));
                Assert.That(playback.Sessions[0].Disposed, Is.False);
            }
            finally { movie.UnloadCatalog(); }
        }

        [Test]
        public void CustomRegistryExecutableEntryIsSupportedButOutputOnlyEntryIsRejected()
        {
            var registry = NodeDefinitionRegistry.CreateDefault();
            registry.Register(new NodeDefinition("custom.logic", "Custom", "Test", new[] { new PortDefinition("enter", "Enter", PortDirection.Input, PortKind.Control) }));
            registry.Register(new NodeDefinition("custom.output", "Output", "Test", new PortDefinition[0]));
            var movie = new MovieManager(); movie.SetDependencies(new VideoFlowLoader(registry), new ProbeFactory());
            try
            {
                movie.LoadCatalog(Catalog);
                var custom = GraphA.Replace("media.video", "custom.logic").Replace("\"source\":\"alpha.mp4\"", "");
                Assert.That(movie.LoadChapter("a", custom).Success, Is.True);
                Assert.That(movie.PlayChapter("a").Success, Is.True);
                Assert.That(movie.CurrentNodeId, Is.EqualTo("v"));
                Assert.That(movie.LoadChapter("b", custom.Replace("custom.logic", "custom.output").Replace("graph-a", "graph-b")).Success, Is.False);
                Assert.That(movie.CurrentChapterId, Is.EqualTo("a"));
            }
            finally { movie.UnloadCatalog(); }
        }

        [Test]
        public void OldUiCallbackCannotCompleteReplayedOrReloadedChapter()
        {
            var owner = new GameObject("MovieManager stale UI"); var playback = new RecordingPlayback();
            var ui = owner.AddComponent<FlowUiService>(); var requests = new List<FlowUiRequest>(); ui.Opened += requests.Add;
            var movie = Manager(playback, ui);
            try
            {
                movie.LoadCatalog(Catalog); movie.LoadChapter("a", UiGraphA); movie.PlayChapter("a"); Update(movie); Update(movie);
                var old = requests.Single(); Func<bool> oldCallback = () => ui.Complete(old.ActivationId);
                movie.UnloadCatalog(); movie.LoadCatalog(Catalog); movie.LoadChapter("a", UiGraphA); movie.PlayChapter("a"); Update(movie); Update(movie);
                Assert.That(requests[1].ActivationId, Is.GreaterThan(old.ActivationId));
                Assert.That(oldCallback(), Is.False);
                Assert.That(movie.CurrentVideo.UiActivationIds.Count, Is.EqualTo(1));
                Assert.That(ui.Complete(requests[1].ActivationId), Is.True);
                Update(movie); Update(movie);
                Assert.That(movie.CurrentChapterId, Is.EqualTo("a")); Assert.That(movie.CurrentVideo.UiActivationIds, Is.Empty);
            }
            finally { movie.UnloadCatalog(); UnityEngine.Object.DestroyImmediate(owner); }
        }

        [Test]
        public void CleanupFailureDuringChapterSwitchFaultsAndDoesNotOpenTargetChapter()
        {
            var playback = new RecordingPlayback(); var movie = PlayingA(playback);
            try
            {
                movie.LoadChapter("b", GraphB); playback.Sessions[0].ThrowOnDispose = true;
                Assert.That(movie.PlayChapter("b").Success, Is.False);
                Assert.That(movie.Status, Is.EqualTo(FlowStatus.Faulted));
                Assert.That(movie.CurrentChapterId, Is.EqualTo("a"));
                Assert.That(movie.ActiveNodes, Is.Empty);
                Assert.That(playback.Sources, Is.EqualTo(new[] { "alpha.mp4" }));
            }
            finally { movie.UnloadCatalog(); }
        }

        [Test]
        public void UnloadingTargetChapterFromOldExitObserverPreventsTargetOpening()
        {
            var playback = new RecordingPlayback(); var movie = PlayingA(playback);
            try
            {
                movie.LoadChapter("b", GraphB);
                movie.EventRaised += e => { if (e.Kind == FlowEventKind.NodeExited && e.ChapterId == "a") movie.UnloadChapter("b"); };
                movie.PlayChapter("b");
                Assert.That(playback.Sources, Is.EqualTo(new[] { "alpha.mp4" }));
                Assert.That(movie.LoadedChapterIds, Is.EqualTo(new[] { "a" }));
                Assert.That(movie.Status, Is.EqualTo(FlowStatus.Stopped));
                Assert.That(movie.CurrentChapterId, Is.EqualTo("a"));
                Assert.That(movie.ActiveNodes, Is.Empty);
            }
            finally { movie.UnloadCatalog(); }
        }

        [Test]
        public void FrameworkUpdateAdvancesAndFrameworkShutdownReleasesExactlyOnce()
        {
            GameFrameworkEntry.RegisterModule(typeof(IMovieManager), typeof(MovieManager));
            var movie = GameFrameworkEntry.GetModule<IMovieManager>(); var playback = new RecordingPlayback();
            movie.SetDependencies(new VideoFlowLoader(), new VideoFlowBehaviorFactory(playback, new RecordingUi()));
            try
            {
                movie.LoadCatalog(Catalog); movie.LoadChapter("a", GraphA); movie.PlayChapter("a");
                playback.Sessions[0].Status = VideoPlaybackStatus.Completed;
                GameFrameworkEntry.Update(.1f, .2f); Assert.That(movie.Status, Is.EqualTo(FlowStatus.Running));
                GameFrameworkEntry.Update(.1f, .2f); Assert.That(movie.Status, Is.EqualTo(FlowStatus.Completed));
                Assert.That(playback.Sessions[0].DisposeCount, Is.EqualTo(1));
                movie.PlayChapter("a"); GameFrameworkEntry.Shutdown(); GameFrameworkEntry.Shutdown();
                Assert.That(playback.Sessions[1].DisposeCount, Is.EqualTo(1));
                Assert.That(movie.Chapters, Is.Empty); Assert.That(movie.LoadedChapterIds, Is.Empty);
                Assert.That(movie.Status, Is.EqualTo(FlowStatus.Empty));
                Assert.That(movie.LoadCatalog(Catalog).Success, Is.False);
            }
            finally { GameFrameworkEntry.Shutdown(); }
        }

        static readonly string UiGraphA = "{\"schemaVersion\":1,\"id\":\"graph-a\",\"name\":\"Alpha\",\"nodes\":[{\"id\":\"v\",\"typeId\":\"media.video\",\"title\":\"Video\",\"x\":0,\"y\":0,\"properties\":{\"source\":\"alpha.mp4\",\"frames\":[0]}},{\"id\":\"u\",\"typeId\":\"media.ui\",\"title\":\"Choice\",\"x\":0,\"y\":0,\"properties\":{\"uiType\":\"$UI$\",\"pauseVideo\":true}}],\"edges\":[{\"id\":\"open-ui\",\"sourceNodeId\":\"v\",\"sourcePortId\":\"frame:0\",\"targetNodeId\":\"u\",\"targetPortId\":\"enter\",\"order\":0}]}".Replace("$UI$", typeof(TestFlowUi).FullName + ", " + typeof(TestFlowUi).Assembly.GetName().Name);
        static readonly string UiGraphB = UiGraphA.Replace("graph-a", "graph-b").Replace("alpha.mp4", "beta.mp4");
        const string RoutedGraphA = "{\"schemaVersion\":1,\"id\":\"graph-a\",\"name\":\"Alpha\",\"nodes\":[{\"id\":\"v\",\"typeId\":\"media.video\",\"title\":\"Video\",\"x\":0,\"y\":0,\"properties\":{\"source\":\"alpha.mp4\"}},{\"id\":\"next\",\"typeId\":\"media.video\",\"title\":\"Next\",\"x\":0,\"y\":0,\"properties\":{\"source\":\"next.mp4\"}}],\"edges\":[{\"id\":\"next-video\",\"sourceNodeId\":\"v\",\"sourcePortId\":\"completed\",\"targetNodeId\":\"next\",\"targetPortId\":\"enter\",\"order\":0}]}";
        const string ValueGraphA = "{\"schemaVersion\":1,\"id\":\"graph-a\",\"name\":\"Alpha\",\"nodes\":[{\"id\":\"v\",\"typeId\":\"media.video\",\"title\":\"Video\",\"x\":0,\"y\":0,\"properties\":{\"source\":\"alpha.mp4\",\"frames\":[0]}},{\"id\":\"count\",\"typeId\":\"common.number\",\"title\":\"Count\",\"x\":0,\"y\":0,\"properties\":{\"value\":2}},{\"id\":\"one\",\"typeId\":\"common.number\",\"title\":\"One\",\"x\":0,\"y\":0,\"properties\":{\"value\":1}},{\"id\":\"dec\",\"typeId\":\"flow.modify.float32\",\"title\":\"Subtract\",\"x\":0,\"y\":0,\"properties\":{\"target\":\"count\",\"operation\":\"subtract\"}}],\"edges\":[{\"id\":\"modify\",\"sourceNodeId\":\"v\",\"sourcePortId\":\"frame:0\",\"targetNodeId\":\"dec\",\"targetPortId\":\"enter\",\"order\":0},{\"id\":\"operand\",\"sourceNodeId\":\"one\",\"sourcePortId\":\"valueOut\",\"targetNodeId\":\"dec\",\"targetPortId\":\"operand\",\"order\":0}]}";

        static MovieManager Manager(RecordingPlayback playback, IFlowUiService ui = null)
        {
            var movie = new MovieManager();
            movie.SetDependencies(new VideoFlowLoader(), new VideoFlowBehaviorFactory(playback, ui ?? new RecordingUi()));
            return movie;
        }
        static MovieManager PlayingA(RecordingPlayback playback)
        {
            var movie = Manager(playback); Assert.That(movie.LoadCatalog(Catalog).Success, Is.True);
            Assert.That(movie.LoadChapter("a", GraphA).Success, Is.True); Assert.That(movie.PlayChapter("a").Success, Is.True); return movie;
        }
        static void Update(MovieManager movie)
        {
            typeof(MovieManager).GetMethod("Update", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).Invoke(movie, new object[] { 0f, 0f });
        }
        sealed class CallbackLoader : IVideoFlowLoader
        {
            public Action Callback; public bool Throw;
            public GraphResult<IVideoFlowGraph> Load(string json)
            {
                Callback?.Invoke(); if (Throw) throw new InvalidOperationException("loader failure"); return new VideoFlowLoader().Load(json);
            }
        }
        sealed class CallbackFactory : IFlowNodeBehaviorFactory
        {
            readonly IFlowNodeBehaviorFactory inner; public bool Throw;
            public CallbackFactory(IFlowNodeBehaviorFactory inner) { this.inner = inner; }
            public bool Supports(string type) { if (Throw) throw new InvalidOperationException("factory validation failure"); return inner.Supports(type); }
            public IFlowNodeBehavior Create(IFlowNode node) { return inner.Create(node); }
        }
        sealed class ProbeFactory : IFlowNodeBehaviorFactory
        {
            public bool Supports(string type) { return type == "custom.logic" || type == "custom.output"; }
            public IFlowNodeBehavior Create(IFlowNode node) { return new Probe(); }
            sealed class Probe : IFlowNodeBehavior
            {
                public void Enter(IFlowNodeContext context) { }
                public void Update(float deltaTime, float realDeltaTime) { }
                public void Exit() { }
                public void Dispose() { }
            }
        }

        sealed class RecordingPlayback : IVideoPlaybackService
        {
            public readonly List<RecordingSession> Sessions = new List<RecordingSession>();
            public readonly List<string> Sources = new List<string>();
            public readonly List<string> Trace = new List<string>();
            public IVideoPlaybackSession Open(VideoPlaybackRequest request)
            {
                Sources.Add(request.Source); Trace.Add("open:" + request.Source);
                var session = new RecordingSession(request.Source, Trace); Sessions.Add(session); return session;
            }
        }
        sealed class RecordingSession : IVideoPlaybackSession
        {
            readonly string source; readonly List<string> trace;
            public RecordingSession(string source, List<string> trace) { this.source = source; this.trace = trace; }
            public VideoPlaybackStatus Status { get; set; } = VideoPlaybackStatus.Playing;
            public string ErrorMessage => null;
            public long Frame { get; set; }
            public bool Disposed, Paused, ThrowOnDispose; public int DisposeCount, ResumeCount;
            public void Pause() { Paused = true; }
            public void Resume() { Paused = false; ResumeCount++; }
            public void Stop() { if (Status != VideoPlaybackStatus.Stopped) trace.Add("stop:" + source); Status = VideoPlaybackStatus.Stopped; }
            public void Dispose() { DisposeCount++; Disposed = true; trace.Add("dispose:" + source); Stop(); if (ThrowOnDispose) throw new InvalidOperationException("session disposal failure"); }
        }
        sealed class RecordingUi : IFlowUiService
        {
            readonly List<string> trace;
            public readonly List<UiSession> Sessions = new List<UiSession>();
            public RecordingUi(List<string> trace = null) { this.trace = trace ?? new List<string>(); }
            public IFlowUiSession Open(FlowUiRequest request) { var session = new UiSession(trace); Sessions.Add(session); return session; }
            public sealed class UiSession : IFlowUiSession
            {
                readonly List<string> trace; public bool Disposed;
                public UiSession(List<string> trace) { this.trace = trace; }
                public FlowUiStatus Status => FlowUiStatus.Open;
                public string SelectedBranchId => null;
                public string ErrorMessage => null;
                public void Dispose() { Disposed = true; trace.Add("dispose-ui"); }
            }
        }
    }
}
