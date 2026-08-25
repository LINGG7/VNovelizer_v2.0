using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;

namespace Dirichlet.Mediation
{
    /// <summary>
    /// Platform handle for ad instances. This is a simple wrapper around the handle string
    /// returned from native bridge, matching the native SDK pattern where ad objects are
    /// returned directly from loadXXXAd() methods.
    /// </summary>
    public sealed class DirichletPlatformAdHandle
    {
        public string DebugId { get; }

        internal DirichletPlatformAdHandle(string debugId)
        {
            DebugId = debugId ?? throw new ArgumentNullException(nameof(debugId));
        }

        internal static DirichletPlatformAdHandle FromNative(string handleId)
        {
            if (string.IsNullOrEmpty(handleId))
            {
                handleId = Guid.NewGuid().ToString("N");
            }
            return new DirichletPlatformAdHandle(handleId);
        }

        internal static DirichletPlatformAdHandle CreateStub()
        {
            return new DirichletPlatformAdHandle(Guid.NewGuid().ToString("N"));
        }

        public override string ToString() => $"DirichletPlatformAdHandle(DebugId={DebugId})";
    }

    public enum DirichletAdType
    {
        RewardVideo = 0,
        Interstitial = 1,
        Banner = 2,
        Splash = 3,
        ExpressFeed = 4,
        NativeFeed = 5
    }

    /// <summary>
    /// Mirrors the Android-side DirichletAdManager to keep configuration state and create ad natives.
    /// </summary>
    public static class DirichletAdManager
    {
        private static readonly object StateLock = new object();
        private static DirichletAdConfig currentConfig;

        /// <summary>
        /// Returns the last configuration applied to the native SDK.
        /// </summary>
        public static DirichletAdConfig CurrentConfig
        {
            get
            {
                lock (StateLock)
                {
                    return currentConfig;
                }
            }
        }

        internal static void ApplyConfig(DirichletAdConfig config)
        {
            lock (StateLock)
            {
                currentConfig = config;
            }
        }

        internal static void Clear()
        {
            lock (StateLock)
            {
                currentConfig = null;
            }
        }

        /// <summary>
        /// Creates a Unity-side DirichletAdNative wrapper that mirrors the Android aggregator API.
        /// </summary>
        public static DirichletAdNative CreateAdNative()
        {
            return new DirichletAdNative(DirichletSdk.GetBridge());
        }

        internal static DirichletAdNative CreateAdNative(IDirichletPlatformBridge bridge)
        {
            return new DirichletAdNative(bridge);
        }
    }

    /// <summary>
    /// Thin wrapper around native ad handles provided by the platform bridge.
    /// </summary>

    public sealed class DirichletAdRequest
    {
        public long SpaceId { get; }
        public string Extra1 { get; }
        public string UserId { get; }
        public string RewardName { get; }
        public int? RewardAmount { get; }
        public string Query { get; }
        public int? ExpressViewWidth { get; }
        public int? ExpressViewHeight { get; }
        public int? ExpressImageWidth { get; }
        public int? ExpressImageHeight { get; }
        public long? MinaId { get; }

        private DirichletAdRequest(Builder builder)
        {
            SpaceId = builder.spaceId;
            Extra1 = builder.extra1;
            UserId = builder.userId;
            RewardName = builder.rewardName;
            RewardAmount = builder.rewardAmount;
            Query = builder.query;
            ExpressViewWidth = builder.expressViewWidth;
            ExpressViewHeight = builder.expressViewHeight;
            ExpressImageWidth = builder.expressImageWidth;
            ExpressImageHeight = builder.expressImageHeight;
            MinaId = builder.minaId;
        }

        public Builder ToBuilder()
        {
            var builder = new Builder()
                .WithSpaceId(SpaceId)
                .WithExtra1(Extra1)
                .WithUserId(UserId)
                .WithRewardName(RewardName)
                .WithQuery(Query);

            if (RewardAmount.HasValue)
            {
                builder.WithRewardAmount(RewardAmount.Value);
            }

            if (ExpressViewWidth.HasValue || ExpressViewHeight.HasValue)
            {
                builder.WithExpressViewSize(ExpressViewWidth ?? -1, ExpressViewHeight ?? -1);
            }

            if (ExpressImageWidth.HasValue || ExpressImageHeight.HasValue)
            {
                builder.WithExpressImageSize(ExpressImageWidth ?? -1, ExpressImageHeight ?? -1);
            }

            if (MinaId.HasValue)
            {
                builder.WithMinaId(MinaId.Value);
            }

            return builder;
        }

