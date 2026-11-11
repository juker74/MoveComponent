using System;
using System.Collections.Generic;
using System.Linq;
using GameFrameX.NodeComposer;
using GameFrameX.NodeComposer.Video;

namespace Game.VideoFlow
{
    /// <summary>业务状态机驱动视频阶段；有向图负责端口路由，UI 附属于来源视频上下文。</summary>
    public sealed partial class VideoFlowDriver : IVideoFlowDriver
    {
        readonly IVideoFlowLoader loader;
        readonly IFlowNodeBehaviorFactory factory;
        readonly List<Activation> active = new List<Activation>();
        readonly Queue<Request> requests = new Queue<Request>();
        IVideoFlowGraph graph;
        readonly Dictionary<string,object> values = new Dictionary<string,object>(StringComparer.Ordinal);
        IFlowValueGraph ValueGraph { get { return graph as IFlowValueGraph; } }
        public IReadOnlyDictionary<string,object> Values { get { return new System.Collections.ObjectModel.ReadOnlyDictionary<string,object>(new Dictionary<string,object>(values)); } }
        VideoExecutionContext currentVideo;
        FlowState state;
        long sequence;
        bool busy, stopRequested, disposed;

        public FlowStatus Status { get; private set; }
        public FlowPhase Phase { get { return state == null ? FlowPhase.Idle : state.Phase; } }
        public string CurrentNodeId { get { return currentVideo?.Video.Node.Id ?? active.FirstOrDefault()?.Node.Id; } }
        public VideoExecutionSnapshot CurrentVideo { get { return currentVideo?.Snapshot(); } }
        public IReadOnlyList<FlowActivation> ActiveNodes
        {
            get { return Array.AsReadOnly(active.Select(a => new FlowActivation(a.ActivationId, a.Node.Id, a.State,
                a.Context != null && a.Context.Video != a ? (long?)a.Context.Video.ActivationId : null)).ToArray()); }
        }
        public event Action<FlowEvent> EventRaised;
        bool CanRun { get { return Status == FlowStatus.Running && !stopRequested && !disposed; } }

        public VideoFlowDriver(IVideoFlowLoader loader, IFlowNodeBehaviorFactory factory)
        {
            this.loader = loader ?? throw new ArgumentNullException(nameof(loader));
            this.factory = factory ?? throw new ArgumentNullException(nameof(factory));
        }

        public GraphResult<bool> Load(string json)
        {
            if (disposed || busy || Status == FlowStatus.Running) return GraphResult<bool>.Fail("FlowState", "当前状态不允许加载。");
            var result = loader.Load(json);
            if (!result.Success) return GraphResult<bool>.Fail(result.Issues);
            var unsupported = result.Value.Nodes.Values.Where(n => !(result.Value is IFlowValueGraph && ((IFlowValueGraph)result.Value).GetValueNode(n.Id)!=null) && n.TypeId!="common.control" && !factory.Supports(n.TypeId))
                .Select(n => new GraphIssue("UnsupportedBehavior", "没有对应节点行为: " + n.TypeId, n.Id)).ToArray();
            if (unsupported.Length > 0) return GraphResult<bool>.Fail(unsupported);
            graph = result.Value;
            values.Clear();
            Status = FlowStatus.Ready;
            // 通知期间仍使用重入保护，避免观察者在加载尚未返回时启动流程。
            Execute(() => ChangeState(new InactiveState(this, FlowPhase.Idle)));
            return GraphResult<bool>.Ok(true);
        }

