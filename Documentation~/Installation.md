# 安装与发布

## 通过 Git 安装

此私有仓库根目录直接放置 package.json、Runtime、Editor、Plugins 等目录，保持本仓库布局。不要把它再放到另一个 Unity 工程的 Assets 下后推送，否则将不再是根路径 UPM 包。

1. 确认当前 Git 账号有 `juker74/MoveComponent` 私有仓库的访问权限，并已配置 Git 凭据管理器或系统钥匙串，使 Unity 调用 Git 时能使用现有凭据。不要把 token 写入 Git URL、manifest 或仓库文件。
2. 本包在 `Plugins/ParadoxNotion` 内置完整 NodeCanvas / CanvasCore 源码与资源；CanvasCore 的 `FRAMEWORK_VERSION = 3.30`。若宿主项目已有 `Assets/Plugins/ParadoxNotion` 或其他副本，先将重复插件移出 `Assets`、备份到工程外，避免重复 GUID、程序集和类型。无需另行安装插件。
3. 在目标项目 Packages/manifest.json 中合并 GameFrameX scoped registry，配置见 README 和 `project-manifest.registry.example.json`。
4. 通过 Package Manager 的 Add package from git URL 使用 `https://github.com/juker74/MoveComponent.git#v1.2.1` 安装；GameFrameX 与 Unity 依赖版本由包的 package.json 声明。本版发布与验证状态以 CHANGELOG 和 Validation.md 的实际记录为准。

package.json 的 dependencies 使用 UPM 版本号。Git 地址写在宿主项目 manifest 的 dependencies 中。仓库同时提供 `project-manifest.git.example.json`，列出所有 GameFrameX 直接/传递依赖的 Git 地址和已核对版本标签；使用时同样合并已有 manifest，不能覆盖项目其他依赖。

## 本地安装

Package Manager → Add package from disk → 选择 MoveComponent/package.json。也可在宿主 manifest 使用本地路径：

```json
{
  "dependencies": {
    "com.gameframex.unity.movecomponent": "file:/Users/user/MoveComponent"
  }
}
```

本地安装同样需要配置 registry，并迁移宿主项目中重复的 NodeCanvas / CanvasCore 副本；插件已内置于包中。示例中的本地路径应替换为本机仓库路径。

## 维护与版本发布

私有仓库地址为 `https://github.com/juker74/MoveComponent`，默认分支为 `main`，访问者需拥有仓库权限和已配置的 Git 凭据。本仓库保留模块自身历史，不包含原项目的完整 Git 历史。

发布新版本时先修改 package.json 的 version 并更新 CHANGELOG，完成实际验证后记录结果，再提交变更、创建对应版本标签并推送到私有仓库。建议目标项目使用版本标签固定依赖。来源署名和各组件许可范围见 `Third Party Notices.md`，不得用包根的 Apache-2.0 文本替代内置插件的原有许可。

## 旧项目迁移

包保留原有 .cs/.asmdef GUID、类型命名空间与程序集名称，避免不必要的序列化变化。若目标项目已有原来复制的源码，应先迁移重复的旧源码，再启用本包，避免重复 GUID、程序集或类型定义。保留自己的图资产、JSON、场景、视频与 UI 预制体。内置插件也保留原 `.meta` GUID；尤其不要同时保留 `Assets` 中的旧 ParadoxNotion 与包内副本。

| 原源码范围 | 包内目录 |
| --- | --- |
| Assets/Scripts/Extensions/NodeComposer | Runtime/NodeComposer、Editor/NodeComposer、Tests/Editor/NodeComposer |
| Assets/Scripts/Extensions/NodeComposerVideo | Runtime/VideoNodes、Editor/VideoNodes、Tests/Editor/VideoNodes* |
| Assets/Scripts/Game/Logic/VideoFlow | Runtime/VideoFlow、Tests/Editor/VideoFlow、Tests/Editor/UnityIntegration |
| Assets/Scripts/Framework/UI | Runtime/Integrations/GameFrameX/UI |
| Assets/Scripts/Framework/Fsm | Runtime/Integrations/GameFrameX/Fsm |
| Assets/Plugins/ParadoxNotion | Plugins/ParadoxNotion |

引用 UI 的 JSON 仍使用 `命名空间.类名, 程序集简单名称`，不需要因包目录迁移重写 UI 身份。自己的 UI 类型所在 asmdef 应引用 `Game.Framework.MovieUI`，使用 Movie API 时引用 `Game.VideoFlow.Contracts` / `Game.VideoFlow.Runtime`；新公开类型的命名空间为 `GameFrameX.Movie.Runtime`。

新场景使用 `MovieComponent → IMovieManager → MovieManager`，显式添加带有 `GameFrameXManualStartup` 标记的 MovieComponent，绑定目录 JSON、章节图、UI 服务及视频资源；具体配置见 [影视流程](VideoFlow.md)。同一框架注册只配置一个组件，重复组件不能接管现有模块。`MovieManager` 的 Update/Shutdown 由框架 BaseComponent 统一调用，业务不再手动 Tick/Dispose。组件的 `Play()` 使用配置播放默认章节；运行中可通过 `LoadChapter` 缓存其他章节，再显式 `PlayChapter`。

已有 `VideoFlowDriver` / `VideoFlowHost` / `VideoFlowRunningState` 调用与场景身份继续保留。Driver 现为独立 MovieManager 的薄适配层，Host 与 FSM 继续驱动该兼容实例；同一 Driver 只选择 Host、FSM 或手动 Tick 其中一个更新来源。迁移到 MovieComponent 时移除对应的旧场景启动与更新绑定，避免同一业务同时启动两套入口。目录 GraphKey 的解析与章间决策仍由宿主负责。

## 执行包内测试

在测试项目 manifest 中添加或合并：

```json
{
  "dependencies": {
    "com.unity.test-framework": "1.1.33"
  },
  "testables": ["com.gameframex.unity.movecomponent"]
}
```

打开 Window → General → Test Runner，选择 EditMode。测试程序集与运行程序集分开；Test Framework 仅用于开发验证。视频解码预览的 UnityTest 需要可用的图形/视频环境，不适合用 `-nographics` 验证平台解码。
