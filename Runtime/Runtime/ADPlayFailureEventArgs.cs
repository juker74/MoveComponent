using System;
using GameFrameX.Runtime;
using GameFrameX.Ad;
using GameFrameX.Event.Runtime;

namespace GameFrameX.Ad.Runtime
{
    public class ADPlayFailureEventArgs : GameEventArgs
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

        public string ErrorCode { get; private set; }

        public string ErrorMessage { get; private set; }

        public static ADPlayFailureEventArgs Create(GameFrameX.Ad.ADPlayFailureEventArgs e)
        {
            ADPlayFailureEventArgs adPlayFailureEventArgs = ReferencePool.Acquire<ADPlayFailureEventArgs>();
            adPlayFailureEventArgs.SerialID = e.SerialID;
            adPlayFailureEventArgs.PlacementID = e.PlacementID;
            adPlayFailureEventArgs.PodID = e.PosID;
            adPlayFailureEventArgs.AdFormat = e.ADFormat;
            adPlayFailureEventArgs.Tag = e.Tag;
            adPlayFailureEventArgs.UserData = e.UserData;
            adPlayFailureEventArgs.ErrorCode = e.ErrorCode;
            adPlayFailureEventArgs.ErrorMessage = e.ErrorMessage;
            return adPlayFailureEventArgs;
        }

        public override void Clear()
        {
            SerialID = 0;
            PlacementID = string.Empty;
            PodID = string.Empty;
            AdFormat = default;
            UserData = null;
            Tag = string.Empty;
            ErrorCode = string.Empty;
            ErrorMessage = string.Empty;
        }

        public override string Id => EventId;

        /// <summary>
        /// AD 播放失败事件编号。
        /// </summary>
        public const string EventId = "GameFrameX.Ad.ADPlayFailureEventArgs";
    }
}
