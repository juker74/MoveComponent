using System;
using GameFrameX.Runtime;
using System.Collections.Generic;
using UnityEngine;

namespace GameFrameX.Ad
{
    /// <summary>
    /// 广告辅助器抽象基类
    /// 封装通用的任务队列管理和 Agent 分发逻辑，子类只需实现平台初始化逻辑
    [Serializable]
    public abstract class ADHelperBase : IADHelper
    {
        // 每种 ADFormat 独立一个 TaskPool
        private readonly Dictionary<ADFormat, TaskPool<ADRequestTask>> m_RequestTaskPools
            = new Dictionary<ADFormat, TaskPool<ADRequestTask>>();

        // 存储每个 ADFormat 对应的 AgentHelper，用于 CheckAdStatus 等查询
        private readonly Dictionary<ADFormat, IADRequestAgentHelper> m_RequestAgentHelpers
            = new Dictionary<ADFormat, IADRequestAgentHelper>();

        // 每种 ADFormat 独立一个 PlayTaskPool
        private readonly Dictionary<ADFormat, TaskPool<ADPlayTask>> m_PlayTaskPools
            = new Dictionary<ADFormat, TaskPool<ADPlayTask>>();

        // 存储每个 ADFormat 对应的 PlayAgentHelper
        private readonly Dictionary<ADFormat, IADPlayAgentHelper> m_PlayAgentHelpers
            = new Dictionary<ADFormat, IADPlayAgentHelper>();

        // 追踪所有已请求过的 placementID，按 ADFormat 分组
        private readonly Dictionary<ADFormat, HashSet<string>> m_PlacementIDs
            = new Dictionary<ADFormat, HashSet<string>>();

        private readonly Dictionary<string, LoadedPlacementInfo> m_LoadedPlacements
            = new Dictionary<string, LoadedPlacementInfo>();

        // placementID+format -> 最近一次 AddADRequest 的 serialId，用于 CheckAdStatus 反查 TaskInfo
        private readonly Dictionary<string, int> m_PlacementSerialIds = new Dictionary<string, int>();

        // 最近一次 PlayAD 使用的 placementID + adFormat，用于最终兜底
        private string m_LastPlayPlacementID = null;
        private ADFormat m_LastPlayFormat;

        private float m_LoadTimeOut = 30f;

        [SerializeField] [Tooltip("各平台最大并发加载广告的数量")]
        private List<PlatformAgentCountConfig> m_PlatformAgentCounts = new List<PlatformAgentCountConfig>();

        [SerializeField] [Tooltip("默认最大并发加载数量（未匹配到平台时使用）")] [Range(1, 20)]
        private int m_DefaultAgentCount = 3;

        /// <summary>
        /// 获取当前平台的最大并发加载 Agent 数量
        /// </summary>
        private int GetAgentCount()
        {
            RuntimePlatform current = Application.platform;
            foreach (var config in m_PlatformAgentCounts)
            {
                if (config.Platform == current)
                    return config.MaxAgentCount;
            }

            return m_DefaultAgentCount;
        }

        /// <summary>
        /// 平台并发 Agent 数量配置
        /// </summary>
        [Serializable]
        public struct PlatformAgentCountConfig
        {
            [Tooltip("目标平台")] public RuntimePlatform Platform;

            [Tooltip("最大并发加载数量")] [Range(1, 20)] public int MaxAgentCount;
        }

        // ----------------------------------------------------------------
        // IADHelper 属性
        // ----------------------------------------------------------------
        public abstract string Name { get; }

        public float LoadTimeOut
        {
            get => m_LoadTimeOut;
            set => m_LoadTimeOut = value;
        }

        // ----------------------------------------------------------------
        // IADHelper 事件
        // ----------------------------------------------------------------
        //Request
        public event EventHandler<ADRequestStartEventArgs> ADRequestStart;
        public event EventHandler<ADRequestSuccessEventArgs> ADRequestSuccess;
        public event EventHandler<ADRequestFailureEventArgs> ADRequestFailure;

