using System;
using System.Collections.Generic;
using System.Globalization;
using System.Threading;
using UnityEngine;

namespace Dirichlet.Mediation
{
    /// <summary>
    /// Entry point of the Dirichlet Mediation Unity wrapper.
    /// Provides initialization, configuration, and shared helpers that are platform agnostic.
    /// </summary>
    public static class DirichletSdk
    {
        private static readonly IDirichletPlatformBridge Bridge = DirichletPlatformBridgeFactory.Create();

        private static SynchronizationContext unityContext;
        private static int unityThreadId;
        private static readonly Queue<Action> pendingActions = new Queue<Action>();
        private static readonly object pendingActionsLock = new object();
        private static UnityThreadPump pump;

        /// <summary>
        /// Indicates whether the mediation SDK was initialized successfully.
        /// </summary>
        public static bool IsInitialized { get; private set; }

        public static bool IsUnityThread => unityThreadId != 0 && Thread.CurrentThread.ManagedThreadId == unityThreadId;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void CaptureUnitySynchronizationContext()
        {
            unityThreadId = Thread.CurrentThread.ManagedThreadId;
            unityContext = SynchronizationContext.Current;
            EnsureUnityThreadPump();
        }

        // Internal thread dispatcher. Public SDK callbacks are already marshalled to Unity thread when needed.
        internal static void DispatchToUnityThread(Action action)
        {
            if (action == null)
            {
                return;
            }

            if (IsUnityThread)
            {
                try
                {
                    action();
                }
                catch (Exception ex)
                {
                    Debug.LogException(ex);
                }
                return;
            }

            if (unityContext != null)
            {
                unityContext.Post(_ =>
                {
                    try
                    {
                        action();
                    }
                    catch (Exception ex)
                    {
                        Debug.LogException(ex);
                    }
                }, null);
                return;
            }

            lock (pendingActionsLock)
            {
                pendingActions.Enqueue(action);
            }

            EnsureUnityThreadPump();
        }

        /// <summary>
        /// Initializes the mediation SDK using the aggregator-style configuration.
        /// </summary>
        public static void Init(
            DirichletAdConfig config,
            Action<DirichletInitResult> onSuccess = null,
            Action<DirichletError> onFailure = null)
        {
            InitializeInternal(config, onSuccess, onFailure);
        }

        private static void InitializeInternal(
            DirichletAdConfig config,
            Action<DirichletInitResult> onSuccess,
            Action<DirichletError> onFailure)
        {
            if (IsInitialized)
            {
                Debug.Log("[Dirichlet] Initialize called, but SDK is already initialized.");
                DispatchToUnityThread(() => onSuccess?.Invoke(DirichletInitResult.AlreadyInitialized()));
                return;
            }

            if (config == null)
            {
                DirichletAdManager.Clear();
                var error = new DirichletError("invalid_config", "DirichletAdConfig cannot be null");
                Debug.LogError(error);
                DispatchToUnityThread(() => onFailure?.Invoke(error));
                return;
            }

            var options = config.ToPlatformOptions();
            if (options == null)
            {
                DirichletAdManager.Clear();
                var error = new DirichletError("invalid_config", "Failed to map DirichletAdConfig to native options");
                Debug.LogError(error);
                DispatchToUnityThread(() => onFailure?.Invoke(error));
                return;
            }

            DirichletAdManager.ApplyConfig(config);

            void SuccessHandler(DirichletInitResult result)
            {
                DispatchToUnityThread(() =>
                {
                    IsInitialized = result?.Success ?? false;
                    onSuccess?.Invoke(result ?? DirichletInitResult.Ok("bridge_returned_null"));
                });
            }

            void FailureHandler(DirichletError error)
            {
                DispatchToUnityThread(() =>
                {
                    IsInitialized = false;
                    onFailure?.Invoke(error ?? new DirichletError("bridge_error", "Initialization failed"));
                });
            }

            Bridge.Initialize(options, SuccessHandler, FailureHandler);
        }

        /// <summary>
        /// Requests runtime permissions if the underlying SDK requires them.
        /// </summary>
        public static void RequestPermissionIfNecessary()
        {
            Bridge?.RequestPermissionIfNeeded();
        }

        [Obsolete("Use RequestPermissionIfNecessary() instead.")]
        public static void RequestPermissionIfNeeded()
        {
            RequestPermissionIfNecessary();
        }

        /// <summary>
        /// Returns the native SDK version if available.
        /// </summary>
        public static string GetVersion() => Bridge?.GetSdkVersion() ?? "unknown";

        [Obsolete("Use GetVersion() instead.")]
        public static string GetSdkVersion() => GetVersion();

        internal static IDirichletPlatformBridge GetBridge() => Bridge;

        private static void EnsureUnityThreadPump()
        {
            if (pump != null || !Application.isPlaying)
            {
                return;
            }

            if (!IsUnityThread)
            {
                unityContext?.Post(_ => EnsureUnityThreadPump(), null);
                return;
            }

            var host = new GameObject("DirichletUnityThreadPump")
            {
                hideFlags = HideFlags.HideAndDontSave
            };
            UnityEngine.Object.DontDestroyOnLoad(host);
            pump = host.AddComponent<UnityThreadPump>();
        }

        private sealed class UnityThreadPump : MonoBehaviour
        {
            private readonly List<Action> executionBuffer = new List<Action>(8);

            private void Awake()
            {
                UnityEngine.Object.DontDestroyOnLoad(gameObject);
            }

            private void Update()
            {
                executionBuffer.Clear();

                lock (pendingActionsLock)
                {
                    while (pendingActions.Count > 0)
                    {
                        executionBuffer.Add(pendingActions.Dequeue());
                    }
                }

                for (int i = 0; i < executionBuffer.Count; i++)
                {
                    var action = executionBuffer[i];
                    try
                    {
                        action?.Invoke();
                    }
                    catch (Exception ex)
                    {
                        Debug.LogException(ex);
                    }
                }
            }

            private void OnDestroy()
            {
                if (ReferenceEquals(pump, this))
                {
                    pump = null;
                }
            }
        }
    }

    #region Initialization data

    public enum DirichletAdNetworkType
    {
        Unknown = 0,
        Csj = 1,
        Gdt = 2,
        Tap = 3
    }

    [Serializable]
    /// <summary>
    /// 配置 Dirichlet Mediation SDK 的初始化参数
    /// </summary>
    public sealed class DirichletAdConfig
    {
        public long MediaId { get; }
        public string MediaName { get; }
        public string MediaKey { get; }
        public string GameChannel { get; }
        
        /// <summary>
        /// 子渠道标识
        /// </summary>
        /// <remarks>
        /// <para><b>Android:</b> 用于区分不同的子渠道来源</para>
        /// <para><b>iOS:</b> 此属性不使用，传递的值会被忽略</para>
        /// </remarks>
        public string SubChannel { get; }
        
