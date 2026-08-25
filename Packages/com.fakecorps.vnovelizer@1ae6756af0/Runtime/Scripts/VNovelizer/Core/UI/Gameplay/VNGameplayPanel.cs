using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.EventSystems;
using VNovelizer.Core.API;
using VNovelizer.Core.Commands;
using PrimeTween;
using VNovelizer.Core;
using VNovelizer.Core.Diagnostics;

public class VNGameplayPanel : BasePanel
{

    #region 字段和配置
    public bool IsInitialized { get; private set; }
    public System.Action OnInitialized;

    [SerializeField] private Image bgImage_F;
    [SerializeField] private Image bgImage_B;
    [SerializeField] private Image charLeftImage;
    [SerializeField] private Image charMidImage;
    [SerializeField] private Image charRightImage;
    [SerializeField] private Image speakerBox;
    [SerializeField] private TMP_Text speakerText;
    [SerializeField] private TMP_Text dialogueText;
    [SerializeField] private Transform effectLayer;
    
    [SerializeField] private Image headImage;
    [SerializeField] private Image headFrame;
    [SerializeField] private Transform headProfileTransform;

    [Header("SpeakerBox Default")]
    [Tooltip("Default speaker box sprite.")]
    [SerializeField] private Sprite defaultSpeakerBoxSprite;
    
    [Header("HeadFrame Default")]
    [Tooltip("Default head frame sprite.")]
    [SerializeField] private Sprite defaultHeadFrameSprite;
    
    private Dictionary<string, Vector2> defaultCharPositions = new Dictionary<string, Vector2>();
    private Dictionary<string, float> defaultCharScales = new Dictionary<string, float>();
    private HashSet<string> modifiedCharTransforms = new HashSet<string>();
    
    private Dictionary<string, Vector2> baseCharPositions = new Dictionary<string, Vector2>();

    private Color? defaultDialogueTextColor = null;
    private float? defaultDialogueTextSize = null;
    private bool isDialogueTextModified = false;

    [Header("UI Components")]
    [SerializeField] private Image continueIcon;
    private TMP_Text staminaText;
    private TMP_Text staminaTimerText;
    private Button staminaAddButton;
    private Coroutine staminaTimerCoroutine;
    private const string StaminaHudPrefabPath = "VNovelizerRes/VNPrefabs/UI/Monetization/StaminaHUD";
    private Sequence _iconSequence;
    [SerializeField] private Button autoButton;
    [SerializeField] private Sprite autoPauseSprite;
    [SerializeField] private Sprite autoContinueSprite;

    [SerializeField] private Button skipButton;
    [SerializeField] private Button saveButton;
    [SerializeField] private Button loadButton;
    [SerializeField] private Button historyButton;
    [SerializeField] private Button hideButton;
    [SerializeField] private Button pauseButton;
    private bool isAutoPlaying = false;
    private bool isSkipping = false;
    private bool isTextTyping = false;
    private bool isUIHidden = false;
    private bool isCharLeftVisible = false;
    private bool isCharMidVisible = false;
    private bool isCharRightVisible = false;
    private readonly Dictionary<string, int> characterSpriteLoadVersions = new Dictionary<string, int>();
    private int headSpriteLoadVersion;
    private string currentAppliedBackgroundPath = "";
    private CanvasGroup uiRootCanvasGroup;
    private bool isPanoramaUiSuppressed;
    private float uiRootAlphaBeforePanorama = 1f;
    private bool uiRootInteractableBeforePanorama = true;
    private bool uiRootBlocksRaycastsBeforePanorama = true;
    private CanvasGroup pauseButtonCanvasGroup;
    private Canvas pauseButtonCanvas;
    private Coroutine restoreInputGuardCoroutine;
    private float textSpeed;
    private float currentBaseSpeed;
    private float autoSpeed;
    private bool useNewInputSystem = true;
    private float _lastSkipAdvanceUnscaledTime;
    private const float ConfirmAdvanceGuardAfterRecentTypingFinish = 0.2f;
    private const float ConfirmAdvanceGuardAfterManualTypingComplete = 0.05f;
    private const int PauseButtonChoiceSortingOrder = 20;
    private float _lastTypingFinishUnscaledTime = float.NegativeInfinity;
    private float _lastManualTypingCompleteUnscaledTime = float.NegativeInfinity;

    //Notification
    [Header("Prompt System")]
    [SerializeField] private Transform promptContainer;
    private GameObject promptPrefab;

    private Coroutine autoPlayCoroutine;

    private Tween _typewriterTween;
    private Coroutine _backgroundFitCoroutine;
    private Coroutine _backgroundLoadCoroutine;
    private static readonly Dictionary<string, Sprite> backgroundTextureSpriteCache = new Dictionary<string, Sprite>();

    [SerializeField] private Transform uiRoot;


    private VNInputActions inputActions;

    [Header("Custom")]
    [SerializeField] private Transform dialogueBox;

    #endregion

    #region Awake()
    protected override void Awake()
    {
        base.Awake();

        bgImage_F = GetControl<Image>("BG_Front");
        bgImage_B = GetControl<Image>("BG_Back");
        charLeftImage = GetControl<Image>("Char_Left");
        charMidImage = GetControl<Image>("Char_Mid");
        charRightImage = GetControl<Image>("Char_Right");
        speakerBox = GetControl<Image>("SpeakerBox");
        speakerText = GetControl<TMP_Text>("SpeakerText");
        dialogueText = GetControl<TMP_Text>("DialougeText");
        effectLayer = transform.Find("EffectLayer");


        uiRoot = transform.Find("UIRoot");
        if (uiRoot == null)
        {
            Debug.LogWarning("[VNGameplayPanel] 未找到 UIRoot，HeadProfile 可能无法正确初始化");
        }
        else
        {
            uiRootCanvasGroup = uiRoot.GetComponent<CanvasGroup>();
            if (uiRootCanvasGroup == null)
            {
                uiRootCanvasGroup = uiRoot.gameObject.AddComponent<CanvasGroup>();
            }
        }
        
        if (uiRoot != null)
        {
            Transform headProfile = uiRoot.Find("HeadProfile");
            if (headProfile != null)
            {
                headProfileTransform = headProfile;
                Transform headImageTransform = headProfile.Find("HeadImage");
                Transform headFrameTransform = headProfile.Find("HeadFrame");
                if (headImageTransform != null) headImage = headImageTransform.GetComponent<Image>();
                if (headFrameTransform != null) headFrame = headFrameTransform.GetComponent<Image>();
            }
        }
        
        if (headImage == null) headImage = GetControl<Image>("HeadImage");
        if (headFrame == null) headFrame = GetControl<Image>("HeadFrame");
        if (headProfileTransform == null) headProfileTransform = transform.Find("UIRoot/HeadProfile");

        if (dialogueText == null) Debug.LogError("DialougeText not found in VNGameplayPanel");
        if (speakerText == null) Debug.LogError("SpeakerText not found in VNGameplayPanel");


        if (continueIcon == null)
            continueIcon = transform.Find("UIRoot/DialogueBox/ContinueIcon")?.GetComponent<Image>();

        if (promptContainer == null) promptContainer = transform.Find("PromptLayer");
        promptPrefab = ResourcesManager.GetInstance().Load<GameObject>(VNProjectConfig.Instance.UI_PromptPath + "/PromptItem");
        autoButton = GetControl<Button>("Auto");
        skipButton = GetControl<Button>("Skip");
        saveButton = GetControl<Button>("Save");
        loadButton = GetControl<Button>("Load");
        historyButton = GetControl<Button>("History");
        hideButton = GetControl<Button>("Hide");
        pauseButton = GetControl<Button>("Pause");
        if (pauseButton != null)
        {
            pauseButtonCanvasGroup = pauseButton.GetComponent<CanvasGroup>();
            if (pauseButtonCanvasGroup == null)
            {
                pauseButtonCanvasGroup = pauseButton.gameObject.AddComponent<CanvasGroup>();
            }

            ConfigurePauseButtonOverlay();
        }
        UpdateAutoButtonState();

        if (autoButton != null) autoButton.onClick.AddListener(OnAutoButtonClick);
        if (skipButton != null) skipButton.onClick.AddListener(OnSkipButtonClick);
        if (saveButton != null) saveButton.onClick.AddListener(OnSaveButtonClick);
        if (loadButton != null) loadButton.onClick.AddListener(OnLoadButtonClick);
        if (historyButton != null) historyButton.onClick.AddListener(OnLogButtonClick);
        if (hideButton != null) hideButton.onClick.AddListener(OnHideButtonClick);
        if (pauseButton != null) pauseButton.onClick.AddListener(OnPauseButtonClick);

        if (StaminaManager.IsEnabled)
        {
            CreateStaminaHud();
            StaminaManager.GetInstance().Init();
            StaminaManager.GetInstance().OnStaminaChanged += OnStaminaChanged;
            StaminaManager.GetInstance().OnRecoveryTimerChanged += RefreshStaminaTimer;
            OnStaminaChanged(StaminaManager.GetInstance().CurrentStamina, StaminaManager.GetInstance().MaxStamina);
            staminaTimerCoroutine = StartCoroutine(RefreshStaminaTimerRoutine());
        }

        try
        {
            if (GlobalDataManager.GetInstance() != null)
            {
                if (GlobalDataManager.GetInstance().GetGlobalData() == null)
                {
                    GlobalDataManager.GetInstance().Init();
                }

                if (GlobalDataManager.GetInstance().GetGlobalData() != null)
                {
                    textSpeed = GlobalDataManager.GetInstance().GetGlobalData().TextSpeed;
                    autoSpeed = GlobalDataManager.GetInstance().GetGlobalData().AutoSpeed;
                }
                else
                {
                    textSpeed = 0.05f;
                    autoSpeed = 1.0f;
                }
            }
        }
        catch (System.Exception e)
        {
            Debug.LogWarning("Error accessing GlobalDataManager: " + e.Message + ", using default values");
            textSpeed = 0.05f;
            autoSpeed = 1.0f;
        }

        EventCenter.GetInstance().AddEventListener<Dictionary<string, string>>(VNGameEvents.UpdateDialogue, OnUpdateDialogue);
        EventCenter.GetInstance().AddEventListener<string>(VNGameEvents.ChangeBackground, OnChangeBackground);
        EventCenter.GetInstance().AddEventListener<Dictionary<string, string>>(VNGameEvents.ShowCharacter, OnShowCharacter);
        EventCenter.GetInstance().AddEventListener<string>(VNGameEvents.HideCharacter, OnHideCharacter);
        EventCenter.GetInstance().AddEventListener<Dictionary<string, string>>(VNGameEvents.UpdateHeadProfile, OnUpdateHeadProfile);
        EventCenter.GetInstance().AddEventListener(VNGameEvents.DisplayAllText, OnDisplayAllText);
        EventCenter.GetInstance().AddEventListener("TextSpeedChanged", OnTextSpeedChanged);
        EventCenter.GetInstance().AddEventListener("AutoSpeedChanged", OnAutoSpeedChanged);

        InitializeInputSystem();

        GameStateManager.GetInstance().SetState(GameState.Gameplay);
    }
    #endregion