        public GraphResult<bool> Start(string entryNodeId)
        {
            if (disposed || busy || graph == null || !(Status == FlowStatus.Ready || Status == FlowStatus.Completed || Status == FlowStatus.Stopped))
                return GraphResult<bool>.Fail("FlowState", "当前状态不允许启动。");
            if (entryNodeId == null || !graph.Nodes.ContainsKey(entryNodeId)) return GraphResult<bool>.Fail("Entry", "入口节点不存在。");
            Execute(() =>
            {
                values.Clear();
                if(ValueGraph!=null)
                    foreach(var n in graph.Nodes.Values)
                    {
                        var definition=ValueGraph.GetValueNode(n.Id);
                        if(definition!=null && definition.Kind==ValueNodeKind.Cell)
                            values.Add(n.Id,ValueGraph.GetValueType(definition.DataTypeId).Normalize(n.Properties["value"]));
                    }
                Status = FlowStatus.Running;
                var node = graph.Nodes[entryNodeId];
                if (node.TypeId == VideoNodeSchema.TypeId) PrepareVideo(() => Enter(node, null, null));
                else ChangeState(new InteractionState(this, () => Enter(node, null, null)));
            });
            return Status == FlowStatus.Faulted ? GraphResult<bool>.Fail("Execution", "节点进入失败；请查看 FlowFaulted 事件。") : GraphResult<bool>.Ok(true);
        }

        public void Tick(float deltaTime, float realDeltaTime)
        {
            if (float.IsNaN(deltaTime) || float.IsInfinity(deltaTime) || deltaTime < 0 ||
                float.IsNaN(realDeltaTime) || float.IsInfinity(realDeltaTime) || realDeltaTime < 0)
                throw new ArgumentOutOfRangeException(nameof(deltaTime));
            if (disposed || busy || Status != FlowStatus.Running) return;
            Execute(() =>
            {
                int count = requests.Count;
                for (int i = 0; i < count && CanRun; i++) Process(requests.Dequeue());
                if (CanRun) state.Tick(deltaTime, realDeltaTime);
                if (CanRun && active.Count == 0 && requests.Count == 0)
                {
                    Status = FlowStatus.Completed;
                    ChangeState(new InactiveState(this, FlowPhase.Completed));
                    Notify(FlowEventKind.FlowCompleted);
                }
            });
        }

        public void Stop()
        {
            if (Status != FlowStatus.Running) return;
            stopRequested = true;
            if (!busy) Execute(() => { });
        }
        public void Dispose() { if (disposed) return; disposed = true; Stop(); }

        void Execute(Action action)
        {
            busy = true;
            try { action(); }
            catch (Exception e) { Fault("Behavior", e.Message); }
            finally
            {
                if (stopRequested)
                {
                    stopRequested = false;
                    Status = FlowStatus.Stopped;
                    requests.Clear();
                    var error = Clear();
                    if (error != null) Fault("Cleanup", error.Message);
                    else
                    {
                        ChangeState(new InactiveState(this, FlowPhase.Stopped));
                        Notify(FlowEventKind.FlowStopped);
                    }
                }
                busy = false;
            }
        }

        void Notify(FlowEventKind kind, Activation a = null, string output = null, float delta = 0, float real = 0,
            string code = null, string message = null)
        {
            var handlers = EventRaised;
            if (handlers == null) return;
            var evt = new FlowEvent(kind, graph?.Id, a?.Node.Id, a?.ActivationId ?? 0, a?.InputPortId,
                output, delta, real, code, message, Phase);
            // 观察者异常不应中断资源释放或改变流程状态。
            foreach (Action<FlowEvent> handler in handlers.GetInvocationList())
                try { handler(evt); } catch { }
        }

        void Fault(string code, string message)
        {
            Status = FlowStatus.Faulted;
            requests.Clear();
            Clear();
            stopRequested = false;
            ChangeState(new InactiveState(this, FlowPhase.Faulted));
            Notify(FlowEventKind.FlowFaulted, code: code, message: message);
        }

        Exception Clear()
        {
            if (currentVideo != null) currentVideo.Closing = true;
            Exception first = null;
            foreach (var a in active.ToArray().Reverse())
                try { Exit(a, null); } catch (Exception e) { if (first == null) first = e; }
            currentVideo = null;
            return first;
        }

