using System;
using GameFrameX.Runtime;
using GameFrameX.Ad;
using GameFrameX.Event.Runtime;

namespace GameFrameX.Ad.Runtime
{
    public class ADPlaySuccessEventArgs : GameEventArgs
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

        public static ADPlaySuccessEventArgs Create(GameFrameX.Ad.ADPlaySuccessEventArgs e)
        {
            ADPlaySuccessEventArgs adPlaySuccessEventArgs = ReferencePool.Acquire<ADPlaySuccessEventArgs>();
            adPlaySuccessEventArgs.SerialID = e.SerialID;
            adPlaySuccessEventArgs.PlacementID = e.PlacementID;
            adPlaySuccessEventArgs.PodID = e.PosID;
            adPlaySuccessEventArgs.AdFormat = e.ADFormat;
            adPlaySuccessEventArgs.Tag = e.Tag;
            adPlaySuccessEventArgs.UserData = e.UserData;
            return adPlaySuccessEventArgs;
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
        /// AD 播放成功事件编号。
        /// </summary>
        public const string EventId = "GameFrameX.Ad.ADPlaySuccessEventArgs";
    }
}
