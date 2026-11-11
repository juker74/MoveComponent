# 影视流程业务驱动

现代入口为 `MovieComponent → IMovieManager → MovieManager`。组件配置 Unity 服务并转发 API，模块维护目录、多个只读章节图缓存和单个执行章节；GameFrameX BaseComponent 统一调用模块 Update/Shutdown。执行仍复用同一套有向图、节点生命周期与主流程状态机，运行时不引用 UnityEditor 或 NodeCanvas。

## 场景绑定与业务调用

1. 在已配置 GameFrameX BaseComponent 的场景添加 `GameFrameX.Movie.Runtime.MovieComponent` 和 `Game.VideoFlow.FlowUiService`，由一个 MovieComponent 配置已注册的 Movie 模块。
2. **Catalog Json** 绑定导出的目录 JSON TextAsset。**Chapter Graphs** 每项的 **Chapter Id** 填写目录中的 `chapterId`，并将对应章图 TextAsset 拖入 **Graph Json**。可以预载任意子集；使用 `Play()` 或 **Play On Start** 时必须包含默认起始章节。
3. **Ui Service** 绑定实现 `IFlowUiService` 的组件，例如 FlowUiService。**Video Target** 指定 RenderTexture，场景 RawImage 等显示该纹理；不指定纹理时使用 APIOnly 输出。
4. **Clips** 将导出的 `source` 或 `assetGuid` 填入 **Source Or Guid**，绑定 Project 中的 VideoClip。序列化引用可保留构建资源；`Assets/...` 字符串不是播放器 URL。网络 URL、绝对磁盘路径可交给播放器；其他资源系统通过服务解析边界接入。
5. 勾选 **Play On Start**，或由业务在框架和组件就绪后调用 `movie.Play()`。该方法先校验场景绑定，再替换目录和图缓存、加载已绑定的图并播放默认起始章节；它不是继续上次播放。

MovieComponent 标记为 `GameFrameXManualStartup`，需要显式加入场景并配置，框架自动扫描不会创建未配置的实例。同一框架注册由一个组件拥有；重复组件会被禁用，不能重新配置或清理首个组件的模块资源。框架 BaseComponent 是模块的唯一更新和关闭来源。

```csharp
using GameFrameX.Movie.Runtime;
using UnityEngine;

public sealed class MovieEntry : MonoBehaviour
{
    [SerializeField] MovieComponent movie;

    public void PlayConfigured()
    {
        var result = movie.Play();
        if (!result.Success) Debug.LogError(result.Message);
    }

    // 章图 JSON 由项目资源系统取得，chapterId 来自目录。
    public void LoadAndPlay(string chapterId, string graphJson)
    {
        var loaded = movie.LoadChapter(chapterId, graphJson);
        if (!loaded.Success) { Debug.LogError(loaded.Message); return; }
        var started = movie.PlayChapter(chapterId);
        if (!started.Success) Debug.LogError(started.Message);
    }
}
```

上例的按需加载要求目录已由 `Play()` 或 `LoadCatalog(catalogJson)` 安装。已经缓存的图直接调用 `PlayChapter(chapterId)`；不必重复 LoadChapter。禁用组件只停止执行，重新启用后显式播放；销毁组件退订事件并卸载其模块资源。组件不暴露 Driver/Manager getter，也不自行 Update 或关闭全局框架。

项目 UI 接入层订阅 `FlowUiService.Opened`，按 `request.UiType` 和 `request.Parameters` 创建实际 UI，并用原始 request 调用 `Bind(request, form)`；返回 false 时关闭这次迟到的界面。订阅 `Closed` 清理对应实例。此桥不生成具体控件或预制体：

```csharp
uiService.Opened += request =>
{
    // 按 request.UiType 打开 UI，完成后调用 uiService.Bind(request, form)。
    // Bind 返回 false 时，关闭这次打开的 UI。
};
uiService.Closed += activationId => { /* 关闭对应 UI 实例。 */ };
movie.EventRaised += evt =>
    Debug.Log($"{evt.ChapterId}/{evt.NodeId}: {evt.Kind} #{evt.ActivationId}");
```

