using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using AnyThinkAds.Common;
using AnyThinkAds.Api;
using AOT;
using System;
using AnyThinkAds.ThirdParty.LitJson;

namespace AnyThinkAds.iOS {
	public class ATSDKAPIClient : IATSDKAPIClient {
        static private ATGetUserLocationListener locationListener;
        static private ATGetAreaListener areaListener;

        static private ATConsentDismissListener umpListener;

        public ATSDKAPIClient () {
            Debug.Log("Unity: ATSDKAPIClient::ATSDKAPIClient()");
		}
		public void initSDK(string appId, string appKey) {
			Debug.Log("Unity: ATSDKAPIClient::initSDK(appId=" + appId + ", appKey length=" + (appKey != null ? appKey.Length : 0) + ")");
			initSDK(appId, appKey, null);
	    }

	    public void initSDK(string appId, string appKey, ATSDKInitListener listener) {
	    	Debug.Log("Unity: ATSDKAPIClient::initSDK(appId=" + appId + ", appKey length=" + (appKey != null ? appKey.Length : 0) + ", listener=" + (listener != null ? "set" : "null") + ")");
	    	bool started = ATManager.StartSDK(appId, appKey);
            if (listener != null)
            {
                if (started)
                {
                    listener.initSuccess();
                }
                else
                {
                    listener.initFail("Failed to init.");
                }
            }
	    }

        [MonoPInvokeCallback(typeof(Func<string, int>))]
       static public int DidGetUserLocation(string location)
        {
            if (locationListener != null) { locationListener.didGetUserLocation(Int32.Parse(location)); }
            return 0;
        }

        [MonoPInvokeCallback(typeof(Func<string, int>))]
       static public int DidUMP(string location)
        {
            if (umpListener != null) { umpListener.onConsentDismiss(ATConsentDismissInfo.Empty); }
            return 0;
        }

        [MonoPInvokeCallback(typeof(Func<string, int>))]
        static public int GetAreaInfo(string msg)
        {
            Debug.Log("Unity: ATSDKAPIClient::GetAreaInfo(msg=" + msg + ")");
            if (areaListener != null) 
            { 
                JsonData msgJsonData = JsonMapper.ToObject(msg);
                IDictionary idic = (System.Collections.IDictionary)msgJsonData;

                if (idic.Contains("areaCode")) {
                    string areaCode = (string)msgJsonData["areaCode"];
                    Debug.Log("Unity: ATSDKAPIClient::GetAreaInfo::areaCode(" + areaCode + ")");
                    areaListener.onArea(areaCode);
                }
                
                if (idic.Contains("errorMsg")) { 
                    string errorMsg = (string)msgJsonData["errorMsg"];
                    Debug.Log("Unity: ATSDKAPIClient::GetAreaInfo::errorMsg(" + errorMsg + ")");
                    areaListener.onError(errorMsg);
                }
            }
            return 0;
        }

        public void getUserLocation(ATGetUserLocationListener listener)
        {
            Debug.Log("Unity: ATSDKAPIClient::getUserLocation()");
            checkIsEuTraffic(listener, null);
        }

        public void checkIsEuTraffic(ATGetUserLocationListener listener, string appId)
        {
            Debug.Log("Unity: ATSDKAPIClient::checkIsEuTraffic(appId=" + (appId ?? "null") + ")");
            ATSDKAPIClient.locationListener = listener;
            ATManager.getUserLocation(DidGetUserLocation, appId ?? "");
        }

        public void setGDPRLevel(int level) {
	    	Debug.Log("Unity: ATSDKAPIClient::setGDPRLevel(level=" + level + ")");
	    	ATManager.SetDataConsent(level);
	    }

	    public void showGDPRAuth() {
	    	Debug.Log("Unity: ATSDKAPIClient::showGDPRAuth()");
	    	// ATManager.showGDPRAuth();
	    }

        public void showGDPRConsentDialog(ATConsentDismissListener listener)
        {
            showGDPRConsentDialog(listener, null);
        }