        //Play
        public event EventHandler<ADPlayStartEventArgs> ADPlayStart;
        public event EventHandler<ADPlayShowEventArgs> ADPlayShow;
        public event EventHandler<ADPlayStartPlayEventArgs> ADPlayStartPlay;
        public event EventHandler<ADPlayClickEventArgs> ADPlayClick;
        public event EventHandler<ADPlaySuccessEventArgs> ADPlaySuccess;
        public event EventHandler<ADPlayFailureEventArgs> ADPlayFailure;

        public event EventHandler<ADRequestExpiredEventArgs> ADRequestExpired;
        // ----------------------------------------------------------------
        // 生命周期
        // ----------------------------------------------------------------

        /// <summary>
        /// 平台初始化，子类实现（如 TopOn、MAX SDK 初始化）
        /// </summary>
        public abstract void Init(object userData);

        /// <summary>
        /// 轮询，由 ADManager 每帧驱动
        /// </summary>
        public void Update(float elapseSeconds, float realElapseSeconds)
        {
            foreach (var helper in m_RequestAgentHelpers.Values)
                if (helper is IADCallbackDispatcher dispatcher) dispatcher.DispatchCallbacks();
            foreach (var helper in m_PlayAgentHelpers.Values)
                if (helper is IADCallbackDispatcher dispatcher) dispatcher.DispatchCallbacks();
            UpdateLoadedPlacements(realElapseSeconds);

            foreach (var pool in m_RequestTaskPools.Values)
            {
                pool.Update(elapseSeconds, realElapseSeconds);
            }

            foreach (var pool in m_PlayTaskPools.Values)
            {
                pool.Update(elapseSeconds, realElapseSeconds);
            }
        }

        /// <summary>
        /// 关闭并清理，由 ADManager 调用
        /// </summary>
        public void Shutdown()
        {
            foreach (var pool in m_RequestTaskPools.Values)
            {
                pool.Shutdown();
            }

            foreach (var pool in m_PlayTaskPools.Values)
            {
                pool.Shutdown();
            }

            foreach (var helper in m_RequestAgentHelpers.Values) helper.Shutdown();
            foreach (var helper in m_PlayAgentHelpers.Values) helper.Shutdown();
            m_RequestTaskPools.Clear();
            m_RequestAgentHelpers.Clear();
            m_PlayTaskPools.Clear();
            m_PlayAgentHelpers.Clear();
            m_PlacementIDs.Clear();
            m_LoadedPlacements.Clear();
            m_PlacementSerialIds.Clear();
        }

        /// <summary>
        /// 获取所有已请求过的广告位 ID，按广告格式分组
        /// </summary>
        public IReadOnlyDictionary<ADFormat, IReadOnlyCollection<string>> GetAllPlacementIDs()
        {
            var result = new Dictionary<ADFormat, IReadOnlyCollection<string>>();
            foreach (var kvp in m_PlacementIDs)
            {
                result[kvp.Key] = kvp.Value;
            }

            return result;
        }
        // ----------------------------------------------------------------
        // IADHelper 方法实现
        // ----------------------------------------------------------------

