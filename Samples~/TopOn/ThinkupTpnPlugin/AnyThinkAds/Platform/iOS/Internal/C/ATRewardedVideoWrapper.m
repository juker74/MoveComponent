//
//  ATRewardedVideoWrapper.m
//  UnityContainer
//
//  Created by Martin Lau on 08/08/2018.
//  Copyright © 2018 Martin Lau. All rights reserved.
//

#import "ATRewardedVideoWrapper.h"
#import "ATUnityUtilities.h"
#import <AnyThinkSDK/AnyThinkSDK.h>

NSString *const kLoadExtraUserIDKey = @"UserId";
NSString *const kLoadExtraMediaExtraKey = @"UserExtraData";
@interface ATRewardedVideoWrapper()<ATRewardedVideoDelegate, ATAdMultipleLoadingDelegate>

@property (nonatomic, strong) NSMutableDictionary *adInfoDict;

@end
@implementation ATRewardedVideoWrapper
+(instancetype)sharedInstance {
    static ATRewardedVideoWrapper *sharedInstance = nil;
    static dispatch_once_t onceToken;
    dispatch_once(&onceToken, ^{
        sharedInstance = [[ATRewardedVideoWrapper alloc] init];
    });
    return sharedInstance;
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
    
    if ([selector isEqualToString:@"loadRewardedVideoWithPlacementID:customDataJSONString:callback:"]) {
        [self loadRewardedVideoWithPlacementID:firstObject customDataJSONString:lastObject callback:callback];
    } else if ([selector isEqualToString:@"rewardedVideoReadyForPlacementID:"]) {
        return [NSNumber numberWithBool:[self rewardedVideoReadyForPlacementID:firstObject]];
    } else if ([selector isEqualToString:@"showRewardedVideoWithPlacementID:extraJsonString:"]) {
        [self showRewardedVideoWithPlacementID:firstObject extraJsonString:lastObject];
    } else if ([selector isEqualToString:@"checkAdStatus:"]) {
        return [self checkAdStatus:firstObject];
    } else if ([selector isEqualToString:@"clearCache"]) {
        [self clearCache];
    } else if ([selector isEqualToString:@"setExtra:"]) {
        [self setExtra:firstObject];
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
    }else if ([selector isEqualToString:@"autoLoadRewardedVideoReadyForPlacementID:"]){
        return [NSNumber numberWithBool:[self autoLoadRewardedVideoReadyForPlacementID:firstObject]];
    }else if ([selector isEqualToString:@"getAutoValidAdCaches:"]){
        return [self getAutoValidAdCaches:firstObject];
    }else if ([selector isEqualToString:@"setAutoLocalExtra:customDataJSONString:"]){
        [self setAutoLocalExtra:firstObject customDataJSONString:lastObject];
    }else if ([selector isEqualToString:@"entryAutoAdScenarioWithPlacementID:scenarioID:"]){
        [self entryAutoAdScenarioWithPlacementID:firstObject scenarioID:lastObject];
    }else if ([selector isEqualToString:@"entryAutoAdScenarioWithPlacementID:scenarioID:tkExtraJson:"]){
        [self entryAutoAdScenarioWithPlacementID:firstObject scenarioID:secondObject tkExtraJson:lastObject];   
    }
    else if ([selector isEqualToString:@"showAutoRewardedVideoWithPlacementID:extraJsonString:"]){
        [self showAutoRewardedVideoWithPlacementID:firstObject extraJsonString:lastObject];
    }else if ([selector isEqualToString:@"checkAutoAdStatus:"]) {
        return [self checkAutoAdStatus:firstObject];
    } 
    
    return nil;
}
#pragma mark - normal
-(void) loadRewardedVideoWithPlacementID:(NSString*)placementID customDataJSONString:(NSString*)customDataJSONString callback:(void(*)(const char*, const char*))callback {
    NSLog(@"iOS: ATRewardedVideoWrapper::loadRewardedVideoWithPlacementID placementID=%@ customDataJSONString=%@", placementID, customDataJSONString);
    [self setCallBack:callback forKey:placementID];
    NSMutableDictionary *extra = [NSMutableDictionary dictionary];
    if ([customDataJSONString isKindOfClass:[NSString class]] && [customDataJSONString length] > 0) {
        NSDictionary *extraDict = [NSJSONSerialization JSONObjectWithData:[customDataJSONString dataUsingEncoding:NSUTF8StringEncoding] options:NSJSONReadingAllowFragments error:nil];
        NSLog(@"iOS: extraDict = %@", extraDict);
        
        if (extraDict[kLoadExtraUserIDKey] != nil) { extra[kATAdLoadingExtraUserIDKey] = extraDict[kLoadExtraUserIDKey]; }
        if (extraDict[kLoadExtraMediaExtraKey] != nil) { extra[kATAdLoadingExtraMediaExtraKey] = extraDict[kLoadExtraMediaExtraKey]; }

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
    
    [[ATAdManager sharedManager] loadADWithPlacementID:placementID extra:[extra isKindOfClass:[NSMutableDictionary class]] ? extra : nil delegate:self];
    [[ATAdManager sharedManager] setMultipleLoadingDelegate:self placementId:placementID];
}

-(BOOL) rewardedVideoReadyForPlacementID:(NSString*)placementID {
    BOOL ready = [[ATAdManager sharedManager] rewardedVideoReadyForPlacementID:placementID];
    NSLog(@"iOS: ATRewardedVideoWrapper::rewardedVideoReadyForPlacementID placementID=%@ ready=%d", placementID, ready);
    return ready;
}

-(NSString*) checkAdStatus:(NSString *)placementID {
    NSLog(@"iOS: ATRewardedVideoWrapper::checkAdStatus placementID=%@", placementID);
    ATCheckLoadModel *checkLoadModel = [[ATAdManager sharedManager] checkRewardedVideoLoadStatusForPlacementID:placementID];
    NSMutableDictionary *statusDict = [NSMutableDictionary dictionary];
    statusDict[@"isLoading"] = @(checkLoadModel.isLoading);
    statusDict[@"isReady"] = @(checkLoadModel.isReady);
    statusDict[@"adInfo"] = checkLoadModel.adOfferInfo;
    NSLog(@"iOS: ATRewardedVideoWrapper::statusDict = %@", statusDict);
    return statusDict.jsonFilterString;
}

-(NSString*) getValidAdCaches:(NSString *)placementID {
    NSLog(@"iOS: ATRewardedVideoWrapper::getValidAdCaches placementID=%@", placementID);
    NSArray *array = [[ATAdManager sharedManager] getRewardedVideoValidAdsForPlacementID:placementID];
    NSLog(@"iOS: ATRewardedVideoWrapper::getValidAdCaches array=%@", array);
    return array.jsonFilterString;
}

-(void) showRewardedVideoWithPlacementID:(NSString*)placementID extraJsonString:(NSString*)showConfigJsonString {
    NSDictionary *showCongifDict = ([showConfigJsonString isKindOfClass:[NSString class]] && [showConfigJsonString dataUsingEncoding:NSUTF8StringEncoding] != nil) ? [NSJSONSerialization JSONObjectWithData:[showConfigJsonString dataUsingEncoding:NSUTF8StringEncoding] options:NSJSONReadingAllowFragments error:nil] : nil;
    NSLog(@"iOS: ATRewardedVideoWrapper::showRewardedVideoWithPlacementID = %@ extraJsonString = %@", placementID,showConfigJsonString);
    
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
      
    if (showCustomExt.length > 0 || contentInfoArray.count > 0) {
        [[ATAdManager sharedManager] showRewardedVideoWithPlacementID:placementID config:config inViewController:[ATUnityUtilities topViewController] delegate:self];
        NSLog(@"iOS: ATRewardedVideoWrapper::showRewardedVideoWithPlacementID placementID=%@ scenarioId=%@ showCustomExt=%@ contentInfoArray=%@ showConfig=%@", placementID, scenarioId, showCustomExt, contentInfoArray, showConfigJsonString);
    } else {
         
        [[ATAdManager sharedManager] showRewardedVideoWithPlacementID:placementID scene:scenarioId inViewController:[ATUnityUtilities topViewController] delegate:self];
    }
    
}

-(void) clearCache {
    NSLog(@"iOS: ATRewardedVideoWrapper::clearCache");
}

-(void) setExtra:(NSString*)extra {
    NSLog(@"iOS: ATRewardedVideoWrapper::setExtra extra=%@", extra);
    if ([extra isKindOfClass:[NSString class]]) {
        NSDictionary *extraDict = [NSJSONSerialization JSONObjectWithData:[extra dataUsingEncoding:NSUTF8StringEncoding] options:NSJSONReadingAllowFragments error:nil];
        if ([extraDict isKindOfClass:[NSDictionary class]]) [[ATAdManager sharedManager] setExtra:extraDict];
    }
}

- (void)entryScenarioWithPlacementID:(NSString *)placementID scenarioID:(NSString *)scenarioID tkExtraJson:(NSString *)tkExtraJson{
    NSLog(@"iOS: ATRewardedVideoWrapper::entryScenarioWithPlacementID placementID=%@ scenarioID=%@ tkExtraJson=%@", placementID, scenarioID, tkExtraJson);
    [[ATAdManager sharedManager] entryRewardedVideoScenarioWithPlacementID:placementID scene:scenarioID];
}

-(NSString*) scriptWrapperClass {
    return @"ATRewardedVideoWrapper";
}

#pragma mark - auto
-(void) addAutoLoadAdPlacementID:(NSString*)placementID callback:(void(*)(const char*, const char*))callback {
    NSLog(@"iOS: ATRewardedVideoWrapper::addAutoLoadAdPlacementID placementID=%@", placementID);
    if (placementID == nil) {
        return;
    }
    
    [ATRewardedVideoAutoAdManager sharedInstance].delegate = self;
    
    
    NSArray *placementIDArray = [self jsonStrToArray:placementID];
    
    [placementIDArray enumerateObjectsUsingBlock:^(NSString * _Nonnull obj, NSUInteger idx, BOOL * _Nonnull stop) {
        [self setCallBack:callback forKey:obj];
        NSLog(@"iOS: addAutoLoadAdPlacementID--%@",placementID);
    }];
    [[ATRewardedVideoAutoAdManager sharedInstance] addAutoLoadAdPlacementIDArray:placementIDArray];
    
}

-(void) removeAutoLoadAdPlacementID:(NSString*)placementID{
    NSLog(@"iOS: ATRewardedVideoWrapper::removeAutoLoadAdPlacementID placementID=%@", placementID);
    
    if (placementID == nil) {
           return;
    }
    
    NSArray *placementIDArray = [self jsonStrToArray:placementID];
    
    [[ATRewardedVideoAutoAdManager sharedInstance] removeAutoLoadAdPlacementIDArray:placementIDArray];
}

-(BOOL) autoLoadRewardedVideoReadyForPlacementID:(NSString*)placementID {
    NSLog(@"iOS: ATRewardedVideoWrapper::autoLoadRewardedVideoReadyForPlacementID placementID=%@ ready=%d", placementID, [[ATRewardedVideoAutoAdManager sharedInstance] autoLoadRewardedVideoReadyForPlacementID:placementID]);
    return [[ATRewardedVideoAutoAdManager sharedInstance] autoLoadRewardedVideoReadyForPlacementID:placementID];
}

-(NSString*) getAutoValidAdCaches:(NSString *)placementID{
    NSLog(@"iOS: ATRewardedVideoWrapper::getAutoValidAdCaches placementID=%@", placementID);
    NSArray *array = [[ATRewardedVideoAutoAdManager sharedInstance] checkValidAdCachesWithPlacementID:placementID];
    NSLog(@"iOS: ATRewardedVideoWrapper::getAutoValidAdCaches array=%@", array);
    return array.jsonFilterString;
}

-(NSString*) checkAutoAdStatus:(NSString *)placementID {
    NSLog(@"iOS: ATRewardedVideoWrapper::checkAutoAdStatus placementID=%@", placementID);
    ATCheckLoadModel *checkLoadModel = [[ATRewardedVideoAutoAdManager sharedInstance] checkRewardedVideoLoadStatusForPlacementID:placementID];
    NSMutableDictionary *statusDict = [NSMutableDictionary dictionary];
    statusDict[@"isLoading"] = @(checkLoadModel.isLoading);
    statusDict[@"isReady"] = @(checkLoadModel.isReady);
    statusDict[@"adInfo"] = checkLoadModel.adOfferInfo;
    NSLog(@"iOS: ATRewardedVideoWrapper::checkAutoAdStatus statusDict = %@", statusDict);
    return statusDict.jsonFilterString;
    
}

-(void) setAutoLocalExtra:(NSString*)placementID customDataJSONString:(NSString*)customDataJSONString{
    NSLog(@"iOS: setAutoLocalExtra::placementID = %@ customDataJSONString: %@", placementID,customDataJSONString);

    
    
    if ([customDataJSONString isKindOfClass:[NSString class]]) {
        
        NSDictionary *extraDict = [NSJSONSerialization JSONObjectWithData:[customDataJSONString dataUsingEncoding:NSUTF8StringEncoding] options:NSJSONReadingAllowFragments error:nil];
        
        NSMutableDictionary *extra = [NSMutableDictionary dictionary];

        
        if ([extraDict isKindOfClass:[NSDictionary class]]) {
            
            if (extraDict[kLoadExtraUserIDKey] != nil) {
                extra[kATAdLoadingExtraUserIDKey] = extraDict[kLoadExtraUserIDKey];
            }
            if (extraDict[kLoadExtraMediaExtraKey] != nil) { extra[kATAdLoadingExtraMediaExtraKey] = extraDict[kLoadExtraMediaExtraKey];
            }
            
        };
        
        
        
        [[ATRewardedVideoAutoAdManager sharedInstance] setLocalExtra:extra placementID:placementID];
    }
}

-(void) entryAutoAdScenarioWithPlacementID:(NSString*)placementID scenarioID:(NSString*)scenarioID{
    NSLog(@"iOS: ATRewardedVideoWrapper::entryAutoAdScenarioWithPlacementID placementID=%@ scenarioID=%@", placementID, scenarioID);
    [[ATRewardedVideoAutoAdManager sharedInstance] entryAdScenarioWithPlacementID:placementID scenarioID:scenarioID];
}

-(void) entryAutoAdScenarioWithPlacementID:(NSString*)placementID scenarioID:(NSString*)scenarioID tkExtraJson:(NSString*)tkExtraJson{
    NSLog(@"iOS: ATRewardedVideoWrapper::entryAutoAdScenarioWithPlacementID placementID=%@ scenarioID=%@ tkExtraJson=%@", placementID, scenarioID, tkExtraJson);
    [[ATRewardedVideoAutoAdManager sharedInstance] entryAdScenarioWithPlacementID:placementID scenarioID:scenarioID];
}

-(void) showAutoRewardedVideoWithPlacementID:(NSString*)placementID extraJsonString:(NSString*)extraJsonString {
    
    NSDictionary *showCongifDict = ([extraJsonString isKindOfClass:[NSString class]] && [extraJsonString dataUsingEncoding:NSUTF8StringEncoding] != nil) ? [NSJSONSerialization JSONObjectWithData:[extraJsonString dataUsingEncoding:NSUTF8StringEncoding] options:NSJSONReadingAllowFragments error:nil] : nil;
    
    NSLog(@"iOS: ATRewardedVideoWrapper::showAutoRewardedVideoWithPlacementID = %@ extraJsonString = %@", placementID,extraJsonString);
    
    NSLog(@"iOS: ATRewardedVideoWrapper::extraDict = %@", showCongifDict);
    
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
      
    [[ATRewardedVideoAutoAdManager sharedInstance] showAutoLoadRewardedVideoWithPlacementID:placementID showConfig:config inViewController:[ATUnityUtilities topViewController] delegate:self];
        NSLog(@"iOS: ATRewardedVideoWrapper::showRewardedVideoWithPlacementID placementID=%@ scenarioId=%@ showCustomExt=%@ contentInfoArray=%@ showConfig=%@", placementID, scenarioId, showCustomExt, contentInfoArray, extraJsonString);
}

#pragma mark - delegate
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
    [self invokeCallback:@"OnRewardedVideoLoaded" placementID:placementID error:nil extra:nil];
}

