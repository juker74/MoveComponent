using System;
using System.Collections.Generic;
using Game.VideoFlow;
using GameFrameX.NodeComposer;

namespace GameFrameX.Movie.Runtime
{
    /// <summary>主线程电影模块：缓存多个章节，同时执行一个章节。生命周期由 GF 管理。</summary>
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

    /// <summary>目录项的不可变快照，顺序由章节目录确定。</summary>
    public sealed class MovieChapterInfo
    {
        public string ChapterId { get; }
        public string Name { get; }
        public string GraphKey { get; }
        public string EntryNodeId { get; }
        public MovieChapterInfo(string chapterId, string name, string graphKey, string entryNodeId)
        { ChapterId = chapterId; Name = name; GraphKey = graphKey; EntryNodeId = entryNodeId; }
    }
}
