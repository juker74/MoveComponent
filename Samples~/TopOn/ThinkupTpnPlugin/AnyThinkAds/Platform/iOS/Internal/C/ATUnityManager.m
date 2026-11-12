//
//  ATUnityManager.m
//  UnityContainer
//
//  Created by Martin Lau on 08/08/2018.
//  Copyright © 2018 Martin Lau. All rights reserved.
//

#import "ATUnityManager.h"
#import <AnyThinkSDK/AnyThinkSDK.h>
#import "ATUnityUtilities.h"
#import <CoreTelephony/CTTelephonyNetworkInfo.h>
#import <CoreTelephony/CTCarrier.h>
#import "ATBannerAdWrapper.h"
#import "ATNativeAdWrapper.h"
#import "ATInterstitialAdWrapper.h"
#import "ATRewardedVideoWrapper.h"

/*
 *class:
 *selector:
 *arguments:
 */
bool at_message_from_unity(const char *msg, void(*callback)(const char*, const char *)) {
    NSString *msgStr = [NSString stringWithUTF8String:msg];
    NSDictionary *msgDict = [NSJSONSerialization JSONObjectWithData:[msgStr dataUsingEncoding:NSUTF8StringEncoding] options:NSJSONReadingAllowFragments error:nil];
    Class class = NSClassFromString(msgDict[@"class"]);

    bool ret = false;
    ret = [[[class sharedInstance] selWrapperClassWithDict:msgDict callback:callback != NULL ? callback : nil] boolValue];
    
    return ret;
}

int at_get_message_for_unity(const char *msg, void(*callback)(const char*, const char *)) {
    NSString *msgStr = [NSString stringWithUTF8String:msg];
    NSDictionary *msgDict = [NSJSONSerialization JSONObjectWithData:[msgStr dataUsingEncoding:NSUTF8StringEncoding] options:NSJSONReadingAllowFragments error:nil];
    
    Class class = NSClassFromString(msgDict[@"class"]);
    
    int ret = 0;
    ret = [[[class sharedInstance] selWrapperClassWithDict:msgDict callback:callback != NULL ? callback : nil] intValue];
    
    return ret;
}

char * at_get_string_message_for_unity(const char *msg, void(*callback)(const char*, const char *)) {
    NSString *msgStr = [NSString stringWithUTF8String:msg];
    NSDictionary *msgDict = [NSJSONSerialization JSONObjectWithData:[msgStr dataUsingEncoding:NSUTF8StringEncoding] options:NSJSONReadingAllowFragments error:nil];

    Class class = NSClassFromString(msgDict[@"class"]);
    
    NSString *ret = @"";
    ret = [[class sharedInstance] selWrapperClassWithDict:msgDict callback:callback != NULL ? callback : nil];
    
    if ([ret UTF8String] == NULL)
        return NULL;

    char* res = (char*)malloc(strlen([ret UTF8String]) + 1);
    strcpy(res, [ret UTF8String]);

    return res;
}

/// JSON keys for {@code setAdSourcePrivacyPolicy:} (aligned with Android {@code AdSourcePrivacyPolicyStore.Keys}).
static NSString * const kATPolicyNetworkFirmIds = @"networkFirmIds";
static NSString * const kATPolicyAgreePrivacyStrategy = @"agreePrivacyStrategy";
static NSString * const kATPolicyCanUseAppList = @"isCanUseAppList";
static NSString * const kATPolicyCanUseGeneralData = @"isCanUseGeneralData";
static NSString * const kATPolicyCanUseWifiState = @"isCanUseWifiState";
static NSString * const kATPolicyCanUseMacAddress = @"isCanUseMacAddress";
static NSString * const kATPolicyCanUseWriteExternal = @"isCanUseWriteExternal";
static NSString * const kATPolicyCanUseRecordAudio = @"isCanUsePermissionRecordAudio";
static NSString * const kATPolicyCanUseAndroidId = @"isCanUseAndroidId";
static NSString * const kATPolicyCanUseOaid = @"isCanUseOaid";
static NSString * const kATPolicyCanUseLocation = @"isCanUseLocation";
static NSString * const kATPolicyCanUsePhoneState = @"isCanUsePhoneState";
static NSString * const kATPolicyCanUseIp = @"isCanUseIp";
static NSString * const kATPolicyCanPersonalRecommend = @"isCanPersonalRecommend";
static NSString * const kATPolicyCanShake = @"isCanShake";
static NSString * const kATPolicyForbidSensor = @"forbidSensor";
static NSString * const kATPolicyAllowHardDiskSizeKBytes = @"isAllowHardDiskSizeKBytes";
static NSString * const kATPolicyIdAllSwitch = @"idAllSwitch";
static NSString * const kATPolicyCanUseIdfa = @"isCanUseIdfa";
static NSString * const kATPolicyCustomAndroidId = @"customAndroidId";
static NSString * const kATPolicyCustomIMEI = @"customIMEI";
static NSString * const kATPolicyCustomOaid = @"customOaid";
static NSString * const kATPolicyCustomMacAddress = @"customMacAddress";
static NSString * const kATPolicyCustomIp = @"customIp";
static NSString * const kATPolicyCustomIDFA = @"customIDFA";
static NSString * const kATPolicyCustomLocation = @"customLocation";
static NSString * const kATPolicyLatitude = @"latitude";
static NSString * const kATPolicyLongitude = @"longitude";
static NSString * const kATPolicyShakeValue = @"shakeValue";
static NSString * const kATPolicyShakeAcceleration = @"acceleration";
static NSString * const kATPolicyShakeAngle = @"angle";
static NSString * const kATPolicyShakeTime = @"time";
static NSString * const kATPolicyInstalledPackageNames = @"installedPackageNames";

