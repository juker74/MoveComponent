using System;
using GameFrameX.Runtime;
namespace GameFrameX.Ad
{
    /// <summary>
    /// 广告请求代理，负责处理单个广告位的加载任务。
    /// 多个 Agent 共享同一个 IADRequestAgentHelper，由 TaskPool 调度并发。
    /// </summary>
    internal class ADRequestAgent : ITaskAgent<ADRequestTask>
    {
        private readonly IADRequestAgentHelper m_Helper;
        private ADRequestTask m_Task;
        private float m_WaitTime;
        private bool m_TimeoutReported;

        public Action<ADRequestAgent> ADRequestAgentStart;

        public Action<ADRequestAgent, object> ADRequestAgentSuccess;

        public Action<ADRequestAgent, string, string> ADRequestAgentFailure;

        public ADRequestAgent(IADRequestAgentHelper adRequestAgentHelper)
        {
            if (adRequestAgentHelper == null)
            {
                throw new GameFrameworkException("AD RequestAgentHelper is invalid.");
            }

            m_Helper = adRequestAgentHelper;
            m_Task = null;
            m_WaitTime = 0f;
            m_TimeoutReported = false;

            ADRequestAgentStart = null;
            ADRequestAgentSuccess = null;
            ADRequestAgentFailure = null;
        }

        /// <summary>
        /// 获取当前处理的广告请求任务。
        /// </summary>
        public ADRequestTask Task => m_Task;

        /// <summary>
        /// 获取已经等待时间。
        /// </summary>
        public float WaitTime => m_WaitTime;

        public void Initialize()
        {
            m_Helper.ADRequestAgentHelperComplete += OnADRequestAgentHelperComplete;
            m_Helper.ADRequestAgentHelperError += OnADRequestAgentHelperError;
        }

        public void Update(float elapseSeconds, float realElapseSeconds)
        {
            if (m_Task == null || m_Task.Status != ADRequestState.Doing)
                return;

            m_WaitTime += realElapseSeconds;
            if (!m_TimeoutReported && m_WaitTime >= m_Task.TimeOut)
            {
                m_TimeoutReported = true;
                GameFrameworkLog.Warning(
                    "AD request task '{0}' for placement '{1}' has exceeded timeout '{2}' seconds, waiting for SDK callback.",
                    m_Task.SerialId, m_Task.PlacementID, m_Task.TimeOut);
            }
        }

        public void Shutdown()
        {
            Reset();
            m_Helper.ADRequestAgentHelperComplete -= OnADRequestAgentHelperComplete;
            m_Helper.ADRequestAgentHelperError -= OnADRequestAgentHelperError;
        }

        public StartTaskStatus Start(ADRequestTask task)
        {
            if (task == null)
            {
                throw new GameFrameworkException("Task is invalid.");
            }

            string placementID = task.PlacementID;
            if (string.IsNullOrEmpty(placementID))
            {
                throw new GameFrameworkException("PlacementID is invalid.");
            }

            m_Task = task;
            m_Task.Status = ADRequestState.Doing;

            if (ADRequestAgentStart != null)
            {
                ADRequestAgentStart(this);
            }

            m_WaitTime = 0f;
            m_TimeoutReported = false;
            try
            {
                m_Helper.Request(placementID, task.ForceLoad, task.UserData);
            }
            catch (Exception error)
            {
                if (!task.Done)
                {
                    task.Status = ADRequestState.Error;
                    task.Done = true;
                    ADRequestAgentFailure?.Invoke(this, "AD_LOAD_EXCEPTION", error.Message);
                }
            }
            return StartTaskStatus.CanResume;
        }

        public void Reset()
        {
            m_Task = null;
            m_WaitTime = 0f;
            m_TimeoutReported = false;
        }

        private void OnADRequestAgentHelperComplete(object sender, ADRequestAgentHelperCompleteEventArgs arg)
        {
            // 通过 PlacementID 过滤：只有当前 Agent 负责的广告位才处理
            if (m_Task == null || m_Task.Done || arg.PlacementID != m_Task.PlacementID)
            {
                return;
            }

            m_Task.Status = ADRequestState.Done;
            m_Task.Done = true;
            if (ADRequestAgentSuccess != null)
            {
                ADRequestAgentSuccess(this, arg.ADObject);
            }

        }

        private void OnADRequestAgentHelperError(object sender, ADRequestAgentHelperErrorEventArgs arg)
        {
            // 通过 PlacementID 过滤：只有当前 Agent 负责的广告位才处理
            if (m_Task == null || m_Task.Done || arg.PlacementID != m_Task.PlacementID)
            {
                return;
            }

            m_Task.Status = ADRequestState.Error;
            m_Task.Done = true;
            if (ADRequestAgentFailure != null)
            {
                ADRequestAgentFailure(this, arg.ErrorCode, arg.ErrorMessage);
            }

        }
    }
}
