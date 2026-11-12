using System;
using GameFrameX.Runtime;
namespace GameFrameX.Ad
{
    public sealed class ADRequestExpiredEventArgs : GameFrameworkEventArgs
    {
        /// <summary>
        /// 获取广告位 ID。
        /// </summary>
        public string PlacementID { get; private set; }

        /// <summary>
        /// 广告类型。
        /// </summary>
        public ADFormat ADFormat { get; private set; }

        /// <summary>
        /// 标签。
        /// </summary>
        public string Tag { get; private set; }

        /// <summary>
        /// 获取用户自定义数据。
        /// </summary>
        public object UserData { get; private set; }

        public static ADRequestExpiredEventArgs Create(string placementID, ADFormat adFormat, string tag, object userData)
        {
            ADRequestExpiredEventArgs arg = ReferencePool.Acquire<ADRequestExpiredEventArgs>();
            arg.PlacementID = placementID;
            arg.ADFormat = adFormat;
            arg.Tag = tag;
            arg.UserData = userData;
            return arg;
        }

        public override void Clear()
        {
            PlacementID = string.Empty;
            ADFormat = default;
            Tag = string.Empty;
            UserData = null;
        }
    }
}