## 目录、图缓存与执行

`LoadCatalog` 只安装目录元数据；`LoadChapter` 只校验并缓存显式提供的图 JSON；`PlayChapter` 才执行已缓存章节。`MovieChapterInfo.GraphKey` 是宿主资源键，模块不会自动读取文件、解析 Assets 路径、加载缺失章图或播放首章/下一章。章节 ID 精确匹配，不从图 ID、顺序或文件名推断。

| 操作 | 范围与保留内容 |
| --- | --- |
| `LoadCatalog(json)` | 完整校验成功后停止当前执行，清除旧图缓存和执行数据，安装有序目录与 StartChapterId；无效输入保留原状态。 |
| `LoadChapter(id, json)` | 目录项必须存在；校验图、可执行入口与图 ID 唯一性后加入/替换缓存。可在播放时缓存其他章节，不改变当前执行。当前已选择章节须先 UnloadChapter 才能替换。失败保留原缓存。 |
| `PlayChapter(id)` | 目标必须已缓存；先校验目标，再关闭旧执行并从目录入口开始。无效目标保留原执行；同章重播也新建会话并恢复默认值。 |
| `Stop()` | 关闭视频、UI、逻辑和排队跳转，保留目录、全部图缓存、选中章节及运行值供查询。 |
| `UnloadChapter(id)` | 仅移除该图缓存，保留目录项；若为选中章节，同时清理执行、值和请求，清空 CurrentChapterId，回到 Empty/Idle。其他章节缓存保留；未知或已卸载 ID 无操作。 |
| `UnloadAllChapters()` | 停止并释放全部缓存与执行数据，回到 Empty/Idle；保留目录和 StartChapterId。 |
| `UnloadCatalog()` | 在卸载全部章节的基础上清除目录和 StartChapterId。 |

所有公开调用、服务回调及状态更新都在 Unity 主线程进行。`LoadCatalog`、`LoadChapter`、`PlayChapter` 返回 `GraphResult<bool>`，检查 Success/Issues；清理 API 可重复调用。普通卸载保留依赖服务与事件订阅，GF Shutdown 才关闭模块。生命周期回调内的 Stop/卸载会安全延后到当前操作结束，并优先阻止受影响执行继续跳转；重入的加载/播放返回状态错误。

组件转发完整 `IMovieManager`：上述操作、`SetDependencies`、事件及只读快照。可查询 `Status`、`Phase`、`CurrentChapterId`、`CurrentNodeId`、`StartChapterId`、`Chapters`、`LoadedChapterIds`、`CurrentVideo`、`ActiveNodes` 和 `Values`。Chapters 与 LoadedChapterIds 按目录顺序排列；快照只供观察，不暴露可变图配置。

需要定制加载器、播放器、UI 或节点行为时，在加载章图前通过 `SetDependencies(loader, behaviorFactory)` 注入；服务每次 Open 必须创建独立会话。也可在框架就绪后使用 `GameFrameX.Runtime.GameFrameworkEntry.GetModule<IMovieManager>()` 取得同一注册模块。由组件配置场景服务时，业务直接持有组件；`Play()` 始终使用场景绑定的服务。`IMovieManager` 不提供 Tick/Dispose，业务不手动更新或关闭 GF 模块。

## 编辑图、阶段与事件

独立图节点不代表同级互斥状态。JSON 仍是一视频一节点、一 UI 一节点；主视频上下文按实际触发路径创建。同一 UI 节点多次触发时，每次都有独立激活与来源归属。

- 视频帧 → UI：在来源视频上下文中打开 UI，主流程保持 PlayingVideo。
- UI 完成 → UI：替换该交互，保留来源视频；UI 完成 → 视频：清理旧上下文，经 SwitchingVideo 进入 PreparingVideo。
- 视频完成 → 视频：同样先退出旧视频；图内环路重入同一视频也创建新会话，但保留本次章节的运行值。

