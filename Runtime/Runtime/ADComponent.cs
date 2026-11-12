using System;
using GameFrameX.Runtime;
using System.Collections.Generic;
using GameFrameX.Ad;
using GameFrameX.Ad.Runtime;
using UnityEngine;

namespace GameFrameX.Ad.Runtime
{
    /// <summary>
    /// 单个 RequestAgentHelper 的配置项（类型或自定义实例）。
    /// </summary>
    [Serializable]
    public sealed class ADRequestAgentConfigItem
    {
        [Tooltip("AgentHelper 类型名称（继承 ADRequestAgentHelperBase）")]
        public string AgentHelperTypeName;

        [Tooltip("自定义 AgentHelper（类型名称为空时使用）")]
        public ADRequestAgentHelperBase CustomAgentHelper;
    }

    /// <summary>
    /// 单个 PlayAgentHelper 的配置项（类型或自定义实例）。
    /// </summary>
    [Serializable]
    public sealed class ADPlayAgentConfigItem
    {
        [Tooltip("PlayAgentHelper 类型名称（继承 ADPlayAgentHelperBase）")]
        public string AgentHelperTypeName;

        [Tooltip("自定义 PlayAgentHelper（类型名称为空时使用）")]
        public ADPlayAgentHelperBase CustomAgentHelper;
    }

    /// <summary>
    /// 单个 Helper 的配置块，包含 Helper 实例及其关联的多种 AgentHelper 配置。
    /// </summary>
    [Serializable]
    public sealed class ADHelperConfigItem
    {
        [SerializeReference] [Tooltip("广告平台 Helper 实例")]
        public IADHelper Helper;

        [Tooltip("AgentHelper 配置列表（可配置多种广告类型的 AgentHelper，如 Reward、Interstitial 等）")]
        public List<ADRequestAgentConfigItem> AgentConfigs = new List<ADRequestAgentConfigItem>();

        [Tooltip("PlayAgentHelper 配置列表（可配置多种广告类型的播放 AgentHelper，如 Reward、Interstitial 等）")]
        public List<ADPlayAgentConfigItem> PlayAgentConfigs = new List<ADPlayAgentConfigItem>();
    }

