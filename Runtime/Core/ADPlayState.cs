using System;
using GameFrameX.Runtime;
namespace GameFrameX.Ad
{
    internal enum ADPlayState
    {
        /// <summary>
        /// 准备播放
        /// </summary>
        Todo = 0,

        /// <summary>
        /// 正在执行展示
        /// </summary>
        Doing = 1,

        /// <summary>
        /// 正在展示
        /// </summary>
        Showing = 2,

        /// <summary>
        /// 展示完成
        /// </summary>
        Done = 3,

        /// <summary>
        /// 请求错误
        /// </summary>
        Error = 4,
    }
}