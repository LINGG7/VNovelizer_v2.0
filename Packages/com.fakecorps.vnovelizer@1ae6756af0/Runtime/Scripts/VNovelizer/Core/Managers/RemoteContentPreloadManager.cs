using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.AddressableAssets.ResourceLocators;
using UnityEngine.ResourceManagement.AsyncOperations;
using UnityEngine.ResourceManagement.ResourceLocations;
using UnityEngine.Networking;

public enum RemoteContentLocationState
{
    Found,
    NotFound,
    Failed
}

public enum RemoteContentErrorKind
{
    None,
    Offline,
    Timeout,
    ServerUnavailable,
    ResourceMissing,
    IntegrityFailure,
    StorageFailure,
    Unknown
}

public enum RemoteContentOperationStage
{
    None,
    Initialize,
    CatalogCheck,
    CatalogUpdate,
    LocationLookup,
    SizeQuery,
    Download
}

public struct RemoteContentOperationResult
{
    public RemoteContentOperationStage Stage;
    public RemoteContentErrorKind ErrorKind;
    public string Label;
    public string Error;
    public bool CanRetry;
    public int Attempt;

    public bool Succeeded => ErrorKind == RemoteContentErrorKind.None;
}

public class RemoteContentPreloadManager : BaseManager<RemoteContentPreloadManager>
{
    private const string LoadingTaskID = "remote_content_preload";
    private const string LoadingTaskName = "下载必要资源";
    private const string RequiredChapterLabel = "chapter1_required";
    private const string OptionalChapterLabel = "chapter2_optional";
    private const string OptionalCellularPreferenceKey = "remote_content_allow_optional_cellular";
    private const float LoadingTaskWeight = 1f;
    private const float CatalogOperationTimeoutSeconds = 20f;
    private const float RemoteOperationTimeoutSeconds = 180f;
    private const float OptionalStartDelaySeconds = 3f;
    private const float OptionalRetryDelaySeconds = 30f;
    private const float NetworkRecoveryPollSeconds = 5f;
    private const float RetryBaseDelaySeconds = 2f;
    private const int BundleRequestTimeoutSeconds = 30;
    private const int MaxRequiredDownloadAttempts = 3;
    private const int MaxOptionalDownloadAttempts = 2;

    private readonly HashSet<string> completedLabels = new HashSet<string>();
    private readonly HashSet<string> failedLabels = new HashSet<string>();
    private bool hasStarted;
    private bool isReady;
    private bool isPreloading;
    private bool isOptionalReady;
    private bool isOptionalPreloading;
    private bool hasFailed;
    private float progress;
    private long totalDownloadBytes;
    private long downloadedBytes;
    private string lastError;
    private bool lastPreloadSucceeded = true;
    private bool lastSizeQueryFailed;
    private bool currentPreloadIsRequired;
    private int pendingTimedOutOperations;
    private int optionalFailureCount;
    private string primaryCdnRoot;
    private string backupCdnRoot;
    private bool useBackupCdn;
    private RemoteContentOperationResult lastOperationResult;
    private Coroutine optionalStartDelayCoroutine;
    private Coroutine optionalRetryCoroutine;

    public bool HasStarted => hasStarted;
    public bool IsReady => isReady;
    public bool IsPreloading => isPreloading;
    public bool IsOptionalReady => isOptionalReady;
    public bool IsOptionalPreloading => isOptionalPreloading;
    public bool HasFailed => hasFailed;
    public float Progress => progress;
    public long TotalDownloadBytes => totalDownloadBytes;
    public long DownloadedBytes => downloadedBytes;
    public string LastError => lastError;
    public RemoteContentOperationResult LastOperationResult => lastOperationResult;
    public RemoteContentErrorKind LastErrorKind => lastOperationResult.ErrorKind;
    public int LastAttemptCount => lastOperationResult.Attempt;
    public NetworkReachability CurrentNetwork => Application.internetReachability;
    public bool AllowOptionalDownloadOnCellular =>
        PlayerPrefs.GetInt(OptionalCellularPreferenceKey, 0) == 1;
    public long CacheSpaceOccupied =>
        Caching.ready ? Caching.currentCacheForWriting.spaceOccupied : -1;

    public RemoteContentPreloadManager()
    {
        ConfigureAddressablesWebRequests();
    }

    public void StartMainMenuPreload()
    {
        if (isPreloading)
        {
            return;
        }

        hasStarted = true;

        if (isReady)
        {
            return;
        }

        MonoManager.GetInstance().StartCoroutine(PreloadRequiredContent());
    }

    public IEnumerator WaitUntilReady()
    {
        if (!isReady && !isPreloading)
        {
            StartMainMenuPreload();
        }

        while (!isReady && !hasFailed)
        {
            yield return null;
        }
    }

    public void RetryRequiredContent()
    {
        if (isPreloading)
        {
            return;
        }

        failedLabels.Clear();
        hasFailed = false;
        lastError = null;
        isReady = false;
        hasStarted = true;
        MonoManager.GetInstance().StartCoroutine(PreloadRequiredContent());
    }