        public bool DebugEnabled { get; }
        public string TapClientId { get; }
        public bool ShakeEnabled { get; }
        public string CustomConfigJson { get; }
        public string DataJson { get; }
        
        /// <summary>
        /// 控制是否允许访问广告标识符
        /// </summary>
        /// <remarks>
        /// <para><b>iOS:</b> 控制 IDFA 访问。设置为 true 时，iOS 14+ 会检查 ATT 授权状态后读取 IDFA。</para>
        /// <para><b>Android:</b> 此属性不使用，Android 使用 OAID/AAID 机制。</para>
        /// <para><b>默认值:</b> true</para>
        /// </remarks>
        public bool AllowIDFAAccess { get; }
        
        /// <summary>
        /// 外部配置的 aTags（JSON 格式）
        /// </summary>
        /// <remarks>
        /// <para>可选配置，用于传递额外的标签信息到广告 SDK。</para>
        /// <para><b>格式:</b> JSON 字符串，例如 {"key":"value"}</para>
        /// <para><b>平台支持:</b> iOS/Android 通用</para>
        /// </remarks>
        public string ATags { get; }
        
        internal string LegacyAppId { get; }

        private DirichletAdConfig(Builder builder)
        {
            MediaId = builder.mediaId;
            LegacyAppId = builder.legacyAppId;
            MediaName = builder.mediaName;
            MediaKey = builder.mediaKey;
            GameChannel = string.IsNullOrEmpty(builder.gameChannel) ? "default" : builder.gameChannel;
            SubChannel = builder.subChannel;
            DebugEnabled = builder.enableDebug;
            TapClientId = builder.tapClientId;
            ShakeEnabled = builder.shakeEnabled;
            CustomConfigJson = builder.customConfigJson;
            DataJson = builder.dataJson;
            AllowIDFAAccess = builder.allowIDFAAccess;
            ATags = builder.aTags;
        }

        public Builder ToBuilder()
        {
            return new Builder()
                .WithMediaId(MediaId)
                .WithMediaName(MediaName)
                .WithMediaKey(MediaKey)
                .WithGameChannel(GameChannel)
                .WithSubChannel(SubChannel)
                .EnableDebug(DebugEnabled)
                .WithTapClientId(TapClientId)
                .ShakeEnabled(ShakeEnabled)
                .WithCustomConfigJson(CustomConfigJson)
                .WithDataJson(DataJson)
                .WithAppId(LegacyAppId)
                .AllowIDFAAccess(AllowIDFAAccess)
                .WithATags(ATags);
        }

        internal DirichletPlatformInitOptions ToPlatformOptions()
        {
            return new DirichletPlatformInitOptions
            {
                MediaId = MediaId,
                AppId = LegacyAppId,
                Channel = GameChannel,
                SubChannel = SubChannel,
                EnableLog = DebugEnabled,
                MediaName = MediaName,
                MediaKey = MediaKey,
                TapClientId = TapClientId,
                ShakeEnabled = ShakeEnabled,
                CustomConfigJson = CustomConfigJson,
                DataJson = DataJson,
                AllowIDFAAccess = AllowIDFAAccess,
                ATags = ATags
            };
        }

        public override string ToString()
        {
            return $"DirichletAdConfig(MediaId={MediaId}, MediaName={MediaName}, GameChannel={GameChannel}, SubChannel={SubChannel}, DebugEnabled={DebugEnabled}, TapClientId={(string.IsNullOrEmpty(TapClientId) ? "<null>" : TapClientId)}, ShakeEnabled={ShakeEnabled})";
        }

        public sealed class Builder
        {
            internal long mediaId;
            internal string mediaName = "Unity Dirichlet Demo";
            internal string mediaKey;
            internal string gameChannel = "default";
            internal string subChannel;
            internal bool enableDebug = true;
            internal string tapClientId;
            internal bool shakeEnabled = true;
            internal string customConfigJson;
            internal string dataJson;
            internal string legacyAppId;
            internal bool allowIDFAAccess = true;
            internal string aTags;

            public Builder WithMediaId(long value)
            {
                mediaId = value;
                return this;
            }

            public Builder WithAppId(string appId)
            {
                legacyAppId = appId;
                if (!string.IsNullOrEmpty(appId) && long.TryParse(appId, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed) && parsed > 0)
                {
                    mediaId = parsed;
                }
                return this;
            }

            public Builder WithMediaName(string value)
            {
                mediaName = value;
                return this;
            }

            public Builder WithMediaKey(string value)
            {
                mediaKey = value;
                return this;
            }

            public Builder WithGameChannel(string value)
            {
                gameChannel = value;
                return this;
            }

            /// <summary>
            /// 设置子渠道标识（Android only，iOS 会忽略此值）
            /// </summary>
            public Builder WithSubChannel(string value)
            {
                subChannel = value;
                return this;
            }

            public Builder EnableDebug(bool enabled)
            {
                enableDebug = enabled;
                return this;
            }

            public Builder WithTapClientId(string value)
            {
                tapClientId = value;
                return this;
            }

            public Builder ShakeEnabled(bool enabled)
            {
                shakeEnabled = enabled;
                return this;
            }

            public Builder WithCustomConfigJson(string json)
            {
                customConfigJson = json;
                return this;
            }

            public Builder WithDataJson(string json)
            {
                dataJson = json;
                return this;
            }

            /// <summary>
            /// 设置是否允许访问广告标识符（iOS: IDFA，Android: 忽略）
            /// </summary>
            /// <param name="enabled">true 表示允许访问（默认），false 表示禁止访问</param>
            public Builder AllowIDFAAccess(bool enabled)
            {
                allowIDFAAccess = enabled;
                return this;
            }

            /// <summary>
            /// 设置外部 aTags（JSON 格式，两平台通用）
            /// </summary>
            /// <param name="value">JSON 字符串，例如 {"key":"value"}</param>
            public Builder WithATags(string value)
            {
                aTags = value;
                return this;
            }

            public DirichletAdConfig Build()
            {
                return new DirichletAdConfig(this);
            }
        }
    }

    public sealed class DirichletInitResult
    {
        public bool Success { get; }
        public string Message { get; }

        private DirichletInitResult(bool success, string message)
        {
            Success = success;
            Message = message;
        }

        public static DirichletInitResult Ok(string message = null) => new DirichletInitResult(true, message);
        public static DirichletInitResult Failed(string message) => new DirichletInitResult(false, message);
        public static DirichletInitResult AlreadyInitialized() => new DirichletInitResult(true, "already_initialized");
    }

    /// <summary>
    /// 平台桥接层使用的初始化选项（内部类）
    /// </summary>
    internal sealed class DirichletPlatformInitOptions
    {
        public string AppId { get; set; }
        public long MediaId { get; set; }
        public string Channel { get; set; }
        
        /// <summary>
        /// 子渠道标识（Android only）
        /// </summary>
        public string SubChannel { get; set; }
        
