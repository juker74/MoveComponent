# TopOn Example

核心包不需要此示例。导入后先执行 Tools → GameFrameX → AD → Install Sample TopOn SDK，将 SDK 移到 Assets/ThinkupTpnPlugin（原厂工具固定路径），再使用 ThinkupTpnPlugin/SDK Manager 管理原生依赖。已有 SDK 时先处理重复副本。

打开 Scenes/TopOnExample.unity，在 ADComponent 的 TopOn Helper 配置各平台 AppId / AppKey，在 TopOnExample 填入广告位。示例中没有实际账号。Load / Play 分开发起，必须等加载成功后播放；Editor 模拟不能代替设备实测。

宿主已有 GF 组件时，将示例中的 AD 配置接到宿主，避免重复 Base / Event 生命周期组件。
