//
//  ATSplashAdWrapper.m
//  UnityFramework
//
//  Created by li zhixuan on 2023/5/4.
//

#import "ATSplashAdWrapper.h"
#import "ATUnityUtilities.h"
#import <AnyThinkSDK/AnyThinkSDK.h>

@interface ATSplashAdWrapper () <ATSplashDelegate, ATAdMultipleLoadingDelegate>
@property (nonatomic, strong) NSMutableDictionary *adInfoDict;

@end

@implementation ATSplashAdWrapper

+ (instancetype)sharedInstance {
    static ATSplashAdWrapper *sharedInstance = nil;
    static dispatch_once_t onceToken;
    dispatch_once(&onceToken, ^{
        sharedInstance = [[ATSplashAdWrapper alloc] init];
    });
    return sharedInstance;
}

- (NSString *)scriptWrapperClass {
    return @"ATSplashAdWrapper";
}

- (id)selWrapperClassWithDict:(NSDictionary *)dict callback:(void(*)(const char*, const char*))callback {
    NSString *selector = dict[@"selector"];
    NSArray<NSString*>* arguments = dict[@"arguments"];
    NSString *firstObject = @"";
    NSString *secondObject = @"";
    NSString *lastObject = @"";
    if (![ATUnityUtilities isEmpty:arguments]) {
        for (int i = 0; i < arguments.count; i++) {
            if (i == 0) { firstObject = arguments[i]; }
            else if ( i == 1 && arguments.count == 3) { secondObject = arguments[i]; }
            else { lastObject = arguments[i]; }
        }
    }
    
    if ([selector isEqualToString:@"loadSplashAdWithPlacementID:customDataJSONString:callback:"]) {
        [self loadSplashAdWithPlacementID:firstObject customDataJSONString:lastObject callback:callback];
    } else if ([selector isEqualToString:@"splashAdReadyForPlacementID:"]) {
        return [NSNumber numberWithBool:[self splashAdReadyForPlacementID:firstObject]];
    } else if ([selector isEqualToString:@"showSplashAdWithPlacementID:extraJsonString:"]) {
        [self showSplashAdWithPlacementID:firstObject extraJsonString:lastObject];
    } else if ([selector isEqualToString:@"checkAdStatus:"]) {
        return [self checkAdStatus:firstObject];
    } else if ([selector isEqualToString:@"clearCache"]) {
        [self clearCache];
    } else if ([selector isEqualToString:@"getValidAdCaches:"]) {
        return [self getValidAdCaches:firstObject];
    }else if ([selector isEqualToString:@"entryScenarioWithPlacementID:scenarioID:"]) {
        [self entryScenarioWithPlacementID:firstObject scenarioID:lastObject tkExtraJson:@""];
    }else if ([selector isEqualToString:@"entryScenarioWithPlacementID:scenarioID:tkExtraJson:"]) {
        [self entryScenarioWithPlacementID:firstObject scenarioID:secondObject tkExtraJson:lastObject];
    }
    
    return nil;
}