static BOOL at_policyBool(NSDictionary *dict, NSString *key, BOOL defaultValue) {
    if (![dict isKindOfClass:[NSDictionary class]] || key == nil) {
        return defaultValue;
    }
    id value = dict[key];
    if (value == nil) {
        return defaultValue;
    }
    if ([value isKindOfClass:[NSNumber class]]) {
        return [(NSNumber *)value boolValue];
    }
    if ([value isKindOfClass:[NSString class]]) {
        NSString *s = (NSString *)value;
        if ([s isEqualToString:@"1"]) {
            return YES;
        }
        if ([s isEqualToString:@"0"]) {
            return NO;
        }
        return [s boolValue];
    }
    return defaultValue;
}

static NSString *at_policyString(NSDictionary *dict, NSString *key) {
    if (![dict isKindOfClass:[NSDictionary class]] || key == nil) {
        return @"";
    }
    id value = dict[key];
    if ([value isKindOfClass:[NSString class]]) {
        return (NSString *)value;
    }
    if ([value isKindOfClass:[NSNumber class]]) {
        return [(NSNumber *)value stringValue];
    }
    return @"";
}

static NSArray *at_policyArray(NSDictionary *dict, NSString *key) {
    if (![dict isKindOfClass:[NSDictionary class]] || key == nil) {
        return nil;
    }
    id value = dict[key];
    return [value isKindOfClass:[NSArray class]] ? (NSArray *)value : nil;
}

static NSDictionary *at_policyDictionary(NSDictionary *dict, NSString *key) {
    if (![dict isKindOfClass:[NSDictionary class]] || key == nil) {
        return nil;
    }
    id value = dict[key];
    return [value isKindOfClass:[NSDictionary class]] ? (NSDictionary *)value : nil;
}

@interface ATUnityManager()
@property(nonatomic, readonly) NSMutableDictionary *consentInfo;
@end
@implementation ATUnityManager
+(instancetype)sharedInstance {
    static ATUnityManager *sharedInstance = nil;
    static dispatch_once_t onceToken;
    dispatch_once(&onceToken, ^{
        sharedInstance = [[ATUnityManager alloc] init];
    });
    return sharedInstance;
}

-(instancetype) init {
    self = [super init];
    if (self != nil) {
        _consentInfo = [NSMutableDictionary dictionary];
    }
    return self;
}

- (id)selWrapperClassWithDict:(NSDictionary *)dict callback:(void(*)(const char*))callback {
    NSString *selector = dict[@"selector"];
    NSArray<NSString*>* arguments = dict[@"arguments"];
    NSString *firstObject = @"";
    NSString *lastObject = @"";
    if (![ATUnityUtilities isEmpty:arguments]) {
        for (int i = 0; i < arguments.count; i++) {
            if (i == 0) { firstObject = arguments[i]; }
            else { lastObject = arguments[i]; }
        }
    }
    
    if ([selector isEqualToString:@"startSDKWithAppID:appKey:"]) {
        return [NSNumber numberWithBool:[self startSDKWithAppID:firstObject appKey:lastObject]];
    } else if ([selector isEqualToString:@"subjectToGDPR"]) {
        return [NSNumber numberWithBool:[self subjectToGDPR]];
    }else if ([selector isEqualToString:@"showGDPRConsentDialog:appID:"]) {
        [self showGDPRConsentDialog:callback appID:firstObject];
    }else if ([selector isEqualToString:@"showGDPRSecondConsentDialog:appID:"]) {
        [self showGDPRSecondConsentDialog:callback appID:firstObject];
    }else if ([selector isEqualToString:@"showGDPRConsentDialog:"]) {
        [self showGDPRConsentDialog:callback appID:@""];
    } else if ([selector isEqualToString:@"getUserLocation:"]) {
        [self getUserLocation:callback appID:@""];
    } else if ([selector isEqualToString:@"checkIsEuTraffic:appID:"]) {
        [self getUserLocation:callback appID:firstObject];
    } else if ([selector isEqualToString:@"setPurchaseFlag"]) {
        [self setPurchaseFlag];
    } else if ([selector isEqualToString:@"clearPurchaseFlag"]) {
        [self clearPurchaseFlag];
    } else if ([selector isEqualToString:@"purchaseFlag"]) {
        return [NSNumber numberWithBool:[self purchaseFlag]];
    } else if ([selector isEqualToString:@"setChannel:"]) {
        [self setChannel:firstObject];
    } else if ([selector isEqualToString:@"setSubChannel:"]) {
        [self setSubChannel:firstObject];
    } else if ([selector isEqualToString:@"setCustomData:"]) {
        [self setCustomData:firstObject];
    } else if ([selector isEqualToString:@"setCustomData:forPlacementID:"]) {
        [self setCustomData:firstObject forPlacementID:lastObject];
    } else if ([selector isEqualToString:@"setDebugLog:"]) {
        [self setDebugLog:firstObject];
    } else if ([selector isEqualToString:@"getDataConsent"]) {
        return [NSNumber numberWithInt:[self getDataConsent]];
    } else if ([selector isEqualToString:@"setDataConsent:"]) {
        [self setDataConsent:[NSNumber numberWithInt:firstObject.intValue]];
    } else if ([selector isEqualToString:@"inDataProtectionArea"]) {
        return [NSNumber numberWithBool:[self inDataProtectionArea]];
    } else if ([selector isEqualToString:@"deniedUploadDeviceInfo:"]) {
        [self deniedUploadDeviceInfo:firstObject];
    } else if ([selector isEqualToString:@"setDataConsent:network:"]) {
        [self setDataConsent:firstObject network:[NSNumber numberWithInt:lastObject.intValue]];
    } else if ([selector isEqualToString:@"setExcludeBundleIdArray:"]) {
        [self setExcludeBundleIdArray:firstObject];
    } else if ([selector isEqualToString:@"setExludePlacementid:unitIDArray:"]) {
        [self setExludePlacementid:firstObject unitIDArray:lastObject];
    } else if ([selector isEqualToString:@"setSDKArea:"]) {
        [self setSDKArea:[NSNumber numberWithInt:firstObject.intValue]];
    } else if ([selector isEqualToString:@"getArea:"]) {
        [self getArea:callback];
    } else if ([selector isEqualToString:@"setWXStatus:"]) {
        [self setWXStatus:[NSNumber numberWithDouble:firstObject.boolValue]];
    } else if ([selector isEqualToString:@"setLocationLongitude:dimension:"]) {
        [self setLocationLongitude:[NSNumber numberWithDouble:firstObject.doubleValue] dimension:[NSNumber numberWithDouble:lastObject.doubleValue]];
    }else if ([selector isEqualToString:@"showDebuggerUI:"]) {
        [self showDebuggerUI:firstObject];
    } else if ([selector isEqualToString:@"setLocalStrategyAssetPath:"]) {
        [self setLocalStrategyAssetPath:firstObject];
    } else if ([selector isEqualToString:@"getSDKVersionName"]) {
        return [self getSDKVersionName];
    } else if ([selector isEqualToString:@"setSharedPlacementConfig:"]) {
        [self setSharedPlacementConfig:firstObject];
    }
    else if ([selector isEqualToString:@"putFilter:filterJson:"]) {
        [self putFilter:firstObject filterJson:lastObject];
    }
    else if ([selector isEqualToString:@"removeFilterWithPlacementId:"]) {
        [self removeFilterWithPlacementId:firstObject];
    }
    else if ([selector isEqualToString:@"removeFilters"]) {
        [self removeFilters];
    }
    else if ([selector isEqualToString:@"setAdSourcePrivacyPolicy:"]) {
        [self setAdSourcePrivacyPolicy:firstObject];
    }

    return nil;
}

