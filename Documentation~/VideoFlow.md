# 影视流程业务驱动

读取 NodeComposer 导出的 JSON；创建只读有向图；通过主流程状态机控制视频准备、运行和切换，UI 作为来源视频的附属交互执行。支持多帧端口、一个帧端口打开多个 UI、汇合与有向环。运行时不引用 UnityEditor 或 NodeCanvas。

## 场景使用

1. 创建一个 GameObject，添加 `VideoFlowHost`、`FlowUiService`。
2. 在 Host 的 Graph Json 指定工具导出的 JSON TextAsset，Entry Node Id 填写入口节点 ID（例如示例的 `video-a`）。入口必须显式指定，不从入度推断。
3. 把 FlowUiService 组件拖入 Host 的 Ui Service；视频输出指定 RenderTexture，场景 RawImage 等显示该纹理。不指定纹理时是 APIOnly 输出。
4. 在 Clips 中将导出的 source 路径或 assetGuid 绑定到 Project 中的 VideoClip。构建时通过序列化引用保留资源；不能直接把 `Assets/...` 字符串当作播放器 URL。网络 URL、绝对磁盘路径可以直接播放；资源系统可注入解析器。
5. 勾选 Play On Start，或调用 `host.Play()`。禁用 Host 会停止流程，重新启用后需要再次 Play。
6. UI 系统订阅 `FlowUiService.Opened`，按 request.UiType 和 request.Parameters 创建实际 UI；以 request.ActivationId 保存 UI 实例。取得实际 UI 后调用 `Bind(request, form)`；正常交互结束由基类提交分支，或调用 `Complete(activationId, branchId)`，被动关闭调用 `Cancel(activationId)`，打开失败调用 `Fail(activationId, error)`。订阅 Closed 关闭该实例。此桥不生成具体的选项或按钮界面。

```csharp
uiService.Opened += request =>
{
    // 用业务 UI 系统按 request.UiType 打开界面并传入 request.Parameters。
    // 打开完成取得 MovieUIForm 后，调用 uiService.Bind(request, form)。
    // 如果返回 false，说明请求已失效，接入层应关闭该次打开的 UI。
};
uiService.Closed += activationId => { /* 销毁对应 UI 实例。 */ };
host.EventRaised += evt => UnityEngine.Debug.Log($"{evt.Kind}: {evt.NodeId} #{evt.ActivationId}");
host.Play();
```

## 纯业务入口

```csharp
var driver = new VideoFlowDriver(
    new VideoFlowLoader(),
    new VideoFlowBehaviorFactory(videoPlaybackService, flowUiService));
var loaded = driver.Load(jsonText);
if (!loaded.Success) throw new System.InvalidOperationException(loaded.Message);
var started = driver.Start("video-a");
if (!started.Success) throw new System.InvalidOperationException(started.Message);
// 宿主每帧唯一调用一次：
driver.Tick(deltaTime, unscaledDeltaTime);
// 离开业务：
driver.Dispose();
```

注入的服务必须每次 Open 创建独立会话。接口允许换成资源系统、其他播放器和其他 UI 服务实现；请求中的 source 和 assetGuid 都传到播放器资源解析边界。

框架 FSM 接入使用 `Runtime/Integrations/GameFrameX/Fsm/VideoFlowRunningState.cs`。先 Load/Start，再由 FsmManager 创建 `IFsm<IVideoFlowDriver>` 并启动此状态，它在 OnUpdate 调用 Tick、OnLeave 调用 Stop。使用此方式时不使用 VideoFlowHost 的自动 Tick，避免双重更新。

## 编辑图与业务状态的对应

编辑器中的独立节点，不代表运行时的同级互斥状态。JSON 仍是一视频一节点、一 UI 一节点，不增加状态层级字段，也不靠节点坐标或布局推断归属。主视频上下文由实际触发路径在运行时创建；同一 UI 图节点被多次触发时，每次创建独立交互实例和来源归属。

- 视频帧 → UI：在当前视频上下文中打开 UI，主流程保持 PlayingVideo。
- UI 完成 → UI：替换该交互实例，保留来源视频上下文。
- UI 完成 → 视频：由主流程进入 SwitchingVideo，清理旧上下文，再进入 PreparingVideo。
- 视频完成 → 视频：同样经过 SwitchingVideo；同类型、同节点在环路中重新进入，也创建新的上下文。

`VideoFlowStates.cs` 实现具体状态对象及 Enter/Tick/Leave 分派；`VideoFlowDriver` 解析结果与图连线路由；内部 `VideoExecutionContext` 持有当前视频、帧触发进度、附属交互集合和暂停请求。UI 不成为主状态；播放器暂停时仍会更新 UI。

