using System;
using GameFrameX.Runtime;
using GameFrameX.Ad;
using GameFrameX.Event.Runtime;

namespace GameFrameX.Ad.Runtime
{
    public class ADRequestFailureEventArgs : GameEventArgs
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

        public static ADRequestFailureEventArgs Create(GameFrameX.Ad.ADRequestFailureEventArgs e)
        {
            ADRequestFailureEventArgs adRequestFailureEventArgs = ReferencePool.Acquire<ADRequestFailureEventArgs>();
            adRequestFailureEventArgs.SerialID = e.SerialID;
            adRequestFailureEventArgs.PlacementID = e.PlacementID;
            adRequestFailureEventArgs.ADFormat = e.ADFormat;
            adRequestFailureEventArgs.UserData = e.UserData;
            adRequestFailureEventArgs.ErrorCode = e.ErrorCode;
            adRequestFailureEventArgs.ErrorMessage = e.ErrorMessage;
            adRequestFailureEventArgs.Tag = e.Tag;
            return adRequestFailureEventArgs;
        }

        public override void Clear()
        {
            SerialID = 0;
            PlacementID = string.Empty;
            UserData = null;
            ErrorCode = string.Empty;
            ErrorMessage = string.Empty;
            Tag = string.Empty;
        }

        public override string Id => EventId;

        /// <summary>
        /// AD 请求失败事件编号。
        /// </summary>
        public const string EventId = "GameFrameX.Ad.ADRequestFailureEventArgs";
    }
}