using System;
using GameFrameX.Runtime;


namespace GameFrameX.Ad
{
    public interface IADManager
    {
        /// <summary>
        /// 添加广告平台辅助器（一个平台对应一个 IADHelper，通过 IADHelper.Name 标识）
        /// </summary>
        void AddADHelper(IADHelper adHelper);

        /// <summary>
        /// 移除广告平台辅助器。
        /// </summary>
        bool RemoveADHelper(string platformName);

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
        /// AD 播放点击事件
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
        /// 增加广告加载任务
        /// </summary>
        /// <param name="platformName">平台名称（对应 IADHelper.Name）</param>
        /// <param name="placementID">广告位 ID</param>
        /// <param name="adFormat">广告类型（激励/插屏/Banner/原生）</param>
        /// <param name="tag">标签</param>
        /// <param name="priority">优先级</param>
        /// <param name="userData">用户自定义数据</param>
        /// <returns>任务序列编号</returns>
        int AddADRequest(string platformName, string placementID, ADFormat adFormat, string tag, int priority,
            float expireSeconds, object userData);

        /// <summary>
        /// 播放广告播放任务
        /// </summary>
        /// <param name="platformName">平台（为空自动选最高价值平台）</param>
        /// <param name="placementID">广告位id（为空自动选最高价值广告位）</param>
        /// <param name="adFormat">广告类型</param>
        /// <param name="podid">场景id</param>
        /// <param name="tag">标签</param>
        /// <param name="priority">权重</param>
        /// <param name="userData">用户自定义</param>
        /// <returns></returns>
        int PlayAD(string platformName, string placementID, ADFormat adFormat, string podid, string tag, int priority,
            object userData);

        /// <summary>
        /// 播放广告
        /// </summary>
        /// <param name="adFormat"></param>
        /// <param name="podid"></param>
        /// <param name="tag"></param>
        /// <param name="priority"></param>
        /// <param name="userData"></param>
        /// <returns></returns>
        int PlayAD(ADFormat adFormat, string podid, string tag, int priority, object userData);

        /// <summary>
        /// 移除广告加载任务
        /// </summary>
        /// <param name="platformName">平台名称（对应 IADHelper.Name）</param>
        /// <param name="serialId">任务序列编号</param>
        /// <returns>是否移除成功</returns>
        bool RemoveADRequest(string platformName, int serialId);

        /// <summary>
        /// 移除广告播放任务
        /// </summary>
        /// <param name="platformName">平台名称（对应 IADHelper.Name）</param>
        /// <param name="serialId">任务序列编号</param>
        /// <returns>是否移除成功</returns>
        bool RemoveADPlayRequest(string platformName, int serialId);

        /// <summary>
        /// 获取指定平台指定格式的任务池状态（调试用）
        /// </summary>
        bool GetTaskPoolInfo(string platformName, ADFormat adFormat, out int freeAgents, out int workingAgents,
            out int waitingTasks);

        /// <summary>
        /// 检查指定平台指定广告位的请求任务状态
        /// </summary>
        /// <param name="platformName">平台名称（对应 IADHelper.Name）</param>
        /// <param name="placementID">广告位 ID</param>
        /// <param name="adFormat">广告类型</param>
        /// <returns>请求任务信息，若不存在该广告位的任务则返回无效 TaskInfo。</returns>
        TaskInfo CheckAdStatus(string platformName, string placementID, ADFormat adFormat);

        /// <summary>
        /// 按序列号获取请求任务信息。
        /// </summary>
        /// <param name="platformName">平台名称（对应 IADHelper.Name）</param>
        /// <param name="serialId">任务序列编号</param>
        /// <returns>请求任务信息，若不存在则返回无效 TaskInfo。</returns>
        TaskInfo GetRequestTaskInfo(string platformName, int serialId);

        /// <summary>
        /// 获取指定平台指定广告类型中价值最高的广告位 ID。
        /// </summary>
        /// <param name="platformName">平台名称</param>
        /// <param name="adFormat">广告类型</param>
        /// <returns>价值最高的广告位 ID，无数据时返回 string.Empty。</returns>
        string GetHighestValuePlacementID(string platformName, ADFormat adFormat);

        /// <summary>
        /// 获取指定广告位的价值。
        /// </summary>
        /// <param name="platformName">平台名称</param>
        /// <param name="placementID">广告位 ID</param>
        /// <param name="adFormat">广告类型</param>
        /// <returns>广告价值，无数据时返回 0。</returns>
        double GetAdValue(string platformName, string placementID, ADFormat adFormat);

        /// <summary>
        /// 从给定的 placementID 集合中筛选指定平台已加载就绪的广告位，按价值从高到低排序。
        /// </summary>
        /// <param name="platformName">平台名称</param>
        /// <param name="adFormat">广告类型</param>
        /// <param name="placementIDs">待筛选的广告位 ID 集合</param>
        /// <returns>已就绪的广告位 ID 列表</returns>
        System.Collections.Generic.List<string> GetReadyPlacementIDs(string platformName, ADFormat adFormat,
            System.Collections.Generic.IEnumerable<string> placementIDs);

        /// <summary>
        /// 获取所有平台的所有已请求广告位 ID，按平台和格式分组
        /// </summary>
        /// <returns>Key: 平台名称, Value: (Key: ADFormat, Value: placementID 集合)</returns>
        System.Collections.Generic.IReadOnlyDictionary<string, System.Collections.Generic.IReadOnlyDictionary<ADFormat,
            System.Collections.Generic.IReadOnlyCollection<string>>> GetAllPlacementIDs();
    }
}