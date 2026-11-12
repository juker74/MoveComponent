using System;
using GameFrameX.Runtime;
using System.Collections.Generic;
using AnyThinkAds.Api;
using GameFrameX.Ad;

namespace GameFrameX.Ad.Runtime
{
    public class TopOnPlayInsertPlayAgent : ADPlayAgentHelperBase
    {
        /// <summary>
        /// 正在播放中的广告位对应的场景 ID。
        /// </summary>
        private readonly Dictionary<string, string> m_PosIds = new Dictionary<string, string>();

        private bool m_IsInitialized = false;
        public override ADFormat SupportedFormat => ADFormat.interstitial;

        public override void Initialize()
        {
            if (m_IsInitialized)
            {
                return;
            }

            base.Initialize();
            m_IsInitialized = true;
            //开始展示
            ATInterstitialAd.Instance.client.onAdShowEvent += OnInserADShow;
            //开始播放
            ATInterstitialAd.Instance.client.onAdVideoStartEvent += OnInserADStart;
            //点击
            ATInterstitialAd.Instance.client.onAdClickEvent += OnInserClick;
            //关闭
            ATInterstitialAd.Instance.client.onAdCloseEvent += OnInserADClose;
            //Error
            ATInterstitialAd.Instance.client.onAdVideoFailureEvent += OnInserADDisPlayFailed;
            ATInterstitialAd.Instance.client.onAdShowFailureEvent += OnInserADShowFailure;
        }

        public override void Shutdown()
        {
            if (!m_IsInitialized)
            {
                return;
            }

            ATInterstitialAd.Instance.client.onAdShowEvent -= OnInserADShow;
            //开始播放
            ATInterstitialAd.Instance.client.onAdVideoStartEvent -= OnInserADStart;
            //点击
            ATInterstitialAd.Instance.client.onAdClickEvent -= OnInserClick;
            //关闭
            ATInterstitialAd.Instance.client.onAdCloseEvent -= OnInserADClose;
            //Error
            ATInterstitialAd.Instance.client.onAdVideoFailureEvent -= OnInserADDisPlayFailed;
            ATInterstitialAd.Instance.client.onAdShowFailureEvent -= OnInserADShowFailure;
            m_IsInitialized = false;
            base.Shutdown();
            Reset();
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

            if (!ATInterstitialAd.Instance.hasInterstitialAdReady(placementID))
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
            try { ATInterstitialAd.Instance.showInterstitialAd(placementID, adObject); }
            catch
            {
                RemovePosID(placementID);
                throw;
            }
        }

        public override string CheckAdStatus(string placementID)
        {
            return ATInterstitialAd.Instance.checkAdStatus(placementID);
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

        /// <summary>
        /// 当广告播放完毕
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="arg"></param>


        /// <summary>
        /// 当插屏广告开始
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="arg"></param>
        private void OnInserADStart(object sender, ATAdEventArgs arg)
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
            });
        }

        /// <summary>
        /// 当插屏广告被关闭
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="arg"></param>
        private void OnInserADClose(object sender, ATAdEventArgs arg)
        {
            Dispatch(() =>
            {
            if (!TryGetPosID(arg.placementId, out string posID))
            {
                return;
            }

            RemovePosID(arg.placementId);
            if (m_ADPlayAgentHelperCompleteEvent != null)
            {
                Publish(m_ADPlayAgentHelperCompleteEvent, ADPlayAgentHelperCompleteEventArgs.Create(arg.placementId, posID));
            }
            });
        }

        /// <summary>
        /// 当插屏广告被点击
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="arg"></param>
        private void OnInserClick(object sender, ATAdEventArgs arg)
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

        private void OnInserADDisPlayFailed(object sender, ATAdErrorEventArgs arg)
        {
            Dispatch(() =>
            {
            if (!TryGetPosID(arg.placementId, out string posID))
            {
                return;
            }

            RemovePosID(arg.placementId);
            if (m_ADPlayAgentHelperErrorEvent != null)
            {
                Publish(m_ADPlayAgentHelperErrorEvent, ADPlayAgentHelperErrorEventArgs.Create(arg.placementId, posID, arg.errorMessage,
                        arg.errorCode));
            }
            });
        }

        /// <summary>
        /// 当插屏广告展示失败
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="arg"></param>
        private void OnInserADShowFailure(object sender, ATAdErrorEventArgs arg)
        {
            Dispatch(() =>
            {
            if (!TryGetPosID(arg.placementId, out string posID))
            {
                return;
            }

            RemovePosID(arg.placementId);
            if (m_ADPlayAgentHelperErrorEvent != null)
            {
                Publish(m_ADPlayAgentHelperErrorEvent, ADPlayAgentHelperErrorEventArgs.Create(arg.placementId, posID, arg.errorMessage,
                        arg.errorCode));
            }
            });
        }

        /// <summary>
        /// 当插屏广告开始展示
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="arg"></param>
        private void OnInserADShow(object sender, ATAdEventArgs arg)
        {
            Dispatch(() =>
            {
            if (!TryGetPosID(arg.placementId, out string posID))
            {
                return;
            }

            if (m_ADPlayAgentHelperShowEvent != null)
            {
                Publish(m_ADPlayAgentHelperShowEvent, ADPlayAgentHelperShowEventArgs.Create(arg.placementId, posID));
            }
            });
        }
    }
}