        public void showGDPRConsentDialog(ATConsentDismissListener listener, string appId)
        {
            Debug.Log("Unity: ATSDKAPIClient::showGDPRConsentDialog(appId=" + (appId ?? "null") + ")");
            ATSDKAPIClient.umpListener = listener;
            ATManager.showGDPRConsentDialog(DidUMP, appId ?? "");
        }

        public void showGDPRConsentSecondDialog(ATConsentDismissListener listener, string appId)
        {
            Debug.Log("Unity: ATSDKAPIClient::showGDPRConsentSecondDialog(appId=" + (appId ?? "null") + ")");
            ATSDKAPIClient.umpListener = listener;
            ATManager.showGDPRSecondConsentDialog(DidUMP, appId ?? "");
        }

        public void setPurchaseFlag() {
            Debug.Log("Unity: ATSDKAPIClient::setPurchaseFlag()");
			ATManager.setPurchaseFlag();
		}

		public void clearPurchaseFlag() {
            Debug.Log("Unity: ATSDKAPIClient::clearPurchaseFlag()");
			ATManager.clearPurchaseFlag();
		}

		public bool purchaseFlag() {
            Debug.Log("Unity: ATSDKAPIClient::purchaseFlag()");
			return ATManager.purchaseFlag();
		}

	    public void addNetworkGDPRInfo(int networkType, string mapJson) {
	    	Debug.Log("Unity: ATSDKAPIClient::addNetworkGDPRInfo(networkType=" + networkType + ", mapJson length=" + (mapJson != null ? mapJson.Length : 0) + ")");
	    	ATManager.SetNetworkGDPRInfo(networkType, mapJson);
	    }

        public void setChannel(string channel)
        {
            Debug.Log("Unity: ATSDKAPIClient::setChannel(channel=" + channel + ")");
            ATManager.setChannel(channel);
        }

        public void setSubChannel(string subchannel)
        {
            Debug.Log("Unity: ATSDKAPIClient::setSubChannel(subchannel=" + subchannel + ")");
            ATManager.setSubChannel(subchannel);
        }

        public void initCustomMap(string jsonMap)
        {
            Debug.Log("Unity: ATSDKAPIClient::initCustomMap(jsonMap length=" + (jsonMap != null ? jsonMap.Length : 0) + ")");
            ATManager.setCustomMap(jsonMap);
        }

        public void setCustomDataForPlacementID(string customData, string placementID)
        {
            Debug.Log("Unity: ATSDKAPIClient::setCustomDataForPlacementID(placementID=" + placementID + ", customData length=" + (customData != null ? customData.Length : 0) + ")");
            ATManager.setCustomDataForPlacementID(customData, placementID);
        }

        public void setLogDebug(bool isDebug)
        {
            Debug.Log("Unity: ATSDKAPIClient::setLogDebug(isDebug=" + isDebug + ")");
            ATManager.setLogDebug(isDebug);
        }

        public int getGDPRLevel()
        {
            Debug.Log("Unity: ATSDKAPIClient::getGDPRLevel()");
            return ATManager.GetDataConsent();
        }

        public bool isEUTraffic()
        {
            Debug.Log("Unity: ATSDKAPIClient::isEUTraffic()");
            return ATManager.isEUTraffic();
        }

        public void deniedUploadDeviceInfo(string deniedInfo)
        {
            Debug.Log("Unity: ATSDKAPIClient::deniedUploadDeviceInfo(deniedInfo length=" + (deniedInfo != null ? deniedInfo.Length : 0) + ")");
            ATManager.deniedUploadDeviceInfo(deniedInfo);
        }

        public void setExcludeBundleIdArray(string bundleIds)
        {
            Debug.Log("Unity: ATSDKAPIClient::setExcludeBundleIdArray(bundleIds length=" + (bundleIds != null ? bundleIds.Length : 0) + ")");
            ATManager.setExcludeBundleIdArray(bundleIds);
        }

        public void setExcludeAdSourceIdArrayForPlacementID(string placementID, string adSourceIds) 
        {
            Debug.Log("Unity: ATSDKAPIClient::setExcludeAdSourceIdArrayForPlacementID(placementID=" + placementID + ", adSourceIds length=" + (adSourceIds != null ? adSourceIds.Length : 0) + ")");
            ATManager.setExcludeAdSourceIdArrayForPlacementID(placementID, adSourceIds);
        }
        