        public bool EnableLog { get; set; }
        public string MediaName { get; set; }
        public string MediaKey { get; set; }
        public string TapClientId { get; set; }
        public bool ShakeEnabled { get; set; }
        public string CustomConfigJson { get; set; }
        public string DataJson { get; set; }
        
        /// <summary>
        /// 控制 IDFA 访问（iOS only）
        /// </summary>
        public bool AllowIDFAAccess { get; set; }
        
        /// <summary>
        /// 外部 aTags JSON（通用）
        /// </summary>
        public string ATags { get; set; }

        internal string GetAppIdString()
        {
            if (MediaId > 0)
            {
                return MediaId.ToString(CultureInfo.InvariantCulture);
            }

            return AppId ?? string.Empty;
        }

        public override string ToString()
        {
            return $"DirichletPlatformInitOptions(MediaId={MediaId}, AppId={AppId}, Channel={Channel}, SubChannel={SubChannel}, EnableLog={EnableLog}, MediaName={MediaName}, MediaKey={(string.IsNullOrEmpty(MediaKey) ? "<null>" : "***")}, TapClientId={(string.IsNullOrEmpty(TapClientId) ? "<null>" : TapClientId)}, ShakeEnabled={ShakeEnabled}, AllowIDFAAccess={AllowIDFAAccess}, ATags={(string.IsNullOrEmpty(ATags) ? "<null>" : ATags)})";
        }
    }

    public sealed class DirichletError
    {
        public string Code { get; }
        public string Message { get; }
        public string Adapter { get; }
        public string Network { get; }

        public DirichletError(string code, string message, string adapter = null, string network = null)
        {
            Code = string.IsNullOrEmpty(code) ? "unknown" : code;
            Message = message ?? string.Empty;
            Adapter = adapter;
            Network = network;
        }

        public override string ToString()
        {
            return $"DirichletError(Code={Code}, Message={Message}, Adapter={Adapter}, Network={Network})";
        }
    }

    #endregion

    #region Platform bridge plumbing

    internal interface IDirichletPlatformBridge
    {
        void Initialize(DirichletPlatformInitOptions options, Action<DirichletInitResult> onSuccess, Action<DirichletError> onFailure);
        void LoadRewardVideoAd(DirichletAdRequest request, Action<DirichletPlatformAdHandle> onSuccess, Action<DirichletError> onFailure);
        bool ShowRewardVideoAd(DirichletPlatformAdHandle handle);

        void LoadInterstitialAd(DirichletAdRequest request, Action<DirichletPlatformAdHandle> onSuccess, Action<DirichletError> onFailure);
        bool ShowInterstitialAd(DirichletPlatformAdHandle handle);

        void LoadBannerAd(DirichletAdRequest request, Action<DirichletPlatformAdHandle> onSuccess, Action<DirichletError> onFailure);
        bool ShowBannerAd(DirichletPlatformAdHandle handle, DirichletAdShowOptions options);

        void LoadSplashAd(DirichletAdRequest request, Action<DirichletPlatformAdHandle> onSuccess, Action<DirichletError> onFailure);
        bool ShowSplashAd(DirichletPlatformAdHandle handle, DirichletAdShowOptions options);

        void DestroyAd(DirichletPlatformAdHandle handle);
        bool IsAdValid(DirichletPlatformAdHandle handle);
        void RequestPermissionIfNeeded();
        string GetSdkVersion();

        /// <summary>
        /// Shows a reward video ad with automatic load-and-show logic.
        /// Android only - iOS will call onFailure with not_supported error.
        /// </summary>
        void ShowRewardVideoAutoAd(DirichletAdRequest request, IDirichletRewardVideoAutoAdListener listener);
    }

    internal static class DirichletPlatformBridgeFactory
    {
        private static IDirichletPlatformBridge instance;

        internal static IDirichletPlatformBridge Create()
        {
            if (instance != null)
            {
                return instance;
            }

#if UNITY_ANDROID && !UNITY_EDITOR
            instance = new AndroidDirichletBridge();
#elif UNITY_IOS && !UNITY_EDITOR
            instance = new IOSDirichletBridge();
#else
            instance = new NoopDirichletBridge();
#endif
            return instance;
        }

        internal static void OverrideForTesting(IDirichletPlatformBridge customBridge)
        {
            instance = customBridge;
        }
    }

#if UNITY_ANDROID && !UNITY_EDITOR
    internal sealed class AndroidDirichletBridge : IDirichletPlatformBridge
    {
        private const string BridgeClassName = "com.dirichlet.unity.DirichletUnityBridge";
        private static AndroidJavaClass cachedBridgeClass;

        private static AndroidJavaClass BridgeClass
        {
            get
            {
                if (cachedBridgeClass == null)
                {
                    cachedBridgeClass = new AndroidJavaClass(BridgeClassName);
                }
                return cachedBridgeClass;
            }
        }

        private readonly Dictionary<string, AndroidLoadCallback> loadCallbacks = new Dictionary<string, AndroidLoadCallback>();
        private readonly object loadCallbacksLock = new object();

        public void Initialize(DirichletPlatformInitOptions options, Action<DirichletInitResult> onSuccess, Action<DirichletError> onFailure)
        {
            if (options == null)
            {
                DirichletSdk.DispatchToUnityThread(() => onFailure?.Invoke(new DirichletError("android_invalid_options", "Initialization options cannot be null")));
                return;
            }

            try
            {
                var dataPayload = !string.IsNullOrEmpty(options.DataJson)
                    ? options.DataJson
                    : options.CustomConfigJson;

                // Do not block Unity thread on Android init. The Java bridge enforces a timeout and reports via callback.
                var callback = new AndroidInitCallback(
                    () => onSuccess?.Invoke(DirichletInitResult.Ok("android_bridge")),
                    onFailure);

                BridgeClass.CallStatic(
                    "initializeAsync",
                    options.GetAppIdString(),
                    options.Channel ?? string.Empty,
                    options.SubChannel ?? string.Empty,
                    options.EnableLog,
                    options.MediaName ?? string.Empty,
                    options.MediaKey ?? string.Empty,
                    options.TapClientId ?? string.Empty,
                    dataPayload ?? string.Empty,
                    options.ShakeEnabled,
                    callback);
            }
            catch (Exception ex)
            {
                Debug.LogException(ex);
                DirichletSdk.DispatchToUnityThread(() => onFailure?.Invoke(new DirichletError("android_exception", ex.Message)));
            }
        }

        private sealed class AndroidInitCallback : AndroidJavaProxy
        {
            // Reuse the existing Java callback interface to avoid adding more bridge-only types.
            private const string ListenerInterface = "com.dirichlet.unity.DirichletUnityBridge$LoadListener";

            private readonly Action success;
            private readonly Action<DirichletError> failure;

            public AndroidInitCallback(Action success, Action<DirichletError> failure)
                : base(ListenerInterface)
            {
                this.success = success;
                this.failure = failure;
            }

