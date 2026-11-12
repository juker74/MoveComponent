# ADComponent

可通过 Unity Package Manager 的 Git URL 安装的广告模块。沿用 GF 的 `ADComponent → IADManager → ADManager` 结构；模块更新与关闭交给 GF 生命周期。

```text
https://github.com/juker74/MoveComponent.git#ADComponent
```

Unity 2022.3+。仓库是 UPM 包根目录，不是完整 Unity 项目。

## 安装

先在宿主 `Packages/manifest.json` 合并 `Documentation~/project-manifest.registry.example.json` 中的 GameFrameX scoped registry，再通过 Package Manager → Add package from git URL 安装上述地址。私有仓库访问沿用本机 Git 凭据。

已有项目若含同一广告模块，先将原 `Assets/Scripts/Framework/Advertising` 备份到工程外，再装包，避免重复类型和 GUID。包保留原脚本 GUID；TopOn 已迁移为独立示例程序集，原场景的 SerializeReference 平台配置与 Agent 类型名称需要重新选择后保存。不要同时保留两份 SDK。

## 内容边界

- `Runtime/Core`：平台无关接口、Manager、Helper 基类、任务队列、状态、事件。
- `Runtime/Runtime`：Unity Component、AgentHelper 基类、主线程回调分发和 GF 事件转发。
- `Editor`：通用 AD Inspector，通过反射发现宿主或示例提供的 Helper。
- `Tests/Editor`：不引用 SDK 的核心回归测试。
- `Samples~/TopOn`：TopOn 实现、完整项目内 ThinkupTpnPlugin SDK、空账号示例场景和 SDK 回归测试；仅手动导入示例后编译。

核心不依赖 AnyThink、TopOn、Firebase、Singular 或具体游戏业务。GF 基础包和 Event 为必需依赖。未导出 Hotfix 业务、线上广告位、应用账号和平台配置。

## TopOn 示例

1. Package Manager 选择 ADComponent，导入 **TopOn Example**。
2. 如果宿主没有 SDK，执行 **Tools → GameFrameX → AD → Install Sample TopOn SDK**。它将导入的 `ThinkupTpnPlugin` 移到厂商工具要求的 `Assets/ThinkupTpnPlugin`；目标已有文件时停止，不覆盖。SDK Manager 和原生构建脚本使用这个固定路径。
3. 打开示例的 `Scenes/TopOnExample.unity`。场景已放入 GF Base、Event、AD 组件和示例操作面板。
4. 在 ADComponent 的 TopOn Helper 中填写 Android / iOS AppId、AppKey，在 TopOnExample 中填写对应广告位。所有账号及广告位初始均为空。
5. Play 后点击 Load，等待加载成功后点击 Play。Editor 仅为 SDK 模拟；真实广告、奖励和回调需要目标设备验证。

原生依赖按宿主环境安装 EDM4U，通过 ThinkupTpnPlugin SDK Manager 配置相应网络及依赖。示例保留原 SDK 平台桥接与构建工具；这些内容全部隔离于 `Samples~`，不会随核心包启用。

## 业务调用

宿主配置 GF Base / Event / ADComponent，选定自己的 Helper 与各广告格式的请求/播放 Agent。业务通过 Component 发起加载及播放：

```csharp
ads.AddADRequest("my-platform", placementId, ADFormat.reward, "business", 0, 60f, userData);
ads.PlayAD("my-platform", placementId, ADFormat.reward, sceneId, "business", 0, userData);
```

通过 GF Event 订阅 `GameFrameX.Ad.Runtime.ADRequestSuccessEventArgs`、`ADPlaySuccessEventArgs` 和失败事件。激励发奖由宿主按成功事件和 UserData 关联处理。池化事件参数不要在回调结束后持有。模块不自动替业务补充广告请求。

依赖及来源见 [Dependencies](Documentation~/Dependencies.md) 和 [Third Party Notices](Third%20Party%20Notices.md)。验证结果见 [Validation](Documentation~/Validation.md)。