- (void)setAdSourcePrivacyPolicy:(NSString*)policyJson {
    NSLog(@"iOS: ATUnityManager::setAdSourcePrivacyPolicy policyJson=%@", policyJson);
    if ([ATUnityUtilities isEmpty:policyJson]) {
        return;
    }
    NSDictionary *policyDict = [NSJSONSerialization JSONObjectWithData:[policyJson dataUsingEncoding:NSUTF8StringEncoding] options:NSJSONReadingAllowFragments error:nil];
    if (![policyDict isKindOfClass:[NSDictionary class]]) {
        NSLog(@"iOS: ATUnityManager::setAdSourcePrivacyPolicy invalid policyDict");
        return;
    }
    NSLog(@"iOS: policyDict = %@", policyDict);

    // network filter
    NSArray *networkFirmIds = at_policyArray(policyDict, kATPolicyNetworkFirmIds);

    // permission / privacy switches
    BOOL agreePrivacyStrategy = at_policyBool(policyDict, kATPolicyAgreePrivacyStrategy, YES);
    BOOL isCanUseAppList = at_policyBool(policyDict, kATPolicyCanUseAppList, NO);
    BOOL isCanUseGeneralData = at_policyBool(policyDict, kATPolicyCanUseGeneralData, NO);
    BOOL isCanUseWifiState = at_policyBool(policyDict, kATPolicyCanUseWifiState, NO);
    BOOL isCanUseMacAddress = at_policyBool(policyDict, kATPolicyCanUseMacAddress, NO);
    BOOL isCanUseWriteExternal = at_policyBool(policyDict, kATPolicyCanUseWriteExternal, NO);
    BOOL isCanUsePermissionRecordAudio = at_policyBool(policyDict, kATPolicyCanUseRecordAudio, NO);
    BOOL isCanUseAndroidId = at_policyBool(policyDict, kATPolicyCanUseAndroidId, NO);
    BOOL isCanUseOaid = at_policyBool(policyDict, kATPolicyCanUseOaid, NO);
    BOOL isCanUseLocation = at_policyBool(policyDict, kATPolicyCanUseLocation, NO);
    BOOL isCanUsePhoneState = at_policyBool(policyDict, kATPolicyCanUsePhoneState, NO);
    BOOL isCanUseIp = at_policyBool(policyDict, kATPolicyCanUseIp, NO);
    BOOL isCanPersonalRecommend = at_policyBool(policyDict, kATPolicyCanPersonalRecommend, NO);
    BOOL isCanShake = at_policyBool(policyDict, kATPolicyCanShake, YES);
    BOOL forbidSensor = at_policyBool(policyDict, kATPolicyForbidSensor, NO);
    BOOL isAllowHardDiskSizeKBytes = at_policyBool(policyDict, kATPolicyAllowHardDiskSizeKBytes, NO);
    BOOL idAllSwitch = at_policyBool(policyDict, kATPolicyIdAllSwitch, NO);
    BOOL isCanUseIdfa = at_policyBool(policyDict, kATPolicyCanUseIdfa, NO);

    // developer-supplied device identifiers (when collection is disabled)
    NSString *customAndroidId = at_policyString(policyDict, kATPolicyCustomAndroidId);
    NSString *customIMEI = at_policyString(policyDict, kATPolicyCustomIMEI);
    NSString *customOaid = at_policyString(policyDict, kATPolicyCustomOaid);
    NSString *customMacAddress = at_policyString(policyDict, kATPolicyCustomMacAddress);
    NSString *customIp = at_policyString(policyDict, kATPolicyCustomIp);
    NSString *customIDFA = at_policyString(policyDict, kATPolicyCustomIDFA);

    // customLocation: { "latitude": double, "longitude": double }
    NSDictionary *customLocation = at_policyDictionary(policyDict, kATPolicyCustomLocation);
    double customLatitude = 0;
    double customLongitude = 0;
    if (customLocation != nil) {
        customLatitude = [customLocation[kATPolicyLatitude] doubleValue];
        customLongitude = [customLocation[kATPolicyLongitude] doubleValue];
    }

    // shakeValue: { "acceleration": double, "angle": double, "time": int } (Octopus)
    NSDictionary *shakeValue = at_policyDictionary(policyDict, kATPolicyShakeValue);
    double shakeAcceleration = 0;
    double shakeAngle = 0;
    NSInteger shakeTime = 0;
    if (shakeValue != nil) {
        shakeAcceleration = [shakeValue[kATPolicyShakeAcceleration] doubleValue];
        shakeAngle = [shakeValue[kATPolicyShakeAngle] doubleValue];
        shakeTime = [shakeValue[kATPolicyShakeTime] integerValue];
    }

    NSArray *installedPackageNames = at_policyArray(policyDict, kATPolicyInstalledPackageNames);

    if (!isCanShake || forbidSensor) {
        [[ATSDKGlobalSetting sharedManager] setNetworkSensorType:ATNetworkSensorTypeForbidAll networkSensorList:nil];
    }
    
    if (customLongitude != 0) {
        [[ATSDKGlobalSetting sharedManager] setLocationLongitude:customLongitude dimension:customLatitude];
    }

    NSLog(@"iOS: ATUnityManager::setAdSourcePrivacyPolicy parsed networkFirmIds=%@ agreePrivacyStrategy=%d isCanUseAppList=%d isCanUseGeneralData=%d isCanUseWifiState=%d isCanUseMacAddress=%d isCanUseWriteExternal=%d isCanUsePermissionRecordAudio=%d isCanUseAndroidId=%d isCanUseOaid=%d isCanUseLocation=%d isCanUsePhoneState=%d isCanUseIp=%d isCanPersonalRecommend=%d isCanShake=%d forbidSensor=%d isAllowHardDiskSizeKBytes=%d idAllSwitch=%d isCanUseIdfa=%d customAndroidId=%@ customIMEI=%@ customOaid=%@ customMacAddress=%@ customIp=%@ customIDFA=%@ customLatitude=%f customLongitude=%f shakeAcceleration=%f shakeAngle=%f shakeTime=%ld installedPackageNames=%@",
          networkFirmIds, agreePrivacyStrategy, isCanUseAppList, isCanUseGeneralData, isCanUseWifiState, isCanUseMacAddress, isCanUseWriteExternal, isCanUsePermissionRecordAudio, isCanUseAndroidId, isCanUseOaid, isCanUseLocation, isCanUsePhoneState, isCanUseIp, isCanPersonalRecommend, isCanShake, forbidSensor, isAllowHardDiskSizeKBytes, idAllSwitch, isCanUseIdfa, customAndroidId, customIMEI, customOaid, customMacAddress, customIp, customIDFA, customLatitude, customLongitude, shakeAcceleration, shakeAngle, (long)shakeTime, installedPackageNames);

    /// TODO: Apply parsed policy to each integrated ad-network CustomController (iOS adapters).
    #pragma unused(networkFirmIds, agreePrivacyStrategy, isCanUseAppList, isCanUseGeneralData, isCanUseWifiState, isCanUseMacAddress, isCanUseWriteExternal, isCanUsePermissionRecordAudio, isCanUseAndroidId, isCanUseOaid, isCanUseLocation, isCanUsePhoneState, isCanUseIp, isCanPersonalRecommend, isCanShake, forbidSensor, isAllowHardDiskSizeKBytes, idAllSwitch, isCanUseIdfa, customAndroidId, customIMEI, customOaid, customMacAddress, customIp, customIDFA, customLatitude, customLongitude, shakeAcceleration, shakeAngle, shakeTime, installedPackageNames)
}

