using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.Events;
using UnityEngine.ResourceManagement.AsyncOperations;
using UnityEngine.ResourceManagement.ResourceLocations;

/// <summary>
/// Central Addressables loader. Addresses keep the old Resources-style keys
/// so existing config paths continue to work after assets are marked addressable.
/// </summary>
public class ResourcesManager : BaseManager<ResourcesManager>
{
    private readonly Dictionary<string, AsyncOperationHandle> loadedAssetHandles = new Dictionary<string, AsyncOperationHandle>();
    private readonly Dictionary<string, AsyncOperationHandle> loadedAssetListHandles = new Dictionary<string, AsyncOperationHandle>();
    private readonly Dictionary<string, AsyncOperationHandle> loadedLocationHandles = new Dictionary<string, AsyncOperationHandle>();

    public T Load<T>(string name) where T : UnityEngine.Object
    {
#if UNITY_WEBGL && !UNITY_EDITOR
        Debug.LogWarning($"[ResourcesManager] Synchronous Addressables loading is not supported on WebGL. Using Resources fallback for: {name}");
        return LoadFromResources<T>(name);
#else
        IResourceLocation location = GetResourceLocation<T>(name);
        if (location == null)
        {
            return LoadFromResources<T>(name);
        }

        AsyncOperationHandle<T> handle = GetOrCreateAssetHandle<T>(name, location);
        T asset = CompleteHandle(handle, name);
        if (asset == null)
        {
            return LoadFromResources<T>(name);
        }

        return CreateReturnValue(asset);
#endif
    }

    public T[] LoadAll<T>(string key) where T : UnityEngine.Object
    {
#if UNITY_WEBGL && !UNITY_EDITOR
        Debug.LogWarning($"[ResourcesManager] Synchronous Addressables LoadAll is not supported on WebGL. Using Resources fallback for: {key}");
        return LoadAllFromResources<T>(key);
#else
        IList<IResourceLocation> locations = GetResourceLocations<T>(key);
        if (locations == null || locations.Count == 0)
        {
            return LoadAllFromResources<T>(key);
        }

        AsyncOperationHandle<IList<T>> handle = GetOrCreateAssetListHandle<T>(key, locations);
        IList<T> assets = CompleteHandle(handle, key);

        if (assets == null)
        {
            return LoadAllFromResources<T>(key);
        }

        T[] result = new T[assets.Count];
        for (int i = 0; i < assets.Count; i++)
        {
            result[i] = assets[i];
        }

        return result;
#endif
    }

    public IEnumerator LoadAllAsync<T>(string key, UnityAction<T[]> callback) where T : UnityEngine.Object
    {
        IList<IResourceLocation> locations = null;
        yield return GetResourceLocationsAsync<T>(key, result => locations = result);

        if (locations == null || locations.Count == 0)
        {
            callback?.Invoke(LoadAllFromResources<T>(key));
            yield break;
        }

        AsyncOperationHandle<IList<T>> handle = GetOrCreateAssetListHandle<T>(key, locations);
        yield return handle;

        IList<T> assets = GetHandleResult(handle, key);
        if (assets == null)
        {
            callback?.Invoke(LoadAllFromResources<T>(key));
            yield break;
        }

        T[] result = new T[assets.Count];
        for (int i = 0; i < assets.Count; i++)
        {
            result[i] = assets[i];
        }

        callback?.Invoke(result);
    }

    public void LoadAsync<T>(string name, UnityAction<T> callback) where T : UnityEngine.Object
    {
        MonoManager.GetInstance().StartCoroutine(ILoadAsync<T>(name, callback, null));
    }

    public void LoadOptionalAsync<T>(string name, UnityAction<T> callback) where T : UnityEngine.Object
    {
        MonoManager.GetInstance().StartCoroutine(ILoadOptionalAsync<T>(name, callback));
    }

