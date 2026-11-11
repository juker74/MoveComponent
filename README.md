# MoveComponent

可通过 Git URL 安装的 Unity UPM 源码仓库，包含完整的多端口节点编辑器、视频/UI 节点扩展、JSON 导入导出和影视流程驱动。仓库根目录的 `package.json` 就是包入口，不需要 `?path=`，也不需要导出 `.unitypackage`。

- 包名：`com.gameframex.unity.movecomponent`
- 版本：`1.0.0`
- Unity：`2022.3` 起，验证版本 `2022.3.62f3`
- 保留现有命名空间、程序集名称与脚本 GUID，便于迁移原项目。

## 安装

1. 在目标项目安装 **NodeCanvas / ParadoxNotion CanvasCore**，需要名为 `ParadoxNotion` 的程序集。当前验证使用 CanvasCore `3.30`；该插件未包含在本仓库中。
2. 将下面的 registry 合并到目标项目 `Packages/manifest.json` 的 `scopedRegistries` 数组，保留项目已有配置：

```json
{
  "name": "GameFrameX",
  "url": "https://gameframex.upm.alianblank.uk",
  "scopes": ["com.gameframex.unity"]
}
```

3. 在 Unity **Window → Package Manager → + → Add package from git URL** 输入以下地址，安装 `v1.0.0`：

```text
https://github.com/juker74/MoveComponent.git#v1.0.0
```

`package.json` 中声明的 GameFrameX 与 Unity 依赖会由 UPM 解析。包内的 registry 配置示例供宿主项目合并，UPM 不会从依赖包自动设置项目的 scoped registries。

也可以先通过 **Add package from disk** 选择本仓库根目录的 `package.json`。如果不使用 GameFrameX registry，见 [依赖说明](Documentation~/Dependencies.md) 中完整的 Git 依赖安装方案。

## 编辑与运行

1. 通过 **Tools → GameFrameX → Node Composer → 创建图** 新建图，双击图资产打开编辑器。
2. 添加视频、UI、数据、修改或条件节点，用控制线决定执行顺序，用带类型的数据端口传值；视频支持拖入 VideoClip、画面预览和多个帧输出，UI 支持类型选择及动态结果端口。
3. 在工具栏导入/导出 JSON。Package Manager 的 **Samples → Graph Examples → Import** 提供可导入的示例图与短视频。
4. 在场景添加 `Game.VideoFlow.VideoFlowHost` 和 `FlowUiService`，配置导出的 JSON、入口节点 ID、视频纹理与 Clips 映射，并勾选 Play On Start。
5. 具体影视 UI 继承 `Game.Framework.UI.MovieUIForm`。项目 UI 接入层订阅 `FlowUiService.Opened`，按请求类型创建界面后调用 `Bind(request, form)`，订阅 `Closed` 清理界面。具体控件与业务预制体由目标项目提供。

驱动通过有向图和主流程状态机运行；UI/逻辑节点可以附属于来源视频。数据节点是每次运行共享的值容器：视频循环保留当前值，重新 Start 恢复初始值。所有运行代码不依赖 UnityEditor 或 NodeCanvas 编辑器。

## 目录

| 目录 | 内容 |
| --- | --- |
| `Runtime/NodeComposer` | 图协议、严格 JSON、类型注册、数据/条件/修改节点定义 |
| `Runtime/VideoNodes` | 视频、帧端口、UI 类型配置和动态分支协议 |
| `Runtime/VideoFlow` | 图加载、运行值存储、节点生命周期、驱动状态机和播放器/UI 服务 |
| `Runtime/Integrations/GameFrameX` | MovieUIForm 基类与 GameFrameX FSM 更新适配 |
| `Editor/NodeComposer` | 节点画布、多端口连线、属性配置、JSON 文件操作 |
| `Editor/VideoNodes` | 视频预览、资源拖拽、帧编辑、影视 UI 类选择与分支编辑 |
| `Tests/Editor` | 协议、驱动、Unity 接入及编辑器测试和测试视频 |
| `Samples~` | 可通过 Package Manager 导入的示例 |
| `Documentation~` | 安装、依赖 Git 地址、模块说明与验证记录 |
| `Tools~` | 无额外 Python 依赖的包结构验证脚本 |

## 进一步使用

- [安装、Git 发布与旧项目迁移](Documentation~/Installation.md)
- [完整依赖、Git 地址与版本](Documentation~/Dependencies.md)
- [节点编辑器](Documentation~/NodeComposer.md)
- [视频/UI 扩展](Documentation~/VideoNodes.md)
- [驱动、UI 接入与值类型扩展](Documentation~/VideoFlow.md)
- [验证记录](Documentation~/Validation.md)

对包结构执行检查：

```sh
python3 Tools~/validate_package.py
```

具体 UI、项目资源加载、游戏流程配置不属于包内固定玩法；包提供完整编辑、导出、解析和运行能力及相应接入边界。

## 引用、来源与作者

本模块整理自基于 [GameFrameX.Unity](https://github.com/GameFrameX/GameFrameX.Unity) 的本地工程，提供节点编辑、视频扩展与流程驱动能力。保留原有命名空间、程序集身份和脚本 GUID。框架来源、外部依赖与设计参考分别列出如下：

| 类型 | 项目或文章 | 作者 / 维护者 | 用途 |
| --- | --- | --- | --- |
| 框架来源 | [GameFrameX.Unity](https://github.com/GameFrameX/GameFrameX.Unity) / [GameFrameX](https://github.com/GameFrameX) | Blank（包元数据署名）、GameFrameX 贡献者 | 模块结构、FSM、UI 及相关基础设施 |
| 外部编辑器依赖 | [NodeCanvas / CanvasCore](https://nodecanvas.paradoxnotion.com/) | ParadoxNotion | 节点画布与编辑器基础；需单独安装，源码未包含在本仓库 |
| 设计参考 | [MissionSystem](https://github.com/529324416/MissionSystem) | PrinceBiscuit（GitHub：[529324416](https://github.com/529324416)） | 节点编辑与图结构设计的参考项目 |
| 设计参考 | [用户提供的知乎文章](https://zhuanlan.zhihu.com/p/718702259) | 作者以原文署名为准（本次未能核实） | 早期设计讨论参考 |
| Git 忽略模板 | [github/gitignore — Unity.gitignore](https://github.com/github/gitignore/blob/main/Unity.gitignore) | GitHub gitignore 社区贡献者 | 标准 Unity 忽略规则，附加本仓库工具和产物规则 |

本仓库由 [juker74](https://github.com/juker74) 维护。依赖模块的具体 Git 地址与验证版本见 [Dependencies.md](Documentation~/Dependencies.md)，其他第三方说明见 [Third Party Notices.md](Third%20Party%20Notices.md)。来源框架的 Apache-2.0 许可保留在 [LICENSE.md](LICENSE.md)；外部插件和依赖各自遵循原作者许可。