- (void)loadSplashAdWithPlacementID:(NSString*)placementID customDataJSONString:(NSString*)customDataJSONString callback:(void(*)(const char*, const char*))callback {
    NSLog(@"iOS: ATSplashAdWrapper::loadSplashAdWithPlacementID placementID=%@ customDataJSONString=%@", placementID, customDataJSONString);
    [self setCallBack:callback forKey:placementID];
    NSMutableDictionary *extra = [NSMutableDictionary dictionary];
    if ([customDataJSONString isKindOfClass:[NSString class]] && [customDataJSONString length] > 0) {
        NSDictionary *extraDict = [NSJSONSerialization JSONObjectWithData:[customDataJSONString dataUsingEncoding:NSUTF8StringEncoding] options:NSJSONReadingAllowFragments error:nil];
        [extra addEntriesFromDictionary:extraDict];
        
        NSDictionary *atAdRequest = extraDict[@"atAdRequest"];
        if (atAdRequest) {
            if ([atAdRequest containsObjectForKey:@"channelSource"]) {
                NSInteger channelSource = [atAdRequest[@"channelSource"] integerValue];
                ATSDKConfiguration *sdkConfiguration = [[ATSDKConfiguration alloc] init];
                sdkConfiguration.adChannelSource = channelSource;
                [[ATAPI sharedInstance] updateSdkConfigure:sdkConfiguration];
            }
            NSDictionary *adxBidFloorInfo = atAdRequest[@"adxBidFloorInfo"] ? : @{};
            NSDictionary *preLoadInfo = atAdRequest[@"preLoadInfo"] ? : @{};
            if (adxBidFloorInfo) {
                NSString *bidFloor = adxBidFloorInfo[@"bidFloor"] ? : @"";
                NSDictionary *extraMap = adxBidFloorInfo[@"extraMap"] ? : @{};
                NSString *currency = adxBidFloorInfo[@"currency"] ? : @"USD";
            }
            
            if (preLoadInfo) {
                NSString *requestId = preLoadInfo[@"requestId"] ? : @"";
                NSString *psId = preLoadInfo[@"psId"] ? : @"";
                NSString *placementId = preLoadInfo[@"placementId"] ? : @"";
                NSString *cpEcpmSwitch = preLoadInfo[@"cpEcpmSwitch"] ? : @"";
                NSString *cpEcpmTimeout = preLoadInfo[@"cpEcpmTimeout"] ? : @"";
            }
        }
        
    }
    NSString *defaultAdSourceConfig = extra[@"default_adSource_config"];
    NSLog(@"iOS: ATSplashAdWrapper::extra = %@", extra);
    [[ATAdManager  sharedManager] loadADWithPlacementID:placementID extra:extra delegate:self containerView:nil];
    [[ATAdManager sharedManager] setMultipleLoadingDelegate:self placementId:placementID];

}


- (BOOL)splashAdReadyForPlacementID:(NSString*)placementID {
    BOOL ready = [[ATAdManager sharedManager] splashReadyForPlacementID:placementID];
    NSLog(@"iOS: ATSplashAdWrapper::splashAdReadyForPlacementID placementID=%@ ready=%d", placementID, ready);
    return ready;
}

- (NSString*)getValidAdCaches:(NSString *)placementID {
    NSLog(@"iOS: ATSplashAdWrapper::getValidAdCaches placementID=%@", placementID);
    NSArray *array = [[ATAdManager sharedManager] getSplashValidAdsForPlacementID:placementID];
    NSLog(@"iOS: ATSplashAdWrapper::array = %@", array);
    return array.jsonFilterString;
}

- (void)showSplashAdWithPlacementID:(NSString*)placementID extraJsonString:(NSString*)showConfigJsonString {
    NSLog(@"iOS: ATSplashAdWrapper::showSplashAdWithPlacementID placementID=%@ extraJsonString=%@", placementID, showConfigJsonString);
    
    NSDictionary *showCongifDict = ([showConfigJsonString isKindOfClass:[NSString class]] && [showConfigJsonString dataUsingEncoding:NSUTF8StringEncoding] != nil) ? [NSJSONSerialization JSONObjectWithData:[showConfigJsonString dataUsingEncoding:NSUTF8StringEncoding] options:NSJSONReadingAllowFragments error:nil] : nil;
    
    NSString *scenarioId = showCongifDict[@"tkExtraJson"] ? : @"";
    if (scenarioId.length == 0) {
        scenarioId = showCongifDict[kATUnityUtilitiesAdShowingExtraScenarioKey] ? : @"";
    }
     
    NSString *showCustomExt = showCongifDict[@"showCustomExt"] ? : @"";
    NSDictionary *atCustomContentResult = showCongifDict[@"atCustomContentResult"] ? : @{};
    NSArray *customContentResult = atCustomContentResult[@"items"] ? : @[];
    
    NSMutableArray *contentInfoArray = [NSMutableArray arrayWithCapacity:0];
    [customContentResult enumerateObjectsUsingBlock:^(id  _Nonnull obj, NSUInteger idx, BOOL * _Nonnull stop) {
        NSString *customContentString = obj[@"customContentString"] ? : @"";
        double customContentDouble = [obj[@"customContentDouble"] doubleValue];
        
        NSDictionary *customContentObject = obj[@"customContentObject"] ? : @{};
        if (customContentString.length > 0) {
            ATCustomContentInfo *info = [[ATCustomContentInfo alloc] initInfoWithContentString:customContentString contentObject:customContentObject];
            [contentInfoArray addObject:info];
        } else {
            ATCustomContentInfo *info = [[ATCustomContentInfo alloc] initInfoWithContentDouble:customContentDouble contentObject:customContentObject];
            [contentInfoArray addObject:info];
        }
    }];
    
    ATCustomContentResult *contentResult = [[ATCustomContentResult alloc] initContentResultWithInfoArray:contentInfoArray];
    
    ATShowConfig *config = [[ATShowConfig alloc] initWithScene:scenarioId showCustomExt:showCustomExt customContentResult:contentResult];
    
    [[ATAdManager sharedManager] showSplashWithPlacementID:placementID config:config window:[ATUnityUtilities keyWindow] inViewController:[ATUnityUtilities topViewController] extra:nil delegate:self];
     
}

