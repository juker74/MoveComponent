using System;
using GameFrameX.Runtime;
namespace GameFrameX.Ad
{
    public sealed class ADRequestFailureEventArgs : GameFrameworkEventArgs
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
        /// 获取用户自定义数据
        /// </summary>
        public object UserData { get; private set; }

        /// <summary>
        /// 广告类型
        /// </summary>
        public ADFormat ADFormat { get; private set; }

        /// <summary>
        /// 错误信息
        /// </summary>
        public string ErrorMessage { get; private set; }

        /// <summary>
        /// 错误码
        /// </summary>
        public string ErrorCode { get; private set; }

        /// <summary>
        /// 标签
        /// </summary>
        public string Tag { get; private set; }

        public static ADRequestFailureEventArgs Create(int serialID, string placementID, ADFormat adFormat,
            string errorCode, string errorMessage, string tag, object userData)
        {
            ADRequestFailureEventArgs arg = ReferencePool.Acquire<ADRequestFailureEventArgs>();
            arg.SerialID = serialID;
            arg.PlacementID = placementID;
            arg.ADFormat = adFormat;
            arg.ErrorCode = errorCode;
            arg.ErrorMessage = errorMessage;
            arg.Tag = tag;
            arg.UserData = userData;
            return arg;
        }

        public override void Clear()
        {
            SerialID = 0;
            PlacementID = string.Empty;
            ADFormat = default;
            ErrorCode = string.Empty;
            ErrorMessage = string.Empty;
            Tag = string.Empty;
            UserData = null;
        }
    }
}