- (void)putFilter:(NSString*)placementId filterJson:(NSString*)filterJson  {
    NSLog(@"iOS: ATUnityManager::putFilter placementId=%@ filterJson=%@", placementId, filterJson);
    if (![ATUnityUtilities isEmpty:placementId] && ![ATUnityUtilities isEmpty:filterJson]) {
        NSDictionary *filterDict = [NSJSONSerialization JSONObjectWithData:[filterJson dataUsingEncoding:NSUTF8StringEncoding] options:NSJSONReadingAllowFragments error:nil];
        NSArray *groups = filterDict[@"groups"] ? : @[];
        
        // groups = @[
        //     @{@"biddingType": @[@"NORMAL"]},
        //        @{@"networkId": @[@(8)]}
        // ];
        // placementId = @"b5bacaccb61c29";
        
        ATAdCustomFilter *filter = [[ATAdCustomFilter alloc] init];
        [groups enumerateObjectsUsingBlock:^(id  _Nonnull obj, NSUInteger idx, BOOL * _Nonnull stop) {
            
            
            NSMutableArray *networkFirmIDs = [NSMutableArray arrayWithCapacity:0];
            NSMutableArray *biddingTypes = [NSMutableArray arrayWithCapacity:0];
            NSMutableArray *networkPlacementIds = [NSMutableArray arrayWithCapacity:0];
            
            NSArray *networkId = obj[@"networkId"] ? : @[];
            NSArray *biddingType = obj[@"biddingType"] ? : @[];
            NSArray *networkPlacementId = obj[@"networkPlacementId"] ? : @[];
            [networkPlacementIds addObjectsFromArray:networkPlacementId];
            
            NSDictionary *e_cpm = obj[@"e_cpm"] ? : @{};
            NSString *currency = e_cpm[@"currency"] ? : @"";
            NSString *moreThanPrice = e_cpm[@"moreThanPrice"] ? : @"";
            NSString *lessThanPrice = e_cpm[@"lessThanPrice"] ? : @"";
            ATBiddingCurrencyType currencyType = ATBiddingCurrencyTypeCNYCents;
            if ([[currency lowercaseString] containsString:@"cny"]) {
                currencyType = ATBiddingCurrencyTypeCNY;
            } else if ([[currency lowercaseString] containsString:@"us"]) {
                currencyType = ATBiddingCurrencyTypeUS;
            }
            
            if ([biddingType containsObject:@"NORMAL"] || [biddingType containsObject:@"normal"]) {
                [biddingTypes addObject:@(ATAdCustomFilterBiddingModeNormal)];
            }
            
            if ([biddingType containsObject:@"s2s"] || [biddingType containsObject:@"S2S"]) {
                [biddingTypes addObject:@(ATAdCustomFilterBiddingModeS2S)];
            }
            
            if ([biddingType containsObject:@"c2s"] || [biddingType containsObject:@"C2S"]) {
                [biddingTypes addObject:@(ATAdCustomFilterBiddingModeC2S)];
            }
            
            if (moreThanPrice.length > 0) {
                ATAdCustomFilterEcpmItem *ecpmItem = [[ATAdCustomFilterEcpmItem alloc] initWithMoreThenPrice:moreThanPrice lessThenPrice:lessThanPrice currency:currencyType];
                filter.ecpmItem(ecpmItem);
            }
            
            [networkId enumerateObjectsUsingBlock:^(id  _Nonnull obj, NSUInteger idx, BOOL * _Nonnull stop) {
                NSNumber *num = @([obj integerValue]);
                [networkFirmIDs addObject:num];
            }];
            
            // 过滤  广告源
            if (networkFirmIDs.count > 0) {
                filter.networkFirmIDs(networkFirmIDs);
            }
            
            if (biddingTypes.count > 0) {
                filter.biddingModes(biddingTypes);
            }
            
            if (networkPlacementIds.count > 0) {
                filter.networkSlotIDs(networkPlacementIds);
            }
            
            if (idx != groups.count - 1) {
                filter.orFilter();
            }
                 
        }];
        // 设置到全局
        [[ATSDKGlobalSetting sharedManager] addFilter:filter forPlacementID:placementId];
         
    }
    
}

