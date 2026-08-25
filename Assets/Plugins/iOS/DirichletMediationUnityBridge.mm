//
//  DirichletMediationUnityBridge.mm
//  Dirichlet Mediation Unity Bridge for iOS
//
//
//  Created by Dirichlet Unity SDK
//  Copyright © 2025 Dirichlet Inc. All rights reserved.
//

#import "DirichletMediationUnityBridge.h"
#import <DirichletMediationSDK/DirichletMediationSDK.h>
#import <UIKit/UIKit.h>

// Unity callback interface
extern "C" void UnitySendMessage(const char* obj, const char* method, const char* msg);

// Constants
static NSString * const kUnityCallbackObject = @"DirichletMediationEventReceiver";
static NSString * const kUnityCallbackMethod = @"OnNativeEvent";
static NSString * const kUnityLoadCallbackObject = @"DirichletMediationIOSLoadCallbackReceiver";
static NSString * const kUnityLoadCallbackMethod = @"OnLoadCallback";
static NSString * const kUnityInitCallbackObject = @"DirichletMediationIOSInitCallbackReceiver";
static NSString * const kUnityInitCallbackMethod = @"OnInitCallback";

// Helper to convert C string to NSString
static NSString* CreateNSString(const char* cString) {
    return cString ? [NSString stringWithUTF8String:cString] : @"";
}

// Helper to convert NSString to C string (caller must free)
static char* MakeCString(NSString* nsString) {
    if (nsString == nil) {
        return NULL;
    }
    const char* utf8String = [nsString UTF8String];
    char* cString = (char*)malloc(strlen(utf8String) + 1);
    strcpy(cString, utf8String);
    return cString;
}

// Helper to send event to Unity
static void SendEventToUnity(NSString* handleId, NSString* eventName, NSString* adType, NSDictionary* data) {
    NSMutableDictionary* payload = [NSMutableDictionary dictionary];
    payload[@"handle"] = handleId ?: @"";
    payload[@"eventName"] = eventName ?: @"";
    payload[@"adType"] = adType ?: @"";
    if (data) {
        payload[@"data"] = data;
    }
    
    NSError* error = nil;
    NSData* jsonData = [NSJSONSerialization dataWithJSONObject:payload options:0 error:&error];
    if (jsonData && !error) {
        NSString* jsonString = [[NSString alloc] initWithData:jsonData encoding:NSUTF8StringEncoding];
        UnitySendMessage([kUnityCallbackObject UTF8String], [kUnityCallbackMethod UTF8String], [jsonString UTF8String]);
    }
}

// Helper to send load callback to Unity (separate from ad events)
static void SendLoadCallbackToUnity(NSString* handleId, NSString* eventName, NSString* adType, NSDictionary* data) {
    NSMutableDictionary* payload = [NSMutableDictionary dictionary];
    payload[@"handle"] = handleId ?: @"";
    payload[@"eventName"] = eventName ?: @"";
    payload[@"adType"] = adType ?: @"";
    if (data) {
        payload[@"data"] = data;
    }
    
    NSError* error = nil;
    NSData* jsonData = [NSJSONSerialization dataWithJSONObject:payload options:0 error:&error];
    if (jsonData && !error) {
        NSString* jsonString = [[NSString alloc] initWithData:jsonData encoding:NSUTF8StringEncoding];
        UnitySendMessage([kUnityLoadCallbackObject UTF8String], [kUnityLoadCallbackMethod UTF8String], [jsonString UTF8String]);
    }
}

// Helper to send init callback to Unity (async initialization result)
static void SendInitCallbackToUnity(BOOL success, NSError* error, NSString* extraMessage) {
    NSMutableDictionary* payload = [NSMutableDictionary dictionary];
    payload[@"success"] = @(success);

    NSMutableDictionary* data = [NSMutableDictionary dictionary];
    if (error) {
        data[@"code"] = @(error.code);
        data[@"message"] = error.localizedDescription ?: @"";
        if (error.domain) {
            data[@"domain"] = error.domain;
        }
    } else if (extraMessage.length > 0) {
        data[@"message"] = extraMessage;
    }

    if (data.count > 0) {
        payload[@"data"] = data;
    }

    NSError* jsonError = nil;
    NSData* jsonData = [NSJSONSerialization dataWithJSONObject:payload options:0 error:&jsonError];
    if (jsonData && !jsonError) {
        NSString* jsonString = [[NSString alloc] initWithData:jsonData encoding:NSUTF8StringEncoding];
        UnitySendMessage([kUnityInitCallbackObject UTF8String], [kUnityInitCallbackMethod UTF8String], [jsonString UTF8String]);
    }
}

