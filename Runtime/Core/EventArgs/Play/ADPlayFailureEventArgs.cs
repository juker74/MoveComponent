using System;
using GameFrameX.Runtime;
namespace GameFrameX.Ad
{
    public sealed class ADPlayFailureEventArgs : GameFrameworkEventArgs
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

        /// <summary>
        /// 错误码
        /// </summary>
        public string ErrorCode { get; private set; }

        /// <summary>
        /// 错误信息
        /// </summary>
        public string ErrorMessage { get; private set; }

        public static ADPlayFailureEventArgs Create(int serialId, string placementID, ADFormat adFormat, string podid,
            string tag, object userData, string errorCode, string errorMessage)
        {
            ADPlayFailureEventArgs arg = ReferencePool.Acquire<ADPlayFailureEventArgs>();
            arg.SerialID = serialId;
            arg.PlacementID = placementID;
            arg.ADFormat = adFormat;
            arg.PosID = podid;
            arg.Tag = tag;
            arg.UserData = userData;
            arg.ErrorCode = errorCode;
            arg.ErrorMessage = errorMessage;
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
            ErrorCode = string.Empty;
            ErrorMessage = string.Empty;
        }
    }
}