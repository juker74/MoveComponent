using System;
using System.Collections.Generic;
using Game.VideoFlow;
using GameFrameX.NodeComposer;
using GameFrameX.Runtime;
using UnityEngine;
using UnityEngine.Video;

namespace GameFrameX.Movie.Runtime
{
    /// <summary>场景电影入口；模块由 GF 统一更新，资源通过场景显式配置。</summary>
    [AddComponentMenu("GameFrameX/Movie")]
    [GameFrameXManualStartup]
    public sealed class MovieComponent : GameFrameworkComponent, IMovieManager
    {
        [Serializable] public sealed class ChapterGraphBinding { public string chapterId; public TextAsset graphJson; }
        [Serializable] public sealed class ClipBinding { public string sourceOrGuid; public VideoClip clip; }

        [SerializeField] TextAsset catalogJson;
        [SerializeField] ChapterGraphBinding[] chapterGraphs = new ChapterGraphBinding[0];
        [SerializeField] MonoBehaviour uiService;
        [SerializeField] RenderTexture videoTarget;
        [SerializeField] ClipBinding[] clips = new ClipBinding[0];
        [SerializeField] bool playOnStart;

        static readonly IReadOnlyList<MovieChapterInfo> EmptyChapters = Array.AsReadOnly(new MovieChapterInfo[0]);
        static readonly IReadOnlyList<string> EmptyIds = Array.AsReadOnly(new string[0]);
        static readonly IReadOnlyList<FlowActivation> EmptyNodes = Array.AsReadOnly(new FlowActivation[0]);
        static readonly IReadOnlyDictionary<string, object> EmptyValues =
            new System.Collections.ObjectModel.ReadOnlyDictionary<string, object>(new Dictionary<string, object>());
        IMovieManager manager;
        string pendingConfiguredChapterId;
        bool changing, destroyed, dependenciesConfigured;
        int cleanupVersion;

        public FlowStatus Status { get { return manager?.Status ?? FlowStatus.Empty; } }
        public FlowPhase Phase { get { return manager?.Phase ?? FlowPhase.Idle; } }
        public string CurrentChapterId { get { return manager?.CurrentChapterId; } }
        public string CurrentNodeId { get { return manager?.CurrentNodeId; } }
        public string StartChapterId { get { return manager?.StartChapterId; } }
        public IReadOnlyList<MovieChapterInfo> Chapters { get { return manager?.Chapters ?? EmptyChapters; } }
        public IReadOnlyList<string> LoadedChapterIds { get { return manager?.LoadedChapterIds ?? EmptyIds; } }
        public VideoExecutionSnapshot CurrentVideo { get { return manager?.CurrentVideo; } }
        public IReadOnlyList<FlowActivation> ActiveNodes { get { return manager?.ActiveNodes ?? EmptyNodes; } }
        public IReadOnlyDictionary<string, object> Values { get { return manager?.Values ?? EmptyValues; } }
        public event Action<FlowEvent> EventRaised;

        protected override void Awake()
        {
            ImplementationComponentType = typeof(MovieManager);
            InterfaceComponentType = typeof(IMovieManager);
            base.Awake();
            if (!IsRuntimeComponentReady) return;
            manager = GameFrameworkEntry.GetModule<IMovieManager>();
            manager.EventRaised += OnFlowEvent;
        }

        void Start()
        {
            if (!playOnStart) return;
            var result = Play();
            if (result.Success) return;
            var nodeId = CurrentNodeId;
            var phase = Phase;
            var chapterId = CurrentChapterId;
            long activationId = 0;
            foreach (var active in ActiveNodes)
                if (active.NodeId == nodeId) { activationId = active.ActivationId; break; }
            foreach (var issue in result.Issues)
                OnFlowEvent(new FlowEvent(FlowEventKind.Diagnostic, null, nodeId ?? issue.NodeId, activationId,
                    errorCode: issue.Code, errorMessage: issue.Message, phase: phase, chapterId: chapterId));
        }

        public void SetDependencies(IVideoFlowLoader loader, IFlowNodeBehaviorFactory behaviorFactory)
        {
            if (!Available || changing) throw new InvalidOperationException("电影组件尚未就绪、已停用或正在执行其他操作。");
            changing = true;
            try { manager.SetDependencies(loader, behaviorFactory); dependenciesConfigured = true; }
            finally { changing = false; }
        }

        public GraphResult<bool> LoadCatalog(string json) { return Operation(m => m.LoadCatalog(json)); }
        public GraphResult<bool> LoadChapter(string chapterId, string json)
        { return Operation(m => { EnsureDependencies(); return m.LoadChapter(chapterId, json); }); }
        public GraphResult<bool> PlayChapter(string chapterId) { return Operation(m => m.PlayChapter(chapterId)); }