        public void setSDKArea(int area)
        {
            Debug.Log("Unity: ATSDKAPIClient::setSDKArea(area=" + area + ")");
            ATManager.setSDKArea(area);
        }
        
        public void getArea(ATGetAreaListener listener)
        {
            Debug.Log("Unity: ATSDKAPIClient::getArea()");
            ATSDKAPIClient.areaListener = listener;
            ATManager.getArea(GetAreaInfo);
        }
        
        public void setWXStatus(bool install)
        {
            Debug.Log("Unity: ATSDKAPIClient::setWXStatus(install=" + install + ")");
            ATManager.setWXStatus(install);
        }
        
        public void setLocation(double longitude, double latitude)
        {
            Debug.Log("Unity: ATSDKAPIClient::setLocation(longitude=" + longitude + ", latitude=" + latitude + ")");
            ATManager.setLocation(longitude, latitude);
        }
        
        //iOS显示Debugger UI
        public void showDebuggerUI() 
        {
            Debug.Log("Unity: ATSDKAPIClient::showDebuggerUI()");
            ATManager.showDebuggerUI("");
        }

        public void showDebuggerUI(string debugKey) 
        {
            Debug.Log("Unity: ATSDKAPIClient::showDebuggerUI(debugKey=" + (debugKey ?? "null") + ")");
            ATManager.showDebuggerUI(debugKey);
        }

        public void start()
        {
            Debug.Log("Unity: ATSDKAPIClient::start() — iOS 上与 Android ATSDK.start() 无对等接口；策略拉取已在 initSDK/startWithAppID 中触发。");
        }

        public void setLocalStrategyAssetPath(string assetPath)
        {
            Debug.Log("Unity: ATSDKAPIClient::setLocalStrategyAssetPath(assetPath=" + assetPath + ")");
            ATUnityCBridge.SendMessageToC("ATUnityManager", "setLocalStrategyAssetPath:", new object[] { assetPath });
        }

        public string getSDKVersion()
        {
            var version = ATUnityCBridge.GetStringMessageFromC("ATUnityManager", "getSDKVersionName", null);
            if (version == null || version.Length == 0) {
                version = "";
            }
            Debug.Log("Unity: ATSDKAPIClient::getSDKVersion() → " + version);
            return version;
        }

        public void setSharedPlacementConfig(string configJson)
        {
            Debug.Log("Unity: ATSDKAPIClient::setSharedPlacementConfig(jsonLength=" + (configJson != null ? configJson.Length : 0) + ")");
            ATUnityCBridge.SendMessageToC("ATUnityManager", "setSharedPlacementConfig:", new object[] { configJson });
        }

        public void setAdSourcePrivacyPolicy(string policyJson)
        {
            Debug.Log("Unity: ATSDKAPIClient::setAdSourcePrivacyPolicy(jsonLength=" + (policyJson != null ? policyJson.Length : 0) + ")");
            ATUnityCBridge.SendMessageToC("ATUnityManager", "setAdSourcePrivacyPolicy:", new object[] { policyJson });
        }

        public void putFilter(string placementId, string filterJson)
        {
            Debug.Log("Unity: ATSDKAPIClient::putFilter(placementId=" + placementId + ", filterJson length=" + (filterJson != null ? filterJson.Length : 0) + ")");
            ATUnityCBridge.SendMessageToC("ATUnityManager", "putFilter:filterJson:", new object[] { placementId, filterJson });
        }

        public void removeFilterWithPlacementId(string placementId)
        {
            Debug.Log("Unity: ATSDKAPIClient::removeFilterWithPlacementId(placementId=" + placementId + ")");
            ATUnityCBridge.SendMessageToC("ATUnityManager", "removeFilterWithPlacementId:", new object[] { placementId });
        }

        public void removeFilters()
        {
            Debug.Log("Unity: ATSDKAPIClient::removeFilters()");
            ATUnityCBridge.SendMessageToC("ATUnityManager", "removeFilters", null);
        }
	}
}