    private IEnumerator PreloadRequiredContent()
    {
        isPreloading = true;
        currentPreloadIsRequired = true;
        isReady = false;
        hasFailed = false;
        lastError = null;
        lastOperationResult = default;
        progress = 0f;
        downloadedBytes = 0;
        yield return WaitForPendingTimedOutOperations(true);
        MonoManager.GetInstance().StartCoroutine(PreloadCommonUIPanels());
        bool initialized = false;
        lastPreloadSucceeded = false;
        yield return InitializeAndUpdateCatalogs(true, succeeded => initialized = succeeded);
        if (initialized)
        {
            yield return PreloadLabels(
                BuildRequiredPreloadLabels(),
                true,
                "准备下载第一章资源",
                "第一章资源已缓存",
                "第一章资源下载完成");
        }

        if (!lastPreloadSucceeded)
        {
            isPreloading = false;
            isReady = false;
            hasFailed = true;
            if (string.IsNullOrEmpty(lastError))
            {
                lastError = BuildUserFacingError(lastOperationResult);
            }
            RegisterOrUpdateLoadingTask(progress, lastError);
            Debug.LogWarning($"[RemoteContentPreload] Required content failed. Failed labels: {string.Join(", ", failedLabels)}.");
            yield break;
        }

        isPreloading = false;
        isReady = true;
        Debug.Log($"[RemoteContentPreload] Required content ready. Downloaded {FormatBytes(downloadedBytes)} / {FormatBytes(totalDownloadBytes)}.");
        yield return PreloadCommonUIPanels();
    }

    public void StartOptionalContentPreloadAfterGameplayStarted()
    {
        if (!isReady || hasFailed || isOptionalReady || isOptionalPreloading || optionalStartDelayCoroutine != null)
        {
            return;
        }

        optionalStartDelayCoroutine = MonoManager.GetInstance().StartCoroutine(StartOptionalContentPreloadAfterDelay());
    }

    private IEnumerator StartOptionalContentPreloadAfterDelay()
    {
        yield return new WaitForSecondsRealtime(OptionalStartDelaySeconds);
        optionalStartDelayCoroutine = null;

        if (!isReady || hasFailed || isOptionalReady || isOptionalPreloading)
        {
            yield break;
        }

        StartOptionalContentPreload();
    }

    private void StartOptionalContentPreload()
    {
        if (isOptionalReady || isOptionalPreloading)
        {
            return;
        }

        if (!IsOptionalNetworkAllowed())
        {
            ScheduleOptionalContentRetry();
            return;
        }

        MonoManager.GetInstance().StartCoroutine(PreloadOptionalContent());
    }

    private IEnumerator PreloadOptionalContent()
    {
        isOptionalPreloading = true;
        currentPreloadIsRequired = false;
        yield return WaitForPendingTimedOutOperations(false);
        yield return PreloadLabels(
            BuildOptionalPreloadLabels(),
            false,
            null,
            null,
            null);

        isOptionalPreloading = false;
        isOptionalReady = lastPreloadSucceeded;
        if (lastPreloadSucceeded)
        {
            optionalFailureCount = 0;
            Debug.Log($"[RemoteContentPreload] Optional content ready. Downloaded {FormatBytes(downloadedBytes)} / {FormatBytes(totalDownloadBytes)}.");
        }
        else
        {
            optionalFailureCount++;
            Debug.LogWarning($"[RemoteContentPreload] Optional content failed. Will retry in {OptionalRetryDelaySeconds:F0} seconds.");
            ScheduleOptionalContentRetry();
        }
    }

    private void ScheduleOptionalContentRetry()
    {
        if (optionalRetryCoroutine != null || isOptionalReady || isOptionalPreloading)
        {
            return;
        }

        optionalRetryCoroutine = MonoManager.GetInstance().StartCoroutine(RetryOptionalContentAfterDelay());
    }

    private IEnumerator RetryOptionalContentAfterDelay()
    {
        while (!IsOptionalNetworkAllowed())
        {
            yield return new WaitForSecondsRealtime(NetworkRecoveryPollSeconds);
        }

        float backoff = OptionalRetryDelaySeconds * Mathf.Pow(2f, Mathf.Min(optionalFailureCount - 1, 3));
        yield return new WaitForSecondsRealtime(AddRetryJitter(backoff));
        optionalRetryCoroutine = null;

        if (!isOptionalReady && !isOptionalPreloading)
        {
            StartOptionalContentPreload();
        }
    }

    public void SetAllowOptionalDownloadOnCellular(bool allow)
    {
        PlayerPrefs.SetInt(OptionalCellularPreferenceKey, allow ? 1 : 0);
        PlayerPrefs.Save();

        if (allow && !isOptionalReady && !isOptionalPreloading)
        {
            StartOptionalContentPreload();
        }
    }

    public bool ConfigureCdnFailover(string primaryRoot, string backupRoot)
    {
        primaryRoot = NormalizeCdnRoot(primaryRoot);
        backupRoot = NormalizeCdnRoot(backupRoot);
        if (string.IsNullOrEmpty(primaryRoot) || string.IsNullOrEmpty(backupRoot)
            || !string.Equals(ExtractContentVersion(primaryRoot), ExtractContentVersion(backupRoot), System.StringComparison.Ordinal))
        {
            Debug.LogError("[RemoteContentPreload] CDN failover roots must be valid and use the same content version.");
            return false;
        }

        primaryCdnRoot = primaryRoot;
        backupCdnRoot = backupRoot;
        useBackupCdn = false;
        Addressables.InternalIdTransformFunc = location => TransformCdnInternalId(location.InternalId);
        return true;
    }