// Parse JSON extras to dictionary
static NSDictionary* ParseExtras(const char* extrasJson) {
    if (!extrasJson || strlen(extrasJson) == 0) {
        return nil;
    }
    
    NSString* jsonString = CreateNSString(extrasJson);
    NSData* jsonData = [jsonString dataUsingEncoding:NSUTF8StringEncoding];
    if (!jsonData) {
        return nil;
    }
    
    NSError* error = nil;
    NSDictionary* dict = [NSJSONSerialization JSONObjectWithData:jsonData options:0 error:&error];
    return error ? nil : dict;
}

#pragma mark - Ad Instance Manager

@interface DirichletMediationInstanceManager : NSObject

@property (nonatomic, strong) NSMutableDictionary<NSString*, id>* adInstances;
@property (nonatomic, strong) dispatch_queue_t syncQueue;

+ (instancetype)shared;
- (void)storeAd:(id)ad forHandle:(NSString*)handleId;
- (id)adForHandle:(NSString*)handleId;
- (void)removeAdForHandle:(NSString*)handleId;
- (NSString*)generateHandle;

@end

@implementation DirichletMediationInstanceManager

+ (instancetype)shared {
    static DirichletMediationInstanceManager* instance = nil;
    static dispatch_once_t onceToken;
    dispatch_once(&onceToken, ^{
        instance = [[DirichletMediationInstanceManager alloc] init];
    });
    return instance;
}

- (instancetype)init {
    if (self = [super init]) {
        _adInstances = [NSMutableDictionary dictionary];
        _syncQueue = dispatch_queue_create("com.dirichlet.mediation.unity.admanager", DISPATCH_QUEUE_SERIAL);
    }
    return self;
}

- (void)storeAd:(id)ad forHandle:(NSString*)handleId {
    dispatch_sync(self.syncQueue, ^{
        self.adInstances[handleId] = ad;
    });
}

- (id)adForHandle:(NSString*)handleId {
    __block id ad = nil;
    dispatch_sync(self.syncQueue, ^{
        ad = self.adInstances[handleId];
    });
    return ad;
}

- (void)removeAdForHandle:(NSString*)handleId {
    dispatch_sync(self.syncQueue, ^{
        [self.adInstances removeObjectForKey:handleId];
    });
}

- (NSString*)generateHandle {
    return [[NSUUID UUID] UUIDString];
}

@end

#pragma mark - Ad Delegates

// Reward Video Ad Delegate
@interface DirichletMediationUnityRewardVideoAdDelegate : NSObject <DRMRewardVideoAdDelegate>
@property (nonatomic, strong) NSString* handleId;
@end

@implementation DirichletMediationUnityRewardVideoAdDelegate

- (void)rewardVideoAdDidShow:(DRMRewardVideoAd *)rewardVideoAd {
    SendEventToUnity(self.handleId, @"show", @"reward_video", nil);
}

- (void)rewardVideoAdDidFailToShow:(DRMRewardVideoAd *)rewardVideoAd withError:(NSError *)error {
    NSDictionary* data = @{
        @"code": @(error.code),
        @"message": error.localizedDescription ?: @"Unknown error"
    };
    SendEventToUnity(self.handleId, @"show_error", @"reward_video", data);
}

- (void)rewardVideoAdDidClick:(DRMRewardVideoAd *)rewardVideoAd {
    SendEventToUnity(self.handleId, @"click", @"reward_video", nil);
}

- (void)rewardVideoAdDidClose:(DRMRewardVideoAd *)rewardVideoAd {
    SendEventToUnity(self.handleId, @"close", @"reward_video", nil);
}

- (void)rewardVideoAdDidRewardUser:(DRMRewardVideoAd *)rewardVideoAd {
    NSDictionary* data = @{
        @"rewardVerify": @(YES),
        @"rewardAmount": @(0),
        @"rewardName": @"",
        @"code": @(0),
        @"message": @""
    };
    SendEventToUnity(self.handleId, @"reward", @"reward_video", data);
}