        internal Dictionary<string, object> ToBridgePayload()
        {
            var payload = new Dictionary<string, object>(StringComparer.Ordinal)
            {
                ["space_id"] = SpaceId
            };

            if (!string.IsNullOrEmpty(Extra1))
            {
                payload["extra1"] = Extra1;
            }

            if (!string.IsNullOrEmpty(UserId))
            {
                payload["user_id"] = UserId;
            }

            if (!string.IsNullOrEmpty(RewardName))
            {
                payload["reward_name"] = RewardName;
            }

            if (RewardAmount.HasValue)
            {
                payload["reward_amount"] = RewardAmount.Value;
            }

            if (!string.IsNullOrEmpty(Query))
            {
                payload["query"] = Query;
            }

            if (ExpressViewWidth.HasValue)
            {
                payload["express_width"] = ExpressViewWidth.Value;
            }

            if (ExpressViewHeight.HasValue)
            {
                payload["express_height"] = ExpressViewHeight.Value;
            }

            if (ExpressImageWidth.HasValue)
            {
                payload["express_image_width"] = ExpressImageWidth.Value;
            }

            if (ExpressImageHeight.HasValue)
            {
                payload["express_image_height"] = ExpressImageHeight.Value;
            }

            if (MinaId.HasValue)
            {
                payload["mina_id"] = MinaId.Value;
            }

            return payload;
        }

        public sealed class Builder
        {
            internal long spaceId;
            internal string extra1;
            internal string userId;
            internal string rewardName;
            internal int? rewardAmount;
            internal string query;
            internal int? expressViewWidth;
            internal int? expressViewHeight;
            internal int? expressImageWidth;
            internal int? expressImageHeight;
            internal long? minaId;

            public Builder WithSpaceId(long value)
            {
                if (value <= 0)
                {
                    throw new ArgumentOutOfRangeException(nameof(value), "SpaceId must be greater than zero.");
                }

                spaceId = value;
                return this;
            }

            public Builder WithExtra1(string value)
            {
                extra1 = value;
                return this;
            }

            public Builder WithUserId(string value)
            {
                userId = value;
                return this;
            }

            public Builder WithRewardName(string value)
            {
                rewardName = value;
                return this;
            }

            public Builder WithRewardAmount(int value)
            {
                rewardAmount = value;
                return this;
            }

            public Builder WithQuery(string value)
            {
                query = value;
                return this;
            }

            public Builder WithExpressViewSize(int width, int height)
            {
                expressViewWidth = width;
                expressViewHeight = height;
                return this;
            }

            public Builder WithExpressImageSize(int width, int height)
            {
                expressImageWidth = width;
                expressImageHeight = height;
                return this;
            }

            public Builder WithMinaId(long value)
            {
                minaId = value;
                return this;
            }

            public DirichletAdRequest Build()
            {
                if (spaceId <= 0)
                {
                    throw new InvalidOperationException("SpaceId must be set to a value greater than zero before building the request.");
                }

                return new DirichletAdRequest(this);
            }
        }
    }

    public enum DirichletBannerAlignment
    {
        Top = 0,
        Bottom = 1
    }

    public sealed class DirichletAdShowOptions
    {
        public DirichletBannerAlignment BannerAlignment { get; set; } = DirichletBannerAlignment.Bottom;
        public int BannerOffset { get; set; }

        internal Dictionary<string, object> ToBridgePayload()
        {
            var payload = new Dictionary<string, object>(StringComparer.Ordinal);

            // Banner options are always included if set, Bridge will handle based on ad type
            payload["banner_baseline"] = (int)BannerAlignment;
            payload["banner_offset"] = BannerOffset;

            return payload;
        }
    }

