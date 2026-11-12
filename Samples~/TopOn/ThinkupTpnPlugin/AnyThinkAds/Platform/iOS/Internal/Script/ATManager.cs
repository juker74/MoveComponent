using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System;

public class ATManager {
	private static bool SDKStarted;
	public static bool StartSDK(string appID, string appKey) {
		Debug.Log("Unity: ATManager::StartSDK(" + appID + "," + appKey + ")");
		if (!SDKStarted) {
			Debug.Log("Unity: ATManager:: SDK has not been started before; starting SDK.");
			SDKStarted = true;
			return ATUnityCBridge.SendMessageToC("ATUnityManager", "startSDKWithAppID:appKey:", new object[]{appID, appKey});
		} else {
			Debug.Log("Unity: ATManager:: SDK already started; ignoring duplicate StartSDK.");
            return false;
		}
	}

	public static void setPurchaseFlag() {
        Debug.Log("Unity: ATManager::setPurchaseFlag()");
		ATUnityCBridge.SendMessageToC("ATUnityManager", "setPurchaseFlag", null);
	}

	public static void clearPurchaseFlag() {
        Debug.Log("Unity: ATManager::clearPurchaseFlag()");
		ATUnityCBridge.SendMessageToC("ATUnityManager", "clearPurchaseFlag", null);
	}

	public static bool purchaseFlag() {
        Debug.Log("Unity: ATManager::purchaseFlag()");
		return ATUnityCBridge.SendMessageToC("ATUnityManager", "purchaseFlag", null);
	}

	public static bool isEUTraffic() {
        Debug.Log("Unity: ATManager::isEUTraffic()");
		return ATUnityCBridge.SendMessageToC("ATUnityManager", "inDataProtectionArea", null);
	}

    public static void getUserLocation(Func<string, int> callback, string appId = "")
    {
        Debug.Log("Unity: ATManager::getUserLocation(appId=" + appId + ")");
        ATUnityCBridge.SendMessageToCWithCallBack("ATUnityManager", "checkIsEuTraffic:appID:", new object[] { appId }, callback);
    }

    public static void showGDPRAuth() {
        Debug.Log("Unity: ATManager::showGDPRAuth()");
        ATUnityCBridge.SendMessageToC("ATUnityManager", "showGDPRAuth", null);
    }

	public static void ShowGDPRAuthDialog() {
        Debug.Log("Unity: ATManager::ShowGDPRAuthDialog()");
		ATUnityCBridge.SendMessageToC("ATUnityManager", "presentDataConsentDialog", null);
	}

	public static int GetDataConsent() {
        Debug.Log("Unity: ATManager::GetDataConsent()");
		return ATUnityCBridge.GetMessageFromC("ATUnityManager", "getDataConsent", null);
	}

	public static void SetDataConsent(int consent) {
        Debug.Log("Unity: ATManager::SetDataConsent(consent=" + consent + ")");
		ATUnityCBridge.SendMessageToC("ATUnityManager", "setDataConsent:", new object[]{consent});
	}

	public static void SetNetworkGDPRInfo(int network, string mapJson) {
        Debug.Log("Unity: ATManager::SetNetworkGDPRInfo(network=" + network + ", mapJson length=" + (mapJson != null ? mapJson.Length : 0) + ")");
		ATUnityCBridge.SendMessageToC("ATUnityManager", "setDataConsent:network:", new object[]{mapJson, network});
	}

    public static void setChannel(string channel)
    {
        Debug.Log("Unity: ATManager::setChannel(channel=" + channel + ")");
        ATUnityCBridge.SendMessageToC("ATUnityManager", "setChannel:", new object[] {channel});
    }

    public static void setSubChannel(string subchannel)
    {
        Debug.Log("Unity: ATManager::setSubChannel(subchannel=" + subchannel + ")");
        ATUnityCBridge.SendMessageToC("ATUnityManager", "setSubChannel:", new object[] {subchannel});
    }

    public static void setCustomMap(string jsonMap)
    {
        Debug.Log("Unity: ATManager::setCustomMap(jsonMap length=" + (jsonMap != null ? jsonMap.Length : 0) + ")");
        ATUnityCBridge.SendMessageToC("ATUnityManager", "setCustomData:", new object[] { jsonMap });
    }

