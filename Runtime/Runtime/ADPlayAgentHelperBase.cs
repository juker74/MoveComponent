using System;
using GameFrameX.Runtime;
using GameFrameX.Ad;
using UnityEngine;

namespace GameFrameX.Ad.Runtime
{
    public abstract class ADPlayAgentHelperBase : MonoBehaviour, IADPlayAgentHelper, IADCallbackDispatcher
    {
        private readonly ADNativeCallbacks callbacks = new ADNativeCallbacks();
        protected void Dispatch(Action callback) => callbacks.Enqueue(callback);
        public void DispatchCallbacks() => callbacks.Dispatch();
        protected void Publish<T>(EventHandler<T> handler, T args) where T : EventArgs, IReference
            => ADNativeCallbacks.Publish(this, handler, args);
        //调用播放触发
        protected EventHandler<ADPlayAgentHelperStartEventArgs> m_ADPlayAgentHelperStartEvent;

        //展示
        protected EventHandler<ADPlayAgentHelperShowEventArgs> m_ADPlayAgentHelperShowEvent;

        //播放
        protected EventHandler<ADPlayAgentHelperStartPlayEventArgs> m_ADPlayAgentStartPlayEvent;

        //点击
        protected EventHandler<ADPlayAgentHelperClickEventArgs> m_ADPlayAgentHelperClickEvent;

        //close,end
        protected EventHandler<ADPlayAgentHelperCompleteEventArgs> m_ADPlayAgentHelperCompleteEvent;

        //error
        protected EventHandler<ADPlayAgentHelperErrorEventArgs> m_ADPlayAgentHelperErrorEvent;

        /// <summary>
        /// 支持的广告类型
        /// </summary>
        public abstract ADFormat SupportedFormat { get; }


        /// <summary>
        /// AD 播放代理辅助器开始事件
        /// </summary>
        public event EventHandler<ADPlayAgentHelperStartEventArgs> ADPlayAgentHelperStartEvent
        {
            add { m_ADPlayAgentHelperStartEvent += value; }
            remove { m_ADPlayAgentHelperStartEvent -= value; }
        }

        public event EventHandler<ADPlayAgentHelperStartPlayEventArgs> ADPlayAgentHelperStartPlay
        {
            add { m_ADPlayAgentStartPlayEvent += value; }
            remove { m_ADPlayAgentStartPlayEvent -= value; }
        }

        /// <summary>
        /// AD 广告点击事件
        /// </summary>
        public event EventHandler<ADPlayAgentHelperClickEventArgs> ADPlayAgentHelperClick
        {
            add { m_ADPlayAgentHelperClickEvent += value; }
            remove { m_ADPlayAgentHelperClickEvent -= value; }
        }

        /// <summary>
        /// AD 广告展示事件
        /// </summary>
        public event EventHandler<ADPlayAgentHelperShowEventArgs> ADPlayAgentHelperShow
        {
            add { m_ADPlayAgentHelperShowEvent += value; }
            remove { m_ADPlayAgentHelperShowEvent -= value; }
        }

        /// <summary>
        /// AD 播放代理辅助器完成事件
        /// </summary>
        public event EventHandler<ADPlayAgentHelperCompleteEventArgs> ADPlayAgentHelperComplete
        {
            add { m_ADPlayAgentHelperCompleteEvent += value; }
            remove { m_ADPlayAgentHelperCompleteEvent -= value; }
        }

        /// <summary>
        /// AD 播放代理辅助器错误事件
        /// </summary>
        public event EventHandler<ADPlayAgentHelperErrorEventArgs> ADPlayAgentHelperError
        {
            add { m_ADPlayAgentHelperErrorEvent += value; }
            remove { m_ADPlayAgentHelperErrorEvent -= value; }
        }

        /// <summary>
        /// 初始化AD 播放代理辅助器
        /// </summary>
        public virtual void Initialize()
        {
            callbacks.Initialize();
        }

        /// <summary>
        /// 关闭并清理 AD 播放代理辅助器
        /// </summary>
        public virtual void Shutdown()
        {
            callbacks.Shutdown();
        }

        /// <summary>
        /// 通过AD 请求代理辅助器播放AD
        /// </summary>
        /// <param name="placementID"></param>
        /// <param name="posID"></param>
        /// <param name="userData"></param>
        public abstract void Play(string placementID, string posID, object userData);

        /// <summary>
        /// 检查广告位状态
        /// </summary>
        /// <param name="placementID"></param>
        /// <returns></returns>
        /// <exception cref="NotImplementedException"></exception>
        public abstract string CheckAdStatus(string placementID);

        public abstract void Reset();
    }
}