主流程阶段由 `driver.Phase` 暴露：Idle → PreparingVideo → PlayingVideo → SwitchingVideo → PreparingVideo；最终到 Completed / Stopped / Faulted。为兼容独立 UI 入口或视频结束后进入 UI，Interacting 阶段允许没有来源视频的独立交互。

`driver.CurrentVideo` 是当前视频上下文的不可变快照，可查看 NodeId、ActivationId、UiActivationIds 和 PauseCount；`driver.ActiveNodes` 中的 SourceVideoActivationId 表示交互归属。`FlowEventKind.PhaseChanged` 发布主流程状态变化，UI 打开/关闭仅发布其节点生命周期事件，不会伪装成主状态切换。

```csharp
host.EventRaised += evt =>
{
    if (evt.Kind == FlowEventKind.PhaseChanged)
        UnityEngine.Debug.Log($"主流程阶段：{evt.Phase}");
};
```

业务状态机使用普通状态对象，位于 Game；`Framework/Fsm/VideoFlowRunningState` 仍是可选的 GameFrameX 更新入口，不把 UI 或每个视频图节点注册成框架互斥状态。

## 状态与切换时序

流程状态：Empty → Ready → Running → Completed / Stopped / Faulted。激活状态：Entering → Active → Exiting → Exited。每次进入使用递增 ActivationId、新行为实例和新播放/UI 会话，环路重新进入同一节点同样如此。

视频 A → 视频 B：先关闭 A 关联 UI，退出并 Dispose A，随后创建 B 会话并进入 B。节点共用行为类型不意味着共用实例；不依赖框架 FSM 同类型状态重入。旧上下文进入 Exiting 后立即失效，旧播放器回调也不能操作新播放器。

Enter 内完成、帧触发、UI 回调的结果仅入队。每次 Tick 只消费开始时已存在的请求；处理产生的新请求留到下一 Tick，避免同步环路无限递归。UI 呈现可能比检测到帧晚一个业务 Tick，不提供视频硬件级逐帧定点暂停保证。

- 帧号由播放器已呈现帧检测；跨过多个配置帧时按帧号顺序逐一触发，每次视频激活每个配置帧触发一次，回拖不会重复触发。
- UI pauseVideo 仅暂停其来源视频；多个 UI 分别持有暂停租约，最后一个释放后恢复。已经退出或正在关闭的视频不会恢复。
- UI 所选分支无后继：结束 UI；下一 UI：保留来源视频；下一视频：关闭旧视频和它的所有 UI，再进入新视频。
- 取消 UI：仅退出该 UI，不走分支连线。直接从帧打开 UI 时，来源视频被停止、自然结束或替换会清理它所有 UI，清理不触发 UI 选中分支连线。视频自然结束仍走自己的 completed。
- 视频播放错误：有 failed 连线则走该连线，否则 FlowFaulted。行为 Enter/Update/Exit/Dispose 抛异常时清理所有激活并发布故障。
- Start 可在 Ready、Completed、Stopped 启动；运行中拒绝 Load；加载失败保留原图。Faulted 需要重新 Load。Stop/Dispose 幂等。
- 所有调用及 UI 状态更新在 Unity 主线程进行。生命周期观察者异常隔离，不改变执行结果；事件处理器应自行记录自己的异常。事件处理器中的 Stop 延迟到当前操作结束，优先于待处理跳转。

## 扩展

实现 IFlowNodeBehaviorFactory / IFlowNodeBehavior，并向自定义 NodeDefinitionRegistry 注册节点定义，交给 VideoFlowLoader(registry)。Complete 终结当前激活，Emit 发布非终结输出，Cancel 无跳转退出，Fail 终结流程。输出端口和边保留类型校验；控制端口驱动节点激活，数据端口读取运行值；两者独立。内置数据、修改和条件行为由驱动提供，自定义行为也可通过上下文读取输入与修改数据节点。

## 验证

Tests/Editor 覆盖加载、冻结参数、环、同类型视频切换顺序、暂停租约、UI 路由/取消、旧上下文、重入和故障清理。Tests/Unity 验证真实 VideoPlayer 会话对象隔离、纹理所有权、资源映射和 UI 服务桥；不以编辑模式测试声称平台解码或网络视频播放已验证。

