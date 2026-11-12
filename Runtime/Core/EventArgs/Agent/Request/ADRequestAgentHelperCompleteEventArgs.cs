using System;
using GameFrameX.Runtime;
namespace GameFrameX.Ad
{
    public sealed class ADRequestAgentHelperCompleteEventArgs : GameFrameworkEventArgs
    {
        /// <summary>
        /// 获取广告位 ID，用于回调时匹配对应的请求任务
        /// </summary>
        public string PlacementID { get; private set; }

        /// <summary>
        /// 广告物体对象，只有banner和原生广告有
        /// </summary>
        public object ADObject { get; private set; }

        public static ADRequestAgentHelperCompleteEventArgs Create(string placementID,
            object adObject)
        {
            ADRequestAgentHelperCompleteEventArgs arg = ReferencePool.Acquire<ADRequestAgentHelperCompleteEventArgs>();
            arg.PlacementID = placementID;
            arg.ADObject = adObject;
            return arg;
        }

        public override void Clear()
        {
            PlacementID = string.Empty;
            ADObject = null;
        }
    }
}