        /// <summary>
        /// 注册广告加载代理辅助器
        /// 根据 Helper.SupportedFormat 自动归入对应的 TaskPool。
        /// 创建多个 ADRequestAgent 共享同一个 Helper，通过 TaskPool 调度实现多广告位并发。
        /// </summary>
        public void AddADRequestAgentHelper(IADRequestAgentHelper helper)
        {
            if (helper == null)
                throw new GameFrameworkException("AD request agent helper is invalid.");

            ADFormat format = helper.SupportedFormat;
            if (m_RequestAgentHelpers.ContainsKey(format))
            {
                GameFrameworkLog.Warning("AD request agent helper for ADFormat '{0}' is already registered.", format);
                return;
            }

            if (!m_RequestTaskPools.TryGetValue(format, out TaskPool<ADRequestTask> pool))
            {
                pool = new TaskPool<ADRequestTask>();
                m_RequestTaskPools[format] = pool;
            }

            m_RequestAgentHelpers[format] = helper;

            // 初始化一次（SDK 事件订阅等）
            helper.Initialize();

            // 根据平台配置创建对应数量的 Agent，用于并发加载广告
            int agentCount = GetAgentCount();
            for (int i = 0; i < agentCount; i++)
            {
                ADRequestAgent agent = new ADRequestAgent(helper);
                agent.ADRequestAgentStart += OnAgentStart;
                agent.ADRequestAgentSuccess += OnAgentSuccess;
                agent.ADRequestAgentFailure += OnAgentFailure;
                pool.AddAgent(agent);
            }
        }

        /// <summary>
        /// 注册广告播放代理辅助器
        /// 根据 Helper.SupportedFormat 自动归入对应的 TaskPool。
        /// 创建多个 ADPlayAgent 共享同一个 Helper，通过 TaskPool 调度实现多广告位并发播放。
        /// </summary>
        public virtual void AddADPlayAgentHelper(IADPlayAgentHelper helper)
        {
            if (helper == null)
                throw new GameFrameworkException("AD play agent helper is invalid.");

            ADFormat format = helper.SupportedFormat;
            if (m_PlayAgentHelpers.ContainsKey(format))
            {
                GameFrameworkLog.Warning("AD play agent helper for ADFormat '{0}' is already registered.", format);
                return;
            }

            if (!m_PlayTaskPools.TryGetValue(format, out TaskPool<ADPlayTask> pool))
            {
                pool = new TaskPool<ADPlayTask>();
                m_PlayTaskPools[format] = pool;
            }

            m_PlayAgentHelpers[format] = helper;

            // 初始化一次（SDK 事件订阅等）
            helper.Initialize();

            // 根据平台配置创建对应数量的 Agent，用于并发播放广告
            int agentCount = GetAgentCount();
            for (int i = 0; i < agentCount; i++)
            {
                ADPlayAgent agent = new ADPlayAgent(helper);
                agent.ADPlayAgentStart += OnPlayAgentStart;
                agent.ADPlayAgentShow += OnPlayAgentShow;
                agent.ADPlayAgentStartPlay += OnPlayAgentStartPlay;
                agent.ADPlayAgentClick += OnPlayAgentClick;
                agent.ADPlayAgentSuccess += OnPlayAgentSuccess;
                agent.ADPlayAgentFailure += OnPlayAgentFailure;
                pool.AddAgent(agent);
            }
        }

        /// <summary>
        /// 增加广告加载任务
        /// </summary>
        /// <returns>任务序列编号</returns>
        public int AddADRequest(string placementID, ADFormat adFormat, string tag, int priority, float expireSeconds,
            object userData)
        {
            return AddADRequest(placementID, adFormat, tag, priority, expireSeconds, false, userData);
        }