        void Enter(IFlowNode node, string input, VideoExecutionContext source, bool serialRoute = false)
        {
            if (!CanRun) return;
            var a = new Activation(this, node, input, ++sequence, source);
            a.SerialRoute=serialRoute;
            var valueDefinition=ValueGraph?.GetValueNode(node.Id);
            a.Behavior = (valueDefinition!=null || node.TypeId=="common.control" ? new ValueBehavior(this,valueDefinition) : factory.Create(node)) ?? throw new InvalidOperationException("行为工厂返回 null。");
            if (node.TypeId == VideoNodeSchema.TypeId)
            {
                if (currentVideo != null) throw new InvalidOperationException("进入新视频前必须退出旧视频上下文。");
                currentVideo = new VideoExecutionContext(a);
                a.Context = currentVideo;
            }
            else if (source != null) source.Interactions.Add(a);
            active.Add(a);
            Notify(FlowEventKind.NodeEntered, a);
            if (!CanRun) return;
            a.Behavior.Enter(a);
            a.State = FlowActivationState.Active;
        }

        void Exit(Activation a, string output)
        {
            if (!active.Remove(a)) return;
            a.State = FlowActivationState.Exiting;
            if (a.Context != null)
            {
                a.Context.Interactions.Remove(a);
                if (a.Context.Video == a)
                {
                    a.Context.Closing = true;
                    if (currentVideo == a.Context) currentVideo = null;
                }
            }
            try { a.Behavior.Exit(); }
            finally
            {
                try { a.Behavior.Dispose(); }
                finally { a.State = FlowActivationState.Exited; Notify(FlowEventKind.NodeExited, a, output); }
            }
        }

        void CloseVideo(VideoExecutionContext context, Activation completing, string output)
        {
            if (context == null) return;
            context.Closing = true;
            foreach (var child in context.Interactions.ToArray()) Exit(child, child == completing ? output : null);
            Exit(context.Video, context.Video == completing ? output : null);
        }

        void Process(Request request)
        {
            var a = request.Activation;
            if (!active.Contains(a)) return;
            if (request.ErrorCode != null) { Fault(request.ErrorCode, request.ErrorMessage); return; }
            if (request.Cancel)
            {
                if (a.Context != null && a.Context.Video == a) CloseVideo(a.Context, null, null);
                else Exit(a, null);
                ContinueInteractions();
                return;
            }
            var edges = graph.GetOutgoing(a.Node.Id, request.Port);
            if (!request.Terminal)
            {
                if(request.SerialRoute && a.Context!=null)a.Context.FrameRoutePending=false;
                foreach (var edge in edges)
                {
                    if (!CanRun) break;
                    var target = graph.Nodes[edge.TargetNodeId];
                    if (target.TypeId == VideoNodeSchema.TypeId)
                        SwitchVideo(() => CloseVideo(currentVideo, null, null), () => Enter(target, edge.TargetPortId, null));
                    else Enter(target, edge.TargetPortId, a.Context, request.SerialRoute);
                }
                return;
            }
            bool nextVideo = edges.Count > 0 && graph.Nodes[edges[0].TargetNodeId].TypeId == VideoNodeSchema.TypeId;
            if (nextVideo)
            {
                var edge = edges[0];
                var oldContext = a.Context;
                SwitchVideo(() =>
                {
                    if (oldContext != null) CloseVideo(oldContext, a, request.Port);
                    else Exit(a, request.Port);
                }, () => Enter(graph.Nodes[edge.TargetNodeId], edge.TargetPortId, null));
                return;
            }
            var source = a.Context;
            if (source != null && source.Video == a)
            {
                CloseVideo(source, a, request.Port);
                source = null;
            }
            else Exit(a, request.Port);
            foreach (var edge in edges) Enter(graph.Nodes[edge.TargetNodeId], edge.TargetPortId, source, a.SerialRoute);
            ContinueInteractions();
        }

        void ContinueInteractions()
        {
            if (CanRun && currentVideo == null && active.Count > 0 && Phase != FlowPhase.Interacting)
                ChangeState(new InteractionState(this));
        }

        void UpdateActivation(Activation a, float delta, float real)
        {
            if (!CanRun || a == null || a.State != FlowActivationState.Active || a.Terminal) return;
            a.Behavior.Update(delta, real);
            Notify(FlowEventKind.NodeUpdated, a, delta: delta, real: real);
        }

