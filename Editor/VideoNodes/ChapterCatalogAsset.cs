using System;
using System.Collections.Generic;
using GameFrameX.NodeComposer.Editor;
using UnityEngine;

namespace GameFrameX.NodeComposer.Video.Editor
{
    [CreateAssetMenu(menuName = "GameFrameX/Chapter Catalog", fileName = "ChapterCatalog")]
    public sealed class ChapterCatalogAsset : ScriptableObject
    {
        public string StartChapterId = "";
        public List<ChapterCatalogEntry> Chapters = new List<ChapterCatalogEntry>();
    }

    [Serializable]
    public sealed class ChapterCatalogEntry
    {
        public string ChapterId = Guid.NewGuid().ToString("N");
        public string Name = "新章节";
        public ComposerGraph Graph;
        public string EntryNodeId = "";
    }
}
