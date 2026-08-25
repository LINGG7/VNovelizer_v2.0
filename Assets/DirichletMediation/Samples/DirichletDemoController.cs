using System;
using System.Globalization;
using System.Threading;
using Dirichlet.Mediation;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Dirichlet.Mediation.Samples
{
    /// <summary>
    /// Dirichlet 聚合 SDK 的 Unity 演示脚本，涵盖初始化、权限申请、各广告位加载/展示与日志输出。
    /// </summary>
    public sealed class DirichletDemoController : MonoBehaviour
    {
        // Android 配置
        private const string AndroidMediaId = "1011568";
        private const string AndroidMediaName = "老婆们大战牛头人";
        private const string AndroidMediaKey = "8mJUSJYGzZbcl4azA8HQDCnOswkGvZYZjrIVsnz21TjvAtfId5yeHOuTgewtJapJ";
        private const string AndroidRewardSlot = "1045256";
        private const string AndroidInterstitialSlot = "1045320";
        private const string AndroidBannerSlot = "1045355";
        private const string AndroidSplashSlot = "1045271";

        // iOS 配置
        private const string IOSMediaId = "1012736";
        private const string IOSMediaName = "TapTap-iOS";
        private const string IOSMediaKey = "cdpwD6CcGxqaGEw8ie6LA6JlyM9vKxQuPlfJqEVp2u330IlG8ieBppcz85VrLJ71";
        private const string IOSRewardSlot = "1047667";        // 激励视频-竖屏
        private const string IOSInterstitialSlot = "1047694";  // 插屏-半屏-竖屏
        private const string IOSBannerSlot = "1047679";         // banner-竖屏
        private const string IOSSplashSlot = "1047675";         // 开屏-竖屏

        // 平台自适应配置
#if UNITY_IOS && !UNITY_EDITOR
        private const string DefaultMediaId = IOSMediaId;
        private const string DefaultMediaName = IOSMediaName;
        private const string DefaultMediaKey = IOSMediaKey;
        private const string DefaultRewardSlot = IOSRewardSlot;
        private const string DefaultInterstitialSlot = IOSInterstitialSlot;
        private const string DefaultBannerSlot = IOSBannerSlot;
        private const string DefaultSplashSlot = IOSSplashSlot;
#else
        private const string DefaultMediaId = AndroidMediaId;
        private const string DefaultMediaName = AndroidMediaName;
        private const string DefaultMediaKey = AndroidMediaKey;
        private const string DefaultRewardSlot = AndroidRewardSlot;
        private const string DefaultInterstitialSlot = AndroidInterstitialSlot;
        private const string DefaultBannerSlot = AndroidBannerSlot;
        private const string DefaultSplashSlot = AndroidSplashSlot;
#endif

        // 通用配置
        private const string DefaultChannel = "taptap2";
        private const string DefaultSubChannel = "release";
        private const string DefaultTapClientId = "0RiAlMny7jiz086FaU";
        private const string DefaultUserId = "unity_user";
        private const string DefaultRewardName = "金币";
        private const int DefaultRewardAmount = 10;
        // Use readonly instead of const to avoid unreachable-code warnings while keeping the values centralized.
        // 0 means "use network default / auto size".
        private static readonly int DefaultBannerWidth = 0;
        private static readonly int DefaultBannerHeight = 0;
        private const int DefaultSplashWidth = 1080;
        private const int DefaultSplashHeight = 1920;

        // UI 引用
        private InputField mediaIdInput;
        private InputField channelInput;
        private InputField subChannelInput;
        private Toggle debugToggle;
        private Button initButton;
        private Button requestPermissionButton;

        private InputField rewardSlotInput;
        private Button rewardLoadButton;
        private Button rewardShowButton;
        private Button rewardAutoButton;

        private InputField interstitialSlotInput;
        private Button interstitialLoadButton;
        private Button interstitialShowButton;

        private InputField bannerSlotInput;
        private Button bannerLoadButton;
        private Button bannerShowButton;

        private InputField splashSlotInput;
        private Button splashLoadButton;
        private Button splashShowButton;

        private Text statusText;
        private ScrollRect statusScroll;
        private RectTransform statusOverlay;
        private RectTransform statusBody;
        private Text statusToggleLabel;
        private bool statusPanelCollapsed;
        private int statusUnreadCount;
        private Vector2 statusDragPointerOffset;
        private RectTransform canvasRect;

        private const float STATUS_OVERLAY_WIDTH = 480f;
        private const float STATUS_OVERLAY_HEIGHT = 320f;
        private const float STATUS_OVERLAY_COLLAPSED_HEIGHT = 86f;

        private Sprite roundedSprite;
        private Texture2D roundedTexture;
        private Font uiFont;
        private RectTransform safeAreaRect;
        private Rect cachedSafeArea = Rect.zero;
        private Vector2Int cachedScreenSize = Vector2Int.zero;
        private ScreenOrientation cachedOrientation = ScreenOrientation.AutoRotation;

        // 音频探针
        private AudioSource audioProbeSource;
        private Button audioProbeToggleButton;
        private Text audioProbeStatusText;
        private bool audioProbeRunning;

        // 广告句柄
        private DirichletRewardVideoAd rewardAd;
        private DirichletInterstitialAd interstitialAd;
        private DirichletBannerAd bannerAd;
        private DirichletSplashAd splashAd;
        private DirichletAdNative adNative;

        private SynchronizationContext demoUnityContext;
        private int demoUnityThreadId;

        private int originalVSyncCount;
        private int originalTargetFrameRate;
        private int appliedVSyncCount;
        private int appliedTargetFrameRate;
        private bool frameRateOverridesApplied;

        private void Awake()
        {
            demoUnityThreadId = Thread.CurrentThread.ManagedThreadId;
            demoUnityContext = SynchronizationContext.Current;

            // 启用 iOS ProMotion 高刷新率支持（120Hz）
            // 需配合 Info.plist 中的 CADisableMinimumFrameDurationOnPhone = true
            CaptureFrameRateState();
            ApplyDemoFrameRateOverrides();
            
            SetupCanvas();
            BindEvents();
            Log("Dirichlet 聚合 Demo 已启动");
        }

        private void OnDestroy()
        {
            StopAudioProbe();
            RestoreFrameRateOverrides();
        }

        #region 初始化与事件

        private void CaptureFrameRateState()
        {
            originalVSyncCount = QualitySettings.vSyncCount;
            originalTargetFrameRate = Application.targetFrameRate;
        }

        private void ApplyDemoFrameRateOverrides()
        {
            appliedVSyncCount = 0;
            appliedTargetFrameRate = 120;

            QualitySettings.vSyncCount = appliedVSyncCount;
            Application.targetFrameRate = appliedTargetFrameRate;
            frameRateOverridesApplied = true;
        }

        private void RestoreFrameRateOverrides()
        {
            if (!frameRateOverridesApplied)
            {
                return;
            }

            // Only rollback when the host didn't change the values after the demo applied overrides.
            if (QualitySettings.vSyncCount == appliedVSyncCount)
            {
                QualitySettings.vSyncCount = originalVSyncCount;
            }

            if (Application.targetFrameRate == appliedTargetFrameRate)
            {
                Application.targetFrameRate = originalTargetFrameRate;
            }

            frameRateOverridesApplied = false;
        }

        private void BindEvents()
        {
            if (initButton != null)
            {
                initButton.onClick.AddListener(InitializeSdk);
            }

            if (requestPermissionButton != null)
            {
                requestPermissionButton.onClick.AddListener(RequestPermissions);
            }

            if (rewardLoadButton != null)
            {
                rewardLoadButton.onClick.AddListener(LoadRewardAd);
            }

            if (rewardShowButton != null)
            {
                rewardShowButton.onClick.AddListener(ShowRewardAd);
            }

            if (rewardAutoButton != null)
            {
                rewardAutoButton.onClick.AddListener(ShowRewardAutoAd);
            }

            if (interstitialLoadButton != null)
            {
                interstitialLoadButton.onClick.AddListener(LoadInterstitialAd);
            }

            if (interstitialShowButton != null)
            {
                interstitialShowButton.onClick.AddListener(ShowInterstitialAd);
            }

            if (bannerLoadButton != null)
            {
                bannerLoadButton.onClick.AddListener(LoadBannerAd);
            }

            if (bannerShowButton != null)
            {
                bannerShowButton.onClick.AddListener(ShowBannerAd);
            }

            if (splashLoadButton != null)
            {
                splashLoadButton.onClick.AddListener(LoadSplashAd);
            }

            if (splashShowButton != null)
            {
                splashShowButton.onClick.AddListener(ShowSplashAd);
            }
        }

        private void InitializeSdk()
        {
            var mediaIdText = string.IsNullOrWhiteSpace(mediaIdInput?.text) ? DefaultMediaId : mediaIdInput.text.Trim();

            if (!long.TryParse(mediaIdText, NumberStyles.Integer, CultureInfo.InvariantCulture, out var mediaId) || mediaId <= 0)
            {
                Log($"请输入有效的 MediaId（当前值: {mediaIdText}）");
                return;
            }

            var config = new DirichletAdConfig.Builder()
                .WithMediaId(mediaId)
                .WithGameChannel(string.IsNullOrWhiteSpace(channelInput?.text) ? DefaultChannel : channelInput.text.Trim())
                .WithSubChannel(string.IsNullOrWhiteSpace(subChannelInput?.text) ? DefaultSubChannel : subChannelInput.text.Trim())
                .EnableDebug(debugToggle == null || debugToggle.isOn)
                .WithMediaName(DefaultMediaName)
                .WithMediaKey(DefaultMediaKey)
                .WithTapClientId(DefaultTapClientId)
                .ShakeEnabled(true)
                .Build();

            Log($"开始初始化，Media={config.MediaName}, MediaId={config.MediaId}, Channel={config.GameChannel}");

            DirichletSdk.Init(config,
                result =>
                {
                    Log("初始化成功" + (string.IsNullOrEmpty(result?.Message) ? string.Empty : $": {result.Message}"));
                    ToggleInitializationButtons(false);
                    ToggleAdLoaders(true);
                    requestPermissionButton.interactable = true;
                    adNative = DirichletAdManager.CreateAdNative();
                },
                error =>
                {
                    Log($"初始化失败: {error}");
                    ToggleAdLoaders(false);
                    requestPermissionButton.interactable = false;
                });
        }

        private void RequestPermissions()
        {
            DirichletSdk.RequestPermissionIfNecessary();
            Log("已请求所需权限");
        }

        private bool EnsureInitialized()
        {
            if (DirichletSdk.IsInitialized)
            {
                return true;
            }

            Log("请先初始化 SDK");
            return false;
        }

        #endregion

        #region 广告操作

        private void LoadRewardAd()
        {
            if (!EnsureInitialized())
            {
                return;
            }

            if (adNative == null)
            {
                adNative = DirichletAdManager.CreateAdNative();
            }

            var slot = string.IsNullOrWhiteSpace(rewardSlotInput?.text) ? DefaultRewardSlot : rewardSlotInput.text.Trim();

            if (!long.TryParse(slot, out var spaceId))
            {
                Log($"激励广告位 ID 格式错误: {slot}");
                return;
            }

            CleanupAd(ref rewardAd);
            rewardShowButton.interactable = false;

            var userId = string.IsNullOrEmpty(SystemInfo.deviceUniqueIdentifier) ? DefaultUserId : SystemInfo.deviceUniqueIdentifier;

            var request = new DirichletAdRequest.Builder()
                .WithSpaceId(spaceId)
                .WithUserId(userId)
                .WithRewardName(DefaultRewardName)
                .WithRewardAmount(DefaultRewardAmount)
                .WithExtra1("unity-demo")
                .WithQuery("UnityDemo")
                .Build();

            adNative.LoadRewardVideoAd(request,
                ad =>
                {
                    rewardAd = ad;
                    AttachRewardEvents(rewardAd, slot);
                    Log($"激励广告加载成功 (slot={slot})");
                    rewardShowButton.interactable = true;
                },
                error =>
                {
                    Log($"激励广告加载失败: {error}");
                    rewardShowButton.interactable = false;
                });
        }

        private void ShowRewardAd()
        {
            if (rewardAd == null)
            {
                Log("请先加载激励广告");
                return;
            }

            var shown = rewardAd.Show();
            Log(shown ? "激励广告展示已调用" : "激励广告展示失败");
            rewardShowButton.interactable = false;
        }

        private void ShowRewardAutoAd()
        {
            if (!EnsureInitialized())
            {
                return;
            }

            if (adNative == null)
            {
                adNative = DirichletAdManager.CreateAdNative();
            }

            var slot = string.IsNullOrWhiteSpace(rewardSlotInput?.text) ? DefaultRewardSlot : rewardSlotInput.text.Trim();

            if (!long.TryParse(slot, out var spaceId))
            {
                Log($"激励广告位 ID 格式错误: {slot}");
                return;
            }

            var userId = string.IsNullOrEmpty(SystemInfo.deviceUniqueIdentifier) ? DefaultUserId : SystemInfo.deviceUniqueIdentifier;

            var request = new DirichletAdRequest.Builder()
                .WithSpaceId(spaceId)
                .WithUserId(userId)
                .WithRewardName(DefaultRewardName)
                .WithRewardAmount(DefaultRewardAmount)
                .WithExtra1("unity-demo")
                .WithQuery("UnityDemo")
                .Build();

            Log($"开始自动激励广告 (slot={slot})");
            rewardAutoButton.interactable = false;

            adNative.ShowRewardVideoAutoAd(request, new RewardAutoAdListener(this, slot));
        }

        private sealed class RewardAutoAdListener : IDirichletRewardVideoAutoAdListener
        {
            private readonly DirichletDemoController controller;
            private readonly string slot;

            public RewardAutoAdListener(DirichletDemoController controller, string slot)
            {
                this.controller = controller;
                this.slot = slot;
            }

            public void OnError(DirichletError error)
            {
                controller.Log($"自动激励({slot}) 失败: {error}");
                controller.rewardAutoButton.interactable = true;
            }

            public void OnAdShow()
            {
                controller.Log($"自动激励({slot}) 展示回调");
            }

            public void OnAdClose()
            {
                controller.Log($"自动激励({slot}) 关闭回调");
                controller.rewardAutoButton.interactable = true;
            }

            public void OnRewardVerify(DirichletRewardVerificationEventArgs args)
            {
                var state = args.IsVerified ? "验证通过" : "验证失败";
                controller.Log($"自动激励({slot}) 发奖回调: {state}, 数量={args.RewardAmount}, 奖励={args.RewardName}, Code={args.Code}, Message={args.Message}");
            }

            public void OnAdClick()
            {
                controller.Log($"自动激励({slot}) 点击回调");
            }
        }

        private void LoadInterstitialAd()
        {
            if (!EnsureInitialized())
            {
                return;
            }

            if (adNative == null)
            {
                adNative = DirichletAdManager.CreateAdNative();
            }

            var slot = string.IsNullOrWhiteSpace(interstitialSlotInput?.text) ? DefaultInterstitialSlot : interstitialSlotInput.text.Trim();

            if (!long.TryParse(slot, out var spaceId))
            {
                Log($"插屏广告位 ID 格式错误: {slot}");
                return;
            }

            CleanupAd(ref interstitialAd);
            interstitialShowButton.interactable = false;

            var request = new DirichletAdRequest.Builder()
                .WithSpaceId(spaceId)
                .WithQuery("UnityDemo")
                .Build();

            adNative.LoadInterstitialAd(request,
                ad =>
                {
                    interstitialAd = ad;
                    AttachInterstitialEvents(interstitialAd, slot);
                    Log($"插屏广告加载成功 (slot={slot})");
                    interstitialShowButton.interactable = true;
                },
                error =>
                {
                    Log($"插屏广告加载失败: {error}");
                    interstitialShowButton.interactable = false;
                });
        }

        private void ShowInterstitialAd()
        {
            if (interstitialAd == null)
            {
                Log("请先加载插屏广告");
                return;
            }

            var shown = interstitialAd.Show();
            Log(shown ? "插屏广告展示已调用" : "插屏广告展示失败");
            interstitialShowButton.interactable = false;
        }

        private void LoadBannerAd()
        {
            if (!EnsureInitialized())
            {
                return;
            }

            if (adNative == null)
            {
                adNative = DirichletAdManager.CreateAdNative();
            }

            var slot = string.IsNullOrWhiteSpace(bannerSlotInput?.text) ? DefaultBannerSlot : bannerSlotInput.text.Trim();

            if (!long.TryParse(slot, out var spaceId))
            {
                Log($"横幅广告位 ID 格式错误: {slot}");
                return;
            }

            CleanupAd(ref bannerAd);
            bannerShowButton.interactable = false;

            var requestBuilder = new DirichletAdRequest.Builder()
                .WithSpaceId(spaceId)
                .WithQuery("UnityDemo");

            if (DefaultBannerWidth > 0 && DefaultBannerHeight > 0)
            {
                requestBuilder = requestBuilder.WithExpressViewSize(DefaultBannerWidth, DefaultBannerHeight);
            }

            var request = requestBuilder
                .Build();

            adNative.LoadBannerAd(request,
                ad =>
                {
                    bannerAd = ad;
                    AttachBannerEvents(bannerAd, slot);
                    Log($"横幅广告加载成功 (slot={slot})");
                    bannerShowButton.interactable = true;
                },
                error =>
                {
                    Log($"横幅广告加载失败: {error}");
                    bannerShowButton.interactable = false;
                });
        }

        private void ShowBannerAd()
        {
            if (bannerAd == null)
            {
                Log("请先加载横幅广告");
                return;
            }

            var shown = bannerAd.Show();
            Log(shown ? "横幅广告展示已调用 (请在渠道 SDK 中查看渲染效果)" : "横幅广告展示失败");
            bannerShowButton.interactable = false;
        }

        private void LoadSplashAd()
        {
            if (!EnsureInitialized())
            {
                return;
            }

            if (adNative == null)
            {
                adNative = DirichletAdManager.CreateAdNative();
            }

            var slot = string.IsNullOrWhiteSpace(splashSlotInput?.text) ? DefaultSplashSlot : splashSlotInput.text.Trim();

            if (!long.TryParse(slot, out var spaceId))
            {
                Log($"开屏广告位 ID 格式错误: {slot}");
                return;
            }

            CleanupAd(ref splashAd);
            splashShowButton.interactable = false;

            var request = new DirichletAdRequest.Builder()
                .WithSpaceId(spaceId)
                .WithExpressViewSize(DefaultSplashWidth, DefaultSplashHeight)
                .WithQuery("UnityDemo")
                .Build();

            adNative.LoadSplashAd(request,
                ad =>
                {
                    splashAd = ad;
                    AttachSplashEvents(splashAd, slot);
                    Log($"开屏广告加载成功 (slot={slot})");
                    splashShowButton.interactable = true;
                },
                error =>
                {
                    Log($"开屏广告加载失败: {error}");
                    splashShowButton.interactable = false;
                });
        }

        private void ShowSplashAd()
        {
            if (splashAd == null)
            {
                Log("请先加载开屏广告");
                return;
            }

            var shown = splashAd.Show();
            Log(shown ? "开屏广告展示已调用" : "开屏广告展示失败");
            splashShowButton.interactable = false;
        }

        #endregion

        #region UI 构建

        private void SetupCanvas()
        {
            EnsureRoundedSprite();

            EnsureEventSystem();

            var canvasObj = new GameObject("DirichletDemoCanvas");
            var canvas = canvasObj.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.pixelPerfect = true;
            canvasRect = canvas.GetComponent<RectTransform>();

            safeAreaRect = new GameObject("SafeArea").AddComponent<RectTransform>();
            safeAreaRect.SetParent(canvasObj.transform, false);
            safeAreaRect.anchorMin = Vector2.zero;
            safeAreaRect.anchorMax = Vector2.one;
            safeAreaRect.offsetMin = Vector2.zero;
            safeAreaRect.offsetMax = Vector2.zero;
            safeAreaRect.pivot = new Vector2(0.5f, 0.5f);

            var scaler = canvasObj.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1080f, 1920f);
            scaler.matchWidthOrHeight = 0.5f;

            canvasObj.AddComponent<GraphicRaycaster>();

            var scrollObj = new GameObject("ScrollView");
            var scrollRectTransform = scrollObj.AddComponent<RectTransform>();
            var layoutRoot = (Transform)(safeAreaRect != null ? safeAreaRect : canvasRect);
            scrollRectTransform.SetParent(layoutRoot, false);
            scrollRectTransform.anchorMin = Vector2.zero;
            scrollRectTransform.anchorMax = Vector2.one;
            scrollRectTransform.offsetMin = Vector2.zero;
            scrollRectTransform.offsetMax = Vector2.zero;

            var background = scrollObj.AddComponent<Image>();
            background.color = new Color(0.07f, 0.1f, 0.15f, 1f);

            var scrollRect = scrollObj.AddComponent<ScrollRect>();
            scrollRect.horizontal = false;
            scrollRect.vertical = true;
            scrollRect.movementType = ScrollRect.MovementType.Clamped;
            scrollRect.scrollSensitivity = 28f;

            var viewport = new GameObject("Viewport").AddComponent<RectTransform>();
            viewport.SetParent(scrollRectTransform, false);
            viewport.anchorMin = Vector2.zero;
            viewport.anchorMax = Vector2.one;
            viewport.offsetMin = Vector2.zero;
            viewport.offsetMax = Vector2.zero;
            viewport.pivot = new Vector2(0.5f, 0.5f);
            viewport.gameObject.AddComponent<RectMask2D>();

            var viewportImage = viewport.gameObject.AddComponent<Image>();
            viewportImage.color = new Color(0f, 0f, 0f, 0f);

            var content = new GameObject("Content").AddComponent<RectTransform>();
            content.SetParent(viewport, false);
            content.anchorMin = new Vector2(0f, 1f);
            content.anchorMax = new Vector2(1f, 1f);
            content.pivot = new Vector2(0.5f, 1f);
            content.offsetMin = Vector2.zero;
            content.offsetMax = Vector2.zero;

            var contentLayout = content.gameObject.AddComponent<VerticalLayoutGroup>();
            contentLayout.padding = new RectOffset(36, 36, 48, 72);
            contentLayout.spacing = 32f;
            contentLayout.childControlWidth = true;
            contentLayout.childForceExpandWidth = true;
            contentLayout.childControlHeight = true;
            contentLayout.childForceExpandHeight = false;

            scrollRect.viewport = viewport;
            scrollRect.content = content;

            CreateHeroSection(content);
            CreateInitializationSection(content);
            CreateRewardSection(content);
            CreateInterstitialSection(content);
            CreateBannerSection(content);
            CreateSplashSection(content);
            CreateAudioProbeSection(content);

            Canvas.ForceUpdateCanvases();
            LayoutRebuilder.ForceRebuildLayoutImmediate(content);
            var preferredHeight = LayoutUtility.GetPreferredHeight(content);
            content.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, preferredHeight);

            CreateStatusOverlay();
            ApplySafeArea(true);
        }

        private void Update()
        {
            ApplySafeArea();
        }

        private void ApplySafeArea(bool force = false)
        {
            if (safeAreaRect == null || canvasRect == null)
            {
                return;
            }

            var safeArea = Screen.safeArea;
            var screenWidth = Mathf.Max(Screen.width, 1);
            var screenHeight = Mathf.Max(Screen.height, 1);
            var orientation = Screen.orientation;

            if (!force &&
                safeArea == cachedSafeArea &&
                cachedScreenSize.x == screenWidth &&
                cachedScreenSize.y == screenHeight &&
                cachedOrientation == orientation)
            {
                return;
            }

            cachedSafeArea = safeArea;
            cachedScreenSize = new Vector2Int(screenWidth, screenHeight);
            cachedOrientation = orientation;

            var anchorMin = safeArea.position;
            var anchorMax = safeArea.position + safeArea.size;
            anchorMin.x /= screenWidth;
            anchorMin.y /= screenHeight;
            anchorMax.x /= screenWidth;
            anchorMax.y /= screenHeight;

            safeAreaRect.anchorMin = anchorMin;
            safeAreaRect.anchorMax = anchorMax;
            safeAreaRect.offsetMin = Vector2.zero;
            safeAreaRect.offsetMax = Vector2.zero;
        }

        private void EnsureRoundedSprite()
        {
            if (roundedSprite != null)
            {
                return;
            }

            if (roundedTexture == null)
            {
                const int size = 8;
                roundedTexture = new Texture2D(size, size, TextureFormat.ARGB32, false)
                {
                    hideFlags = HideFlags.HideAndDontSave
                };

                var fill = Color.white;
                for (int y = 0; y < size; y++)
                {
                    for (int x = 0; x < size; x++)
                    {
                        roundedTexture.SetPixel(x, y, fill);
                    }
                }
                roundedTexture.Apply();
            }

            roundedSprite = Sprite.Create(
                roundedTexture,
                new Rect(0f, 0f, roundedTexture.width, roundedTexture.height),
                new Vector2(0.5f, 0.5f),
                roundedTexture.width);
            roundedSprite.hideFlags = HideFlags.HideAndDontSave;
        }

        private void CreateHeroSection(Transform parent)
        {
            var hero = new GameObject("Hero").AddComponent<RectTransform>();
            hero.SetParent(parent, false);
            hero.anchorMin = new Vector2(0f, 1f);
            hero.anchorMax = new Vector2(1f, 1f);
            hero.pivot = new Vector2(0.5f, 1f);
            hero.offsetMin = Vector2.zero;
            hero.offsetMax = Vector2.zero;

            var layout = hero.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(0, 0, 8, 16);
            layout.spacing = 6f;
            layout.childAlignment = TextAnchor.UpperLeft;
            layout.childControlWidth = true;
            layout.childForceExpandWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandHeight = false;

            var element = hero.gameObject.AddComponent<LayoutElement>();
            element.flexibleWidth = 1f;
            element.flexibleHeight = 0f;

            CreateText(hero, "HeroTitle", "Dirichlet 聚合 SDK", 60, FontStyle.Bold, Color.white, TextAnchor.MiddleLeft, true, 34, 64);
            CreateText(hero, "HeroSubtitle", "跨渠道广告能力一站式演示", 32, FontStyle.Normal, new Color(0.73f, 0.78f, 0.84f, 1f), TextAnchor.MiddleLeft, true, 20, 36);
        }

        private void CreateInitializationSection(Transform parent)
        {
            var section = CreateSection(parent, "InitSection", out var layoutGroup, "初始化配置");
            layoutGroup.padding = new RectOffset(36, 36, 36, 36);
            layoutGroup.spacing = 22f;

            mediaIdInput = CreateLabeledInput(section, "MediaId", "Media ID", DefaultMediaId);
            channelInput = CreateLabeledInput(section, "Channel", "渠道标识", DefaultChannel);
            subChannelInput = CreateLabeledInput(section, "SubChannel", "子渠道", DefaultSubChannel);
            debugToggle = CreateToggle(section, "DebugToggle", "开启调试日志", true);

            var buttonRow = CreateHorizontalGroup(section, "InitButtons");
            initButton = CreateButton(buttonRow, "InitButton", "初始化 SDK");
            requestPermissionButton = CreateButton(buttonRow, "PermissionButton", "请求权限");
            requestPermissionButton.interactable = false;
        }

        private void CreateRewardSection(Transform parent)
        {
            var section = CreateSection(parent, "RewardSection", out var layoutGroup, "激励视频广告");
            layoutGroup.padding = new RectOffset(36, 36, 36, 36);
            layoutGroup.spacing = 22f;

            rewardSlotInput = CreateLabeledInput(section, "RewardSlot", "广告位 ID", DefaultRewardSlot);
            var row = CreateHorizontalGroup(section, "RewardButtons");
            rewardLoadButton = CreateButton(row, "RewardLoad", "加载激励");
            rewardShowButton = CreateButton(row, "RewardShow", "展示激励");
            rewardLoadButton.interactable = false;
            rewardShowButton.interactable = false;

            // Auto reward ad button (iOS/Android)
            var autoRow = CreateHorizontalGroup(section, "RewardAutoButtons");
            rewardAutoButton = CreateButton(autoRow, "RewardAuto", "自动激励");
            rewardAutoButton.interactable = false;
        }

        private void CreateInterstitialSection(Transform parent)
        {
            var section = CreateSection(parent, "InterstitialSection", out var layoutGroup, "插屏广告");
            layoutGroup.padding = new RectOffset(36, 36, 36, 36);
            layoutGroup.spacing = 22f;

            interstitialSlotInput = CreateLabeledInput(section, "InterstitialSlot", "广告位 ID", DefaultInterstitialSlot);
            var row = CreateHorizontalGroup(section, "InterstitialButtons");
            interstitialLoadButton = CreateButton(row, "InterstitialLoad", "加载插屏");
            interstitialShowButton = CreateButton(row, "InterstitialShow", "展示插屏");
            interstitialLoadButton.interactable = false;
            interstitialShowButton.interactable = false;
        }

        private void CreateBannerSection(Transform parent)
        {
            var section = CreateSection(parent, "BannerSection", out var layoutGroup, "横幅广告");
            layoutGroup.padding = new RectOffset(36, 36, 36, 36);
            layoutGroup.spacing = 22f;

            bannerSlotInput = CreateLabeledInput(section, "BannerSlot", "广告位 ID", DefaultBannerSlot);
            var row = CreateHorizontalGroup(section, "BannerButtons");
            bannerLoadButton = CreateButton(row, "BannerLoad", "加载横幅");
            bannerShowButton = CreateButton(row, "BannerShow", "展示横幅");
            bannerLoadButton.interactable = false;
            bannerShowButton.interactable = false;
        }

        private void CreateSplashSection(Transform parent)
        {
            var section = CreateSection(parent, "SplashSection", out var layoutGroup, "开屏广告");
            layoutGroup.padding = new RectOffset(36, 36, 36, 36);
            layoutGroup.spacing = 22f;

            splashSlotInput = CreateLabeledInput(section, "SplashSlot", "广告位 ID", DefaultSplashSlot);
            var row = CreateHorizontalGroup(section, "SplashButtons");
            splashLoadButton = CreateButton(row, "SplashLoad", "加载开屏");
            splashShowButton = CreateButton(row, "SplashShow", "展示开屏");
            splashLoadButton.interactable = false;
            splashShowButton.interactable = false;
        }

        private void CreateAudioProbeSection(Transform parent)
        {
            var section = CreateSection(parent, "AudioProbeSection", out var layoutGroup, "背景音频探针");
            layoutGroup.padding = new RectOffset(36, 36, 36, 36);
            layoutGroup.spacing = 22f;

            var descText = CreateText(section, "AudioProbeDesc",
                "播放 440Hz 正弦波背景音，用于验证广告展示/关闭后宿主音频是否自动恢复。",
                26, FontStyle.Normal, new Color(0.7f, 0.75f, 0.8f), TextAnchor.MiddleLeft, true, 22, 24);

            audioProbeStatusText = CreateText(section, "AudioProbeStatus",
                "状态: 未播放",
                28, FontStyle.Bold, new Color(0.6f, 0.85f, 0.6f), TextAnchor.MiddleLeft, true, 22, 24);

            var row = CreateHorizontalGroup(section, "AudioProbeButtons");
            audioProbeToggleButton = CreateButton(row, "AudioProbeToggle", "播放背景音");

            audioProbeToggleButton.onClick.AddListener(ToggleAudioProbe);
        }

        private void ToggleAudioProbe()
        {
            if (audioProbeRunning)
            {
                StopAudioProbe();
            }
            else
            {
                StartAudioProbe();
            }
        }

        private void StartAudioProbe()
        {
            if (audioProbeSource == null)
            {
                audioProbeSource = gameObject.AddComponent<AudioSource>();
                audioProbeSource.loop = true;
                audioProbeSource.volume = 0.3f;
                audioProbeSource.playOnAwake = false;

                // 生成 1 秒 440Hz 正弦波
                const int sampleRate = 44100;
                const float frequency = 440f;
                var samples = new float[sampleRate];
                for (int i = 0; i < sampleRate; i++)
                {
                    samples[i] = Mathf.Sin(2f * Mathf.PI * frequency * i / sampleRate);
                }

                var clip = AudioClip.Create("AudioProbe440Hz", sampleRate, 1, sampleRate, false);
                clip.SetData(samples, 0);
                audioProbeSource.clip = clip;
            }

            audioProbeSource.Play();
            audioProbeRunning = true;
            UpdateAudioProbeUI();
            Log("背景音频探针: 已开始播放 440Hz 正弦波");
        }

        private void StopAudioProbe()
        {
            if (audioProbeSource != null)
            {
                audioProbeSource.Stop();
            }

            audioProbeRunning = false;
            UpdateAudioProbeUI();
            Log("背景音频探针: 已停止播放");
        }

        private void UpdateAudioProbeUI()
        {
            if (audioProbeToggleButton != null)
            {
                var label = audioProbeToggleButton.GetComponentInChildren<Text>();
                if (label != null)
                {
                    label.text = audioProbeRunning ? "停止背景音" : "播放背景音";
                }
            }

            if (audioProbeStatusText != null)
            {
                audioProbeStatusText.text = audioProbeRunning ? "状态: 正在播放 440Hz" : "状态: 未播放";
                audioProbeStatusText.color = audioProbeRunning
                    ? new Color(0.3f, 0.9f, 0.3f)
                    : new Color(0.6f, 0.85f, 0.6f);
            }
        }

        private void CreateStatusOverlay()
        {
            var parentRect = safeAreaRect != null ? safeAreaRect : canvasRect;
            if (parentRect == null)
            {
                return;
            }

            statusOverlay = new GameObject("StatusOverlay").AddComponent<RectTransform>();
            statusOverlay.SetParent(parentRect, false);
            statusOverlay.anchorMin = new Vector2(1f, 1f);
            statusOverlay.anchorMax = new Vector2(1f, 1f);
            statusOverlay.pivot = new Vector2(1f, 1f);
            statusOverlay.anchoredPosition = new Vector2(-32f, -32f);
            statusOverlay.sizeDelta = new Vector2(STATUS_OVERLAY_WIDTH, STATUS_OVERLAY_HEIGHT);
            statusOverlay.SetSiblingIndex(parentRect.childCount - 1);

            EnsureRoundedSprite();

            var overlayBackground = statusOverlay.gameObject.AddComponent<Image>();
            overlayBackground.color = new Color(0.07f, 0.09f, 0.12f, 0.92f);
            if (roundedSprite != null)
            {
                overlayBackground.sprite = roundedSprite;
                overlayBackground.type = Image.Type.Sliced;
            }
            overlayBackground.raycastTarget = true;

            var overlayLayout = statusOverlay.gameObject.AddComponent<VerticalLayoutGroup>();
            overlayLayout.padding = new RectOffset(18, 18, 20, 12);
            overlayLayout.spacing = 10f;
            overlayLayout.childAlignment = TextAnchor.UpperLeft;
            overlayLayout.childControlWidth = true;
            overlayLayout.childForceExpandWidth = true;
            overlayLayout.childControlHeight = true;
            overlayLayout.childForceExpandHeight = false;

            var header = new GameObject("Header").AddComponent<RectTransform>();
            header.SetParent(statusOverlay, false);
            var headerElement = header.gameObject.AddComponent<LayoutElement>();
            headerElement.minHeight = 56f;
            headerElement.flexibleHeight = 0f;

            var headerLayout = header.gameObject.AddComponent<HorizontalLayoutGroup>();
            headerLayout.spacing = 12f;
            headerLayout.childAlignment = TextAnchor.MiddleLeft;
            headerLayout.childControlWidth = false;
            headerLayout.childForceExpandWidth = false;
            headerLayout.childControlHeight = true;
            headerLayout.childForceExpandHeight = false;

            var iconContainer = new GameObject("Icon").AddComponent<RectTransform>();
            iconContainer.SetParent(header, false);
            iconContainer.sizeDelta = new Vector2(48f, 48f);
            var iconLayout = iconContainer.gameObject.AddComponent<LayoutElement>();
            iconLayout.minWidth = 48f;
            iconLayout.minHeight = 48f;

            var iconBackground = iconContainer.gameObject.AddComponent<Image>();
            iconBackground.color = new Color(0.18f, 0.22f, 0.28f, 1f);
            if (roundedSprite != null)
            {
                iconBackground.sprite = roundedSprite;
                iconBackground.type = Image.Type.Sliced;
            }

            var iconText = CreateText(iconContainer, "IconText", "LOG", 20, FontStyle.Bold, Color.white, TextAnchor.MiddleCenter, true, 14, 24);
            var iconRect = iconText.rectTransform;
            iconRect.anchorMin = Vector2.zero;
            iconRect.anchorMax = Vector2.one;
            iconRect.offsetMin = Vector2.zero;
            iconRect.offsetMax = Vector2.zero;
            var iconTextLayout = iconText.GetComponent<LayoutElement>();
            if (iconTextLayout != null)
            {
                iconTextLayout.flexibleWidth = 0f;
                iconTextLayout.flexibleHeight = 0f;
                iconTextLayout.minHeight = 0f;
            }

            var titleContainer = new GameObject("TitleContainer").AddComponent<RectTransform>();
            titleContainer.SetParent(header, false);
            var titleLayout = titleContainer.gameObject.AddComponent<LayoutElement>();
            titleLayout.flexibleWidth = 1f;

            var titleGroup = titleContainer.gameObject.AddComponent<VerticalLayoutGroup>();
            titleGroup.spacing = 2f;
            titleGroup.childAlignment = TextAnchor.MiddleLeft;
            titleGroup.childControlWidth = true;
            titleGroup.childForceExpandWidth = true;
            titleGroup.childControlHeight = true;
            titleGroup.childForceExpandHeight = false;

            var headerTitle = CreateText(titleContainer, "StatusTitle", "状态日志", 28, FontStyle.Bold, Color.white, TextAnchor.MiddleLeft, true, 20, 28);
            var headerTitleLayout = headerTitle.GetComponent<LayoutElement>();
            if (headerTitleLayout != null)
            {
                headerTitleLayout.flexibleWidth = 1f;
                headerTitleLayout.minHeight = 28f;
            }

            var toggleRect = new GameObject("ToggleButton").AddComponent<RectTransform>();
            toggleRect.SetParent(header, false);
            toggleRect.sizeDelta = new Vector2(160f, 52f);
            var toggleElement = toggleRect.gameObject.AddComponent<LayoutElement>();
            toggleElement.minWidth = 150f;
            toggleElement.preferredHeight = 52f;

            var toggleBackground = toggleRect.gameObject.AddComponent<Image>();
            toggleBackground.color = new Color(0.22f, 0.27f, 0.34f, 1f);
            if (roundedSprite != null)
            {
                toggleBackground.sprite = roundedSprite;
                toggleBackground.type = Image.Type.Sliced;
            }

            var toggleButton = toggleRect.gameObject.AddComponent<Button>();
            var toggleColors = toggleButton.colors;
            toggleColors.normalColor = toggleBackground.color;
            toggleColors.highlightedColor = new Color(0.24f, 0.29f, 0.36f, 1f);
            toggleColors.pressedColor = new Color(0.12f, 0.15f, 0.19f, 1f);
            toggleColors.selectedColor = toggleColors.highlightedColor;
            toggleColors.colorMultiplier = 1f;
            toggleButton.colors = toggleColors;

            statusToggleLabel = CreateText(toggleRect, "Label", "收起日志", 26, FontStyle.Normal, Color.white, TextAnchor.MiddleCenter, true, 18, 26);
            var toggleLabelRect = statusToggleLabel.rectTransform;
            toggleLabelRect.anchorMin = Vector2.zero;
            toggleLabelRect.anchorMax = Vector2.one;
            toggleLabelRect.offsetMin = Vector2.zero;
            toggleLabelRect.offsetMax = Vector2.zero;
            var toggleLabelLayout = statusToggleLabel.GetComponent<LayoutElement>();
            if (toggleLabelLayout != null)
            {
                toggleLabelLayout.flexibleWidth = 0f;
                toggleLabelLayout.flexibleHeight = 0f;
                toggleLabelLayout.minHeight = 0f;
            }

            toggleButton.onClick.AddListener(ToggleStatusPanel);

            var dragTrigger = statusOverlay.gameObject.GetComponent<EventTrigger>();
            if (dragTrigger == null)
            {
                dragTrigger = statusOverlay.gameObject.AddComponent<EventTrigger>();
            }

            var pointerDownEntry = new EventTrigger.Entry { eventID = EventTriggerType.PointerDown };
            pointerDownEntry.callback.AddListener(evt => OnStatusOverlayPointerDown((PointerEventData)evt));
            dragTrigger.triggers.Add(pointerDownEntry);

            var dragEntry = new EventTrigger.Entry { eventID = EventTriggerType.Drag };
            dragEntry.callback.AddListener(evt => OnStatusOverlayDrag((PointerEventData)evt));
            dragTrigger.triggers.Add(dragEntry);

            statusBody = new GameObject("Body").AddComponent<RectTransform>();
            statusBody.SetParent(statusOverlay, false);
            var bodyElement = statusBody.gameObject.AddComponent<LayoutElement>();
            bodyElement.flexibleHeight = 1f;
            bodyElement.minHeight = 210f;

            var bodyBackground = statusBody.gameObject.AddComponent<Image>();
            bodyBackground.color = new Color(0.11f, 0.13f, 0.17f, 1f);
            if (roundedSprite != null)
            {
                bodyBackground.sprite = roundedSprite;
                bodyBackground.type = Image.Type.Sliced;
            }

            var viewport = new GameObject("Viewport").AddComponent<RectTransform>();
            viewport.SetParent(statusBody, false);
            viewport.anchorMin = Vector2.zero;
            viewport.anchorMax = Vector2.one;
            viewport.offsetMin = new Vector2(12f, 18f);
            viewport.offsetMax = new Vector2(-12f, -12f);
            viewport.pivot = new Vector2(0.5f, 0.5f);
            viewport.gameObject.AddComponent<RectMask2D>();
            var viewportBackground = viewport.gameObject.AddComponent<Image>();
            viewportBackground.color = new Color(0f, 0f, 0f, 0f);

            var content = new GameObject("Content").AddComponent<RectTransform>();
            content.SetParent(viewport, false);
            content.anchorMin = new Vector2(0f, 1f);
            content.anchorMax = new Vector2(1f, 1f);
            content.pivot = new Vector2(0.5f, 1f);
            content.offsetMin = Vector2.zero;
            content.offsetMax = Vector2.zero;

            var contentLayout = content.gameObject.AddComponent<VerticalLayoutGroup>();
            contentLayout.padding = new RectOffset(0, 0, 0, 32);
            contentLayout.spacing = 12f;
            contentLayout.childAlignment = TextAnchor.UpperLeft;
            contentLayout.childControlWidth = true;
            contentLayout.childForceExpandWidth = true;
            contentLayout.childControlHeight = true;
            contentLayout.childForceExpandHeight = false;

            var contentFitter = content.gameObject.AddComponent<ContentSizeFitter>();
            contentFitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            contentFitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;

            var logContainer = new GameObject("StatusText").AddComponent<RectTransform>();
            logContainer.SetParent(content, false);

            statusText = logContainer.gameObject.AddComponent<Text>();
            statusText.font = ResolveUIFont();
            statusText.fontSize = 26;
            statusText.color = new Color(0.82f, 0.86f, 0.92f, 1f);
            statusText.alignment = TextAnchor.UpperLeft;
            statusText.horizontalOverflow = HorizontalWrapMode.Wrap;
            statusText.verticalOverflow = VerticalWrapMode.Overflow;
            statusText.raycastTarget = false;
            statusText.text = "就绪";

            statusScroll = statusBody.gameObject.AddComponent<ScrollRect>();
            statusScroll.viewport = viewport;
            statusScroll.content = content;
            statusScroll.horizontal = false;
            statusScroll.vertical = true;
            statusScroll.movementType = ScrollRect.MovementType.Clamped;
            statusScroll.scrollSensitivity = 16f;
            statusScroll.decelerationRate = 0.135f;
            statusScroll.inertia = true;
            statusScroll.verticalNormalizedPosition = 0f;

            statusPanelCollapsed = false;
            statusUnreadCount = 0;
        }

        private void OnStatusOverlayPointerDown(PointerEventData eventData)
        {
            var boundsRect = safeAreaRect != null ? safeAreaRect : canvasRect;
            if (boundsRect == null || statusOverlay == null)
            {
                return;
            }

            if (RectTransformUtility.ScreenPointToLocalPointInRectangle(boundsRect, eventData.position, eventData.pressEventCamera, out var localPoint))
            {
                statusDragPointerOffset = statusOverlay.anchoredPosition - localPoint;
            }
        }

        private void OnStatusOverlayDrag(PointerEventData eventData)
        {
            var boundsRect = safeAreaRect != null ? safeAreaRect : canvasRect;
            if (boundsRect == null || statusOverlay == null)
            {
                return;
            }

            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(boundsRect, eventData.position, eventData.pressEventCamera, out var localPoint))
            {
                return;
            }

            var target = localPoint + statusDragPointerOffset;

            var canvasBounds = boundsRect.rect;
            var currentWidth = statusOverlay.sizeDelta.x;
            var currentHeight = statusOverlay.sizeDelta.y;

            var minX = -canvasBounds.width + currentWidth;
            var maxX = 0f;
            var minY = -canvasBounds.height + currentHeight;
            var maxY = 0f;

            target.x = Mathf.Clamp(target.x, minX, maxX);
            target.y = Mathf.Clamp(target.y, minY, maxY);

            statusOverlay.anchoredPosition = target;
        }

        private void ToggleStatusPanel()
        {
            if (statusOverlay == null || statusBody == null)
            {
                return;
            }

            statusPanelCollapsed = !statusPanelCollapsed;
            var isCollapsed = statusPanelCollapsed;

            statusBody.gameObject.SetActive(!isCollapsed);

            if (statusScroll != null)
            {
                statusScroll.enabled = !isCollapsed;
            }

            var size = statusOverlay.sizeDelta;
            size.y = isCollapsed ? STATUS_OVERLAY_COLLAPSED_HEIGHT : STATUS_OVERLAY_HEIGHT;
            statusOverlay.sizeDelta = size;
            Canvas.ForceUpdateCanvases();

            if (isCollapsed)
            {
                if (statusToggleLabel != null)
                {
                    statusToggleLabel.text = statusUnreadCount > 0 ? $"展开日志 (+{statusUnreadCount})" : "展开日志";
                }
            }
            else
            {
                statusUnreadCount = 0;

                if (statusToggleLabel != null)
                {
                    statusToggleLabel.text = "收起日志";
                }

                if (statusScroll != null)
                {
                    Canvas.ForceUpdateCanvases();
                    statusScroll.verticalNormalizedPosition = 0f;
                }
            }
        }

        private RectTransform CreateSection(Transform parent, string name, out VerticalLayoutGroup layoutGroup, string title = null)
        {
            var section = new GameObject(name).AddComponent<RectTransform>();
            section.SetParent(parent, false);
            section.anchorMin = new Vector2(0f, 1f);
            section.anchorMax = new Vector2(1f, 1f);
            section.pivot = new Vector2(0.5f, 1f);
            section.offsetMin = Vector2.zero;
            section.offsetMax = Vector2.zero;

            var image = section.gameObject.AddComponent<Image>();
            image.color = new Color(0.12f, 0.15f, 0.19f, 1f);
            if (roundedSprite != null)
            {
                image.sprite = roundedSprite;
                image.type = Image.Type.Sliced;
            }

            layoutGroup = section.gameObject.AddComponent<VerticalLayoutGroup>();
            layoutGroup.padding = new RectOffset(28, 28, 28, 28);
            layoutGroup.spacing = 18f;
            layoutGroup.childControlWidth = true;
            layoutGroup.childForceExpandWidth = true;
            layoutGroup.childControlHeight = true;
            layoutGroup.childForceExpandHeight = false;

            var element = section.gameObject.AddComponent<LayoutElement>();
            element.flexibleWidth = 1f;
            element.flexibleHeight = 0f;

            if (!string.IsNullOrEmpty(title))
            {
                var titleText = CreateText(section, name + "Title", title, 46, FontStyle.Bold, new Color(0f, 0.83f, 1f, 1f));
                titleText.alignment = TextAnchor.MiddleLeft;
            }

            return section;
        }

        private InputField CreateLabeledInput(Transform parent, string name, string label, string defaultValue)
        {
            var container = new GameObject(name).AddComponent<RectTransform>();
            container.SetParent(parent, false);
            container.anchorMin = new Vector2(0f, 1f);
            container.anchorMax = new Vector2(1f, 1f);
            container.pivot = new Vector2(0.5f, 1f);
            container.offsetMin = Vector2.zero;
            container.offsetMax = Vector2.zero;

            var layout = container.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(0, 0, 0, 0);
            layout.spacing = 8f;
            layout.childControlWidth = true;
            layout.childForceExpandWidth = true;

            CreateText(container, name + "Label", label, 30, FontStyle.Normal, new Color(0.82f, 0.86f, 0.92f, 1f));

            var inputRect = new GameObject(name + "Input").AddComponent<RectTransform>();
            inputRect.SetParent(container, false);
            inputRect.anchorMin = new Vector2(0f, 1f);
            inputRect.anchorMax = new Vector2(1f, 1f);
            inputRect.pivot = new Vector2(0.5f, 1f);
            inputRect.offsetMin = Vector2.zero;
            inputRect.offsetMax = Vector2.zero;

            var image = inputRect.gameObject.AddComponent<Image>();
            image.color = new Color(0.18f, 0.23f, 0.31f, 1f);
            if (roundedSprite != null)
            {
                image.sprite = roundedSprite;
                image.type = Image.Type.Sliced;
            }

            var inputField = inputRect.gameObject.AddComponent<InputField>();

            var text = CreateText(inputRect, "Text", defaultValue, 30, FontStyle.Normal, Color.white);
            text.rectTransform.anchorMin = new Vector2(0f, 0f);
            text.rectTransform.anchorMax = new Vector2(1f, 1f);
            text.rectTransform.offsetMin = new Vector2(14f, 14f);
            text.rectTransform.offsetMax = new Vector2(-14f, -14f);

            var placeholder = CreateText(inputRect, "Placeholder", "请输入", 30, FontStyle.Italic, new Color(0.55f, 0.6f, 0.65f, 1f));
            placeholder.rectTransform.anchorMin = new Vector2(0f, 0f);
            placeholder.rectTransform.anchorMax = new Vector2(1f, 1f);
            placeholder.rectTransform.offsetMin = new Vector2(14f, 14f);
            placeholder.rectTransform.offsetMax = new Vector2(-14f, -14f);

            inputField.textComponent = text;
            inputField.placeholder = placeholder;
            inputField.text = defaultValue;

            var element = inputRect.gameObject.AddComponent<LayoutElement>();
            element.flexibleWidth = 1f;
            element.minHeight = 80f;

            return inputField;
        }

        private Toggle CreateToggle(Transform parent, string name, string label, bool defaultValue)
        {
            var container = new GameObject(name).AddComponent<RectTransform>();
            container.SetParent(parent, false);
            container.anchorMin = new Vector2(0f, 1f);
            container.anchorMax = new Vector2(1f, 1f);
            container.pivot = new Vector2(0.5f, 1f);
            container.offsetMin = Vector2.zero;
            container.offsetMax = Vector2.zero;

            var layout = container.gameObject.AddComponent<HorizontalLayoutGroup>();
            layout.spacing = 16f;
            layout.childAlignment = TextAnchor.MiddleLeft;
            layout.childControlWidth = true;
            layout.childForceExpandWidth = false;

            var background = new GameObject("Background").AddComponent<RectTransform>();
            background.SetParent(container, false);
            background.sizeDelta = new Vector2(48f, 48f);

            var bgImage = background.gameObject.AddComponent<Image>();
            bgImage.color = new Color(0.18f, 0.22f, 0.28f, 1f);
            if (roundedSprite != null)
            {
                bgImage.sprite = roundedSprite;
                bgImage.type = Image.Type.Sliced;
            }

            var checkmark = new GameObject("Checkmark").AddComponent<RectTransform>();
            checkmark.SetParent(background, false);
            checkmark.anchorMin = new Vector2(0.2f, 0.2f);
            checkmark.anchorMax = new Vector2(0.8f, 0.8f);
            checkmark.offsetMin = Vector2.zero;
            checkmark.offsetMax = Vector2.zero;

            var checkImage = checkmark.gameObject.AddComponent<Image>();
            checkImage.color = new Color(0f, 0.76f, 1f, 1f);

            var toggle = container.gameObject.AddComponent<Toggle>();
            toggle.targetGraphic = bgImage;
            toggle.graphic = checkImage;
            toggle.isOn = defaultValue;

            var toggleColors = toggle.colors;
            toggleColors.normalColor = new Color(0.18f, 0.22f, 0.28f, 1f);
            toggleColors.highlightedColor = new Color(0.22f, 0.28f, 0.35f, 1f);
            toggleColors.pressedColor = new Color(0f, 0.76f, 1f, 1f);
            toggleColors.selectedColor = new Color(0f, 0.76f, 1f, 1f);
            toggle.colors = toggleColors;

            CreateText(container, name + "Label", label, 30, FontStyle.Normal, new Color(0.82f, 0.86f, 0.92f, 1f));

            return toggle;
        }

        private RectTransform CreateHorizontalGroup(Transform parent, string name)
        {
            var container = new GameObject(name).AddComponent<RectTransform>();
            container.SetParent(parent, false);
            container.anchorMin = new Vector2(0f, 1f);
            container.anchorMax = new Vector2(1f, 1f);
            container.pivot = new Vector2(0.5f, 1f);
            container.offsetMin = Vector2.zero;
            container.offsetMax = Vector2.zero;

            var layout = container.gameObject.AddComponent<HorizontalLayoutGroup>();
            layout.spacing = 18f;
            layout.padding = new RectOffset(0, 0, 0, 0);
            layout.childAlignment = TextAnchor.MiddleLeft;
            layout.childControlWidth = true;
            layout.childForceExpandWidth = true;

            return container;
        }

        // 标记是否使用静态字体（静态字体支持 Best Fit，动态字体不支持）
        private bool useStaticFont;
        
        private Font ResolveUIFont()
        {
            if (uiFont != null)
            {
                return uiFont;
            }

            // 优先尝试加载 Resources 中的静态字体
            uiFont = Resources.Load<Font>("DirichletMediationFonts/NotoSansSC-Regular");
            if (uiFont != null)
            {
                useStaticFont = true;
                Debug.Log($"[Dirichlet Demo] Using static font: {uiFont.name}");
                return uiFont;
            }
            
            // 静态字体不存在，回退到系统动态字体
            useStaticFont = false;
            var candidates = GetPlatformFontCandidates();
            try
            {
                uiFont = Font.CreateDynamicFontFromOSFont(candidates, 32);
                if (uiFont != null)
                {
                    uiFont.hideFlags = HideFlags.HideAndDontSave;
                    Debug.Log($"[Dirichlet Demo] Using dynamic system font: {uiFont.name}");
                    return uiFont;
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[Dirichlet Demo] Failed to load system font ({string.Join(", ", candidates)}): {ex.Message}");
            }

            Debug.LogWarning("[Dirichlet Demo] Falling back to built-in Arial font");
            uiFont = Resources.GetBuiltinResource<Font>("Arial.ttf");
            return uiFont;
        }
        
        private static string[] GetPlatformFontCandidates()
        {
#if UNITY_IOS && !UNITY_EDITOR
            // iOS 字体名称需要使用 PostScript 名称或带点前缀的系统字体名
            return new[]
            {
                ".PingFang SC",           // iOS 系统苹方字体（带点前缀）
                "PingFangSC-Regular",     // 苹方常规体 PostScript 名称
                "PingFangSC-Medium",      // 苹方中等体
                "STHeitiSC-Light",        // 华文黑体
                "STHeitiSC-Medium",
                "Heiti SC",               // 黑体-简
                ".SF UI Text"             // San Francisco 字体
            };
#elif UNITY_ANDROID && !UNITY_EDITOR
            return new[]
            {
                "Noto Sans CJK SC",
                "Droid Sans Fallback",
                "Roboto",
                "sans-serif"
            };
#else
            return new[]
            {
                "PingFang SC",
                "Microsoft YaHei",
                "SimHei",
                "Arial Unicode MS",
                "Arial"
            };
#endif
        }
        
        /// <summary>
        /// 预加载中文字符到动态字体图集，确保所有 UI 文本能正确渲染
        /// </summary>
        private void PreloadChineseCharacters(Font font)
        {
            if (font == null || !font.dynamic) return;
            
            // 收集 Demo UI 中使用的所有中文字符
            const string chineseChars = 
                "初始化配置渠道标识子开启调试日志请求权限" +
                "激励视频广告位加载展示插屏横幅开屏聚合" +
                "成功失败错误状态信息版本号收起" +
                "金币奖励数量用户跨能力一站式演示";
            
            // 预请求不同字号和样式的字符，确保覆盖 UI 中的各种文本样式
            var sizes = new[] { 26, 28, 30, 32, 34, 36, 40, 46, 60 };
            var styles = new[] { FontStyle.Normal, FontStyle.Bold };
            
            foreach (var size in sizes)
            {
                foreach (var style in styles)
                {
                    font.RequestCharactersInTexture(chineseChars, size, style);
                }
            }
            
            Debug.Log($"[Dirichlet Demo] Preloaded {chineseChars.Length} Chinese characters into font atlas");
        }

        private Text CreateText(Transform parent, string name, string value, int fontSize, FontStyle style, Color color, TextAnchor alignment = TextAnchor.MiddleLeft, bool enableBestFit = false, int minSize = 18, int maxSize = 60)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var text = go.AddComponent<Text>();
            text.text = value;
            text.font = ResolveUIFont();
            text.fontSize = fontSize;
            text.fontStyle = style;
            text.color = color;
            text.raycastTarget = false;
            text.alignment = alignment;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            text.resizeTextForBestFit = enableBestFit;
            text.resizeTextMinSize = enableBestFit ? minSize : fontSize;
            text.resizeTextMaxSize = enableBestFit ? maxSize : fontSize;

            var rect = text.rectTransform;
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(1f, 1f);
            rect.pivot = new Vector2(0.5f, 1f);
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;

            var element = go.AddComponent<LayoutElement>();
            element.flexibleWidth = 1f;
            element.flexibleHeight = 0f;
            element.minHeight = Mathf.Max(42f, fontSize * 0.6f + 20f);

            return text;
        }

        private Button CreateButton(Transform parent, string name, string label)
        {
            var rect = new GameObject(name).AddComponent<RectTransform>();
            rect.SetParent(parent, false);
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(1f, 1f);
            rect.pivot = new Vector2(0.5f, 1f);
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;

            var image = rect.gameObject.AddComponent<Image>();
            image.color = new Color(0.2f, 0.3f, 0.42f, 1f);
            if (roundedSprite != null)
            {
                image.sprite = roundedSprite;
                image.type = Image.Type.Sliced;
            }

            var button = rect.gameObject.AddComponent<Button>();
            ApplyButtonStyle(button);

            // 静态字体支持 Best Fit，动态字体需禁用以避免图集重建导致字符丢失
            var text = CreateText(rect, name + "Label", label, 30, FontStyle.Bold, Color.white, TextAnchor.MiddleCenter, useStaticFont, 22, 34);
            text.horizontalOverflow = HorizontalWrapMode.Overflow;
            text.rectTransform.anchorMin = Vector2.zero;
            text.rectTransform.anchorMax = Vector2.one;
            text.rectTransform.offsetMin = new Vector2(8f, 8f);
            text.rectTransform.offsetMax = new Vector2(-8f, -8f);

            var element = rect.gameObject.AddComponent<LayoutElement>();
            element.flexibleWidth = 1f;
            element.minHeight = 96f;

            return button;
        }

        private void ApplyButtonStyle(Button button)
        {
            var colors = button.colors;
            colors.normalColor = new Color(0.21f, 0.32f, 0.45f, 1f);
            colors.highlightedColor = new Color(0.25f, 0.38f, 0.52f, 1f);
            colors.pressedColor = new Color(0f, 0.66f, 1f, 1f);
            colors.selectedColor = colors.highlightedColor;
            colors.disabledColor = new Color(0.15f, 0.2f, 0.26f, 0.6f);
            button.colors = colors;
        }

        private void EnsureEventSystem()
        {
            if (FindObjectOfType<EventSystem>() != null)
            {
                return;
            }

            var eventSystem = new GameObject("EventSystem");
            eventSystem.AddComponent<EventSystem>();
            eventSystem.AddComponent<StandaloneInputModule>();
        }

        #endregion

        #region 帮助方法

        private void CleanupAd<T>(ref T ad) where T : DirichletAd
        {
            if (ad != null)
            {
                try
                {
                    ad.Destroy();
                }
                catch (Exception ex)
                {
                    Log($"释放广告时异常: {ex.Message}");
                }

                ad = null;
            }
        }

        private void ToggleInitializationButtons(bool interactable)
        {
            if (initButton != null)
            {
                initButton.interactable = interactable;
            }
        }

        private void ToggleAdLoaders(bool interactable)
        {
            if (rewardLoadButton != null)
            {
                rewardLoadButton.interactable = interactable;
            }

            if (rewardAutoButton != null)
            {
                rewardAutoButton.interactable = interactable;
            }

            if (interstitialLoadButton != null)
            {
                interstitialLoadButton.interactable = interactable;
            }

            if (bannerLoadButton != null)
            {
                bannerLoadButton.interactable = interactable;
            }

            if (splashLoadButton != null)
            {
                splashLoadButton.interactable = interactable;
            }
        }

        private void Log(string message)
        {
            if (Thread.CurrentThread.ManagedThreadId != demoUnityThreadId)
            {
                if (demoUnityContext != null)
                {
                    demoUnityContext.Post(_ => Log(message), null);
                }
                return;
            }

            var line = $"[{DateTime.Now:HH:mm:ss}] {message}";
            Debug.Log("[Dirichlet Demo] " + line);

            if (statusText == null)
            {
                return;
            }

            if (string.IsNullOrEmpty(statusText.text))
            {
                statusText.text = line;
            }
            else
            {
                statusText.text += "\n" + line;
            }

            Canvas.ForceUpdateCanvases();

            if (!statusPanelCollapsed && statusScroll != null)
            {
                statusScroll.verticalNormalizedPosition = 0f;
            }
            else if (statusPanelCollapsed)
            {
                statusUnreadCount++;
                if (statusToggleLabel != null)
                {
                    statusToggleLabel.text = statusUnreadCount > 0 ? $"展开日志 (+{statusUnreadCount})" : "展开日志";
                }
            }
            else if (statusToggleLabel != null)
            {
                statusToggleLabel.text = "收起日志";
            }
        }

        private void AttachCommonEvents(DirichletAd ad, string label)
        {
            if (ad == null)
            {
                return;
            }

            ad.Shown += () => Log($"{label} 展示回调");
            ad.Clicked += () => Log($"{label} 点击回调");
            ad.Closed += () => Log($"{label} 关闭回调");
        }

        private void AttachRewardEvents(DirichletRewardVideoAd ad, string slot)
        {
            AttachCommonEvents(ad, $"激励({slot})");
            if (ad == null)
            {
                return;
            }

            ad.RewardVerified += args =>
            {
                var state = args.IsVerified ? "验证通过" : "验证失败";
                Log($"激励发奖回调: {state}, 数量={args.RewardAmount}, 奖励={args.RewardName}, Code={args.Code}, Message={args.Message}");
            };
        }

        private void AttachInterstitialEvents(DirichletInterstitialAd ad, string slot)
        {
            AttachCommonEvents(ad, $"插屏({slot})");
        }

        private void AttachBannerEvents(DirichletBannerAd ad, string slot)
        {
            AttachCommonEvents(ad, $"横幅({slot})");
        }

        private void AttachSplashEvents(DirichletSplashAd ad, string slot)
        {
            AttachCommonEvents(ad, $"开屏({slot})");
        }

        #endregion
    }
}
