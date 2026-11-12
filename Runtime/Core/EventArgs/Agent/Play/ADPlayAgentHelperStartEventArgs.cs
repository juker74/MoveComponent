using System;
using GameFrameX.Runtime;
namespace GameFrameX.Ad
{
    /// <summary>
    /// 辅助器播放广告开始事件
    /// </summary>
    public sealed class ADPlayAgentHelperStartEventArgs : GameFrameworkEventArgs
    {
        /// <summary>
        /// 获取广告位 ID，用于回调时匹配对应的请求任务
        /// </summary>
        public string PlacementID { get; private set; }

        /// <summary>
        /// 场景id
        /// </summary>
        public string PosID { get; private set; }

        public static ADPlayAgentHelperStartEventArgs Create(string placementID, string podID)
        {
            ADPlayAgentHelperStartEventArgs arg = ReferencePool.Acquire<ADPlayAgentHelperStartEventArgs>();
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