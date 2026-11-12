using System;
using GameFrameX.Runtime;
namespace GameFrameX.Ad
{
    public sealed class ADRequestAgentHelperErrorEventArgs : GameFrameworkEventArgs
    {
        /// <summary>
        /// 获取广告位 ID，用于回调时匹配对应的请求任务
        /// </summary>
        public string PlacementID { get; private set; }

        /// <summary>
        /// 错误信息
        /// </summary>
        private string m_ErrorMessage;

        public string ErrorMessage => m_ErrorMessage;

        /// <summary>
        /// 错误代码
        /// </summary>
        private string m_ErrorCode;

        public string ErrorCode => m_ErrorCode;

        /// <summary>
        /// 创建AD 请求代理辅助器错误事件
        /// </summary>
        /// <param name="placementID">广告位 ID</param>
        /// <param name="errorCode"></param>
        /// <param name="errorMessage"></param>
        /// <returns></returns>
        public static ADRequestAgentHelperErrorEventArgs Create(string placementID, string errorCode, string errorMessage)
        {
            ADRequestAgentHelperErrorEventArgs arg = ReferencePool.Acquire<ADRequestAgentHelperErrorEventArgs>();
            arg.PlacementID = placementID;
            arg.m_ErrorMessage = errorMessage;
            arg.m_ErrorCode = errorCode;
            return arg;
        }

        public override void Clear()
        {
            PlacementID = string.Empty;
            m_ErrorCode = string.Empty;
            m_ErrorMessage = string.Empty;
        }
    }
}