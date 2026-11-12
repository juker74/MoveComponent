using System;
using System.Collections.Generic;
using AnyThinkAds.Api;
using GameFrameX.Ad;

namespace GameFrameX.Ad.Runtime
{
    public sealed class TopOnRequestSplashAgent : ADRequestAgentHelperBase
    {
        private readonly HashSet<string> m_LoadingPlacements = new HashSet<string>();
        private bool m_IsInitialized;

        public override ADFormat SupportedFormat => ADFormat.splash;

        public override void Initialize()
        {
            if (m_IsInitialized) return;

            base.Initialize();
            m_IsInitialized = true;
            ATSplashAd.Instance.client.onAdLoadEvent += OnSplashAdLoad;
            ATSplashAd.Instance.client.onAdLoadFailureEvent += OnSplashAdError;
        }

        public override void Shutdown()
        {
            if (!m_IsInitialized) return;

            ATSplashAd.Instance.client.onAdLoadEvent -= OnSplashAdLoad;
            ATSplashAd.Instance.client.onAdLoadFailureEvent -= OnSplashAdError;
            m_IsInitialized = false;
            base.Shutdown();
            Reset();
        }

        public override void Request(string placementID, bool forceLoad, object userData)
        {
            if (!forceLoad && ATSplashAd.Instance.hasSplashAdReady(placementID))
            {
                PublishComplete(placementID);
                return;
            }

            if (!m_LoadingPlacements.Add(placementID)) return;

            try
            {
                ATSplashAd.Instance.loadSplashAd(placementID, new Dictionary<string, object>());
            }
            catch
            {
                m_LoadingPlacements.Remove(placementID);
                throw;
            }
        }

        public override string CheckAdStatus(string placementID) => ATSplashAd.Instance.checkAdStatus(placementID);

        public override double GetAdValue(string placementID)
        {
            string json = CheckAdStatus(placementID);
            if (string.IsNullOrEmpty(json)) return 0;
            return new ATCallbackInfo(json).publisher_revenue;
        }

        public override bool IsAdReady(string placementID) => ATSplashAd.Instance.hasSplashAdReady(placementID);

        public override void Reset() => m_LoadingPlacements.Clear();

        private void OnSplashAdLoad(object sender, ATAdEventArgs e)
        {
            Dispatch(() =>
            {
                if (string.IsNullOrEmpty(e.placementId)) return;
                m_LoadingPlacements.Remove(e.placementId);
                PublishComplete(e.placementId);
            });
        }

        private void OnSplashAdError(object sender, ATAdErrorEventArgs e)
        {
            Dispatch(() =>
            {
                if (string.IsNullOrEmpty(e.placementId)) return;
                m_LoadingPlacements.Remove(e.placementId);
                if (m_ADRequestAgentHelperError != null)
                {
                    Publish(m_ADRequestAgentHelperError,
                        ADRequestAgentHelperErrorEventArgs.Create(e.placementId, e.errorCode, e.errorMessage));
                }
            });
        }

        private void PublishComplete(string placementID)
        {
            if (m_ADRequestAgentHelperComplete != null)
            {
                Publish(m_ADRequestAgentHelperComplete,
                    ADRequestAgentHelperCompleteEventArgs.Create(placementID, ATSplashAd.Instance));
            }
        }
    }
}