`MovieManager` 与 `VideoFlowStates.cs` 负责状态和路由；`VideoExecutionContext` 持有视频、帧进度、附属交互与暂停请求。`movie.Phase` 暴露 Idle、PreparingVideo、PlayingVideo、SwitchingVideo、Interacting、Completed、Stopped、Faulted。Interacting 支持无来源视频的独立 UI；暂停视频时 UI 仍更新。

`CurrentVideo` 可查看 NodeId、ActivationId、UiActivationIds 和 PauseCount；`ActiveNodes` 的 SourceVideoActivationId 表示交互归属。`FlowEventKind.PhaseChanged` 通知主阶段变化，UI 只发布对应节点事件。`FlowEvent.ChapterId` 标明事件所属章节，GraphId/NodeId 继续标明图与节点；旧单图兼容入口没有目录，ChapterId 为 null。

## 故障诊断与宿主日志

Movie 运行时代码不固定调用 GFX Log、Unity Debug 或其他日志后端。保留现有 GFX 模块结构和依赖；不订阅诊断也可以正常运行。业务按需订阅 `IMovieManager.EventRaised` 或 `MovieComponent.EventRaised`（二选一，组件转发模块事件），兼容入口订阅 Driver/Host 的同名事件。

- `FlowFaulted` 表示执行故障，清理节点后发布。事件在清理前保存 GraphId、ChapterId、NodeId、ActivationId、输入/输出端口和错误信息；`Exception` 为原始异常，主动 `context.Fail` 上报的业务错误可以没有异常。
- `Diagnostic` 表示监听异常、附加清理异常或自动播放配置失败。`ObserverException` 的 `RelatedEventKind` 标明原始事件类型，并保留该事件的身份和原始异常。监听异常本身不改变流程状态，也不阻止其余监听者收到原始通知。诊断监听者抛出的异常不再生成诊断，避免递归。
- 执行异常与清理异常同时发生时，`FlowFaulted` 保留执行异常；清理异常单独以 `Cleanup` 诊断上报。自动播放失败沿用 `GraphResult.Issues` 的错误码与消息，其上下文表示当前组件状态，未知的图 ID 为 null。主动调用 API 时仍应检查返回的 `GraphResult`。
- `FlowUiService.EventRaised` 单独通知禁用清理或打开失败后的清理异常，代码为 `UiCleanup`。UI 桥只掌握请求的 ActivationId，未知图/章节/节点字段为 null；可同时订阅此事件。`Opened`/`Closed` 继续是业务操作回调，失败仍会向调用者抛出；同时发生的绑定释放与关闭回调异常由 `AggregateException` 保留。

以下代码放在宿主接入层，示例选用 Unity Debug；也可以把输出替换为 GFX `Log.Error` 或项目已有日志函数，模块无需修改：

```csharp
void WriteMovieDiagnostic(Game.VideoFlow.FlowEvent evt)
{
    if (evt.Kind != Game.VideoFlow.FlowEventKind.FlowFaulted &&
        evt.Kind != Game.VideoFlow.FlowEventKind.Diagnostic) return;
    var context = $"chapter={evt.ChapterId}, graph={evt.GraphId}, node={evt.NodeId}, activation={evt.ActivationId}";
    UnityEngine.Debug.LogError($"[Movie/{evt.ErrorCode}] {context}: {evt.ErrorMessage}\n{evt.Exception}");
}

// 初始化接入时订阅；接入对象销毁时退订相同方法。
movie.EventRaised += WriteMovieDiagnostic;
uiService.EventRaised += WriteMovieDiagnostic;
// movie.EventRaised -= WriteMovieDiagnostic;
// uiService.EventRaised -= WriteMovieDiagnostic;
```

## 状态与切换时序

执行状态使用 Empty、Ready、Running、Completed、Stopped、Faulted；Status 描述执行，成功加载目录和章图后仍可能为 Empty，目录/缓存是否存在由 Chapters/LoadedChapterIds 查询。激活状态为 Entering → Active → Exiting → Exited。ActivationId 在模块生命周期内持续递增，Stop、卸载、换目录和同章重播均不重置，旧回调不会因 ID 重用命中新执行。