        private int AddADRequest(string placementID, ADFormat adFormat, string tag, int priority, float expireSeconds,
            bool forceLoad, object userData)
        {
            // 未指定 placementID 时自动选择
            if (string.IsNullOrEmpty(placementID))
                placementID = ResolveFallbackPlacement(adFormat);

            if (string.IsNullOrEmpty(placementID))
                throw new GameFrameworkException("PlacementID is invalid and no available placement found.");

            if (!m_RequestTaskPools.TryGetValue(adFormat, out TaskPool<ADRequestTask> pool))
                throw new GameFrameworkException(
                    Utility.Text.Format("No agent registered for ADFormat '{0}'. Call AddADRequestAgentHelper first.",
                        adFormat));

            string key = GetLoadedPlacementKey(placementID, adFormat);
            if (TryGetPendingRequestSerialId(key, out int pendingSerialId))
            {
                GameFrameworkLog.Debug(
                    "AD request task for placement '{0}' and ADFormat '{1}' is already pending, reuse serial id '{2}'.",
                    placementID, adFormat, pendingSerialId);
                return pendingSerialId;
            }

            // 追踪 placementID
            if (!m_PlacementIDs.TryGetValue(adFormat, out HashSet<string> idSet))
            {
                idSet = new HashSet<string>();
                m_PlacementIDs[adFormat] = idSet;
            }

            idSet.Add(placementID);

            ADRequestTask task = ADRequestTask.Create(placementID, adFormat, m_LoadTimeOut, expireSeconds, forceLoad,
                tag, priority, userData);
            pool.AddTask(task);

            // 最近一次请求的 serialId，用于 CheckAdStatus 反查
            m_PlacementSerialIds[key] = task.SerialId;

            return task.SerialId;
        }

        public int PlayAD(string placementID, ADFormat adFormat, string podid, string tag, int priority,
            object userData)
        {
            if (string.IsNullOrEmpty(podid))
                throw new GameFrameworkException("podid is invalid.");

            // 未指定 placementID 时自动选择
            if (string.IsNullOrEmpty(placementID))
                placementID = ResolveFallbackPlacement(adFormat);

            if (string.IsNullOrEmpty(placementID))
                throw new GameFrameworkException("PlacementID is invalid and no available placement found.");

            if (!m_PlayTaskPools.TryGetValue(adFormat, out TaskPool<ADPlayTask> pool))
                throw new GameFrameworkException(
                    Utility.Text.Format("No play agent registered for ADFormat '{0}'. Call AddADPlayAgentHelper first.",
                        adFormat));

            ADPlayTask task = ADPlayTask.Create(placementID, podid, adFormat, m_LoadTimeOut, tag, priority, userData);
            pool.AddTask(task);

            // 记录最近一次播放的广告位，用于跨平台兜底
            m_LastPlayPlacementID = placementID;
            m_LastPlayFormat = adFormat;

            return task.SerialId;
        }

        /// <summary>
        /// 取消指定序列号的加载任务
        /// </summary>
        public bool RemoveADRequest(int serialId)
        {
            foreach (var pool in m_RequestTaskPools.Values)
            {
                if (pool.RemoveTask(serialId))
                    return true;
            }

            return false;
        }

        public bool RemoveADPlayRequest(int serialId)
        {
            foreach (var pool in m_PlayTaskPools.Values)
            {
                if (pool.RemoveTask(serialId))
                    return true;
            }

            return false;
        }

        /// <summary>
        /// 获取指定格式任务池状态（调试用）
        /// </summary>
        public bool GetTaskPoolInfo(ADFormat adFormat, out int freeAgents, out int workingAgents, out int waitingTasks)
        {
            if (m_RequestTaskPools.TryGetValue(adFormat, out TaskPool<ADRequestTask> pool))
            {
                freeAgents = pool.FreeAgentCount;
                workingAgents = pool.WorkingAgentCount;
                waitingTasks = pool.WaitingTaskCount;
                return true;
            }

            freeAgents = workingAgents = waitingTasks = 0;
            return false;
        }

        /// <summary>
        /// 按广告位查找请求任务信息。遍历对应 ADFormat 任务池中的工作和等待任务，
        /// 返回第一个匹配 placementID 的 TaskInfo。不存在时返回无效 TaskInfo。
        /// </summary>
        public TaskInfo CheckAdStatus(string placementID, ADFormat adFormat)
        {
            string key = GetLoadedPlacementKey(placementID, adFormat);
            if (m_PlacementSerialIds.TryGetValue(key, out int serialId))
            {
                return GetRequestTaskInfo(serialId);
            }
            return default(TaskInfo);
        }