    /// <summary>
    /// AD（广告）组件，负责配置和注册广告平台辅助器到 ADManager。
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("Game Framework/AD")]
    public sealed class ADComponent : GameFrameworkComponent
    {
        private IADManager m_ADManager;
        private GameFrameX.Event.Runtime.IEventManager m_EventComponent;

        [SerializeField] private Transform m_InstanceRoot = null;

        [SerializeField] private float m_Timeout = 30f;

        [SerializeField] private List<ADHelperConfigItem> m_ADHelperConfigs = new List<ADHelperConfigItem>();

        private readonly List<string> m_RegisteredADHelperNames = new List<string>();
        private bool m_Started;

        /// <summary>
        /// 获取 AD 模块超时时间。
        /// </summary>
        public float Timeout
        {
            get => m_Timeout;
            set => m_Timeout = value;
        }

        public void RegisterHelper(IADHelper helper)
        {
            m_ADManager.AddADHelper(helper);
            m_RegisteredADHelperNames.Add(helper.Name);
        }

        protected override void Awake()
        {
            InterfaceComponentType = typeof(IADManager);
            ImplementationComponentType = typeof(GameFrameX.Ad.ADManager);
            base.Awake();
            if (!IsRuntimeComponentReady) return;

            m_ADManager = GameFrameworkEntry.GetModule<IADManager>();
            if (m_ADManager == null)
            {
                Debug.LogError("[ADComponent] ADManager is invalid.");
                return;
            }

            m_ADManager.ADRequestStart += OnADRequestStart;
            m_ADManager.ADRequestSuccess += OnADRequestSuccess;
            m_ADManager.ADRequestFailure += OnADRequestFailure;
            m_ADManager.ADRequestExpired += OnADRequestExpired;
            m_ADManager.ADPlayStart += OnADPlayStart;
            m_ADManager.ADPlayShow += OnADPlayShow;
            m_ADManager.ADPlayStartPlay += OnADPlayStartPlay;
            m_ADManager.ADPlayClick += OnADPlayClick;
            m_ADManager.ADPlaySuccess += OnADPlaySuccess;
            m_ADManager.ADPlayFailure += OnADPlayFailure;
        }

        private void Start()
        {
            if (m_Started) return;
            if (m_ADManager == null)
            {
                Debug.LogError("[ADComponent] ADManager is invalid.");
                return;
            }

            if (m_EventComponent == null)
            {
                if (!EnsureEventComponent())
                {
                    GameFrameworkLog.Error("Event component is invalid.");
                    return;
                }
            }

            if (m_InstanceRoot == null)
            {
                m_InstanceRoot = new GameObject("AD Agent Instances").transform;
                m_InstanceRoot.SetParent(gameObject.transform);
                m_InstanceRoot.localScale = Vector3.one;
            }

            foreach (ADHelperConfigItem config in m_ADHelperConfigs)
            {
                if (config == null || config.Helper == null)
                    continue;

                try
                {
                    config.Helper.Init(null);
                    config.Helper.LoadTimeOut = m_Timeout;
                    RegisterHelper(config.Helper);
                    Debug.Log($"[ADComponent] Registered AD helper: {config.Helper.Name}");

                    // 遍历启用的 RequestAgentHelper 配置，每种广告格式只注册一个长期辅助器。
                    int requestIndex = 0;
                    HashSet<ADFormat> registeredRequestFormats = new HashSet<ADFormat>();
                    if (config.AgentConfigs != null)
                    {
                        foreach (ADRequestAgentConfigItem agentConfig in config.AgentConfigs)
                        {
                            if (string.IsNullOrEmpty(agentConfig.AgentHelperTypeName) &&
                                agentConfig.CustomAgentHelper == null)
                            {
                                continue;
                            }

                            if (AddADRequestAgentHelper(config.Helper, agentConfig, registeredRequestFormats,
                                    requestIndex))
                            {
                                requestIndex++;
                            }
                        }
                    }

                    // 遍历启用的 PlayAgentHelper 配置，每种广告格式只注册一个长期辅助器。
                    int playIndex = 0;
                    HashSet<ADFormat> registeredPlayFormats = new HashSet<ADFormat>();
                    if (config.PlayAgentConfigs != null)
                    {
                        foreach (ADPlayAgentConfigItem agentConfig in config.PlayAgentConfigs)
                        {
                            if (string.IsNullOrEmpty(agentConfig.AgentHelperTypeName) &&
                                agentConfig.CustomAgentHelper == null)
                            {
                                continue;
                            }

                            if (AddADPlayAgentHelper(config.Helper, agentConfig, registeredPlayFormats, playIndex))
                            {
                                playIndex++;
                            }
                        }
                    }
                }
                catch (Exception e)
                {
                    Debug.LogError(
                        $"[ADComponent] Failed to init/register AD helper '{config.Helper?.Name}': {e.Message}");
                }
            }
            m_Started = true;
        }

        private void OnDestroy()
        {
            Shutdown();
        }

        internal void Shutdown()
        {
            m_Started = false;
            if (m_ADManager != null)
            {
                m_ADManager.ADRequestStart -= OnADRequestStart;
                m_ADManager.ADRequestSuccess -= OnADRequestSuccess;
                m_ADManager.ADRequestFailure -= OnADRequestFailure;
                m_ADManager.ADRequestExpired -= OnADRequestExpired;
                m_ADManager.ADPlayStart -= OnADPlayStart;
                m_ADManager.ADPlayShow -= OnADPlayShow;
                m_ADManager.ADPlayStartPlay -= OnADPlayStartPlay;
                m_ADManager.ADPlayClick -= OnADPlayClick;
                m_ADManager.ADPlaySuccess -= OnADPlaySuccess;
                m_ADManager.ADPlayFailure -= OnADPlayFailure;

                for (int i = m_RegisteredADHelperNames.Count - 1; i >= 0; i--)
                {
                    m_ADManager.RemoveADHelper(m_RegisteredADHelperNames[i]);
                }

                m_ADManager = null;
            }

            m_RegisteredADHelperNames.Clear();
            m_EventComponent = null;
        }

        /// <summary>
        /// 创建 AgentHelper 实例并挂到 InstanceRoot 下，然后注册到对应 Helper。
        /// </summary>
        private bool AddADRequestAgentHelper(IADHelper helper, ADRequestAgentConfigItem agentConfig,
            HashSet<ADFormat> registeredFormats, int index)
        {
            IADRequestAgentHelper agentHelper = CreateADRequestAgentHelper(agentConfig, index);
            if (agentHelper == null)
            {
                Debug.LogError(
                    $"[ADComponent] Can not create AD request agent helper '{agentConfig.AgentHelperTypeName}'.");
                return false;
            }

            MonoBehaviour agentHelperBehaviour = agentHelper as MonoBehaviour;
            if (agentHelperBehaviour == null)
            {
                Debug.LogError(
                    $"[ADComponent] AD request agent helper '{agentHelper.GetType().FullName}' must inherit MonoBehaviour.");
                return false;
            }

            if (!registeredFormats.Add(agentHelper.SupportedFormat))
            {
                Debug.LogWarning(
                    $"[ADComponent] AD request agent helper for format '{agentHelper.SupportedFormat}' is already enabled, ignore '{agentHelper.GetType().Name}'.");
                if (agentHelperBehaviour != agentConfig.CustomAgentHelper)
                {
                    Destroy(agentHelperBehaviour.gameObject);
                }

                return false;
            }

            agentHelperBehaviour.name = Utility.Text.Format("AD Request Agent Helper - {0}", index);
            Transform transform = agentHelperBehaviour.transform;
            transform.SetParent(m_InstanceRoot);
            transform.localScale = Vector3.one;

            helper.AddADRequestAgentHelper(agentHelper);
            Debug.Log(
                $"[ADComponent] Registered agent: {agentHelper.GetType().Name}#{index} to helper '{helper.Name}' (format: {agentHelper.SupportedFormat})");
            return true;
        }

        private IADRequestAgentHelper CreateADRequestAgentHelper(ADRequestAgentConfigItem agentConfig, int index)
        {
            if (!string.IsNullOrEmpty(agentConfig.AgentHelperTypeName))
            {
                Type helperType = Utility.Assembly.GetType(agentConfig.AgentHelperTypeName);
                if (helperType == null)
                {
                    GameFrameworkLog.Warning("Can not find helper type '{0}'.", agentConfig.AgentHelperTypeName);
                    return null;
                }

                if (!typeof(IADRequestAgentHelper).IsAssignableFrom(helperType))
                {
                    GameFrameworkLog.Warning("Type '{0}' is not assignable from '{1}'.", typeof(IADRequestAgentHelper).FullName,
                        helperType.FullName);
                    return null;
                }

                if (!typeof(MonoBehaviour).IsAssignableFrom(helperType))
                {
                    GameFrameworkLog.Warning("Type '{0}' must inherit from '{1}'.", helperType.FullName,
                        typeof(MonoBehaviour).FullName);
                    return null;
                }

                return (IADRequestAgentHelper)new GameObject().AddComponent(helperType);
            }

            if (agentConfig.CustomAgentHelper == null)
            {
                GameFrameworkLog.Warning("You must set custom helper with '{0}' type first.",
                    typeof(ADRequestAgentHelperBase).FullName);
                return null;
            }

            if (agentConfig.CustomAgentHelper.gameObject.scene.IsValid())
            {
                return index > 0 ? Instantiate(agentConfig.CustomAgentHelper) : agentConfig.CustomAgentHelper;
            }

            return Instantiate(agentConfig.CustomAgentHelper);
        }

        /// <summary>
        /// 创建 PlayAgentHelper 实例并挂到 InstanceRoot 下，然后注册到对应 Helper。
        /// </summary>
        private bool AddADPlayAgentHelper(IADHelper helper, ADPlayAgentConfigItem agentConfig,
            HashSet<ADFormat> registeredFormats, int index)
        {
            IADPlayAgentHelper agentHelper = CreateADPlayAgentHelper(agentConfig, index);
            if (agentHelper == null)
            {
                Debug.LogError(
                    $"[ADComponent] Can not create AD play agent helper '{agentConfig.AgentHelperTypeName}'.");
                return false;
            }

            MonoBehaviour agentHelperBehaviour = agentHelper as MonoBehaviour;
            if (agentHelperBehaviour == null)
            {
                Debug.LogError(
                    $"[ADComponent] AD play agent helper '{agentHelper.GetType().FullName}' must inherit MonoBehaviour.");
                return false;
            }

            if (!registeredFormats.Add(agentHelper.SupportedFormat))
            {
                Debug.LogWarning(
                    $"[ADComponent] AD play agent helper for format '{agentHelper.SupportedFormat}' is already enabled, ignore '{agentHelper.GetType().Name}'.");
                if (agentHelperBehaviour != agentConfig.CustomAgentHelper)
                {
                    Destroy(agentHelperBehaviour.gameObject);
                }

                return false;
            }

            agentHelperBehaviour.name = Utility.Text.Format("AD Play Agent Helper - {0}", index);
            Transform transform = agentHelperBehaviour.transform;
            transform.SetParent(m_InstanceRoot);
            transform.localScale = Vector3.one;

            helper.AddADPlayAgentHelper(agentHelper);
            Debug.Log(
                $"[ADComponent] Registered play agent: {agentHelper.GetType().Name}#{index} to helper '{helper.Name}' (format: {agentHelper.SupportedFormat})");
            return true;
        }

        private IADPlayAgentHelper CreateADPlayAgentHelper(ADPlayAgentConfigItem agentConfig, int index)
        {
            if (!string.IsNullOrEmpty(agentConfig.AgentHelperTypeName))
            {
                Type helperType = Utility.Assembly.GetType(agentConfig.AgentHelperTypeName);
                if (helperType == null)
                {
                    GameFrameworkLog.Warning("Can not find helper type '{0}'.", agentConfig.AgentHelperTypeName);
                    return null;
                }

                if (!typeof(IADPlayAgentHelper).IsAssignableFrom(helperType))
                {
                    GameFrameworkLog.Warning("Type '{0}' is not assignable from '{1}'.", typeof(IADPlayAgentHelper).FullName,
                        helperType.FullName);
                    return null;
                }

                if (!typeof(MonoBehaviour).IsAssignableFrom(helperType))
                {
                    GameFrameworkLog.Warning("Type '{0}' must inherit from '{1}'.", helperType.FullName,
                        typeof(MonoBehaviour).FullName);
                    return null;
                }

                return (IADPlayAgentHelper)new GameObject().AddComponent(helperType);
            }

            if (agentConfig.CustomAgentHelper == null)
            {
                GameFrameworkLog.Warning("You must set custom helper with '{0}' type first.",
                    typeof(ADPlayAgentHelperBase).FullName);
                return null;
            }

            if (agentConfig.CustomAgentHelper.gameObject.scene.IsValid())
            {
                return index > 0 ? Instantiate(agentConfig.CustomAgentHelper) : agentConfig.CustomAgentHelper;
            }

            return Instantiate(agentConfig.CustomAgentHelper);
        }

        private bool EnsureADManager(string operation)
        {
            if (m_ADManager != null)
            {
                return true;
            }

            Debug.LogError($"[ADComponent] Can not {operation}: ADManager is invalid.");
            return false;
        }

        /// <summary>
        /// 增加广告加载任务。
        /// </summary>
        public int AddADRequest(string platformName, string placementID, ADFormat adFormat, string tag, int priority,
            float expireSeconds, object userData)
        {
            if (!EnsureADManager("add AD request"))
            {
                return -1;
            }

            return m_ADManager.AddADRequest(platformName, placementID, adFormat, tag, priority, expireSeconds,
                userData);
        }

        /// <summary>
        /// 移除广告加载任务。
        /// </summary>
        public bool RemoveADRequest(string platformName, int serialId)
        {
            if (!EnsureADManager("remove AD request"))
            {
                return false;
            }

            return m_ADManager.RemoveADRequest(platformName, serialId);
        }

        /// <summary>
        /// 增加广告播放任务。
        /// </summary>
        public int PlayAD(string platformName, string placementID, ADFormat adFormat, string podid, string tag,
            int priority, object userData)
        {
            if (!EnsureADManager("play AD"))
            {
                return -1;
            }

            return m_ADManager.PlayAD(platformName, placementID, adFormat, podid, tag, priority, userData);
        }

        public int PlayAD(ADFormat adFormat, string podid, string tag, int priority, object userData)
        {
            if (!EnsureADManager("play AD"))
            {
                return -1;
            }

            return m_ADManager.PlayAD(adFormat, podid, tag, priority, userData);
        }

        /// <summary>
        /// 移除广告播放任务。
        /// </summary>
        public bool RemoveADPlayRequest(string platformName, int serialId)
        {
            if (!EnsureADManager("remove AD play request"))
            {
                return false;
            }

            return m_ADManager.RemoveADPlayRequest(platformName, serialId);
        }

        /// <summary>
        /// 获取指定平台指定格式的任务池状态（调试用）。
        /// </summary>
        public bool GetTaskPoolInfo(string platformName, ADFormat adFormat, out int freeAgents, out int workingAgents,
            out int waitingTasks)
        {
            freeAgents = 0;
            workingAgents = 0;
            waitingTasks = 0;
            if (!EnsureADManager("get AD task pool info"))
            {
                return false;
            }

            return m_ADManager.GetTaskPoolInfo(platformName, adFormat, out freeAgents, out workingAgents,
                out waitingTasks);
        }

        /// <summary>
        /// 检查指定平台指定广告位的状态。
        /// </summary>
        /// <param name="platformName">平台名称</param>
        /// <param name="placementID">广告位 ID</param>
        /// <param name="adFormat">广告类型</param>
        /// <returns>请求任务信息，若不存在该广告位的任务则返回无效 TaskInfo。</returns>
        public TaskInfo CheckAdStatus(string platformName, string placementID, ADFormat adFormat)
        {
            if (!EnsureADManager("check AD status"))
            {
                return default(TaskInfo);
            }

            return m_ADManager.CheckAdStatus(platformName, placementID, adFormat);
        }

        /// <summary>
        /// 按序列号获取请求任务信息。
        /// </summary>
        /// <param name="platformName">平台名称</param>
        /// <param name="serialId">任务序列编号</param>
        /// <returns>请求任务信息，若不存在则返回无效 TaskInfo。</returns>
        public TaskInfo GetRequestTaskInfo(string platformName, int serialId)
        {
            if (!EnsureADManager("get AD request task info"))
            {
                return default(TaskInfo);
            }

            return m_ADManager.GetRequestTaskInfo(platformName, serialId);
        }

        /// <summary>
        /// 获取指定平台指定广告类型中价值最高的广告位 ID。
        /// </summary>
        /// <param name="platformName">平台名称</param>
        /// <param name="adFormat">广告类型</param>
        /// <returns>价值最高的广告位 ID，无数据时返回 string.Empty。</returns>
        public string GetHighestValuePlacementID(string platformName, ADFormat adFormat)
        {
            if (!EnsureADManager("get highest value AD placement ID"))
            {
                return string.Empty;
            }

            return m_ADManager.GetHighestValuePlacementID(platformName, adFormat);
        }

        /// <summary>
        /// 获取指定广告位的价值。
        /// </summary>
        public double GetAdValue(string platformName, string placementID, ADFormat adFormat)
        {
            if (!EnsureADManager("get AD value"))
            {
                return 0d;
            }

            return m_ADManager.GetAdValue(platformName, placementID, adFormat);
        }

        /// <summary>
        /// 从给定的 placementID 集合中筛选指定平台已加载就绪的广告位。
        /// </summary>
        /// <param name="platformName">平台名称</param>
        /// <param name="adFormat">广告类型</param>
        /// <param name="placementIDs">待筛选的广告位 ID 集合</param>
        /// <returns>已就绪的广告位 ID 列表</returns>
        public List<string> GetReadyPlacementIDs(string platformName, ADFormat adFormat,
            IEnumerable<string> placementIDs)
        {
            if (!EnsureADManager("get ready AD placement IDs"))
            {
                return new List<string>();
            }

            return m_ADManager.GetReadyPlacementIDs(platformName, adFormat, placementIDs);
        }

        /// <summary>
        /// 获取所有平台所有广告位的加载状态
        /// Key: 平台名称, Value: 按格式分组的 (placementID, status) 列表
        /// </summary>
        public Dictionary<string, Dictionary<ADFormat, List<PlacementStatusInfo>>> GetAllAdStatuses()
        {
            var result = new Dictionary<string, Dictionary<ADFormat, List<PlacementStatusInfo>>>();
            if (m_ADManager == null) return result;

            var allPlacements = m_ADManager.GetAllPlacementIDs();
            foreach (var platformKvp in allPlacements)
            {
                string platformName = platformKvp.Key;
                var formatDict = new Dictionary<ADFormat, List<PlacementStatusInfo>>();
                foreach (var formatKvp in platformKvp.Value)
                {
                    var infoList = new List<PlacementStatusInfo>();
                    foreach (string placementID in formatKvp.Value)
                    {
                        TaskInfo taskInfo = default(TaskInfo);
                        try
                        {
                            taskInfo = m_ADManager.CheckAdStatus(platformName, placementID, formatKvp.Key);
                        }
                        catch (System.Exception ex)
                        {
                            infoList.Add(new PlacementStatusInfo
                            {
                                PlacementID = placementID,
                                Status = $"Error: {ex.Message}",
                            });
                            continue;
                        }

                        string statusText = taskInfo.IsValid ? taskInfo.Status.ToString() : "No task";
                        infoList.Add(new PlacementStatusInfo
                        {
                            PlacementID = placementID,
                            Status = statusText,
                        });
                    }

                    formatDict[formatKvp.Key] = infoList;
                }

                result[platformName] = formatDict;
            }

            return result;
        }

        private bool EnsureEventComponent()
        {
            if (m_EventComponent != null)
            {
                return true;
            }

            m_EventComponent = GameFrameworkEntry.GetModule<GameFrameX.Event.Runtime.IEventManager>();
            if (m_EventComponent != null)
            {
                return true;
            }

            Debug.LogError("[ADComponent] EventComponent is invalid.");
            return false;
        }

        private void OnADRequestStart(object sender, GameFrameX.Ad.ADRequestStartEventArgs e)
        {
            if (!EnsureEventComponent())
            {
                return;
            }

            m_EventComponent.Fire(this, GameFrameX.Ad.Runtime.ADRequestStartEventArgs.Create(e));
        }

        private void OnADRequestSuccess(object sender, GameFrameX.Ad.ADRequestSuccessEventArgs e)
        {
            if (!EnsureEventComponent())
            {
                return;
            }

            m_EventComponent.Fire(this, GameFrameX.Ad.Runtime.ADRequestSuccessEventArgs.Create(e));
        }

        private void OnADRequestFailure(object sender, GameFrameX.Ad.ADRequestFailureEventArgs e)
        {
            if (!EnsureEventComponent())
            {
                return;
            }

            m_EventComponent.Fire(this, GameFrameX.Ad.Runtime.ADRequestFailureEventArgs.Create(e));
        }

        private void OnADPlayStart(object sender, GameFrameX.Ad.ADPlayStartEventArgs e)
        {
            if (!EnsureEventComponent())
            {
                return;
            }

            m_EventComponent.Fire(this, GameFrameX.Ad.Runtime.ADPlayStartEventArgs.Create(e));
        }

        private void OnADPlaySuccess(object sender, GameFrameX.Ad.ADPlaySuccessEventArgs e)
        {
            if (!EnsureEventComponent())
            {
                return;
            }

            m_EventComponent.Fire(this, GameFrameX.Ad.Runtime.ADPlaySuccessEventArgs.Create(e));
        }

        private void OnADPlayFailure(object sender, GameFrameX.Ad.ADPlayFailureEventArgs e)
        {
            if (!EnsureEventComponent())
            {
                return;
            }

            m_EventComponent.Fire(this, GameFrameX.Ad.Runtime.ADPlayFailureEventArgs.Create(e));
        }

        private void OnADRequestExpired(object sender, GameFrameX.Ad.ADRequestExpiredEventArgs e)
        {
            if (!EnsureEventComponent())
            {
                return;
            }

            m_EventComponent.Fire(this, GameFrameX.Ad.Runtime.ADRequestExpiredEventArgs.Create(e));
        }

        private void OnADPlayShow(object sender, GameFrameX.Ad.ADPlayShowEventArgs e)
        {
            if (!EnsureEventComponent())
            {
                return;
            }

            m_EventComponent.Fire(this, GameFrameX.Ad.Runtime.ADPlayShowEventArgs.Create(e));
        }

        private void OnADPlayStartPlay(object sender, GameFrameX.Ad.ADPlayStartPlayEventArgs e)
        {
            if (!EnsureEventComponent())
            {
                return;
            }

            m_EventComponent.Fire(this, GameFrameX.Ad.Runtime.ADPlayStartPlayEventArgs.Create(e));
        }

        private void OnADPlayClick(object sender, GameFrameX.Ad.ADPlayClickEventArgs e)
        {
            if (!EnsureEventComponent())
            {
                return;
            }

            m_EventComponent.Fire(this, GameFrameX.Ad.Runtime.ADPlayClickEventArgs.Create(e));
        }
    }

    /// <summary>
    /// 单个广告位的状态信息
    /// </summary>
    [System.Serializable]
    public struct PlacementStatusInfo
    {
        /// <summary>
        /// 广告位 ID
        /// </summary>
        public string PlacementID;

        /// <summary>
        /// 广告位加载状态（SDK 返回的 JSON 或错误信息）
        /// </summary>
        public string Status;

        public override string ToString()
        {
            return $"{PlacementID}: {Status}";
        }
    }
}
