using System;
using System.Collections.Generic;
using AnyThinkAds.Api;
using GameFrameX.Ad;

namespace GameFrameX.Ad.Runtime
{
    public sealed class TopOnPlaySplashPlayAgent : ADPlayAgentHelperBase
    {
        private readonly Dictionary<string, string> m_PosIds = new Dictionary<string, string>();
        private bool m_IsInitialized;

        public override ADFormat SupportedFormat => ADFormat.splash;

        public override void Initialize()
        {
            if (m_IsInitialized) return;

            base.Initialize();
            m_IsInitialized = true;
            ATSplashAd.Instance.client.onAdShowEvent += OnSplashAdShow;
            ATSplashAd.Instance.client.onAdClickEvent += OnSplashAdClick;
            ATSplashAd.Instance.client.onAdCloseEvent += OnSplashAdClose;
            ATSplashAd.Instance.client.onAdShowFailureEvent += OnSplashAdShowFailure;
        }

        public override void Shutdown()
        {
            if (!m_IsInitialized) return;

            ATSplashAd.Instance.client.onAdShowEvent -= OnSplashAdShow;
            ATSplashAd.Instance.client.onAdClickEvent -= OnSplashAdClick;
            ATSplashAd.Instance.client.onAdCloseEvent -= OnSplashAdClose;
            ATSplashAd.Instance.client.onAdShowFailureEvent -= OnSplashAdShowFailure;
            m_IsInitialized = false;
            base.Shutdown();
            Reset();
        }

        public override void Play(string placementID, string posID, object userData)
        {
            if (m_PosIds.ContainsKey(placementID))
            {
                PublishError(placementID, posID, "Placement is already playing.", "AD_ALREADY_PLAYING");
                return;
            }

            if (m_ADPlayAgentHelperStartEvent != null)
            {
                Publish(m_ADPlayAgentHelperStartEvent,
                    ADPlayAgentHelperStartEventArgs.Create(placementID, posID));
            }

            if (!ATSplashAd.Instance.hasSplashAdReady(placementID))
            {
                PublishError(placementID, posID, "Placement is not ready.", "AD_NOT_READY");
                return;
            }

            m_PosIds.Add(placementID, posID);
            try
            {
                ATSplashAd.Instance.showSplashAd(placementID,
                    new Dictionary<string, string> { { ATConst.SCENARIO, posID } });
            }
            catch
            {
                RemovePosID(placementID);
                throw;
            }
        }

        public override string CheckAdStatus(string placementID) => ATSplashAd.Instance.checkAdStatus(placementID);

        public override void Reset() => m_PosIds.Clear();

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
            if (!string.IsNullOrEmpty(placementID)) m_PosIds.Remove(placementID);
        }

        private void OnSplashAdShow(object sender, ATAdEventArgs e)
        {
            Dispatch(() =>
            {
                if (!TryGetPosID(e.placementId, out string posID)) return;
                if (m_ADPlayAgentHelperShowEvent != null)
                {
                    Publish(m_ADPlayAgentHelperShowEvent,
                        ADPlayAgentHelperShowEventArgs.Create(e.placementId, posID));
                }

                if (m_ADPlayAgentStartPlayEvent != null)
                {
                    Publish(m_ADPlayAgentStartPlayEvent,
                        ADPlayAgentHelperStartPlayEventArgs.Create(e.placementId, posID));
                }
            });
        }

        private void OnSplashAdClick(object sender, ATAdEventArgs e)
        {
            Dispatch(() =>
            {
                if (!TryGetPosID(e.placementId, out string posID)) return;
                if (m_ADPlayAgentHelperClickEvent != null)
                {
                    Publish(m_ADPlayAgentHelperClickEvent,
                        ADPlayAgentHelperClickEventArgs.Create(e.placementId, posID));
                }
            });
        }

        private void OnSplashAdClose(object sender, ATAdEventArgs e)
        {
            Dispatch(() =>
            {
                if (!TryGetPosID(e.placementId, out string posID)) return;
                RemovePosID(e.placementId);
                if (m_ADPlayAgentHelperCompleteEvent != null)
                {
                    Publish(m_ADPlayAgentHelperCompleteEvent,
                        ADPlayAgentHelperCompleteEventArgs.Create(e.placementId, posID));
                }
            });
        }

        private void OnSplashAdShowFailure(object sender, ATAdErrorEventArgs e)
        {
            Dispatch(() =>
            {
                if (!TryGetPosID(e.placementId, out string posID)) return;
                RemovePosID(e.placementId);
                PublishError(e.placementId, posID, e.errorMessage, e.errorCode);
            });
        }

        private void PublishError(string placementID, string posID, string message, string code)
        {
            if (m_ADPlayAgentHelperErrorEvent != null)
            {
                Publish(m_ADPlayAgentHelperErrorEvent,
                    ADPlayAgentHelperErrorEventArgs.Create(placementID, posID, message, code));
            }
        }
    }
}
