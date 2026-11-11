# 通用节点拼接工具

Node Composer 仅编辑图配置，不包含自己的任务系统或节点执行器。复用本包 `Plugins/ParadoxNotion` 中完整内置的 NodeCanvas / CanvasCore，保留插件源码与资源；宿主项目不需要再安装一份插件。

## 使用

1. 在 Unity 菜单选择 `Tools > GameFrameX > Node Composer > 创建图`，或在 Project 右键选择 `Create > GameFrameX > Node Composer Graph`。
2. 双击图资产打开 NodeCanvas 画布。通过工具栏“添加节点”选择文本、数值、布尔或控制节点；也可在画布右键的“配置节点”分类中添加。
3. 从节点右侧输出端口拖到左侧输入端口；也支持从输入拖到输出。端口显示名称和数据类型，白色代表无数据控制端口，蓝色代表数据端口。
4. 选中节点，在画布属性面板编辑名称和值。右键端口可选择断开指定连线；单击线可选中，右键线或 Delete 删除。
5. 使用工具栏“导入配置 JSON / 导出配置 JSON”。插件原有的 Import JSON / Export JSON 菜单使用不同的内部格式，不用于本工具的交换协议。

支持拖动布局、画布缩放、复制节点和 Undo/Redo。图资产与派生类型只存在于 Editor 程序集；运行时消费 `GraphDocument` 或导出的 JSON，不直接引用图资产。

## 连接规则

- 数据端口只连接同一 `DataTypeId` 的数据端口；不做隐式类型转换。
- 控制端口不携带数据，没有 DataTypeId，只连接控制端口。
- 同一节点可以有多个独立输入／输出端口，端口数量不等于每个端口的连接容量。
- 内置数据节点的数据输入容量为 1、数据输出不限；数据节点与通用控制节点的控制端口保留原连接容量。新增修改节点的下一步、条件结果端口各最多连接一个后继。
- 允许 A → B → A、A → B → C → A 等环路；不允许节点连接自己。
- 允许同一节点对通过不同端口连接多条边；禁止同一对端口重复连线。
- 工具只保存拓扑，不求值、不传输数据、不执行控制流程。

## 扩展节点

在项目自己的 Editor 程序集中通过初始化代码注册类型；程序集需要引用 `GameFrameX.NodeComposer.Runtime` 与 `GameFrameX.NodeComposer.Editor`。无需为每种节点再派生 NodeCanvas Node。

```csharp
using GameFrameX.NodeComposer;
using GameFrameX.NodeComposer.Editor;
using UnityEditor;

[InitializeOnLoad]
static class RegisterExampleNode
{
    static RegisterExampleNode()
    {
        ComposerGraph.Registry.Register(new NodeDefinition(
            "example.combine", "组合", "示例",
            new[] {
                new PortDefinition("left", "左值", PortDirection.Input,
                    PortKind.Data, "int32", 1),
                new PortDefinition("right", "右值", PortDirection.Input,
                    PortKind.Data, "int32", 1),
                new PortDefinition("result", "结果", PortDirection.Output,
                    PortKind.Data, "int32"),
                new PortDefinition("enter", "进入", PortDirection.Input,
                    PortKind.Control),
                new PortDefinition("next", "下一步", PortDirection.Output,
                    PortKind.Control)
            },
            new PropertyDefinition("label", "说明", "string", "")));
    }
}
```

TypeId、端口 Id 和 DataTypeId 是稳定协议标识，修改显示名称不会改变连接。自定义类型可通过 `RegisterDataType` 登记稳定别名与属性值校验；自定义属性值仅接受 JSON 数据，默认面板编辑器支持 string、bool、int32、float32，其他类型通过 JSON 配置。不要把 Unity 对象或任意 CLR 对象放进属性。

## JSON

`schemaVersion=1`，包含图 ID／名称、节点 ID／类型／属性／坐标，以及连线 ID／两端节点及端口 ID／Order。端口定义来自注册表，不在每个节点实例中重复保存。导入环境必须注册相同的节点定义。

完整示例见 `Samples~/GraphExamples/cycle.json`：同一对节点通过数据和控制端口形成环路。

导入时先严格解析与校验，再建立临时图，成功后一次可撤销地替换。未知字段、类型或端口以及不合法连线会阻止导入。导出先校验并写同目录临时文件，再替换目标文件，失败保留已有文件。JSON 文件上限 16 MiB，嵌套上限 64 层；不支持 NaN、Infinity、任意对象引用和超出 double 精确整数范围的 long。

## 验证

在 Unity Test Runner 的 EditMode 下运行 `GameFrameX.NodeComposer.Tests`。覆盖协议往返、数据／控制连接、环路、类型／方向／容量／重复边校验、插件序列化、失败导入保护、复制 ID、Undo 以及文件安全覆盖。

2026-11-10：Unity 2022.3.62f3 EditMode 测试 15 项通过，包含旧节点引用回归；独立 Mono 协议测试 7 项通过（为上述测试的子集）。鼠标拖线和缩放菜单尚未进行人工交互验收。

包导出后的独立项目验证结果见 Validation.md；上述历史记录不替代本包的验证报告。