    public sealed class DirichletAdNative
    {
        private readonly IDirichletPlatformBridge bridge;
        private static readonly object RewardAutoSessionLock = new object();
        private static readonly Dictionary<string, AutoRewardVideoSession> RewardAutoSessions = new Dictionary<string, AutoRewardVideoSession>(StringComparer.Ordinal);

        public static DirichletAdNative Create() => DirichletAdManager.CreateAdNative();

        internal DirichletAdNative(IDirichletPlatformBridge bridge)
        {
            this.bridge = bridge ?? throw new ArgumentNullException(nameof(bridge));
        }

        public void LoadRewardVideoAd(DirichletAdRequest request, Action<DirichletRewardVideoAd> onLoaded, Action<DirichletError> onFailure)
        {
        if (!ValidateRequest(request, onFailure))
        {
            return;
        }

        bridge.LoadRewardVideoAd(request,
            handle =>
            {
                var ad = DirichletRewardVideoAd.Create(bridge, handle, request.SpaceId);
                ad.MarkLoaded();
                onLoaded?.Invoke(ad);
            },
            onFailure);
        }

        public void LoadInterstitialAd(DirichletAdRequest request, Action<DirichletInterstitialAd> onLoaded, Action<DirichletError> onFailure)
        {
        if (!ValidateRequest(request, onFailure))
        {
            return;
        }

        bridge.LoadInterstitialAd(request,
            handle =>
            {
                var ad = DirichletInterstitialAd.Create(bridge, handle, request.SpaceId);
                ad.MarkLoaded();
                onLoaded?.Invoke(ad);
            },
            onFailure);
        }

        public void LoadBannerAd(DirichletAdRequest request, Action<DirichletBannerAd> onLoaded, Action<DirichletError> onFailure)
        {
        if (!ValidateRequest(request, onFailure))
        {
            return;
        }

        bridge.LoadBannerAd(request,
            handle =>
            {
                var ad = DirichletBannerAd.Create(bridge, handle, request.SpaceId);
                ad.MarkLoaded();
                onLoaded?.Invoke(ad);
            },
            onFailure);
        }

        public void LoadSplashAd(DirichletAdRequest request, Action<DirichletSplashAd> onLoaded, Action<DirichletError> onFailure)
        {
        if (!ValidateRequest(request, onFailure))
        {
            return;
        }

        bridge.LoadSplashAd(request,
            handle =>
            {
                var ad = DirichletSplashAd.Create(bridge, handle, request.SpaceId);
                ad.MarkLoaded();
                onLoaded?.Invoke(ad);
            },
            onFailure);
        }

        public void LoadExpressFeedAd(DirichletAdRequest request, Action<IReadOnlyList<DirichletAd>> onLoaded, Action<DirichletError> onFailure)
        {
            NotifyNotSupported("express_feed", onFailure);
        }

        public void LoadNativeFeedAd(DirichletAdRequest request, Action<IReadOnlyList<DirichletAd>> onLoaded, Action<DirichletError> onFailure)
        {
            NotifyNotSupported("native_feed", onFailure);
        }

        /// <summary>
        /// Shows a reward video ad with automatic load-and-show logic.
        /// This method combines loading and showing into a single operation.
        ///
        /// - Android: 使用 native 侧的 AutoAd（可能包含缓存策略，保持原有行为）
        /// - iOS: 使用 Unity C# 侧的“load 成功后立刻 show”的简化方案（不做缓存）
        /// </summary>
        /// <param name="request">Ad request parameters</param>
        /// <param name="listener">Listener for all ad events (show/close/reward/click/error)</param>
        public void ShowRewardVideoAutoAd(DirichletAdRequest request, IDirichletRewardVideoAutoAdListener listener)
        {
            if (!ValidateRequest(request, error => listener?.OnError(error)))
            {
                return;
            }

#if UNITY_ANDROID && !UNITY_EDITOR
            bridge.ShowRewardVideoAutoAd(request, listener);
#else
            // iOS/Editor/other platforms: use simplified load+show implementation (no cache).
            ShowRewardVideoLoadAndShowInternal(request, listener);
#endif
        }

