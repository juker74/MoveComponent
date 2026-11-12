using System;
using GameFrameX.Runtime;
namespace GameFrameX.Ad
{
    /// <summary>
    /// 辅助器展示广告事件
    /// </summary>
    public sealed class ADPlayAgentHelperShowEventArgs : GameFrameworkEventArgs
    {
        public string PlacementID { get; private set; }

        /// <summary>
        /// 场景id
        /// </summary>
        public string PosID { get; private set; }

        public static ADPlayAgentHelperShowEventArgs Create(string placementID, string podID)
        {
            ADPlayAgentHelperShowEventArgs arg = ReferencePool.Acquire<ADPlayAgentHelperShowEventArgs>();
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