    public void LoadLocalFirstAsync<T>(string name, UnityAction<T> callback, string taskID = null, string taskName = null, float weight = 1f) where T : UnityEngine.Object
    {
        if (!string.IsNullOrEmpty(taskID))
        {
            LoadingProgressManager progressManager = LoadingProgressManager.GetInstance();
            if (progressManager.GetTaskProgress(taskID) < 0)
            {
                string displayName = string.IsNullOrEmpty(taskName) ? $"Load asset: {name}" : taskName;
                progressManager.RegisterTask(taskID, displayName, weight);
            }
            else if (!string.IsNullOrEmpty(taskName))
            {
                progressManager.UpdateTaskName(taskID, taskName);
            }
        }

        T localAsset = Resources.Load<T>(name);
        if (localAsset != null)
        {
            if (!string.IsNullOrEmpty(taskID))
            {
                LoadingProgressManager.GetInstance().CompleteTask(taskID);
            }

            callback?.Invoke(CreateReturnValue(localAsset));
            return;
        }

        MonoManager.GetInstance().StartCoroutine(ILoadAsync<T>(name, callback, taskID));
    }

    public void LoadAsync<T>(string name, UnityAction<T> callback, string taskID = null, string taskName = null, float weight = 1f) where T : UnityEngine.Object
    {
        if (!string.IsNullOrEmpty(taskID))
        {
            LoadingProgressManager progressManager = LoadingProgressManager.GetInstance();
            if (progressManager.GetTaskProgress(taskID) < 0)
            {
                string displayName = string.IsNullOrEmpty(taskName) ? $"Load asset: {name}" : taskName;
                progressManager.RegisterTask(taskID, displayName, weight);
            }
            else if (!string.IsNullOrEmpty(taskName))
            {
                progressManager.UpdateTaskName(taskID, taskName);
            }
        }

        MonoManager.GetInstance().StartCoroutine(ILoadAsync<T>(name, callback, taskID));
    }

    public void ReleaseCachedAssets()
    {
        foreach (AsyncOperationHandle handle in loadedAssetHandles.Values)
        {
            if (handle.IsValid())
            {
                Addressables.Release(handle);
            }
        }

        foreach (AsyncOperationHandle handle in loadedAssetListHandles.Values)
        {
            if (handle.IsValid())
            {
                Addressables.Release(handle);
            }
        }

        foreach (AsyncOperationHandle handle in loadedLocationHandles.Values)
        {
            if (handle.IsValid())
            {
                Addressables.Release(handle);
            }
        }

        loadedAssetHandles.Clear();
        loadedAssetListHandles.Clear();
        loadedLocationHandles.Clear();
    }

    private IEnumerator ILoadAsync<T>(string name, UnityAction<T> callback, string taskID) where T : UnityEngine.Object
    {
        IList<IResourceLocation> locations = null;
        yield return GetResourceLocationsAsync<T>(name, result => locations = result);

        IResourceLocation location = locations != null && locations.Count > 0 ? locations[0] : null;
        if (location == null)
        {
            if (!string.IsNullOrEmpty(taskID))
            {
                LoadingProgressManager.GetInstance().CompleteTask(taskID);
            }

            callback?.Invoke(LoadFromResources<T>(name));
            yield break;
        }

        AsyncOperationHandle<T> handle = GetOrCreateAssetHandle<T>(name, location);

        if (!string.IsNullOrEmpty(taskID))
        {
            while (!handle.IsDone)
            {
                LoadingProgressManager.GetInstance().UpdateTaskProgress(taskID, handle.PercentComplete);
                yield return null;
            }
        }
        else
        {
            yield return handle;
        }

        if (!string.IsNullOrEmpty(taskID))
        {
            LoadingProgressManager.GetInstance().CompleteTask(taskID);
        }

        T asset = GetHandleResult(handle, name);
        if (asset == null)
        {
            callback?.Invoke(LoadFromResources<T>(name));
            yield break;
        }

        callback?.Invoke(CreateReturnValue(asset));
    }