        /// <inheritdoc />
        public TaskInfo GetRequestTaskInfo(int serialId)
        {
            foreach (var pool in m_RequestTaskPools.Values)
            {
                TaskInfo info = pool.GetTaskInfo(serialId);
                if (info.IsValid) return info;
            }
            return default(TaskInfo);
        }
        private static string GetLoadedPlacementKey(string placementID, ADFormat adFormat)
        {
            return Utility.Text.Format("{0}:{1}", adFormat, placementID);
        }

        private bool TryGetPendingRequestSerialId(string placementKey, out int serialId)
        {
            if (m_PlacementSerialIds.TryGetValue(placementKey, out serialId))
            {
                TaskInfo taskInfo = GetRequestTaskInfo(serialId);
                if (taskInfo.IsValid && taskInfo.Status != TaskStatus.Done)
                {
                    return true;
                }
            }

            serialId = 0;
            return false;
        }

        /// <inheritdoc />
        public string GetHighestValuePlacementID(ADFormat adFormat)
        {
            if (!m_PlacementIDs.TryGetValue(adFormat, out HashSet<string> placements) || placements.Count == 0)
                return string.Empty;

            if (!m_RequestAgentHelpers.TryGetValue(adFormat, out IADRequestAgentHelper agentHelper))
                return string.Empty;

            string bestPlacementID = null;
            double bestValue = double.MinValue;

            foreach (string pid in placements)
            {
                double value = agentHelper.GetAdValue(pid);
                if (value > bestValue || (Math.Abs(value - bestValue) < 0.0001 && bestPlacementID == null))
                {
                    bestValue = value;
                    bestPlacementID = pid;
                }
            }

            return bestPlacementID ?? string.Empty;
        }

        /// <summary>
        /// 尝试获取指定 placementID 的广告价值，用于跨平台比较。
        /// </summary>
        internal bool TryGetAdValue(string placementID, ADFormat adFormat, out double value)
        {
            value = 0;
            if (!m_RequestAgentHelpers.TryGetValue(adFormat, out IADRequestAgentHelper agentHelper))
                return false;
            value = agentHelper.GetAdValue(placementID);
            return true;
        }

        /// <summary>
        /// 获取最近一次播放记录，用于跨平台兜底。
        /// </summary>
        internal bool TryGetLastPlayInfo(out string placementID, out ADFormat adFormat)
        {
            placementID = m_LastPlayPlacementID;
            adFormat = m_LastPlayFormat;
            return !string.IsNullOrEmpty(m_LastPlayPlacementID);
        }

        /// <summary>
        /// 自动选择 placementID：优先最高价值，兜底取同格式下任一广告位。
        /// </summary>
        private string ResolveFallbackPlacement(ADFormat adFormat)
        {
            string pid = GetHighestValuePlacementID(adFormat);
            if (!string.IsNullOrEmpty(pid)) return pid;

            if (m_PlacementIDs.TryGetValue(adFormat, out HashSet<string> placements) && placements.Count > 0)
            {
                foreach (string p in placements) return p;
            }

            return string.Empty;
        }

        /// <inheritdoc />
        public List<string> GetReadyPlacementIDs(ADFormat adFormat, IEnumerable<string> placementIDs)
        {
            var result = new List<string>();
            if (placementIDs == null) return result;
            if (!m_RequestAgentHelpers.TryGetValue(adFormat, out IADRequestAgentHelper helper)) return result;

            // 收集就绪的 placementID 及其价值
            var readyWithValues = new List<System.Tuple<string, double>>();
            foreach (string pid in placementIDs)
            {
                if (!string.IsNullOrEmpty(pid) && helper.IsAdReady(pid))
                {
                    double val = helper.GetAdValue(pid);
                    readyWithValues.Add(System.Tuple.Create(pid, val));
                }
            }

            // 按价值从高到低排序
            readyWithValues.Sort((a, b) => b.Item2.CompareTo(a.Item2));

            foreach (var item in readyWithValues)
                result.Add(item.Item1);

            return result;
        }

