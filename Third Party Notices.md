# 来源与许可

- 广告模块来自 InteractiveMovieGame 的 `Assets/Scripts/Framework/Advertising`。保留现有命名空间与脚本 GUID。项目业务层（Hotfix、存档、Firebase、Singular、平台广告位映射）不在本包内。
- GameFrameX：Blank / GameFrameX，https://github.com/GameFrameX 。框架作为依赖解析，原许可由各依赖包保留。
- TopOn / ThinkupTpnPlugin：随项目提供的第三方 SDK（Integration Manager 2.2.0），仅位于 `Samples~/TopOn/ThinkupTpnPlugin`，原版权归其权利人所有；本仓库不为第三方 SDK 重新授权。保留原有源码、资源、二进制和署名。
- 示例为可选内容，核心程序集没有 AnyThink / TopOn 引用。

根目录保留原 MoveComponent 仓库 Apache-2.0 许可文本（LICENSE.md）；该文本不替代第三方 SDK 自身许可。

SDK 副本仅作两项包化调整：清空 plugin_setting_data.json 的 AdMob 账号字段；移除 Integration Manager 未使用的 UnityEngine.UI using，避免额外 UGUI 依赖。其余内容保留项目副本。
