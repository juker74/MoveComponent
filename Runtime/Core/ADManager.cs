using System;
using GameFrameX.Runtime;
using System.Collections.Generic;

namespace GameFrameX.Ad
{
    /// <summary>
    /// AD 模块管理器，支持同时接入多个广告平台。
    /// 每个平台对应一个 IADHelper，通过 Name 区分。
    /// </summary>
    public sealed partial class ADManager : GameFrameworkModule, IADManager
    {
        private readonly Dictionary<string, IADHelper> m_ADHelpers = new Dictionary<string, IADHelper>();

        // ────────────────────────────────
        // IADManager 事件
        // ────────────────────────────────
        public event EventHandler<ADRequestStartEventArgs> ADRequestStart;
        public event EventHandler<ADRequestSuccessEventArgs> ADRequestSuccess;

        public event EventHandler<ADRequestFailureEventArgs> ADRequestFailure;
        public event EventHandler<ADRequestExpiredEventArgs> ADRequestExpired;

        //
        public event EventHandler<ADPlayStartEventArgs> ADPlayStart;
        public event EventHandler<ADPlayShowEventArgs> ADPlayShow;
        public event EventHandler<ADPlayStartPlayEventArgs> ADPlayStartPlay;
        public event EventHandler<ADPlayClickEventArgs> ADPlayClick;
        public event EventHandler<ADPlaySuccessEventArgs> ADPlaySuccess;
        public event EventHandler<ADPlayFailureEventArgs> ADPlayFailure;

        // ────────────────────────────────
        // IADManager 方法
        // ────────────────────────────────

        /// <inheritdoc />
        public void AddADHelper(IADHelper adHelper)
        {
            if (adHelper == null)
                throw new GameFrameworkException("AD helper is invalid.");

            string name = adHelper.Name;
            if (string.IsNullOrEmpty(name))
                throw new GameFrameworkException("AD helper name is invalid.");

            if (m_ADHelpers.ContainsKey(name))
                throw new GameFrameworkException($"AD helper '{name}' is already registered.");

            m_ADHelpers[name] = adHelper;

            // 订阅该 Helper 的事件，聚合到 Manager 层
            adHelper.ADRequestStart += OnHelperADRequestStart;
            adHelper.ADRequestSuccess += OnHelperADRequestSuccess;
            adHelper.ADRequestFailure += OnHelperADRequestFailure;
            adHelper.ADRequestExpired += OnHelperADRequestExpired;
            adHelper.ADPlayStart += OnHelperADPlayStart;
            adHelper.ADPlayShow += OnHelperADPlayShow;
            adHelper.ADPlayStartPlay += OnHelperADPlayStartPlay;
            adHelper.ADPlayClick += OnHelperADPlayClick;
            adHelper.ADPlaySuccess += OnHelperADPlaySuccess;
            adHelper.ADPlayFailure += OnHelperADPlayFailure;
        }

        /// <inheritdoc />
        public bool RemoveADHelper(string platformName)
        {
            if (string.IsNullOrEmpty(platformName))
                return false;

            if (!m_ADHelpers.TryGetValue(platformName, out IADHelper helper))
                return false;

            helper.ADRequestStart -= OnHelperADRequestStart;
            helper.ADRequestSuccess -= OnHelperADRequestSuccess;
            helper.ADRequestFailure -= OnHelperADRequestFailure;
            helper.ADRequestExpired -= OnHelperADRequestExpired;
            helper.ADPlayStart -= OnHelperADPlayStart;
            helper.ADPlayShow -= OnHelperADPlayShow;
            helper.ADPlayStartPlay -= OnHelperADPlayStartPlay;
            helper.ADPlayClick -= OnHelperADPlayClick;
            helper.ADPlaySuccess -= OnHelperADPlaySuccess;
            helper.ADPlayFailure -= OnHelperADPlayFailure;
            helper.Shutdown();
            m_ADHelpers.Remove(platformName);
            return true;
        }

        /// <inheritdoc />
        public int AddADRequest(string platformName, string placementID, ADFormat adFormat, string tag, int priority,
            float expireSeconds, object userData)
        {
            if (string.IsNullOrEmpty(platformName))
            {
                // 跨平台选择：取所有平台中价值最高的 placementID
                if (TrySelectBest(ref platformName, ref placementID, adFormat))
                    return m_ADHelpers[platformName]
                        .AddADRequest(placementID, adFormat, tag, priority, expireSeconds, userData);

                throw new GameFrameworkException("No AD platform available to auto-select placement.");
            }

            IADHelper helper = GetOrThrow(platformName);
            return helper.AddADRequest(placementID, adFormat, tag, priority, expireSeconds, userData);
        }