        private void RecordLoadedPlacement(ADRequestAgent agent)
        {
            if (agent.Task.ExpireSeconds <= 0f)
            {
                return;
            }

            string key = GetLoadedPlacementKey(agent.Task.PlacementID, agent.Task.ADFormat);
            m_LoadedPlacements[key] = new LoadedPlacementInfo
            {
                PlacementID = agent.Task.PlacementID,
                ADFormat = agent.Task.ADFormat,
                Tag = agent.Task.Tag,
                Priority = agent.Task.Priority,
                UserData = agent.Task.UserData,
                ExpireSeconds = agent.Task.ExpireSeconds,
                RemainingSeconds = agent.Task.ExpireSeconds,
            };
        }

        private void RemoveLoadedPlacement(string placementID, ADFormat adFormat)
        {
            m_LoadedPlacements.Remove(GetLoadedPlacementKey(placementID, adFormat));
        }

        private void UpdateLoadedPlacements(float realElapseSeconds)
        {
            if (m_LoadedPlacements.Count <= 0)
            {
                return;
            }

            List<string> placementKeys = new List<string>(m_LoadedPlacements.Keys);
            List<LoadedPlacementInfo> expiredPlacements = null;
            for (int i = 0; i < placementKeys.Count; i++)
            {
                string key = placementKeys[i];
                if (!m_LoadedPlacements.TryGetValue(key, out LoadedPlacementInfo info))
                {
                    continue;
                }

                info.RemainingSeconds -= realElapseSeconds;
                m_LoadedPlacements[key] = info;
                if (info.RemainingSeconds <= 0f)
                {
                    if (expiredPlacements == null)
                    {
                        expiredPlacements = new List<LoadedPlacementInfo>();
                    }

                    expiredPlacements.Add(info);
                }
            }

            if (expiredPlacements == null)
            {
                return;
            }

            foreach (LoadedPlacementInfo info in expiredPlacements)
            {
                RemoveLoadedPlacement(info.PlacementID, info.ADFormat);
                if (ADRequestExpired != null)
                {
                    ADRequestExpiredEventArgs expiredArgs = ADRequestExpiredEventArgs.Create(
                        info.PlacementID, info.ADFormat, info.Tag, info.UserData);
                    ADRequestExpired(this, expiredArgs);
                    ReferencePool.Release(expiredArgs);
                }

                AddADRequest(info.PlacementID, info.ADFormat, info.Tag, info.Priority, info.ExpireSeconds, true,
                    info.UserData);
            }
        }

        private struct LoadedPlacementInfo
        {
            public string PlacementID;
            public ADFormat ADFormat;
            public string Tag;
            public int Priority;
            public object UserData;
            public float ExpireSeconds;
            public float RemainingSeconds;
        }

        // ----------------------------------------------------------------
        // 内部回调（由 ADRequestAgent 调用）
        // ----------------------------------------------------------------

        private void OnAgentStart(ADRequestAgent agent)
        {
            if (ADRequestStart != null)
            {
                ADRequestStartEventArgs startArgs = ADRequestStartEventArgs.Create(
                    agent.Task.SerialId, agent.Task.PlacementID, agent.Task.ADFormat, agent.Task.Tag,
                    agent.Task.UserData);
                ADRequestStart(this, startArgs);
                ReferencePool.Release(startArgs);
            }
        }

        private void OnAgentSuccess(ADRequestAgent agent, object adObject)
        {
            RecordLoadedPlacement(agent);

            if (ADRequestSuccess != null)
            {
                ADRequestSuccessEventArgs successArgs = ADRequestSuccessEventArgs.Create(
                    agent.Task.SerialId,
                    agent.Task.PlacementID,
                    agent.Task.ADFormat,
                    adObject,
                    agent.Task.Tag,
                    agent.Task.UserData
                );
                ADRequestSuccess(this, successArgs);
                ReferencePool.Release(successArgs);
            }
        }