        private void ShowRewardVideoLoadAndShowInternal(DirichletAdRequest request, IDirichletRewardVideoAutoAdListener listener)
        {
            LoadRewardVideoAd(
                request,
                ad =>
                {
                    if (ad == null)
                    {
                        listener?.OnError(new DirichletError("invalid_ad", "Load callback returned null ad"));
                        return;
                    }

                    var sessionId = Guid.NewGuid().ToString("N");
                    var session = new AutoRewardVideoSession(sessionId, ad, listener);
                    RegisterRewardAutoSession(session);
                    ad.SetInteractionListener(session);

                    // Load succeeded; show immediately.
                    var shown = ad.Show();
                    if (!shown)
                    {
                        session.FailAndDispose(new DirichletError("show_failed", "ShowRewardVideoAd returned false"));
                    }
                },
                error =>
                {
                    listener?.OnError(error ?? new DirichletError("load_failed", "LoadRewardVideoAd failed"));
                });
        }

    private static bool ValidateRequest(DirichletAdRequest request, Action<DirichletError> onFailure)
    {
        if (request == null)
        {
            onFailure?.Invoke(new DirichletError("invalid_request", "DirichletAdRequest cannot be null"));
            return false;
        }

        if (request.SpaceId <= 0)
        {
            onFailure?.Invoke(new DirichletError("invalid_space_id", "DirichletAdRequest.SpaceId must be greater than zero"));
            return false;
        }

        return true;
    }

        private void NotifyNotSupported(string feature, Action<DirichletError> onFailure)
        {
            onFailure?.Invoke(new DirichletError("not_supported", $"Dirichlet Unity bridge does not yet support {feature} ads."));
        }

        private static void RegisterRewardAutoSession(AutoRewardVideoSession session)
        {
            if (session == null)
            {
                return;
            }

            lock (RewardAutoSessionLock)
            {
                RewardAutoSessions[session.SessionId] = session;
            }
        }

        private static void UnregisterRewardAutoSession(string sessionId)
        {
            if (string.IsNullOrEmpty(sessionId))
            {
                return;
            }

            lock (RewardAutoSessionLock)
            {
                RewardAutoSessions.Remove(sessionId);
            }
        }

        private sealed class AutoRewardVideoSession : IDirichletRewardAdInteractionListener
        {
            public string SessionId { get; }

            private readonly DirichletRewardVideoAd ad;
            private readonly IDirichletRewardVideoAutoAdListener listener;
            private bool disposed;

            public AutoRewardVideoSession(string sessionId, DirichletRewardVideoAd ad, IDirichletRewardVideoAutoAdListener listener)
            {
                SessionId = string.IsNullOrEmpty(sessionId) ? Guid.NewGuid().ToString("N") : sessionId;
                this.ad = ad ?? throw new ArgumentNullException(nameof(ad));
                this.listener = listener;
            }

            public void OnAdShow()
            {
                if (disposed)
                {
                    return;
                }

                listener?.OnAdShow();
            }

            public void OnAdClick()
            {
                if (disposed)
                {
                    return;
                }

                listener?.OnAdClick();
            }

            public void OnAdClose()
            {
                if (disposed)
                {
                    return;
                }

                listener?.OnAdClose();
                Dispose();
            }

            public void OnRewardVerify(DirichletRewardVerificationEventArgs args)
            {
                if (disposed)
                {
                    return;
                }

                listener?.OnRewardVerify(args);
            }

            public void FailAndDispose(DirichletError error)
            {
                if (disposed)
                {
                    return;
                }

                listener?.OnError(error ?? new DirichletError("unknown_error", "Unknown error"));
                Dispose();
            }

            private void Dispose()
            {
                if (disposed)
                {
                    return;
                }

                disposed = true;
                UnregisterRewardAutoSession(SessionId);

                try
                {
                    ad?.Destroy();
                }
                catch (Exception ex)
                {
                    Debug.LogWarning($"[Dirichlet] Auto reward ad Destroy failed: {ex.Message}");
                }
            }
        }
    }