    private IEnumerator ILoadOptionalAsync<T>(string name, UnityAction<T> callback) where T : UnityEngine.Object
    {
        IList<IResourceLocation> locations = null;
        yield return GetResourceLocationsAsync<T>(name, result => locations = result);

        IResourceLocation location = locations != null && locations.Count > 0 ? locations[0] : null;
        if (location == null)
        {
            T resourceAsset = Resources.Load<T>(name);
            callback?.Invoke(resourceAsset == null ? null : CreateReturnValue(resourceAsset));
            yield break;
        }

        AsyncOperationHandle<T> handle = GetOrCreateAssetHandle<T>(name, location);
        yield return handle;

        if (handle.Status == AsyncOperationStatus.Succeeded && handle.Result != null)
        {
            callback?.Invoke(CreateReturnValue(handle.Result));
            yield break;
        }

        T fallbackAsset = Resources.Load<T>(name);
        callback?.Invoke(fallbackAsset == null ? null : CreateReturnValue(fallbackAsset));
    }

    private AsyncOperationHandle<T> GetOrCreateAssetHandle<T>(string name, IResourceLocation location) where T : UnityEngine.Object
    {
        string cacheKey = GetCacheKey<T>(name);

        if (loadedAssetHandles.TryGetValue(cacheKey, out AsyncOperationHandle cachedHandle) && cachedHandle.IsValid())
        {
            return cachedHandle.Convert<T>();
        }

        AsyncOperationHandle<T> handle = Addressables.LoadAssetAsync<T>(location);
        loadedAssetHandles[cacheKey] = handle;
        return handle;
    }

    private IResourceLocation GetResourceLocation<T>(string key) where T : UnityEngine.Object
    {
        IList<IResourceLocation> locations = GetResourceLocations<T>(key);
        if (locations == null || locations.Count == 0)
        {
            return null;
        }

        return locations[0];
    }

    private IEnumerator GetResourceLocationsAsync<T>(string key, UnityAction<IList<IResourceLocation>> callback) where T : UnityEngine.Object
    {
        string cacheKey = GetLocationCacheKey<T>(key);

        if (loadedLocationHandles.TryGetValue(cacheKey, out AsyncOperationHandle cachedHandle) && cachedHandle.IsValid())
        {
            AsyncOperationHandle<IList<IResourceLocation>> cachedLocationsHandle = cachedHandle.Convert<IList<IResourceLocation>>();
            if (!cachedLocationsHandle.IsDone)
            {
                yield return cachedLocationsHandle;
            }

            callback?.Invoke(cachedLocationsHandle.Status == AsyncOperationStatus.Succeeded ? cachedLocationsHandle.Result : null);
            yield break;
        }

        AsyncOperationHandle<IList<IResourceLocation>> handle;
        try
        {
            handle = Addressables.LoadResourceLocationsAsync(key, typeof(T));
            loadedLocationHandles[cacheKey] = handle;
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[ResourcesManager] Addressables location lookup exception, falling back to Resources: {key}. {e.Message}");
            callback?.Invoke(null);
            yield break;
        }

        yield return handle;

        if (handle.Status == AsyncOperationStatus.Succeeded && handle.Result != null && handle.Result.Count > 0)
        {
            callback?.Invoke(handle.Result);
            yield break;
        }

        if (handle.IsValid())
        {
            Addressables.Release(handle);
        }

        loadedLocationHandles.Remove(cacheKey);

        AsyncOperationHandle<IList<IResourceLocation>> untypedHandle;
        try
        {
            untypedHandle = Addressables.LoadResourceLocationsAsync(key);
            loadedLocationHandles[cacheKey] = untypedHandle;
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[ResourcesManager] Addressables untyped location lookup exception, falling back to Resources: {key}. {e.Message}");
            callback?.Invoke(null);
            yield break;
        }

        yield return untypedHandle;

        if (untypedHandle.Status == AsyncOperationStatus.Succeeded)
        {
            callback?.Invoke(untypedHandle.Result);
            yield break;
        }

        Debug.LogError($"[ResourcesManager] Addressables location lookup failed: {key}");
        callback?.Invoke(null);
    }