        private void OnAgentFailure(ADRequestAgent agent, string errorCode, string messge)
        {
            if (ADRequestFailure != null)
            {
                ADRequestFailureEventArgs failureArgs = ADRequestFailureEventArgs.Create(
                    agent.Task.SerialId,
                    agent.Task.PlacementID,
                    agent.Task.ADFormat,
                    errorCode,
                    messge,
                    agent.Task.Tag,
                    agent.Task.UserData
                );
                ADRequestFailure(this, failureArgs);
                ReferencePool.Release(failureArgs);
            }
        }
        // ----------------------------------------------------------------
        // 内部回调（由 ADPlayAgent 调用）
        // ----------------------------------------------------------------

        private void OnPlayAgentStart(ADPlayAgent agent)
        {
            if (ADPlayStart != null)
            {
                ADPlayStartEventArgs startArgs = ADPlayStartEventArgs.Create(
                    agent.Task.SerialId, agent.Task.PlacementID, agent.Task.ADFormat, agent.Task.PosID, agent.Task.Tag,
                    agent.Task.UserData);
                ADPlayStart(this, startArgs);
                ReferencePool.Release(startArgs);
            }
        }

        private void OnPlayAgentShow(ADPlayAgent agent)
        {
            if (ADPlayShow != null)
            {
                ADPlayShowEventArgs showArgs = ADPlayShowEventArgs.Create(agent.Task.SerialId, agent.Task.PlacementID,
                    agent.Task.ADFormat, agent.Task.PosID, agent.Task.Tag,
                    agent.Task.UserData);
                ADPlayShow(this, showArgs);
                ReferencePool.Release(showArgs);
            }
        }

        private void OnPlayAgentStartPlay(ADPlayAgent agent)
        {
            if (ADPlayStartPlay != null)
            {
                ADPlayStartPlayEventArgs startPlayArgs = ADPlayStartPlayEventArgs.Create(agent.Task.SerialId,
                    agent.Task.PlacementID, agent.Task.ADFormat, agent.Task.PosID, agent.Task.Tag,
                    agent.Task.UserData);
                ADPlayStartPlay(this, startPlayArgs);
                ReferencePool.Release(startPlayArgs);
            }
        }

        private void OnPlayAgentClick(ADPlayAgent agent)
        {
            if (ADPlayClick != null)
            {
                ADPlayClickEventArgs arg = ADPlayClickEventArgs.Create(agent.Task.SerialId, agent.Task.PlacementID,
                    agent.Task.ADFormat, agent.Task.PosID, agent.Task.Tag,
                    agent.Task.UserData);
                ADPlayClick(this, arg);
                ReferencePool.Release(arg);
            }
        }

        private void OnPlayAgentSuccess(ADPlayAgent agent)
        {
            RemoveLoadedPlacement(agent.Task.PlacementID, agent.Task.ADFormat);

            if (ADPlaySuccess != null)
            {
                ADPlaySuccessEventArgs successArgs = ADPlaySuccessEventArgs.Create(
                    agent.Task.SerialId, agent.Task.PlacementID, agent.Task.ADFormat, agent.Task.PosID, agent.Task.Tag,
                    agent.Task.UserData);
                ADPlaySuccess(this, successArgs);
                ReferencePool.Release(successArgs);
            }
        }

        private void OnPlayAgentFailure(ADPlayAgent agent, string errorCode, string errorMessge)
        {
            if (ADPlayFailure != null)
            {
                ADPlayFailureEventArgs failureArgs = ADPlayFailureEventArgs.Create(
                    agent.Task.SerialId, agent.Task.PlacementID, agent.Task.ADFormat, agent.Task.PosID,
                    agent.Task.Tag, agent.Task.UserData, errorCode, errorMessge);
                ADPlayFailure(this, failureArgs);
                ReferencePool.Release(failureArgs);
            }
        }
    }
}