    private void CreateStaminaHud()
    {
        Transform parent = uiRoot != null ? uiRoot : transform;
        Transform existingHud = parent.Find("StaminaHUD");
        if (existingHud != null)
        {
            BindStaminaHud(existingHud);
            return;
        }

        GameObject prefab = ResourcesManager.GetInstance().Load<GameObject>(StaminaHudPrefabPath);
        if (prefab != null)
        {
            GameObject hud = Instantiate(prefab, parent, false);
            hud.name = "StaminaHUD";

            BindStaminaHud(hud.transform);
            return;
        }

        GameObject root = new GameObject("StaminaHUD", typeof(RectTransform), typeof(Image));
        root.transform.SetParent(parent, false);

        RectTransform rootRect = root.GetComponent<RectTransform>();
        rootRect.anchorMin = new Vector2(0f, 1f);
        rootRect.anchorMax = new Vector2(0f, 1f);
        rootRect.pivot = new Vector2(0f, 1f);
        rootRect.anchoredPosition = new Vector2(24f, -24f);
        rootRect.sizeDelta = new Vector2(250f, 44f);

        Image rootImage = root.GetComponent<Image>();
        rootImage.color = new Color(0.08f, 0.08f, 0.1f, 0.72f);

        GameObject labelObj = new GameObject("StaminaText", typeof(RectTransform), typeof(TextMeshProUGUI));
        labelObj.transform.SetParent(root.transform, false);
        RectTransform labelRect = labelObj.GetComponent<RectTransform>();
        labelRect.anchorMin = new Vector2(0f, 0f);
        labelRect.anchorMax = new Vector2(0f, 1f);
        labelRect.offsetMin = new Vector2(14f, 0f);
        labelRect.offsetMax = new Vector2(112f, 0f);

        staminaText = labelObj.GetComponent<TMP_Text>();
        staminaText.fontSize = 22f;
        staminaText.fontStyle = FontStyles.Bold;
        staminaText.alignment = TextAlignmentOptions.MidlineLeft;
        staminaText.color = Color.white;
        ApplyDefaultTMPFont(staminaText);

        GameObject timerObj = new GameObject("StaminaTimerText", typeof(RectTransform), typeof(TextMeshProUGUI));
        timerObj.transform.SetParent(root.transform, false);
        RectTransform timerRect = timerObj.GetComponent<RectTransform>();
        timerRect.anchorMin = new Vector2(0f, 0f);
        timerRect.anchorMax = new Vector2(0f, 1f);
        timerRect.offsetMin = new Vector2(118f, 0f);
        timerRect.offsetMax = new Vector2(198f, 0f);

        staminaTimerText = timerObj.GetComponent<TMP_Text>();
        staminaTimerText.fontSize = 18f;
        staminaTimerText.fontStyle = FontStyles.Bold;
        staminaTimerText.alignment = TextAlignmentOptions.MidlineLeft;
        staminaTimerText.color = new Color(1f, 0.82f, 0.42f, 1f);
        ApplyDefaultTMPFont(staminaTimerText);

        GameObject addObj = new GameObject("StaminaAddButton", typeof(RectTransform), typeof(Image), typeof(Button));
        addObj.transform.SetParent(root.transform, false);
        RectTransform addRect = addObj.GetComponent<RectTransform>();
        addRect.anchorMin = new Vector2(1f, 0.5f);
        addRect.anchorMax = new Vector2(1f, 0.5f);
        addRect.pivot = new Vector2(1f, 0.5f);
        addRect.anchoredPosition = new Vector2(-6f, 0f);
        addRect.sizeDelta = new Vector2(34f, 34f);

        Image addImage = addObj.GetComponent<Image>();
        addImage.color = new Color(0.9f, 0.72f, 0.28f, 1f);

        staminaAddButton = addObj.GetComponent<Button>();
        staminaAddButton.targetGraphic = addImage;
        staminaAddButton.onClick.AddListener(OnStaminaAddClicked);

        GameObject addTextObj = new GameObject("Text", typeof(RectTransform), typeof(TextMeshProUGUI));
        addTextObj.transform.SetParent(addObj.transform, false);
        RectTransform addTextRect = addTextObj.GetComponent<RectTransform>();
        addTextRect.anchorMin = Vector2.zero;
        addTextRect.anchorMax = Vector2.one;
        addTextRect.offsetMin = Vector2.zero;
        addTextRect.offsetMax = Vector2.zero;

        TMP_Text addText = addTextObj.GetComponent<TMP_Text>();
        addText.text = "+";
        addText.fontSize = 28f;
        addText.fontStyle = FontStyles.Bold;
        addText.alignment = TextAlignmentOptions.Center;
        addText.color = new Color(0.1f, 0.08f, 0.05f, 1f);
        ApplyDefaultTMPFont(addText);
    }

    private static void ApplyDefaultTMPFont(TMP_Text text)
    {
        if (text == null || TMP_Settings.defaultFontAsset == null)
            return;

        text.font = TMP_Settings.defaultFontAsset;
        text.SetAllDirty();
    }

    private void BindStaminaHud(Transform hud)
    {
        staminaText = hud.Find("StaminaText")?.GetComponent<TMP_Text>();
        staminaTimerText = hud.Find("StaminaTimerText")?.GetComponent<TMP_Text>();
        staminaAddButton = hud.Find("StaminaAddButton")?.GetComponent<Button>();

        if (staminaAddButton != null)
        {
            staminaAddButton.onClick.RemoveListener(OnStaminaAddClicked);
            staminaAddButton.onClick.AddListener(OnStaminaAddClicked);
        }
    }

    private void OnStaminaChanged(int current, int max)
    {
        if (staminaText != null)
            staminaText.text = $"{current}/{max}";

        RefreshStaminaTimer();
    }

    private IEnumerator RefreshStaminaTimerRoutine()
    {
        WaitForSecondsRealtime wait = new WaitForSecondsRealtime(1f);

        while (true)
        {
            RefreshStaminaTimer();
            yield return wait;
        }
    }

    private void RefreshStaminaTimer()
    {
        if (staminaTimerText == null)
            return;

        StaminaManager staminaManager = StaminaManager.GetInstance();
        staminaManager.Init();

        bool shouldShowTimer = staminaManager.IsRecoveryTimerVisible;
        staminaTimerText.gameObject.SetActive(shouldShowTimer);
        if (!shouldShowTimer)
            return;

        TimeSpan remaining = staminaManager.GetTimeUntilNextRecovery();
        int totalSeconds = Mathf.CeilToInt((float)remaining.TotalSeconds);
        totalSeconds = Mathf.Max(0, totalSeconds);
        int minutes = totalSeconds / 60;
        int seconds = totalSeconds % 60;
        staminaTimerText.text = $"{minutes:00}:{seconds:00}";
    }

    private void OnStaminaAddClicked()
    {
        if (!StaminaManager.IsEnabled)
            return;

        StaminaRecoveryPopup.Show();
    }

    private IEnumerator Start()
    {
        IsInitialized = false;
        yield return new WaitForEndOfFrame();
        Canvas.ForceUpdateCanvases();
        IsInitialized = true;
        OnInitialized?.Invoke();
    }

    protected override void OnButtonClick(string ButtonName)
    {
        base.OnButtonClick(ButtonName);
    }

    /// <summary>
    /// 显示玩法面板，并确保输入绑定处于启用状态。
    /// </summary>
    public override void ShowMe()
    {
        gameObject.SetActive(true);
        EnsureInputActionsEnabled();
    }

    /// <summary>
    /// 初始化或重新启用 VN 输入，并重新绑定确认键回调。
    /// </summary>
    private void EnsureInputActionsEnabled()
    {
        if (inputActions == null)
        {
            InitializeInputSystem();
        }

        if (inputActions != null)
        {
            if (!inputActions.VNControls.enabled)
            {
                inputActions.VNControls.Enable();
                VNDebug.LogVerbose("[VNGameplayPanel] ShowMe: Input Actions enabled");
            }
            
            inputActions.VNControls.Confirm.performed -= OnConfirm;
            inputActions.VNControls.Confirm.performed += OnConfirm;
            
            VNDebug.LogVerbose("[VNGameplayPanel] ShowMe: Input Actions enabled and bound");
        }
        else
        {
            Debug.LogError("[VNGameplayPanel] ShowMe: Input Actions 为空，无法启用输入");
        }
    }

    private void Update()
    {
        UpdatePauseButtonOverlaySorting();

        if (isSkipping && !isTextTyping)
        {
            if (!isAutoPlaying && !isUIHidden)
            {
                GameStateManager stateManager = GameStateManager.GetInstance();
                if (stateManager != null && !stateManager.CanInteractGameplay())
                {
                    isSkipping = false;
                    UpdateSkipButtonState();
                    return;
                }
                
                if (stateManager.CurrentState == GameState.Choice)
                {
                    isSkipping = false;
                    UpdateSkipButtonState();
                    return;
                }

                float now = Time.unscaledTime;
                if (now - _lastSkipAdvanceUnscaledTime < 0.02f)
                    return;
                _lastSkipAdvanceUnscaledTime = now;

                if (ShouldBlockAdvanceAfterManualTypingComplete())
                {
                    VNDebug.LogVerbose("[VNGameplayPanel] Advance blocked by manual typing guard");
                    return;
                }

                VNManager.GetInstance().NextLine();
            }
        }

        if (useNewInputSystem)
            UpdateTouchConfirmInput();
        else
            UpdateInputFallback();
    }

