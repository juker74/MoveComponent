using System;
using GameFrameX.Runtime;
namespace GameFrameX.Ad
{
    /// <summary>
    /// 辅助器点击广告事件
    /// </summary>
    public sealed class ADPlayAgentHelperClickEventArgs : GameFrameworkEventArgs
    {
        /// <summary>
        /// 获取广告位 ID，用于回调时匹配对应的请求任务
        /// </summary>
        public string PlacementID { get; private set; }

        /// <summary>
        /// 场景id
        /// </summary>
        public string PosID { get; private set; }

        public static ADPlayAgentHelperClickEventArgs Create(string placementID, string podID)
        {
            ADPlayAgentHelperClickEventArgs arg = ReferencePool.Acquire<ADPlayAgentHelperClickEventArgs>();
            arg.PlacementID = placementID;
            arg.PosID = podID;
            return arg;
        }

        public override void Clear()
        {
            PlacementID = string.Empty;
            PosID = string.Empty;
        }
    }
}