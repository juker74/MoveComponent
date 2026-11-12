using System;
using GameFrameX.Runtime;
namespace GameFrameX.Ad
{
    public sealed class ADRequestStartEventArgs : GameFrameworkEventArgs
    {
        public ADRequestStartEventArgs()
        {
            SerialID = 0;
            PlacementID = string.Empty;
            UserData = null;
        }

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
        public ADFormat AdFormat { get; private set; }

        /// <summary>
        /// 标签
        /// </summary>
        public string Tag { get; private set; }

        public static ADRequestStartEventArgs Create(int serialID, string placementID, ADFormat adFormat, string tag,
            object userData)
        {
            ADRequestStartEventArgs args = ReferencePool.Acquire<ADRequestStartEventArgs>();
            args.SerialID = serialID;
            args.PlacementID = placementID;
            args.AdFormat = adFormat;
            args.Tag = tag;
            args.UserData = userData;
            return args;
        }

        public override void Clear()
        {
            SerialID = 0;
            PlacementID = string.Empty;
            UserData = null;
            Tag = string.Empty;
        }
    }
}