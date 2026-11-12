using System;
using GameFrameX.Runtime;
namespace GameFrameX.Ad
{
    internal class ADPlayAgent : ITaskAgent<ADPlayTask>
    {
        private readonly IADPlayAgentHelper m_Helper;
        private ADPlayTask m_Task;
        private float m_WaitTime;
        private bool m_TimeoutReported;
        public Action<ADPlayAgent> ADPlayAgentStart;
        public Action<ADPlayAgent> ADPlayAgentShow;
        public Action<ADPlayAgent> ADPlayAgentStartPlay;
        public Action<ADPlayAgent> ADPlayAgentClick;
        public Action<ADPlayAgent> ADPlayAgentSuccess;
        public Action<ADPlayAgent, string, string> ADPlayAgentFailure;

        public ADPlayAgent(IADPlayAgentHelper adPlayAgentHelper)
        {
            if (adPlayAgentHelper == null)
            {
                throw new GameFrameworkException("ADPlayAgentHelper is invalid.");
            }

            m_Helper = adPlayAgentHelper;
            m_Task = null;
            m_WaitTime = 0;
            m_TimeoutReported = false;
            ADPlayAgentStart = null;
            ADPlayAgentShow = null;
            ADPlayAgentStartPlay = null;
            ADPlayAgentClick = null;
            ADPlayAgentSuccess = null;
            ADPlayAgentFailure = null;
        }

        /// <summary>
        /// 获得当前处理的广告播放任务
        /// </summary>
        public ADPlayTask Task => m_Task;

        /// <summary>
        /// 获取已经等待的时间
        /// </summary>
        public float WaitTime => m_WaitTime;

        public void Initialize()
        {
            m_Helper.ADPlayAgentHelperStartEvent += OnADPlayAgentHelperStart;
            m_Helper.ADPlayAgentHelperShow += OnADPlayAgentHelperShow;
            m_Helper.ADPlayAgentHelperStartPlay += OnADPlayAgentHelperStartPlay;
            m_Helper.ADPlayAgentHelperClick += OnADPlayAgentHelperClick;
            m_Helper.ADPlayAgentHelperComplete += OnADPlayAgentHelperSuccess;
            m_Helper.ADPlayAgentHelperError += OnADPlayAgentHelperError;
        }

        public void Update(float elapseSeconds, float realElapseSeconds)
        {
            if (m_Task == null || m_Task.States != ADPlayState.Doing)
                return;
            m_WaitTime += realElapseSeconds;
            if (!m_TimeoutReported && m_WaitTime >= m_Task.TimeOut)
            {
                m_TimeoutReported = true;
                string message = Utility.Text.Format(
                    "AD play task '{0}' for placement '{1}', pos '{2}' has exceeded timeout '{3}' seconds.",
                    m_Task.SerialId, m_Task.PlacementID, m_Task.PosID, m_Task.TimeOut);
                GameFrameworkLog.Warning(message);
                CompleteFailure("AD_PLAY_TIMEOUT", message);
            }
        }

        public void Shutdown()
        {
            Reset();
            m_Helper.ADPlayAgentHelperStartEvent -= OnADPlayAgentHelperStart;
            m_Helper.ADPlayAgentHelperShow -= OnADPlayAgentHelperShow;
            m_Helper.ADPlayAgentHelperStartPlay -= OnADPlayAgentHelperStartPlay;
            m_Helper.ADPlayAgentHelperClick -= OnADPlayAgentHelperClick;
            m_Helper.ADPlayAgentHelperComplete -= OnADPlayAgentHelperSuccess;
            m_Helper.ADPlayAgentHelperError -= OnADPlayAgentHelperError;
        }

        public StartTaskStatus Start(ADPlayTask task)
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
            m_Task.States = ADPlayState.Doing;
            m_WaitTime = 0f;
            m_TimeoutReported = false;
            try
            {
                m_Helper.Play(placementID, task.PosID, task.UserData);
            }
            catch (Exception error)
            {
                if (!task.Done) CompleteFailure("AD_PLAY_EXCEPTION", error.Message);
            }
            return StartTaskStatus.CanResume;
        }

        public void Reset()
        {
            m_Task = null;
            m_WaitTime = 0f;
            m_TimeoutReported = false;
        }

        private bool IsCurrentTask(string placementID, string posID)
        {
            return m_Task != null && !m_Task.Done && placementID == m_Task.PlacementID && posID == m_Task.PosID;
        }

        private void CompleteSuccess()
        {
            m_Task.States = ADPlayState.Done;
            m_Task.Done = true;
            if (ADPlayAgentSuccess != null)
            {
                ADPlayAgentSuccess(this);
            }

        }

        private void CompleteFailure(string errorCode, string errorMessage)
        {
            m_Task.States = ADPlayState.Error;
            m_Task.Done = true;
            if (ADPlayAgentFailure != null)
            {
                ADPlayAgentFailure(this, errorCode, errorMessage);
            }

        }

        private void OnADPlayAgentHelperClick(object sender, ADPlayAgentHelperClickEventArgs e)
        {
            if (!IsCurrentTask(e.PlacementID, e.PosID))
            {
                return;
            }

            if (ADPlayAgentClick != null)
            {
                ADPlayAgentClick(this);
            }

        }

        private void OnADPlayAgentHelperStartPlay(object sender, ADPlayAgentHelperStartPlayEventArgs e)
        {
            if (!IsCurrentTask(e.PlacementID, e.PosID))
            {
                return;
            }

            if (ADPlayAgentStartPlay != null)
            {
                ADPlayAgentStartPlay(this);
            }

        }

        private void OnADPlayAgentHelperShow(object sender, ADPlayAgentHelperShowEventArgs e)
        {
            if (!IsCurrentTask(e.PlacementID, e.PosID))
            {
                return;
            }

            if (ADPlayAgentShow != null)
            {
                ADPlayAgentShow(this);
            }

            m_Task.States = ADPlayState.Showing;

        }

        private void OnADPlayAgentHelperStart(object sender, ADPlayAgentHelperStartEventArgs e)
        {
            if (!IsCurrentTask(e.PlacementID, e.PosID))
            {
                return;
            }

            if (ADPlayAgentStart != null)
            {
                ADPlayAgentStart(this);
            }

        }

        /// <summary>
        /// 当播放广告成功
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void OnADPlayAgentHelperSuccess(object sender, ADPlayAgentHelperCompleteEventArgs e)
        {
            // 通过 PlacementID 过滤：只有当前 Agent 负责的广告位才处理
            if (!IsCurrentTask(e.PlacementID, e.PosID))
            {
                return;
            }

            CompleteSuccess();

        }

        /// <summary>
        /// 当播放广告失败
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void OnADPlayAgentHelperError(object sender, ADPlayAgentHelperErrorEventArgs e)
        {
            // 通过 PlacementID 过滤：只有当前 Agent 负责的广告位才处理
            if (!IsCurrentTask(e.PlacementID, e.PosID))
            {
                return;
            }

            CompleteFailure(e.ErrorCode, e.Message);

        }
    }
}
