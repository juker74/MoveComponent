using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using GameFrameX.NodeComposer;
using GameFrameX.NodeComposer.Video;

namespace Game.VideoFlow.Tests
{
    public abstract class AbstractTestFlowUi : Game.Framework.UI.MovieUIForm { }
    public sealed class TestFlowUi : AbstractTestFlowUi { }
    public class GenericTestFlowUi<T> : Game.Framework.UI.MovieUIForm { }
    public class VideoFlowTests
    {
        static string Json(params NodeDocument[] nodes) { return Json(nodes, new EdgeDocument[0]); }
        static string Json(NodeDocument[] nodes, params EdgeDocument[] edges)
        {
            var registry = new NodeDefinitionRegistry(); new VideoNodeExtension().Register(registry);
            return new GraphJsonCodec(registry).Encode(new GraphDocument { Id="test", Nodes=nodes.ToList(), Edges=edges.ToList() }).Value;
        }
        static NodeDocument Video(string id="v", params int[] frames) { return new NodeDocument { Id=id, TypeId="media.video", Properties=new Dictionary<string,object> { {"source","movie.mp4"}, {"frames",frames.Select(x=>(object)x).ToList()} } }; }
        static NodeDocument Ui(string id="u", bool pause=true) { return new NodeDocument {Id=id,TypeId="media.ui",Properties=new Dictionary<string,object>{{"uiType",typeof(TestFlowUi).FullName+", "+typeof(TestFlowUi).Assembly.GetName().Name},{"pauseVideo",pause},{"parameters",new Dictionary<string,object>{{"items",new List<object>{"a"}}}}}}; }
        static EdgeDocument Edge(string a,string port,string b,int order=0) {return new EdgeDocument {Id=a+port+b,SourceNodeId=a,SourcePortId=port,TargetNodeId=b,TargetPortId="enter",Order=order};}