    /// <summary>
    /// Base Unity-side ad handle. Responsible for keeping track of lifecycle state.
    /// Matches the native SDK pattern where ad objects are returned from loadXXXAd() methods.
    /// </summary>
    public abstract class DirichletAd
    {
        protected readonly DirichletPlatformAdHandle PlatformHandle;
        private readonly IDirichletPlatformBridge bridge;
        private readonly long spaceId;

        public string SlotId => spaceId > 0 ? spaceId.ToString(CultureInfo.InvariantCulture) : string.Empty;
        public bool IsLoaded { get; protected set; }

        /// <summary>
        /// Returns whether the ad is still valid and can be shown.
        /// This checks the native ad object's validity, which may expire after some time.
        /// Always call this before Show() to ensure the ad hasn't expired.
        /// </summary>
        public bool IsValid => bridge?.IsAdValid(PlatformHandle) ?? false;

        /// <summary>
        /// Raised when the native layer confirms the ad was shown to the user.
        /// </summary>
        public event Action Shown;

        /// <summary>
        /// Raised when the user clicks the ad.
        /// </summary>
        public event Action Clicked;

        /// <summary>
        /// Raised when the ad is closed (either by user action or channel logic).
        /// </summary>
        public event Action Closed;

        internal DirichletAd(IDirichletPlatformBridge bridge, DirichletPlatformAdHandle platformHandle, long spaceId)
        {
            this.bridge = bridge ?? throw new ArgumentNullException(nameof(bridge));
            PlatformHandle = platformHandle ?? throw new ArgumentNullException(nameof(platformHandle));
            this.spaceId = spaceId;
            if (!string.IsNullOrEmpty(PlatformHandle.DebugId))
            {
                DirichletAdEventRouter.Register(PlatformHandle.DebugId, this);
            }
        }

        internal IDirichletPlatformBridge Bridge => bridge;

        public virtual bool Show()
        {
            if (!IsLoaded)
            {
                Debug.LogWarning($"[Dirichlet] Attempted to show ad before load success. Slot={SlotId}");
            }

            var shown = ShowInternal(null);
            if (shown)
            {
                IsLoaded = false;
            }
            return shown;
        }

        public void Destroy()
        {
            if (!string.IsNullOrEmpty(PlatformHandle?.DebugId))
            {
                DirichletAdEventRouter.Unregister(PlatformHandle.DebugId);
            }
            DestroyInternal();
            IsLoaded = false;
        }

        internal void MarkLoaded()
        {
            IsLoaded = true;
        }

        protected abstract bool ShowInternal(DirichletAdShowOptions options);

        protected abstract void DestroyInternal();

        internal virtual void HandleNativeEvent(DirichletNativeEvent nativeEvent)
        {
            switch (nativeEvent.EventName)
            {
                case DirichletNativeEventNames.Show:
                    OnShown();
                    break;
                case DirichletNativeEventNames.Click:
                    OnClicked();
                    break;
                case DirichletNativeEventNames.Close:
                    OnClosed();
                    break;
            }
        }

        protected virtual void OnShown()
        {
            Shown?.Invoke();
        }

        protected virtual void OnClicked()
        {
            Clicked?.Invoke();
        }

        protected virtual void OnClosed()
        {
            Closed?.Invoke();
        }
    }

    public abstract class DirichletRewardAdBase : DirichletAd
    {
        public event Action<DirichletRewardVerificationEventArgs> RewardVerified;
        private IDirichletRewardAdInteractionListener interactionListener;

        internal DirichletRewardAdBase(IDirichletPlatformBridge bridge, DirichletPlatformAdHandle handle, long spaceId)
            : base(bridge, handle, spaceId)
        {
        }

        public void SetInteractionListener(IDirichletRewardAdInteractionListener listener)
        {
            interactionListener = listener;
        }

        public void SetRewardAdInteractionListener(IDirichletRewardAdInteractionListener listener)
        {
            SetInteractionListener(listener);
        }

        protected override void OnShown()
        {
            base.OnShown();
            interactionListener?.OnAdShow();
        }

        protected override void OnClicked()
        {
            base.OnClicked();
            interactionListener?.OnAdClick();
        }

        protected override void OnClosed()
        {
            base.OnClosed();
            interactionListener?.OnAdClose();
        }