- (NSString*)checkAdStatus:(NSString *)placementID {
    NSLog(@"iOS: ATSplashAdWrapper::checkAdStatus placementID=%@", placementID);
    ATCheckLoadModel *checkLoadModel = [[ATAdManager sharedManager] checkSplashLoadStatusForPlacementID:placementID];
    NSMutableDictionary *statusDict = [NSMutableDictionary dictionary];
    statusDict[@"isLoading"] = @(checkLoadModel.isLoading);
    statusDict[@"isReady"] = @(checkLoadModel.isReady);
    statusDict[@"adInfo"] = checkLoadModel.adOfferInfo;
    NSLog(@"iOS: ATSplashAdWrapper::statusDict = %@", statusDict);
    return statusDict.jsonFilterString;
}

- (void)entryScenarioWithPlacementID:(NSString *)placementID scenarioID:(NSString *)scenarioID tkExtraJson:(NSString *)tkExtraJson{
    NSLog(@"iOS: ATSplashAdWrapper::entryScenarioWithPlacementID placementID=%@ scenarioID=%@ tkExtraJson=%@", placementID, scenarioID, tkExtraJson);
    [[ATAdManager sharedManager] entrySplashScenarioWithPlacementID:placementID scene:scenarioID];
}

- (void) clearCache {
    NSLog(@"iOS: ATSplashAdWrapper::clearCache");
}

#pragma mark - ATSplashDelegate
/// Splash ad displayed successfully
- (void)splashDidShowForPlacementID:(NSString *)placementID
                              extra:(NSDictionary *)extra {
    [self invokeCallback:@"OnSplashAdShow" placementID:placementID error:nil extra:extra];
    [self invokeCallback:@"OnAdRevenuePaid" placementID:placementID error:nil extra:extra];
}

/// Splash ad click
- (void)splashDidClickForPlacementID:(NSString *)placementID
                               extra:(NSDictionary *)extra {
    [self invokeCallback:@"OnSplashAdClick" placementID:placementID error:nil extra:extra];
}

/// Splash ad closed
- (void)splashDidCloseForPlacementID:(NSString *)placementID
                               extra:(NSDictionary *)extra {
    [self invokeCallback:@"OnSplashAdClose" placementID:placementID error:nil extra:extra];
}

/// Callback when the splash ad is loaded successfully
/// @param isTimeout whether timeout
/// v 5.7.73
- (void)didFinishLoadingSplashADWithPlacementID:(NSString *)placementID
                                      isTimeout:(BOOL)isTimeout {
}

/// Splash ad loading timeout callback
/// v 5.7.73
- (void)didTimeoutLoadingSplashADWithPlacementID:(NSString *)placementID {
    [self invokeCallback:@"OnSplashAdLoadTimeout" placementID:placementID error:nil extra:nil];
}

/// Splash ad failed to display
/// currently supports Pangle, Guangdiantong and Baidu
- (void)splashDidShowFailedForPlacementID:(NSString *)placementID
                                    error:(NSError *)error
                                    extra:(NSDictionary *)extra {
    [self invokeCallback:@"OnSplashAdFailedToShow" placementID:placementID error:error extra:extra];
}

///  Whether the click jump of Splash ad is in the form of Deeplink
/// note: only suport TopOn Adx ad
- (void)splashDeepLinkOrJumpForPlacementID:(NSString *)placementID
                                     extra:(NSDictionary *)extra
                                    result:(BOOL)success {
    NSMutableDictionary *newExtra = [[NSMutableDictionary alloc] initWithDictionary:extra];
    newExtra[@"success"] = @(success);
    [self invokeCallback:@"OnSplashAdDeeplink" placementID:placementID error:nil extra:newExtra];
}

