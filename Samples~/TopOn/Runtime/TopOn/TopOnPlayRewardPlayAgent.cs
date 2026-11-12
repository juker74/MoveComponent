using System;
using GameFrameX.Runtime;
using System.Collections.Generic;
using AnyThinkAds.Api;
using GameFrameX.Ad;

namespace GameFrameX.Ad.Runtime
{
    public class TopOnPlayRewardPlayAgent : ADPlayAgentHelperBase
    {
        /// <summary>
        /// 正在播放中的广告位对应的场景 ID。
        /// </summary>
        private readonly Dictionary<string, string> m_PosIds = new Dictionary<string, string>();

        private bool m_IsInitialized = false;

        public override ADFormat SupportedFormat => ADFormat.reward;

        public override void Initialize()
        {
            if (m_IsInitialized)
            {
                return;
            }

            base.Initialize();
            m_IsInitialized = true;
            //播放
            ATRewardedVideo.Instance.client.onAdVideoStartEvent += OnRewardStart;
            //click
            ATRewardedVideo.Instance.client.onAdClickEvent += OnReardClick;
            //complete
            ATRewardedVideo.Instance.client.onAdVideoCloseEvent += OnRewardClose;
            //error
            ATRewardedVideo.Instance.client.onAdVideoFailureEvent += OnRewardFailure;
        }

        public override void Play(string placementID, string posID, object userData)
        {
            if (m_PosIds.ContainsKey(placementID))
            {
                if (m_ADPlayAgentHelperErrorEvent != null)
                {
                    Publish(m_ADPlayAgentHelperErrorEvent, ADPlayAgentHelperErrorEventArgs.Create(placementID, posID,
                            $"Placement '{placementID}' is already playing.", "AD_ALREADY_PLAYING"));
                }
                return;
            }

            if (m_ADPlayAgentHelperStartEvent != null)
            {
                Publish(m_ADPlayAgentHelperStartEvent, ADPlayAgentHelperStartEventArgs.Create(placementID, posID));
            }

            if (!ATRewardedVideo.Instance.hasAdReady(placementID))
            {
                if (m_ADPlayAgentHelperErrorEvent != null)
                {
                    Publish(m_ADPlayAgentHelperErrorEvent, ADPlayAgentHelperErrorEventArgs.Create(placementID, posID,
                            $"Placement '{placementID}' is not ready.", "AD_NOT_READY"));
                }
                return;
            }

            m_PosIds.Add(placementID, posID);
            Dictionary<string, string> adObject = new Dictionary<string, string>();
            adObject.Add(ATConst.SCENARIO, posID);
            try { ATRewardedVideo.Instance.showAd(placementID, adObject); }
            catch
            {
                RemovePosID(placementID);
                throw;
            }
        }

        public override void Shutdown()
        {
            if (!m_IsInitialized) return;
            ATRewardedVideo.Instance.client.onAdVideoStartEvent -= OnRewardStart;
            ATRewardedVideo.Instance.client.onAdClickEvent -= OnReardClick;
            ATRewardedVideo.Instance.client.onAdVideoCloseEvent -= OnRewardClose;
            ATRewardedVideo.Instance.client.onAdVideoFailureEvent -= OnRewardFailure;
            m_IsInitialized = false;
            base.Shutdown();
            Reset();
        }

        public override string CheckAdStatus(string placementID)
        {
            return ATRewardedVideo.Instance.checkAdStatus(placementID);
        }

        public override void Reset()
        {
            m_PosIds.Clear();
        }

        private bool TryGetPosID(string placementID, out string posID)
        {
            if (string.IsNullOrEmpty(placementID))
            {
                posID = string.Empty;
                return false;
            }

            return m_PosIds.TryGetValue(placementID, out posID);
        }

        private void RemovePosID(string placementID)
        {
            if (!string.IsNullOrEmpty(placementID))
            {
                m_PosIds.Remove(placementID);
            }
        }

        private void OnReardClick(object sender, ATAdEventArgs arg)
        {
            Dispatch(() =>
            {
            if (!TryGetPosID(arg.placementId, out string posID))
            {
                return;
            }

            if (m_ADPlayAgentHelperClickEvent != null)
            {
                Publish(m_ADPlayAgentHelperClickEvent, ADPlayAgentHelperClickEventArgs.Create(arg.placementId, posID));
            }
            });
        }

        /// <summary>
        /// 当激励广告关闭
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="arg"></param>
        private void OnRewardClose(object sender, ATAdRewardEventArgs arg)
        {
            Dispatch(() =>
            {
            if (!TryGetPosID(arg.placementId, out string posID))
            {
                return;
            }

            RemovePosID(arg.placementId);
            if (arg.isRewarded && m_ADPlayAgentHelperCompleteEvent != null)
            {
                Publish(m_ADPlayAgentHelperCompleteEvent, ADPlayAgentHelperCompleteEventArgs.Create(arg.placementId, posID));
            }
            else if (!arg.isRewarded && m_ADPlayAgentHelperErrorEvent != null)
            {
                Publish(m_ADPlayAgentHelperErrorEvent,
                    ADPlayAgentHelperErrorEventArgs.Create(arg.placementId, posID,
                        "Rewarded ad closed without a reward.", "AD_REWARD_NOT_EARNED"));
            }
            });
        }

        /// <summary>
        /// 当激励广告展示失败
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void OnRewardFailure(object sender, ATAdErrorEventArgs e)
        {
            Dispatch(() =>
            {
            if (!TryGetPosID(e.placementId, out string posID))
            {
                return;
            }

            RemovePosID(e.placementId);
            if (m_ADPlayAgentHelperErrorEvent != null)
            {
                Publish(m_ADPlayAgentHelperErrorEvent, ADPlayAgentHelperErrorEventArgs.Create(e.placementId, posID, e.errorMessage, e.errorCode));
            }
            });
        }

        /// <summary>
        /// 当激励广告播放
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="arg"></param>
        private void OnRewardStart(object sender, ATAdEventArgs arg)
        {
            Dispatch(() =>
            {
            if (!TryGetPosID(arg.placementId, out string posID))
            {
                return;
            }

            if (m_ADPlayAgentStartPlayEvent != null)
            {
                Publish(m_ADPlayAgentStartPlayEvent, ADPlayAgentHelperStartPlayEventArgs.Create(arg.placementId, posID));
            }

            if (m_ADPlayAgentHelperShowEvent != null)
            {
                Publish(m_ADPlayAgentHelperShowEvent, ADPlayAgentHelperShowEventArgs.Create(arg.placementId, posID));
            }
            });
        }

    }
}
