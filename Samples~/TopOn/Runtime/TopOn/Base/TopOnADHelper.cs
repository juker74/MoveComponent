using System;
using System.Collections.Generic;
using GameFrameX.Runtime;
using AnyThinkAds.Api;
using GameFrameX.Ad;
using UnityEngine;

namespace GameFrameX.Ad.Runtime
{
    /// <summary>
    /// TopOn SDK 初始化参数（供代码侧调用）
    /// </summary>
    [Serializable]
    public class TopOnInitParams
    {
        [field: SerializeField]
        public string AppId { get; set; }

        [field: SerializeField]
        public string AppKey { get; set; }
    }

    [Serializable]
    public struct TopOnPlatformAppConfig
    {
        [Tooltip("目标平台")] public RuntimePlatform Platform;
        [Tooltip("TopOn App Id")] public string AppId;
        [Tooltip("TopOn App Key")] public string AppKey;
    }

    [Serializable]
    public class TopOnADHelper : ADHelperBase
    {
        private bool initialized;
        private const string helperName = "topon";

        [SerializeField]
        [Tooltip("各平台 TopOn App Id 和 App Key")]
        private List<TopOnPlatformAppConfig> m_PlatformConfigs = new List<TopOnPlatformAppConfig>();

        public override string Name => helperName;

        public override void Init(object userData)
        {
            if (Application.isEditor) return;
            if (initialized) return;
            string appId;
            string appKey;

            switch (userData)
            {
                case TopOnInitParams initParams:
                    appId = initParams.AppId;
                    appKey = initParams.AppKey;
                    break;

                case null:
                    if (!TryGetPlatformCredentials(Application.platform, out appId, out appKey))
                    {
                        throw new GameFrameworkException(Utility.Text.Format(
                            "[TopOnADHelper] Init failed: no AppId/AppKey configuration for platform {0}.",
                            Application.platform));
                    }
                    break;

                default:
                    throw new GameFrameworkException(Utility.Text.Format("[TopOnADHelper] Init failed: userData must be TopOnInitParams or null, got {0}.", userData.GetType()));
            }

            if (string.IsNullOrEmpty(appId) || string.IsNullOrEmpty(appKey))
            {
                throw new GameFrameworkException("[TopOnADHelper] Init failed: AppId or AppKey is empty.");
            }

            initialized = false;
            ATSDKAPI.initSDK(appId, appKey, new SdkInitListener(this));
            ATSDKAPI.setLogDebug(Debug.isDebugBuild);
            Debug.Log("[TopOnADHelper] SDK initialization requested.");
        }

        private sealed class SdkInitListener : ATSDKInitListener
        {
            private readonly TopOnADHelper owner;

            internal SdkInitListener(TopOnADHelper owner)
            {
                this.owner = owner;
            }

            public void initSuccess()
            {
                owner.initialized = true;
                Debug.Log("[TopOnADHelper] SDK initialization succeeded.");
#if DEVELOPMENT_BUILD && (UNITY_ANDROID || UNITY_IOS)
                ATSDKAPI.showDebuggerUI();
#endif
            }

            public void initFail(string message)
            {
                owner.initialized = false;
                Debug.LogError($"[TopOnADHelper] SDK initialization failed: {message}");
            }
        }

        private bool TryGetPlatformCredentials(RuntimePlatform platform, out string appId, out string appKey)
        {
            foreach (TopOnPlatformAppConfig config in m_PlatformConfigs)
            {
                if (config.Platform != platform)
                    continue;

                appId = config.AppId;
                appKey = config.AppKey;
                return true;
            }

            appId = string.Empty;
            appKey = string.Empty;
            return false;
        }
    }
}
