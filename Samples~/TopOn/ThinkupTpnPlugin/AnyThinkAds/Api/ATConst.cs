namespace AnyThinkAds.Api
{
    public class ATConst
    {
        public const string ADAPTIVE_HEIGHT = "AdaptiveHeight"; // value is string
        public const string ADAPTIVE_HEIGHT_YES = "1"; // value is string
        public const string POSITION = "Position"; // value is string
        public const string POSITION_TOP = "Top"; // value is string
        public const string POSITION_BOTTOM = "Bottom"; // value is string

        public const string SCENARIO = "Scenario"; // value is string
        public const string USERID_KEY = "UserId"; // value is string
        public const string USER_EXTRA_DATA = "UserExtraData"; // value is string
        public const string USE_REWARDED_VIDEO_AS_INTERSTITIAL = "UseRewardedVideoAsInterstitial"; // value is string
        public const string USE_REWARDED_VIDEO_AS_INTERSTITIAL_YES = "1"; // value is string
        public const string USE_REWARDED_VIDEO_AS_INTERSTITIAL_NO = "0"; // value is string

        public const string WIDTH = "Width"; // value is string
        public const string HEIGHT = "Height"; // value is string

        public const string AT_AD_REQUEST = "atAdRequest"; // value is object

        // Ad source privacy policy keys
        public static class AdSourcePrivacyPolicy
        {
            public const string IS_CAN_USE_APP_LIST = "isCanUseAppList"; // value is bool
            public const string IS_CAN_USE_GENERAL_DATA = "isCanUseGeneralData"; // value is bool
            public const string IS_CAN_USE_WIFI_STATE = "isCanUseWifiState"; // value is bool
            public const string IS_CAN_USE_MAC_ADDRESS = "isCanUseMacAddress"; // value is bool
            public const string IS_CAN_USE_WRITE_EXTERNAL = "isCanUseWriteExternal"; // value is bool
            public const string IS_CAN_USE_PERMISSION_RECORD_AUDIO = "isCanUsePermissionRecordAudio"; // value is bool
            public const string IS_CAN_USE_ANDROID_ID = "isCanUseAndroidId"; // value is bool
            public const string IS_CAN_USE_OAID = "isCanUseOaid"; // value is bool
            public const string IS_CAN_USE_LOCATION = "isCanUseLocation"; // value is bool
            public const string IS_CAN_USE_PHONE_STATE = "isCanUsePhoneState"; // value is bool
            public const string AGREE_PRIVACY_STRATEGY = "agreePrivacyStrategy"; // value is bool
            public const string ID_ALL_SWITCH = "idAllSwitch"; // value is bool
            public const string IS_CAN_USE_IP = "isCanUseIp"; // value is bool
            public const string IS_CAN_PERSONAL_RECOMMEND = "isCanPersonalRecommend"; // value is bool
            public const string IS_CAN_SHAKE = "isCanShake"; // value is bool
            public const string FORBID_SENSOR = "forbidSensor"; // value is bool
            public const string IS_ALLOW_HARD_DISK_SIZE_KBYTES = "isAllowHardDiskSizeKBytes"; // value is bool
            public const string IS_CAN_USE_IDFA = "isCanUseIdfa"; // value is bool (iOS)

            public const string CUSTOM_ANDROID_ID = "customAndroidId"; // value is string
            public const string CUSTOM_IMEI = "customIMEI"; // value is string
            public const string CUSTOM_OAID = "customOaid"; // value is string
            public const string CUSTOM_MAC_ADDRESS = "customMacAddress"; // value is string
            public const string CUSTOM_IDFA = "customIDFA"; // value is string
            public const string CUSTOM_IP = "customIp"; // value is string
            public const string CUSTOM_LOCATION = "customLocation"; // value is object

            public const string NETWORK_FIRM_IDS = "networkFirmIds"; // value is int[]
            public const string INSTALLED_PACKAGE_NAMES = "installedPackageNames"; // value is string[]
            public const string SHAKE_VALUE = "shakeValue"; // value is object

            public static class CustomLocation
            {
                public const string LATITUDE = "latitude"; // value is double
                public const string LONGITUDE = "longitude"; // value is double
            }

            public static class ShakeValue
            {
                public const string ACCELERATION = "acceleration"; // value is double
                public const string ANGLE = "angle"; // value is double
                public const string TIME = "time"; // value is int
            }
        }

        // Waterfall filter keys
        public static class PutFilter
        {
            public const string GROUPS = "groups"; // value is List<object>

            public static class Group
            {
                public const string NETWORK_ID = "networkId"; // value is List<string>
                public const string BIDDING_TYPE = "biddingType"; // value is List<string>
                public const string NETWORK_PLACEMENT_ID = "networkPlacementId"; // value is List<string>
                public const string E_CPM = "e_cpm"; // value is object
            }

            public static class ECpm
            {
                public const string CURRENCY = "currency"; // value is string
                public const string MORE_THAN_PRICE = "moreThanPrice"; // value is double
                public const string LESS_THAN_PRICE = "lessThanPrice"; // value is double
            }

            public static class BidType
            {
                public const string NORMAL = "NORMAL"; // value is string
                public const string C2S = "C2S"; // value is string
                public const string S2S = "S2S"; // value is string
            }

            public static class Currency
            {
                public const string USD = "USD"; // value is string
                public const string RMB = "RMB"; // value is string
                public const string RMB_CENT = "RMB_CENT"; // value is string
            }
        }

        // ATAdRequest keys (nested under atAdRequest)
        public static class AtAdRequest
        {
            public const string CHANNEL_SOURCE = "channelSource"; // value is int
            public const string ADX_BID_FLOOR_INFO = "adxBidFloorInfo"; // value is object
            public const string PRE_LOAD_INFO = "preLoadInfo"; // value is object

            public static class AdxBidFloorInfo
            {
                public const string BID_FLOOR = "bidFloor"; // value is double
                public const string CURRENCY = "currency"; // value is string
                public const string EXTRA_MAP = "extraMap"; // value is object
            }

            public static class PreLoadInfo
            {
                public const string REQUEST_ID = "requestId"; // value is string
                public const string PS_ID = "psId"; // value is string
                public const string PLACEMENT_ID = "placementId"; // value is string
                public const string CP_ECPM_SWITCH = "cpEcpmSwitch"; // value is int
                public const string CP_ECPM_TIMEOUT = "cpEcpmTimeout"; // value is long
            }
        }

        // Show config keys
        public static class ShowConfig
        {
            public const string SCENARIO_ID = "scenarioId"; // value is string
            public const string SHOW_CUSTOM_EXT = "showCustomExt"; // value is string
            public const string AT_CUSTOM_CONTENT_RESULT = "atCustomContentResult"; // value is object

            public static class CustomContentResult
            {
                public const string ITEMS = "items"; // value is List<object>
                public const string PAYLOAD_KIND = "payloadKind"; // value is string
                public const string CUSTOM_CONTENT_STRING = "customContentString"; // value is string
                public const string CUSTOM_CONTENT_DOUBLE = "customContentDouble"; // value is double
                public const string CUSTOM_CONTENT_OBJECT = "customContentObject"; // value is object
                public const string PAYLOAD_KIND_DOUBLE = "double"; // value is string
            }
        }

        // Shared placement config keys
        public static class SharedPlacementConfig
        {
            public const string REWARD_VIDEO_LOCAL_EXTRA = "rewardVideoLocalExtra"; // value is object
            public const string INTERSTITIAL_LOCAL_EXTRA = "interstitialLocalExtra"; // value is object
            public const string SPLASH_LOCAL_EXTRA = "splashLocalExtra"; // value is object
            public const string BANNER_LOCAL_EXTRA = "bannerLocalExtra"; // value is object
            public const string NATIVE_LOCAL_EXTRA = "nativeLocalExtra"; // value is object

            public static class LocalExtra
            {
                public const string AD_WIDTH = "ad_width"; // value is int
                public const string AD_HEIGHT = "ad_height"; // value is int
            }
        }

        // Multiple-loaded callback keys
        public static class RequestingInfo
        {
            public const string BIDDING_ATTEMPT_AD_INFO_LIST = "biddingAttemptAdInfoList"; // value is List<object>
            public const string LOADING_AD_INFO_LIST = "loadingAdInfoList"; // value is List<object>
        }
    }
}