切换视频时先关闭关联 UI，退出并释放旧视频，再创建新会话。Enter 内完成、帧触发和 UI 回调只排队；每次框架 Update 消费开始时已有的请求，新请求留到下次更新，避免同步环路递归。UI 呈现可能晚一个业务更新，不提供硬件级逐帧定点暂停保证。

- 跨过多个配置帧时按帧号顺序触发；每个帧端口每次视频激活只触发一次。
- UI 的 pauseVideo 只暂停来源视频；最后一个暂停租约释放后恢复，正在关闭或已退出的视频不恢复。
- UI 无后继时结束交互；后继 UI 保留来源上下文；后继视频清理来源视频及其所有 UI。取消只退出 UI，不走结果连线。
- 来源视频停止、自然结束或替换会清理附属 UI，清理不产生 UI 结果分支；自然结束仍走视频 completed。播放错误走 failed，未配置则 FlowFaulted。
- 行为生命周期异常触发清理与故障事件；观察者异常隔离。退出后的行为上下文、旧 UI 请求与捕获的旧回调均不能修改新执行。

## 旧单图兼容入口

`VideoFlowDriver` 保留原 API，内部只是未注册 MovieManager 的薄适配层；`VideoFlowHost` 和 `VideoFlowRunningState` 继续服务旧单图场景，执行实现没有第二套状态机：

```csharp
var driver = new VideoFlowDriver(new VideoFlowLoader(),
    new VideoFlowBehaviorFactory(videoPlaybackService, flowUiService));
var loaded = driver.Load(jsonText);
if (!loaded.Success) throw new System.InvalidOperationException(loaded.Message);
var started = driver.Start(entryNodeId);
if (!started.Success) throw new System.InvalidOperationException(started.Message);
driver.Tick(deltaTime, unscaledDeltaTime); // 兼容实例：由唯一宿主每帧调用。
driver.Dispose();                         // 该独立实例的宿主负责释放。
```

旧 Host 绑定单图 Graph Json、Entry Node Id、Ui Service、Video Target 与 Clips，并由自己的 Update 调用 Tick。可选 FSM 先 Load/Start，再创建 `IFsm<IVideoFlowDriver>` 并启动 `VideoFlowRunningState`；OnUpdate 调用 Tick，OnLeave 调用 Stop。同一 Driver 只选 Host、FSM 或手动 Tick 一个来源。新 MovieComponent 使用 GF 注册模块，不再挂这些兼容更新入口；迁移时同时移除原场景的旧启动绑定，避免同一业务重复播放。

兼容 Driver 的 Start 可在 Ready、Completed、Stopped 启动，恢复全部初始值；运行中拒绝 Load，加载失败保留原图，Faulted 需重新 Load。现代入口使用前述显式目录/缓存/PlayChapter 规则。

## 扩展

实现 IFlowNodeBehaviorFactory / IFlowNodeBehavior，并向自定义 NodeDefinitionRegistry 注册节点定义，交给 VideoFlowLoader(registry)。Complete 终结当前激活，Emit 发布非终结输出，Cancel 无跳转退出，Fail 终结流程。输出端口和边保留类型校验；控制端口驱动节点激活，数据端口读取运行值；两者独立。内置数据、修改和条件行为由驱动提供，自定义行为也可通过上下文读取输入与修改数据节点。

## 验证

下列为既有验证记录；1.2.0 的完整验证、目标项目接入与发布证据待集成后记录，详见 Validation.md。

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

原有 `common.text`、`common.number`、`common.boolean` 同时作为运行时的值容器，另提供 `common.integer`。节点 ID 是身份，名称用于显示；JSON 的 `value` 是初始值，`Values` 是当前值只读快照。数据节点无需走控制线即可被读取。每次 `PlayChapter`（包括同章重播）恢复该章全部初始值，兼容 Driver 的 Start 也重置初始值；图内重新进入视频（包括同一个视频）保留修改，并创建新播放会话。图属性不被运行值修改，不提供跨章节共享值。

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
