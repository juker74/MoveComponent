using System;
using GameFrameX.Runtime;

namespace GameFrameX.Ad
{
    /// <summary>
    /// 广告请求代理辅助器接口
    /// </summary>
    public interface IADRequestAgentHelper
    {
        /// <summary>
        /// 支持的广告类型
        /// </summary>
        public ADFormat SupportedFormat { get; }

        /// <summary>
        /// AD 请求代理辅助器完成事件
        /// </summary>
        event EventHandler<ADRequestAgentHelperCompleteEventArgs> ADRequestAgentHelperComplete;

        /// <summary>
        /// AD 请求代理辅助器错误事件
        /// </summary>
        event EventHandler<ADRequestAgentHelperErrorEventArgs> ADRequestAgentHelperError;

        /// <summary>
        /// 初始化 AD 请求代理辅助器。
        /// </summary>
        void Initialize();

        /// <summary>
        /// 关闭并清理 AD 请求代理辅助器。
        /// </summary>
        void Shutdown();

        /// <summary>
        /// 通过AD 请求代理辅助器发送AD 请求
        /// </summary>
        /// <param name="placementID"></param>
        /// <param name="forceLoad">是否强制调用 SDK 加载，跳过已有广告 ready 检查。</param>
        /// <param name="userData"></param>
        void Request(string placementID, bool forceLoad, object userData);

        /// <summary>
        /// 检查广告位状态
        /// </summary>
        /// <param name="placementID">广告位 ID</param>
        /// <returns>广告状态 JSON 字符串</returns>
        string CheckAdStatus(string placementID);

        /// <summary>
        /// 获取指定广告位的价值（各平台实现不同，如 TopOn 返回 publisher_revenue）。
        /// </summary>
        /// <param name="placementID">广告位 ID</param>
        /// <returns>广告价值（如 publisher_revenue），无数据时返回 0。</returns>
        double GetAdValue(string placementID);

        /// <summary>
        /// 判断指定广告位是否已加载就绪（各平台实现不同，如 TopOn 调用 hasAdReady）。
        /// </summary>
        /// <param name="placementID">广告位 ID</param>
        /// <returns>true 表示广告已就绪可播放</returns>
        bool IsAdReady(string placementID);

        /// <summary>
        /// 重置当前 AD 请求辅助器状态。
        /// </summary>
        void Reset();
    }
}