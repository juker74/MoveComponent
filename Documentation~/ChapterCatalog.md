# 章节目录编辑

一张 `ComposerGraph` 表示一章。章节目录资产维护章节列表、默认起始章节、每章对应的图和入口。运行时使用 `MovieComponent → IMovieManager → MovieManager`，支持缓存多个章节图，同时执行一个章节。章节完成后由业务显式决定下一次 `PlayChapter`，不会自动切章。

## 创建和配置

1. 通过 **Tools → GameFrameX → Node Composer → 创建章节目录** 创建 `ChapterCatalog.asset`。也可以使用 **Assets → Create → GameFrameX → Chapter Catalog**。
2. 在 Inspector 的章节列表中点击 `+` 添加章节，填写名称，拖入已有的 Node Composer Graph。
3. 从入口列表选择本章的起始节点。列表显示名称和文档 ID，实际保存 ID；修改节点名称不影响入口。更换 Graph 会清空入口，需要重新选择。
4. 选择默认起始章节。拖动列表改变默认章节顺序；名称和排序变化不会改变章节 ID。
5. 点击 **打开图** 编辑章内节点，点击 **校验目录** 检查所有章节。

列表增删、排序、名称、图和入口配置均支持 Unity Undo/Redo。删除章节只移除目录项，不删除其 Graph 资产；删除默认起始章节后需要重新选择起始章节。

章节 ID 在添加时自动生成，Inspector 中只读。图内入口仅列出可执行的视频、UI、控制或逻辑节点，数据容器不作为章节入口。每章应使用独立图；重复引用同一个图，或复制资产导致 Graph 文档 ID 重复，会在校验时指出。

## 导出完整章节 bundle

点击 **导出全部 JSON**，选择空目录或本工具以前导出的专用目录。输出：

```text
Story/
├── .movecomponent-chapters
├── catalog.json
└── chapters/
    ├── <chapterId>.json
    └── <chapterId>.json
```

目录 JSON 的格式为：

```json
{
  "schemaVersion": 1,
  "startChapterId": "chapter_01",
  "chapters": [
    {
      "chapterId": "chapter_01",
      "name": "第一章",
      "graphKey": "chapters/chapter_01.json",
      "entryNodeId": "video_a"
    }
  ]
}
```

`graphKey` 自动按章节 ID 生成，是相对 bundle 的资源键；运行时通过 `MovieChapterInfo.GraphKey` 读取。宿主资源接入层可据此取得图 JSON，再按 `ChapterId` 调用 `LoadChapter`。它不是编辑器 `Assets` 路径；模块不会自动访问磁盘、资源系统或网络。章节数组顺序被保留。

导出复用现有图协议和视频驱动加载器，检查未知节点、端口/连线、视频来源、UI 类型、数据运算配置、章节身份和入口。全部校验与编码通过后才发布整批文件。原目录只在属于本工具输出且没有额外文件时允许替换；重导出会移除不再包含的章节 JSON，保留仍存在 JSON 的 Unity `.meta`。不要在导出目录中混放笔记或其他资源。

单个 JSON 不可超过 16 MiB。章节 ID 使用 1～128 个英文字母、数字、`_` 或 `-`，以字母或数字开头；禁止跨平台保留文件名，章节 ID 和资源键按不区分大小写校验唯一性。

## 导入

使用 **Tools → GameFrameX → Node Composer → 导入章节目录 JSON**，或目录 Inspector 中的 **导入为新目录**：

1. 选择 bundle 中的 `catalog.json`，保留相对目录结构和所有章图 JSON。
2. 选择当前工程 `Assets` 内的父目录。
3. 工具新建唯一的 `ImportedChapters` 子目录，创建章节目录资产与 `Graphs` 子目录下的图资产。

导入保留章节 ID、Graph 文档 ID、节点/连线 ID、章节顺序与入口。新建的 Unity 资产具有新的 Unity GUID。它不会覆盖当前目录或已有图；在导入前会预检全部 JSON，路径越界、符号链接、缺失/损坏图、无效入口均会拒绝。资产写入失败时仅清理本次新建的目录。

## 代码边界

- `Runtime/VideoNodes`：`ChapterCatalogDocument`、`ChapterDocument`、`ChapterCatalogJsonCodec`，纯数据，不引用 UnityEditor 或 NodeCanvas。
- `Editor/VideoNodes`：`ChapterCatalogAsset`、`ChapterCatalogInspector`、`ChapterCatalogFileService`，负责资产引用、编辑与 bundle 交换。
- `ChapterCatalogFileService.Validate(asset)` 返回目录纯数据；`Export(absoluteDirectory, asset)` 写完整 bundle；`Import(absoluteCatalogPath, newAssetFolder)` 导入为全新资产。
- `GameFrameX.Movie.Runtime.IMovieManager`：`LoadCatalog` 安装目录描述，`LoadChapter` 校验并缓存显式提供的图 JSON，`PlayChapter` 运行一个已缓存章节。三者互不隐含；仅加载目录不会加载图或开始播放。

场景中的 `MovieComponent.Play()` 会预检 Catalog Json 和已配置的 Chapter Graphs，然后加载这些图并播放默认起始章节。可以只绑定部分章节，但必须包含默认起始章节。其余图可在资源取得后调用组件的 `LoadChapter(chapterId, graphJson)` 加入缓存，再显式调用 `PlayChapter(chapterId)`。章节 ID 按精确身份匹配，不从图 ID 或文件名推断；不同缓存章节不能共用同一 Graph 文档 ID。

`Stop` 保留目录、图缓存和当前章节供查询；`UnloadChapter` 仅移除该图缓存（若正在选择该章则一并释放执行数据），保留目录项；`UnloadAllChapters` 保留目录；`UnloadCatalog` 连目录一起清除。加载、重播、资源绑定和事件规则见 [影视流程](VideoFlow.md)。章节解锁、通关、存档与跨章节变量由宿主实现。