    public void ClearRemoteContentCache(System.Action<bool> completedCallback = null)
    {
        if (isPreloading || isOptionalPreloading || pendingTimedOutOperations > 0)
        {
            completedCallback?.Invoke(false);
            return;
        }

        MonoManager.GetInstance().StartCoroutine(ClearRemoteContentCacheCoroutine(completedCallback));
    }

    private IEnumerator ClearRemoteContentCacheCoroutine(System.Action<bool> completedCallback)
    {
        bool succeeded = true;
        string[] labels = { RequiredChapterLabel, OptionalChapterLabel };
        foreach (string label in labels)
        {
            AsyncOperationHandle<bool> clearHandle = Addressables.ClearDependencyCacheAsync(label, false);
            yield return WaitForAddressablesOperation(clearHandle, CatalogOperationTimeoutSeconds);

            if (!clearHandle.IsDone)
            {
                succeeded = false;
                DrainTimedOutOperation(clearHandle);
                continue;
            }

            succeeded &= clearHandle.Status == AsyncOperationStatus.Succeeded && clearHandle.Result;
            ReleaseIfValid(clearHandle);
        }

        if (succeeded)
        {
            completedLabels.Clear();
            failedLabels.Clear();
            isReady = false;
            isOptionalReady = false;
        }

        completedCallback?.Invoke(succeeded);
    }

    private IEnumerator PreloadCommonUIPanels()
    {
        VNProjectConfig config = VNProjectConfig.Instance;
        if (config == null)
        {
            yield break;
        }

        UIManager uiManager = UIManager.GetInstance();
        yield return new WaitForSecondsRealtime(1f);
        uiManager.PreloadPanelPrefab("SettingsPanel", config.UI_SettingsPath);
        yield return new WaitForSecondsRealtime(0.5f);

        uiManager.PreloadPanelPrefab("GalleryPanel", config.UI_GalleryPath);
    }

    private List<string> BuildRequiredPreloadLabels()
    {
        List<string> labels = new List<string>();
        AddLabel(labels, RequiredChapterLabel);
        return labels;
    }

    private List<string> BuildOptionalPreloadLabels()
    {
        List<string> labels = new List<string>();
        AddLabel(labels, OptionalChapterLabel);
        return labels;
    }

    private void AddLabel(List<string> labels, string label)
    {
        if (string.IsNullOrWhiteSpace(label) || labels.Contains(label))
        {
            return;
        }

        labels.Add(label);
    }

    private IEnumerator InitializeAndUpdateCatalogs(bool showProgressTask, System.Action<bool> initializedCallback)
    {
        if (showProgressTask)
        {
            RegisterOrUpdateLoadingTask(0f, "检查资源目录");
        }

        initializedCallback(false);
        AsyncOperationHandle initializeHandle;
        try
        {
            initializeHandle = Addressables.InitializeAsync(false);
        }
        catch (System.Exception e)
        {
            RecordFailure(RemoteContentOperationStage.Initialize, ClassifyFailure(e, false),
                null, e.Message, true, 1);
            yield break;
        }
        yield return WaitForAddressablesOperation(initializeHandle, CatalogOperationTimeoutSeconds);

        if (!initializeHandle.IsDone)
        {
            RecordFailure(RemoteContentOperationStage.Initialize, RemoteContentErrorKind.Timeout, null, "Addressables 初始化超时", true, 1);
            Debug.LogWarning("[RemoteContentPreload] Addressables initialization timed out. Skipping catalog update.");
            DrainTimedOutOperation(initializeHandle);
            yield break;
        }

        if (initializeHandle.Status != AsyncOperationStatus.Succeeded)
        {
            RecordFailure(RemoteContentOperationStage.Initialize, ClassifyFailure(initializeHandle.OperationException, false), null, initializeHandle.OperationException?.Message, true, 1);
            Debug.LogWarning($"[RemoteContentPreload] Failed to initialize Addressables: {initializeHandle.OperationException?.Message}");
            ReleaseIfValid(initializeHandle);
            yield break;
        }

        ReleaseIfValid(initializeHandle);

        initializedCallback(true);
        AsyncOperationHandle<List<string>> checkHandle = Addressables.CheckForCatalogUpdates(false);
        yield return WaitForAddressablesOperation(checkHandle, CatalogOperationTimeoutSeconds);

        if (!checkHandle.IsDone)
        {
            RecordFailure(RemoteContentOperationStage.CatalogCheck, RemoteContentErrorKind.Timeout, null, "Catalog 检查超时", true, 1);
            Debug.LogWarning("[RemoteContentPreload] Catalog update check timed out. Continuing with the currently loaded catalog.");
            DrainTimedOutOperation(checkHandle);
            yield break;
        }

        if (checkHandle.Status != AsyncOperationStatus.Succeeded)
        {
            RecordFailure(RemoteContentOperationStage.CatalogCheck, ClassifyFailure(checkHandle.OperationException, false), null, checkHandle.OperationException?.Message, true, 1);
            Debug.LogWarning($"[RemoteContentPreload] Failed to check catalog updates: {checkHandle.OperationException?.Message}");
            ReleaseIfValid(checkHandle);
            yield break;
        }

        List<string> catalogsToUpdate = checkHandle.Result;
        if (catalogsToUpdate == null || catalogsToUpdate.Count == 0)
        {
            Addressables.Release(checkHandle);
            yield break;
        }

        if (showProgressTask)
        {
            RegisterOrUpdateLoadingTask(0f, "更新资源目录");
        }

        AsyncOperationHandle<List<IResourceLocator>> updateHandle = Addressables.UpdateCatalogs(catalogsToUpdate, false);
        yield return WaitForAddressablesOperation(updateHandle, CatalogOperationTimeoutSeconds);

        if (!updateHandle.IsDone)
        {
            RecordFailure(RemoteContentOperationStage.CatalogUpdate, RemoteContentErrorKind.Timeout, null, "Catalog 更新超时", true, 1);
            Debug.LogWarning("[RemoteContentPreload] Catalog update timed out. Continuing with the currently loaded catalog.");
            DrainTimedOutOperation(updateHandle);
            ReleaseIfValid(checkHandle);
            yield break;
        }

        if (updateHandle.Status == AsyncOperationStatus.Succeeded)
        {
            completedLabels.Clear();
            failedLabels.Clear();
            ResourcesManager.GetInstance().ReleaseCachedAssets();
            Debug.Log($"[RemoteContentPreload] Updated {catalogsToUpdate.Count} remote catalog(s).");
        }
        else
        {
            RecordFailure(RemoteContentOperationStage.CatalogUpdate, ClassifyFailure(updateHandle.OperationException, false), null, updateHandle.OperationException?.Message, true, 1);
            Debug.LogWarning($"[RemoteContentPreload] Failed to update catalogs: {updateHandle.OperationException?.Message}");
        }

        ReleaseIfValid(updateHandle);
        ReleaseIfValid(checkHandle);
    }