///  Splash ad closes details page
- (void)splashDetailDidClosedForPlacementID:(NSString *)placementID
                                      extra:(NSDictionary *)extra {
    
}

/// Called when splash zoomout view did click
/// note: only suport Pangle splash zoomout view and the Tencent splash V+ ad
- (void)splashZoomOutViewDidClickForPlacementID:(NSString *)placementID
                                          extra:(NSDictionary *)extra {
    
}

/// Called when splash zoomout view did close
/// note: only suport Pangle splash zoomout view and the Tencent splash V+ ad
- (void)splashZoomOutViewDidCloseForPlacementID:(NSString *)placementID
                                          extra:(NSDictionary *)extra {
    
}

/// This callback is triggered when the skip button is customized.
/// note: only suport TopOn MyOffer, TopOn Adx and TopOn OnlineApi
/// 5.7.61+
- (void)splashCountdownTime:(NSInteger)countdown
             forPlacementID:(NSString *)placementID
                      extra:(NSDictionary *)extra {
    
}

#pragma mark - ATAdLoadingDelegate
/// Callback when the successful loading of the ad
- (void)didFinishLoadingADWithPlacementID:(NSString *)placementID {
    [self invokeCallback:@"OnSplashAdLoaded" placementID:placementID error:nil extra:nil];
}

/// Callback of ad loading failure
- (void)didFailToLoadADWithPlacementID:(NSString*)placementID
                                 error:(NSError*)error {
    error = error != nil ? error : [NSError errorWithDomain:@"com.secmtp.Unity3DPackage" code:100001 userInfo:@{NSLocalizedDescriptionKey:@"AT has failed to load ad", NSLocalizedFailureReasonErrorKey:@"AT has failed to load ad"}];
    [self invokeCallback:@"OnSplashAdLoadFailure" placementID:placementID error:error extra:nil];
    
}

/// Ad start load
- (void)didStartLoadingADSourceWithPlacementID:(NSString *)placementID
                                         extra:(NSDictionary*)extra {
    [self invokeCallback:@"startLoadingADSource" placementID:placementID error:nil extra:extra];
    
}
/// Ad load success
- (void)didFinishLoadingADSourceWithPlacementID:(NSString *)placementID
                                          extra:(NSDictionary*)extra {
    self.adInfoDict = extra;
    [self invokeCallback:@"finishLoadingADSource" placementID:placementID error:nil extra:extra];
    
}
/// Ad load fail
- (void)didFailToLoadADSourceWithPlacementID:(NSString*)placementID
                                       extra:(NSDictionary*)extra
                                       error:(NSError*)error {
    [self invokeCallback:@"failToLoadADSource" placementID:placementID error:error extra:extra];
}

/// Ad start bidding
- (void)didStartBiddingADSourceWithPlacementID:(NSString *)placementID
                                         extra:(NSDictionary*)extra {
    [self invokeCallback:@"startBiddingADSource" placementID:placementID error:nil extra:extra];
}

/// Ad bidding success
- (void)didFinishBiddingADSourceWithPlacementID:(NSString *)placementID
                                          extra:(NSDictionary*)extra {
    [self invokeCallback:@"finishBiddingADSource" placementID:placementID error:nil extra:extra];
}

/// Ad bidding fail
- (void)didFailBiddingADSourceWithPlacementID:(NSString*)placementID
                                        extra:(NSDictionary*)extra
                                        error:(NSError*)error {
    [self invokeCallback:@"failBiddingADSource" placementID:placementID error:error extra:extra];
}

#pragma mark - ATAdMultipleLoadingDelegate
- (void)didFinishMultipleLoadingADWithPlacementID:(NSString *)placementID
                                   requestingInfo:(ATAdRequestingInfo *)requestingInfo {
    NSMutableDictionary *requestingInfoDict = [NSMutableDictionary dictionaryWithCapacity:0];
    if (requestingInfo != nil) {
        if (requestingInfo.biddingAdInfoArrray) {
            requestingInfoDict[@"biddingAttemptAdInfoList"] = requestingInfo.biddingAdInfoArrray;
        }
        if (requestingInfo.loadingAdInfoArrray) {
            requestingInfoDict[@"loadingAdInfoList"] = requestingInfo.loadingAdInfoArrray;
        }
    }
    [self invokeCallback:@"OnAdMultipleLoaded" placementID:placementID error:nil extra:[requestingInfoDict copy]];
}


@end
