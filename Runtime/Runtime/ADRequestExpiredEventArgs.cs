using System;
using GameFrameX.Runtime;
using GameFrameX.Ad;
using GameFrameX.Event.Runtime;

namespace GameFrameX.Ad.Runtime
{
    public class ADRequestExpiredEventArgs : GameEventArgs
    {
        /// <summary>
        /// 获取广告位 ID。
        /// </summary>
        public string PlacementID { get; private set; }

        /// <summary>
        /// 广告类型。
        /// </summary>
        public ADFormat AdFormat { get; private set; }

        /// <summary>
        /// 标签。
        /// </summary>
        public string Tag { get; private set; }

        /// <summary>
        /// 获取用户自定义数据。
        /// </summary>
        public object UserData { get; private set; }

        public static ADRequestExpiredEventArgs Create(GameFrameX.Ad.ADRequestExpiredEventArgs e)
        {
            ADRequestExpiredEventArgs adRequestExpiredEventArgs = ReferencePool.Acquire<ADRequestExpiredEventArgs>();
            adRequestExpiredEventArgs.PlacementID = e.PlacementID;
            adRequestExpiredEventArgs.AdFormat = e.ADFormat;
            adRequestExpiredEventArgs.Tag = e.Tag;
            adRequestExpiredEventArgs.UserData = e.UserData;
            return adRequestExpiredEventArgs;
        }

        public override void Clear()
        {
            PlacementID = string.Empty;
            AdFormat = default;
            Tag = string.Empty;
            UserData = null;
        }

        public override string Id => EventId;

        /// <summary>
        /// AD 请求过期事件编号。
        /// </summary>
        public const string EventId = "GameFrameX.Ad.ADRequestExpiredEventArgs";
    }
}