- (void)removeFilterWithPlacementId:(NSString*)placementId {
    NSLog(@"iOS: ATUnityManager::removeFilterWithPlacementId placementId=%@", placementId);
    if (![ATUnityUtilities isEmpty:placementId]) {
        [[ATSDKGlobalSetting sharedManager] removeFilterWithPlacementIDs:@[placementId]];
    }
}

- (void)removeFilters {
    NSLog(@"iOS: ATUnityManager::removeFilters");
    [[ATSDKGlobalSetting sharedManager] removeAllFilters];
}

- (void)setSharedPlacementConfig:(NSString*)configJson {
    NSLog(@"iOS: ATUnityManager::setSharedPlacementConfig configJson=%@", configJson);
    if (![ATUnityUtilities isEmpty:configJson]) {
        NSDictionary *configDict = [NSJSONSerialization JSONObjectWithData:[configJson dataUsingEncoding:NSUTF8StringEncoding] options:NSJSONReadingAllowFragments error:nil];
         
        NSDictionary *splashLoadExtra = configDict[@"splashLocalExtra"] ? : @{};
        NSDictionary *interstitialLoadExtra = configDict[@"interstitialLocalExtra"] ? : @{};
        NSDictionary *rewardedVideoLoadExtra = configDict[@"rewardVideoLocalExtra"] ? : @{};
        NSDictionary *bannerLoadExtra = configDict[@"bannerLocalExtra"] ? : @{};
        NSDictionary *nativeLoadExtra = configDict[@"nativeLocalExtra"] ? : @{};
        
        ATSharePlacementConfig *sharePlacementConfig = [[ATSharePlacementConfig alloc] init];
        sharePlacementConfig.splashLoadExtra = splashLoadExtra;
        sharePlacementConfig.interstitialLoadExtra = interstitialLoadExtra;
        sharePlacementConfig.rewardedVideoLoadExtra = rewardedVideoLoadExtra;
        sharePlacementConfig.bannerLoadExtra = bannerLoadExtra;
        sharePlacementConfig.nativeLoadExtra = nativeLoadExtra;
        [ATSDKGlobalSetting sharedManager].sharePlacementConfig = sharePlacementConfig;

    }
}

-(BOOL) startSDKWithAppID:(NSString*)appID appKey:(NSString*)appKey {
    NSLog(@"iOS: ATUnityManager::startSDKWithAppID appID=%@ appKey=%@", appID, appKey);
    [[ATSDKGlobalSetting sharedManager] setSystemPlatformType:ATSystemPlatformTypeUnity];
    BOOL started = [[ATAPI sharedInstance] startWithAppID:appID appKey:appKey error:nil];
    NSLog(@"iOS: ATUnityManager::startSDKWithAppID started=%d", started);
    return started;
}

