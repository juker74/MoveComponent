//
//  ATInterstitialAdWrapper.m
//  UnityContainer
//
//  Created by Martin Lau on 2019/1/8.
//  Copyright © 2019 Martin Lau. All rights reserved.
//

#import "ATInterstitialAdWrapper.h"
#import "ATUnityUtilities.h"
#import <AnyThinkSDK/AnyThinkSDK.h>

NSString *const kLoadUseRVAsInterstitialKey = @"UseRewardedVideoAsInterstitial";
NSString *const kInterstitialExtraAdSizeKey = @"interstitial_ad_size";
static NSString *kATInterstitialSizeUsesPixelFlagKey = @"uses_pixel";

@interface ATInterstitialAdWrapper()<ATInterstitialDelegate, ATAdMultipleLoadingDelegate>

@property (nonatomic, strong) NSMutableDictionary *adInfoDict;

@end
@implementation ATInterstitialAdWrapper
+(instancetype)sharedInstance {
    static ATInterstitialAdWrapper *sharedInstance = nil;
    static dispatch_once_t onceToken;
    dispatch_once(&onceToken, ^{
        sharedInstance = [[ATInterstitialAdWrapper alloc] init];
    });
    return sharedInstance;
}

-(NSString*) scriptWrapperClass {
    return @"ATInterstitialAdWrapper";
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
    
    if ([selector isEqualToString:@"loadInterstitialAdWithPlacementID:customDataJSONString:callback:"]) {
        [self loadInterstitialAdWithPlacementID:firstObject customDataJSONString:lastObject callback:callback];
    } else if ([selector isEqualToString:@"interstitialAdReadyForPlacementID:"]) {
        return [NSNumber numberWithBool:[self interstitialAdReadyForPlacementID:firstObject]];
    } else if ([selector isEqualToString:@"showInterstitialAdWithPlacementID:extraJsonString:"]) {
        [self showInterstitialAdWithPlacementID:firstObject extraJsonString:lastObject];
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
    // auto
    else if ([selector isEqualToString:@"addAutoLoadAdPlacementID:callback:"]){
        [self addAutoLoadAdPlacementID:firstObject callback:callback];
    }else if ([selector isEqualToString:@"removeAutoLoadAdPlacementID:"]){
        [self removeAutoLoadAdPlacementID:firstObject];
    }else if ([selector isEqualToString:@"autoLoadInterstitialAdReadyForPlacementID:"]){
        return [NSNumber numberWithBool:[self autoLoadInterstitialAdReadyForPlacementID:firstObject]];
    }else if ([selector isEqualToString:@"getAutoValidAdCaches:"]){
        return [self getAutoValidAdCaches:firstObject];
    }else if ([selector isEqualToString:@"setAutoLocalExtra:customDataJSONString:"]){
        [self setAutoLocalExtra:firstObject customDataJSONString:lastObject];
    }else if ([selector isEqualToString:@"entryAutoAdScenarioWithPlacementID:scenarioID:"]){
        [self entryAutoAdScenarioWithPlacementID:firstObject scenarioID:lastObject];
    }else if ([selector isEqualToString:@"entryAutoAdScenarioWithPlacementID:scenarioID:tkExtraJson:"]){
        [self entryAutoAdScenarioWithPlacementID:firstObject scenarioID:secondObject tkExtraJson:lastObject];
    }else if ([selector isEqualToString:@"showAutoInterstitialAd:extraJsonString:"]){
        [self showAutoInterstitialAd:firstObject extraJsonString:lastObject];
    }else if ([selector isEqualToString:@"checkAutoAdStatus:"]) {
        return [self checkAutoAdStatus:firstObject];
    } 
    
    return nil;
}

-(void) loadInterstitialAdWithPlacementID:(NSString*)placementID customDataJSONString:(NSString*)customDataJSONString callback:(void(*)(const char*, const char*))callback {
    NSLog(@"iOS: ATInterstitialAdWrapper::loadInterstitialAdWithPlacementID placementID=%@ customDataJSONString=%@", placementID, customDataJSONString);
    [self setCallBack:callback forKey:placementID];
    NSMutableDictionary *extra = [NSMutableDictionary dictionary];
    if ([customDataJSONString isKindOfClass:[NSString class]] && [customDataJSONString length] > 0) {
        NSDictionary *extraDict = [NSJSONSerialization JSONObjectWithData:[customDataJSONString dataUsingEncoding:NSUTF8StringEncoding] options:NSJSONReadingAllowFragments error:nil];
        NSLog(@"iOS: extraDict = %@", extraDict);

        if (extraDict[kLoadUseRVAsInterstitialKey] != nil) {
            extra[kATInterstitialExtraUsesRewardedVideo] = @([extraDict[kLoadUseRVAsInterstitialKey] boolValue]);
        }
        
        CGFloat scale = [extraDict[kATInterstitialSizeUsesPixelFlagKey] boolValue] ? [UIScreen mainScreen].nativeScale : 1.0f;
        if ([extraDict[kInterstitialExtraAdSizeKey] isKindOfClass:[NSString class]] && [[extraDict[kInterstitialExtraAdSizeKey] componentsSeparatedByString:@"x"] count] == 2) {
            NSArray<NSString*>* com = [extraDict[kInterstitialExtraAdSizeKey] componentsSeparatedByString:@"x"];
            extra[kATInterstitialExtraAdSizeKey] = [NSValue valueWithCGSize:CGSizeMake([com[0] doubleValue] / scale, [com[1] doubleValue] / scale)];

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
    }
    
    NSLog(@"iOS: ATInterstitialAdWrapper::extra = %@", extra);
    [[ATAdManager sharedManager] loadADWithPlacementID:placementID extra:extra != nil ? extra : nil delegate:self];
    [[ATAdManager sharedManager] setMultipleLoadingDelegate:self placementId:placementID];
}

-(BOOL) interstitialAdReadyForPlacementID:(NSString*)placementID {
    BOOL ready = [[ATAdManager sharedManager] interstitialReadyForPlacementID:placementID];
    NSLog(@"iOS: ATInterstitialAdWrapper::interstitialAdReadyForPlacementID placementID=%@ ready=%d", placementID, ready);
    return ready;
}

-(NSString*) getValidAdCaches:(NSString *)placementID {
    NSLog(@"iOS: ATInterstitialAdWrapper::getValidAdCaches placementID=%@", placementID);
    NSArray *array = [[ATAdManager sharedManager] getInterstitialValidAdsForPlacementID:placementID];
    NSLog(@"iOS: ATInterstitialAdWrapper::getValidAdCaches array=%@", array);
    return array.jsonFilterString;
}

-(void) showInterstitialAdWithPlacementID:(NSString*)placementID extraJsonString:(NSString*)showConfigJsonString {
    NSLog(@"iOS: ATInterstitialAdWrapper::showInterstitialAdWithPlacementID placementID=%@ extraJsonString=%@", placementID, showConfigJsonString);
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
      
    if (contentInfoArray.count > 0 || showCustomExt.length > 0) {
        [[ATAdManager sharedManager] showInterstitialWithPlacementID:placementID showConfig:config inViewController:[ATUnityUtilities topViewController] delegate:self nativeMixViewBlock:^(ATNativeMixInterstitialView * _Nonnull selfRenderingMixInterstitialView) {
            
        }];
        NSLog(@"iOS: ATInterstitialAdWrapper::showInterstitialAdWithPlacementID placementID=%@ scenarioId=%@ showCustomExt=%@ contentInfoArray=%@ showConfig=%@", placementID, scenarioId, showCustomExt, contentInfoArray,  showConfigJsonString);
    } else {
        [[ATAdManager sharedManager] showInterstitialWithPlacementID:placementID scene:scenarioId inViewController:[ATUnityUtilities topViewController] delegate:self];
    }
    
}

-(NSString*) checkAdStatus:(NSString *)placementID {
    NSLog(@"iOS: ATInterstitialAdWrapper::checkAdStatus placementID=%@", placementID);
    ATCheckLoadModel *checkLoadModel = [[ATAdManager sharedManager] checkInterstitialLoadStatusForPlacementID:placementID];
    NSMutableDictionary *statusDict = [NSMutableDictionary dictionary];
    statusDict[@"isLoading"] = @(checkLoadModel.isLoading);
    statusDict[@"isReady"] = @(checkLoadModel.isReady);
    statusDict[@"adInfo"] = checkLoadModel.adOfferInfo;
    NSLog(@"iOS: ATInterstitialAdWrapper::statusDict = %@", statusDict);
    return statusDict.jsonFilterString;
}
- (void)entryScenarioWithPlacementID:(NSString *)placementID scenarioID:(NSString *)scenarioID tkExtraJson:(NSString *)tkExtraJson{
    NSLog(@"iOS: ATInterstitialAdWrapper::entryScenarioWithPlacementID placementID=%@ scenarioID=%@ tkExtraJson=%@", placementID, scenarioID, tkExtraJson);
    [[ATAdManager sharedManager] entryInterstitialScenarioWithPlacementID:placementID scene:scenarioID];
}
-(void) clearCache {
    NSLog(@"iOS: ATInterstitialAdWrapper::clearCache");
}

#pragma mark - auto
-(void) addAutoLoadAdPlacementID:(NSString*)placementID callback:(void(*)(const char*, const char*))callback {
    NSLog(@"iOS: ATInterstitialAdWrapper::addAutoLoadAdPlacementID placementID=%@", placementID);
    if (placementID == nil) {
        return;
    }
    
    [ATInterstitialAutoAdManager sharedInstance].delegate = self;
    
    NSArray *placementIDArray = [self jsonStrToArray:placementID];

    [placementIDArray enumerateObjectsUsingBlock:^(NSString * _Nonnull obj, NSUInteger idx, BOOL * _Nonnull stop) {
        [self setCallBack:callback forKey:obj];
        NSLog(@"iOS: addAutoLoadAdPlacementID--%@",placementID);
    }];
    
    
    [[ATInterstitialAutoAdManager sharedInstance] addAutoLoadAdPlacementIDArray:placementIDArray];
}

-(void) removeAutoLoadAdPlacementID:(NSString*)placementID{
    NSLog(@"iOS: ATInterstitialAdWrapper::removeAutoLoadAdPlacementID placementID=%@", placementID);
    
    if (placementID == nil) {
        return;
    }
    
    NSArray *placementIDArray = [self jsonStrToArray:placementID];
    
    [[ATInterstitialAutoAdManager sharedInstance] removeAutoLoadAdPlacementIDArray:placementIDArray];
}

-(BOOL) autoLoadInterstitialAdReadyForPlacementID:(NSString*)placementID {
    NSLog(@"iOS: ATInterstitialAdWrapper::autoLoadInterstitialAdReadyForPlacementID placementID=%@ ready=%d", placementID, [[ATInterstitialAutoAdManager sharedInstance] autoLoadInterstitialReadyForPlacementID:placementID]);
    return [[ATInterstitialAutoAdManager sharedInstance] autoLoadInterstitialReadyForPlacementID:placementID];
}

-(NSString*) getAutoValidAdCaches:(NSString *)placementID{
    NSLog(@"iOS: ATInterstitialAdWrapper::getAutoValidAdCaches placementID=%@", placementID);
    NSArray *array = [[ATInterstitialAutoAdManager sharedInstance] checkValidAdCachesWithPlacementID:placementID];
    NSLog(@"iOS: ATInterstitialAdWrapper::getAutoValidAdCaches array=%@", array);
    
    return array.jsonFilterString;
}

-(NSString*) checkAutoAdStatus:(NSString *)placementID {
    NSLog(@"iOS: ATInterstitialAdWrapper::checkAutoAdStatus placementID=%@", placementID);
    ATCheckLoadModel *checkLoadModel = [[ATInterstitialAutoAdManager sharedInstance] checkInterstitialLoadStatusForPlacementID:placementID];
    
    NSMutableDictionary *statusDict = [NSMutableDictionary dictionary];
    statusDict[@"isLoading"] = @(checkLoadModel.isLoading);
    statusDict[@"isReady"] = @(checkLoadModel.isReady);
    statusDict[@"adInfo"] = checkLoadModel.adOfferInfo;
    
    NSLog(@"iOS: ATInterstitialAdWrapper::checkAutoAdStatus statusDict = %@", statusDict);
    return statusDict.jsonFilterString;
}

-(void) setAutoLocalExtra:(NSString*)placementID customDataJSONString:(NSString*)customDataJSONString{
    NSLog(@"iOS: ATInterstitialAdWrapper::setAutoLocalExtra placementID=%@ customDataJSONString=%@", placementID, customDataJSONString);
    if ([customDataJSONString isKindOfClass:[NSString class]]) {
        
        NSDictionary *extraDict = [NSJSONSerialization JSONObjectWithData:[customDataJSONString dataUsingEncoding:NSUTF8StringEncoding] options:NSJSONReadingAllowFragments error:nil];
        
        NSMutableDictionary *extra = [NSMutableDictionary dictionary];

        if ([extraDict isKindOfClass:[NSDictionary class]]) {
            
            if (extraDict[kLoadUseRVAsInterstitialKey] != nil) {
                extra[kATInterstitialExtraUsesRewardedVideo] = @([extraDict[kLoadUseRVAsInterstitialKey] boolValue]);
            }
            
            CGFloat scale = [extraDict[kATInterstitialSizeUsesPixelFlagKey] boolValue] ? [UIScreen mainScreen].nativeScale : 1.0f;
            if ([extraDict[kInterstitialExtraAdSizeKey] isKindOfClass:[NSString class]] && [[extraDict[kInterstitialExtraAdSizeKey] componentsSeparatedByString:@"x"] count] == 2) {
                NSArray<NSString*>* com = [extraDict[kInterstitialExtraAdSizeKey] componentsSeparatedByString:@"x"];
                extra[kATInterstitialExtraAdSizeKey] = [NSValue valueWithCGSize:CGSizeMake([com[0] doubleValue] / scale, [com[1] doubleValue] / scale)];
            }
        }
        
        NSLog(@"iOS: ATInterstitialAdWrapper::setAutoLocalExtra statusDict = %@", extraDict);
        [[ATInterstitialAutoAdManager sharedInstance] setLocalExtra:extra placementID:placementID];
    
    }
}

-(void) entryAutoAdScenarioWithPlacementID:(NSString*)placementID scenarioID:(NSString*)scenarioID{
    NSLog(@"iOS: ATInterstitialAdWrapper::entryAutoAdScenarioWithPlacementID = %@ scenarioID = %@", placementID,scenarioID);

    [[ATInterstitialAutoAdManager sharedInstance] entryAdScenarioWithPlacementID:placementID scenarioID:scenarioID];
}

-(void) entryAutoAdScenarioWithPlacementID:(NSString*)placementID scenarioID:(NSString*)scenarioID tkExtraJson:(NSString*)tkExtraJson{
    NSLog(@"iOS: ATInterstitialAdWrapper::entryAutoAdScenarioWithPlacementID placementID=%@ scenarioID=%@ tkExtraJson=%@", placementID, scenarioID, tkExtraJson);
    [[ATInterstitialAutoAdManager sharedInstance] entryAdScenarioWithPlacementID:placementID scenarioID:scenarioID];
}

-(void) showAutoInterstitialAd:(NSString*)placementID extraJsonString:(NSString*)extraJsonString {
    
    NSDictionary *showCongifDict = ([extraJsonString isKindOfClass:[NSString class]] && [extraJsonString dataUsingEncoding:NSUTF8StringEncoding] != nil) ? [NSJSONSerialization JSONObjectWithData:[extraJsonString dataUsingEncoding:NSUTF8StringEncoding] options:NSJSONReadingAllowFragments error:nil] : nil;
    
    NSLog(@"iOS: ATInterstitialAdWrapper::showAutoInterstitialAd = %@ extraJsonString = %@", placementID,extraJsonString);
    
    NSLog(@"iOS: ATInterstitialAdWrapper::extraDict = %@", showCongifDict);
 
    
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
      
    [[ATInterstitialAutoAdManager sharedInstance] showAutoLoadInterstitialWithPlacementID:placementID showConfig:config inViewController:[ATUnityUtilities topViewController] delegate:self];
        NSLog(@"iOS: ATInterstitialAdWrapper::showInterstitialAdWithPlacementID placementID=%@ scenarioId=%@ showCustomExt=%@ contentInfoArray=%@ showConfig=%@", placementID, scenarioId, showCustomExt, contentInfoArray,  extraJsonString);
     
}

#pragma mark - delegate method(s)
// ad
- (void)didStartLoadingADSourceWithPlacementID:(NSString *)placementID extra:(NSDictionary*)extra{
    [self invokeCallback:@"startLoadingADSource" placementID:placementID error:nil extra:extra];
}

- (void)didFinishLoadingADSourceWithPlacementID:(NSString *)placementID extra:(NSDictionary*)extra{
    self.adInfoDict = extra;
    [self invokeCallback:@"finishLoadingADSource" placementID:placementID error:nil extra:extra];
}

- (void)didFailToLoadADSourceWithPlacementID:(NSString*)placementID extra:(NSDictionary*)extra error:(NSError*)error{
    [self invokeCallback:@"failToLoadADSource" placementID:placementID error:error extra:extra];
}

// bidding
- (void)didStartBiddingADSourceWithPlacementID:(NSString *)placementID extra:(NSDictionary*)extra{
    [self invokeCallback:@"startBiddingADSource" placementID:placementID error:nil extra:extra];
}

- (void)didFinishBiddingADSourceWithPlacementID:(NSString *)placementID extra:(NSDictionary*)extra{
    [self invokeCallback:@"finishBiddingADSource" placementID:placementID error:nil extra:extra];
}

- (void)didFailBiddingADSourceWithPlacementID:(NSString*)placementID extra:(NSDictionary*)extra error:(NSError*)error{
    [self invokeCallback:@"failBiddingADSource" placementID:placementID error:error extra:extra];
}



-(void) didFinishLoadingADWithPlacementID:(NSString *)placementID {
    [self invokeCallback:@"OnInterstitialAdLoaded" placementID:placementID error:nil extra:nil];
}

-(void) didFailToLoadADWithPlacementID:(NSString*)placementID error:(NSError*)error {
    error = error != nil ? error : [NSError errorWithDomain:@"com.secmtp.Unity3DPackage" code:100001 userInfo:@{NSLocalizedDescriptionKey:@"AT has failed to load ad", NSLocalizedFailureReasonErrorKey:@"AT has failed to load ad"}];
    [self invokeCallback:@"OnInterstitialAdLoadFailure" placementID:placementID error:error extra:nil];
}

-(void) interstitialDidShowForPlacementID:(NSString*)placementID extra:(NSDictionary *)extra {
    [self invokeCallback:@"OnInterstitialAdShow" placementID:placementID error:nil extra:extra];
    [self invokeCallback:@"OnAdRevenuePaid" placementID:placementID error:nil extra:extra];
    [[NSNotificationCenter defaultCenter] postNotificationName:kATUnityUtilitiesInterstitialImpressionNotification object:nil];
}

-(void) interstitialFailedToShowForPlacementID:(NSString*)placementID error:(NSError*)error extra:(NSDictionary *)extra {
    error = error != nil ? error : [NSError errorWithDomain:@"com.secmtp.Unity3DPackage" code:100001 userInfo:@{NSLocalizedDescriptionKey:@"AT has failed to show ad", NSLocalizedFailureReasonErrorKey:@"AT has failed to show ad"}];
    [self invokeCallback:@"OnInterstitialAdFailedToShow" placementID:placementID error:error extra:nil];
}

-(void) interstitialDidStartPlayingVideoForPlacementID:(NSString*)placementID extra:(NSDictionary *)extra {
    [self invokeCallback:@"OnInterstitialAdVideoPlayStart" placementID:placementID error:nil extra:extra];
}

-(void) interstitialDidEndPlayingVideoForPlacementID:(NSString*)placementID extra:(NSDictionary *)extra {
    [self invokeCallback:@"OnInterstitialAdVideoPlayEnd" placementID:placementID error:nil extra:extra];
}

-(void) interstitialDidFailToPlayVideoForPlacementID:(NSString*)placementID error:(NSError*)error extra:(NSDictionary *)extra {
    [self invokeCallback:@"OnInterstitialAdVideoPlayFailure" placementID:placementID error:error extra:extra];
}

-(void) interstitialDidCloseForPlacementID:(NSString*)placementID extra:(NSDictionary *)extra {
    [self invokeCallback:@"OnInterstitialAdClose" placementID:placementID error:nil extra:extra];
    [[NSNotificationCenter defaultCenter] postNotificationName:kATUnityUtilitiesInterstitialCloseNotification object:nil];
}

-(void) interstitialDidClickForPlacementID:(NSString*)placementID extra:(NSDictionary *)extra {
    [self invokeCallback:@"OnInterstitialAdClick" placementID:placementID error:nil extra:extra];
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