    private IEnumerator WaitForAddressablesOperation(AsyncOperationHandle handle, float timeoutSeconds)
    {
        float startedAt = Time.realtimeSinceStartup;
        while (!handle.IsDone && Time.realtimeSinceStartup - startedAt < timeoutSeconds)
        {
            yield return null;
        }
    }

    private IEnumerator PreloadLabels(List<string> labels, bool showProgressTask, string preparingTaskName, string cachedTaskName, string completedTaskName)
    {
        lastPreloadSucceeded = false;
        yield return PreloadLabels(labels, showProgressTask, preparingTaskName, cachedTaskName, completedTaskName, succeeded => lastPreloadSucceeded = succeeded);
    }

    private IEnumerator PreloadLabels(List<string> labels, bool showProgressTask, string preparingTaskName, string cachedTaskName, string completedTaskName, System.Action<bool> completedCallback)
    {
        if (labels == null || labels.Count == 0)
        {
            completedCallback?.Invoke(true);
            yield break;
        }

        progress = 0f;
        downloadedBytes = 0;

        if (showProgressTask && !string.IsNullOrEmpty(preparingTaskName))
        {
            RegisterOrUpdateLoadingTask(0f, preparingTaskName);
        }

        yield return CalculateTotalDownloadSize(labels);

        if (lastSizeQueryFailed)
        {
            if (showProgressTask)
            {
                RegisterOrUpdateLoadingTask(progress, "资源下载失败，请检查网络后重试");
            }
            completedCallback?.Invoke(false);
            yield break;
        }

        if (totalDownloadBytes <= 0)
        {
            progress = 1f;
            if (showProgressTask && !string.IsNullOrEmpty(cachedTaskName))
            {
                CompleteLoadingTask(cachedTaskName);
            }
            Debug.Log("[RemoteContentPreload] No remote content needs downloading.");
            completedCallback?.Invoke(true);
            yield break;
        }

        if (showProgressTask)
        {
            RegisterOrUpdateLoadingTask(0f, $"{LoadingTaskName} 0% ({FormatBytes(0)} / {FormatBytes(totalDownloadBytes)})");
        }

        long completedBytesBeforeLabel = 0;
        bool allSucceeded = true;

        foreach (string label in labels)
        {
            if (completedLabels.Contains(label))
            {
                continue;
            }

            long labelSize = 0;
            yield return GetDownloadSize(label, currentPreloadIsRequired, size => labelSize = size);

            if (lastSizeQueryFailed)
            {
                allSucceeded = false;
                break;
            }

            if (labelSize <= 0)
            {
                completedLabels.Add(label);
                continue;
            }

            bool labelSucceeded = false;
            yield return DownloadLabel(label, labelSize, completedBytesBeforeLabel, showProgressTask, succeeded => labelSucceeded = succeeded);
            if (!labelSucceeded)
            {
                allSucceeded = false;
                if (showProgressTask)
                {
                    RegisterOrUpdateLoadingTask(progress, $"资源下载失败，请检查网络后重试: {label}");
                }
                break;
            }

            completedBytesBeforeLabel += labelSize;
            downloadedBytes = System.Math.Min(totalDownloadBytes, completedBytesBeforeLabel);
            progress = totalDownloadBytes > 0 ? (float)downloadedBytes / totalDownloadBytes : 1f;

            if (showProgressTask)
            {
                RegisterOrUpdateLoadingTask(progress, BuildProgressTaskName(label));
            }
        }

        if (allSucceeded)
        {
            progress = 1f;
            if (showProgressTask && !string.IsNullOrEmpty(completedTaskName))
            {
                CompleteLoadingTask(completedTaskName);
            }
        }

        completedCallback?.Invoke(allSucceeded);
    }