        /// <summary>先完整验证场景绑定，再替换当前目录/缓存并播放默认章节。</summary>
        public GraphResult<bool> Play()
        {
            return Operation(live =>
            {
                if (catalogJson == null) return GraphResult<bool>.Fail("Catalog", "请配置章节目录 JSON。");
                var json = catalogJson.text;
                var loader = new VideoFlowLoader();
                var factory = SceneFactory();
                var graphs = new List<KeyValuePair<string, string>>();
                var candidate = new MovieManager();
                try
                {
                    candidate.SetDependencies(loader, factory);
                    var result = candidate.LoadCatalog(json);
                    if (!result.Success) return result;
                    var ids = new HashSet<string>(StringComparer.Ordinal);
                    foreach (var binding in chapterGraphs ?? new ChapterGraphBinding[0])
                    {
                        if (binding == null || binding.graphJson == null || string.IsNullOrWhiteSpace(binding.chapterId))
                            return GraphResult<bool>.Fail("ChapterBinding", "章节绑定必须包含章节 ID 和图 JSON。");
                        if (!ids.Add(binding.chapterId)) return GraphResult<bool>.Fail("ChapterBinding", "章节绑定 ID 重复: " + binding.chapterId);
                        var graph = binding.graphJson.text;
                        result = candidate.LoadChapter(binding.chapterId, graph);
                        if (!result.Success) return result;
                        graphs.Add(new KeyValuePair<string, string>(binding.chapterId, graph));
                    }
                    if (!ids.Contains(candidate.StartChapterId))
                        return GraphResult<bool>.Fail("ChapterBinding", "默认章节缺少图绑定: " + candidate.StartChapterId);
                    pendingConfiguredChapterId = candidate.StartChapterId;
                }
                finally { candidate.UnloadCatalog(); }

                int version = cleanupVersion;
                // 先进入模块的受保护操作；GF 更新回调中的重入必须在修改当前执行前失败。
                var loaded = live.LoadCatalog(json);
                if (!loaded.Success) return loaded;
                if (!Available || version != cleanupVersion) return Cancelled();
                live.SetDependencies(loader, factory);
                dependenciesConfigured = true;
                foreach (var graph in graphs)
                {
                    loaded = live.LoadChapter(graph.Key, graph.Value);
                    if (!loaded.Success) return loaded;
                    if (!Available || version != cleanupVersion) return Cancelled();
                }
                return live.PlayChapter(live.StartChapterId);
            });
        }

        bool Available { get { return !destroyed && manager != null && isActiveAndEnabled; } }
        static GraphResult<bool> Cancelled() { return GraphResult<bool>.Fail("FlowState", "电影操作已被停止、卸载或组件生命周期取消。"); }

        GraphResult<bool> Operation(Func<IMovieManager, GraphResult<bool>> action)
        {
            if (!Available || changing) return GraphResult<bool>.Fail("FlowState", "电影组件尚未就绪、已停用或正在执行其他操作。");
            changing = true;
            int version = cleanupVersion;
            try
            {
                var result = action(manager);
                return result.Success && (!Available || version != cleanupVersion) ? Cancelled() : result;
            }
            catch (Exception e) { return GraphResult<bool>.Fail("MovieComponent", e.Message); }
            finally { pendingConfiguredChapterId = null; changing = false; }
        }

        void EnsureDependencies()
        {
            if (dependenciesConfigured) return;
            manager.SetDependencies(new VideoFlowLoader(), SceneFactory());
            dependenciesConfigured = true;
        }

        VideoFlowBehaviorFactory SceneFactory()
        {
            var ui = uiService as IFlowUiService;
            if (ui == null) throw new InvalidOperationException("UI 服务组件必须实现 IFlowUiService。");
            var mapped = new Dictionary<string, VideoClip>(StringComparer.Ordinal);
            foreach (var binding in clips ?? new ClipBinding[0])
            {
                if (binding == null || string.IsNullOrWhiteSpace(binding.sourceOrGuid) || binding.clip == null)
                    throw new InvalidOperationException("视频绑定必须包含资源键和 VideoClip。");
                if (mapped.ContainsKey(binding.sourceOrGuid)) throw new InvalidOperationException("视频资源键重复: " + binding.sourceOrGuid);
                mapped.Add(binding.sourceOrGuid, binding.clip);
            }
            return new VideoFlowBehaviorFactory(new UnityVideoPlaybackService(transform, videoTarget, request =>
            {
                VideoClip clip;
                if (mapped.TryGetValue(request.Source, out clip)) return clip;
                return !string.IsNullOrEmpty(request.AssetGuid) && mapped.TryGetValue(request.AssetGuid, out clip) ? clip : null;
            }), ui);
        }

        void OnFlowEvent(FlowEvent evt)
        {
            FlowEventDispatcher.Raise(EventRaised, evt);
        }

        // 清理始终交给缓存模块；重入时由模块排队，GF 关闭后不重新获取模块。
        public void Stop() { cleanupVersion++; manager?.Stop(); }
        public void UnloadChapter(string chapterId)
        {
            if (chapterId != null && (chapterId == manager?.CurrentChapterId || chapterId == pendingConfiguredChapterId)) cleanupVersion++;
            manager?.UnloadChapter(chapterId);
        }
        public void UnloadAllChapters() { cleanupVersion++; manager?.UnloadAllChapters(); }
        public void UnloadCatalog() { cleanupVersion++; manager?.UnloadCatalog(); }
        void OnDisable() { Stop(); }
        void OnDestroy()
        {
            destroyed = true;
            cleanupVersion++;
            var owned = manager;
            manager = null;
            if (owned == null) return;
            owned.EventRaised -= OnFlowEvent;
            owned.UnloadCatalog();
            EventRaised = null;
        }
    }
}
