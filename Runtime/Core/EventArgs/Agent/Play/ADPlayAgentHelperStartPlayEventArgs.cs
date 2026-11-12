using System;
using GameFrameX.Runtime;
namespace GameFrameX.Ad
{
    /// <summary>
    /// 开始播放广告的事件参数
    /// </summary>
    public sealed class ADPlayAgentHelperStartPlayEventArgs : GameFrameworkEventArgs

    {
        public string PlacementID { get; private set; }

        /// <summary>
        /// 场景id
        /// </summary>
        public string PosID { get; private set; }

        public static ADPlayAgentHelperStartPlayEventArgs Create(string placementID, string posID)
        {
            ADPlayAgentHelperStartPlayEventArgs arg = ReferencePool.Acquire<ADPlayAgentHelperStartPlayEventArgs>();
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