    private void UpdateTouchConfirmInput()
    {
#if ENABLE_INPUT_SYSTEM
        Touchscreen touchscreen = Touchscreen.current;
        if (touchscreen != null && touchscreen.primaryTouch.press.wasPressedThisFrame)
        {
            HandleConfirmInput();
        }
#endif
    }

    //  private static bool IsPointerOverGameObjectNow()
    private bool IsPointerOverGameObjectNow()
    {
        var es = EventSystem.current;
        if (es == null) return false;
        int pointerId = -1;
#if ENABLE_INPUT_SYSTEM
        var ts = Touchscreen.current;
        if (ts != null && ts.primaryTouch.press.isPressed)
            pointerId = ts.primaryTouch.touchId.ReadValue();
#endif
        return es.IsPointerOverGameObject(pointerId);
    }

    private bool IsPointerOverBlockingUI()
    {
        var es = EventSystem.current;
        if (es == null) return false;

        Vector2 pointerPosition;
#if ENABLE_INPUT_SYSTEM
        if (Touchscreen.current != null && Touchscreen.current.primaryTouch.press.isPressed)
            pointerPosition = Touchscreen.current.primaryTouch.position.ReadValue();
        else if (Mouse.current != null)
            pointerPosition = Mouse.current.position.ReadValue();
        else
            return false;
#else
    pointerPosition = Input.mousePosition;
#endif

        var eventData = new PointerEventData(es)
        {
            position = pointerPosition
        };

        var results = new List<RaycastResult>();
        es.RaycastAll(eventData, results);

        foreach (var hit in results)
        {
            var go = hit.gameObject;
            if (go == null) continue;

            if (dialogueBox != null && go.transform.IsChildOf(dialogueBox))
                continue;

            if (speakerBox != null && go.transform.IsChildOf(speakerBox.transform))
                continue;

            if (go.GetComponentInParent<Button>() != null ||
                go.GetComponentInParent<Toggle>() != null ||
                go.GetComponentInParent<Slider>() != null ||
                go.GetComponentInParent<Scrollbar>() != null ||
                go.GetComponentInParent<ScrollRect>() != null ||
                go.GetComponentInParent<InputField>() != null ||
                go.GetComponentInParent<TMP_InputField>() != null ||
                go.GetComponentInParent<Dropdown>() != null ||
                go.GetComponentInParent<TMP_Dropdown>() != null)
            {
                return true;
            }
        }

        return false;
    }


    #region 输入系统

    private void InitializeInputSystem()
    {
        if (inputActions == null)
        {
            inputActions = new VNInputActions();
        }

        useNewInputSystem = true;
        VNDebug.LogVerbose("[VNGameplayPanel] Input System Initialized via C# Class");
    }

    protected override void OnEnable()
    {
        base.OnEnable();

        VNDebug.LogVerbose($"[VNGameplayPanel] OnEnable called - inputActions: {inputActions != null}");
        
        if (inputActions == null)
        {
            InitializeInputSystem();
        }

        if (inputActions != null)
        {
            inputActions.VNControls.Enable();

            inputActions.VNControls.Confirm.performed -= OnConfirm;
            inputActions.VNControls.Skip.performed -= OnSkip;
            inputActions.VNControls.Skip.canceled -= OnSkipCanceled;
            inputActions.VNControls.Auto.performed -= OnAuto;
            inputActions.VNControls.Hide.performed -= OnHide;
            inputActions.VNControls.Log.performed -= OnLog;
            inputActions.VNControls.Save.performed -= OnSave;
            inputActions.VNControls.Settings.performed -= OnPause;
            
            inputActions.VNControls.Confirm.performed += OnConfirm;
            inputActions.VNControls.Skip.performed += OnSkip;
            inputActions.VNControls.Skip.canceled += OnSkipCanceled;
            inputActions.VNControls.Auto.performed += OnAuto;
            inputActions.VNControls.Hide.performed += OnHide;
            inputActions.VNControls.Log.performed += OnLog;
            inputActions.VNControls.Save.performed += OnSave;
            inputActions.VNControls.Settings.performed += OnPause;

            VNDebug.LogVerbose("[VNGameplayPanel] Input Actions Enabled & Bound");
        }
        else
        {
            Debug.LogError("[VNGameplayPanel] Input Actions 为空，无法启用输入");
        }
    }

    private void OnDisable()
    {
        if (inputActions != null)
        {
            inputActions.VNControls.Confirm.performed -= OnConfirm;

            inputActions.VNControls.Skip.performed -= OnSkip;
            inputActions.VNControls.Skip.canceled -= OnSkipCanceled;

            inputActions.VNControls.Auto.performed -= OnAuto;
            inputActions.VNControls.Hide.performed -= OnHide;
            inputActions.VNControls.Log.performed -= OnLog;

            inputActions.VNControls.Save.performed -= OnSave;

            inputActions.VNControls.Settings.performed -= OnPause;

            // 禁用玩法输入 Action Map。
            inputActions.VNControls.Disable();

            VNDebug.LogVerbose("[VNGameplayPanel] Input Actions Disabled");
        }

        HideContinueIcon();
    }
    #endregion

    #region 输入回调

    public void OnConfirm(InputAction.CallbackContext context)
    {
        HandleConfirmInput();
    }

    private void HandleConfirmInput()
    {
        VNDebug.LogVerbose($"[VNGameplayPanel] OnConfirm called - State: {GameStateManager.GetInstance().CurrentState}, isAutoPlaying: {isAutoPlaying}, isUIHidden: {isUIHidden}, isTextTyping: {isTextTyping}");
        VNDebug.LogVerbose($"[TypingTrace][OnConfirm] state={GameStateManager.GetInstance().CurrentState}, isAutoPlaying={isAutoPlaying}, isUIHidden={isUIHidden}, isTextTyping={isTextTyping}, isTextDisplaying={VNManager.GetInstance().IsTextDisplaying()}");

        if (!GameStateManager.GetInstance().CanInteractGameplay())
            return;

        //if (IsPointerOverGameObjectNow())
        //{
        //    return;
        //}

        if (IsPointerOverBlockingUI())
        {
            VNDebug.LogVerbose("[VNGameplayPanel] 点击在可交互 UI 上，忽略推进输入");
            return;
        }

        if (isUIHidden)
        {
            ShowHiddenUI();
            return;
        }

        if (!isAutoPlaying && !isUIHidden)
        {
            if (isTextTyping)
            {
                VNDebug.LogVerbose("[TypingTrace][OnConfirm] branch=CompleteTextTyping");
                CompleteTextTyping();
            }
            else
            {
                if (ShouldBlockAdvanceAfterRecentTypingFinish())
                {
                    VNDebug.LogVerbose("[TypingTrace][OnConfirm] branch=BlockedByRecentTypingFinish");
                    return;
                }

                if (ShouldBlockAdvanceAfterManualTypingComplete())
                {
                    VNDebug.LogVerbose("[VNGameplayPanel] Advance blocked by manual typing guard");
                    VNDebug.LogVerbose("[TypingTrace][OnConfirm] branch=BlockedByManualTypingGuard");
                    return;
                }

                VNDebug.LogVerbose("[VNGameplayPanel] Execute NextLine");
                VNDebug.LogVerbose("[TypingTrace][OnConfirm] branch=NextLine");
                VNManager.GetInstance().NextLine();
            }
        }
    }

    public void OnConfirm()
    {
        OnConfirm(new InputAction.CallbackContext());
    }

    public void OnSkip(InputAction.CallbackContext context)
    {
        if (!GameStateManager.GetInstance().CanInteractGameplay())
        {
            return;
        }
        
        if (GameStateManager.GetInstance().CurrentState == GameState.Choice)
        {
            return;
        }
        
        VNDebug.LogVerbose("[VNGameplayPanel] Skip mode enabled");
        isSkipping = true;
        UpdateSkipButtonState();
        Time.timeScale = 10f;
    }

    public void OnSkipCanceled(InputAction.CallbackContext context)
    {
        isSkipping = false;
        UpdateSkipButtonState();
        Time.timeScale = 1f;
    }

    // public void OnAuto(InputAction.CallbackContext context)
    // {
    //     VNManager.GetInstance().ToggleAutoPlay();
    // }
    public void OnAuto(InputAction.CallbackContext context)
    {
        if (!GameStateManager.GetInstance().CanInteractGameplay())
            return;

        VNManager.GetInstance().ToggleAutoPlay();
        UpdateAutoButtonState();
    }

    public void OnHide(InputAction.CallbackContext context)
    {
        if (GameStateManager.GetInstance().CurrentState == GameState.Panorama)
            return;

        ToggleUI();
    }

    public void OnLog(InputAction.CallbackContext context)
    {
        var historyPanel = UIManager.GetInstance().GetPanel<HistoryPanel>("HistoryPanel");
        
        if (historyPanel != null && historyPanel.gameObject.activeSelf)
        {
            if (context.control != null)
            {
                string controlPath = context.control.path;
                if (controlPath.Contains("scroll"))
                {
                    return;
                }
            }
            
            UIManager.GetInstance().HidePanel("HistoryPanel");
            GameStateManager.GetInstance().RestoreState();
            return;
        }
        
        if (!GameStateManager.GetInstance().CanOpenPanel(GameState.History))
        {
            Debug.LogWarning("[VNGameplayPanel] Cannot open history panel in current state");
            return;
        }

        GameStateManager.GetInstance().SetState(GameState.History);
        string path = VNProjectConfig.Instance != null ? VNProjectConfig.Instance.UI_HistoryPath : "VNPrefabs/UI/History";
        if (string.IsNullOrEmpty(path)) path = "VNPrefabs/UI/History";
        UIManager.GetInstance().ShowPanel<HistoryPanel>("HistoryPanel", path, E_UI_Layer.Top, null);
    }