2026-11-11 初版验证：Unity 2022.3.62f3 EditMode 中业务模块 23 项测试全部通过（19 项核心行为 + 4 项 Unity 接入）。本次主流程状态机修订后，业务模块 29 项 Unity EditMode 测试通过（25 项核心 + 4 项 Unity 接入），核心 25 项也通过独立 Mono/NUnit 执行；业务与框架适配程序集独立编译通过。完整编辑器套件另有两项既有 Undo 失败：CanUndoSuccessfulReplacement、RemovingFrameRemovesOnlyItsEdgesAndUndoRestoresEverything，本轮未修改对应编辑器实现。

## 影视 UI 类选择（UGUI）

具体影视界面继承 `Game.Framework.UI.MovieUIForm`，该基类在 `Runtime/Integrations/GameFrameX/UI` 中继承框架 `UGUI`，因此仍然属于 `UIForm`。保留现有 UGUI UIManager / UGUIFormHelper / UGUIUIGroupHelper 配置即可，无需替换辅助器。

节点属性面板只列出 `MovieUIForm` 的非抽象、非泛型子类（支持间接继承）；UI 的预制体、资源路径及 UIGroup 仍按 UGUI 原有约定配置。`FlowUiRequest.UiType` 是具体界面的 `System.Type`，由 UI 接入层交给现有打开接口。

JSON 的 `uiType` 保存 `命名空间.类名, 程序集简单名称`，不保存下拉索引或程序集版本。流程加载时要求该程序集已加载，类型确实继承 `MovieUIForm`；类型删除或改名需要在节点中重新选择。原 `uiId` 字段已移除，旧配置需重新选择 UI 类后导出；不会将旧 ID 猜测为类名。

```csharp
public sealed class ChoiceUI : Game.Framework.UI.MovieUIForm
{
    // 继承 UGUI 原有生命周期，实现具体交互。
}
```

若使用 UGUI 代码生成器，注意其当前生成的 partial 声明固定继承 `UGUI`。同一类的生成部分和逻辑部分不能分别指定 `UGUI` 与 `MovieUIForm`：需统一基类；重新生成后也应检查该声明。

已有原生 `.asset` 编辑图：选中任一 UI 节点，点击“升级此图的旧 UI 配置”，再重新选择影视 UI 类。升级保留参数与连线，支持 Undo。旧版导出 JSON 如含 `uiId`，需先将该字段替换为 `"uiType": ""` 再导入重选，业务加载器不接受旧 ID。

类型列表排除仅在 Editor 中编译的程序集和测试程序集。UI 类若放在独立 asmdef 中，该 asmdef 需要引用 `Game.Framework.MovieUI`。

## 动态 UI 分支与基类

`FlowUiRequest.Branches` 是本次完整分支集合，包含未连线分支；列表顺序与节点配置一致，集合元素不可变。`FlowUiService.Opened` 仍由项目 UI 接入层负责打开具体预制体。异步打开后用原始 request 对象调用 `uiService.Bind(request, form)`，绑定检查具体类型和本次请求身份；旧流程已经退出时返回 false，接入层应关闭本次迟到的 UI。仅传 ActivationId 的重载适用于同步且确定仍是同一次打开的场景。

绑定完成时基类调用 `OnInteractionBound()`，此时可访问 `Branches` 和 `Parameters`，不应在 UGUI 的 `OnOpen()` 中假设交互已绑定。具体 UI 不访问图或视频播放器，也不用硬编码分支 ID：

```csharp
// 在具体 MovieUIForm 子类中：
protected override void OnInteractionBound()
{
    // 例如成功为第 1 项、失败为第 2 项；数量不符应明确报配置错误。
    if (Branches.Count != 2)
        throw new System.InvalidOperationException("此交互需要成功、失败两个分支。");
    // 创建按钮/计时器时，捕获本次绑定的分支回调：
    System.Func<bool> onSuccess = CaptureFinish(Branches[0]);
    System.Func<bool> onFailure = CaptureFinish(Branches[1]);
    // 将这两个回调绑定到实际控件。选项文字仍从 Parameters 中取得。
}
```

同步处理可以使用 `TryFinish(Branches[index])`；按钮、计时器、异步任务应保存 `CaptureFinish(Branches[index])` 返回的回调，避免 UI 被复用后旧回调误提交新结果。基类拒绝外来分支对象与重复提交。`TryCancel()` 表示放弃，不产生输出；覆盖 OnClose/OnRecycle 时需调用基类实现。流程强制关闭先解绑，再发 Closed 通知，因此实际销毁 UI 不会产生结果分支。

