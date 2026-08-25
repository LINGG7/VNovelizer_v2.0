using System;
using System.Collections;
using UnityEngine;

public interface IRewardedAdService
{
    void ShowRewardedAd(Action onRewarded, Action onFailed);
}

public interface IRewardedAdServiceLifecycle
{
    void Init();
}

public class RewardedAdManager : BaseManager<RewardedAdManager>
{
    private IRewardedAdService adService = new MockRewardedAdService();
    private bool isInitialized;

    public void Init()
    {
        if (isInitialized)
            return;

        isInitialized = true;

        if (adService is IRewardedAdServiceLifecycle lifecycle)
            lifecycle.Init();
    }

    public void SetService(IRewardedAdService service)
    {
        adService = service ?? new MockRewardedAdService();

        if (isInitialized && adService is IRewardedAdServiceLifecycle lifecycle)
            lifecycle.Init();
    }

    public void ShowRewardedAd(Action onRewarded, Action onFailed)
    {
        Init();
        adService.ShowRewardedAd(onRewarded, onFailed);
    }
}

public class MockRewardedAdService : IRewardedAdService
{
    public void ShowRewardedAd(Action onRewarded, Action onFailed)
    {
        MonoManager monoManager = MonoManager.GetInstance();
        if (monoManager != null)
        {
            monoManager.StartCoroutine(SimulateAd(onRewarded));
            return;
        }

        onRewarded?.Invoke();
    }

    private IEnumerator SimulateAd(Action onRewarded)
    {
        yield return new WaitForSecondsRealtime(0.5f);
        onRewarded?.Invoke();
    }
}