- (BOOL) subjectToGDPR {
    BOOL subject = [@[@"AT", @"BE", @"BG", @"HR", @"CY", @"CZ", @"DK", @"EE", @"FI", @"FR", @"DE", @"GR", @"HU", @"IS", @"IE", @"IT", @"LV", @"LI", @"LT", @"LU", @"MT", @"NL", @"NO", @"PL", @"PT", @"RO", @"SK", @"SI", @"ES", @"SE", @"GB", @"UK"] containsObject:[[CTTelephonyNetworkInfo new].subscriberCellularProvider.isoCountryCode length] > 0 ? [[CTTelephonyNetworkInfo new].subscriberCellularProvider.isoCountryCode uppercaseString] : @""];
    NSLog(@"iOS: ATUnityManager::subjectToGDPR subject=%d", subject);
    return subject;
}

-(NSString*) getSDKVersionName {
    NSString *version = [[ATAPI sharedInstance] version];
    NSLog(@"iOS: ATUnityManager::getSDKVersionName version=%@", version);
    return version;
}

-(void)setLocalStrategyAssetPath:(NSString*)assetPath {
    NSLog(@"iOS: ATUnityManager::setLocalStrategyAssetPath assetPath=%@", assetPath);
    if (![ATUnityUtilities isEmpty:assetPath]) {
        NSString *path = [[NSBundle mainBundle] pathForResource:assetPath ofType:@""];
        if (path.length == 0) {
            path = [[NSBundle mainBundle] pathForResource:[NSString stringWithFormat:@"Data/Raw/%@", assetPath] ofType:@""];
        }
        NSBundle *bundle = [NSBundle bundleWithPath:path];
        if (bundle == nil) {
            NSLog(@"iOS: ATUnityManager::setLocalStrategyAssetPath - bundle is nil");
            return;
        }
        [[ATSDKGlobalSetting sharedManager] setPresetPlacementConfigPathBundle:bundle];
    }
}

-(void) showGDPRConsentDialog:(void(*)(const char*))callback appID:(NSString*)appID {
    NSLog(@"iOS: ATUnityManager::showGDPRConsentDialog appID=%@", appID);
    if (appID != nil && [appID length] > 0) {
        [[ATAPI sharedInstance] showGDPRConsentDialogWithAppID:appID inViewController:[ATUnityUtilities topViewController] dismissalCallback:^{
            if (callback != NULL) { callback(appID.UTF8String); }
        }];
    } else {
        [[ATAPI sharedInstance] showGDPRConsentDialogInViewController:[ATUnityUtilities topViewController] dismissalCallback:^{
            if (callback != NULL) {
                callback(@"".UTF8String);
            }
        }];
    } 
}

-(void) showGDPRSecondConsentDialog:(void(*)(const char*))callback appID:(NSString*)appID {
    NSLog(@"iOS: ATUnityManager::showGDPRSecondConsentDialog appID=%@", appID); 
    if (appID.length == 0) {
        appID = @"";
    }
    if ([[ATAPI sharedInstance] respondsToSelector:@selector(showSecondGDPRConsentDialogWithAppID:inViewController:dismissalCallback:)]) {
        [[ATAPI sharedInstance] showSecondGDPRConsentDialogWithAppID:appID inViewController:[ATUnityUtilities topViewController] dismissalCallback:^{
            BOOL gdprConsent = [ATAPI sharedInstance].dataConsentSet == ATDataConsentSetNonpersonalized;
            NSLog(@"iOS: ATUMPGDPRConsent::UMP二次弹窗展示结束，当前GDPR配置，gdprConsent:%@", gdprConsent ? @"限制" : @"不限制");
        }];
    }
    
}

-(void)getUserLocation:(void(*)(const char*))callback appID:(NSString*)appID {
    NSLog(@"iOS: ATUnityManager::getUserLocation appID=%@", appID);
    if (appID.length > 0) {
        if ([[ATAPI sharedInstance] respondsToSelector:@selector(getUserLocationWithAppID:callback:)]) {
            [[ATAPI sharedInstance] getUserLocationWithAppID:appID callback:^(ATUserLocation location) {
                if (callback != NULL) { callback(@(location).stringValue.UTF8String); }
            }];
        }
    } else {
        [[ATAPI sharedInstance] getUserLocationWithCallback:^(ATUserLocation location) {
            if (callback != NULL) { callback(@(location).stringValue.UTF8String); }
        }];
    }
} 

-(void) setPurchaseFlag {
    NSLog(@"iOS: ATUnityManager::setPurchaseFlag");
}

-(void) clearPurchaseFlag {
    NSLog(@"iOS: ATUnityManager::clearPurchaseFlag");
}

-(BOOL) purchaseFlag {
    BOOL flag = NO;
    NSLog(@"iOS: ATUnityManager::purchaseFlag flag=%d", flag);
    return flag;
}

-(void) setChannel:(NSString*)channel {
    NSLog(@"iOS: ATUnityManager::setChannel channel=%@", channel);
    [[ATSDKGlobalSetting sharedManager] setChannel:channel];
}

-(void) setSubChannel:(NSString*)subChannel {
    NSLog(@"iOS: ATUnityManager::setSubChannel subChannel=%@", subChannel);
    [[ATSDKGlobalSetting sharedManager] setSubchannel:subChannel];
}