    public void OnSave(InputAction.CallbackContext context)
    {
        if (!GameStateManager.GetInstance().CanOpenPanel(GameState.SaveLoad))
        {
            Debug.LogWarning("[VNGameplayPanel] Cannot open save/load panel in current state");
            return;
        }

        var saveLoadPanel = UIManager.GetInstance().GetPanel<SaveLoadPanel>("SaveLoadPanel");

        if (saveLoadPanel != null && saveLoadPanel.gameObject.activeSelf)
        {
            UIManager.GetInstance().HidePanel("SaveLoadPanel");
            GameStateManager.GetInstance().RestoreState();
        }
        else
        {
            OpenSavePanelAfterScreenshot();
        }
    }

    public void OnPause(InputAction.CallbackContext context)
    {
        GameState currentState = GameStateManager.GetInstance().CurrentState;
        if (currentState != GameState.Gameplay && currentState != GameState.AutoPlay && 
            currentState != GameState.Choice && currentState != GameState.Pause)
        {
            return;
        }
        
        if (!GameStateManager.GetInstance().CanOpenPanel(GameState.Pause))
        {
            Debug.LogWarning("[VNGameplayPanel] Cannot open pause panel in current state");
            return;
        }

        var pausePanel = UIManager.GetInstance().GetPanel<PausePanel>("PausePanel");

        if (pausePanel != null && pausePanel.gameObject.activeSelf)
        {
            UIManager.GetInstance().HidePanel("PausePanel");
            GameStateManager.GetInstance().RestoreState();
        }
        else
        {
            SaveManager.GetInstance().CaptureCurrentScreen();
            SetPauseButtonOverlayRaised(false);
            
            string path = VNProjectConfig.Instance != null ? VNProjectConfig.Instance.UI_PausePath : "VNPrefabs/UI/Pause";
            if (string.IsNullOrEmpty(path)) path = "VNPrefabs/UI/Pause";
            UIManager.GetInstance().ShowPanel<PausePanel>("PausePanel", path, E_UI_Layer.System, null);
        }
    }
    
    /// <summary>
    /// 点击暂停按钮时复用暂停输入逻辑。
    /// </summary>
    private void OnPauseButtonClick()
    {
        OnPause(new InputAction.CallbackContext());
    }
    #endregion