@end

// Interstitial Ad Delegate
@interface DirichletMediationUnityInterstitialAdDelegate : NSObject <DRMInterstitialAdDelegate>
@property (nonatomic, strong) NSString* handleId;
@end

@implementation DirichletMediationUnityInterstitialAdDelegate

- (void)interstitialAdDidShow:(DRMInterstitialAd *)interstitialAd {
    SendEventToUnity(self.handleId, @"show", @"interstitial", nil);
}

- (void)interstitialAdDidFailToShow:(DRMInterstitialAd *)interstitialAd withError:(NSError *)error {
    NSDictionary* data = @{
        @"code": @(error.code),
        @"message": error.localizedDescription ?: @"Unknown error"
    };
    SendEventToUnity(self.handleId, @"show_error", @"interstitial", data);
}

- (void)interstitialAdDidClick:(DRMInterstitialAd *)interstitialAd {
    SendEventToUnity(self.handleId, @"click", @"interstitial", nil);
}

- (void)interstitialAdDidClose:(DRMInterstitialAd *)interstitialAd {
    SendEventToUnity(self.handleId, @"close", @"interstitial", nil);
}

@end

// Banner Ad Delegate
@interface DirichletMediationUnityBannerAdDelegate : NSObject <DRMBannerAdDelegate>
@property (nonatomic, strong) NSString* handleId;
@end

@implementation DirichletMediationUnityBannerAdDelegate

- (void)bannerAdDidShow:(DRMBannerAd *)bannerAd {
    SendEventToUnity(self.handleId, @"show", @"banner", nil);
}

- (void)bannerAdDidFailToShow:(DRMBannerAd *)bannerAd withError:(NSError *)error {
    NSDictionary* data = @{
        @"code": @(error.code),
        @"message": error.localizedDescription ?: @"Unknown error"
    };
    SendEventToUnity(self.handleId, @"show_error", @"banner", data);
}

- (void)bannerAdDidClick:(DRMBannerAd *)bannerAd {
    SendEventToUnity(self.handleId, @"click", @"banner", nil);
}

- (void)bannerAdDidClose:(DRMBannerAd *)bannerAd {
    SendEventToUnity(self.handleId, @"close", @"banner", nil);
}

@end

// Splash Ad Delegate
@interface DirichletMediationUnitySplashAdDelegate : NSObject <DRMSplashAdDelegate>
@property (nonatomic, strong) NSString* handleId;
@end

@implementation DirichletMediationUnitySplashAdDelegate

- (void)splashAdDidShow:(DRMSplashAd *)splashAd {
    SendEventToUnity(self.handleId, @"show", @"splash", nil);
}

- (void)splashAdDidFailToShow:(DRMSplashAd *)splashAd withError:(NSError *)error {
    NSDictionary* data = @{
        @"code": @(error.code),
        @"message": error.localizedDescription ?: @"Unknown error"
    };
    SendEventToUnity(self.handleId, @"show_error", @"splash", data);
}

- (void)splashAdDidClick:(DRMSplashAd *)splashAd {
    SendEventToUnity(self.handleId, @"click", @"splash", nil);
}

- (void)splashAdDidClose:(DRMSplashAd *)splashAd {
    SendEventToUnity(self.handleId, @"close", @"splash", nil);
}

@end

#pragma mark - Bridge Implementation