-(void) setCustomData:(NSString*)customDataStr {
    NSLog(@"iOS: ATUnityManager::setCustomData customDataStr=%@", customDataStr);
    if ([customDataStr isKindOfClass:[NSString class]] && [customDataStr length] > 0) {
        NSDictionary *customData = [NSJSONSerialization JSONObjectWithData:[customDataStr dataUsingEncoding:NSUTF8StringEncoding] options:NSJSONReadingAllowFragments error:nil];
        [[ATSDKGlobalSetting sharedManager] setCustomData:customData];
    }
}

-(void) setCustomData:(NSString*)customDataStr forPlacementID:(NSString*)placementID {
    NSLog(@"iOS: ATUnityManager::setCustomData:forPlacementID placementID=%@ customDataStr=%@", placementID, customDataStr);
    if ([customDataStr isKindOfClass:[NSString class]] && [customDataStr length] > 0) {
        NSDictionary *customData = [NSJSONSerialization JSONObjectWithData:[customDataStr dataUsingEncoding:NSUTF8StringEncoding] options:NSJSONReadingAllowFragments error:nil];
        [[ATSDKGlobalSetting sharedManager] setCustomData:customData forPlacementID:placementID];
    }
}

-(void) setDebugLog:(NSString*)flagStr {
    NSLog(@"iOS: ATUnityManager::setDebugLog flagStr=%@", flagStr);
    [ATAPI setLogEnabled:[flagStr boolValue]];
}

-(int) getDataConsent {
    int consent = [@{@(ATDataConsentSetPersonalized):@0, @(ATDataConsentSetNonpersonalized):@1, @(ATDataConsentSetUnknown):@2}[@([ATAPI sharedInstance].dataConsentSet)] intValue];
    NSLog(@"iOS: ATUnityManager::getDataConsent consent=%d", consent);
    return consent;
}

-(void) setDataConsent:(NSNumber*)dataConsent {
    NSLog(@"iOS: ATUnityManager::setDataConsent dataConsent=%@", dataConsent);
    [[ATAPI sharedInstance] setDataConsentSet:[@{@0:@(ATDataConsentSetPersonalized), @1:@(ATDataConsentSetNonpersonalized), @2:@(ATDataConsentSetUnknown)}[dataConsent] integerValue] consentString:@{}];
}

-(BOOL) inDataProtectionArea {
    BOOL inArea = [[ATAPI sharedInstance] inDataProtectionArea];
    NSLog(@"iOS: ATUnityManager::inDataProtectionArea inArea=%d", inArea);
    return inArea;
}

-(void) deniedUploadDeviceInfo:(NSString *)deniedInfo {
    NSLog(@"iOS: ATUnityManager::deniedUploadDeviceInfo deniedInfo=%@", deniedInfo);
    if (![ATUnityUtilities isEmpty:deniedInfo]) {
        NSArray *deniedInfoArray = [NSJSONSerialization JSONObjectWithData:[deniedInfo dataUsingEncoding:NSUTF8StringEncoding] options:NSJSONReadingAllowFragments error:nil];
        [[ATSDKGlobalSetting sharedManager] setDeniedUploadInfoArray:deniedInfoArray];
    }
}

/*
 *
 */
-(void) setDataConsent:(NSString*)consentJsonString network:(NSNumber*)network {
    NSLog(@"iOS: ATUnityManager::setDataConsent:network: consentJsonString=%@ network=%@", consentJsonString, network);
    NSLog(@"iOS: API was deprecated, please use SetDataConsent(int consent)");
//    NSLog(@"iOS: constenJsonString = %@, network = %@", consentJsonString, network);
//    NSDictionary *networks = @{@1:kATNetworkNameFacebook, @2:kATNetworkNameAdmob, @3:kATNetworkNameInmobi, @4:kATNetworkNameFlurry, @5:kATNetworkNameApplovin, @6:kATNetworkNameMintegral, @8:kATNetworkNameGDT, @9:kATNetworkNameChartboost, @10:kATNetworkNameTapjoy, @11:kATNetworkNameIronSource, @12:kATNetworkNameUnityAds, @13:kATNetworkNameVungle, @14:kATNetworkNameAdColony, @1:kATNetworkNameOneway, @18:kATNetworkNameMobPower, @20:kATNetworkNameYeahmobi, @21:kATNetworkNameAppnext, @22:kATNetworkNameBaidu};
//    if ([networks containsObjectForKey:network]) {
//        if (([consentJsonString isKindOfClass:[NSString class]] && [consentJsonString dataUsingEncoding:NSUTF8StringEncoding] != nil)) {
//            NSDictionary *consentDict = [NSJSONSerialization JSONObjectWithData:[consentJsonString dataUsingEncoding:NSUTF8StringEncoding] options:NSJSONReadingAllowFragments error:nil];
//            _consentInfo[networks[network]] =  [consentDict containsObjectForKey:@"value"] ? consentDict[@"value"] : consentDict;
//        } else {
//            [_consentInfo removeObjectForKey:networks[network]];
//        }
//        NSLog(@"iOS: consentInfo = %@", _consentInfo);
//        if ([_consentInfo[kATNetworkNameMintegral] isKindOfClass:[NSDictionary class]]) {
//            NSMutableDictionary<NSNumber*, NSNumber*>* mintegralInfo = [NSMutableDictionary<NSNumber*, NSNumber*> dictionary];
//            [_consentInfo[kATNetworkNameMintegral] enumerateKeysAndObjectsUsingBlock:^(id  _Nonnull key, id  _Nonnull obj, BOOL * _Nonnull stop) {
//                if ([key respondsToSelector:@selector(integerValue)] && [obj respondsToSelector:@selector(integerValue)]) mintegralInfo[@([key integerValue])] = @([obj integerValue]);
//            }];
//            NSLog(@"iOS: consentInfo = %@, %@", [((NSDictionary*)_consentInfo[kATNetworkNameMintegral]).allKeys[0] class], [((NSDictionary*)_consentInfo[kATNetworkNameMintegral]).allValues[0] class]);
//            _consentInfo[kATNetworkNameMintegral] = mintegralInfo;
//            NSLog(@"iOS: consentInfo = %@, %@", [((NSDictionary*)_consentInfo[kATNetworkNameMintegral]).allKeys[0] class], [((NSDictionary*)_consentInfo[kATNetworkNameMintegral]).allValues[0] class]);
//        }
//        [[ATAPI sharedInstance] setNetworkConsentInfo:_consentInfo];
//    }
}