        void UpdateVideo(float delta, float real)
        {
            var context = currentVideo;
            if (context == null) return;
            var session = context.Session;
            // 仅推迟正常帧/完成路由；错误和外部停止必须继续清理附属交互。
            if(session!=null && (session.Status==VideoPlaybackStatus.Failed || session.Status==VideoPlaybackStatus.Stopped))
            { UpdateActivation(context.Video,delta,real);return; }
            if(context.FrameRoutePending || context.Interactions.Any(a=>a.SerialRoute))return;
            if (session != null && (session.Status == VideoPlaybackStatus.Playing || session.Status == VideoPlaybackStatus.Completed))
                context.EmitFrames(session.Frame);
            if(!context.FrameRoutePending)UpdateActivation(context.Video, delta, real);
        }

        void UpdateInteractions(float delta, float real)
        {
            // UI 不成为主状态；暂停视频时继续处理交互结果。
            foreach (var a in active.Where(a => a.Context == null || a.Context.Video != a).ToArray())
                UpdateActivation(a, delta, real);
        }

        sealed class ValueBehavior : IFlowNodeBehavior
        {
            readonly VideoFlowDriver driver;
            readonly ValueNodeDefinition definition;
            public ValueBehavior(VideoFlowDriver driver,ValueNodeDefinition definition) { this.driver=driver;this.definition=definition; }
            public void Enter(IFlowNodeContext context)
            {
                if(definition==null) { context.Complete("next");return; }
                object value;
                if(definition.Kind==ValueNodeKind.Cell)
                {
                    if(context.TryReadInput("valueIn",out value))context.WriteValue(context.Node.Id,value);
                    context.Complete("next");return;
                }
                var type=driver.ValueGraph.GetValueType(definition.DataTypeId);
                var operation=(string)context.Node.Properties["operation"];
                if(definition.Kind==ValueNodeKind.Modify)
                {
                    if(!context.TryReadInput("operand",out value))throw new InvalidOperationException("缺少操作值。");
                    var target=(string)context.Node.Properties["target"];
                    context.WriteValue(target,type.Modify(operation,context.ReadValue(target),value));
                    context.Complete("next");
                }
                else
                {
                    object left,right;
                    if(!context.TryReadInput("left",out left)||!context.TryReadInput("right",out right))throw new InvalidOperationException("缺少比较输入。");
                    context.Complete(type.Compare(operation,left,right)?"true":"false");
                }
            }
            public void Update(float delta,float real) { }
            public void Exit() { }
            public void Dispose() { }
        }

        sealed class Request
        { public Activation Activation; public string Port, ErrorCode, ErrorMessage; public bool Terminal, Cancel, SerialRoute; }

        sealed class Lease : IDisposable
        {
            Action release;
            public Lease(Action release) { this.release = release; }
            public void Dispose() { var action = release; release = null; action?.Invoke(); }
        }

        sealed class VideoExecutionContext
        {
            public readonly Activation Video;
            public readonly List<Activation> Interactions = new List<Activation>();
            readonly int[] frames;
            int nextFrame;
            public IVideoPlaybackSession Session;
            public int Pauses;
            public bool Closing, FrameRoutePending;
            public VideoExecutionContext(Activation video)
            {
                Video = video;
                frames = ((IReadOnlyList<object>)video.Node.Properties[VideoNodeSchema.FramesProperty])
                    .Select(Convert.ToInt32).OrderBy(f => f).ToArray();
            }
            public void EmitFrames(long frame)
            {
                while (!Closing && !FrameRoutePending && nextFrame < frames.Length && frame >= frames[nextFrame])
                {
                    var port=VideoNodeExtension.FramePortId(frames[nextFrame++]);
                    var serial=Video.driver.graph.GetOutgoing(Video.Node.Id,port).Any(e=>Video.driver.ValueGraph?.GetValueNode(e.TargetNodeId)!=null || Video.driver.graph.Nodes[e.TargetNodeId].TypeId=="common.control");
                    FrameRoutePending=serial;
                    Video.Enqueue(port,false,serial);
                }
            }
            public VideoExecutionSnapshot Snapshot()
            { return new VideoExecutionSnapshot(Video.Node.Id, Video.ActivationId, Interactions.Where(a => a.Node.TypeId==UiNodeSchema.TypeId).Select(a => a.ActivationId), Pauses); }
        }

