using System;
using GameFrameX.Runtime;
namespace GameFrameX.Ad
{
    public sealed class ADRequestSuccessEventArgs : GameFrameworkEventArgs
    {
        /// <summary>
        /// 获取广告请求任务的序列编号
        /// </summary>
        public int SerialID { get; private set; }

        /// <summary>
        /// 获取广告请求任务的单元id
        /// </summary>
        public string PlacementID { get; private set; }

        /// <summary>
        /// 广告类型
        /// </summary>
        public ADFormat ADFormat { get; private set; }

        /// <summary>
        /// 获取用户自定义数据
        /// </summary>
        public object UserData { get; private set; }
        
        /// <summary>
        /// 加载成功的广告对象
        /// </summary>
        public object ADObject { get; private set; }

        /// <summary>
        /// 标签
        /// </summary>
        public string Tag { get; private set; }

        public static ADRequestSuccessEventArgs Create(int serialID, string placementID, ADFormat adFormat,
            object adObject, string tag, object userData)
        {
            ADRequestSuccessEventArgs arg = ReferencePool.Acquire<ADRequestSuccessEventArgs>();
            arg.SerialID = serialID;
            arg.PlacementID = placementID;
            arg.ADFormat = adFormat;
            arg.ADObject = adObject;
            arg.Tag = tag;
            arg.UserData = userData;
            //arg.Ecpm = ecpm;
            return arg;
        }

        public override void Clear()
        {
            SerialID = 0;
            PlacementID = string.Empty;
            UserData = null;
            ADObject = null;
            Tag = string.Empty;
        }
    }
}