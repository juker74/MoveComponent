using System;
using GameFrameX.Runtime;
using GameFrameX.Ad;
using UnityEngine;

namespace GameFrameX.Ad.Runtime
{
    public abstract class ADRequestAgentHelperBase : MonoBehaviour, IADRequestAgentHelper, IADCallbackDispatcher
    {
        private readonly ADNativeCallbacks callbacks = new ADNativeCallbacks();
        protected void Dispatch(Action callback) => callbacks.Enqueue(callback);
        public void DispatchCallbacks() => callbacks.Dispatch();
        protected void Publish<T>(EventHandler<T> handler, T args) where T : EventArgs, IReference
            => ADNativeCallbacks.Publish(this, handler, args);
        /// <summary>
        /// 支持的广告类型
        /// </summary>
        public abstract ADFormat SupportedFormat { get; }

        protected EventHandler<ADRequestAgentHelperCompleteEventArgs> m_ADRequestAgentHelperComplete;
        protected EventHandler<ADRequestAgentHelperErrorEventArgs> m_ADRequestAgentHelperError;

        /// <summary>
        /// AD 请求代理辅助器完成事件
        /// </summary>
        public event EventHandler<ADRequestAgentHelperCompleteEventArgs> ADRequestAgentHelperComplete
        {
            add { m_ADRequestAgentHelperComplete += value; }
            remove { m_ADRequestAgentHelperComplete -= value; }
        }

        /// <summary>
        /// AD 请求代理辅助器错误事件
        /// </summary>
        public event EventHandler<ADRequestAgentHelperErrorEventArgs> ADRequestAgentHelperError
        {
            add { m_ADRequestAgentHelperError += value; }
            remove { m_ADRequestAgentHelperError -= value; }
        }

        /// <summary>
        /// 初始化 AD 请求代理辅助器。
        /// </summary>
        public virtual void Initialize()
        {
            callbacks.Initialize();
        }

        /// <summary>
        /// 关闭并清理 AD 请求代理辅助器。
        /// </summary>
        public virtual void Shutdown()
        {
            callbacks.Shutdown();
        }

        /// <summary>
        /// 通过 AD 请求代理辅助器加载AD
        /// </summary>
        /// <param name="placementID"></param>
        /// <param name="forceLoad">是否强制调用 SDK 加载，跳过已有广告 ready 检查。</param>
        /// <param name="userData"></param>
        public abstract void Request(string placementID, bool forceLoad, object userData);

        /// <summary>
        /// 检查广告位状态
        /// </summary>
        /// <param name="placementID">广告位 ID</param>
        /// <returns>广告状态 JSON 字符串</returns>
        public abstract string CheckAdStatus(string placementID);

        /// <summary>
        /// 获取指定广告位的价值。
        /// </summary>
        /// <param name="placementID">广告位 ID</param>
        /// <returns>广告价值（如 publisher_revenue），无数据时返回 0。</returns>
        public abstract double GetAdValue(string placementID);

        /// <summary>
        /// 判断指定广告位是否已加载就绪。
        /// </summary>
        /// <param name="placementID">广告位 ID</param>
        /// <returns>true 表示广告已就绪可播放</returns>
        public abstract bool IsAdReady(string placementID);

        public abstract void Reset();
    }
}
