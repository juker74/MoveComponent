using System;
using System.Collections.Generic;

namespace GameFrameX.NodeComposer.Video
{
    /// <summary>章节清单；列表顺序就是默认章节顺序，不包含编辑器资产引用。</summary>
    [Serializable]
    public sealed class ChapterCatalogDocument
    {
        public int SchemaVersion = 1;
        public string StartChapterId = "";
        public List<ChapterDocument> Chapters = new List<ChapterDocument>();
    }

    [Serializable]
    public sealed class ChapterDocument
    {
        public string ChapterId;
        public string Name;
        public string GraphKey;
        public string EntryNodeId;
    }
}