        public int PlayAD(string platformName, string placementID, ADFormat adFormat, string podid, string tag,
            int priority,
            object userData)
        {
            if (string.IsNullOrEmpty(platformName))
            {
                // 跨平台选择：取所有平台中价值最高的 placementID
                if (TrySelectBest(ref platformName, ref placementID, adFormat))
                    return m_ADHelpers[platformName].PlayAD(placementID, adFormat, podid, tag, priority, userData);

                throw new GameFrameworkException("No AD platform available to auto-select placement.");
            }

            IADHelper helper = GetOrThrow(platformName);
            return helper.PlayAD(placementID, adFormat, podid, tag, priority, userData);
        }

        public int PlayAD(ADFormat adFormat, string podid, string tag, int priority, object userData)
        {
            return PlayAD(string.Empty, string.Empty, adFormat, podid, tag, priority, userData);
        }

        /// <summary>
        /// 跨平台选择最佳平台和 placementID。
        /// 优先选价值最高的，兜底取任一平台任一广告位，最终兜底用最近播放的。
        /// </summary>
        private bool TrySelectBest(ref string platformName, ref string placementID, ADFormat adFormat)
        {
            string bestPlatform = null;
            string bestPlacement = null;
            double bestValue = double.MinValue;

            // 第一轮：所有平台中取价值最高的 placementID
            foreach (var kvp in m_ADHelpers)
            {
                string pid = kvp.Value.GetHighestValuePlacementID(adFormat);
                if (string.IsNullOrEmpty(pid)) continue;

                if (kvp.Value is ADHelperBase hb && hb.TryGetAdValue(pid, adFormat, out double val) && val > bestValue)
                {
                    bestValue = val;
                    bestPlatform = kvp.Key;
                    bestPlacement = pid;
                }
                else if (bestPlatform == null)
                {
                    bestPlatform = kvp.Key;
                    bestPlacement = pid;
                }
            }

            // 第二轮：兜底——任一平台任一广告位
            if (bestPlatform == null)
            {
                foreach (var kvp in m_ADHelpers)
                {
                    var placements = kvp.Value.GetAllPlacementIDs();
                    if (placements.TryGetValue(adFormat, out var pids) && pids.Count > 0)
                    {
                        foreach (string pid in pids)
                        {
                            bestPlatform = kvp.Key;
                            bestPlacement = pid;
                            break;
                        }

                        if (bestPlatform != null) break;
                    }
                }
            }

            // 第三轮：最终兜底——最近一次播放记录
            if (bestPlatform == null)
            {
                foreach (var kvp in m_ADHelpers)
                {
                    if (kvp.Value is ADHelperBase hb && hb.TryGetLastPlayInfo(out string lastPid, out ADFormat lastFmt)
                                                     && lastFmt == adFormat && !string.IsNullOrEmpty(lastPid))
                    {
                        bestPlatform = kvp.Key;
                        bestPlacement = lastPid;
                        break;
                    }
                }
            }

            if (bestPlatform != null)
            {
                platformName = bestPlatform;
                placementID = bestPlacement;
                return true;
            }

            return false;
        }

        /// <inheritdoc />
        public double GetAdValue(string platformName, string placementID, ADFormat adFormat)
        {
            if (!m_ADHelpers.TryGetValue(platformName, out IADHelper helper))
                return 0;
            if (helper is ADHelperBase hb && hb.TryGetAdValue(placementID, adFormat, out double val))
                return val;
            return 0;
        }

        /// <inheritdoc />
        public List<string> GetReadyPlacementIDs(string platformName, ADFormat adFormat,
            IEnumerable<string> placementIDs)
        {
            if (!m_ADHelpers.TryGetValue(platformName, out IADHelper helper))
                return new List<string>();
            return helper.GetReadyPlacementIDs(adFormat, placementIDs);
        }

        /// <inheritdoc />
        public bool RemoveADRequest(string platformName, int serialId)
        {
            if (!m_ADHelpers.TryGetValue(platformName, out IADHelper helper))
                return false;

            return helper.RemoveADRequest(serialId);
        }

        /// <inheritdoc />
        public bool RemoveADPlayRequest(string platformName, int serialId)
        {
            if (!m_ADHelpers.TryGetValue(platformName, out IADHelper helper))
                return false;

            return helper.RemoveADPlayRequest(serialId);
        }

        /// <inheritdoc />
        public bool GetTaskPoolInfo(string platformName, ADFormat adFormat, out int freeAgents, out int workingAgents,
            out int waitingTasks)
        {
            if (m_ADHelpers.TryGetValue(platformName, out IADHelper helper))
            {
                // Helper 实现此方法时可直接透传；当前接口未声明则走基类
                return GetTaskPoolInfoFromHelper(helper, adFormat, out freeAgents, out workingAgents, out waitingTasks);
            }

            freeAgents = workingAgents = waitingTasks = 0;
            return false;
        }

        /// <inheritdoc />
        public TaskInfo CheckAdStatus(string platformName, string placementID, ADFormat adFormat)
        {
            if (!m_ADHelpers.TryGetValue(platformName, out IADHelper helper))
            {
                return default(TaskInfo);
            }

            return helper.CheckAdStatus(placementID, adFormat);
        }