    private IEnumerator CalculateTotalDownloadSize(List<string> labels)
    {
        totalDownloadBytes = 0;
        lastSizeQueryFailed = false;

        foreach (string label in labels)
        {
            if (completedLabels.Contains(label))
            {
                continue;
            }

            long size = 0;
            yield return GetDownloadSize(label, currentPreloadIsRequired, result => size = result);
            if (lastSizeQueryFailed)
            {
                yield break;
            }
            totalDownloadBytes += size;
        }

        Debug.Log($"[RemoteContentPreload] Preload size: {FormatBytes(totalDownloadBytes)}.");
    }

    private bool ValidateLocationState(string label, bool required, RemoteContentLocationState state)
    {
        if (state == RemoteContentLocationState.Found)
        {
            return true;
        }

        if (state == RemoteContentLocationState.Failed || required)
        {
            lastSizeQueryFailed = true;
            failedLabels.Add(label);
            if (state == RemoteContentLocationState.NotFound)
            {
                RecordFailure(RemoteContentOperationStage.LocationLookup, RemoteContentErrorKind.ResourceMissing,
                    label, $"必需资源标签不存在或为空: {label}", false, 1);
            }
        }
        else
        {
            Debug.LogWarning($"[RemoteContentPreload] Optional label is absent or empty; skipping: {label}");
        }

        return false;
    }

    private IEnumerator GetDownloadSize(string label, bool required, System.Action<long> callback)
    {
        RemoteContentLocationState locationState = RemoteContentLocationState.Failed;
        yield return HasLocations(label, result => locationState = result);

        if (!ValidateLocationState(label, required, locationState))
        {
            callback?.Invoke(0);
            yield break;
        }

        AsyncOperationHandle<long> sizeHandle;
        try
        {
            sizeHandle = Addressables.GetDownloadSizeAsync(label);
        }
        catch (System.Exception e)
        {
            failedLabels.Add(label);
            lastSizeQueryFailed = true;
            RecordFailure(RemoteContentOperationStage.SizeQuery, ClassifyFailure(e, false), label, e.Message, true, 1);
            callback?.Invoke(0);
            yield break;
        }
        yield return WaitForAddressablesOperation(sizeHandle, CatalogOperationTimeoutSeconds);

        if (!sizeHandle.IsDone)
        {
            Debug.LogWarning($"[RemoteContentPreload] Download size query timed out: {label}");
            failedLabels.Add(label);
            lastSizeQueryFailed = true;
            RecordFailure(RemoteContentOperationStage.SizeQuery, RemoteContentErrorKind.Timeout, label, "资源大小查询超时", true, 1);
            DrainTimedOutOperation(sizeHandle);
            callback?.Invoke(0);
            yield break;
        }

        if (sizeHandle.Status == AsyncOperationStatus.Succeeded)
        {
            callback?.Invoke(sizeHandle.Result);
        }
        else
        {
            Debug.LogWarning($"[RemoteContentPreload] Failed to query download size: {label}");
            failedLabels.Add(label);
            lastSizeQueryFailed = true;
            RecordFailure(RemoteContentOperationStage.SizeQuery, ClassifyFailure(sizeHandle.OperationException, false), label, sizeHandle.OperationException?.Message, true, 1);
            callback?.Invoke(0);
        }

        ReleaseIfValid(sizeHandle);
    }

    private IEnumerator HasLocations(string label, System.Action<RemoteContentLocationState> callback)
    {
        AsyncOperationHandle<IList<IResourceLocation>> locationHandle;
        try
        {
            locationHandle = Addressables.LoadResourceLocationsAsync(label);
        }
        catch (System.Exception e)
        {
            RecordFailure(RemoteContentOperationStage.LocationLookup, ClassifyFailure(e, false), label, e.Message, true, 1);
            callback?.Invoke(RemoteContentLocationState.Failed);
            yield break;
        }
        yield return WaitForAddressablesOperation(locationHandle, CatalogOperationTimeoutSeconds);

        if (!locationHandle.IsDone)
        {
            Debug.LogWarning($"[RemoteContentPreload] Location lookup timed out: {label}");
            RecordFailure(RemoteContentOperationStage.LocationLookup, RemoteContentErrorKind.Timeout, label, "资源位置查询超时", true, 1);
            DrainTimedOutOperation(locationHandle);
            callback?.Invoke(RemoteContentLocationState.Failed);
            yield break;
        }

        if (locationHandle.Status != AsyncOperationStatus.Succeeded)
        {
            RecordFailure(RemoteContentOperationStage.LocationLookup, ClassifyFailure(locationHandle.OperationException, false), label, locationHandle.OperationException?.Message, true, 1);
            ReleaseIfValid(locationHandle);
            callback?.Invoke(RemoteContentLocationState.Failed);
            yield break;
        }

        bool hasLocations = locationHandle.Result != null && locationHandle.Result.Count > 0;
        ReleaseIfValid(locationHandle);
        callback?.Invoke(hasLocations ? RemoteContentLocationState.Found : RemoteContentLocationState.NotFound);
    }

