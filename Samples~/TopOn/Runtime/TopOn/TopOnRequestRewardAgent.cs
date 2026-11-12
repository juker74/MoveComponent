using System;
using GameFrameX.Runtime;
using System.Collections.Generic;
using AnyThinkAds.Api;
using GameFrameX.Ad;

namespace GameFrameX.Ad.Runtime
{
    public class TopOnRequestRewardAgent : ADRequestAgentHelperBase
    {
        /// <summary>
        /// 正在加载中的广告位集合（每个广告位只需一次 SDK 调用）
        /// </summary>
        private readonly HashSet<string> m_LoadingPlacements = new HashSet<string>();

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
            ATRewardedVideo.Instance.client.onAdLoadEvent += OnRewardADLoad;
            ATRewardedVideo.Instance.client.onAdLoadFailureEvent += OnRewardADError;
        }

        public override void Shutdown()
        {
            if (!m_IsInitialized)
            {
                return;
            }

            ATRewardedVideo.Instance.client.onAdLoadEvent -= OnRewardADLoad;
            ATRewardedVideo.Instance.client.onAdLoadFailureEvent -= OnRewardADError;
            m_IsInitialized = false;
            base.Shutdown();
            Reset();
        }

        public override void Request(string placementID, bool forceLoad, object userData)
        {
            // 检查是否已有就绪广告
            if (!forceLoad && ATRewardedVideo.Instance.hasAdReady(placementID))
            {
                if (m_ADRequestAgentHelperComplete != null)
                {
                    Publish(m_ADRequestAgentHelperComplete, ADRequestAgentHelperCompleteEventArgs.Create(placementID, ATRewardedVideo.Instance));
                }
                return;
            }

            // 如果该广告位已在加载中，不重复调用 SDK
            if (m_LoadingPlacements.Contains(placementID))
            {
                return;
            }

            m_LoadingPlacements.Add(placementID);

            Dictionary<string, string> jsonmap = new Dictionary<string, string>();
            jsonmap.Add(ATConst.USERID_KEY, "");
            jsonmap.Add(ATConst.USER_EXTRA_DATA, "");
            try { ATRewardedVideo.Instance.loadVideoAd(placementID, jsonmap); }
            catch
            {
                m_LoadingPlacements.Remove(placementID);
                throw;
            }
        }

        public override string CheckAdStatus(string placementID)
        {
            return ATRewardedVideo.Instance.checkAdStatus(placementID);
        }

        public override double GetAdValue(string placementID)
        {
            string json = ATRewardedVideo.Instance.checkAdStatus(placementID);
            if (string.IsNullOrEmpty(json)) return 0;
            var info = new ATCallbackInfo(json);
            return info.publisher_revenue;
        }

        public override bool IsAdReady(string placementID)
        {
            return ATRewardedVideo.Instance.hasAdReady(placementID);
        }

        public override void Reset()
        {
            m_LoadingPlacements.Clear();
        }

        private void OnRewardADLoad(object sender, ATAdEventArgs e)
        {
            Dispatch(() =>
            {
            if (string.IsNullOrEmpty(e.placementId))
                return;

            m_LoadingPlacements.Remove(e.placementId);

            if (m_ADRequestAgentHelperComplete != null)
            {
                Publish(m_ADRequestAgentHelperComplete, ADRequestAgentHelperCompleteEventArgs.Create(e.placementId, ATRewardedVideo.Instance));
            }
            });
        }

        private void OnRewardADError(object sender, ATAdErrorEventArgs e)
        {
            Dispatch(() =>
            {
            if (string.IsNullOrEmpty(e.placementId))
                return;

            m_LoadingPlacements.Remove(e.placementId);

            if (m_ADRequestAgentHelperError != null)
            {
                Publish(m_ADRequestAgentHelperError, ADRequestAgentHelperErrorEventArgs.Create(e.placementId, e.errorCode, e.errorMessage));
            }
            });
        }
    }
}
