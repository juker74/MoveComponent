using System;
using GameFrameX.Runtime;

namespace GameFrameX.Ad
{
    public interface IADHelper
    {
        /// <summary>
        /// 辅助器名字，一个平台对应一个辅助器
        /// </summary>
        public string Name { get; }

        /// <summary>
        /// 初始化
        /// </summary>
        /// <param name="userData"></param>
        public void Init(object userData);

        /// <summary>
        /// 获取或设置 AD 请求超时时长，以秒为单位
        /// </summary>
        float LoadTimeOut { get; set; }

        /// <summary>
        /// AD 请求开始事件
        /// </summary>
        event EventHandler<ADRequestStartEventArgs> ADRequestStart;

        /// <summary>
        /// AD 请求成功事件
        /// </summary>
        event EventHandler<ADRequestSuccessEventArgs> ADRequestSuccess;

        /// <summary>
        /// AD 请求失败事件
        /// </summary>
        event EventHandler<ADRequestFailureEventArgs> ADRequestFailure;

        /// <summary>
        /// AD 请求过期事件
        /// </summary>
        event EventHandler<ADRequestExpiredEventArgs> ADRequestExpired;

        /// <summary>
        /// AD 播放开始事件
        /// </summary>
        event EventHandler<ADPlayStartEventArgs> ADPlayStart;

        /// <summary>
        /// AD 播放展示事件
        /// </summary>
        event EventHandler<ADPlayShowEventArgs> ADPlayShow;

        /// <summary>
        /// AD 播放开始播放事件
        /// </summary>
        event EventHandler<ADPlayStartPlayEventArgs> ADPlayStartPlay;

        /// <summary>
        /// AD 广告点击事件
        /// </summary>
        event EventHandler<ADPlayClickEventArgs> ADPlayClick;

        /// <summary>
        /// AD 播放成功事件
        /// </summary>
        event EventHandler<ADPlaySuccessEventArgs> ADPlaySuccess;

        /// <summary>
        /// AD 播放失败事件
        /// </summary>
        event EventHandler<ADPlayFailureEventArgs> ADPlayFailure;


        /// <summary>
        /// 增加AD 请求代理辅助器
        /// </summary>
        /// <param name="adRequestAgentHelper"></param>
        void AddADRequestAgentHelper(IADRequestAgentHelper adRequestAgentHelper);

        /// <summary>
        /// 增加AD 播放代理辅助器
        /// </summary>
        /// <param name="adPlayAgentHelper"></param>
        void AddADPlayAgentHelper(IADPlayAgentHelper adPlayAgentHelper);

        /// <summary>
        /// 增加广告加载任务
        /// </summary>
        /// <param name="placementID">广告位id</param>
        /// <param name="tag">标签</param>
        /// <param name="priority">权重</param>
        /// <param name="userData">用户自定义数据</param>
        /// <returns></returns>
        int AddADRequest(string placementID, ADFormat adFormat, string tag, int priority, float expireSeconds, object userData);

        /// <summary>
        /// 增加广告播放任务
        /// </summary>
        /// <param name="placementID"></param>
        /// <param name="adFormat"></param>
        /// <param name="podid"></param>
        /// <param name="tag"></param>
        /// <param name="priority"></param>
        /// <param name="userData"></param>
        /// <returns></returns>
        int PlayAD(string placementID, ADFormat adFormat, string podid, string tag, int priority, object userData);

        /// <summary>
        /// 取消指定序列号的加载任务
        /// </summary>
        public bool RemoveADRequest(int serialId);

        /// <summary>
        /// 取消指定序列号的播放任务
        /// </summary>
        /// <param name="serialId"></param>
        /// <returns></returns>
        public bool RemoveADPlayRequest(int serialId);

        /// <summary>
        /// 检查指定广告位的请求任务状态
        /// </summary>
        /// <param name="placementID">广告位 ID</param>
        /// <param name="adFormat">广告类型</param>
        /// <returns>请求任务信息，若不存在该广告位的任务则返回无效 TaskInfo。</returns>
        TaskInfo CheckAdStatus(string placementID, ADFormat adFormat);

        /// <summary>
        /// 按序列号获取请求任务信息。
        /// </summary>
        /// <param name="serialId">任务序列编号</param>
        /// <returns>请求任务信息，若不存在则返回无效 TaskInfo。</returns>
        TaskInfo GetRequestTaskInfo(int serialId);

        /// <summary>
        /// 获取指定广告类型中价值最高的广告位 ID。
        /// </summary>
        /// <param name="adFormat">广告类型</param>
        /// <returns>价值最高的广告位 ID，无数据时返回 string.Empty。</returns>
        string GetHighestValuePlacementID(ADFormat adFormat);

        /// <summary>
        /// 从给定的 placementID 集合中筛选已加载就绪的广告位，按价值从高到低排序。
        /// </summary>
        /// <param name="adFormat">广告类型</param>
        /// <param name="placementIDs">待筛选的广告位 ID 集合</param>
        /// <returns>已就绪的广告位 ID 列表</returns>
        System.Collections.Generic.List<string> GetReadyPlacementIDs(ADFormat adFormat, System.Collections.Generic.IEnumerable<string> placementIDs);

        /// <summary>
        /// 轮询更新，由 ADManager 每帧驱动
        /// </summary>
        void Update(float elapseSeconds, float realElapseSeconds);

        /// <summary>
        /// 获取所有已请求过的广告位 ID，按广告格式分组
        /// </summary>
        /// <returns>Key: ADFormat, Value: placementID 集合</returns>
        System.Collections.Generic.IReadOnlyDictionary<ADFormat, System.Collections.Generic.IReadOnlyCollection<string>>
            GetAllPlacementIDs();

        /// <summary>
        /// 关闭并清理资源
        /// </summary>
        void Shutdown();
    }
}