    private IEnumerator DownloadLabel(string label, long labelSize, long completedBytesBeforeLabel, bool showProgressTask, System.Action<bool> completedCallback)
    {
        int maxAttempts = currentPreloadIsRequired ? MaxRequiredDownloadAttempts : MaxOptionalDownloadAttempts;
        for (int attempt = 1; attempt <= maxAttempts; attempt++)
        {
            if (!IsNetworkAvailable())
            {
                RecordFailure(RemoteContentOperationStage.Download, RemoteContentErrorKind.Offline, label, "设备当前离线", true, attempt);
                if (attempt >= maxAttempts)
                {
                    completedCallback?.Invoke(false);
                    yield break;
                }

                yield return WaitForNetworkRecovery(currentPreloadIsRequired ? 30f : 0f);
                if (!IsNetworkAvailable())
                {
                    continue;
                }
            }

            bool attemptSucceeded = false;
            bool attemptCanRetry = true;
            yield return DownloadLabelAttempt(
                label,
                labelSize,
                completedBytesBeforeLabel,
                showProgressTask,
                attempt,
                (succeeded, canRetry) =>
                {
                    attemptSucceeded = succeeded;
                    attemptCanRetry = canRetry;
                });

            if (attemptSucceeded)
            {
                completedCallback?.Invoke(true);
                yield break;
            }

            if (!attemptCanRetry || attempt >= maxAttempts)
            {
                completedCallback?.Invoke(false);
                yield break;
            }

            TrySwitchToBackupCdn(lastOperationResult.ErrorKind);
            yield return WaitForPendingTimedOutOperations(showProgressTask);
            yield return new WaitForSecondsRealtime(AddRetryJitter(RetryBaseDelaySeconds * Mathf.Pow(2f, attempt - 1)));
        }

        completedCallback?.Invoke(false);
    }

    private IEnumerator DownloadLabelAttempt(
        string label,
        long labelSize,
        long completedBytesBeforeLabel,
        bool showProgressTask,
        int attempt,
        System.Action<bool, bool> completedCallback)
    {
        Debug.Log($"[RemoteContentPreload] Downloading {label}: {FormatBytes(labelSize)}, attempt {attempt}.");

        AsyncOperationHandle downloadHandle;
        try
        {
            downloadHandle = Addressables.DownloadDependenciesAsync(label, false);
        }
        catch (System.Exception e)
        {
            RemoteContentErrorKind errorKind = ClassifyFailure(e, false);
            RecordFailure(RemoteContentOperationStage.Download, errorKind, label, e.Message, IsRetryable(errorKind), attempt);
            completedCallback?.Invoke(false, IsRetryable(errorKind));
            yield break;
        }
        float startedAt = Time.realtimeSinceStartup;

        while (!downloadHandle.IsDone && Time.realtimeSinceStartup - startedAt < RemoteOperationTimeoutSeconds)
        {
            var downloadStatus = downloadHandle.GetDownloadStatus();
            long currentLabelBytes = downloadStatus.TotalBytes > 0
                ? downloadStatus.DownloadedBytes
                : (long)(labelSize * downloadHandle.PercentComplete);
            currentLabelBytes = System.Math.Min(labelSize, currentLabelBytes);
            downloadedBytes = completedBytesBeforeLabel + currentLabelBytes;
            progress = totalDownloadBytes > 0 ? (float)downloadedBytes / totalDownloadBytes : 0f;
            if (showProgressTask)
            {
                RegisterOrUpdateLoadingTask(progress, BuildProgressTaskName(label));
            }
            yield return null;
        }

        if (!downloadHandle.IsDone)
        {
            failedLabels.Add(label);
            Debug.LogWarning($"[RemoteContentPreload] Download timed out: {label}");
            RecordFailure(RemoteContentOperationStage.Download, RemoteContentErrorKind.Timeout, label, "资源下载超时", true, attempt);
            DrainTimedOutOperation(downloadHandle);
            completedCallback?.Invoke(false, true);
            yield break;
        }

        if (downloadHandle.Status == AsyncOperationStatus.Succeeded)
        {
            completedLabels.Add(label);
            failedLabels.Remove(label);
            Debug.Log($"[RemoteContentPreload] Downloaded {label}.");
            RecordSuccess(RemoteContentOperationStage.Download, label, attempt);
            completedCallback?.Invoke(true, false);
        }
        else
        {
            failedLabels.Add(label);
            Debug.LogWarning($"[RemoteContentPreload] Failed to download {label}: {downloadHandle.OperationException?.Message}");
            RemoteContentErrorKind errorKind = ClassifyFailure(downloadHandle.OperationException, false);
            bool canRetry = IsRetryable(errorKind)
                || (errorKind == RemoteContentErrorKind.ResourceMissing && !string.IsNullOrEmpty(backupCdnRoot));
            RecordFailure(RemoteContentOperationStage.Download, errorKind, label, downloadHandle.OperationException?.Message, canRetry, attempt);
            completedCallback?.Invoke(false, canRetry);
        }

        ReleaseIfValid(downloadHandle);
    }

    private void ConfigureAddressablesWebRequests()
    {
        Addressables.ResourceManager.WebRequestOverride = request =>
        {
            if (request.timeout <= 0)
            {
                request.timeout = BundleRequestTimeoutSeconds;
            }

            if (request.redirectLimit < 0)
            {
                request.redirectLimit = 4;
            }
        };
    }

