using System;
using System.Collections;
using System.Globalization;
using Dirichlet.Mediation;
using UnityEngine;

public sealed class DirichletRewardedAdService : IRewardedAdService, IRewardedAdServiceLifecycle
{
    private const string MediaName = "\u5723\u8bfa\u6c40\u4e4b\u68a6";
    private const string Channel = "taptap2";
    private const string SubChannel = "release";
    private const string TapClientId = "0RiAlMny7jiz086FaU";
    private const string RewardName = "stamina";
    private const int RewardAmount = 1;
    private const float RewardedAdRequestTimeoutSeconds = 8f;
    private const string WatcherObjectName = "DirichletRewardedAdServiceWatcher";

    private const long MediaId = 1102728;
    private const string MediaKey = "eOmxJpMJnUVRHCWKAtK1fWSU3y1LRyRpgiItuy1ZcdARcuGbykykLBpn15bRSDch";
    private const long RewardSpaceId = 1056694;

    private bool isInitialized;
    private bool sdkInitFinished;
    private bool isSdkReady;
    private bool hasPendingAdRequest;
    private bool isShowing;
    private bool adOpened;
    private bool rewardGranted;
    private DirichletAdNative adNative;
    private Action pendingRewarded;
    private Action pendingFailed;
    private Coroutine requestTimeoutCoroutine;
    private int requestTimeoutToken;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterAssembliesLoaded)]
    private static void RegisterService()
    {
#if !UNITY_EDITOR && (UNITY_ANDROID || UNITY_IOS)
        RewardedAdManager.GetInstance().SetService(new DirichletRewardedAdService());
#endif
    }

    public void Init()
    {
        if (isInitialized)
            return;

        isInitialized = true;
        EnsureAdStateWatcher();

        DirichletSdk.Init(
            BuildSdkConfig(),
            result =>
            {
                sdkInitFinished = true;
                isSdkReady = result == null || result.Success;
                adNative = DirichletAdManager.CreateAdNative();
                DirichletSdk.RequestPermissionIfNecessary();
                Debug.Log("[DirichletRewardedAdService] SDK init success: " + (result?.Message ?? string.Empty));

                if (hasPendingAdRequest)
                    TryShowPendingRewardedAd();
            },
            error =>
            {
                sdkInitFinished = true;
                isSdkReady = false;
                Debug.LogError("[DirichletRewardedAdService] SDK init failed: " + error);

                if (hasPendingAdRequest)
                    FailPending();
            });
    }

    public void ShowRewardedAd(Action onRewarded, Action onFailed)
    {
        Init();

        if (hasPendingAdRequest || isShowing)
        {
            Debug.LogWarning("[DirichletRewardedAdService] Rewarded ad is already loading or showing.");
            onFailed?.Invoke();
            return;
        }

        pendingRewarded = onRewarded;
        pendingFailed = onFailed;
        hasPendingAdRequest = true;
        adOpened = false;
        rewardGranted = false;
        StartRequestTimeout();

        if (!sdkInitFinished)
        {
            Debug.Log("[DirichletRewardedAdService] SDK is initializing, reward ad request is pending.");
            return;
        }

        if (!isSdkReady)
        {
            Debug.LogWarning("[DirichletRewardedAdService] SDK is not ready.");
            FailPending();
            return;
        }

        TryShowPendingRewardedAd();
    }

    private static DirichletAdConfig BuildSdkConfig()
    {
        return new DirichletAdConfig.Builder()
            .WithMediaId(MediaId)
            .WithMediaName(MediaName)
            .WithMediaKey(MediaKey)
            .WithGameChannel(Channel)
            .WithSubChannel(SubChannel)
            .WithTapClientId(TapClientId)
            .EnableDebug(Debug.isDebugBuild)
            .ShakeEnabled(true)
            .Build();
    }

    private static DirichletAdRequest BuildRewardRequest()
    {
        string userId = string.IsNullOrEmpty(SystemInfo.deviceUniqueIdentifier)
            ? "unity_user"
            : SystemInfo.deviceUniqueIdentifier;

        return new DirichletAdRequest.Builder()
            .WithSpaceId(RewardSpaceId)
            .WithUserId(userId)
            .WithRewardName(RewardName)
            .WithRewardAmount(RewardAmount)
            .WithExtra1("vnovelizer")
            .WithQuery(Application.identifier ?? string.Empty)
            .Build();
    }

    private void TryShowPendingRewardedAd()
    {
        if (!hasPendingAdRequest || isShowing)
            return;

        if (adNative == null)
            adNative = DirichletAdManager.CreateAdNative();

        isShowing = true;
        adNative.ShowRewardVideoAutoAd(BuildRewardRequest(), new RewardAutoListener(this));
    }

    private void OnAdShow()
    {
        MarkAdOpened("show callback");
    }

    private void OnRewardVerify(DirichletRewardVerificationEventArgs args)
    {
        if (args == null || !args.IsVerified || rewardGranted)
            return;

        MarkAdOpened("reward verify");
        rewardGranted = true;
        Action rewarded = pendingRewarded;
        ClearPending();
        rewarded?.Invoke();
    }

    private void OnAdClose()
    {
        isShowing = false;

        if (hasPendingAdRequest && !rewardGranted)
            FailPending();
    }

    private void OnAdError(DirichletError error)
    {
        isShowing = false;
        Debug.LogError("[DirichletRewardedAdService] Rewarded ad failed: " + error);
        FailPending();
    }

    private void OnApplicationLeftForeground()
    {
        if (!hasPendingAdRequest || adOpened)
            return;

        MarkAdOpened("application pause/focus");
    }

    private void MarkAdOpened(string reason)
    {
        if (adOpened)
            return;

        adOpened = true;
        StopRequestTimeout();
        Debug.Log("[DirichletRewardedAdService] Rewarded ad opened by " + reason + ", stop request timeout.");
    }

    private void StartRequestTimeout()
    {
        StopRequestTimeout();

        int token = ++requestTimeoutToken;
        MonoManager monoManager = MonoManager.GetInstance();
        if (monoManager != null)
            requestTimeoutCoroutine = monoManager.StartCoroutine(RequestTimeoutRoutine(token));
    }

    private void StopRequestTimeout()
    {
        requestTimeoutToken++;

        if (requestTimeoutCoroutine == null)
            return;

        MonoManager monoManager = MonoManager.GetInstance();
        if (monoManager != null)
            monoManager.StopCoroutine(requestTimeoutCoroutine);

        requestTimeoutCoroutine = null;
    }

    private IEnumerator RequestTimeoutRoutine(int token)
    {
        yield return new WaitForSecondsRealtime(RewardedAdRequestTimeoutSeconds);

        if (token != requestTimeoutToken || !hasPendingAdRequest || adOpened)
            yield break;

        requestTimeoutCoroutine = null;
        requestTimeoutToken++;
        isShowing = false;
        Debug.LogWarning("[DirichletRewardedAdService] Rewarded ad request timed out.");
        FailPending();
    }

    private void FailPending()
    {
        Action failed = pendingFailed;
        ClearPending();
        failed?.Invoke();
    }

    private void ClearPending()
    {
        StopRequestTimeout();
        hasPendingAdRequest = false;
        adOpened = false;
        rewardGranted = false;
        pendingRewarded = null;
        pendingFailed = null;
    }

    private void EnsureAdStateWatcher()
    {
        GameObject host = GameObject.Find(WatcherObjectName);
        if (host == null)
        {
            host = new GameObject(WatcherObjectName)
            {
                hideFlags = HideFlags.HideAndDontSave
            };
            UnityEngine.Object.DontDestroyOnLoad(host);
        }

        AdStateWatcher watcher = host.GetComponent<AdStateWatcher>();
        if (watcher == null)
            watcher = host.AddComponent<AdStateWatcher>();

        watcher.Bind(this);
    }

    private sealed class RewardAutoListener : IDirichletRewardVideoAutoAdListener
    {
        private readonly DirichletRewardedAdService owner;

        public RewardAutoListener(DirichletRewardedAdService owner)
        {
            this.owner = owner;
        }

        public void OnError(DirichletError error)
        {
            owner.OnAdError(error);
        }

        public void OnAdShow()
        {
            owner.OnAdShow();
        }

        public void OnAdClose()
        {
            owner.OnAdClose();
        }

        public void OnRewardVerify(DirichletRewardVerificationEventArgs args)
        {
            Debug.Log(
                "[DirichletRewardedAdService] Reward verify: " +
                (args?.IsVerified ?? false).ToString(CultureInfo.InvariantCulture));
            owner.OnRewardVerify(args);
        }

        public void OnAdClick() { }
    }

    private sealed class AdStateWatcher : MonoBehaviour
    {
        private DirichletRewardedAdService owner;

        public void Bind(DirichletRewardedAdService service)
        {
            owner = service;
        }

        private void OnApplicationPause(bool pauseStatus)
        {
            if (pauseStatus)
                owner?.OnApplicationLeftForeground();
        }

        private void OnApplicationFocus(bool hasFocus)
        {
            if (!hasFocus)
                owner?.OnApplicationLeftForeground();
        }
    }
}