        /// <inheritdoc />
        public TaskInfo GetRequestTaskInfo(string platformName, int serialId)
        {
            if (!m_ADHelpers.TryGetValue(platformName, out IADHelper helper))
            {
                return default(TaskInfo);
            }

            return helper.GetRequestTaskInfo(serialId);
        }

        /// <inheritdoc />
        public string GetHighestValuePlacementID(string platformName, ADFormat adFormat)
        {
            if (!m_ADHelpers.TryGetValue(platformName, out IADHelper helper))
                return string.Empty;

            return helper.GetHighestValuePlacementID(adFormat);
        }

        /// <inheritdoc />
        public IReadOnlyDictionary<string, IReadOnlyDictionary<ADFormat, IReadOnlyCollection<string>>>
            GetAllPlacementIDs()
        {
            var result = new Dictionary<string, IReadOnlyDictionary<ADFormat, IReadOnlyCollection<string>>>();
            foreach (var kvp in m_ADHelpers)
            {
                result[kvp.Key] = kvp.Value.GetAllPlacementIDs();
            }

            return result;
        }

        // ────────────────────────────────
        // GameFrameworkModule 生命周期
        // ────────────────────────────────

        protected override void Update(float elapseSeconds, float realElapseSeconds)
        {
            foreach (var helper in m_ADHelpers.Values)
            {
                helper.Update(elapseSeconds, realElapseSeconds);
            }
        }

        protected override void Shutdown()
        {
            foreach (var helper in m_ADHelpers.Values)
            {
                helper.ADRequestStart -= OnHelperADRequestStart;
                helper.ADRequestSuccess -= OnHelperADRequestSuccess;
                helper.ADRequestFailure -= OnHelperADRequestFailure;
                helper.ADRequestExpired -= OnHelperADRequestExpired;
                helper.ADPlayStart -= OnHelperADPlayStart;
                helper.ADPlayShow -= OnHelperADPlayShow;
                helper.ADPlayStartPlay -= OnHelperADPlayStartPlay;
                helper.ADPlayClick -= OnHelperADPlayClick;
                helper.ADPlaySuccess -= OnHelperADPlaySuccess;
                helper.ADPlayFailure -= OnHelperADPlayFailure;
                helper.Shutdown();
            }

            m_ADHelpers.Clear();
        }

        // ────────────────────────────────
        // 内部辅助
        // ────────────────────────────────

        private IADHelper GetOrThrow(string platformName)
        {
            if (string.IsNullOrEmpty(platformName))
                throw new GameFrameworkException("Platform name is invalid.");

            if (!m_ADHelpers.TryGetValue(platformName, out IADHelper helper))
                throw new GameFrameworkException($"AD helper for platform '{platformName}' is not registered.");

            return helper;
        }

        private static bool GetTaskPoolInfoFromHelper(IADHelper helper, ADFormat adFormat, out int freeAgents,
            out int workingAgents, out int waitingTasks)
        {
            if (helper is ADHelperBase baseHelper)
            {
                return baseHelper.GetTaskPoolInfo(adFormat, out freeAgents, out workingAgents, out waitingTasks);
            }

            freeAgents = workingAgents = waitingTasks = 0;
            return false;
        }

        // ────────────────────────────────
        // 事件转发（Helper → Manager）
        // ────────────────────────────────

        private void OnHelperADRequestStart(object sender, ADRequestStartEventArgs e)
        {
            ADRequestStart?.Invoke(this, e);
        }

        private void OnHelperADRequestSuccess(object sender, ADRequestSuccessEventArgs e)
        {
            ADRequestSuccess?.Invoke(this, e);
        }

        private void OnHelperADRequestFailure(object sender, ADRequestFailureEventArgs e)
        {
            ADRequestFailure?.Invoke(this, e);
        }

        private void OnHelperADRequestExpired(object sender, ADRequestExpiredEventArgs e)
        {
            ADRequestExpired?.Invoke(this, e);
        }


        private void OnHelperADPlayStart(object sender, ADPlayStartEventArgs e)
        {
            ADPlayStart?.Invoke(this, e);
        }

        private void OnHelperADPlayShow(object sender, ADPlayShowEventArgs e)
        {
            ADPlayShow?.Invoke(this, e);
        }

        private void OnHelperADPlayStartPlay(object sender, ADPlayStartPlayEventArgs e)
        {
            ADPlayStartPlay?.Invoke(this, e);
        }

        private void OnHelperADPlayClick(object sender, ADPlayClickEventArgs e)
        {
            ADPlayClick?.Invoke(this, e);
        }

        private void OnHelperADPlaySuccess(object sender, ADPlaySuccessEventArgs e)
        {
            ADPlaySuccess?.Invoke(this, e);
        }

        private void OnHelperADPlayFailure(object sender, ADPlayFailureEventArgs e)
        {
            ADPlayFailure?.Invoke(this, e);
        }
    }
}
