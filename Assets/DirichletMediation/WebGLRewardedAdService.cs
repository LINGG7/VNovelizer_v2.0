using System;
using System.Collections;
using System.Runtime.InteropServices;
using UnityEngine;

public sealed class WebGLRewardedAdService : IRewardedAdService, IRewardedAdServiceLifecycle
{
    private const string RewardAdUnitId = "1056694";
    private const string WatcherObjectName = "WebGLRewardedAdServiceWatcher";
    private const float RewardedAdRequestTimeoutSeconds = 8f;

    private bool isInitialized;
    private bool hasPendingAdRequest;
    private Action pendingRewarded;
    private Action pendingFailed;
    private Coroutine requestTimeoutCoroutine;
    private int requestTimeoutToken;

#if UNITY_WEBGL && !UNITY_EDITOR
    [DllImport("__Internal")]
    private static extern int VNovelizerWebGLRewardedAdIsSupported();

    [DllImport("__Internal")]
    private static extern void VNovelizerWebGLRewardedAdShow(string adUnitId, string gameObjectName);
#endif

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterAssembliesLoaded)]
    private static void RegisterService()
    {
#if UNITY_WEBGL && !UNITY_EDITOR
        RewardedAdManager.GetInstance().SetService(new WebGLRewardedAdService());
#endif
    }

    public void Init()
    {
        if (isInitialized)
            return;

        isInitialized = true;
        EnsureAdStateWatcher();
    }

    public void ShowRewardedAd(Action onRewarded, Action onFailed)
    {
        Init();

        if (hasPendingAdRequest)
        {
            Debug.LogWarning("[WebGLRewardedAdService] Rewarded ad is already loading or showing.");
            onFailed?.Invoke();
            return;
        }

#if UNITY_WEBGL && !UNITY_EDITOR
        if (VNovelizerWebGLRewardedAdIsSupported() == 0)
        {
            Debug.LogWarning("[WebGLRewardedAdService] Rewarded ad is not supported by this WebGL host.");
            onFailed?.Invoke();
            return;
        }

        pendingRewarded = onRewarded;
        pendingFailed = onFailed;
        hasPendingAdRequest = true;
        StartRequestTimeout();

        VNovelizerWebGLRewardedAdShow(RewardAdUnitId, WatcherObjectName);
#else
        onFailed?.Invoke();
#endif
    }

    internal void OnAdLoaded(string payload)
    {
        Debug.Log("[WebGLRewardedAdService] Rewarded ad loaded: " + payload);
    }

    internal void OnAdShown()
    {
        StopRequestTimeout();
        Debug.Log("[WebGLRewardedAdService] Rewarded ad shown.");
    }

    internal void OnAdClosed(string payload)
    {
        Debug.Log("[WebGLRewardedAdService] Rewarded ad closed: " + payload);
    }

    internal void OnAdRewarded(string payload)
    {
        Action rewarded = pendingRewarded;
        ClearPending();
        Debug.Log("[WebGLRewardedAdService] Rewarded ad completed: " + payload);
        rewarded?.Invoke();
    }

    internal void OnAdFailed(string payload)
    {
        Debug.LogWarning("[WebGLRewardedAdService] Rewarded ad failed: " + payload);
        FailPending();
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

        if (token != requestTimeoutToken || !hasPendingAdRequest)
            yield break;

        requestTimeoutCoroutine = null;
        requestTimeoutToken++;
        Debug.LogWarning("[WebGLRewardedAdService] Rewarded ad request timed out.");
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

        WebGLRewardedAdCallbackReceiver receiver = host.GetComponent<WebGLRewardedAdCallbackReceiver>();
        if (receiver == null)
            receiver = host.AddComponent<WebGLRewardedAdCallbackReceiver>();

        receiver.Bind(this);
    }
}

internal sealed class WebGLRewardedAdCallbackReceiver : MonoBehaviour
{
    private WebGLRewardedAdService owner;

    public void Bind(WebGLRewardedAdService service)
    {
        owner = service;
    }

    public void OnWebGLRewardedAdLoaded(string payload)
    {
        owner?.OnAdLoaded(payload);
    }

    public void OnWebGLRewardedAdShown(string payload)
    {
        owner?.OnAdShown();
    }

    public void OnWebGLRewardedAdClosed(string payload)
    {
        owner?.OnAdClosed(payload);
    }

    public void OnWebGLRewardedAdRewarded(string payload)
    {
        owner?.OnAdRewarded(payload);
    }

    public void OnWebGLRewardedAdFailed(string payload)
    {
        owner?.OnAdFailed(payload);
    }
}
