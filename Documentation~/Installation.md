# 安装与发布

## 通过 Git 安装

仓库根目录直接放置 package.json、Runtime、Editor 等目录，保持本仓库布局。不要把它再放到另一个 Unity 工程的 Assets 下后推送，否则将不再是根路径 UPM 包。

1. 目标项目先安装 NodeCanvas，确保存在 `ParadoxNotion` 程序集和编辑器资源。测试使用的 CanvasCore 源码报告 FRAMEWORK_VERSION = 3.30。
2. 在目标项目 Packages/manifest.json 中合并 GameFrameX scoped registry，配置见 README 和 `project-manifest.registry.example.json`。
3. 使用 `https://github.com/juker74/MoveComponent.git#v1.0.0` 安装已发布版本。
4. 通过 Package Manager 的 Add package from git URL 安装；依赖版本由包的 package.json 声明。

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

本地安装同样需要先配置 registry 和安装 NodeCanvas。

## 维护与版本发布

公开仓库地址为 `https://github.com/juker74/MoveComponent`，默认分支为 `main`。本仓库保留模块自身历史，不包含原项目的完整 Git 历史。

发布新版本时先修改 package.json 的 version 并更新 CHANGELOG，然后提交变更、创建对应版本标签并推送。建议目标项目使用版本标签固定依赖。

## 旧项目迁移

包保留原有 .cs/.asmdef GUID、类型命名空间与程序集名称，避免不必要的序列化变化。若目标项目已有原来复制的源码，应先迁移重复的旧源码，再启用本包，避免重复 GUID、程序集或类型定义。保留自己的图资产、JSON、场景、视频与 UI 预制体。

| 原源码范围 | 包内目录 |
| --- | --- |
| Assets/Scripts/Extensions/NodeComposer | Runtime/NodeComposer、Editor/NodeComposer、Tests/Editor/NodeComposer |
| Assets/Scripts/Extensions/NodeComposerVideo | Runtime/VideoNodes、Editor/VideoNodes、Tests/Editor/VideoNodes* |
| Assets/Scripts/Game/Logic/VideoFlow | Runtime/VideoFlow、Tests/Editor/VideoFlow、Tests/Editor/UnityIntegration |
| Assets/Scripts/Framework/UI | Runtime/Integrations/GameFrameX/UI |
| Assets/Scripts/Framework/Fsm | Runtime/Integrations/GameFrameX/Fsm |

引用 UI 的 JSON 仍使用 `命名空间.类名, 程序集简单名称`，不需要因包目录迁移重写 UI 身份。自己的 UI 类型所在 asmdef 应引用 `Game.Framework.MovieUI`，使用驱动时引用 `Game.VideoFlow.Contracts` / `Game.VideoFlow.Runtime`。

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