    private bool IsNetworkAvailable()
    {
        return Application.internetReachability != NetworkReachability.NotReachable;
    }

    private bool IsOptionalNetworkAllowed()
    {
        if (!IsNetworkAvailable())
        {
            return false;
        }

        return Application.internetReachability != NetworkReachability.ReachableViaCarrierDataNetwork
            || AllowOptionalDownloadOnCellular;
    }

    private bool TrySwitchToBackupCdn(RemoteContentErrorKind errorKind)
    {
        if (useBackupCdn || string.IsNullOrEmpty(backupCdnRoot))
        {
            return false;
        }

        if (errorKind != RemoteContentErrorKind.ServerUnavailable
            && errorKind != RemoteContentErrorKind.ResourceMissing)
        {
            return false;
        }

        useBackupCdn = true;
        Debug.LogWarning("[RemoteContentPreload] Switching Addressables requests to the configured backup CDN.");
        return true;
    }

    private string TransformCdnInternalId(string internalId)
    {
        if (string.IsNullOrEmpty(internalId) || string.IsNullOrEmpty(primaryCdnRoot)
            || !internalId.StartsWith(primaryCdnRoot, System.StringComparison.OrdinalIgnoreCase))
        {
            return internalId;
        }

        string selectedRoot = useBackupCdn ? backupCdnRoot : primaryCdnRoot;
        return selectedRoot + internalId.Substring(primaryCdnRoot.Length);
    }

    private string NormalizeCdnRoot(string root)
    {
        if (string.IsNullOrWhiteSpace(root)
            || !System.Uri.TryCreate(root, System.UriKind.Absolute, out System.Uri uri)
            || (uri.Scheme != System.Uri.UriSchemeHttps && uri.Scheme != System.Uri.UriSchemeHttp))
        {
            return null;
        }

        return root.Trim().TrimEnd('/') + "/";
    }

    private string ExtractContentVersion(string root)
    {
        if (!System.Uri.TryCreate(root, System.UriKind.Absolute, out System.Uri uri))
        {
            return null;
        }

        string[] segments = uri.AbsolutePath.Split(new[] { '/' }, System.StringSplitOptions.RemoveEmptyEntries);
        for (int i = 0; i + 1 < segments.Length; i++)
        {
            if (string.Equals(segments[i], "prod", System.StringComparison.OrdinalIgnoreCase))
            {
                return segments[i + 1];
            }
        }

        return null;
    }

    private IEnumerator WaitForNetworkRecovery(float maximumWaitSeconds)
    {
        float startedAt = Time.realtimeSinceStartup;
        while (!IsNetworkAvailable())
        {
            if (maximumWaitSeconds > 0f && Time.realtimeSinceStartup - startedAt >= maximumWaitSeconds)
            {
                yield break;
            }

            yield return new WaitForSecondsRealtime(NetworkRecoveryPollSeconds);
        }
    }

    private IEnumerator WaitForPendingTimedOutOperations(bool showProgressTask)
    {
        while (pendingTimedOutOperations > 0)
        {
            if (showProgressTask)
            {
                RegisterOrUpdateLoadingTask(progress, "等待上一网络请求结束");
            }
            yield return null;
        }
    }

    private void DrainTimedOutOperation(AsyncOperationHandle handle)
    {
        if (!handle.IsValid())
        {
            return;
        }

        pendingTimedOutOperations++;
        MonoManager.GetInstance().StartCoroutine(ReleaseOperationWhenComplete(handle));
    }

    private IEnumerator ReleaseOperationWhenComplete(AsyncOperationHandle handle)
    {
        while (handle.IsValid() && !handle.IsDone)
        {
            yield return null;
        }

        ReleaseIfValid(handle);
        pendingTimedOutOperations = Mathf.Max(0, pendingTimedOutOperations - 1);
    }

    private void ReleaseIfValid(AsyncOperationHandle handle)
    {
        if (handle.IsValid())
        {
            Addressables.Release(handle);
        }
    }

    private float AddRetryJitter(float delaySeconds)
    {
        return delaySeconds * UnityEngine.Random.Range(0.85f, 1.15f);
    }

    public static RemoteContentErrorKind ClassifyFailure(System.Exception exception, bool timedOut)
    {
        return ClassifyFailure(exception, timedOut, Application.internetReachability);
    }

    public static RemoteContentErrorKind ClassifyFailure(
        System.Exception exception,
        bool timedOut,
        NetworkReachability reachability)
    {
        if (timedOut)
        {
            return RemoteContentErrorKind.Timeout;
        }

        if (reachability == NetworkReachability.NotReachable)
        {
            return RemoteContentErrorKind.Offline;
        }

        string message = exception?.ToString()?.ToLowerInvariant() ?? string.Empty;
        if (message.Contains("404") || message.Contains("not found"))
        {
            return RemoteContentErrorKind.ResourceMissing;
        }
        if (message.Contains("crc") || message.Contains("hash") || message.Contains("corrupt"))
        {
            return RemoteContentErrorKind.IntegrityFailure;
        }
        if (message.Contains("disk") || message.Contains("storage") || message.Contains("quota") || message.Contains("space"))
        {
            return RemoteContentErrorKind.StorageFailure;
        }
        if (message.Contains("timeout") || message.Contains("timed out"))
        {
            return RemoteContentErrorKind.Timeout;
        }
        if (message.Contains("403") || message.Contains("401"))
        {
            return RemoteContentErrorKind.ResourceMissing;
        }
        if (message.Contains("429") || message.Contains("408") || message.Contains("500")
            || message.Contains("502") || message.Contains("503") || message.Contains("504")
            || message.Contains("network") || message.Contains("dns") || message.Contains("connection"))
        {
            return RemoteContentErrorKind.ServerUnavailable;
        }

        return RemoteContentErrorKind.Unknown;
    }