-(void) didFailToLoadADWithPlacementID:(NSString*)placementID error:(NSError*)error {
    error = error != nil ? error : [NSError errorWithDomain:@"com.secmtp.Unity3DPackage" code:100001 userInfo:@{NSLocalizedDescriptionKey:@"AT has failed to load ad", NSLocalizedFailureReasonErrorKey:@"AT has failed to load ad"}];
    [self invokeCallback:@"OnRewardedVideoLoadFailure" placementID:placementID error:error extra:nil];
}

-(void) rewardedVideoDidStartPlayingForPlacementID:(NSString*)placementID extra:(NSDictionary *)extra {
    [self invokeCallback:@"OnRewardedVideoPlayStart" placementID:placementID error:nil extra:extra];
    [[NSNotificationCenter defaultCenter] postNotificationName:kATUnityUtilitiesRewardedVideoImpressionNotification object:nil];
    [self invokeCallback:@"OnAdRevenuePaid" placementID:placementID error:nil extra:extra];
}

-(void) rewardedVideoDidEndPlayingForPlacementID:(NSString*)placementID extra:(NSDictionary *)extra {
    [self invokeCallback:@"OnRewardedVideoPlayEnd" placementID:placementID error:nil extra:extra];
}

-(void) rewardedVideoDidFailToPlayForPlacementID:(NSString*)placementID error:(NSError*)error extra:(NSDictionary *)extra {
    error = error != nil ? error : [NSError errorWithDomain:@"com.secmtp.Unity3DPackage" code:100001 userInfo:@{NSLocalizedDescriptionKey:@"AT has failed to play video", NSLocalizedFailureReasonErrorKey:@"AT has failed to play video"}];
    [self invokeCallback:@"OnRewardedVideoPlayFailure" placementID:placementID error:error extra:extra];
}

