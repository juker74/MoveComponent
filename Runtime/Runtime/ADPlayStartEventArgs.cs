using System;
using GameFrameX.Runtime;
using GameFrameX.Ad;
using GameFrameX.Event.Runtime;

namespace GameFrameX.Ad.Runtime
{
    public class ADPlayStartEventArgs : GameEventArgs
    {
        /// <summary>
        /// 获取广告播放任务的序列编号
        /// </summary>
        public int SerialID { get; private set; }

        /// <summary>
        /// 获取广告位 ID
        /// </summary>
        public string PlacementID { get; private set; }

        /// <summary>
        /// 场景 ID
        /// </summary>
        public string PodID { get; private set; }

        /// <summary>
        /// 广告类型
        /// </summary>
        public ADFormat AdFormat { get; private set; }

        /// <summary>
        /// 获取用户自定义数据
        /// </summary>
        public object UserData { get; private set; }

        /// <summary>
        /// 标签
        /// </summary>
        public string Tag { get; private set; }

        public static ADPlayStartEventArgs Create(GameFrameX.Ad.ADPlayStartEventArgs e)
        {
            ADPlayStartEventArgs adPlayStartEventArgs = ReferencePool.Acquire<ADPlayStartEventArgs>();
            adPlayStartEventArgs.SerialID = e.SerialID;
            adPlayStartEventArgs.PlacementID = e.PlacementID;
            adPlayStartEventArgs.PodID = e.PosID;
            adPlayStartEventArgs.AdFormat = e.ADFormat;
            adPlayStartEventArgs.Tag = e.Tag;
            adPlayStartEventArgs.UserData = e.UserData;
            return adPlayStartEventArgs;
        }

        public override void Clear()
        {
            SerialID = 0;
            PlacementID = string.Empty;
            PodID = string.Empty;
            AdFormat = default;
            UserData = null;
            Tag = string.Empty;
        }

        public override string Id => EventId;

        /// <summary>
        /// AD 播放开始事件编号。
        /// </summary>
        public const string EventId = "GameFrameX.Ad.ADPlayStartEventArgs";
    }
}