-(void) setExcludeBundleIdArray:(NSString*)bundleIds {
    NSLog(@"iOS: ATUnityManager::setExcludeBundleIdArray bundleIds=%@", bundleIds);
    if (![ATUnityUtilities isEmpty:bundleIds]) {
        NSArray *bundleIdArray = [NSJSONSerialization JSONObjectWithData:[bundleIds dataUsingEncoding:NSUTF8StringEncoding] options:NSJSONReadingAllowFragments error:nil];
        [[ATSDKGlobalSetting sharedManager] setExludeAppleIdArray:bundleIdArray];
    }
}

-(void) setExludePlacementid:(NSString*)placementID unitIDArray:(NSString*)adsourceIds {
    NSLog(@"iOS: ATUnityManager::setExludePlacementid placementID=%@ adsourceIds=%@", placementID, adsourceIds);
    if (![ATUnityUtilities isEmpty:adsourceIds]) {
        NSArray *adsourceIdArray = [NSJSONSerialization JSONObjectWithData:[adsourceIds dataUsingEncoding:NSUTF8StringEncoding] options:NSJSONReadingAllowFragments error:nil];
        [[ATAdManager sharedManager] setExludePlacementid:placementID
                    unitIDArray:adsourceIdArray];
    }
}

-(void) setSDKArea:(NSNumber*)area {
    NSLog(@"iOS: ATUnityManager::setSDKArea area=%@", area);
}

-(void) getArea:(void(*)(const char *))callback {
    NSLog(@"iOS: ATUnityManager::getArea");
    NSMutableDictionary *resultDict = [NSMutableDictionary dictionary];
    [[ATAPI sharedInstance] getAreaSuccess:^(NSString *areaCodeStr) {
        NSLog(@"iOS: ATUnityManager::getArea:Success:%@",areaCodeStr);
        if (areaCodeStr != nil) {
            resultDict[@"areaCode"] = areaCodeStr;
            if (callback != NULL) { callback(resultDict.jsonString.UTF8String); }
        }
    } failure:^(NSError *error) {
        NSLog(@"iOS: ATUnityManager::getArea:failure:%@",error.domain);
        if (error.domain != nil) {
            resultDict[@"errorMsg"] = error.domain;
            if (callback != NULL) { callback(resultDict.jsonString.UTF8String); }
        }
    }];
}

-(void) setWXStatus:(NSNumber *)flag {
    NSLog(@"iOS: ATUnityManager::setWXStatus install=%d", flag.boolValue);
    [ATSDKGlobalSetting sharedManager].isInstallWX = flag.boolValue;
}

-(void) setLocationLongitude:(NSNumber*)longitude dimension:(NSNumber*)latitude {
    NSLog(@"iOS: ATUnityManager::setLocationLongitude:dimension: longitude=%@ latitude=%@", longitude, latitude);
    [[ATSDKGlobalSetting sharedManager] setLocationLongitude:longitude.doubleValue dimension:latitude.doubleValue];
}
 
- (void)showDebuggerUI:(NSString *)debugKey {
    NSLog(@"iOS: ATUnityManager::showDebuggerUI debugKey=%@", debugKey);
    NSString *classStr = @"ATDebuggerAPI";
    Class debuggerAPIClass = NSClassFromString(classStr);
    if(!debuggerAPIClass) {
        NSLog(@"iOS: ATUnityManager::showDebuggerUI - NO %@", classStr);
        return;
    }

    SEL sharedInstanceSel = @selector(sharedInstance);
    if (![debuggerAPIClass respondsToSelector:sharedInstanceSel]) {
        NSLog(@"iOS: ATUnityManager::showDebuggerUI - NO sharedInstance selector");
        return;
    }
    
    // 通过sharedInstanceSel获取单例对象
    id debugger = [debuggerAPIClass performSelector:sharedInstanceSel];
    
    NSString *functionStr = @"showDebuggerInViewController:showType:debugkey:";
    SEL sel = NSSelectorFromString(functionStr);
    if ([debugger respondsToSelector:sel]) {
        NSMethodSignature *signature = [debugger methodSignatureForSelector:sel];
        if (signature) {
            NSInvocation *invocation = [NSInvocation invocationWithMethodSignature:signature];
            [invocation setTarget:debugger];
            [invocation setSelector:sel];
            
            // 设置参数
            UIViewController *rootViewController = [ATUnityUtilities topViewController];
            NSNumber *showType = @1; // 假设 showType 为1
            
            [invocation setArgument:&rootViewController atIndex:2]; // 注意：参数索引从2开始，0和1被target和selector占用
            [invocation setArgument:&showType atIndex:3];
            [invocation setArgument:&debugKey atIndex:4];
            
            // 调用方法
            [invocation invoke];
        }
    } else {
        NSLog(@"iOS: ATUnityManager::showDebuggerUI - NO %@", functionStr);
    }
}


@end