    public static void setCustomDataForPlacementID(string customData, string placementID)
    {
        Debug.Log("Unity: ATManager::setCustomDataForPlacementID(placementID=" + placementID + ", customData length=" + (customData != null ? customData.Length : 0) + ")");
        ATUnityCBridge.SendMessageToC("ATUnityManager", "setCustomData:forPlacementID:", new object[] {customData, placementID});
    }

    public static void setLogDebug(bool isDebug)
    {
        Debug.Log("Unity: ATManager::setLogDebug(isDebug=" + isDebug + ")");
        ATUnityCBridge.SendMessageToC("ATUnityManager", "setDebugLog:", new object[] { isDebug ? "true" : "false" });
    }

    public static void deniedUploadDeviceInfo(string deniedInfo)
    {
        Debug.Log("Unity: ATManager::deniedUploadDeviceInfo(deniedInfo length=" + (deniedInfo != null ? deniedInfo.Length : 0) + ")");
        ATUnityCBridge.SendMessageToC("ATUnityManager", "deniedUploadDeviceInfo:", new object[] {deniedInfo});
    }

    public static void setExcludeBundleIdArray(string bundleIds)
    {
        Debug.Log("Unity: ATManager::setExcludeBundleIdArray(bundleIds length=" + (bundleIds != null ? bundleIds.Length : 0) + ")");
        ATUnityCBridge.SendMessageToC("ATUnityManager", "setExcludeBundleIdArray:", new object[] {bundleIds});
    }

    public static void setExcludeAdSourceIdArrayForPlacementID(string placementID, string adSourceIds) 
    {
        Debug.Log("Unity: ATManager::setExcludeAdSourceIdArrayForPlacementID(placementID=" + placementID + ", adSourceIds length=" + (adSourceIds != null ? adSourceIds.Length : 0) + ")");
        ATUnityCBridge.SendMessageToC("ATUnityManager", "setExludePlacementid:unitIDArray:", new object[] {placementID, adSourceIds});
    }
    
    public static void setSDKArea(int area)
    {
        Debug.Log("Unity: ATManager::setSDKArea(area=" + area + ")");
        ATUnityCBridge.SendMessageToC("ATUnityManager", "setSDKArea:", new object[] {area});
    }
    
    public static void getArea(Func<string, int> callback)
    {
        Debug.Log("Unity: ATManager::getArea()");
        ATUnityCBridge.SendMessageToCWithCallBack("ATUnityManager", "getArea:", new object[] { }, callback);
    }
    
    public static void setWXStatus(bool install)
    {
        Debug.Log("Unity: ATManager::setWXStatus(install=" + install + ")");
    	ATUnityCBridge.SendMessageToC("ATUnityManager", "setWXStatus:", new object[] {install});
    }
    
    public static void setLocation(double longitude, double latitude)
    {
        Debug.Log("Unity: ATManager::setLocation(longitude=" + longitude + ", latitude=" + latitude + ")");
    	ATUnityCBridge.SendMessageToC("ATUnityManager", "setLocationLongitude:dimension:", new object[] {longitude, latitude});
    }

    public static void showDebuggerUI(string debugKey) 
    {
        Debug.Log("Unity: ATManager::showDebuggerUI(debugKey=" + (debugKey ?? "null") + ")");
		ATUnityCBridge.SendMessageToC("ATUnityManager", "showDebuggerUI:", new object[] {debugKey});
	}

    public static void showGDPRConsentDialog(Func<string, int> callback, string appId = "") 
    {
        Debug.Log("Unity: ATManager::showGDPRConsentDialog(appId=" + appId + ")");
        ATUnityCBridge.SendMessageToCWithCallBack("ATUnityManager", "showGDPRConsentDialog:appID:", new object[] { appId }, callback);
	}
    
    public static void showGDPRSecondConsentDialog(Func<string, int> callback, string appId = "") 
    {
        Debug.Log("Unity: ATManager::showGDPRSecondConsentDialog(appId=" + appId + ")");
        ATUnityCBridge.SendMessageToCWithCallBack("ATUnityManager", "showGDPRSecondConsentDialog:appID:", new object[] { appId }, callback);
	}

}
