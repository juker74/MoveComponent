using System;
using GameFrameX.Runtime;
namespace GameFrameX.Ad
{
    internal class ADRequestTask : TaskBase
    {
        private static int s_Serial = 0;

        private ADRequestState m_States;

        //广告位id
        private string m_PlacementID;

        //广告类型
        private ADFormat m_ADFormat;
        private float m_TimeOut;
        private float m_ExpireSeconds;
        private bool m_ForceLoad;

        public ADRequestTask()
        {
            m_States = ADRequestState.Todo;
            m_TimeOut = 0f;
            m_ExpireSeconds = 0f;
            m_ForceLoad = false;
            m_PlacementID = string.Empty;
        }

        /// <summary>
        /// 获取或设置ad请求的状态
        /// </summary>
        public ADRequestState Status
        {
            get { return m_States; }
            set { m_States = value; }
        }

        /// <summary>
        /// 获取广告请求超时时长，以秒为单位
        /// </summary>
        public float TimeOut
        {
            get { return m_TimeOut; }
        }

        /// <summary>
        /// 获取广告加载成功后的过期时长，以秒为单位。
        /// </summary>
        public float ExpireSeconds
        {
            get { return m_ExpireSeconds; }
        }

        /// <summary>
        /// 获取是否强制调用 SDK 加载，跳过已有广告 ready 检查。
        /// </summary>
        public bool ForceLoad
        {
            get { return m_ForceLoad; }
        }

        /// <summary>
        /// 广告位id
        /// </summary>
        public string PlacementID
        {
            get { return m_PlacementID; }
        }

        /// <summary>
        /// 广告类型
        /// </summary>
        public ADFormat ADFormat
        {
            get { return m_ADFormat; }
        }

        /// <summary>
        /// 创建 AD 请求任务
        /// </summary>
        /// <param name="placementID">广告位ID</param>
        /// <param name="timeOut">超时时间</param>
        /// <param name="priority">权重</param>
        /// <param name="userData">用户自定义数据</param>
        /// <param name="tag"></param>
        /// <returns></returns>
        public static ADRequestTask Create(string placementID, ADFormat adFormat, float timeOut, float expireSeconds,
            bool forceLoad, string tag, int priority, object userData)
        {
            ADRequestTask adRequestTask = ReferencePool.Acquire<ADRequestTask>();
            adRequestTask.Initialize(s_Serial++, tag, priority, userData);
            adRequestTask.m_TimeOut = timeOut;
            adRequestTask.m_ExpireSeconds = expireSeconds;
            adRequestTask.m_ForceLoad = forceLoad;
            adRequestTask.m_PlacementID = placementID;
            adRequestTask.m_ADFormat = adFormat;
            return adRequestTask;
        }

        public override void Clear()
        {
            base.Clear();
            m_States = ADRequestState.Todo;
            m_PlacementID = string.Empty;
            m_ADFormat = default;
            m_TimeOut = 0;
            m_ExpireSeconds = 0;
            m_ForceLoad = false;
        }
    }
}