    public static bool IsRetryable(RemoteContentErrorKind errorKind)
    {
        return errorKind == RemoteContentErrorKind.Offline
            || errorKind == RemoteContentErrorKind.Timeout
            || errorKind == RemoteContentErrorKind.ServerUnavailable
            || errorKind == RemoteContentErrorKind.IntegrityFailure
            || errorKind == RemoteContentErrorKind.Unknown;
    }

    private void RecordSuccess(RemoteContentOperationStage stage, string label, int attempt)
    {
        lastOperationResult = new RemoteContentOperationResult
        {
            Stage = stage,
            ErrorKind = RemoteContentErrorKind.None,
            Label = label,
            CanRetry = false,
            Attempt = attempt
        };
        Debug.Log($"[RemoteContentTelemetry] stage={stage} result=success label={label ?? "-"} attempt={attempt} network={Application.internetReachability}");
    }

    private void RecordFailure(
        RemoteContentOperationStage stage,
        RemoteContentErrorKind errorKind,
        string label,
        string error,
        bool canRetry,
        int attempt)
    {
        lastOperationResult = new RemoteContentOperationResult
        {
            Stage = stage,
            ErrorKind = errorKind,
            Label = label,
            Error = error,
            CanRetry = canRetry,
            Attempt = attempt
        };
        lastError = BuildUserFacingError(lastOperationResult);
        Debug.LogWarning(
            $"[RemoteContentTelemetry] stage={stage} result=failure kind={errorKind} label={label ?? "-"} " +
            $"attempt={attempt} retryable={canRetry} network={Application.internetReachability} error={SanitizeError(error)}");
    }

    private string BuildUserFacingError(RemoteContentOperationResult result)
    {
        switch (result.ErrorKind)
        {
            case RemoteContentErrorKind.Offline:
                return "当前无网络连接，联网后请重试";
            case RemoteContentErrorKind.Timeout:
                return $"网络响应超时，请重试（第 {Mathf.Max(1, result.Attempt)} 次）";
            case RemoteContentErrorKind.ResourceMissing:
                return "服务器资源不存在或版本不匹配，请稍后再试";
            case RemoteContentErrorKind.IntegrityFailure:
                return "资源校验失败，正在等待重新下载";
            case RemoteContentErrorKind.StorageFailure:
                return "存储空间不足或缓存不可用，请清理空间后重试";
            case RemoteContentErrorKind.ServerUnavailable:
                return "资源服务器暂时不可用，请稍后重试";
            default:
                return "资源下载失败，请检查网络后重试";
        }
    }

    private string SanitizeError(string error)
    {
        if (string.IsNullOrEmpty(error))
        {
            return "-";
        }

        int queryIndex = error.IndexOf('?');
        string sanitized = queryIndex >= 0 ? error.Substring(0, queryIndex) : error;
        return sanitized.Replace('\r', ' ').Replace('\n', ' ');
    }

    private void RegisterOrUpdateLoadingTask(float taskProgress, string taskName)
    {
        LoadingProgressManager progressManager = LoadingProgressManager.GetInstance();
        LoadingTask existingTask = progressManager.GetTask(LoadingTaskID);

        if (existingTask != null && existingTask.IsCompleted && taskProgress < 1f)
        {
            progressManager.UnregisterTask(LoadingTaskID);
            existingTask = null;
        }

        if (existingTask == null)
        {
            progressManager.RegisterTask(LoadingTaskID, taskName, LoadingTaskWeight);
        }
        else
        {
            progressManager.UpdateTaskName(LoadingTaskID, taskName);
        }

        progressManager.UpdateTaskProgress(LoadingTaskID, taskProgress);
    }

    private void CompleteLoadingTask(string taskName)
    {
        LoadingProgressManager progressManager = LoadingProgressManager.GetInstance();

        if (progressManager.GetTaskProgress(LoadingTaskID) < 0f)
        {
            progressManager.RegisterTask(LoadingTaskID, taskName, LoadingTaskWeight);
        }
        else
        {
            progressManager.UpdateTaskName(LoadingTaskID, taskName);
        }

        progressManager.CompleteTask(LoadingTaskID);
    }

    private string BuildProgressTaskName(string label)
    {
        return $"{LoadingTaskName}: {label} {progress * 100f:F0}% ({FormatBytes(downloadedBytes)} / {FormatBytes(totalDownloadBytes)})";
    }

    private string FormatBytes(long bytes)
    {
        if (bytes < 1024) return $"{bytes} B";
        if (bytes < 1024 * 1024) return $"{bytes / 1024f:F1} KB";
        return $"{bytes / (1024f * 1024f):F1} MB";
    }
}