            // Called from Java (case-sensitive method name).
            public void onSuccess()
            {
                DirichletSdk.DispatchToUnityThread(() => success?.Invoke());
            }

            // Called from Java (case-sensitive method name).
            public void onError(string code, string message)
            {
                var errorCode = string.IsNullOrEmpty(code) ? "android_init_failed" : code;
                DirichletSdk.DispatchToUnityThread(() => failure?.Invoke(new DirichletError(errorCode, message ?? string.Empty)));
            }
        }

        public void RequestPermissionIfNeeded()
        {
            try
            {
                BridgeClass.CallStatic("requestPermissionIfNeeded");
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[Dirichlet][Android] requestPermissionIfNeeded failed: {ex.Message}");
            }
        }

        public string GetSdkVersion()
        {
            try
            {
                return BridgeClass.CallStatic<string>("getSdkVersion") ?? "android-unknown";
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[Dirichlet][Android] getSdkVersion failed: {ex.Message}");
                return "android-error";
            }
        }

        public void LoadRewardVideoAd(DirichletAdRequest request, Action<DirichletPlatformAdHandle> onSuccess, Action<DirichletError> onFailure)
        {
            LoadAdInternal(DirichletAdType.RewardVideo, request, onSuccess, onFailure);
        }

        public bool ShowRewardVideoAd(DirichletPlatformAdHandle handle)
        {
            return ShowAdInternal(handle, null);
        }

        public void LoadInterstitialAd(DirichletAdRequest request, Action<DirichletPlatformAdHandle> onSuccess, Action<DirichletError> onFailure)
        {
            LoadAdInternal(DirichletAdType.Interstitial, request, onSuccess, onFailure);
        }

        public bool ShowInterstitialAd(DirichletPlatformAdHandle handle)
        {
            return ShowAdInternal(handle, null);
        }

        public void LoadBannerAd(DirichletAdRequest request, Action<DirichletPlatformAdHandle> onSuccess, Action<DirichletError> onFailure)
        {
            LoadAdInternal(DirichletAdType.Banner, request, onSuccess, onFailure);
        }

        public bool ShowBannerAd(DirichletPlatformAdHandle handle, DirichletAdShowOptions options)
        {
            return ShowAdInternal(handle, options);
        }

        public void LoadSplashAd(DirichletAdRequest request, Action<DirichletPlatformAdHandle> onSuccess, Action<DirichletError> onFailure)
        {
            LoadAdInternal(DirichletAdType.Splash, request, onSuccess, onFailure);
        }

        public bool ShowSplashAd(DirichletPlatformAdHandle handle, DirichletAdShowOptions options)
        {
            return ShowAdInternal(handle, options);
        }

        public void DestroyAd(DirichletPlatformAdHandle handle)
        {
            DestroyAdInternal(handle);
        }

        public bool IsAdValid(DirichletPlatformAdHandle handle)
        {
            if (handle == null || string.IsNullOrEmpty(handle.DebugId))
            {
                return false;
            }

            try
            {
                return BridgeClass.CallStatic<bool>("isAdValid", handle.DebugId);
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[DirichletMediation][Android] IsAdValid failed: {ex.Message}");
                return false;
            }
        }