    private IList<IResourceLocation> GetResourceLocations<T>(string key) where T : UnityEngine.Object
    {
        string cacheKey = GetLocationCacheKey<T>(key);

        if (loadedLocationHandles.TryGetValue(cacheKey, out AsyncOperationHandle cachedHandle) && cachedHandle.IsValid())
        {
            return cachedHandle.Convert<IList<IResourceLocation>>().Result;
        }

        AsyncOperationHandle<IList<IResourceLocation>> handle;
        try
        {
            handle = Addressables.LoadResourceLocationsAsync(key, typeof(T));
            handle.WaitForCompletion();
            loadedLocationHandles[cacheKey] = handle;
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[ResourcesManager] Addressables location lookup exception, falling back to Resources: {key}. {e.Message}");
            return null;
        }

        if (handle.Status == AsyncOperationStatus.Succeeded && handle.Result != null && handle.Result.Count > 0)
        {
            return handle.Result;
        }

        if (handle.IsValid())
        {
            Addressables.Release(handle);
        }

        loadedLocationHandles.Remove(cacheKey);

        AsyncOperationHandle<IList<IResourceLocation>> untypedHandle;
        try
        {
            untypedHandle = Addressables.LoadResourceLocationsAsync(key);
            untypedHandle.WaitForCompletion();
            loadedLocationHandles[cacheKey] = untypedHandle;
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[ResourcesManager] Addressables untyped location lookup exception, falling back to Resources: {key}. {e.Message}");
            return null;
        }

        if (untypedHandle.Status == AsyncOperationStatus.Succeeded)
        {
            return untypedHandle.Result;
        }

        Debug.LogError($"[ResourcesManager] Addressables location lookup failed: {key}");
        return null;
    }

    private AsyncOperationHandle<IList<T>> GetOrCreateAssetListHandle<T>(string key, IList<IResourceLocation> locations) where T : UnityEngine.Object
    {
        string cacheKey = GetListCacheKey<T>(key);

        if (loadedAssetListHandles.TryGetValue(cacheKey, out AsyncOperationHandle cachedHandle) && cachedHandle.IsValid())
        {
            return cachedHandle.Convert<IList<T>>();
        }

        AsyncOperationHandle<IList<T>> handle = Addressables.LoadAssetsAsync<T>(locations, null);
        loadedAssetListHandles[cacheKey] = handle;
        return handle;
    }

    private TResult CompleteHandle<TResult>(AsyncOperationHandle<TResult> handle, string key)
    {
        try
        {
            handle.WaitForCompletion();
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[ResourcesManager] Addressables load exception, falling back to Resources: {key}. {e.Message}");
            return default;
        }

        return GetHandleResult(handle, key);
    }

    private TResult GetHandleResult<TResult>(AsyncOperationHandle<TResult> handle, string key)
    {
        if (handle.Status == AsyncOperationStatus.Succeeded)
        {
            return handle.Result;
        }

        Debug.LogError($"[ResourcesManager] Addressables load failed: {key}");
        return default;
    }

    private T LoadFromResources<T>(string name) where T : UnityEngine.Object
    {
        T asset = Resources.Load<T>(name);
        if (asset == null)
        {
            Debug.LogError($"[ResourcesManager] Addressables and Resources load failed: {name}");
            return null;
        }

        return CreateReturnValue(asset);
    }

    private T[] LoadAllFromResources<T>(string key) where T : UnityEngine.Object
    {
        T[] assets = Resources.LoadAll<T>(key);
        if (assets == null || assets.Length == 0)
        {
            Debug.LogError($"[ResourcesManager] Addressables label and Resources folder not found or empty: {key}");
            return Array.Empty<T>();
        }

        return assets;
    }

    private T CreateReturnValue<T>(T asset) where T : UnityEngine.Object
    {
        if (asset is GameObject gameObject)
        {
            return GameObject.Instantiate(gameObject) as T;
        }

        return asset;
    }

    private string GetCacheKey<T>(string key)
    {
        return $"{typeof(T).FullName}:{key}";
    }

    private string GetListCacheKey<T>(string key)
    {
        return $"List<{typeof(T).FullName}>:{key}";
    }

    private string GetLocationCacheKey<T>(string key)
    {
        return $"Locations<{typeof(T).FullName}>:{key}";
    }
}
