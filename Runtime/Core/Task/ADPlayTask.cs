using System;
using GameFrameX.Runtime;
namespace GameFrameX.Ad
{
    internal class ADPlayTask : TaskBase
    {
        private static int s_Serial = 0;

        private ADPlayState _mStateses;

        //广告位id
        private string m_PlacementID;

        //广告类型
        private ADFormat m_ADFormat;

        // 场景id
        private string m_PosID;

        private float m_TimeOut;

        public ADPlayTask()
        {
            _mStateses = ADPlayState.Todo;
            m_TimeOut = 0f;
            m_PlacementID = string.Empty;
            m_PosID = string.Empty;
        }

        /// <summary>
        /// 场景id
        /// </summary>
        public string PosID
        {
            get { return m_PosID; }
        }

        /// <summary>
        /// 广告类型
        /// </summary>
        public ADFormat ADFormat
        {
            get { return m_ADFormat; }
        }

        /// <summary>
        /// 广告位id
        /// </summary>
        public string PlacementID
        {
            get { return m_PlacementID; }
        }

        /// <summary>
        /// 获取广告请求超时时长，以秒为单位
        /// </summary>
        public float TimeOut
        {
            get { return m_TimeOut; }
        }

        public ADPlayState States
        {
            get { return _mStateses; }
            set { _mStateses = value; }
        }

        /// <summary>
        /// 创建AD 播放请求
        /// </summary>
        /// <param name="placementID"></param>
        /// <param name="podID"></param>
        /// <param name="adFormat"></param>
        /// <param name="timeOut"></param>
        /// <param name="tag"></param>
        /// <param name="priority"></param>
        /// <param name="userData"></param>
        /// <returns></returns>
        public static ADPlayTask Create(string placementID, string podID, ADFormat adFormat, float timeOut, string tag,
            int priority, object userData)
        {
            ADPlayTask adPlayTask = ReferencePool.Acquire<ADPlayTask>();
            adPlayTask.Initialize(s_Serial++, tag, priority, userData);
            adPlayTask.m_TimeOut = timeOut;
            adPlayTask.m_PlacementID = placementID;
            adPlayTask.m_PosID = podID;
            adPlayTask.m_ADFormat = adFormat;
            return adPlayTask;
        }

        public override void Clear()
        {
            base.Clear();
            _mStateses = ADPlayState.Todo;
            m_PosID = string.Empty;
            m_PlacementID = string.Empty;
            m_ADFormat = default;
            m_TimeOut = 0;
        }
    }
}