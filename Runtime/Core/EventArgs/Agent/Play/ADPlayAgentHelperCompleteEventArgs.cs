using System;
using GameFrameX.Runtime;
namespace GameFrameX.Ad
{
    /// <summary>
    /// 辅助器播放成功的事件
    /// </summary>
    public sealed class ADPlayAgentHelperCompleteEventArgs : GameFrameworkEventArgs
    {
        /// <summary>
        /// 获取广告位 ID，用于回调时匹配对应的请求任务
        /// </summary>
        public string PlacementID { get; private set; }

        /// <summary>
        /// 场景id
        /// </summary>
        public string PosID { get; private set; }


        public static ADPlayAgentHelperCompleteEventArgs Create(string placementID, string posID)
        {
            ADPlayAgentHelperCompleteEventArgs arg = ReferencePool.Acquire<ADPlayAgentHelperCompleteEventArgs>();
            arg.PlacementID = placementID;
            arg.PosID = posID;
            return arg;
        }

        public override void Clear()
        {
            PlacementID = string.Empty;
            PosID = string.Empty;
        }
    }
}