`IFlowUiSession.SelectedBranchId` 保存选中的稳定 ID。行为将它转换为分支输出端口，沿现有有向图路由；未知分支由桥拒绝，第三方服务返回非法分支时流程明确报错。玩法失败是普通分支，系统异常仍使用 Fail。旧配置保留 completed 兼容，新分支需显式提交。后继视频仍先清理来源视频及附属 UI 再进入新视频；后继 UI 保留来源上下文；无连线仅结束当前交互并释放暂停。

## 数据节点与流程值统一

原有 `common.text`、`common.number`、`common.boolean` 同时作为运行时的值容器，另提供 `common.integer`。节点 ID 是身份，名称用于显示；JSON 的 `value` 是初始值，驱动中的 `Values` 是当前值只读快照。数据节点无需走控制线即可被读取。每次 `Start` 恢复全部初始值，图内重新进入视频（包括同一个视频）保留修改，并从头创建新播放会话。图属性不被运行值修改。

- 数据节点 `valueOut` 输出当前值，可连接多个消费者；仅供读取，不触发节点执行。
- 数据节点 `valueIn` 可选；只有控制线进入该节点时才采样并写入当前值，然后走 `next`。输入不形成别名，不递归计算，因此数据节点互连不会造成求值递归。
- `flow.modify.<数据类型>`：选择相同类型的目标数据节点，以 `operand` 接收操作值，控制进入后执行运算，再走 `next`。目标保存稳定节点 ID。
- `flow.condition.<数据类型>`：通过 `left`、`right` 读取两个当前值，控制进入时比较，沿 `true` 或 `false` 路由；未连接的结果结束本次逻辑激活。
- 默认 string/bool 支持赋值、等于、不等于；int32/float32 另支持增加、减少及大小比较。整数溢出、非有限浮点值等会使流程明确报错，失败写入不会改变原值。当前没有自动数值转换，整数和浮点端口不能直接混连。

例如：数据节点 X 的输出接条件的左值，另一个数据节点的输出接右值；视频帧接条件的进入，成立接 UI，不成立可以结束此逻辑链。UI 分支可以接修改节点，修改完成再接视频。所有值都来自通用节点配置，不包含特定玩法逻辑。

视频帧进入条件/修改/通用控制节点后，这条逻辑链和后续 UI 都附属于原视频；等待结果期间按顺序推迟该视频后续帧和正常 completed 路由，防止较晚的循环帧抢先取消尚未完成的交互。此时是否暂停实际播放仍由 UI 的 pauseVideo 决定；不暂停时视频可继续播放，待逻辑链结束后补发跨过的帧。播放错误与外部停止仍立即进入清理流程。直接帧到 UI 的原有并发行为保持兼容。

自定义行为使用 `context.TryReadInput(portId, out value)`、`ReadValue(dataNodeId)`、`WriteValue(dataNodeId, value)` 访问当前运行；终结请求被接受或退出后，上下文不能继续写入。`Values` 快照用于外部观察，不能直接修改。

## 扩展值类型

在工具注册表与传给 `VideoFlowLoader(registry)` 的业务注册表中使用同一个注册方法：

1. `RegisterDataType(id, acceptsJson)` 声明 JSON 属性的合法表示。
2. `RegisterValueType(new ValueTypeDefinition(id, normalize, modifications, comparisons))` 注册从 JSON 表示/当前运行值到不可变值的转换与该类型支持的运算。运行值必须是值类型或 string；自定义结构体及其内部成员需保持不可变。例如结构体用 JSON 对象表示，decimal/大整数用字符串表示，避免 JSON double 精度损失。
3. `RegisterValueNodes(nodeTypeId, displayName, id, initialJsonValue)` 注册数据节点及对应修改、条件节点，编辑器自动提供目标下拉框、运算选择和自定义初始值 JSON 编辑。

`assign` 默认提供；比较默认使用值相等，也可以注册自定义 `equal`。运算实现返回同类型运行值，结果再次归一化并校验。没有注册的运算不会出现在该类型的菜单中，手工篡改 JSON 也会在业务加载时被拒绝。自定义值类型不要求具备加减或大小比较。

加载器会检查修改目标、操作是否受支持、必需数据输入和数据来源；无效加载不替换当前图。当前支持的数据源为数据节点的当前值输出，尚未加入表达式或递归计算节点。

2026-11-11 统一数据节点版本验证：当前项目 Unity EditMode 选定70项全部通过（42项核心业务、8项Unity接入、7项通用协议、7项帧/UI协议、6项编辑器集成）；5个相关程序集独立编译通过。独立业务42项、协议18项也全部通过。本轮仅验证选定用例，不将两项既有Undo失败计为已修复。

包导出后的独立项目验证结果见 Validation.md；上述历史记录不替代本包的验证报告。