        sealed class Activation : IFlowNodeContext
        {
            internal readonly VideoFlowDriver driver;
            public IFlowNode Node { get; }
            public string InputPortId { get; }
            public long ActivationId { get; }
            public FlowActivationState State = FlowActivationState.Entering;
            public IFlowNodeBehavior Behavior;
            public bool Terminal, SerialRoute;
            public VideoExecutionContext Context;
            public IVideoPlaybackSession SourceVideo { get { return Context?.Session; } }
            public Activation(VideoFlowDriver driver, IFlowNode node, string input, long id, VideoExecutionContext source)
            { this.driver = driver; Node = node; InputPortId = input; ActivationId = id; Context = source; }
            bool Valid { get { return driver.CanRun && !Terminal && (Context == null || !Context.Closing) &&
                (State == FlowActivationState.Entering || State == FlowActivationState.Active); } }
            public bool TryReadInput(string port,out object value)
            {
                if(!Valid)throw new InvalidOperationException("节点激活已结束。");
                var edge=driver.graph.Edges.SingleOrDefault(e=>e.TargetNodeId==Node.Id && e.TargetPortId==port && driver.ValueGraph?.GetOutput(e.SourceNodeId,e.SourcePortId)?.Kind==PortKind.Data);
                value=null;
                if(edge==null)return false;
                value=ReadValue(edge.SourceNodeId);return true;
            }
            public object ReadValue(string id)
            {
                if(!Valid)throw new InvalidOperationException("节点激活已结束。");
                return driver.values[id];
            }
            public bool WriteValue(string id,object value)
            {
                if(!Valid)return false;
                var definition=driver.ValueGraph?.GetValueNode(id);
                if(definition==null || definition.Kind!=ValueNodeKind.Cell)throw new ArgumentException("目标不是数据节点: "+id);
                driver.values[id]=driver.ValueGraph.GetValueType(definition.DataTypeId).Normalize(value);return true;
            }
            public bool HasOutputConnection(string port) { return driver.graph.GetOutgoing(Node.Id, port).Count > 0; }
            public bool Complete(string port) { return Enqueue(port, true); }
            public bool Emit(string port) { return Enqueue(port, false); }
            internal bool Enqueue(string port, bool terminal, bool serial = false)
            {
                if (!Valid) return false;
                try { driver.graph.GetOutgoing(Node.Id, port); }
                catch (ArgumentException) { return Fail("OutputPort", "未知输出端口: " + port); }
                var output=driver.ValueGraph?.GetOutput(Node.Id,port);
                if(output!=null && output.Kind!=PortKind.Control)return Fail("OutputPort","数据端口不可作为控制路由: "+port);
                Terminal = terminal;
                driver.requests.Enqueue(new Request { Activation = this, Port = port, Terminal = terminal, SerialRoute = serial });
                return true;
            }
            public bool Cancel()
            {
                if (!Valid) return false;
                Terminal = true;
                driver.requests.Enqueue(new Request { Activation = this, Cancel = true });
                return true;
            }
            public bool Fail(string code, string message)
            {
                if (!Valid) return false;
                Terminal = true;
                driver.requests.Enqueue(new Request { Activation = this, ErrorCode = code ?? "Behavior", ErrorMessage = message });
                return true;
            }
            public void BindVideo(IVideoPlaybackSession session)
            {
                if (!Valid || Context == null || Context.Video != this || Context.Session != null || session == null)
                    throw new InvalidOperationException("仅视频激活可绑定一次播放会话。");
                Context.Session = session;
            }
            public IDisposable PauseSourceVideo()
            {
                var context = Context;
                if (!Valid || context == null || context.Session == null) return new Lease(() => { });
                if (context.Pauses++ == 0) context.Session.Pause();
                return new Lease(() =>
                {
                    if (--context.Pauses == 0 && !context.Closing && context.Video.State == FlowActivationState.Active &&
                        context.Session.Status == VideoPlaybackStatus.Playing) context.Session.Resume();
                });
            }
        }
    }
}
