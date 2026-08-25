//
//  DirichletMediationUnityBridge.h
//  Dirichlet Mediation Unity Bridge for iOS
//
//  Created by Dirichlet Unity SDK
//  Copyright © 2025 Dirichlet Inc. All rights reserved.
//

#import <Foundation/Foundation.h>

#ifdef __cplusplus
extern "C" {
#endif

/// Initialize the Dirichlet Mediation SDK
/// @param mediaId Media ID string
/// @param mediaKey Media key string
/// @param enableLog Enable debug logging
/// @param mediaName Media name string (optional)
/// @param gameChannel Game channel string (optional)
/// @param shakeEnabled Enable shake interaction (for GDT adapter)
/// @param allowIDFAAccess Allow IDFA access (for TapADN and GDT adapters)
/// @param aTags External aTags JSON string (optional)
/// @return YES if initialization started successfully, NO otherwise
bool DirichletMediationUnityBridge_Initialize(
    const char* mediaId,
    const char* mediaKey,
    bool enableLog,
    const char* mediaName,
    const char* gameChannel,
    bool shakeEnabled,
    bool allowIDFAAccess,
    const char* aTags
);

/// Request permissions if necessary (ATT for iOS 14+)
void DirichletMediationUnityBridge_RequestPermissionIfNeeded(void);

/// Get SDK version
/// @return SDK version string (caller should NOT free this pointer)
const char* DirichletMediationUnityBridge_GetSdkVersion(void);

/// Load reward video ad
/// @param spaceId Space/slot ID
/// @param extras JSON string with additional parameters
/// @return Handle ID for the ad instance (caller should copy/free)
const char* DirichletMediationUnityBridge_LoadRewardVideoAd(long long spaceId, const char* extras);

/// Load interstitial ad
/// @param spaceId Space/slot ID
/// @param extras JSON string with additional parameters
/// @return Handle ID for the ad instance
const char* DirichletMediationUnityBridge_LoadInterstitialAd(long long spaceId, const char* extras);

/// Load banner ad
/// @param spaceId Space/slot ID
/// @param extras JSON string with additional parameters
/// @return Handle ID for the ad instance
const char* DirichletMediationUnityBridge_LoadBannerAd(long long spaceId, const char* extras);

/// Load splash ad
/// @param spaceId Space/slot ID
/// @param extras JSON string with additional parameters
/// @return Handle ID for the ad instance
const char* DirichletMediationUnityBridge_LoadSplashAd(long long spaceId, const char* extras);

/// Show ad by handle
/// @param handleId Handle ID returned from load methods
/// @param extras JSON string with show options (e.g., banner alignment)
/// @return YES if show started successfully, NO otherwise
bool DirichletMediationUnityBridge_ShowAd(const char* handleId, const char* extras);

/// Destroy ad by handle
/// @param handleId Handle ID returned from load methods
void DirichletMediationUnityBridge_DestroyAd(const char* handleId);

#ifdef __cplusplus
}
#endif