-(void) rewardedVideoDidCloseForPlacementID:(NSString*)placementID rewarded:(BOOL)rewarded extra:(NSDictionary *)extra {
    [self invokeCallback:@"OnRewardedVideoClose" placementID:placementID error:nil extra:@{@"rewarded":@(rewarded), @"extra":extra != nil ? extra : @{}}];
    [[NSNotificationCenter defaultCenter] postNotificationName:kATUnityUtilitiesRewardedVideoCloseNotification object:nil];
}

-(void) rewardedVideoDidClickForPlacementID:(NSString*)placementID extra:(NSDictionary *)extra {
    [self invokeCallback:@"OnRewardedVideoClick" placementID:placementID error:nil extra:extra];
}

-(void) rewardedVideoDidRewardSuccessForPlacemenID:(NSString*)placementID extra:(NSDictionary*)extra {
    [self invokeCallback:@"OnRewardedVideoReward" placementID:placementID error:nil extra:extra];
}

//again
// rewarded video again
-(void) rewardedVideoAgainDidStartPlayingForPlacementID:(NSString*)placementID extra:(NSDictionary*)extra {
    [self invokeCallback:@"OnRewardedVideoAdAgainPlayStart" placementID:placementID error:nil extra:extra];
}

-(void) rewardedVideoAgainDidEndPlayingForPlacementID:(NSString*)placementID extra:(NSDictionary*)extra {
    [self invokeCallback:@"OnRewardedVideoAdAgainPlayEnd" placementID:placementID error:nil extra:extra];
}

-(void) rewardedVideoAgainDidFailToPlayForPlacementID:(NSString*)placementID error:(NSError*)error extra:(NSDictionary*)extra {
    error = error != nil ? error : [NSError errorWithDomain:@"com.secmtp.Unity3DPackage" code:100001 userInfo:@{NSLocalizedDescriptionKey:@"AT has failed to play video", NSLocalizedFailureReasonErrorKey:@"AT has failed to play video"}];
    [self invokeCallback:@"OnRewardedVideoAdAgainPlayFailed" placementID:placementID error:error extra:extra];
}

-(void) rewardedVideoAgainDidClickForPlacementID:(NSString*)placementID extra:(NSDictionary*)extra {
    [self invokeCallback:@"OnRewardedVideoAdAgainPlayClicked" placementID:placementID error:nil extra:extra];
}

-(void) rewardedVideoAgainDidRewardSuccessForPlacemenID:(NSString*)placementID extra:(NSDictionary*)extra {
    [self invokeCallback:@"OnAgainReward" placementID:placementID error:nil extra:extra];
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
