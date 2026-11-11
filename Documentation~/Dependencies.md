# 依赖模块与来源

## GameFrameX 依赖

GameFrameX 依赖沿用 1.0.0 历史验证固定的版本组合，包括 UI 引入的传递依赖版本，以避免新项目解析到不同的组合。下列 Git 版本标签此前已通过 git ls-remote 核对，提交号保存在 dependency-sources.json。1.0.1 内置插件后的整包验证状态见 Validation.md。

| 包名 | 版本 | Git 安装地址 |
| --- | --- | --- |
| `com.gameframex.unity` | 2.5.4 | `https://github.com/gameframex/com.gameframex.unity.git#2.5.4` |
| `com.gameframex.unity.litjson` | 1.2.2 | `https://github.com/gameframex/com.gameframex.unity.litjson.git#1.2.2` |
| `com.gameframex.unity.readassets` | 1.2.1 | `https://github.com/gameframex/com.gameframex.unity.readassets.git#1.2.1` |
| `com.gameframex.unity.cysharp.unitask` | 2.6.2 | `https://github.com/gameframex/com.gameframex.unity.cysharp.unitask.git#2.6.2` |
| `com.gameframex.unity.json.simplejson` | 1.1.2 | `https://github.com/gameframex/com.gameframex.unity.json.simplejson.git#1.1.2` |
| `com.gameframex.unity.ui` | 2.11.2 | `https://github.com/gameframex/com.gameframex.unity.ui.git#2.11.2` |
| `com.gameframex.unity.asset` | 3.2.0 | `https://github.com/gameframex/com.gameframex.unity.asset.git#3.2.0` |
| `com.gameframex.unity.event` | 1.4.0 | `https://github.com/gameframex/com.gameframex.unity.event.git#1.4.0` |
| `com.gameframex.unity.localization` | 2.3.2 | `https://github.com/gameframex/com.gameframex.unity.localization.git#2.3.2` |
| `com.gameframex.unity.setting` | 1.6.1 | `https://github.com/gameframex/com.gameframex.unity.setting.git#1.6.1` |
| `com.gameframex.unity.tuyoogame.yooasset` | 2.9.4 | `https://github.com/gameframex/com.gameframex.unity.tuyoogame.yooasset.git#2.9.4` |
| `com.gameframex.unity.ui.ugui` | 2.6.3 | `https://github.com/gameframex/com.gameframex.unity.ui.ugui.git#2.6.3` |
| `com.gameframex.unity.fsm` | 1.2.0 | `https://github.com/gameframex/com.gameframex.unity.fsm.git#1.2.0` |

推荐方式：在宿主项目合并 GameFrameX scoped registry，再通过 Git URL 安装 MoveComponent。配置示例见 project-manifest.registry.example.json。

全 Git 方式：合并 project-manifest.git.example.json 到宿主 manifest.dependencies，它包含以上全部 GameFrameX 直接/传递依赖，使用已核对标签；Unity 自有包仍由 Unity registry 提供。MoveComponent 使用私有 Git 地址 `https://github.com/juker74/MoveComponent.git#v1.0.1`；访问者需要仓库权限与现有 Git 凭据，不要把 token 放进 URL。

## 内置 NodeCanvas / ParadoxNotion

- 完整源码与资源位置：`Plugins/ParadoxNotion`，包括 `CanvasCore` 和 `NodeCanvas`，无需单独安装。
- 程序集：`ParadoxNotion`（含 NodeCanvas.Framework 与 NodeCanvas.Editor）和 `NodeCanvas`；保留原程序集名称与 `.meta` GUID。
- CanvasCore 版本：`3.30`，取自内置源码 `CanvasCore/Framework/Runtime/Graphs/Internal/GraphSource.cs` 的 `FRAMEWORK_VERSION`。内置布局已在 Unity 2022.3.62f3 验证，详情见 Validation.md。
- 原作者 / 来源：Gavalakis Evangelos / ParadoxNotion，https://nodecanvas.paradoxnotion.com/ 。
- 分发方式：随本私有仓库的 MoveComponent `v1.0.1` 一起安装；原安装文件没有独立的 Git URL 或 UPM 包名，不额外声明虚构的插件依赖。
- 宿主项目若已有 `Assets/Plugins/ParadoxNotion` 或其他 NodeCanvas / CanvasCore 副本，先将重复插件移出 `Assets` 并备份到工程外，避免重复 GUID、程序集和类型。
- 许可与署名：见仓库根目录 `Third Party Notices.md`；保留原作者信息和 FullSerializer 的原始 MIT 许可文本，包根 Apache-2.0 不覆盖 NodeCanvas / CanvasCore。

内置 NodeCanvas 使用 Unity AI、Physics、Physics2D 等模块，MoveComponent package.json 保留相应内置模块声明。

## Unity 依赖

| 包名 | 版本 | 用途 |
| --- | --- | --- |
| `com.unity.modules.androidjni` | 1.0.0 | 依赖包自动引入的 Unity 内置或官方包 |
| `com.unity.modules.animation` | 1.0.0 | 依赖包自动引入的 Unity 内置或官方包 |
| `com.unity.modules.assetbundle` | 1.0.0 | 依赖包自动引入的 Unity 内置或官方包 |
| `com.unity.modules.audio` | 1.0.0 | 播放器与框架 |
| `com.unity.modules.imgui` | 1.0.0 | 节点编辑器 |
| `com.unity.modules.jsonserialize` | 1.0.0 | 依赖包自动引入的 Unity 内置或官方包 |
| `com.unity.modules.unitywebrequest` | 1.0.0 | 依赖包自动引入的 Unity 内置或官方包 |
| `com.unity.scriptablebuildpipeline` | 2.1.0 | 依赖包自动引入的 Unity 内置或官方包 |
| `com.unity.modules.unitywebrequestassetbundle` | 1.0.0 | 依赖包自动引入的 Unity 内置或官方包 |
| `com.unity.ugui` | 1.0.0 | GameFrameX UGUI 运行时与编辑器 |
| `com.unity.modules.ui` | 1.0.0 | 依赖包自动引入的 Unity 内置或官方包 |
| `com.unity.textmeshpro` | 3.0.9 | GameFrameX UGUI 代码生成编辑器依赖 |
| `com.unity.modules.video` | 1.0.0 | 视频播放和预览 |
| `com.unity.modules.imageconversion` | 1.0.0 | 编辑器预览纹理 |
| `com.unity.modules.uielements` | 1.0.0 | 编辑器/框架依赖 |
| `com.unity.modules.ai` | 1.0.0 | NodeCanvas 类型与任务引用 |
| `com.unity.modules.physics` | 1.0.0 | NodeCanvas 与框架资源类型 |
| `com.unity.modules.physics2d` | 1.0.0 | NodeCanvas 事件与任务引用 |

Unity 自有模块与 Scriptable Build Pipeline 等通过 Unity registry/编辑器提供，不填写非公开的内部仓库地址。

开发验证另需 `com.unity.test-framework` 1.1.33（其依赖 NUnit 1.0.6），在宿主项目配置 testables 后运行；测试工具不是生产运行的必需依赖。

## 兼容范围

1.0.0 的历史验证环境为 Unity 2022.3.62f3 与表中的依赖版本组合，当时 NodeCanvas 单独安装。1.0.1 完整内置插件的编译、资源加载和 87 项测试已通过，具体结果见 Validation.md。未宣称在所有 Unity / NodeCanvas / GameFrameX 版本上验证通过。程序集名称保持不变，原项目已有同一模块副本时需先迁移重复源码。
