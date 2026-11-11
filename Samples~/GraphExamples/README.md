# 图配置示例

- cycle.json：通用数据与控制环，适合验证编辑器导入/导出。
- video-cycle.json：视频环路，入口 video-a。
- video-ui.json：多帧 UI 触发与结果分支，入口 video-a。UI 类型是占位配置，导入后请选择目标项目中实际的 MovieUIForm 子类。
- preview.mp4：短测试视频。

从 Package Manager 的 Samples 导入本示例，创建 Node Composer Graph，再点击工具栏“导入配置 JSON”。视频示例用 movecomponent-preview 作为资源键；运行时在 VideoFlowHost.Clips 中将该键绑定到 preview.mp4，或将项目自己的 VideoClip 拖到节点重新导出。不得将编辑器资源路径直接当作运行时 URL。

本示例不包含具体选项 UI 或游戏玩法预制体；接入方法见包根目录 README 与 Documentation~/VideoFlow.md。