        internal override void HandleNativeEvent(DirichletNativeEvent nativeEvent)
        {
            base.HandleNativeEvent(nativeEvent);

            if (nativeEvent.EventName == DirichletNativeEventNames.Reward && nativeEvent.Data != null)
            {
                var data = nativeEvent.Data;
                var args = new DirichletRewardVerificationEventArgs(
                    data.RewardVerify,
                    data.RewardAmount,
                    data.RewardName,
                    data.Code,
                    data.Message);
                RewardVerified?.Invoke(args);
                interactionListener?.OnRewardVerify(args);
            }
        }

        protected override bool ShowInternal(DirichletAdShowOptions options)
        {
            return Bridge.ShowRewardVideoAd(PlatformHandle);
        }

        protected override void DestroyInternal()
        {
            Bridge.DestroyAd(PlatformHandle);
        }
    }

    public sealed class DirichletRewardVideoAd : DirichletRewardAdBase
    {
        private DirichletRewardVideoAd(IDirichletPlatformBridge bridge, DirichletPlatformAdHandle handle, long spaceId)
            : base(bridge, handle, spaceId)
        {
        }

        internal static DirichletRewardVideoAd Create(IDirichletPlatformBridge bridge, DirichletPlatformAdHandle handle, long spaceId)
        {
            return new DirichletRewardVideoAd(bridge, handle, spaceId);
        }
    }

    [Obsolete("Use DirichletRewardVideoAd instead.")]
    public sealed class DirichletRewardAd : DirichletRewardAdBase
    {
        internal DirichletRewardAd(IDirichletPlatformBridge bridge, DirichletPlatformAdHandle handle, long spaceId) : base(bridge, handle, spaceId)
        {
        }
    }

    public sealed class DirichletInterstitialAd : DirichletAd
    {
        private IDirichletInterstitialAdInteractionListener interactionListener;

        private DirichletInterstitialAd(IDirichletPlatformBridge bridge, DirichletPlatformAdHandle handle, long spaceId)
            : base(bridge, handle, spaceId)
        {
        }

        public void SetInteractionListener(IDirichletInterstitialAdInteractionListener listener)
        {
            interactionListener = listener;
        }

        protected override void OnShown()
        {
            base.OnShown();
            interactionListener?.OnAdShow();
        }

        protected override void OnClicked()
        {
            base.OnClicked();
            interactionListener?.OnAdClick();
        }

        protected override void OnClosed()
        {
            base.OnClosed();
            interactionListener?.OnAdClose();
        }

        protected override bool ShowInternal(DirichletAdShowOptions options)
        {
            return Bridge.ShowInterstitialAd(PlatformHandle);
        }

        protected override void DestroyInternal()
        {
            Bridge.DestroyAd(PlatformHandle);
        }

        internal static DirichletInterstitialAd Create(IDirichletPlatformBridge bridge, DirichletPlatformAdHandle handle, long spaceId)
        {
            return new DirichletInterstitialAd(bridge, handle, spaceId);
        }
    }

    public sealed class DirichletBannerAd : DirichletAd
    {
        private IDirichletBannerAdInteractionListener interactionListener;

        private DirichletBannerAd(IDirichletPlatformBridge bridge, DirichletPlatformAdHandle handle, long spaceId)
            : base(bridge, handle, spaceId)
        {
        }

        public void SetInteractionListener(IDirichletBannerAdInteractionListener listener)
        {
            interactionListener = listener;
        }

        public bool Show(DirichletAdShowOptions options)
        {
            var showOptions = options ?? new DirichletAdShowOptions();
            var shown = ShowInternal(showOptions);
            if (shown)
            {
                IsLoaded = false;
            }
            return shown;
        }

        public override bool Show()
        {
            return Show(null);
        }

        protected override void OnShown()
        {
            base.OnShown();
            interactionListener?.OnAdShow();
        }

        protected override void OnClicked()
        {
            base.OnClicked();
            interactionListener?.OnAdClick();
        }

        protected override void OnClosed()
        {
            base.OnClosed();
            interactionListener?.OnAdClose();
        }

