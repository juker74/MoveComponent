using System;
using GameFrameX.Runtime;

namespace GameFrameX.Ad
{
    public interface IADPlayAgentHelper
    {
        /// <summary>
        /// 支持的广告类型
        /// </summary>
        public ADFormat SupportedFormat { get; }

        /// <summary>
        /// AD 播放代理辅助器开始调用播放事件
        /// </summary>
        public event EventHandler<ADPlayAgentHelperStartEventArgs> ADPlayAgentHelperStartEvent;

        /// <summary>
        /// AD 播放代理辅助器开始展示事件
        /// </summary>
        public event EventHandler<ADPlayAgentHelperShowEventArgs> ADPlayAgentHelperShow;

        /// <summary>
        /// AD 播放代理辅助器开始播放事件
        /// </summary>
        public event EventHandler<ADPlayAgentHelperStartPlayEventArgs> ADPlayAgentHelperStartPlay;

        /// <summary>
        /// AD 播放代理辅助器点击事件
        /// </summary>
        public event EventHandler<ADPlayAgentHelperClickEventArgs> ADPlayAgentHelperClick;
        
        /// <summary>
        /// AD 播放代理辅助器完成事件,close,end
        /// </summary>
        public event EventHandler<ADPlayAgentHelperCompleteEventArgs> ADPlayAgentHelperComplete;

        /// <summary>
        /// AD 播放代理辅助器错误事件
        /// </summary>
        public event EventHandler<ADPlayAgentHelperErrorEventArgs> ADPlayAgentHelperError;

        /// <summary>
        /// 初始化 AD 请求代理辅助器。
        /// </summary>
        void Initialize();

        /// <summary>
        /// 关闭并清理 AD 请求代理辅助器。
        /// </summary>
        void Shutdown();

        /// <summary>
        /// 通过AD 播放代理辅助器发送AD 播放事件
        /// </summary>
        /// <param name="posID"></param>
        public void Play(string placementID, string posID, object userData);

        /// <summary>
        /// 检查广告位状态
        /// </summary>
        /// <param name="placementID">广告位 ID</param>
        /// <returns>广告状态 JSON 字符串</returns>
        string CheckAdStatus(string placementID);

        /// <summary>
        /// 重置当前 AD 请求辅助器状态。
        /// </summary>
        void Reset();
    }
}