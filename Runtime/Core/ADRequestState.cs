using System;
using GameFrameX.Runtime;
namespace GameFrameX.Ad
{
    internal enum ADRequestState
    {
        /// <summary>
        /// 准备请求
        /// </summary>
        Todo = 0,

        /// <summary>
        /// 请求中
        /// </summary>
        Doing = 1,

        /// <summary>
        /// 请求完成
        /// </summary>
        Done = 2,

        /// <summary>
        /// 请求错误
        /// </summary>
        Error = 3,
    }
}