        private void LoadAdInternal(DirichletAdType adType, DirichletAdRequest request, Action<DirichletPlatformAdHandle> onSuccess, Action<DirichletError> onFailure)
        {
            if (request == null)
            {
                DirichletSdk.DispatchToUnityThread(() => onFailure?.Invoke(new DirichletError("invalid_request", "Request cannot be null")));
                return;
            }

            try
            {
                var payload = request.ToBridgePayload();
                var callback = new AndroidLoadCallback(this, null, () =>
                {
                    // Handle will be set in callback's onSuccess
                }, onFailure);

                string handleId;
                using (var extras = BuildJsonObject(payload))
                {
                    // Use the new direct load methods that match native SDK pattern
                    // extras already contains space_id from request.ToBridgePayload()
                    switch (adType)
                    {
                        case DirichletAdType.RewardVideo:
                            handleId = BridgeClass.CallStatic<string>("loadRewardVideoAd", extras, callback);
                            break;
                        case DirichletAdType.Interstitial:
                            handleId = BridgeClass.CallStatic<string>("loadInterstitialAd", extras, callback);
                            break;
                        case DirichletAdType.Banner:
                            handleId = BridgeClass.CallStatic<string>("loadBannerAd", extras, callback);
                            break;
                        case DirichletAdType.Splash:
                            handleId = BridgeClass.CallStatic<string>("loadSplashAd", extras, callback);
                            break;
                        default:
                            DirichletSdk.DispatchToUnityThread(() => onFailure?.Invoke(new DirichletError("unsupported_type", $"Unsupported ad type: {adType}")));
                            return;
                    }
                }

                if (string.IsNullOrEmpty(handleId))
                {
                    DirichletSdk.DispatchToUnityThread(() => onFailure?.Invoke(new DirichletError("invalid_handle", "Bridge returned null handle")));
                    return;
                }

                // Create handle - simple wrapper around handle string
                var handle = DirichletPlatformAdHandle.FromNative(handleId);
                callback.SetHandle(handle);
                callback.SetSuccessCallback(() => onSuccess?.Invoke(handle));

                lock (loadCallbacksLock)
                {
                    loadCallbacks[handleId] = callback;
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[Dirichlet][Android] LoadAd failed: {ex.Message}");
                DirichletSdk.DispatchToUnityThread(() => onFailure?.Invoke(new DirichletError("android_exception", ex.Message)));
            }
        }

        private bool ShowAdInternal(DirichletPlatformAdHandle handle, DirichletAdShowOptions options)
        {
            try
            {
                var payload = options?.ToBridgePayload();

                using (var extras = BuildJsonObject(payload))
                {
                    return BridgeClass.CallStatic<bool>("showAd", handle.DebugId, extras);
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[Dirichlet][Android] ShowAd failed: {ex.Message}");
                return false;
            }
        }

        private void DestroyAdInternal(DirichletPlatformAdHandle handle)
        {
            try
            {
                BridgeClass.CallStatic("destroyAd", handle.DebugId);
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[Dirichlet][Android] DestroyAd failed: {ex.Message}");
            }
            finally
            {
                RemoveLoadCallback(handle?.DebugId);
            }
        }

        private AndroidJavaObject BuildJsonObject(Dictionary<string, object> dictionary)
        {
            if (dictionary == null || dictionary.Count == 0)
            {
                return null;
            }

            AndroidJavaObject json = null;

            try
            {
                json = new AndroidJavaObject("org.json.JSONObject");

                foreach (var kv in dictionary)
                {
                    if (string.IsNullOrEmpty(kv.Key))
                    {
                        continue;
                    }

                    var value = kv.Value;
                    if (value == null)
                    {
                        continue;
                    }

                    try
                    {
                        switch (value)
                        {
                            case bool boolValue:
                                json.Call<AndroidJavaObject>("put", kv.Key, boolValue);
                                break;
                            case int intValue:
                                json.Call<AndroidJavaObject>("put", kv.Key, intValue);
                                break;
                            case long longValue:
                                json.Call<AndroidJavaObject>("put", kv.Key, longValue);
                                break;
                            case float floatValue:
                                json.Call<AndroidJavaObject>("put", kv.Key, (double)floatValue);
                                break;
                            case double doubleValue:
                                json.Call<AndroidJavaObject>("put", kv.Key, doubleValue);
                                break;
                            case Enum enumValue:
                                json.Call<AndroidJavaObject>("put", kv.Key, Convert.ToInt32(enumValue, CultureInfo.InvariantCulture));
                                break;
                            default:
                                json.Call<AndroidJavaObject>("put", kv.Key, value.ToString());
                                break;
                        }
                    }
                    catch (Exception putEx)
                    {
                        Debug.LogWarning($"[Dirichlet][Android] Failed to add extra {kv.Key}: {putEx.Message}");
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[Dirichlet][Android] Failed to build json object: {ex.Message}");
            }

            return json;
        }

        private void RemoveLoadCallback(string handleId)
        {
            if (string.IsNullOrEmpty(handleId))
            {
                return;
            }

            lock (loadCallbacksLock)
            {
                loadCallbacks.Remove(handleId);
            }
        }

        private sealed class AndroidLoadCallback : AndroidJavaProxy
        {
            private readonly AndroidDirichletBridge owner;
            private string handleId;
            private Action success;
            private readonly Action<DirichletError> failure;

            public AndroidLoadCallback(AndroidDirichletBridge owner, string handleId, Action success, Action<DirichletError> failure)
                : base("com.dirichlet.unity.DirichletUnityBridge$LoadListener")
            {
                this.owner = owner;
                this.handleId = handleId;
                this.success = success;
                this.failure = failure;
            }

            public void SetHandle(DirichletPlatformAdHandle handle)
            {
                if (handle != null)
                {
                    handleId = handle.DebugId;
                }
            }

            public void SetSuccessCallback(Action callback)
            {
                success = callback;
            }

            public void onSuccess()
            {
                owner.RemoveLoadCallback(handleId);
                DirichletSdk.DispatchToUnityThread(() => success?.Invoke());
            }

            public void onError(string code, string message)
            {
                owner.RemoveLoadCallback(handleId);
                var errorCode = string.IsNullOrEmpty(code) ? "android_error" : code;
                DirichletSdk.DispatchToUnityThread(() => failure?.Invoke(new DirichletError(errorCode, message ?? string.Empty)));
            }
        }

        public void ShowRewardVideoAutoAd(DirichletAdRequest request, IDirichletRewardVideoAutoAdListener listener)
        {
            if (request == null)
            {
                DirichletSdk.DispatchToUnityThread(() => listener?.OnError(new DirichletError("invalid_request", "Request cannot be null")));
                return;
            }

            try
            {
                var payload = request.ToBridgePayload();
                var callback = new AndroidRewardVideoAutoAdCallback(listener);

                using (var extras = BuildJsonObject(payload))
                {
                    BridgeClass.CallStatic("showRewardVideoAutoAd", extras, callback);
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[Dirichlet][Android] ShowRewardVideoAutoAd failed: {ex.Message}");
                DirichletSdk.DispatchToUnityThread(() => listener?.OnError(new DirichletError("android_exception", ex.Message)));
            }
        }

        private sealed class AndroidRewardVideoAutoAdCallback : AndroidJavaProxy
        {
            private readonly IDirichletRewardVideoAutoAdListener listener;

            public AndroidRewardVideoAutoAdCallback(IDirichletRewardVideoAutoAdListener listener)
                : base("com.dirichlet.unity.DirichletUnityBridge$RewardVideoAutoAdListener")
            {
                this.listener = listener;
            }

            public void onError(string code, string message)
            {
                var errorCode = string.IsNullOrEmpty(code) ? "android_error" : code;
                DirichletSdk.DispatchToUnityThread(() => listener?.OnError(new DirichletError(errorCode, message ?? string.Empty)));
            }

            public void onAdShow()
            {
                DirichletSdk.DispatchToUnityThread(() => listener?.OnAdShow());
            }

            public void onAdClose()
            {
                DirichletSdk.DispatchToUnityThread(() => listener?.OnAdClose());
            }

            public void onRewardVerify(bool rewardVerify, int rewardAmount, string rewardName, int code, string msg)
            {
                var args = new DirichletRewardVerificationEventArgs(rewardVerify, rewardAmount, rewardName ?? string.Empty, code, msg ?? string.Empty);
                DirichletSdk.DispatchToUnityThread(() => listener?.OnRewardVerify(args));
            }

            public void onAdClick()
            {
                DirichletSdk.DispatchToUnityThread(() => listener?.OnAdClick());
            }
        }
    }
#elif UNITY_IOS && !UNITY_EDITOR
    internal sealed class IOSDirichletBridge : IDirichletPlatformBridge
    {
        [System.Runtime.InteropServices.DllImport("__Internal")]
        private static extern bool DirichletMediationUnityBridge_Initialize(
            string mediaId, string mediaKey, bool enableLog, string mediaName,
            string gameChannel, bool shakeEnabled, bool allowIDFAAccess, string aTags);

        [System.Runtime.InteropServices.DllImport("__Internal")]
        private static extern void DirichletMediationUnityBridge_RequestPermissionIfNeeded();

        [System.Runtime.InteropServices.DllImport("__Internal")]
        private static extern string DirichletMediationUnityBridge_GetSdkVersion();

        [System.Runtime.InteropServices.DllImport("__Internal")]
        private static extern string DirichletMediationUnityBridge_LoadRewardVideoAd(long spaceId, string extras);

        [System.Runtime.InteropServices.DllImport("__Internal")]
        private static extern string DirichletMediationUnityBridge_LoadInterstitialAd(long spaceId, string extras);

        [System.Runtime.InteropServices.DllImport("__Internal")]
        private static extern string DirichletMediationUnityBridge_LoadBannerAd(long spaceId, string extras);

        [System.Runtime.InteropServices.DllImport("__Internal")]
        private static extern string DirichletMediationUnityBridge_LoadSplashAd(long spaceId, string extras);

        [System.Runtime.InteropServices.DllImport("__Internal")]
        private static extern bool DirichletMediationUnityBridge_ShowAd(string handleId, string extras);

        [System.Runtime.InteropServices.DllImport("__Internal")]
        private static extern void DirichletMediationUnityBridge_DestroyAd(string handleId);

        [System.Runtime.InteropServices.DllImport("__Internal")]
        private static extern bool DirichletMediationUnityBridge_IsAdValid(string handleId);

        private readonly Dictionary<string, IOSLoadCallback> loadCallbacks = new Dictionary<string, IOSLoadCallback>();
        private readonly object loadCallbacksLock = new object();
        private static bool loadCallbackReceiverInitialized;
        private static bool initCallbackReceiverInitialized;
        private static readonly object initCallbackLock = new object();
        private static Action<DirichletInitResult> pendingInitSuccess;
        private static Action<DirichletError> pendingInitFailure;

        public void Initialize(DirichletPlatformInitOptions options, Action<DirichletInitResult> onSuccess, Action<DirichletError> onFailure)
        {
            if (options == null)
            {
                DirichletSdk.DispatchToUnityThread(() => onFailure?.Invoke(new DirichletError("ios_invalid_options", "Initialization options cannot be null")));
                return;
            }

            // Register callbacks for async result
            lock (initCallbackLock)
            {
                pendingInitSuccess = onSuccess;
                pendingInitFailure = onFailure;
            }

            EnsureInitCallbackReceiver();

            try
            {
                // iOS Mediation SDK uses async callback (aligned with Ad Unity implementation)
                var started = DirichletMediationUnityBridge_Initialize(
                    options.GetAppIdString(),
                    options.MediaKey ?? string.Empty,
                    options.EnableLog,
                    options.MediaName ?? string.Empty,
                    options.Channel ?? string.Empty,
                    options.ShakeEnabled,
                    options.AllowIDFAAccess,
                    options.ATags ?? string.Empty);

                if (!started)
                {
                    lock (initCallbackLock)
                    {
                        pendingInitSuccess = null;
                        pendingInitFailure = null;
                    }

                    DirichletSdk.DispatchToUnityThread(() =>
                        onFailure?.Invoke(new DirichletError("ios_init_rejected", "Initialization could not be started")));
                }
            }
            catch (Exception ex)
            {
                Debug.LogException(ex);
                lock (initCallbackLock)
                {
                    pendingInitSuccess = null;
                    pendingInitFailure = null;
                }

                DirichletSdk.DispatchToUnityThread(() =>
                    onFailure?.Invoke(new DirichletError("ios_exception", ex.Message)));
            }
        }

        public void RequestPermissionIfNeeded()
        {
            try
            {
                DirichletMediationUnityBridge_RequestPermissionIfNeeded();
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[DirichletMediation][iOS] RequestPermissionIfNeeded failed: {ex.Message}");
            }
        }

        public string GetSdkVersion()
        {
            try
            {
                return DirichletMediationUnityBridge_GetSdkVersion() ?? "ios-unknown";
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[DirichletMediation][iOS] GetSdkVersion failed: {ex.Message}");
                return "ios-error";
            }
        }

        public void LoadRewardVideoAd(DirichletAdRequest request, Action<DirichletPlatformAdHandle> onSuccess, Action<DirichletError> onFailure)
        {
            LoadAdInternal(DirichletAdType.RewardVideo, request, onSuccess, onFailure);
        }

        public bool ShowRewardVideoAd(DirichletPlatformAdHandle handle)
        {
            return ShowAdInternal(handle, null);
        }

        public void LoadInterstitialAd(DirichletAdRequest request, Action<DirichletPlatformAdHandle> onSuccess, Action<DirichletError> onFailure)
        {
            LoadAdInternal(DirichletAdType.Interstitial, request, onSuccess, onFailure);
        }

        public bool ShowInterstitialAd(DirichletPlatformAdHandle handle)
        {
            return ShowAdInternal(handle, null);
        }

        public void LoadBannerAd(DirichletAdRequest request, Action<DirichletPlatformAdHandle> onSuccess, Action<DirichletError> onFailure)
        {
            LoadAdInternal(DirichletAdType.Banner, request, onSuccess, onFailure);
        }

        public bool ShowBannerAd(DirichletPlatformAdHandle handle, DirichletAdShowOptions options)
        {
            return ShowAdInternal(handle, options);
        }

        public void LoadSplashAd(DirichletAdRequest request, Action<DirichletPlatformAdHandle> onSuccess, Action<DirichletError> onFailure)
        {
            LoadAdInternal(DirichletAdType.Splash, request, onSuccess, onFailure);
        }

        public bool ShowSplashAd(DirichletPlatformAdHandle handle, DirichletAdShowOptions options)
        {
            return ShowAdInternal(handle, options);
        }

        public void DestroyAd(DirichletPlatformAdHandle handle)
        {
            DestroyAdInternal(handle);
        }

        public bool IsAdValid(DirichletPlatformAdHandle handle)
        {
            if (handle == null || string.IsNullOrEmpty(handle.DebugId))
            {
                return false;
            }

            try
            {
                return DirichletMediationUnityBridge_IsAdValid(handle.DebugId);
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[DirichletMediation][iOS] IsAdValid failed: {ex.Message}");
                return false;
            }
        }

        private void LoadAdInternal(DirichletAdType adType, DirichletAdRequest request, Action<DirichletPlatformAdHandle> onSuccess, Action<DirichletError> onFailure)
        {
            if (request == null)
            {
                DirichletSdk.DispatchToUnityThread(() => onFailure?.Invoke(new DirichletError("invalid_request", "Request cannot be null")));
                return;
            }

            try
            {
                var payload = request.ToBridgePayload();
                var callback = new IOSLoadCallback(this, null, () => { }, onFailure);

                string handleId;
                var extrasJson = BuildJsonString(payload);

                switch (adType)
                {
                    case DirichletAdType.RewardVideo:
                        handleId = DirichletMediationUnityBridge_LoadRewardVideoAd(request.SpaceId, extrasJson);
                        break;
                    case DirichletAdType.Interstitial:
                        handleId = DirichletMediationUnityBridge_LoadInterstitialAd(request.SpaceId, extrasJson);
                        break;
                    case DirichletAdType.Banner:
                        handleId = DirichletMediationUnityBridge_LoadBannerAd(request.SpaceId, extrasJson);
                        break;
                    case DirichletAdType.Splash:
                        handleId = DirichletMediationUnityBridge_LoadSplashAd(request.SpaceId, extrasJson);
                        break;
                    default:
                        DirichletSdk.DispatchToUnityThread(() => onFailure?.Invoke(new DirichletError("unsupported_type", $"Unsupported ad type: {adType}")));
                        return;
                }

                if (string.IsNullOrEmpty(handleId))
                {
                    DirichletSdk.DispatchToUnityThread(() => onFailure?.Invoke(new DirichletError("invalid_handle", "Bridge returned null handle")));
                    return;
                }

                // Create handle and setup callback
                var handle = DirichletPlatformAdHandle.FromNative(handleId);
                callback.SetHandle(handle);
                callback.SetSuccessCallback(() => onSuccess?.Invoke(handle));

                lock (loadCallbacksLock)
                {
                    loadCallbacks[handleId] = callback;
                }

                // Ensure load callback receiver is initialized
                EnsureLoadCallbackReceiver();
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[DirichletMediation][iOS] LoadAd failed: {ex.Message}");
                DirichletSdk.DispatchToUnityThread(() => onFailure?.Invoke(new DirichletError("ios_exception", ex.Message)));
            }
        }

        private bool ShowAdInternal(DirichletPlatformAdHandle handle, DirichletAdShowOptions options)
        {
            try
            {
                var payload = options?.ToBridgePayload();
                var extrasJson = BuildJsonString(payload);
                return DirichletMediationUnityBridge_ShowAd(handle.DebugId, extrasJson);
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[DirichletMediation][iOS] ShowAd failed: {ex.Message}");
                return false;
            }
        }

        private void DestroyAdInternal(DirichletPlatformAdHandle handle)
        {
            try
            {
                DirichletMediationUnityBridge_DestroyAd(handle.DebugId);
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[DirichletMediation][iOS] DestroyAd failed: {ex.Message}");
            }
            finally
            {
                RemoveLoadCallback(handle?.DebugId);
            }
        }

        private string BuildJsonString(Dictionary<string, object> dictionary)
        {
            if (dictionary == null || dictionary.Count == 0)
            {
                return string.Empty;
            }

            try
            {
                var jsonBuilder = new System.Text.StringBuilder();
                jsonBuilder.Append("{");
                var first = true;

                foreach (var kv in dictionary)
                {
                    if (string.IsNullOrEmpty(kv.Key) || kv.Value == null)
                    {
                        continue;
                    }

                    if (!first)
                    {
                        jsonBuilder.Append(",");
                    }
                    first = false;

                    jsonBuilder.Append($"\"{kv.Key}\":");

                    if (kv.Value is string)
                    {
                        jsonBuilder.Append($"\"{kv.Value}\"");
                    }
                    else if (kv.Value is bool)
                    {
                        jsonBuilder.Append(((bool)kv.Value) ? "true" : "false");
                    }
                    else
                    {
                        jsonBuilder.Append(kv.Value.ToString());
                    }
                }

                jsonBuilder.Append("}");
                return jsonBuilder.ToString();
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[DirichletMediation][iOS] Failed to build json string: {ex.Message}");
                return string.Empty;
            }
        }

        private void RemoveLoadCallback(string handleId)
        {
            if (string.IsNullOrEmpty(handleId))
            {
                return;
            }

            lock (loadCallbacksLock)
            {
                loadCallbacks.Remove(handleId);
            }
        }

        private void EnsureLoadCallbackReceiver()
        {
            if (!DirichletSdk.IsUnityThread)
            {
                DirichletSdk.DispatchToUnityThread(EnsureLoadCallbackReceiver);
                return;
            }

            const string receiverName = "DirichletMediationIOSLoadCallbackReceiver";
            
            if (loadCallbackReceiverInitialized)
            {
                var existing = GameObject.Find(receiverName);
                if (existing != null)
                {
                    return;
                }
                Debug.LogWarning("[DirichletMediation][iOS] LoadCallbackReceiver was destroyed, recreating...");
                loadCallbackReceiverInitialized = false;
            }

            var host = new GameObject(receiverName)
            {
                hideFlags = HideFlags.HideAndDontSave
            };
            UnityEngine.Object.DontDestroyOnLoad(host);
            var receiver = host.AddComponent<IOSLoadCallbackReceiver>();
            receiver.bridge = this;

            loadCallbackReceiverInitialized = true;
        }

        private void EnsureInitCallbackReceiver()
        {
            if (!DirichletSdk.IsUnityThread)
            {
                DirichletSdk.DispatchToUnityThread(EnsureInitCallbackReceiver);
                return;
            }

            const string receiverName = "DirichletMediationIOSInitCallbackReceiver";
            if (initCallbackReceiverInitialized)
            {
                var existing = GameObject.Find(receiverName);
                if (existing != null)
                {
                    return;
                }

                Debug.LogWarning("[DirichletMediation][iOS] InitCallbackReceiver was destroyed, recreating...");
                initCallbackReceiverInitialized = false;
            }

            var host = new GameObject(receiverName)
            {
                hideFlags = HideFlags.HideAndDontSave
            };
            UnityEngine.Object.DontDestroyOnLoad(host);
            var receiver = host.AddComponent<IOSInitCallbackReceiver>();
            receiver.bridge = this;

            initCallbackReceiverInitialized = true;
        }

        internal void HandleLoadCallback(string payload)
        {
            if (string.IsNullOrEmpty(payload))
            {
                return;
            }

            try
            {
                var message = JsonUtility.FromJson<LoadCallbackPayload>(payload);
                if (message == null || string.IsNullOrEmpty(message.handle))
                {
                    return;
                }

                IOSLoadCallback callback;
                lock (loadCallbacksLock)
                {
                    if (!loadCallbacks.TryGetValue(message.handle, out callback))
                    {
                        Debug.LogWarning($"[DirichletMediation][iOS] No callback found for handle: {message.handle}");
                        return;
                    }
                }

                if (message.eventName == "load_success")
                {
                    callback.OnSuccess();
                }
                else if (message.eventName == "load_error")
                {
                    var code = message.data?.code.ToString() ?? "unknown";
                    var msg = message.data?.message ?? "Unknown error";
                    callback.OnError(code, msg);
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[DirichletMediation][iOS] Failed to handle load callback: {ex.Message}\n{payload}");
            }
        }

        internal void HandleInitCallback(string payload)
        {
            if (string.IsNullOrEmpty(payload))
            {
                return;
            }

            InitCallbackPayload message = null;
            try
            {
                message = JsonUtility.FromJson<InitCallbackPayload>(payload);
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[DirichletMediation][iOS] Failed to parse init callback: {ex.Message}\n{payload}");
            }

            var success = message?.success ?? false;
            var data = message?.data;
            var code = data?.code ?? -1;
            var domain = data?.domain;
            var description = data?.message;

            Action<DirichletInitResult> successCallback;
            Action<DirichletError> failureCallback;

            lock (initCallbackLock)
            {
                successCallback = pendingInitSuccess;
                failureCallback = pendingInitFailure;
                pendingInitSuccess = null;
                pendingInitFailure = null;
            }

            if (success)
            {
                var messageText = string.IsNullOrEmpty(description) ? "ios_mediation_bridge" : description;
                var result = DirichletInitResult.Ok(messageText);
                DirichletSdk.DispatchToUnityThread(() => successCallback?.Invoke(result));
                return;
            }

            var errorCode = code > 0 ? $"ios_init_{code}" : "ios_init_failed";
            var errorMessage = string.IsNullOrEmpty(description) ? "Initialization failed" : description;
            if (!string.IsNullOrEmpty(domain))
            {
                errorMessage = $"{errorMessage} ({domain})";
            }

            var error = new DirichletError(errorCode, errorMessage);
            DirichletSdk.DispatchToUnityThread(() =>
            {
                if (failureCallback != null)
                {
                    failureCallback(error);
                }
                else
                {
                    Debug.LogWarning($"[DirichletMediation][iOS] Init failure received but no callback registered: {error}");
                }
            });
        }

        [Serializable]
        private class LoadCallbackPayload
        {
            public string handle;
            public string eventName;
            public string adType;
            public LoadCallbackPayloadData data;
        }

        [Serializable]
        private class LoadCallbackPayloadData
        {
            public int code;
            public string message;
        }

        [Serializable]
        private class InitCallbackPayload
        {
            public bool success;
            public InitCallbackPayloadData data;
        }

        [Serializable]
        private class InitCallbackPayloadData
        {
            public int code;
            public string message;
            public string domain;
        }

        private class IOSLoadCallbackReceiver : MonoBehaviour
        {
            public IOSDirichletBridge bridge;

            public void OnLoadCallback(string payload)
            {
                bridge?.HandleLoadCallback(payload);
            }
        }

        private class IOSInitCallbackReceiver : MonoBehaviour
        {
            public IOSDirichletBridge bridge;

            public void OnInitCallback(string payload)
            {
                bridge?.HandleInitCallback(payload);
            }
        }

        private sealed class IOSLoadCallback
        {
            private readonly IOSDirichletBridge owner;
            private string handleId;
            private Action success;
            private readonly Action<DirichletError> failure;

            public IOSLoadCallback(IOSDirichletBridge owner, string handleId, Action success, Action<DirichletError> failure)
            {
                this.owner = owner;
                this.handleId = handleId;
                this.success = success;
                this.failure = failure;
            }

            public void SetHandle(DirichletPlatformAdHandle handle)
            {
                if (handle != null)
                {
                    handleId = handle.DebugId;
                }
            }

            public void SetSuccessCallback(Action callback)
            {
                success = callback;
            }

            public void OnSuccess()
            {
                owner.RemoveLoadCallback(handleId);
                DirichletSdk.DispatchToUnityThread(() => success?.Invoke());
            }

            public void OnError(string code, string message)
            {
                owner.RemoveLoadCallback(handleId);
                DirichletSdk.DispatchToUnityThread(() => failure?.Invoke(new DirichletError(code, message)));
            }
        }

        public void ShowRewardVideoAutoAd(DirichletAdRequest request, IDirichletRewardVideoAutoAdListener listener)
        {
            // iOS hasn't added this API yet
            DirichletSdk.DispatchToUnityThread(() => listener?.OnError(new DirichletError("not_supported", "showRewardVideoAutoAd is not supported on iOS yet")));
        }
    }
#else
    internal sealed class NoopDirichletBridge : IDirichletPlatformBridge
    {
        public void Initialize(DirichletPlatformInitOptions options, Action<DirichletInitResult> onSuccess, Action<DirichletError> onFailure)
        {
            Debug.LogWarning("[Dirichlet] No platform bridge available (editor/unsupported platform).");
            DirichletSdk.DispatchToUnityThread(() => onSuccess?.Invoke(DirichletInitResult.Ok("noop")));
        }

        public void InitializeWithoutTap(DirichletPlatformInitOptions options, Action<DirichletInitResult> onSuccess, Action<DirichletError> onFailure)
        {
            Debug.LogWarning("[Dirichlet] InitializeWithoutTap ignored on noop bridge.");
            DirichletSdk.DispatchToUnityThread(() => onSuccess?.Invoke(DirichletInitResult.Ok("noop")));
        }

        public void UpdateConfig(DirichletPlatformInitOptions options)
        {
            Debug.Log("[Dirichlet] UpdateConfig ignored on noop bridge.");
        }

        public void RequestPermissionIfNeeded()
        {
            Debug.Log("[Dirichlet] RequestPermissionIfNeeded ignored on noop bridge.");
        }

        public string GetSdkVersion() => "noop";

        private static DirichletPlatformAdHandle CreateStubHandle()
        {
            return DirichletPlatformAdHandle.CreateStub();
        }

        private static void LoadStubAd(DirichletPlatformAdHandle handle, Action<DirichletPlatformAdHandle> onSuccess)
        {
            Debug.Log($"[Dirichlet] LoadAd noop for {handle.DebugId}");
            DirichletSdk.DispatchToUnityThread(() => onSuccess?.Invoke(handle));
        }

        public void LoadRewardVideoAd(DirichletAdRequest request, Action<DirichletPlatformAdHandle> onSuccess, Action<DirichletError> onFailure)
        {
            var handle = CreateStubHandle();
            LoadStubAd(handle, onSuccess);
        }

        public bool ShowRewardVideoAd(DirichletPlatformAdHandle handle)
        {
            Debug.Log($"[Dirichlet] ShowRewardAd noop for {handle.DebugId}");
            return true;
        }

        public void LoadInterstitialAd(DirichletAdRequest request, Action<DirichletPlatformAdHandle> onSuccess, Action<DirichletError> onFailure)
        {
            var handle = CreateStubHandle();
            LoadStubAd(handle, onSuccess);
        }

        public bool ShowInterstitialAd(DirichletPlatformAdHandle handle)
        {
            Debug.Log($"[Dirichlet] ShowInterstitialAd noop for {handle.DebugId}");
            return true;
        }

        public void LoadBannerAd(DirichletAdRequest request, Action<DirichletPlatformAdHandle> onSuccess, Action<DirichletError> onFailure)
        {
            var handle = CreateStubHandle();
            LoadStubAd(handle, onSuccess);
        }

        public bool ShowBannerAd(DirichletPlatformAdHandle handle, DirichletAdShowOptions options)
        {
            Debug.Log($"[Dirichlet] ShowBannerAd noop for {handle.DebugId}");
            return true;
        }

        public void LoadSplashAd(DirichletAdRequest request, Action<DirichletPlatformAdHandle> onSuccess, Action<DirichletError> onFailure)
        {
            var handle = CreateStubHandle();
            LoadStubAd(handle, onSuccess);
        }

        public bool ShowSplashAd(DirichletPlatformAdHandle handle, DirichletAdShowOptions options)
        {
            Debug.Log($"[Dirichlet] ShowSplashAd noop for {handle.DebugId}");
            return true;
        }

        public void DestroyAd(DirichletPlatformAdHandle handle)
        {
            Debug.Log($"[Dirichlet] DestroyAd noop for {handle.DebugId}");
        }

        public bool IsAdValid(DirichletPlatformAdHandle handle)
        {
            Debug.Log($"[Dirichlet] IsAdValid noop for {handle?.DebugId}");
            return true;
        }

        public void ShowRewardVideoAutoAd(DirichletAdRequest request, IDirichletRewardVideoAutoAdListener listener)
        {
            Debug.Log("[Dirichlet] ShowRewardVideoAutoAd noop");
            DirichletSdk.DispatchToUnityThread(() =>
            {
                listener?.OnAdShow();
                listener?.OnRewardVerify(new DirichletRewardVerificationEventArgs(true, 10, "noop_reward", 0, "noop"));
                listener?.OnAdClose();
            });
        }
    }
#endif

    #endregion
}