extern "C" {

bool DirichletMediationUnityBridge_Initialize(
    const char* mediaId,
    const char* mediaKey,
    bool enableLog,
    const char* mediaName,
    const char* gameChannel,
    bool shakeEnabled,
    bool allowIDFAAccess,
    const char* aTags
) {
    NSString* nsMediaId = CreateNSString(mediaId);
    NSString* nsMediaKey = CreateNSString(mediaKey);
    
    NSLog(@"[DirichletMediationUnityBridge] Initialize called with mediaId=%@, mediaKey=%@, enableLog=%d", 
          nsMediaId, nsMediaKey, enableLog);
    
    if (nsMediaId.length == 0 || nsMediaKey.length == 0) {
        NSLog(@"[DirichletMediationUnityBridge] Initialize failed: mediaId and mediaKey are required");
        return false;
    }
    
    // Check if SDK is already initialized
    if ([DirichletMediation isInitialized]) {
        NSLog(@"[DirichletMediationUnityBridge] SDK already initialized");
        return true;
    }
    
    DRMSDKConfig* config = [DRMSDKConfig configWithMediaId:nsMediaId mediaKey:nsMediaKey];
    if (!config) {
        NSLog(@"[DirichletMediationUnityBridge] Failed to create SDK config");
        return false;
    }
    
    config.isDebug = enableLog;
    config.shakeEnabled = shakeEnabled;
    config.allowIDFAAccess = allowIDFAAccess;
    
    if (mediaName && strlen(mediaName) > 0) {
        config.mediaName = CreateNSString(mediaName);
    }
    
    if (gameChannel && strlen(gameChannel) > 0) {
        config.gameChannel = CreateNSString(gameChannel);
    }
    
    if (aTags && strlen(aTags) > 0) {
        config.aTags = CreateNSString(aTags);
    }
    
    NSLog(@"[DirichletMediationUnityBridge] Starting SDK initialization (async callback)...");
    NSLog(@"[DirichletMediationUnityBridge] Config details - mediaId:%@, mediaKey:%@, mediaName:%@, gameChannel:%@",
          config.mediaId, config.mediaKey, config.mediaName, config.gameChannel);
    
    // Use async callback pattern (aligned with Ad Unity implementation)
    void (^startBlock)(void) = ^{
        [DirichletMediation startWithConfig:config completion:^(BOOL success, NSError * _Nullable error) {
            if (error) {
                NSLog(@"[DirichletMediationUnityBridge] SDK init callback - success:%d, error:%@ (code:%ld, domain:%@)",
                      success, error.localizedDescription, (long)error.code, error.domain);
            } else {
                NSLog(@"[DirichletMediationUnityBridge] SDK init callback - success:%d", success);
            }
            SendInitCallbackToUnity(success, error, success ? @"ios_mediation_bridge" : nil);
        }];
    };
    
    if ([NSThread isMainThread]) {
        startBlock();
    } else {
        dispatch_async(dispatch_get_main_queue(), startBlock);
    }
    
    return true;
}

void DirichletMediationUnityBridge_RequestPermissionIfNeeded(void) {
    // iOS 14+ ATT permission is handled internally by the SDK
    NSLog(@"[DirichletMediationUnityBridge] RequestPermissionIfNeeded called");
}

const char* DirichletMediationUnityBridge_GetSdkVersion(void) {
    static char* versionCString = NULL;
    if (versionCString == NULL) {
        versionCString = MakeCString([DirichletMediation sdkVersion]);
    }
    return versionCString;
}

const char* DirichletMediationUnityBridge_LoadRewardVideoAd(long long spaceId, const char* extras) {
    NSString* handleId = [[DirichletMediationInstanceManager shared] generateHandle];
    NSDictionary* extrasDict = ParseExtras(extras);
    
    // Create load request
    DRMAdLoadRequest* request = [[DRMAdLoadRequest alloc] initWithSpaceId:[NSString stringWithFormat:@"%lld", spaceId]];
    
    // Apply extras if provided (matching Unity C# ToBridgePayload keys)
    if (extrasDict[@"user_id"]) {
        request.rewardUserId = extrasDict[@"user_id"];
    }
    if (extrasDict[@"extra1"]) {
        request.rewardExtra = extrasDict[@"extra1"];
    }
    if (extrasDict[@"reward_name"]) {
        request.rewardName = extrasDict[@"reward_name"];
    }
    if (extrasDict[@"reward_amount"]) {
        request.rewardAmount = [extrasDict[@"reward_amount"] integerValue];
    }
    if (extrasDict[@"mina_id"]) {
        request.minaId = [NSString stringWithFormat:@"%@", extrasDict[@"mina_id"]];
    }
    
    [DRMRewardVideoAd loadWithRequest:request completion:^(NSArray<DRMRewardVideoAd *> * _Nullable ads, NSError * _Nullable error) {
        if (ads && ads.count > 0) {
            DRMRewardVideoAd* ad = ads.firstObject;
            DirichletMediationUnityRewardVideoAdDelegate* delegate = [[DirichletMediationUnityRewardVideoAdDelegate alloc] init];
            delegate.handleId = handleId;
            ad.delegate = delegate;
            
            [[DirichletMediationInstanceManager shared] storeAd:ad forHandle:handleId];
            [[DirichletMediationInstanceManager shared] storeAd:delegate forHandle:[handleId stringByAppendingString:@"_delegate"]];
            
            NSLog(@"[DirichletMediationUnityBridge] RewardVideoAd loaded: %@", handleId);
            SendLoadCallbackToUnity(handleId, @"load_success", @"reward_video", nil);
        } else {
            NSLog(@"[DirichletMediationUnityBridge] RewardVideoAd load failed: %@", error.localizedDescription);
            NSDictionary* errorData = @{
                @"code": @(error.code),
                @"message": error.localizedDescription ?: @"Unknown error"
            };
            SendLoadCallbackToUnity(handleId, @"load_error", @"reward_video", errorData);
        }
    }];
    
    return MakeCString(handleId);
}

const char* DirichletMediationUnityBridge_LoadInterstitialAd(long long spaceId, const char* extras) {
    NSString* handleId = [[DirichletMediationInstanceManager shared] generateHandle];
    NSDictionary* extrasDict = ParseExtras(extras);
    
    // Create load request
    DRMAdLoadRequest* request = [[DRMAdLoadRequest alloc] initWithSpaceId:[NSString stringWithFormat:@"%lld", spaceId]];
    
    // Apply extras if provided (matching Unity C# ToBridgePayload keys)
    if (extrasDict[@"mina_id"]) {
        request.minaId = [NSString stringWithFormat:@"%@", extrasDict[@"mina_id"]];
    }
    
    [DRMInterstitialAd loadWithRequest:request completion:^(NSArray<DRMInterstitialAd *> * _Nullable ads, NSError * _Nullable error) {
        if (ads && ads.count > 0) {
            DRMInterstitialAd* ad = ads.firstObject;
            DirichletMediationUnityInterstitialAdDelegate* delegate = [[DirichletMediationUnityInterstitialAdDelegate alloc] init];
            delegate.handleId = handleId;
            ad.delegate = delegate;
            
            [[DirichletMediationInstanceManager shared] storeAd:ad forHandle:handleId];
            [[DirichletMediationInstanceManager shared] storeAd:delegate forHandle:[handleId stringByAppendingString:@"_delegate"]];
            
            NSLog(@"[DirichletMediationUnityBridge] InterstitialAd loaded: %@", handleId);
            SendLoadCallbackToUnity(handleId, @"load_success", @"interstitial", nil);
        } else {
            NSLog(@"[DirichletMediationUnityBridge] InterstitialAd load failed: %@", error.localizedDescription);
            NSDictionary* errorData = @{
                @"code": @(error.code),
                @"message": error.localizedDescription ?: @"Unknown error"
            };
            SendLoadCallbackToUnity(handleId, @"load_error", @"interstitial", errorData);
        }
    }];
    
    return MakeCString(handleId);
}

const char* DirichletMediationUnityBridge_LoadBannerAd(long long spaceId, const char* extras) {
    NSString* handleId = [[DirichletMediationInstanceManager shared] generateHandle];
    NSDictionary* extrasDict = ParseExtras(extras);
    
    // Create load request
    DRMAdLoadRequest* request = [[DRMAdLoadRequest alloc] initWithSpaceId:[NSString stringWithFormat:@"%lld", spaceId]];
    
    // Apply extras if provided (matching Unity C# ToBridgePayload keys)
    if (extrasDict[@"mina_id"]) {
        request.minaId = [NSString stringWithFormat:@"%@", extrasDict[@"mina_id"]];
    }
    // Set ad size for Banner (CSJ/GDT adapters need this)
    NSNumber* width = extrasDict[@"express_width"];
    NSNumber* height = extrasDict[@"express_height"];
    if (width || height) {
        CGFloat w = width ? [width floatValue] : 0;
        CGFloat h = height ? [height floatValue] : 0;
        request.adSize = CGSizeMake(w, h);
    }
    
    [DRMBannerAd loadWithRequest:request completion:^(NSArray<DRMBannerAd *> * _Nullable ads, NSError * _Nullable error) {
        if (ads && ads.count > 0) {
            DRMBannerAd* ad = ads.firstObject;
            DirichletMediationUnityBannerAdDelegate* delegate = [[DirichletMediationUnityBannerAdDelegate alloc] init];
            delegate.handleId = handleId;
            ad.delegate = delegate;
            
            [[DirichletMediationInstanceManager shared] storeAd:ad forHandle:handleId];
            [[DirichletMediationInstanceManager shared] storeAd:delegate forHandle:[handleId stringByAppendingString:@"_delegate"]];
            
            NSLog(@"[DirichletMediationUnityBridge] BannerAd loaded: %@", handleId);
            SendLoadCallbackToUnity(handleId, @"load_success", @"banner", nil);
        } else {
            NSLog(@"[DirichletMediationUnityBridge] BannerAd load failed: %@", error.localizedDescription);
            NSDictionary* errorData = @{
                @"code": @(error.code),
                @"message": error.localizedDescription ?: @"Unknown error"
            };
            SendLoadCallbackToUnity(handleId, @"load_error", @"banner", errorData);
        }
    }];
    
    return MakeCString(handleId);
}

const char* DirichletMediationUnityBridge_LoadSplashAd(long long spaceId, const char* extras) {
    NSString* handleId = [[DirichletMediationInstanceManager shared] generateHandle];
    NSDictionary* extrasDict = ParseExtras(extras);
    
    // Create load request
    DRMAdLoadRequest* request = [[DRMAdLoadRequest alloc] initWithSpaceId:[NSString stringWithFormat:@"%lld", spaceId]];
    
    // Apply extras if provided (matching Unity C# ToBridgePayload keys)
    if (extrasDict[@"mina_id"]) {
        request.minaId = [NSString stringWithFormat:@"%@", extrasDict[@"mina_id"]];
    }
    // Set ad size for Splash (CSJ adapter needs this)
    NSNumber* width = extrasDict[@"express_width"];
    NSNumber* height = extrasDict[@"express_height"];
    if (width || height) {
        CGFloat w = width ? [width floatValue] : 0;
        CGFloat h = height ? [height floatValue] : 0;
        request.adSize = CGSizeMake(w, h);
    }
    
    [DRMSplashAd loadWithRequest:request completion:^(NSArray<DRMSplashAd *> * _Nullable ads, NSError * _Nullable error) {
        if (ads && ads.count > 0) {
            DRMSplashAd* ad = ads.firstObject;
            DirichletMediationUnitySplashAdDelegate* delegate = [[DirichletMediationUnitySplashAdDelegate alloc] init];
            delegate.handleId = handleId;
            ad.delegate = delegate;
            
            [[DirichletMediationInstanceManager shared] storeAd:ad forHandle:handleId];
            [[DirichletMediationInstanceManager shared] storeAd:delegate forHandle:[handleId stringByAppendingString:@"_delegate"]];
            
            NSLog(@"[DirichletMediationUnityBridge] SplashAd loaded: %@", handleId);
            SendLoadCallbackToUnity(handleId, @"load_success", @"splash", nil);
        } else {
            NSLog(@"[DirichletMediationUnityBridge] SplashAd load failed: %@", error.localizedDescription);
            NSDictionary* errorData = @{
                @"code": @(error.code),
                @"message": error.localizedDescription ?: @"Unknown error"
            };
            SendLoadCallbackToUnity(handleId, @"load_error", @"splash", errorData);
        }
    }];
    
    return MakeCString(handleId);
}

bool DirichletMediationUnityBridge_ShowAd(const char* handleId, const char* extras) {
    NSString* nsHandleId = CreateNSString(handleId);
    id ad = [[DirichletMediationInstanceManager shared] adForHandle:nsHandleId];
    
    if (!ad) {
        NSLog(@"[DirichletMediationUnityBridge] ShowAd failed: Ad not found for handle %@", nsHandleId);
        return false;
    }
    
    // Ensure show is always called on main thread (aligned with Ad Unity implementation)
    dispatch_async(dispatch_get_main_queue(), ^{
        UIViewController* rootVC = [[[UIApplication sharedApplication] keyWindow] rootViewController];
        if (!rootVC) {
            NSLog(@"[DirichletMediationUnityBridge] ShowAd failed: Root view controller not found");
            return;
        }
        
        if ([ad isKindOfClass:[DRMRewardVideoAd class]]) {
            DRMRewardVideoAd* rewardAd = (DRMRewardVideoAd*)ad;
            if ([rewardAd isReady]) {
                [rewardAd showFromViewController:rootVC];
                NSLog(@"[DirichletMediationUnityBridge] Showing reward video ad: %@", nsHandleId);
            }
        } else if ([ad isKindOfClass:[DRMInterstitialAd class]]) {
            DRMInterstitialAd* interstitialAd = (DRMInterstitialAd*)ad;
            if ([interstitialAd isReady]) {
                [interstitialAd showFromViewController:rootVC];
                NSLog(@"[DirichletMediationUnityBridge] Showing interstitial ad: %@", nsHandleId);
            }
        } else if ([ad isKindOfClass:[DRMBannerAd class]]) {
            DRMBannerAd* bannerAd = (DRMBannerAd*)ad;
            UIView* bannerView = bannerAd.view;
            if (bannerView) {
                // Banner 广告需要将 view 添加到视图控制器上
                // 注意：Unity 侧需要通过 Unity UI 系统来处理 Banner 视图
                // 这里我们发送一个事件通知 Unity 侧，让 Unity 侧来处理视图的展示
                // 或者直接将视图添加到根视图控制器上（临时方案）
                [rootVC.view addSubview:bannerView];
                bannerView.translatesAutoresizingMaskIntoConstraints = NO;
                // 设置约束，让 Banner 显示在底部
                [NSLayoutConstraint activateConstraints:@[
                    [bannerView.leadingAnchor constraintEqualToAnchor:rootVC.view.leadingAnchor],
                    [bannerView.trailingAnchor constraintEqualToAnchor:rootVC.view.trailingAnchor],
                    [bannerView.bottomAnchor constraintEqualToAnchor:rootVC.view.safeAreaLayoutGuide.bottomAnchor],
                    [bannerView.heightAnchor constraintEqualToConstant:bannerAd.size.height > 0 ? bannerAd.size.height : 50]
                ]];
                NSLog(@"[DirichletMediationUnityBridge] Showing banner ad: %@", nsHandleId);
            } else {
                NSLog(@"[DirichletMediationUnityBridge] Banner ad view not available: %@", nsHandleId);
            }
        } else if ([ad isKindOfClass:[DRMSplashAd class]]) {
            DRMSplashAd* splashAd = (DRMSplashAd*)ad;
            if ([splashAd isReady]) {
                [splashAd showFromViewController:rootVC];
                NSLog(@"[DirichletMediationUnityBridge] Showing splash ad: %@", nsHandleId);
            }
        } else {
            NSLog(@"[DirichletMediationUnityBridge] ShowAd failed: Ad not ready or unknown type");
        }
    });
    
    return true;
}

void DirichletMediationUnityBridge_DestroyAd(const char* handleId) {
    NSString* nsHandleId = CreateNSString(handleId);
    NSLog(@"[DirichletMediationUnityBridge] Destroying ad: %@", nsHandleId);
    
    // Remove ad instance
    [[DirichletMediationInstanceManager shared] removeAdForHandle:nsHandleId];
    
    // Remove delegate
    NSString* delegateKey = [nsHandleId stringByAppendingString:@"_delegate"];
    [[DirichletMediationInstanceManager shared] removeAdForHandle:delegateKey];
}

bool DirichletMediationUnityBridge_IsAdValid(const char* handleId) {
    NSString* nsHandleId = CreateNSString(handleId);
    id ad = [[DirichletMediationInstanceManager shared] adForHandle:nsHandleId];
    
    if (!ad) {
        NSLog(@"[DirichletMediationUnityBridge] IsAdValid: Ad not found for handle %@", nsHandleId);
        return false;
    }
    
    if ([ad isKindOfClass:[DRMRewardVideoAd class]]) {
        DRMRewardVideoAd* rewardAd = (DRMRewardVideoAd*)ad;
        return [rewardAd isReady];
    } else if ([ad isKindOfClass:[DRMInterstitialAd class]]) {
        DRMInterstitialAd* interstitialAd = (DRMInterstitialAd*)ad;
        return [interstitialAd isReady];
    } else if ([ad isKindOfClass:[DRMBannerAd class]]) {
        // Banner ads don't have isReady, assume valid if loaded
        return true;
    } else if ([ad isKindOfClass:[DRMSplashAd class]]) {
        DRMSplashAd* splashAd = (DRMSplashAd*)ad;
        return [splashAd isReady];
    }
    
    return false;
}

} // extern "C"