        static NodeDocument BranchUi(string id="u", bool pause=true)
        {
            var ui=Ui(id,pause);ui.Properties["branches"]=new List<object>{
                new Dictionary<string,object>{{"id","a"},{"name","成功"}},
                new Dictionary<string,object>{{"id","b"},{"name","失败"}}};return ui;
        }
        [Test] public void UiBranchCanOpenAnotherUiWithoutReplacingSourceVideo()
        {
            var playback=new Playback();var ui=new UiService();
            var driver=new VideoFlowDriver(new VideoFlowLoader(),new VideoFlowBehaviorFactory(playback,ui));
            Assert.That(driver.Load(Json(new[]{Video("v",10),BranchUi(),BranchUi("next",false)},
                Edge("v","frame:10","u"),Edge("u","branch:b","next"))).Success,Is.True);
            driver.Start("v");playback.Frame=10;driver.Tick(0,0);driver.Tick(0,0);
            ui.Sessions[0].SelectedBranchId="b";ui.Sessions[0].Status=FlowUiStatus.Completed;
            driver.Tick(0,0);driver.Tick(0,0);
            Assert.That(ui.Sessions.Count,Is.EqualTo(2));Assert.That(ui.Sessions[0].Disposed,Is.True);
            Assert.That(driver.CurrentVideo.NodeId,Is.EqualTo("v"));Assert.That(playback.Paused,Is.False);
            Assert.That(ui.Requests[1].Branches.Select(b=>b.Id),Is.EqualTo(new[]{"a","b"}));driver.Stop();
        }
        [Test] public void CustomUiServiceReturningUnknownBranchFaultsRatherThanTakingAnotherRoute()
        {
            var ui=new UiService();var driver=new VideoFlowDriver(new VideoFlowLoader(),new VideoFlowBehaviorFactory(new Playback(),ui));
            Assert.That(driver.Load(Json(BranchUi())).Success,Is.True);driver.Start("u");
            ui.Sessions[0].SelectedBranchId="missing";ui.Sessions[0].Status=FlowUiStatus.Completed;
            driver.Tick(0,0);driver.Tick(0,0);
            Assert.That(driver.Status,Is.EqualTo(FlowStatus.Faulted));Assert.That(ui.Sessions[0].Disposed,Is.True);
        }
        [Test] public void UiSelectedBranchSwitchesOnlyToItsVideo()
        {
            var playback=new Playback();var ui=new UiService();
            var driver=new VideoFlowDriver(new VideoFlowLoader(),new VideoFlowBehaviorFactory(playback,ui));
            var loaded=driver.Load(Json(new[]{Video("v",10),BranchUi(),Video("win"),Video("lose")},
                Edge("v","frame:10","u"),Edge("u","branch:a","win"),Edge("u","branch:b","lose")));
            Assert.That(loaded.Success,Is.True,loaded.Message);
            driver.Start("v");playback.Frame=10;driver.Tick(0,0);driver.Tick(0,0);
            Assert.That(ui.Requests.Single().Branches.Select(b=>b.Id),Is.EqualTo(new[]{"a","b"}));
            ui.Sessions[0].SelectedBranchId="b";ui.Sessions[0].Status=FlowUiStatus.Completed;
            driver.Tick(0,0);driver.Tick(0,0);
            Assert.That(driver.CurrentVideo.NodeId,Is.EqualTo("lose"));
            Assert.That(ui.Sessions[0].Disposed,Is.True);driver.Stop();
        }
        [Test] public void UnconnectedUiBranchEndsInteractionAndResumesSourceVideo()
        {
            var playback=new Playback();var ui=new UiService();
            var driver=new VideoFlowDriver(new VideoFlowLoader(),new VideoFlowBehaviorFactory(playback,ui));
            Assert.That(driver.Load(Json(new[]{Video("v",10),BranchUi()},Edge("v","frame:10","u"))).Success,Is.True);
            driver.Start("v");playback.Frame=10;driver.Tick(0,0);driver.Tick(0,0);
            Assert.That(playback.Paused,Is.True);
            ui.Sessions[0].SelectedBranchId="a";ui.Sessions[0].Status=FlowUiStatus.Completed;
            driver.Tick(0,0);driver.Tick(0,0);
            Assert.That(driver.CurrentVideo.NodeId,Is.EqualTo("v"));Assert.That(playback.Paused,Is.False);
            Assert.That(ui.Sessions[0].Disposed,Is.True);driver.Stop();
        }
        [Test] public void LoaderRejectsMissingAbstractGenericAndUnrelatedUiTypes()
        {
            foreach(var typeId in new[]{"", "Missing.Choice, MissingAssembly", typeof(string).AssemblyQualifiedName,
                typeof(AbstractTestFlowUi).FullName+", "+typeof(AbstractTestFlowUi).Assembly.GetName().Name,
                typeof(GenericTestFlowUi<>).FullName+", "+typeof(GenericTestFlowUi<>).Assembly.GetName().Name,
                typeof(GenericTestFlowUi<int>).FullName+", "+typeof(GenericTestFlowUi<int>).Assembly.GetName().Name})
            {
                var ui=Ui();ui.Properties["uiType"]=typeId;
                var result=new VideoFlowLoader().Load(Json(ui));
                Assert.That(result.Success,Is.False,typeId);
                Assert.That(result.Issues.Any(i=>i.Code=="UiType"),Is.True,result.Message);
            }
        }
        [Test] public void SelectedUiClassIsPassedToServiceAfterJsonRoundTrip()
        {
            var ui=new UiService();var driver=new VideoFlowDriver(new VideoFlowLoader(),new VideoFlowBehaviorFactory(new Playback(),ui));
            var result=driver.Load(Json(Ui()));Assert.That(result.Success,Is.True,result.Message);
            driver.Start("u");
            Assert.That(ui.Requests.Single().UiType,Is.EqualTo(typeof(TestFlowUi)));
            driver.Stop();
        }
        [Test] public void LoaderPreservesCyclesAndFreezesNestedParameters()
        {
            var result=new VideoFlowLoader().Load(Json(new[]{Video("a"),Video("b"),Ui()}, Edge("a","completed","b"),Edge("b","completed","a")));
            Assert.That(result.Success,Is.True,result.Message);
            Assert.That(result.Value.GetOutgoing("b","completed")[0].TargetNodeId,Is.EqualTo("a"));
            var parameters=(IDictionary<string,object>)result.Value.Nodes["u"].Properties["parameters"];
            Assert.Throws<NotSupportedException>(()=>parameters["new"]=true);
            Assert.Throws<NotSupportedException>(()=>((IList<object>)parameters["items"]).Add("b"));
        }
        [Test] public void LoaderRejectsFrameToVideoAndEmptySource()
        {
            Assert.That(new VideoFlowLoader().Load(Json(new[]{Video("a",10),Video("b")},Edge("a","frame:10","b"))).Success,Is.False);
            var v=Video();v.Properties["source"]=" ";
            Assert.That(new VideoFlowLoader().Load(Json(v)).Success,Is.False);
        }
        [Test] public void CompletionIsDeferredAndOldContextCannotCompleteNextActivation()
        {
            var factory=new RecordingFactory();var driver=new VideoFlowDriver(new VideoFlowLoader(),factory);
            Assert.That(driver.Load(Json(new[]{Video("a"),Video("b")},Edge("a","completed","b"))).Success,Is.True);
            driver.Start("a"); var old=factory.Contexts[0];
            Assert.That(old.Complete("completed"),Is.True);
            Assert.That(driver.CurrentNodeId,Is.EqualTo("a"));
            driver.Tick(0.1f,0.1f);
            Assert.That(driver.CurrentNodeId,Is.EqualTo("b"));
            Assert.That(old.Complete("completed"),Is.False);
            Assert.That(factory.Trace.Take(3),Is.EqualTo(new[]{"enter:a","exit:a","dispose:a"}));
            driver.Dispose();
        }
        [Test] public void FrameCrossingOpensMultipleUiAndPauseReleasedOnlyAfterLastUi()
        {
            var video=new Playback();var ui=new UiService();var driver=Driver(video,ui);
            driver.Load(Json(new[]{Video("v",10,20),Ui("one"),Ui("two")},Edge("v","frame:10","one"),Edge("v","frame:10","two",1)));
            driver.Start("v");video.Frame=15;driver.Tick(.1f,.1f);driver.Tick(.1f,.1f);
            Assert.That(driver.ActiveNodes.Count,Is.EqualTo(3));Assert.That(video.Paused,Is.True);
            ui.Sessions[0].Status=FlowUiStatus.Completed;driver.Tick(.1f,.1f);driver.Tick(.1f,.1f);
            Assert.That(video.Paused,Is.True);
            ui.Sessions[1].Status=FlowUiStatus.Completed;driver.Tick(.1f,.1f);driver.Tick(.1f,.1f);
            Assert.That(video.Paused,Is.False);Assert.That(driver.CurrentNodeId,Is.EqualTo("v"));
            driver.Tick(.1f,.1f);Assert.That(ui.Sessions.Count,Is.EqualTo(2));driver.Dispose();
        }
        [Test] public void UiCompletionSwitchesVideoAndCancelsOtherUi()
        {
            var video=new Playback();var ui=new UiService();var driver=Driver(video,ui);
            driver.Load(Json(new[]{Video("v",0),Video("next"),Ui("one"),Ui("two")},Edge("v","frame:0","one"),Edge("v","frame:0","two",1),Edge("one","completed","next")));
            driver.Start("v");driver.Tick(0,0);driver.Tick(0,0);
            ui.Sessions[0].Status=FlowUiStatus.Completed;driver.Tick(0,0);driver.Tick(0,0);
            Assert.That(driver.CurrentNodeId,Is.EqualTo("next"));Assert.That(driver.ActiveNodes.Count,Is.EqualTo(1));
            Assert.That(ui.Sessions.All(x=>x.Disposed),Is.True);driver.Dispose();
        }
        [Test] public void CancelledUiDoesNotFollowCompletedEdge()
        {
            var video=new Playback();var ui=new UiService();var driver=Driver(video,ui);
            driver.Load(Json(new[]{Video("v",0),Video("next"),Ui()},Edge("v","frame:0","u"),Edge("u","completed","next")));
            driver.Start("v");driver.Tick(0,0);driver.Tick(0,0);ui.Sessions[0].Status=FlowUiStatus.Cancelled;
            driver.Tick(0,0);driver.Tick(0,0);Assert.That(driver.CurrentNodeId,Is.EqualTo("v"));Assert.That(video.Paused,Is.False);driver.Dispose();
        }
        [Test] public void StopFromEnterEventPreventsBehaviorEnterAndCompletesCleanup()
        {
            var factory=new RecordingFactory();var driver=new VideoFlowDriver(new VideoFlowLoader(),factory);
            driver.Load(Json(Video()));driver.EventRaised+=e=>{if(e.Kind==FlowEventKind.NodeEntered)driver.Stop();};
            driver.Start("v");Assert.That(driver.Status,Is.EqualTo(FlowStatus.Stopped));Assert.That(driver.ActiveNodes,Is.Empty);Assert.That(factory.Trace,Does.Not.Contain("enter:v"));
        }
        [Test] public void FailedLoadRetainsGraphAndInvalidDeltaRejected()
        {
            var driver=new VideoFlowDriver(new VideoFlowLoader(),new RecordingFactory());driver.Load(Json(Video()));
            Assert.That(driver.Load("{}").Success,Is.False);Assert.That(driver.Start("v").Success,Is.True);
            Assert.Throws<ArgumentOutOfRangeException>(()=>driver.Tick(float.NaN,0));driver.Dispose();
        }
        [Test] public void StopDoesNotResumeSourceWhileClosingItsUi()
        {
            var video=new Playback();var ui=new UiService();var driver=Driver(video,ui);
            driver.Load(Json(new[]{Video("v",0),Ui()},Edge("v","frame:0","u")));
            driver.Start("v");driver.Tick(0,0);driver.Tick(0,0);Assert.That(video.Paused,Is.True);
            driver.Stop();Assert.That(video.ResumeCount,Is.EqualTo(0));Assert.That(driver.ActiveNodes,Is.Empty);
        }
        [Test] public void FaultRequestStopsRemainingQueueWithoutUpdatingOtherNodes()
        {
            var factory=new RecordingFactory();var driver=new VideoFlowDriver(new VideoFlowLoader(),factory);
            driver.Load(Json(new[]{Video("v",0),Ui()},Edge("v","frame:0","u")));driver.Start("v");
            factory.Contexts[0].Emit("frame:0");driver.Tick(0,0);
            var events=new List<FlowEvent>();driver.EventRaised+=events.Add;
            factory.Contexts[0].Fail("test","failure");factory.Contexts[1].Complete("completed");driver.Tick(0,0);
            Assert.That(driver.Status,Is.EqualTo(FlowStatus.Faulted));Assert.That(driver.ActiveNodes,Is.Empty);
            Assert.That(events.Count(e=>e.Kind==FlowEventKind.FlowFaulted),Is.EqualTo(1));
        }
        [Test] public void VideoCompletionCancelsDependentUiAndFollowsItsOwnRoute()
        {
            var video=new Playback();var ui=new UiService();var driver=Driver(video,ui);
            driver.Load(Json(new[]{Video("v",0),Video("next"),Ui()},Edge("v","frame:0","u"),Edge("v","completed","next")));
            driver.Start("v");driver.Tick(0,0);driver.Tick(0,0);video.Status=VideoPlaybackStatus.Completed;
            driver.Tick(0,0);driver.Tick(0,0);Assert.That(driver.CurrentNodeId,Is.EqualTo("next"));Assert.That(ui.Sessions[0].Disposed,Is.True);driver.Dispose();
        }
        [Test] public void CyclicSynchronousCompletionAdvancesOncePerTick()
        {
            var factory=new RecordingFactory{CompleteOnEnter=true};var driver=new VideoFlowDriver(new VideoFlowLoader(),factory);
            driver.Load(Json(new[]{Video("a"),Video("b")},Edge("a","completed","b"),Edge("b","completed","a")));
            driver.Start("a");Assert.That(factory.Contexts.Count,Is.EqualTo(1));driver.Tick(0,0);Assert.That(factory.Contexts.Count,Is.EqualTo(2));driver.Tick(0,0);Assert.That(factory.Contexts.Count,Is.EqualTo(3));driver.Dispose();
        }
        [Test] public void EnterExceptionDisposesPartiallyEnteredBehavior()
        {
            var factory=new RecordingFactory{ThrowOnEnter=true};var driver=new VideoFlowDriver(new VideoFlowLoader(),factory);driver.Load(Json(Video()));
            Assert.That(driver.Start("v").Success,Is.False);Assert.That(driver.Status,Is.EqualTo(FlowStatus.Faulted));Assert.That(factory.Trace,Does.Contain("dispose:v"));Assert.That(driver.ActiveNodes,Is.Empty);
        }
        [Test] public void CompletedFlowCanRestartWithNewActivationId()
        {
            var factory=new RecordingFactory{CompleteOnEnter=true};var driver=new VideoFlowDriver(new VideoFlowLoader(),factory);driver.Load(Json(Video()));driver.Start("v");var old=factory.Contexts[0];driver.Tick(0,0);
            Assert.That(driver.Status,Is.EqualTo(FlowStatus.Completed));driver.Start("v");Assert.That(factory.Contexts[1].ActivationId,Is.GreaterThan(old.ActivationId));Assert.That(old.Fail("late","late"),Is.False);driver.Dispose();
        }
        [Test] public void UnsupportedBehaviorRejectsLoad()
        {
            var factory=new RecordingFactory{Supported=false};var driver=new VideoFlowDriver(new VideoFlowLoader(),factory);
            Assert.That(driver.Load(Json(Video())).Success,Is.False);Assert.That(driver.Status,Is.EqualTo(FlowStatus.Empty));
        }
        [Test] public void ReentrantStopDuringExitDoesNotPoisonRestart()
        {
            var driver=new VideoFlowDriver(new VideoFlowLoader(),new RecordingFactory());driver.Load(Json(Video()));driver.Start("v");
            driver.EventRaised+=e=>{if(e.Kind==FlowEventKind.NodeExited)driver.Stop();};driver.Stop();
            Assert.That(driver.Start("v").Success,Is.True);Assert.That(driver.Status,Is.EqualTo(FlowStatus.Running));Assert.That(driver.ActiveNodes.Count,Is.EqualTo(1));driver.Dispose();
        }
        [Test] public void ConsecutiveVideoNodesDisposeOldSessionBeforeOpeningNewSession()
        {
            var sessions=new SessionService();var driver=new VideoFlowDriver(new VideoFlowLoader(),new VideoFlowBehaviorFactory(sessions,new UiService()));
            driver.Load(Json(new[]{Video("a"),Video("b")},Edge("a","completed","b")));driver.Start("a");
            sessions.Sessions[0].Status=VideoPlaybackStatus.Completed;driver.Tick(0,0);driver.Tick(0,0);
            Assert.That(sessions.OpenedAfterOldDisposed,Is.True);Assert.That(sessions.Sessions.Count,Is.EqualTo(2));Assert.That(driver.CurrentNodeId,Is.EqualTo("b"));Assert.That(sessions.Sessions[1].Status,Is.EqualTo(VideoPlaybackStatus.Playing));driver.Dispose();
        }
        sealed class SessionService:IVideoPlaybackService
        {
            public readonly List<Playback> Sessions=new List<Playback>();public bool OpenedAfterOldDisposed;
            public IVideoPlaybackSession Open(VideoPlaybackRequest request)
            {if(Sessions.Count>0)OpenedAfterOldDisposed=Sessions[Sessions.Count-1].Disposed;var session=new Playback();Sessions.Add(session);return session;}
        }
        [Test] public void StoppedSourceVideoCancelsItsDependentUi()
        {
            var video=new Playback();var ui=new UiService();var driver=Driver(video,ui);driver.Load(Json(new[]{Video("v",0),Ui()},Edge("v","frame:0","u")));
            driver.Start("v");driver.Tick(0,0);driver.Tick(0,0);video.Status=VideoPlaybackStatus.Stopped;driver.Tick(0,0);driver.Tick(0,0);
            Assert.That(driver.ActiveNodes,Is.Empty);Assert.That(ui.Sessions[0].Disposed,Is.True);
        }
        [Test] public void UiTransitionExitEventCarriesOnlyTheUiCompletionPort()
        {
            var video=new Playback();var ui=new UiService();var driver=Driver(video,ui);var events=new List<FlowEvent>();driver.EventRaised+=events.Add;
            driver.Load(Json(new[]{Video("v",0),Video("next"),Ui()},Edge("v","frame:0","u"),Edge("u","completed","next")));
            driver.Start("v");driver.Tick(0,0);driver.Tick(0,0);ui.Sessions[0].Status=FlowUiStatus.Completed;driver.Tick(0,0);driver.Tick(0,0);
            Assert.That(events.Single(e=>e.Kind==FlowEventKind.NodeExited && e.NodeId=="u").OutputPortId,Is.EqualTo("completed"));
            Assert.That(events.Single(e=>e.Kind==FlowEventKind.NodeExited && e.NodeId=="v").OutputPortId,Is.Null);driver.Dispose();
        }
        [Test] public void PreparingVideoDoesNotTriggerFrameUiUntilPlaybackReady()
        {
            var video=new Playback{InitialStatus=VideoPlaybackStatus.Preparing};var ui=new UiService();var driver=Driver(video,ui);
            driver.Load(Json(new[]{Video("v",0),Ui()},Edge("v","frame:0","u")));driver.Start("v");
            Assert.That(driver.Phase,Is.EqualTo(FlowPhase.PreparingVideo));driver.Tick(0,0);driver.Tick(0,0);
            Assert.That(ui.Sessions,Is.Empty);
            video.Status=VideoPlaybackStatus.Playing;driver.Tick(0,0);driver.Tick(0,0);
            Assert.That(driver.Phase,Is.EqualTo(FlowPhase.PlayingVideo));Assert.That(ui.Sessions.Count,Is.EqualTo(1));driver.Dispose();
        }
        [Test] public void FrameUiIsAttachedToVideoWithoutChangingTheMainState()
        {
            var video=new Playback();var ui=new UiService();var driver=Driver(video,ui);var phases=new List<FlowPhase>();
            driver.EventRaised+=e=>{if(e.Kind==FlowEventKind.PhaseChanged)phases.Add(e.Phase);};
            driver.Load(Json(new[]{Video("v",0),Ui()},Edge("v","frame:0","u")));driver.Start("v");
            var source=driver.CurrentVideo.ActivationId;var phaseCount=phases.Count;driver.Tick(0,0);driver.Tick(0,0);
            Assert.That(driver.Phase,Is.EqualTo(FlowPhase.PlayingVideo));Assert.That(phases.Count,Is.EqualTo(phaseCount));
            Assert.That(driver.CurrentVideo.ActivationId,Is.EqualTo(source));Assert.That(driver.CurrentVideo.UiActivationIds.Count,Is.EqualTo(1));
            Assert.That(driver.CurrentVideo.PauseCount,Is.EqualTo(1));Assert.That(driver.ActiveNodes.Single(n=>n.NodeId=="u").SourceVideoActivationId,Is.EqualTo(source));driver.Dispose();
        }
        [Test] public void VideoReplacementGoesThroughSwitchingBeforePreparingNewVideo()
        {
            var sessions=new SessionService();var driver=new VideoFlowDriver(new VideoFlowLoader(),new VideoFlowBehaviorFactory(sessions,new UiService()));
            var phases=new List<FlowPhase>();bool oldDisposedAtPrepare=false;
            driver.EventRaised+=e=>{if(e.Kind==FlowEventKind.PhaseChanged){phases.Add(e.Phase);if(e.Phase==FlowPhase.PreparingVideo && sessions.Sessions.Count>0)oldDisposedAtPrepare=sessions.Sessions[0].Disposed;}};
            driver.Load(Json(new[]{Video("a"),Video("b")},Edge("a","completed","b")));driver.Start("a");
            sessions.Sessions[0].Status=VideoPlaybackStatus.Completed;driver.Tick(0,0);driver.Tick(0,0);
            Assert.That(phases.Skip(phases.IndexOf(FlowPhase.SwitchingVideo)),Is.EqualTo(new[]{FlowPhase.SwitchingVideo,FlowPhase.PreparingVideo,FlowPhase.PlayingVideo}));
            Assert.That(oldDisposedAtPrepare,Is.True);Assert.That(driver.CurrentVideo.NodeId,Is.EqualTo("b"));driver.Dispose();
        }
        [Test] public void UiChainPreservesVideoContextAndDoesNotSwitchMainState()
        {
            var video=new Playback();var ui=new UiService();var driver=Driver(video,ui);driver.Load(Json(new[]{Video("v",0),Ui("one"),Ui("two")},Edge("v","frame:0","one"),Edge("one","completed","two")));
            driver.Start("v");var source=driver.CurrentVideo.ActivationId;driver.Tick(0,0);driver.Tick(0,0);
            ui.Sessions[0].Status=FlowUiStatus.Completed;driver.Tick(0,0);driver.Tick(0,0);
            Assert.That(driver.CurrentVideo.ActivationId,Is.EqualTo(source));Assert.That(driver.CurrentVideo.UiActivationIds.Count,Is.EqualTo(1));Assert.That(driver.Phase,Is.EqualTo(FlowPhase.PlayingVideo));
            Assert.That(driver.ActiveNodes.Single(n=>n.NodeId=="two").SourceVideoActivationId,Is.EqualTo(source));driver.Dispose();
        }
        [Test] public void StopDuringSwitchingPreventsNewVideoOpen()
        {
            var sessions=new SessionService();var driver=new VideoFlowDriver(new VideoFlowLoader(),new VideoFlowBehaviorFactory(sessions,new UiService()));
            driver.EventRaised+=e=>{if(e.Kind==FlowEventKind.PhaseChanged && e.Phase==FlowPhase.SwitchingVideo)driver.Stop();};
            driver.Load(Json(new[]{Video("a"),Video("b")},Edge("a","completed","b")));driver.Start("a");sessions.Sessions[0].Status=VideoPlaybackStatus.Completed;driver.Tick(0,0);driver.Tick(0,0);
            Assert.That(sessions.Sessions.Count,Is.EqualTo(1));Assert.That(sessions.Sessions[0].Disposed,Is.True);Assert.That(driver.Phase,Is.EqualTo(FlowPhase.Stopped));Assert.That(driver.CurrentVideo,Is.Null);
        }
        [Test] public void PreparationFailureFaultsAndReleasesVideoContext()
        {
            var video=new Playback{InitialStatus=VideoPlaybackStatus.Preparing};var driver=Driver(video,new UiService());driver.Load(Json(Video()));driver.Start("v");video.Status=VideoPlaybackStatus.Failed;driver.Tick(0,0);driver.Tick(0,0);
            Assert.That(driver.Status,Is.EqualTo(FlowStatus.Faulted));Assert.That(driver.Phase,Is.EqualTo(FlowPhase.Faulted));Assert.That(driver.CurrentVideo,Is.Null);Assert.That(video.Disposed,Is.True);
        }
        [Test] public void FrameConditionPreservesSourceAndDefersLaterLoopUntilUiResolves()
        {
            var r=NodeDefinitionRegistry.CreateDefault();new VideoNodeExtension().Register(r);
            var cell=new NodeDocument{Id="flag",TypeId="common.boolean",Properties=new Dictionary<string,object>{{"value",true}}};
            var condition=new NodeDocument{Id="test",TypeId="flow.condition.bool"};
            var later=new NodeDocument{Id="later",TypeId="common.control"};
            var edges=new[]{Edge("v","frame:0","test"),Edge("v","frame:10","later"),Edge("later","next","v"),
                Edge("test","true","u"),new EdgeDocument{Id="left",SourceNodeId="flag",SourcePortId="valueOut",TargetNodeId="test",TargetPortId="left"},
                new EdgeDocument{Id="right",SourceNodeId="flag",SourcePortId="valueOut",TargetNodeId="test",TargetPortId="right",Order=1}};
            var json=new GraphJsonCodec(r).Encode(new GraphDocument{Nodes=new[]{Video("v",0,10),Ui(),cell,condition,later}.ToList(),Edges=edges.ToList()});
            Assert.That(json.Success,Is.True,json.Message);
            var playback=new Playback();var ui=new UiService();var d=Driver(playback,ui);
            var loaded=d.Load(json.Value);Assert.That(loaded.Success,Is.True,loaded.Message);d.Start("v");var source=d.CurrentVideo.ActivationId;
            playback.Frame=20;playback.Status=VideoPlaybackStatus.Completed;
            for(int i=0;i<5;i++)d.Tick(0,0);
            Assert.That(ui.Sessions.Count,Is.EqualTo(1));Assert.That(ui.Sessions[0].Disposed,Is.False);
            Assert.That(d.CurrentVideo.ActivationId,Is.EqualTo(source));Assert.That(d.ActiveNodes.Single(n=>n.NodeId=="u").SourceVideoActivationId,Is.EqualTo(source));
            ui.Sessions[0].Status=FlowUiStatus.Completed;
            for(int i=0;i<5;i++)d.Tick(0,0);
            Assert.That(d.CurrentVideo.ActivationId,Is.GreaterThan(source));Assert.That(d.CurrentVideo.NodeId,Is.EqualTo("v"));d.Dispose();
        }
        [Test] public void FailedOrStoppedVideoStillCleansUiWhileSerialLogicWaits()
        {
            foreach(var status in new[]{VideoPlaybackStatus.Failed,VideoPlaybackStatus.Stopped})
            {
                var r=NodeDefinitionRegistry.CreateDefault();new VideoNodeExtension().Register(r);
                var json=new GraphJsonCodec(r).Encode(new GraphDocument{
                    Nodes=new[]{Video("v",0),Ui("u",false),new NodeDocument{Id="logic",TypeId="common.control"}}.ToList(),
                    Edges=new[]{Edge("v","frame:0","logic"),Edge("logic","next","u")}.ToList()});
                Assert.That(json.Success,Is.True,json.Message);
                var playback=new Playback();var ui=new UiService();var d=Driver(playback,ui);
                Assert.That(d.Load(json.Value).Success,Is.True);d.Start("v");
                for(int i=0;i<4;i++)d.Tick(0,0);
                Assert.That(ui.Sessions.Count,Is.EqualTo(1));playback.Status=status;
                d.Tick(0,0);d.Tick(0,0);
                Assert.That(d.ActiveNodes,Is.Empty);Assert.That(ui.Sessions[0].Disposed,Is.True);
                Assert.That(d.Status,Is.EqualTo(status==VideoPlaybackStatus.Failed?FlowStatus.Faulted:FlowStatus.Completed));d.Dispose();
            }
        }
        [Test] public void ReenteringSameVideoRestartsPlaybackButRetainsMutatedCells()
        {
            var r=NodeDefinitionRegistry.CreateDefault();new VideoNodeExtension().Register(r);
            var nodes=new[]{Video("v",0),new NodeDocument{Id="count",TypeId="common.number",Properties=new Dictionary<string,object>{{"value",2d}}},
                new NodeDocument{Id="one",TypeId="common.number",Properties=new Dictionary<string,object>{{"value",1d}}},
                new NodeDocument{Id="zero",TypeId="common.number"},
                new NodeDocument{Id="dec",TypeId="flow.modify.float32",Properties=new Dictionary<string,object>{{"target","count"},{"operation","subtract"}}},
                new NodeDocument{Id="test",TypeId="flow.condition.float32",Properties=new Dictionary<string,object>{{"operation","greater"}}}};
            var edges=new[]{Edge("v","frame:0","dec"),Edge("dec","next","test"),Edge("test","true","v"),
                new EdgeDocument{Id="operand",SourceNodeId="one",SourcePortId="valueOut",TargetNodeId="dec",TargetPortId="operand"},
                new EdgeDocument{Id="left",SourceNodeId="count",SourcePortId="valueOut",TargetNodeId="test",TargetPortId="left"},
                new EdgeDocument{Id="right",SourceNodeId="zero",SourcePortId="valueOut",TargetNodeId="test",TargetPortId="right"}};
            var json=new GraphJsonCodec(r).Encode(new GraphDocument{Nodes=nodes.ToList(),Edges=edges.ToList()});Assert.That(json.Success,Is.True,json.Message);
            var videos=new SessionService();var d=new VideoFlowDriver(new VideoFlowLoader(),new VideoFlowBehaviorFactory(videos,new UiService()));
            Assert.That(d.Load(json.Value).Success,Is.True);d.Start("v");
            for(int i=0;i<10;i++)d.Tick(0,0);
            Assert.That(videos.Sessions.Count,Is.EqualTo(2));Assert.That(videos.OpenedAfterOldDisposed,Is.True);
            Assert.That(d.Values["count"],Is.EqualTo(0f));
            videos.Sessions[1].Status=VideoPlaybackStatus.Completed;d.Tick(0,0);d.Tick(0,0);
            Assert.That(d.Status,Is.EqualTo(FlowStatus.Completed));d.Start("v");Assert.That(d.Values["count"],Is.EqualTo(2f));d.Dispose();
        }
        [Test] public void DataOutputCannotBeEmittedAsControl()
        {
            var r=NodeDefinitionRegistry.CreateDefault();var f=new RecordingFactory();
            var d=new VideoFlowDriver(new VideoFlowLoader(r),f);
            var probe=new NodeDefinition("probe","probe","test",new[]{new PortDefinition("value","value",PortDirection.Output,PortKind.Data,"bool")});r.Register(probe);
            Assert.That(d.Load(new GraphJsonCodec(r).Encode(new GraphDocument{Nodes=new List<NodeDocument>{new NodeDocument{Id="p",TypeId="probe"}}}).Value).Success,Is.True);
            d.Start("p");f.Contexts[0].Emit("value");d.Tick(0,0);
            Assert.That(d.Status,Is.EqualTo(FlowStatus.Faulted));d.Dispose();
        }
        static VideoFlowDriver Driver(Playback v,UiService u) {return new VideoFlowDriver(new VideoFlowLoader(),new VideoFlowBehaviorFactory(v,u));}
        sealed class RecordingFactory:IFlowNodeBehaviorFactory
        {
            public List<IFlowNodeContext> Contexts=new List<IFlowNodeContext>();public List<string> Trace=new List<string>();
            public bool CompleteOnEnter,ThrowOnEnter;public bool Supported=true;
            public bool Supports(string type){return Supported;}
            public IFlowNodeBehavior Create(IFlowNode node){return new Behavior(this,node.Id);}
            sealed class Behavior:IFlowNodeBehavior
            {
                readonly RecordingFactory f;readonly string id;public Behavior(RecordingFactory f,string id){this.f=f;this.id=id;}
                public void Enter(IFlowNodeContext c){f.Contexts.Add(c);f.Trace.Add("enter:"+id);if(f.ThrowOnEnter)throw new InvalidOperationException("enter failed");if(f.CompleteOnEnter)c.Complete("completed");}public void Update(float d,float r){f.Trace.Add("update:"+id);}public void Exit(){f.Trace.Add("exit:"+id);}public void Dispose(){f.Trace.Add("dispose:"+id);}
            }
        }
        sealed class Playback:IVideoPlaybackService,IVideoPlaybackSession
        {
            public VideoPlaybackStatus Status{get;set;}=VideoPlaybackStatus.Playing;public string ErrorMessage{get{return null;}}
            public VideoPlaybackStatus InitialStatus=VideoPlaybackStatus.Playing;
            public long Frame{get;set;} public bool Paused,Disposed;public int ResumeCount;public IVideoPlaybackSession Open(VideoPlaybackRequest r){Frame=0;Status=InitialStatus;return this;}
            public void Pause(){Paused=true;}public void Resume(){Paused=false;ResumeCount++;}public void Stop(){Status=VideoPlaybackStatus.Stopped;}public void Dispose(){Disposed=true;Stop();}
        }
        sealed class UiService:IFlowUiService
        {
            public List<FlowUiRequest> Requests=new List<FlowUiRequest>();public List<UiSession> Sessions=new List<UiSession>();public IFlowUiSession Open(FlowUiRequest r){Requests.Add(r);var s=new UiSession();Sessions.Add(s);return s;}
        }
        sealed class UiSession:IFlowUiSession
        {public string SelectedBranchId{get;set;}public FlowUiStatus Status{get;set;}public string ErrorMessage{get{return null;}}public bool Disposed;public void Dispose(){Disposed=true;}}
    }
}
