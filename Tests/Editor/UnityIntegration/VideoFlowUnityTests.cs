using System;
using GameFrameX.NodeComposer.Video;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.Video;
using Object = UnityEngine.Object;

namespace Game.VideoFlow.Tests
{
    public sealed class UnityTestMovieUI : Game.Framework.UI.MovieUIForm
    {
        public Action Bound;
        protected override void OnInteractionBound() { Bound?.Invoke(); }
        public int BranchCount => Branches.Count;
        public bool Finish(int index) => TryFinish(Branches[index]);
        public Func<bool> Capture(int index) => CaptureFinish(Branches[index]);
    }
    public class VideoFlowUnityTests
    {
        [Test] public void ReentrantBindCannotLeaveAnUntrackedUiBinding()
        {
            var owner=new GameObject("UiBranchTest");
            try
            {
                var service=owner.AddComponent<FlowUiService>();var first=owner.AddComponent<UnityTestMovieUI>();
                var secondObject=new GameObject("SecondUi");secondObject.transform.SetParent(owner.transform);
                var second=secondObject.AddComponent<UnityTestMovieUI>();
                var request=new FlowUiRequest(typeof(UnityTestMovieUI),null,1,new[]{new UiBranch("a","继续")});
                var session=service.Open(request);bool nested=true;
                first.Bound=()=>nested=service.Bind(request,second);
                Assert.That(service.Bind(request,first),Is.True);Assert.That(nested,Is.False);
                session.Dispose();Assert.That(first.BranchCount,Is.Zero);Assert.That(second.BranchCount,Is.Zero);
            }
            finally{Object.DestroyImmediate(owner);}
        }
        [Test] public void DisposingUiCancelsInteractionAndAsyncOldRequestCannotBindReplacement()
        {
            var owner=new GameObject("UiBranchTest");
            try
            {
                var service=owner.AddComponent<FlowUiService>();var form=owner.AddComponent<UnityTestMovieUI>();
                var parameters=new ReadOnlyDictionary<string,object>(new Dictionary<string,object>());
                var oldRequest=new FlowUiRequest(typeof(UnityTestMovieUI),parameters,1,new[]{new UiBranch("a","继续")});
                var old=service.Open(oldRequest);old.Dispose();
                var request=new FlowUiRequest(typeof(UnityTestMovieUI),parameters,1,new[]{new UiBranch("b","继续")});
                var current=service.Open(request);
                Assert.That(service.Bind(oldRequest,form),Is.False);
                Assert.That(service.Bind(request,form),Is.True);var callback=form.Capture(0);
                form.Dispose();Assert.That(current.Status,Is.EqualTo(FlowUiStatus.Cancelled));
                Assert.That(callback(),Is.False);current.Dispose();
            }
            finally{Object.DestroyImmediate(owner);}
        }
        [Test] public void UiBaseSubmitsSelectedBranchOnceAndOldCallbacksCannotCompleteReopenedUi()
        {
            var owner=new GameObject("UiBranchTest");
            try
            {
                var service=owner.AddComponent<FlowUiService>();var form=owner.AddComponent<UnityTestMovieUI>();
                var branches=new[]{new UiBranch("a","成功"),new UiBranch("b","失败")};
                var parameters=new ReadOnlyDictionary<string,object>(new Dictionary<string,object>());
                var first=service.Open(new FlowUiRequest(typeof(UnityTestMovieUI),parameters,1,branches));
                Assert.That(service.Bind(1,form),Is.True);Assert.That(form.BranchCount,Is.EqualTo(2));
                var old=form.Capture(0);Assert.That(form.Finish(1),Is.True);Assert.That(form.Finish(0),Is.False);
                Assert.That(first.SelectedBranchId,Is.EqualTo("b"));first.Dispose();
                var next=service.Open(new FlowUiRequest(typeof(UnityTestMovieUI),parameters,2,branches));
                Assert.That(service.Bind(2,form),Is.True);Assert.That(old(),Is.False);
                Assert.That(next.Status,Is.EqualTo(FlowUiStatus.Open));
                form.OnRecycle();Assert.That(next.Status,Is.EqualTo(FlowUiStatus.Cancelled));
                next.Dispose();Assert.That(form.BranchCount,Is.Zero);
            }
            finally{Object.DestroyImmediate(owner);}
        }
        [Test] public void UiServiceRejectsInvalidBranchAndUnbindsOnForcedClose()
        {
            var owner=new GameObject("UiBranchTest");
            try
            {
                var service=owner.AddComponent<FlowUiService>();var form=owner.AddComponent<UnityTestMovieUI>();
                var parameters=new ReadOnlyDictionary<string,object>(new Dictionary<string,object>());
                var session=service.Open(new FlowUiRequest(typeof(UnityTestMovieUI),parameters,1,new[]{new UiBranch("a","继续")}));
                Assert.That(service.Bind(1,form),Is.True);var callback=form.Capture(0);
                Assert.That(service.Complete(1,"missing"),Is.False);Assert.That(service.Complete(1),Is.False);
                Assert.That(session.Status,Is.EqualTo(FlowUiStatus.Open));
                session.Dispose();Assert.That(callback(),Is.False);Assert.That(session.Status,Is.EqualTo(FlowUiStatus.Cancelled));
            }
            finally{Object.DestroyImmediate(owner);}
        }
        [Test] public void EachVideoOpenOwnsItsPlayerAndDisposalPreservesExternalTarget()
        {
            var clip=AssetDatabase.LoadAssetAtPath<VideoClip>("Packages/com.gameframex.unity.movecomponent/Tests/Editor/VideoNodesIntegration/Fixtures/preview.mp4");
            Assert.That(clip,Is.Not.Null);
            var parent=new GameObject("FlowTest");var target=new RenderTexture(320,180,0);
            IVideoPlaybackSession first=null,second=null;
            try
            {
                var service=new UnityVideoPlaybackService(parent.transform,target,r=>clip);
                first=service.Open(new VideoPlaybackRequest("clip"));var old=parent.GetComponentInChildren<VideoPlayer>();
                first.Pause();first.Dispose();first.Dispose();Assert.That(old==null,Is.True);
                second=service.Open(new VideoPlaybackRequest("clip"));var player=parent.GetComponentInChildren<VideoPlayer>();
                Assert.That(player,Is.Not.Null);Assert.That(player.clip,Is.EqualTo(clip));Assert.That(player.targetTexture,Is.EqualTo(target));
                second.Dispose();Assert.That(parent.GetComponentsInChildren<VideoPlayer>(),Is.Empty);Assert.That(target!=null,Is.True);
            }
            finally{first?.Dispose();second?.Dispose();Object.DestroyImmediate(parent);Object.DestroyImmediate(target);}
        }
        [Test] public void ProjectPathWithoutResourceMappingFailsBeforeCreatingPlayer()
        {
            var parent=new GameObject("FlowTest");
            try{Assert.Throws<InvalidOperationException>(()=>new UnityVideoPlaybackService(parent.transform).Open(new VideoPlaybackRequest("Assets/video.mp4")));Assert.That(parent.transform.childCount,Is.Zero);}
            finally{Object.DestroyImmediate(parent);}
        }
        [Test] public void HostReentrantPlayFromStoppedEventDoesNotLeakAnExtraPlayer()
        {
            var owner=new GameObject("HostTest");var json=new TextAsset("{\"schemaVersion\":1,\"id\":\"g\",\"name\":\"g\",\"nodes\":[{\"id\":\"v\",\"typeId\":\"media.video\",\"title\":\"v\",\"x\":0,\"y\":0,\"properties\":{\"source\":\"clip\"}}],\"edges\":[]}");
            try
            {
                var ui=owner.AddComponent<FlowUiService>();var host=owner.AddComponent<VideoFlowHost>();
                var flags=BindingFlags.Instance|BindingFlags.NonPublic;
                typeof(VideoFlowHost).GetField("graphJson",flags).SetValue(host,json);
                typeof(VideoFlowHost).GetField("entryNodeId",flags).SetValue(host,"v");
                typeof(VideoFlowHost).GetField("uiService",flags).SetValue(host,ui);
                typeof(VideoFlowHost).GetField("clips",flags).SetValue(host,new[]{new VideoFlowHost.ClipBinding{sourceOrGuid="clip",clip=AssetDatabase.LoadAssetAtPath<VideoClip>("Packages/com.gameframex.unity.movecomponent/Tests/Editor/VideoNodesIntegration/Fixtures/preview.mp4")}});
                host.Play();bool reentered=false;host.EventRaised+=e=>{if(e.Kind==FlowEventKind.FlowStopped && !reentered){reentered=true;host.Play();}};
                host.Play();Assert.That(owner.GetComponentsInChildren<VideoPlayer>().Length,Is.EqualTo(1));Assert.That(host.Driver.Status,Is.EqualTo(FlowStatus.Running));
            }
            finally{Object.DestroyImmediate(owner);Object.DestroyImmediate(json);}
        }
        [Test] public void UiBridgeKeepsIndependentSessionsAndForcedCloseDoesNotComplete()
        {
            var owner=new GameObject("UiTest");
            try
            {
                var service=owner.AddComponent<FlowUiService>();var opened=new List<long>();var closed=new List<long>();
                service.Opened+=r=>opened.Add(r.ActivationId);service.Closed+=closed.Add;
                var parameters=new ReadOnlyDictionary<string,object>(new Dictionary<string,object>());
                var one=service.Open(new FlowUiRequest(typeof(UnityTestMovieUI),parameters,1));var two=service.Open(new FlowUiRequest(typeof(UnityTestMovieUI),parameters,2));
                Assert.That(opened,Is.EqualTo(new long[]{1,2}));Assert.That(service.Complete(1),Is.True);
                Assert.That(one.Status,Is.EqualTo(FlowUiStatus.Completed));Assert.That(two.Status,Is.EqualTo(FlowUiStatus.Open));
                two.Dispose();Assert.That(two.Status,Is.EqualTo(FlowUiStatus.Cancelled));Assert.That(service.Complete(2),Is.False);
                one.Dispose();Assert.That(closed,Is.EqualTo(new long[]{2,1}));
            }
            finally{Object.DestroyImmediate(owner);}
        }
    }
}