        protected override bool ShowInternal(DirichletAdShowOptions options)
        {
            return Bridge.ShowBannerAd(PlatformHandle, options ?? new DirichletAdShowOptions());
        }

        protected override void DestroyInternal()
        {
            Bridge.DestroyAd(PlatformHandle);
        }

        internal static DirichletBannerAd Create(IDirichletPlatformBridge bridge, DirichletPlatformAdHandle handle, long spaceId)
        {
            return new DirichletBannerAd(bridge, handle, spaceId);
        }
    }

    public sealed class DirichletSplashAd : DirichletAd
    {
        private IDirichletSplashAdInteractionListener interactionListener;

        private DirichletSplashAd(IDirichletPlatformBridge bridge, DirichletPlatformAdHandle handle, long spaceId)
            : base(bridge, handle, spaceId)
        {
        }

        public void SetInteractionListener(IDirichletSplashAdInteractionListener listener)
        {
            interactionListener = listener;
        }

        protected override void OnShown()
        {
            base.OnShown();
            interactionListener?.OnAdShow();
        }

        protected override void OnClicked()
        {
            base.OnClicked();
            interactionListener?.OnAdClick();
        }

        protected override void OnClosed()
        {
            base.OnClosed();
            interactionListener?.OnAdClose();
        }

        protected override bool ShowInternal(DirichletAdShowOptions options)
        {
            return Bridge.ShowSplashAd(PlatformHandle, options ?? new DirichletAdShowOptions());
        }

        protected override void DestroyInternal()
        {
            Bridge.DestroyAd(PlatformHandle);
        }

        internal static DirichletSplashAd Create(IDirichletPlatformBridge bridge, DirichletPlatformAdHandle handle, long spaceId)
        {
            return new DirichletSplashAd(bridge, handle, spaceId);
        }
    }

    internal static class DirichletNativeEventNames
    {
        internal const string Show = "show";
        internal const string Click = "click";
        internal const string Close = "close";
        internal const string Reward = "reward";
    }

    internal readonly struct DirichletNativeEvent
    {
        public string EventName { get; }
        public string AdType { get; }
        public DirichletNativeEventData Data { get; }

        public DirichletNativeEvent(string eventName, string adType, DirichletNativeEventData data)
        {
            EventName = string.IsNullOrEmpty(eventName) ? string.Empty : eventName;
            AdType = string.IsNullOrEmpty(adType) ? string.Empty : adType;
            Data = data;
        }
    }

    internal sealed class DirichletNativeEventData
    {
        public bool RewardVerify { get; }
        public int RewardAmount { get; }
        public string RewardName { get; }
        public int Code { get; }
        public string Message { get; }

        public DirichletNativeEventData(bool rewardVerify, int rewardAmount, string rewardName, int code, string message)
        {
            RewardVerify = rewardVerify;
            RewardAmount = rewardAmount;
            RewardName = string.IsNullOrEmpty(rewardName) ? string.Empty : rewardName;
            Code = code;
            Message = message ?? string.Empty;
        }
    }

    public sealed class DirichletRewardVerificationEventArgs : EventArgs
    {
        public bool IsVerified { get; }
        public int RewardAmount { get; }
        public string RewardName { get; }
        public int Code { get; }
        public string Message { get; }

        internal DirichletRewardVerificationEventArgs(bool rewardVerified, int rewardAmount, string rewardName, int code, string message)
        {
            IsVerified = rewardVerified;
            RewardAmount = rewardAmount;
            RewardName = string.IsNullOrEmpty(rewardName) ? string.Empty : rewardName;
            Code = code;
            Message = message ?? string.Empty;
        }
    }

    public interface IDirichletRewardAdInteractionListener
    {
        void OnAdShow();
        void OnAdClick();
        void OnAdClose();
        void OnRewardVerify(DirichletRewardVerificationEventArgs args);
    }

    public interface IDirichletInterstitialAdInteractionListener
    {
        void OnAdShow();
        void OnAdClick();
        void OnAdClose();
    }

    public interface IDirichletBannerAdInteractionListener
    {
        void OnAdShow();
        void OnAdClick();
        void OnAdClose();
    }

    public interface IDirichletSplashAdInteractionListener
    {
        void OnAdShow();
        void OnAdClick();
        void OnAdClose();
    }

