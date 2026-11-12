using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using AnyThinkAds.Api;

namespace AnyThinkAds.Common
{
    public interface IATSDKAPIClient
    {
        void initSDK(string appId, string appKey);
        void initSDK(string appId, string appKey, ATSDKInitListener listener);
        void showDebuggerUI();
        void showDebuggerUI(string debugKey);
        void getUserLocation(ATGetUserLocationListener listener);
        /// <summary>Async EU traffic check. Mirrors native <c>ATSDK.checkIsEuTraffic(context, callback, appId)</c>.</summary>
        void checkIsEuTraffic(ATGetUserLocationListener listener, string appId);
        void setGDPRLevel(int level);
        void showGDPRAuth();
        void showGDPRConsentDialog(ATConsentDismissListener listener);
        /// <summary>UMP consent dialog with <paramref name="appId"/> for multi-app setups.</summary>
        void showGDPRConsentDialog(ATConsentDismissListener listener, string appId);
        /// <summary>UMP second-consent / Privacy Options dialog (e.g. user reopens preferences).</summary>
        void showGDPRConsentSecondDialog(ATConsentDismissListener listener, string appId);
        void addNetworkGDPRInfo(int networkType, string mapJson);
        void setChannel(string channel);
        void setSubChannel(string subchannel);
        void initCustomMap(string cutomMap);
        void setCustomDataForPlacementID(string customData, string placementID);
        void setLogDebug(bool isDebug);
        int getGDPRLevel();
        bool isEUTraffic();
        void deniedUploadDeviceInfo(string deniedInfo);

        void setExcludeBundleIdArray(string bundleIds);
        void setExcludeAdSourceIdArrayForPlacementID(string placementID, string adsourceIds);
        void setSDKArea(int area);
        void getArea(ATGetAreaListener listener);
        void setWXStatus(bool install);
        void setLocation(double longitude, double latitude);

        /// <summary>Call after <c>initSDK</c> to kick off strategy fetch. Mirrors native <c>ATSDK.start()</c>.</summary>
        void start();
        /// <summary>Pre-set bundled strategy file path; must be called before SDK starts loading.</summary>
        void setLocalStrategyAssetPath(string assetPath);
        /// <summary>Native SDK version string (Android <c>ATSDK.getSDKVersionName()</c> / iOS <c>+ATSDK sdkVersion</c>).</summary>
        string getSDKVersion();
        /// <summary>Mirrors native <c>ATSDK.setSharedPlacementConfig</c>; <paramref name="configJson"/> is a nested JSON of per-format <c>*LocalExtra</c> maps.</summary>
        void setSharedPlacementConfig(string configJson);

        /// <summary>Network privacy / device-permission policy JSON. Stored on the Android bridge for ad-network controllers; do NOT mix with the waterfall-filter JSON.</summary>
        void setAdSourcePrivacyPolicy(string policyJson);

        /// <summary>Mirrors native <c>ATSDK.putFilter</c>; root object must contain <c>groups</c> array.</summary>
        void putFilter(string placementId, string filterJson);

        /// <summary>Remove waterfall filter for one placement.</summary>
        void removeFilterWithPlacementId(string placementId);

        /// <summary>Remove all waterfall filters.</summary>
        void removeFilters();
    }
}
