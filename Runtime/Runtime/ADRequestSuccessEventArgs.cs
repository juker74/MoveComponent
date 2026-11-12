using System;
using GameFrameX.Runtime;
using GameFrameX.Ad;
using GameFrameX.Event.Runtime;

namespace GameFrameX.Ad.Runtime
{
    public class ADRequestSuccessEventArgs : GameEventArgs
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

        public static ADRequestSuccessEventArgs Create(GameFrameX.Ad.ADRequestSuccessEventArgs e)
        {
            ADRequestSuccessEventArgs adRequestSuccessEventArgs = ReferencePool.Acquire<ADRequestSuccessEventArgs>();
            adRequestSuccessEventArgs.SerialID = e.SerialID;
            adRequestSuccessEventArgs.PlacementID = e.PlacementID;
            adRequestSuccessEventArgs.ADFormat = e.ADFormat;
            adRequestSuccessEventArgs.UserData = e.UserData;
            adRequestSuccessEventArgs.ADObject = e.ADObject;
            adRequestSuccessEventArgs.Tag = e.Tag;
            return adRequestSuccessEventArgs;
        }

        public override void Clear()
        {
            SerialID = 0;
            PlacementID = string.Empty;
            UserData = null;
            ADObject = null;
            Tag = string.Empty;
        }

        public override string Id => EventId;

        /// <summary>
        /// AD 请求成功事件编号。
        /// </summary>
        public const string EventId = "GameFrameX.Ad.ADRequestSuccessEventArgs";
    }
}