    /// <summary>
    /// Listener interface for auto reward video ad callbacks.
    /// Used with ShowRewardVideoAutoAd which combines load and show into one operation.
    /// Android only - iOS will receive OnError with not_supported error.
    /// </summary>
    public interface IDirichletRewardVideoAutoAdListener
    {
        /// <summary>
        /// Called when ad fails to load or show.
        /// </summary>
        void OnError(DirichletError error);

        /// <summary>
        /// Called when ad is shown to the user.
        /// </summary>
        void OnAdShow();

        /// <summary>
        /// Called when ad is closed.
        /// </summary>
        void OnAdClose();

        /// <summary>
        /// Called when reward verification is completed.
        /// </summary>
        void OnRewardVerify(DirichletRewardVerificationEventArgs args);

        /// <summary>
        /// Called when ad is clicked.
        /// </summary>
        void OnAdClick();
    }

    internal static class DirichletAdEventRouter
    {
        private const string CallbackObjectName = "DirichletMediationEventReceiver";
        private static readonly Dictionary<string, WeakReference> Ads = new Dictionary<string, WeakReference>(StringComparer.Ordinal);
        private static bool receiverInitialized;

        internal static void Register(string handleId, DirichletAd ad)
        {
            if (string.IsNullOrEmpty(handleId) || ad == null)
            {
                return;
            }

            EnsureReceiver();
            Ads[handleId] = new WeakReference(ad);
        }

        internal static void Unregister(string handleId)
        {
            if (string.IsNullOrEmpty(handleId))
            {
                return;
            }

            Ads.Remove(handleId);
        }

        private static void EnsureReceiver()
        {
            if (receiverInitialized)
            {
                return;
            }

            if (!DirichletSdk.IsUnityThread)
            {
                DirichletSdk.DispatchToUnityThread(EnsureReceiver);
                return;
            }

            var existing = GameObject.Find(CallbackObjectName);
            if (existing == null)
            {
                var host = new GameObject(CallbackObjectName)
                {
                    hideFlags = HideFlags.HideAndDontSave
                };
                UnityEngine.Object.DontDestroyOnLoad(host);
                host.AddComponent<DirichletAdEventReceiver>();
            }

            receiverInitialized = true;
        }

        internal static void HandleNativeEvent(string payload)
        {
            if (string.IsNullOrEmpty(payload))
            {
                return;
            }

            NativeEventPayload message;
            try
            {
                message = JsonUtility.FromJson<NativeEventPayload>(payload);
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[Dirichlet] Failed to parse native ad event: {ex.Message}\n{payload}");
                return;
            }

            if (message == null || string.IsNullOrEmpty(message.handle) || string.IsNullOrEmpty(message.eventName))
            {
                return;
            }

            if (!Ads.TryGetValue(message.handle, out var weakReference))
            {
                return;
            }

            if (!(weakReference.Target is DirichletAd ad) || ad == null)
            {
                Ads.Remove(message.handle);
                return;
            }

            var data = message.data != null
                ? new DirichletNativeEventData(
                    message.data.rewardVerify,
                    message.data.rewardAmount,
                    message.data.rewardName,
                    message.data.code,
                    message.data.message)
                : null;

            var nativeEvent = new DirichletNativeEvent(message.eventName, message.adType, data);

            if (DirichletSdk.IsUnityThread)
            {
                ad.HandleNativeEvent(nativeEvent);
            }
            else
            {
                DirichletSdk.DispatchToUnityThread(() => ad.HandleNativeEvent(nativeEvent));
            }
        }

        [Serializable]
        private class NativeEventPayload
        {
            public string handle;
            public string eventName;
            public string adType;
            public NativeEventPayloadData data;
        }

        [Serializable]
        private class NativeEventPayloadData
        {
            public bool rewardVerify;
            public int rewardAmount;
            public string rewardName;
            public int code;
            public string message;
        }

        private sealed class DirichletAdEventReceiver : MonoBehaviour
        {
            public void OnNativeEvent(string payload)
            {
                HandleNativeEvent(payload);
            }
        }
    }
}


