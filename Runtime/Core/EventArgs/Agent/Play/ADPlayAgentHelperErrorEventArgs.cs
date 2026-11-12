using System;
using GameFrameX.Runtime;
namespace GameFrameX.Ad
{
    /// <summary>
    /// 辅助器播放广告失败事件
    /// </summary>
    public sealed class ADPlayAgentHelperErrorEventArgs : GameFrameworkEventArgs
    {
        /// <summary>
        /// 获取广告位 ID，用于回调时匹配对应的请求任务
        /// </summary>
        public string PlacementID { get; private set; }

        /// <summary>
        /// 错误信息
        /// </summary>
        public string Message { get; private set; }

        /// <summary>
        /// 错误代码
        /// </summary>
        public string ErrorCode { get; private set; }

        /// <summary>
        /// 场景id
        /// </summary>
        public string PosID { get; private set; }

        public static ADPlayAgentHelperErrorEventArgs Create(string placementID, string podID, string message,
            string code)
        {
            ADPlayAgentHelperErrorEventArgs arg = ReferencePool.Acquire<ADPlayAgentHelperErrorEventArgs>();
            arg.PlacementID = placementID;
            arg.ErrorCode = code;
            arg.Message = message;
            arg.PosID = podID;
            return arg;
        }

        public override void Clear()
        {
            PlacementID = string.Empty;
            Message = string.Empty;
            ErrorCode = string.Empty;
            PosID = string.Empty;
        }
    }
}