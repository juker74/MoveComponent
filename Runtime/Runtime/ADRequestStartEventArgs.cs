using System;
using GameFrameX.Runtime;
using GameFrameX.Ad;
using GameFrameX.Event.Runtime;

namespace GameFrameX.Ad.Runtime
{
    public class ADRequestStartEventArgs : GameEventArgs
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
        public ADFormat AdFormat { get; private set; }

        /// <summary>
        /// 标签
        /// </summary>
        public string Tag { get; private set; }

        public static ADRequestStartEventArgs Create(GameFrameX.Ad.ADRequestStartEventArgs e)
        {
            ADRequestStartEventArgs adRequestStartEventArgs = ReferencePool.Acquire<ADRequestStartEventArgs>();
            adRequestStartEventArgs.SerialID = e.SerialID;
            adRequestStartEventArgs.PlacementID = e.PlacementID;
            adRequestStartEventArgs.AdFormat = e.AdFormat;
            adRequestStartEventArgs.Tag = e.Tag;
            adRequestStartEventArgs.UserData = e.UserData;
            return adRequestStartEventArgs;
        }

        public override void Clear()
        {
            SerialID = 0;
            PlacementID = string.Empty;
            UserData = null;
            Tag = string.Empty;
        }

        public override string Id => EventId;

        /// <summary>
        /// AD 请求开始事件编号。
        /// </summary>
        public const string EventId = "GameFrameX.Ad.ADRequestStartEventArgs";
    }
}