using System;
using GameFrameX.Runtime;
namespace GameFrameX.Ad
{
    public sealed class ADPlayShowEventArgs : GameFrameworkEventArgs
    {
        /// <summary>
        /// 场景id
        /// </summary>
        public string PosID { get; private set; }

        /// <summary>
        /// 获取广告请求任务的序列编号
        /// </summary>
        public int SerialID { get; private set; }

        /// <summary>
        /// 获取广告请求任务的单元id
        /// </summary>
        public string PlacementID { get; private set; }

        /// <summary>
        /// 获取用户自定义数据
        /// </summary>
        public object UserData { get; private set; }

        /// <summary>
        /// 广告类型
        /// </summary>
        public ADFormat ADFormat { get; private set; }

        /// <summary>
        /// 标签
        /// </summary>
        public string Tag { get; private set; }

        public static ADPlayShowEventArgs Create(int serialId, string placementID, ADFormat adFormat, string podid,
            string tag, object userData)
        {
            ADPlayShowEventArgs arg = ReferencePool.Acquire<ADPlayShowEventArgs>();
            arg.SerialID = serialId;
            arg.PlacementID = placementID;
            arg.ADFormat = adFormat;
            arg.PosID = podid;
            arg.Tag = tag;
            arg.UserData = userData;
            return arg;
        }

        public override void Clear()
        {
            SerialID = 0;
            PlacementID = string.Empty;
            ADFormat = default;
            PosID = string.Empty;
            Tag = string.Empty;
            UserData = null;
        }
    }
}