# 验证记录

日期：2026-11-12；Unity 2022.3.62f3；独立工程 `/private/tmp/ADComponentValidation`。

- 仅安装核心包（未导入 TopOn 示例）：编译成功，核心 EditMode 回归 **20/20 通过**，结果 `/private/tmp/ad-core-results.xml`。
- 导入 TopOn 示例后：核心及 SDK 回归 **46/46 通过**，结果 `/private/tmp/ad-sample-results.xml`。
- SDK 安装菜单成功迁移到 `Assets/ThinkupTpnPlugin`；安装后包含真实场景 EnterPlayMode 的最终测试 **47/47 通过**，结果 `/private/tmp/ad-final-results.xml`。
- 真实示例场景包含 GF Base / Event / ADComponent，验证激励、插屏、开屏各自的请求与播放 Agent 初始化；场景无缺失脚本。
- 原广告模块 Core / Runtime / Editor / TopOn 的全部 C# 源码与项目源文件逐字节一致；只调整目录和程序集引用。源项目没有迁移或删除。
- `python3 Tools~/validate_package.py` 检查核心与 SDK 隔离、程序集唯一、485 个 GUID 唯一、元数据完整、场景引用和空账号配置。
- SDK 资源中原项目 AdMob App ID 已清空；示例 AppId、AppKey、广告位均为空。

验证覆盖模块生命周期、加载去重、加载与播放独立队列、重复关闭、迟到回调、取消任务、异常后任务池继续工作、加载过期、激励完成条件及原生回调主线程分发。

Editor 使用 SDK 模拟，未执行 Android / iOS 原生打包或真实广告请求。目标设备的依赖解析、账号、广告展示和奖励回调需由接入项目实机验证。

## 远端 Git 安装

已通过 `https://github.com/juker74/MoveComponent.git#ADComponent` 实际安装，packages-lock.json 确认为 git 来源，解析提交 `0a5431da3ea0b410ac5083eaea91b70bab4dc1b1`。核心与示例全套 **47/47 通过**，结果 `/private/tmp/ad-git-results.xml`。后续提交仅补充本验证记录。
