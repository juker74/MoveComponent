# 依赖与来源

| 包名 | 版本 | Git 地址 |
| --- | --- | --- |
| com.gameframex.unity | 2.5.4 | https://github.com/gameframex/com.gameframex.unity.git#2.5.4 |
| com.gameframex.unity.event | 1.4.0 | https://github.com/gameframex/com.gameframex.unity.event.git#1.4.0 |

基础包按其 package.json 自动引入 LitJson、ReadAssets、UniTask 及 Unity 内置模块。使用提供的 scoped registry 示例解析 GF 依赖。

包可与 MoveComponent 共存：包名 `com.gameframex.unity.adcomponent` 与 `com.gameframex.unity.movecomponent` 不同。单仓库分支安装必须选择 `#ADComponent`；main 仍为节点编辑器和视频驱动模块。

TopOn 为可选示例，不写入核心依赖。ThinkupTpnPlugin 原生网络依赖由厂商 SDK Manager 与 EDM4U 解析；SDK 二进制、平台桥接、Editor 安装器均放在 Samples~ 下。完整 SDK 以当前项目副本为来源，没有额外编造 UPM 包名。
