# 包导出验证

验证日期：2026-11-11。Unity 版本：2022.3.62f3。

## 独立项目导入

使用全新测试项目，不复制原项目业务源码、场景或设置。先通过 UPM 的 file 引用加载仓库根目录，再通过本地 Git 仓库 URL（git+file 协议及版本标签）安装到 Library/PackageCache；外部依赖使用已安装版本的本地包缓存，NodeCanvas 单独安装。所有 13 个包内程序集成功编译。此验证检查跨项目结构与依赖闭包，不把本地依赖缓存测试声称为远程 registry 下载可用性测试。

导入中发现外部 NodeCanvas / GameFrameX 使用 AI、Physics、Physics2D 和 TextMeshPro 等模块；包 manifest 已显式补齐，并将 GameFrameX 传递依赖版本固定到验证组合。

## 完整包内测试

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

## 仓库检查

`python3 Tools~/validate_package.py` 检查包信息、样例、13个程序集的引用闭包、86个唯一 GUID、资源存在性、缺失/孤立 .meta、Editor 隔离与机器绝对路径。校验脚本仅使用 Python 3.9+ 标准库。

GameFrameX 的 13 个依赖仓库版本标签均通过 git ls-remote 核对；准确标签及提交记录在 dependency-sources.json。NodeCanvas 本地安装元数据不含公开 Git/UPM 身份，因此列为手工安装的外部依赖。

此记录不代表验证所有 Unity 平台的视频解码、IL2CPP 构建、联网资源加载或具体游戏玩法。具体 UI 资源创建仍通过目标项目的接入层完成。

Git 安装验证使用本地临时仓库，不代表已发布远端仓库或完成远端网络认证验证。
