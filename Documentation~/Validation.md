# 包导出验证

## 1.0.1 — 内置 ParadoxNotion

验证日期：2026-11-11。Unity 版本：2022.3.62f3。

独立验证工程通过 UPM `file:` 安装仓库根目录，移除了 `Assets` 中原有的 ParadoxNotion 副本。包内全部 **15 个程序集**（包括 `ParadoxNotion` 与 `NodeCanvas`）成功编译，源码均来自 MoveComponent 包。

- 5 个 EditMode 测试程序集共 **87 项：87 通过、0 失败、0 跳过**；启用图形环境以验证视频解码与预览。
- `StyleSheet/StyleSheetDark`、`StyleSheet/StyleSheetLight`、`TypeIcons/Implicit/System.Object`、`Manual`、`Resources`、`Support`、`Community` 均由 `Resources.Load` 成功加载。
- ComposerGraph 脚本 GUID 正常解析到 `Packages/com.gameframex.unity.movecomponent/Editor/NodeComposer/ComposerGraph.cs`。
- 结构检查通过：**501 个 C# 文件、15 个程序集、758 个唯一 GUID**。全部 1,244 个插件文件与原安装副本逐文件字节一致，根目录 `.meta` 也完整保留。
- `python3 Tools~/test_validate_package.py` 验证真实 NodeCanvas GUID 程序集引用可解析，并确认不存在的 GUID 目标被拒绝；不再将 ParadoxNotion 当作无需提供的外部程序集。

依赖版本沿用记录中的组合；独立验证使用本机已有的 GameFrameX / Unity 包缓存。此外，目标 GameFrameX.Unity 工程已通过私有远端 Git 安装 `v1.0.1`（提交 `155c1783a226d76f2b5748dac9b0b3f8998bde8a`），移除了本地插件副本。UPM 成功解析该提交，15 个程序集均来自 Git 包，以上 7 个 Resources 均加载成功；现有 ComposerGraph 资产正常关联包内脚本，87 项 EditMode 测试再次全部通过，无失败或跳过。

机器可读结果见 `validation-summary.json`。以下 1.0.0 记录保留其原安装布局与历史结论。

## 1.0.0 — 历史记录（NodeCanvas 单独安装）

以下记录适用于未内置 ParadoxNotion 的 1.0.0 包；当时隔离项目使用另外安装的 NodeCanvas，不是 1.0.1 内置插件布局的验证结果。

验证日期：2026-11-11。Unity 版本：2022.3.62f3。

### 独立项目导入

使用全新测试项目，不复制原项目业务源码、场景或设置。先通过 UPM 的 file 引用加载仓库根目录，再通过本地 Git 仓库 URL（git+file 协议及版本标签）安装到 Library/PackageCache；外部依赖使用已安装版本的本地包缓存，NodeCanvas 单独安装。所有 13 个包内程序集成功编译。此验证检查跨项目结构与依赖闭包，不把本地依赖缓存测试声称为远程 registry 下载可用性测试。

导入中发现外部 NodeCanvas / GameFrameX 使用 AI、Physics、Physics2D 和 TextMeshPro 等模块；包 manifest 已显式补齐，并将 GameFrameX 传递依赖版本固定到验证组合。

### 完整包内测试

运行导出包的 5 个 EditMode 测试程序集（启用图形环境），Git 安装后共 **87 项：87 通过、0 失败、0 跳过**。

| 程序集 | 通过 | 失败 |
| --- | --- | --- |
| Game.VideoFlow.Tests.dll | 42 | 0 |
| Game.VideoFlow.Unity.Tests.dll | 8 | 0 |
| GameFrameX.NodeComposer.Tests.dll | 16 | 0 |
| GameFrameX.NodeComposer.Video.Editor.Tests.dll | 10 | 0 |
| GameFrameX.NodeComposer.Video.Tests.dll | 11 | 0 |

覆盖：严格 JSON、类型与连接校验、环路、节点复制/序列化、导入保护、Undo/Redo、动态帧/UI 端口、视频资源移动和预览、状态切换、UI 会话、流程值、条件/修改、自定义类型、同视频重播、过期回调与错误清理。

导出时将测试视频定位改为稳定的 Packages/com.gameframex.unity.movecomponent/... 包内路径，并为两个既有 Undo 用例补充明确的测试分组边界；增加包内视频物理路径断言，并修复视频异步准备在 Layout/Repaint 之间改变控件树造成的偶发 IMGUI 异常（准备期间保留并禁用控件）；未修改当前原项目源码。

### 仓库检查

`python3 Tools~/validate_package.py` 检查包信息、样例、13个程序集的引用闭包、86个唯一 GUID、资源存在性、缺失/孤立 .meta、Editor 隔离与机器绝对路径。校验脚本仅使用 Python 3.9+ 标准库。

GameFrameX 的 13 个依赖仓库版本标签均通过 git ls-remote 核对；准确标签及提交记录在 dependency-sources.json。NodeCanvas 本地安装元数据不含公开 Git/UPM 身份，因此列为手工安装的外部依赖。

此记录不代表验证所有 Unity 平台的视频解码、IL2CPP 构建、联网资源加载或具体游戏玩法。具体 UI 资源创建仍通过目标项目的接入层完成。

Git 安装验证使用本地临时仓库，不代表已发布远端仓库或完成远端网络认证验证。