    #region 旧输入兼容
    private void UpdateInputFallback()
    {
        if (useNewInputSystem) return;

        if (Input.GetMouseButtonDown(0)) OnConfirmFallback();

        if (Input.GetKeyDown(KeyCode.Space) || Input.GetKeyDown(KeyCode.Return)) OnConfirmFallback();
        if (Input.GetKeyDown(KeyCode.A)) OnAutoFallback();
        if (Input.GetKeyDown(KeyCode.H)) OnHideFallback();
        if (Input.GetKeyDown(KeyCode.L)) OnLogFallback();

        if (Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift))
            OnSkipFallback();
        else
            OnSkipCanceledFallback();
    }

    private void OnConfirmFallback()
    {
        VNDebug.LogVerbose($"[TypingTrace][OnConfirmFallback] isAutoPlaying={isAutoPlaying}, isUIHidden={isUIHidden}, isTextTyping={isTextTyping}, isTextDisplaying={VNManager.GetInstance().IsTextDisplaying()}");
        if (!GameStateManager.GetInstance().CanInteractGameplay())
            return;
        if (IsPointerOverGameObjectNow())
            return;
        if (isUIHidden)
        {
            ShowHiddenUI();
            return;
        }
        if (!isAutoPlaying && !isUIHidden)
        {
            if (isTextTyping)
            {
                VNDebug.LogVerbose("[TypingTrace][OnConfirmFallback] branch=CompleteTextTyping");
                CompleteTextTyping();
            }
            else if (ShouldBlockAdvanceAfterRecentTypingFinish())
            {
                VNDebug.LogVerbose("[TypingTrace][OnConfirmFallback] branch=BlockedByRecentTypingFinish");
            }
            else if (!ShouldBlockAdvanceAfterManualTypingComplete())
            {
                VNDebug.LogVerbose("[TypingTrace][OnConfirmFallback] branch=NextLine");
                VNManager.GetInstance().NextLine();
            }
            else
            {
                VNDebug.LogVerbose("[TypingTrace][OnConfirmFallback] branch=BlockedByManualTypingGuard");
            }
        }
    }

    private void OnSkipFallback()
    {
        if (!GameStateManager.GetInstance().CanInteractGameplay())
            return;

        if (!isSkipping)
        {
            isSkipping = true;
            UpdateSkipButtonState();
            Time.timeScale = 10f;
        }
    }

    private void OnSkipCanceledFallback()
    {
        if (isSkipping)
        {
            isSkipping = false;
            UpdateSkipButtonState();
            Time.timeScale = 1f;
        }
    }

    // private void OnAutoFallback()
    // {
    //     isAutoPlaying = !isAutoPlaying;
    //     UpdateAutoButtonState();
    // }
    private void OnAutoFallback()
    {
        if (!GameStateManager.GetInstance().CanInteractGameplay())
            return;

        VNManager.GetInstance().ToggleAutoPlay();
        UpdateAutoButtonState();
    }

    private void OnHideFallback()
    {
        if (GameStateManager.GetInstance().CurrentState == GameState.Panorama)
            return;

        ToggleUI();
    }

    private void OnLogFallback()
    {
        if (!GameStateManager.GetInstance().CanOpenPanel(GameState.History))
            return;

        UIManager.GetInstance().ShowPanel<HistoryPanel>("HistoryPanel", VNProjectConfig.Instance.UI_HistoryPath, E_UI_Layer.Middle, null);
    }
    #endregion

    #region 对话和显示

    /// <summary>
    /// 根据当前说话人显示或隐藏姓名框；旁白不显示姓名框。
    /// </summary>
    public void UpdateSpeakerDisplay(string speaker)
    {
        //string normalizedSpeaker = speaker?.Trim();
        string normalizedSpeaker = speaker?.Trim();
        if (string.IsNullOrWhiteSpace(normalizedSpeaker) ||
            string.Equals(normalizedSpeaker, "旁白", StringComparison.OrdinalIgnoreCase))
        {
            if (speakerBox != null)
            {
                speakerBox.transform.gameObject.SetActive(false);
            }

            //if (speakerText != null)
            //{
            //    speakerText.transform.gameObject.SetActive(false);
            //}
            if (speakerText != null)
            {
                speakerText.transform.gameObject.SetActive(false);
                speakerText.text = string.Empty;
            }

            return;
        }

        if (speakerBox != null)
        {
            speakerBox.gameObject.SetActive(true);
        }
        
        if (speakerText != null)
        {
            speakerText.gameObject.SetActive(true);
        }

        CharacterResManager charResMgr = CharacterResManager.GetInstance();
        if (charResMgr != null)
        {
            //CharacterProfile profile = charResMgr.TryGetCharacterProfile(speaker);
            CharacterProfile profile = charResMgr.TryGetCharacterProfile(normalizedSpeaker);
            if (profile != null && profile.SpeakerBox != null)
            {
                if (speakerBox != null)
                {
                    speakerBox.sprite = profile.SpeakerBox;
                }
                if (speakerText != null)
                {
                    speakerText.text = "";
                }
                return;
            }
        }

        if (speakerText != null)
        {
            //speakerText.text = speaker;
            speakerText.text = normalizedSpeaker;
        }
        if (speakerBox != null)
        {
            Sprite defaultSprite = defaultSpeakerBoxSprite;
            if (defaultSprite == null && VNProjectConfig.Instance != null)
            {
                defaultSprite = VNProjectConfig.Instance.DefaultSpeakerBoxSprite;
            }
            speakerBox.sprite = defaultSprite;
        }
    }

    private void OnUpdateDialogue(Dictionary<string, string> dialogueInfo)
    {
        string speaker = dialogueInfo["speaker"];
        string text = dialogueInfo["text"];

        // Play the dialogue advance cue once whenever a new dialogue line is displayed.
        MusicManager.GetInstance().PlaySFX("Skip", false);

        if (_typewriterTween.isAlive)
        {
            _typewriterTween.Stop();
        }

        if (autoPlayCoroutine != null)
        {
            StopCoroutine(autoPlayCoroutine);
            autoPlayCoroutine = null;
        }

        isTextTyping = true;

        UpdateSpeakerDisplay(speaker);

        currentBaseSpeed = textSpeed;

        dialogueText.text = text;
        dialogueText.maxVisibleCharacters = 0;

        float duration = text.Length * textSpeed;
        HideContinueIcon();

        _typewriterTween = Tween.Custom(0, text.Length, duration, onValueChange: (val) =>
        {
            dialogueText.maxVisibleCharacters = Mathf.FloorToInt(val);
        }, ease: Ease.Linear)
        .OnComplete(OnTypewriterComplete);
    }

    private void OnTypewriterComplete()
    {
        VNDebug.LogVerbose("[TypingTrace][OnTypewriterComplete] invoked");
        FinishTypewriterDisplay();
    }

    private void OnDisplayAllText()
    {
        VNDebug.LogVerbose($"[TypingTrace][OnDisplayAllText] isTextTyping={isTextTyping}, tweenAlive={_typewriterTween.isAlive}");
        if (isTextTyping)
        {
            CompleteTextTyping();
        }
    }

    private bool ShouldBlockAdvanceAfterManualTypingComplete()
    {
        return Time.unscaledTime - _lastManualTypingCompleteUnscaledTime < ConfirmAdvanceGuardAfterManualTypingComplete;
    }

    private bool ShouldBlockAdvanceAfterRecentTypingFinish()
    {
        float delta = Time.unscaledTime - _lastTypingFinishUnscaledTime;
        bool isCommandOrFlowRunning = CommandManager.GetInstance().IsRunning || VNManager.GetInstance().IsFlowRunning();
        bool shouldBlock = delta < ConfirmAdvanceGuardAfterRecentTypingFinish && isCommandOrFlowRunning;

        if (shouldBlock)
        {
            VNDebug.LogVerbose($"[TypingTrace][AdvanceGuard] block recent typing finish delta={delta}, cmdRunning={CommandManager.GetInstance().IsRunning}, flowRunning={VNManager.GetInstance().IsFlowRunning()}");
        }

        return shouldBlock;
    }

    private void FinishTypewriterDisplay()
    {
        if (dialogueText != null) dialogueText.maxVisibleCharacters = 99999;

        bool wasTyping = isTextTyping;
        isTextTyping = false;
        _lastTypingFinishUnscaledTime = Time.unscaledTime;

        VNDebug.LogVerbose($"[TypingTrace][FinishTypewriterDisplay] wasTyping={wasTyping}, dialogueLength={(dialogueText != null ? dialogueText.text.Length : -1)}, finishTime={_lastTypingFinishUnscaledTime}");

        RefreshContinueIconState();

        if (wasTyping)
        {
            VNDebug.LogVerbose("[TypingTrace][FinishTypewriterDisplay] trigger TypingFinished");
            EventCenter.GetInstance().EventTrigger(VNGameEvents.TypingFinished);
        }
    }


    private void OnChangeBackground(string backgroundPath)
    {
        if (bgImage_F == null)
        {
            bgImage_F = GetControl<Image>("BackgroundLayer");
            if (bgImage_F == null) return;
        }

        if (backgroundPath == currentAppliedBackgroundPath && bgImage_F.sprite != null)
        {
            return;
        }

        if (backgroundPath == "black")
        {
            if (_backgroundLoadCoroutine != null)
            {
                StopCoroutine(_backgroundLoadCoroutine);
                _backgroundLoadCoroutine = null;
            }

            bgImage_F.color = Color.black;
            bgImage_F.sprite = null;
            StretchBackgroundImage(bgImage_F);
            currentAppliedBackgroundPath = backgroundPath;
        }
        else
        {
            if (_backgroundLoadCoroutine != null)
            {
                StopCoroutine(_backgroundLoadCoroutine);
            }

            _backgroundLoadCoroutine = StartCoroutine(LoadAndApplyBackground(backgroundPath));
        }
    }

    public IEnumerator PreloadAndApplyBackgroundAsync(string backgroundPath)
    {
        if (bgImage_F == null)
        {
            bgImage_F = GetControl<Image>("BackgroundLayer");
            if (bgImage_F == null) yield break;
        }

        if (backgroundPath == currentAppliedBackgroundPath && bgImage_F.sprite != null)
        {
            yield break;
        }

        if (_backgroundLoadCoroutine != null)
        {
            StopCoroutine(_backgroundLoadCoroutine);
            _backgroundLoadCoroutine = null;
        }

        if (backgroundPath == "black")
        {
            bgImage_F.color = Color.black;
            bgImage_F.sprite = null;
            StretchBackgroundImage(bgImage_F);
            currentAppliedBackgroundPath = backgroundPath;
            yield break;
        }

        yield return LoadAndApplyBackground(backgroundPath);
    }

    private IEnumerator LoadAndApplyBackground(string backgroundPath)
    {
        List<string> candidatePaths = GetBackgroundCandidatePaths(backgroundPath);

        foreach (string path in candidatePaths)
        {
            bool applied = false;
            yield return LoadBackgroundSprite(path, (sprite) =>
            {
                if (sprite != null)
                {
                    bgImage_F.sprite = sprite;
                    bgImage_F.color = Color.white;
                    FitBackgroundImageToCover(bgImage_F);
                    applied = true;
                }
            });

            if (applied)
            {
                currentAppliedBackgroundPath = backgroundPath;
                _backgroundLoadCoroutine = null;
                yield break;
            }
        }

        Debug.LogError($"[VNGameplayPanel] 背景图片加载失败: {backgroundPath}");
        _backgroundLoadCoroutine = null;
    }

    private List<string> GetBackgroundCandidatePaths(string backgroundPath)
    {
        List<string> paths = new List<string>();

        if (backgroundPath.Contains("/"))
        {
            paths.Add(backgroundPath);
            return paths;
        }

        if (VNProjectConfig.Instance != null && !string.IsNullOrEmpty(VNProjectConfig.Instance.BackgroundResPath))
        {
            paths.Add(VNProjectConfig.Instance.BackgroundResPath + "/" + backgroundPath);
        }

        paths.Add("Backgrounds/" + backgroundPath);
        return paths;
    }

    private IEnumerator LoadBackgroundSprite(string path, Action<Sprite> onLoaded)
    {
        bool spriteLoaded = false;
        Sprite spriteResult = null;

        ResourcesManager.GetInstance().LoadOptionalAsync<Sprite>(path, (sprite) =>
        {
            spriteResult = sprite;
            spriteLoaded = true;
        });

        while (!spriteLoaded)
        {
            yield return null;
        }

        if (spriteResult != null)
        {
            onLoaded?.Invoke(spriteResult);
            yield break;
        }

        if (backgroundTextureSpriteCache.TryGetValue(path, out Sprite cachedSprite) && cachedSprite != null)
        {
            onLoaded?.Invoke(cachedSprite);
            yield break;
        }

        bool textureLoaded = false;
        Texture2D textureResult = null;

        ResourcesManager.GetInstance().LoadOptionalAsync<Texture2D>(path, (texture) =>
        {
            textureResult = texture;
            textureLoaded = true;
        });

        while (!textureLoaded)
        {
            yield return null;
        }

        if (textureResult == null)
        {
            onLoaded?.Invoke(null);
            yield break;
        }

        Sprite generatedSprite = Sprite.Create(
            textureResult,
            new Rect(0f, 0f, textureResult.width, textureResult.height),
            new Vector2(0.5f, 0.5f),
            100f);

        generatedSprite.name = textureResult.name;
        backgroundTextureSpriteCache[path] = generatedSprite;
        onLoaded?.Invoke(generatedSprite);
    }

    private void OnRectTransformDimensionsChange()
    {
        FitBackgroundImageToCover(bgImage_F);
        FitBackgroundImageToCover(bgImage_B);
    }

    private void StretchBackgroundImage(Image image)
    {
        if (image == null) return;

        AspectRatioFitter fitter = image.GetComponent<AspectRatioFitter>();
        if (fitter != null)
        {
            fitter.enabled = false;
        }

        RectTransform rect = image.rectTransform;
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.anchoredPosition = Vector2.zero;
        rect.sizeDelta = Vector2.zero;
        image.preserveAspect = false;
    }

    public void FitBackgroundImageToCover(Image image)
    {
        if (image == null || image.sprite == null)
        {
            StretchBackgroundImage(image);
            return;
        }

        float spriteWidth = image.sprite.rect.width;
        float spriteHeight = image.sprite.rect.height;
        if (spriteWidth <= 0f || spriteHeight <= 0f)
        {
            StretchBackgroundImage(image);
            return;
        }

        float spriteAspect = spriteWidth / spriteHeight;
        AspectRatioFitter fitter = image.GetComponent<AspectRatioFitter>();
        if (fitter == null)
        {
            fitter = image.gameObject.AddComponent<AspectRatioFitter>();
        }

        image.preserveAspect = true;
        fitter.enabled = true;
        fitter.aspectMode = AspectRatioFitter.AspectMode.EnvelopeParent;
        fitter.aspectRatio = spriteAspect;

        RectTransform rect = image.rectTransform;
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = Vector2.zero;
    }

    private void OnShowCharacter(Dictionary<string, string> characterInfo)
    {
        string position = characterInfo["position"];
        string characterID = characterInfo["characterID"];
        string emotion = characterInfo["emotion"];

        Image charImage = GetCharImage(position);
        if (charImage == null) return;

        CharacterProfile profile = CharacterResManager.GetInstance().GetCharacterProfile(characterID);
        if (profile == null) return;

        ElementSprite spriteEntry = CharacterResManager.GetInstance().GetCharacterSpriteEntry(characterID, emotion);
        if (spriteEntry == null) return;

        if (spriteEntry.Sprite != null)
        {
            ApplyCharacterSprite(position, charImage, profile, spriteEntry.Sprite);
        }
        else
        {
            PrepareCharacterSpriteLoading(position, charImage);
        }

        int version = GetNextCharacterSpriteLoadVersion(position);
        StartCoroutine(CharacterSpriteLoader.LoadSpriteAsync(spriteEntry, sprite =>
        {
            if (this == null || !IsCurrentCharacterSpriteLoad(position, version))
            {
                return;
            }

            if (sprite != null)
            {
                ApplyCharacterSprite(position, charImage, profile, sprite);
            }
        }));
    }

    private void PrepareCharacterSpriteLoading(string position, Image charImage)
    {
        if (charImage == null)
        {
            return;
        }

        SetCharacterVisibleState(position, true);
        if (charImage.sprite != null)
        {
            charImage.color = Color.white;
            charImage.gameObject.SetActive(!isUIHidden);
            return;
        }

        charImage.color = Color.clear;
        charImage.gameObject.SetActive(false);
    }

    private int GetNextCharacterSpriteLoadVersion(string position)
    {
        string key = position ?? string.Empty;
        int version = characterSpriteLoadVersions.TryGetValue(key, out int current) ? current + 1 : 1;
        characterSpriteLoadVersions[key] = version;
        return version;
    }

    private bool IsCurrentCharacterSpriteLoad(string position, int version)
    {
        string key = position ?? string.Empty;
        return characterSpriteLoadVersions.TryGetValue(key, out int current) && current == version;
    }

    private void ApplyCharacterSprite(string position, Image charImage, CharacterProfile profile, Sprite sprite)
    {
        if (charImage == null || profile == null || sprite == null)
        {
            return;
        }

        RectTransform charRect = charImage.rectTransform;
        Vector2 savedAnchorMin = charRect.anchorMin;
        Vector2 savedAnchorMax = charRect.anchorMax;

        charImage.sprite = sprite;
        charImage.color = Color.white;
        SetCharacterVisibleState(position, true);
        charImage.gameObject.SetActive(!isUIHidden);

        charImage.SetNativeSize();

        charRect.anchorMin = savedAnchorMin;
        charRect.anchorMax = savedAnchorMax;

        string posCode = position;
        if (position == "Left") posCode = "L";
        else if (position == "Mid") posCode = "M";
        else if (position == "Right") posCode = "R";

        if (!baseCharPositions.ContainsKey(posCode))
        {
            baseCharPositions[posCode] = charRect.anchoredPosition;
            VNDebug.LogVerbose($"[VNGameplayPanel] Saved base character position {position}({posCode}): {baseCharPositions[posCode]}");
        }

        Vector2 basePosition = baseCharPositions[posCode];
        charRect.anchoredPosition = basePosition + profile.offset;

        float profileScale = profile.scale > 0 ? profile.scale : 1.0f;
        Vector3 scale = Vector3.one * profileScale;

        float savedScaleX = VNManager.GetInstance().GetCharacterScaleX(posCode);
        if (savedScaleX != 1f)
        {
            scale.x = savedScaleX * profileScale;
        }
        charRect.localScale = scale;

        VNDebug.LogVerbose($"[VNGameplayPanel] Applied character sprite {position}({posCode}) - Scale: {profileScale}, Offset: {profile.offset}, Flip: {savedScaleX}, BasePos: {basePosition}, FinalPos: {charRect.anchoredPosition}");
    }
    private void OnHideCharacter(string position)
    {
        GetNextCharacterSpriteLoadVersion(position);
        Image charImage = GetCharImage(position);
        if (charImage != null)
        {
            SetCharacterVisibleState(position, false);
            charImage.gameObject.SetActive(false);
        }
    }
    
    /// <summary>
    /// 根据剧本中的 headProfile 字段刷新左侧头像。
    /// </summary>
    private void OnUpdateHeadProfile(Dictionary<string, string> headProfileInfo)
    {
        if (headProfileInfo == null) return;
        
        string headProfileValue = headProfileInfo.ContainsKey("headProfile") ? headProfileInfo["headProfile"] : "";
        string speaker = headProfileInfo.ContainsKey("speaker") ? headProfileInfo["speaker"] : "";
        
        string trimmedValue = headProfileValue.Trim().ToLower();
        if (string.IsNullOrEmpty(headProfileValue) || trimmedValue == "hide")
        {
            headSpriteLoadVersion++;
            if (headProfileTransform != null)
            {
                headProfileTransform.gameObject.SetActive(false);
                VNDebug.LogVerbose($"[VNGameplayPanel] HeadProfile hidden: {headProfileValue}");
            }
            return;
        }
        
        string[] parts = headProfileValue.Trim().Split('_');
        if (parts.Length < 2)
        {
            Debug.LogWarning($"[VNGameplayPanel] HeadProfile 格式错误: {headProfileValue}，应为 CharacterID_Emotion");
            if (headProfileTransform != null) headProfileTransform.gameObject.SetActive(false);
            return;
        }
        
        string characterID = parts[0].Trim();
        string emotion = parts[1].Trim();
        
        CharacterProfile profile = CharacterResManager.GetInstance().TryGetCharacterProfile(characterID);
        if (profile != null)
        {
            ElementSprite headEntry = CharacterResManager.GetInstance().GetHeadSpriteEntry(characterID, emotion);
            if (headEntry == null)
            {
                Debug.LogWarning($"[VNGameplayPanel] Character {characterID} has no head sprite for emotion {emotion}");
                if (headProfileTransform != null) headProfileTransform.gameObject.SetActive(false);
                return;
            }

            if (headEntry.Sprite != null && headImage != null)
            {
                ApplyHeadSprite(headEntry.Sprite);
                if (headProfileTransform != null)
                {
                    headProfileTransform.gameObject.SetActive(true);
                }
            }
            else
            {
                PrepareHeadSpriteLoading();
            }

            int version = ++headSpriteLoadVersion;
            StartCoroutine(CharacterSpriteLoader.LoadSpriteAsync(headEntry, sprite =>
            {
                if (this == null || version != headSpriteLoadVersion || headImage == null)
                {
                    return;
                }

                if (sprite != null)
                {
                    ApplyHeadSprite(sprite);
                    if (headProfileTransform != null)
                    {
                        headProfileTransform.gameObject.SetActive(true);
                    }
                }
                else
                {
                    Debug.LogWarning($"[VNGameplayPanel] Character {characterID} has no head sprite for emotion {emotion}");
                    if (headProfileTransform != null) headProfileTransform.gameObject.SetActive(false);
                }
            }));

            ApplyHeadFrame(profile);
        }
        else
        {
            ApplyHeadFrame(null);
            Debug.LogWarning($"[VNGameplayPanel] Character profile not found: {characterID}; hiding head profile");
            if (headProfileTransform != null) headProfileTransform.gameObject.SetActive(false);
        }
    }

    private void ApplyHeadSprite(Sprite sprite)
    {
        if (headImage == null || sprite == null)
        {
            return;
        }

        headImage.sprite = sprite;
        headImage.color = Color.white;
    }

    private void PrepareHeadSpriteLoading()
    {
        if (headImage != null && headImage.sprite == null)
        {
            headImage.color = Color.clear;
            if (headProfileTransform != null)
            {
                headProfileTransform.gameObject.SetActive(false);
            }
        }
    }

    private void ApplyHeadFrame(CharacterProfile profile)
    {
        if (headFrame == null)
        {
            return;
        }

        Sprite frameSprite = null;
        if (profile != null && profile.HeadFrame != null)
        {
            frameSprite = profile.HeadFrame;
        }
        else
        {
            frameSprite = defaultHeadFrameSprite;
            if (frameSprite == null && VNProjectConfig.Instance != null)
            {
                frameSprite = VNProjectConfig.Instance.DefaultHeadFrameSprite;
            }
        }

        headFrame.sprite = frameSprite;
        headFrame.gameObject.SetActive(frameSprite != null);
    }
    private string currentFullText = "";

    private IEnumerator TypeText(string text)
    {
        isTextTyping = true;
        currentFullText = text;

        if (dialogueText == null)
        {
            isTextTyping = false;
            yield break;
        }

        dialogueText.text = "";
        Color textColor = dialogueText.color;
        textColor.a = 1f;
        dialogueText.color = textColor;

        for (int i = 0; i <= text.Length; i++)
        {
            dialogueText.text = text.Substring(0, i);

            if (i < text.Length)
            {
                yield return new WaitForSeconds(textSpeed);
            }
        }

        isTextTyping = false;
        //currentTypingCoroutine = null;
        EventCenter.GetInstance().EventTrigger(VNGameEvents.TypingFinished);
    }

    private void CompleteTextTyping()
    {
        VNDebug.LogVerbose($"[TypingTrace][CompleteTextTyping] enter isTextTyping={isTextTyping}, tweenAlive={_typewriterTween.isAlive}");
        if (!isTextTyping)
            return;

        _lastManualTypingCompleteUnscaledTime = Time.unscaledTime;
        VNDebug.LogVerbose($"[TypingTrace][CompleteTextTyping] set manualCompleteTime={_lastManualTypingCompleteUnscaledTime}");

        if (_typewriterTween.isAlive)
        {
            VNDebug.LogVerbose("[TypingTrace][CompleteTextTyping] call tween.Complete()");
            _typewriterTween.Complete();
        }

        if (isTextTyping)
        {
            VNDebug.LogVerbose("[TypingTrace][CompleteTextTyping] fallback FinishTypewriterDisplay()");
            FinishTypewriterDisplay();
        }
    }

    // private void OnAutoButtonClick()
    // {
    //     if (GameStateManager.GetInstance().CanInteractGameplay())
    //         VNManager.GetInstance().ToggleAutoPlay();

    //     isAutoPlaying = !isAutoPlaying;
    //     UpdateAutoButtonState();
    // }
    private void OnAutoButtonClick()
    {
        if (!GameStateManager.GetInstance().CanInteractGameplay())
            return;

        VNManager.GetInstance().ToggleAutoPlay();
        UpdateAutoButtonState();
    }

    private void OnSkipButtonClick()
    {
        if (!GameStateManager.GetInstance().CanInteractGameplay())
        {
            Debug.LogWarning($"[VNGameplayPanel] Cannot skip in current state: {GameStateManager.GetInstance().CurrentState}");
            return;
        }
        
        if (GameStateManager.GetInstance().CurrentState == GameState.Choice)
        {
            Debug.LogWarning("[VNGameplayPanel] Skip is not allowed during Choice state");
            return;
        }
        
        isSkipping = !isSkipping;
        UpdateSkipButtonState();
        
        if (isSkipping && !isTextTyping && !isAutoPlaying && !isUIHidden)
        {
            VNManager.GetInstance().NextLine();
        }
    }

    private void OnSaveButtonClick()
    {
        if (!GameStateManager.GetInstance().CanOpenPanel(GameState.SaveLoad))
        {
            Debug.LogWarning("[VNGameplayPanel] Cannot open save/load panel in current state");
            return;
        }

        OpenSavePanelAfterScreenshot();
    }

    private void OpenSavePanelAfterScreenshot()
    {
        SaveManager.GetInstance().CaptureCurrentScreen(() =>
        {
            if (this == null || !isActiveAndEnabled)
                return;

            if (!GameStateManager.GetInstance().CanOpenPanel(GameState.SaveLoad))
                return;

            ShowSaveLoadPanelWithDarkFade(SaveLoadPanel.Mode.Save);
        });
    }

    private void OnLoadButtonClick()
    {
        if (!GameStateManager.GetInstance().CanOpenPanel(GameState.SaveLoad))
        {
            Debug.LogWarning("[VNGameplayPanel] Cannot open save/load panel in current state");
            return;
        }

        ShowSaveLoadPanelWithDarkFade(SaveLoadPanel.Mode.Load);
    }

    private void ShowSaveLoadPanelWithDarkFade(SaveLoadPanel.Mode mode)
    {
        UIManager.GetInstance().ShowPanelWithDarkFade<SaveLoadPanel>(
            "SaveLoadPanel",
            VNProjectConfig.Instance.UI_SaveLoadPath,
            E_UI_Layer.Top,
            panel =>
            {
                if (panel == null)
                    return;

                panel.SetOpenedFromMainMenu(false);
                panel.SetMode(mode);
            });
    }

    private void OnLogButtonClick()
    {
        if (!GameStateManager.GetInstance().CanOpenPanel(GameState.History))
        {
            Debug.LogWarning("[VNGameplayPanel] Cannot open history panel in current state");
            return;
        }

        var historyPanel = UIManager.GetInstance().GetPanel<HistoryPanel>("HistoryPanel");

        if (historyPanel != null && historyPanel.gameObject.activeSelf)
        {
            UIManager.GetInstance().HidePanel("HistoryPanel");
            GameStateManager.GetInstance().RestoreState();
        }
        else
        {
            GameStateManager.GetInstance().SetState(GameState.History);
            UIManager.GetInstance().ShowPanel<HistoryPanel>("HistoryPanel", VNProjectConfig.Instance.UI_HistoryPath, E_UI_Layer.Top, null);
        }
    }

    private void OnHideButtonClick()
    {
        if (GameStateManager.GetInstance().CurrentState == GameState.Panorama)
            return;

        ToggleUI();
    }

    // private void UpdateAutoButtonState()
    // {
    //     TMP_Text buttonText = autoButton.GetComponentInChildren<TMP_Text>();
    //     if (buttonText != null)
    //     {
    //         buttonText.text = isAutoPlaying ? "Auto (On)" : "Auto (Off)";
    //     }
    // }
    private void UpdateAutoButtonState()
    {
        isAutoPlaying = VNManager.GetInstance() != null && VNManager.GetInstance().IsAutoPlaying();

        Image buttonImage = autoButton != null ? autoButton.GetComponent<Image>() : null;
        if (buttonImage != null)
        {
            buttonImage.sprite = isAutoPlaying ? autoPauseSprite : autoContinueSprite;
        }
    }

    private void UpdateSkipButtonState()
    {
        TMP_Text buttonText = skipButton.GetComponentInChildren<TMP_Text>();
        if (buttonText != null)
        {
            buttonText.text = isSkipping ? "Skip (On)" : "Skip (Off)";
        }
    }

    private void ToggleUI()
    {
        isUIHidden = !isUIHidden;
        ApplyHiddenUIState();
    }

    private void ShowHiddenUI()
    {
        isUIHidden = false;
        ApplyHiddenUIState();
        GuardRestoredUIFromCurrentClick();
    }

    private void ApplyHiddenUIState()
    {
        if (uiRoot != null)
        {
            uiRoot.gameObject.SetActive(!isUIHidden);
        }

        if (pauseButton != null)
        {
            pauseButton.gameObject.SetActive(!isUIHidden);
        }

        ApplyCharacterVisibility();
    }

    private void ApplyCharacterVisibility()
    {
        SetCharacterObjectVisible(charLeftImage, isCharLeftVisible);
        SetCharacterObjectVisible(charMidImage, isCharMidVisible);
        SetCharacterObjectVisible(charRightImage, isCharRightVisible);
    }

    private void SetCharacterObjectVisible(Image charImage, bool shouldShow)
    {
        if (charImage != null)
        {
            charImage.gameObject.SetActive(shouldShow && !isUIHidden && charImage.sprite != null);
        }
    }

    private void GuardRestoredUIFromCurrentClick()
    {
        if (restoreInputGuardCoroutine != null)
        {
            StopCoroutine(restoreInputGuardCoroutine);
        }

        restoreInputGuardCoroutine = StartCoroutine(GuardRestoredUIFromCurrentClickCoroutine());
    }

    private IEnumerator GuardRestoredUIFromCurrentClickCoroutine()
    {
        SetRestoredUIRaycasts(false);

        while (IsPrimaryPointerPressed())
        {
            yield return null;
        }

        yield return null;
        SetRestoredUIRaycasts(true);
        restoreInputGuardCoroutine = null;
    }

    private bool IsPrimaryPointerPressed()
    {
        if (Mouse.current != null && Mouse.current.leftButton.isPressed)
        {
            return true;
        }

        return Input.GetMouseButton(0);
    }

    private void SetRestoredUIRaycasts(bool enabled)
    {
        if (uiRootCanvasGroup != null)
        {
            uiRootCanvasGroup.blocksRaycasts = enabled;
            uiRootCanvasGroup.interactable = enabled;
        }

        if (pauseButtonCanvasGroup != null)
        {
            pauseButtonCanvasGroup.blocksRaycasts = enabled;
            pauseButtonCanvasGroup.interactable = enabled;
        }
    }

    private void ConfigurePauseButtonOverlay()
    {
        if (pauseButton == null)
        {
            return;
        }

        pauseButtonCanvas = pauseButton.GetComponent<Canvas>();
        if (pauseButtonCanvas == null)
        {
            pauseButtonCanvas = pauseButton.gameObject.AddComponent<Canvas>();
        }

        pauseButtonCanvas.overrideSorting = false;
        pauseButtonCanvas.sortingOrder = 0;

        GraphicRaycaster raycaster = pauseButton.GetComponent<GraphicRaycaster>();
        if (raycaster == null)
        {
            pauseButton.gameObject.AddComponent<GraphicRaycaster>();
        }
    }

    private void UpdatePauseButtonOverlaySorting()
    {
        if (pauseButtonCanvas == null)
        {
            return;
        }

        bool shouldRaise = GameStateManager.GetInstance().CurrentState == GameState.Choice && !isUIHidden;
        SetPauseButtonOverlayRaised(shouldRaise);
    }

    private void SetPauseButtonOverlayRaised(bool shouldRaise)
    {
        if (pauseButtonCanvas == null)
        {
            return;
        }

        int sortingOrder = shouldRaise ? PauseButtonChoiceSortingOrder : 0;
        if (pauseButtonCanvas.overrideSorting == shouldRaise && pauseButtonCanvas.sortingOrder == sortingOrder)
        {
            return;
        }

        pauseButtonCanvas.overrideSorting = shouldRaise;
        pauseButtonCanvas.sortingOrder = sortingOrder;
    }

    private void SetCharacterVisibleState(string position, bool visible)
    {
        switch (position)
        {
            case "Left":
            case "L":
                isCharLeftVisible = visible;
                break;
            case "Mid":
            case "M":
                isCharMidVisible = visible;
                break;
            case "Right":
            case "R":
                isCharRightVisible = visible;
                break;
        }
    }

    private void OnTextSpeedChanged()
    {
        float newSpeed = GlobalDataManager.GetInstance().GetGlobalData().TextSpeed;

        textSpeed = newSpeed;


        if (isTextTyping && _typewriterTween.isAlive && newSpeed > 0.00001f)
        {
            float newTimeScale = currentBaseSpeed / newSpeed;

            _typewriterTween.timeScale = newTimeScale;
        }
    }

    private void OnAutoSpeedChanged()
    {
        autoSpeed = GlobalDataManager.GetInstance().GetGlobalData().AutoSpeed;
    }

    private void OnDestroy()
    {
        IsInitialized = false;
        OnInitialized = null;
        EventCenter.GetInstance().RemoveEventListener<Dictionary<string, string>>(VNGameEvents.UpdateDialogue, OnUpdateDialogue);
        EventCenter.GetInstance().RemoveEventListener<string>(VNGameEvents.ChangeBackground, OnChangeBackground);
        EventCenter.GetInstance().RemoveEventListener<Dictionary<string, string>>(VNGameEvents.ShowCharacter, OnShowCharacter);
        EventCenter.GetInstance().RemoveEventListener<string>(VNGameEvents.HideCharacter, OnHideCharacter);
        EventCenter.GetInstance().RemoveEventListener<Dictionary<string, string>>(VNGameEvents.UpdateHeadProfile, OnUpdateHeadProfile);
        EventCenter.GetInstance().RemoveEventListener(VNGameEvents.DisplayAllText, OnDisplayAllText);
        EventCenter.GetInstance().RemoveEventListener("TextSpeedChanged", OnTextSpeedChanged);
        EventCenter.GetInstance().RemoveEventListener("AutoSpeedChanged", OnAutoSpeedChanged);
        if (StaminaManager.IsEnabled)
        {
            StaminaManager.GetInstance().OnStaminaChanged -= OnStaminaChanged;
            StaminaManager.GetInstance().OnRecoveryTimerChanged -= RefreshStaminaTimer;
        }

        if (staminaTimerCoroutine != null)
        {
            StopCoroutine(staminaTimerCoroutine);
            staminaTimerCoroutine = null;
        }


        Time.timeScale = 1f;
    }

    private void ShowContinueIcon()
    {
        if (continueIcon == null) return;
        if (continueIcon.gameObject.activeSelf && _iconSequence.isAlive) return;

        continueIcon.gameObject.SetActive(true);

        if (_iconSequence.isAlive)
        {
            _iconSequence.Stop();
        }

        RectTransform rect = continueIcon.rectTransform;
        Vector2 anchoredPosition = rect.anchoredPosition;
        anchoredPosition.y = 0f;
        rect.anchoredPosition = anchoredPosition;
        rect.localRotation = Quaternion.identity;
        continueIcon.color = Color.white;

        _iconSequence = Sequence.Create(cycles: -1, cycleMode: Sequence.SequenceCycleMode.Yoyo)
            .Group(Tween.UIAnchoredPositionY(rect, 0f, 10f, 0.55f, Ease.InOutSine))
            .Group(Tween.Alpha(continueIcon, 1f, 0.45f, 0.55f, Ease.InOutSine));
    }

    private void HideContinueIcon()
    {
        if (_iconSequence.isAlive)
        {
            _iconSequence.Stop();
        }

        if (continueIcon != null)
        {
            continueIcon.gameObject.SetActive(false);
        }
    }


    public void ShowPrompt(string text, float duration)
    {
        if (promptPrefab == null || promptContainer == null) return;

        GameObject go = Instantiate(promptPrefab, promptContainer);
        go.transform.SetAsLastSibling();

        TMP_Text txt = go.GetComponentInChildren<TMP_Text>();
        if (txt != null) txt.text = text;

        CanvasGroup cg = go.GetComponent<CanvasGroup>();
        if (cg == null) cg = go.AddComponent<CanvasGroup>();

        RectTransform rect = go.GetComponent<RectTransform>();

        cg.alpha = 0;
        float width = rect.sizeDelta.x;
        rect.anchoredPosition = new Vector2(-width, rect.anchoredPosition.y);

        Sequence.Create()
            .Group(Tween.Alpha(cg, 1, 0.5f, Ease.OutQuad))
            .Group(Tween.UIAnchoredPositionX(rect, 0, 0.5f, Ease.OutBack))
            .ChainDelay(duration)
            .Chain(Tween.Alpha(cg, 0, 0.5f, Ease.InQuad))
            .Group(Tween.UIAnchoredPositionX(rect, -width, 0.5f, Ease.InQuad)) // 缁夎鍤?
            .OnComplete(() => Destroy(go));
    }
    #endregion

    #region 外部访问和状态恢复
    public RectTransform GetCharRect(string posCode)
    {
        if (string.IsNullOrEmpty(posCode)) return null;

        switch (posCode.ToUpper())
        {
            case "L":
            case "LEFT":
                return charLeftImage != null ? charLeftImage.rectTransform : null;

            case "M":
            case "MID":
            case "MIDDLE":
                return charMidImage != null ? charMidImage.rectTransform : null;

            case "R":
            case "RIGHT":
                return charRightImage != null ? charRightImage.rectTransform : null;

            default:
                Debug.LogWarning($"[VNGameplayPanel] 未知角色位置: {posCode}");
                return null;
        }
    }

    public Image GetCharImage(string position)
    {
        switch (position)
        {
            case "Left":
            case "L":
                return charLeftImage;
            case "Mid":
            case "M":
                return charMidImage;
            case "Right":
            case "R":
                return charRightImage;
            default:
                Debug.LogError($"Invalid character position: {position}");
                return null;
        }
    }

    public Image GetBG_F()
    {
        if (bgImage_F != null)
        {
            return bgImage_F;
        }
        return null;
    }

    public Image GetBG_B()
    {
        if (bgImage_B != null)
        { 
            return bgImage_B;
        }
        return null;
        
    }

    public TMP_Text GetDialogueText()
    {
        if (dialogueText != null)
        { 
            return dialogueText;
        }
        return null;
    
    }

    public Image GetSpeakerBox()
    {
        if (speakerBox != null)
        { 
            return speakerBox;
        }
        return null;
    }
    public TMP_Text GetSpeakerText()
    {
        if (speakerText != null)
        {
            return speakerText;
        }
        return null;
    }

    public Transform GetEffectLayer() => effectLayer;

    public Transform GetPanoramaLayerRoot()
    {
        return transform;
    }

    public void SetPanoramaObservationActive(bool active)
    {
        if (uiRootCanvasGroup == null && uiRoot != null)
        {
            uiRootCanvasGroup = uiRoot.GetComponent<CanvasGroup>();
            if (uiRootCanvasGroup == null)
            {
                uiRootCanvasGroup = uiRoot.gameObject.AddComponent<CanvasGroup>();
            }
        }

        if (uiRootCanvasGroup == null) return;

        if (active)
        {
            if (!isPanoramaUiSuppressed)
            {
                uiRootAlphaBeforePanorama = uiRootCanvasGroup.alpha;
                uiRootInteractableBeforePanorama = uiRootCanvasGroup.interactable;
                uiRootBlocksRaycastsBeforePanorama = uiRootCanvasGroup.blocksRaycasts;
                isPanoramaUiSuppressed = true;
            }

            uiRootCanvasGroup.alpha = 0f;
            uiRootCanvasGroup.interactable = false;
            uiRootCanvasGroup.blocksRaycasts = false;
            HideContinueIcon();
            return;
        }

        if (!isPanoramaUiSuppressed) return;

        uiRootCanvasGroup.alpha = uiRootAlphaBeforePanorama;
        uiRootCanvasGroup.interactable = uiRootInteractableBeforePanorama;
        uiRootCanvasGroup.blocksRaycasts = uiRootBlocksRaycastsBeforePanorama;
        isPanoramaUiSuppressed = false;
        RefreshContinueIconState();
    }
    
    /// <summary>
    /// 当前是否仍在逐字显示文本。
    /// </summary>
    public bool IsTextTyping()
    {
        return isTextTyping;
    }

    public void RefreshContinueIconState()
    {
        bool shouldShow = !isTextTyping &&
                          !VNManager.GetInstance().IsTextDisplaying() &&
                          !VNManager.GetInstance().IsFlowRunning() &&
                          !CommandManager.GetInstance().IsRunning;

        VNDebug.LogVerbose($"[TypingTrace][RefreshContinueIconState] shouldShow={shouldShow}, isTextTyping={isTextTyping}, isTextDisplaying={VNManager.GetInstance().IsTextDisplaying()}, flowRunning={VNManager.GetInstance().IsFlowRunning()}, cmdRunning={CommandManager.GetInstance().IsRunning}");

        if (shouldShow)
        {
            ShowContinueIcon();
        }
        else
        {
            HideContinueIcon();
        }
    }

    public void CompleteDialogueTyping() => CompleteTextTyping();
    
    /// <summary>
    /// 记录角色当前位置和缩放，供临时移动后恢复。
    /// </summary>
    public void SaveDefaultCharTransform(string posCode)
    {
        RectTransform rect = GetCharRect(posCode);
        if (rect != null)
        {
            string normalizedPos = NormalizePositionCode(posCode);
            if (!defaultCharPositions.ContainsKey(normalizedPos))
            {
                defaultCharPositions[normalizedPos] = rect.anchoredPosition;
                defaultCharScales[normalizedPos] = rect.localScale.y;
                VNDebug.LogVerbose($"[VNGameplayPanel] 保存默认角色 Transform {posCode}({normalizedPos}): position={rect.anchoredPosition}, scale={rect.localScale.y}");
            }
            modifiedCharTransforms.Add(normalizedPos);
        }
    }
    
    /// <summary>
    /// 将被临时修改过的角色位置和缩放恢复为默认值。
    /// </summary>
    public void RestoreDefaultCharTransforms()
    {
        if (modifiedCharTransforms.Count == 0) return;
        
        foreach (string posCode in modifiedCharTransforms)
        {
            RectTransform rect = GetCharRect(posCode);
            if (rect != null && defaultCharPositions.ContainsKey(posCode) && defaultCharScales.ContainsKey(posCode))
            {
                rect.anchoredPosition = defaultCharPositions[posCode];
                
                Vector3 currentScale = rect.localScale;
                float defaultScale = defaultCharScales[posCode];
                float scaleX = Mathf.Sign(currentScale.x) * Mathf.Abs(defaultScale);
                rect.localScale = new Vector3(scaleX, defaultScale, 1f);
                
                VNDebug.LogVerbose($"[VNGameplayPanel] Restored default transform {posCode}: position={defaultCharPositions[posCode]}, scale={defaultScale}");
            }
        }
        
        modifiedCharTransforms.Clear();
    }
    
    /// <summary>
    /// 将角色位置别名统一为 L、M、R。
    /// </summary>
    private string NormalizePositionCode(string posCode)
    {
        if (string.IsNullOrEmpty(posCode)) return posCode;
        string upper = posCode.ToUpper();
        if (upper == "LEFT" || upper == "L") return "L";
        if (upper == "MID" || upper == "MIDDLE" || upper == "M") return "M";
        if (upper == "RIGHT" || upper == "R") return "R";
        return posCode;
    }

    /// <summary>
    /// 获取对话框 RectTransform，供命令或效果调整位置。
    /// </summary>
    public RectTransform GetDialogueBoxRect()
    {
        if (uiRoot != null)
        {
            Transform dialogueBox = uiRoot.Find("DialogueBox");
            if (dialogueBox != null)
            {
                return dialogueBox.GetComponent<RectTransform>();
            }
        }
        if (dialogueText != null && dialogueText.transform.parent != null)
        {
            return dialogueText.transform.parent.GetComponent<RectTransform>();
        }
        return null;
    }

    /// <summary>
    /// 首次修改文本样式前保存默认颜色和字号。
    /// </summary>
    private void SaveDefaultTextProperties()
    {
        if (dialogueText == null) return;

        if (!defaultDialogueTextColor.HasValue)
        {
            defaultDialogueTextColor = dialogueText.color;
        }
        if (!defaultDialogueTextSize.HasValue)
        {
            defaultDialogueTextSize = dialogueText.fontSize;
        }
        isDialogueTextModified = true;
    }

    /// <summary>
    /// 临时设置对话文本颜色。
    /// </summary>
    public void SetDialogueTextColor(Color color)
    {
        if (dialogueText == null) return;

        SaveDefaultTextProperties();
        dialogueText.color = color;
    }

    /// <summary>
    /// 临时设置对话文本字号。
    /// </summary>
    public void SetDialogueTextSize(float size)
    {
        if (dialogueText == null) return;

        SaveDefaultTextProperties();
        dialogueText.fontSize = size;
    }

    /// <summary>
    /// 恢复对话文本的默认颜色和字号。
    /// </summary>
    public void RestoreDefaultTextProperties()
    {
        if (!isDialogueTextModified || dialogueText == null) return;

        if (defaultDialogueTextColor.HasValue)
        {
            dialogueText.color = defaultDialogueTextColor.Value;
        }
        if (defaultDialogueTextSize.HasValue)
        {
            dialogueText.fontSize = defaultDialogueTextSize.Value;
        }

        isDialogueTextModified = false;
    }
    #endregion
}
