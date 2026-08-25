using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using PrimeTween;
using VNovelizer.Core.API;

/// <summary>
/// 暂停面板
/// </summary>
public class PausePanel : BasePanel
{
    #region UI控件引用

    [SerializeField] private Button saveBtn;
    [SerializeField] private Button loadBtn;
    [SerializeField] private Button settingsBtn;
    [SerializeField] private Button exitBtn;
    [SerializeField] private Button closeBtn;

    [Header("Slide Animation")]
    [SerializeField] private bool enableSlideAnimation = true;
    [SerializeField] private float slideDuration = 0.24f;
    [SerializeField] private float slideOvershootDistance = 34f;

    private RectTransform panelRect;
    private CanvasGroup panelCanvasGroup;
    private Vector2 panelOriginalAnchoredPosition;
    private Coroutine slideAnimationCoroutine;
    private GameObject inputBlocker;
    private const int InputBlockerSortingOrder = 6;

    #endregion

    #region 初始化

    protected override void Awake()
    {
        base.Awake();
        InitializeSlideAnimation();
        
        // 初始化控件
        InitializeControls();
        
        // 绑定事件
        BindEvents();
    }

    private void InitializeSlideAnimation()
    {
        panelRect = transform as RectTransform;
        if (panelRect != null)
        {
            panelOriginalAnchoredPosition = panelRect.anchoredPosition;
        }

        panelCanvasGroup = GetComponent<CanvasGroup>();
        if (panelCanvasGroup == null)
        {
            panelCanvasGroup = gameObject.AddComponent<CanvasGroup>();
        }
    }
    
    /// <summary>
    /// 初始化控件
    /// </summary>
    private void InitializeControls()
    {
        saveBtn = GetControl<Button>("SaveBtn");
        loadBtn = GetControl<Button>("LoadBtn");
        settingsBtn = GetControl<Button>("SettingsBtn");
        exitBtn = GetControl<Button>("ExitBtn");
        closeBtn = GetControl<Button>("CloseBtn");
        
        // 检查关键控件是否存在
        if (saveBtn == null)
            Debug.LogError("[PausePanel] 找不到 SaveBtn 按钮！");
        if (loadBtn == null)
            Debug.LogError("[PausePanel] 找不到 LoadBtn 按钮！");
        if (settingsBtn == null)
            Debug.LogError("[PausePanel] 找不到 SettingsBtn 按钮！");
        if (exitBtn == null)
            Debug.LogError("[PausePanel] 找不到 ExitBtn 按钮！");
        if (closeBtn == null)
            Debug.LogError("[PausePanel] 找不到 CloseBtn 按钮！");
    }
    
    /// <summary>
    /// 绑定事件
    /// </summary>
    private void BindEvents()
    {
        if (saveBtn != null)
            saveBtn.onClick.AddListener(OnSaveBtnClick);
        if (loadBtn != null)
            loadBtn.onClick.AddListener(OnLoadBtnClick);
        if (settingsBtn != null)
            settingsBtn.onClick.AddListener(OnSettingsBtnClick);
        if (exitBtn != null)
            exitBtn.onClick.AddListener(OnExitBtnClick);
        if (closeBtn != null)
            closeBtn.onClick.AddListener(OnCloseBtnClick);
    }
    
    /// <summary>
    /// 解绑事件（用于清理）
    /// </summary>
    private void UnbindEvents()
    {
        if (saveBtn != null)
            saveBtn.onClick.RemoveListener(OnSaveBtnClick);
        if (loadBtn != null)
            loadBtn.onClick.RemoveListener(OnLoadBtnClick);
        if (settingsBtn != null)
            settingsBtn.onClick.RemoveListener(OnSettingsBtnClick);
        if (exitBtn != null)
            exitBtn.onClick.RemoveListener(OnExitBtnClick);
        if (closeBtn != null)
            closeBtn.onClick.RemoveListener(OnCloseBtnClick);
    }
    
    #endregion
    
    #region Unity生命周期
    
    protected override void OnEnable()
    {
        base.OnEnable();
        
        // 每次打开面板时设置状态
        // 检查是否可以打开（允许在Gameplay、AutoPlay、Choice和Pause状态下打开）
        GameState currentState = GameStateManager.GetInstance().CurrentState;
        if (currentState != GameState.Gameplay && currentState != GameState.AutoPlay && 
            currentState != GameState.Choice && currentState != GameState.Pause)
        {
            Debug.LogWarning($"[PausePanel] 当前状态 {currentState} 不允许打开暂停面板，已关闭");
            gameObject.SetActive(false);
            return;
        }
        
        // 只有在非Pause状态时才设置Pause状态
        // 如果已经在Pause状态，不需要重复设置
        if (currentState != GameState.Pause)
        {
            GameStateManager.GetInstance().SetState(GameState.Pause);
        }
    }
    
    public override void ShowMe()
    {
        gameObject.SetActive(true);
        if (!gameObject.activeInHierarchy)
        {
            return;
        }

        EnsureInputBlocker();
        PlaySlideAnimation();
    }
    
    public override void HideMe()
    {
        StopSlideAnimation();
        ResetSlideAnimationState();
        HideInputBlocker();
        gameObject.SetActive(false);
        
        // 恢复游戏状态
        if (GameStateManager.GetInstance() != null && 
            GameStateManager.GetInstance().CurrentState == GameState.Pause)
        {
            GameStateManager.GetInstance().RestoreState();
        }
    }
    
    private void OnDestroy()
    {
        StopSlideAnimation();
        ResetSlideAnimationState();
        HideInputBlocker();

        // 清理事件监听
        UnbindEvents();
        
        // 面板被Destroy时，如果当前状态是Pause，需要恢复游戏状态
        if (GameStateManager.GetInstance() != null && 
            GameStateManager.GetInstance().CurrentState == GameState.Pause)
        {
            GameStateManager.GetInstance().RestoreState();
            Debug.Log("[PausePanel] 面板被Destroy，已恢复游戏状态");
        }
    }
    
    #endregion

    #region 入场动画

    private void PlaySlideAnimation()
    {
        if (!enableSlideAnimation)
        {
            ResetSlideAnimationState();
            return;
        }

        if (panelRect == null || panelCanvasGroup == null)
        {
            InitializeSlideAnimation();
        }

        StopSlideAnimation();
        slideAnimationCoroutine = StartCoroutine(SlideAnimationCoroutine());
    }

    private IEnumerator SlideAnimationCoroutine()
    {
        float duration = Mathf.Max(0.01f, slideDuration);
        float elapsed = 0f;
        Vector2 targetPosition = panelOriginalAnchoredPosition;
        Vector2 startPosition = GetLeftOffscreenPosition(targetPosition);
        Vector2 overshootPosition = targetPosition + Vector2.right * Mathf.Max(0f, slideOvershootDistance);

        panelCanvasGroup.alpha = 1f;
        panelCanvasGroup.interactable = false;
        panelCanvasGroup.blocksRaycasts = true;
        panelRect.anchoredPosition = startPosition;

        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            panelRect.anchoredPosition = EvaluateBrakeSlide(startPosition, overshootPosition, targetPosition, t);
            yield return null;
        }

        slideAnimationCoroutine = null;
        ResetSlideAnimationState();
    }

    private Vector2 GetLeftOffscreenPosition(Vector2 targetPosition)
    {
        float parentWidth = Screen.width;
        RectTransform parentRect = panelRect != null ? panelRect.parent as RectTransform : null;
        if (parentRect != null && parentRect.rect.width > 0f)
        {
            parentWidth = parentRect.rect.width;
        }

        float panelWidth = panelRect != null ? panelRect.rect.width : 0f;
        return new Vector2(targetPosition.x - parentWidth - panelWidth, targetPosition.y);
    }

    private Vector2 EvaluateBrakeSlide(Vector2 startPosition, Vector2 overshootPosition, Vector2 targetPosition, float t)
    {
        if (t < 0.78f)
        {
            float firstPhaseT = EaseOutCubic(t / 0.78f);
            return Vector2.LerpUnclamped(startPosition, overshootPosition, firstPhaseT);
        }

        float brakeT = EaseOutCubic((t - 0.78f) / 0.22f);
        return Vector2.LerpUnclamped(overshootPosition, targetPosition, brakeT);
    }

    private float EaseOutCubic(float t)
    {
        t = Mathf.Clamp01(t);
        return 1f - Mathf.Pow(1f - t, 3f);
    }

    private void StopSlideAnimation()
    {
        if (slideAnimationCoroutine != null)
        {
            StopCoroutine(slideAnimationCoroutine);
            slideAnimationCoroutine = null;
        }
    }

    private void ResetSlideAnimationState()
    {
        if (panelCanvasGroup != null)
        {
            panelCanvasGroup.alpha = 1f;
            panelCanvasGroup.interactable = true;
            panelCanvasGroup.blocksRaycasts = true;
        }

        if (panelRect != null)
        {
            panelRect.anchoredPosition = panelOriginalAnchoredPosition;
        }
    }

    #endregion

    #region 输入遮罩

    private void EnsureInputBlocker()
    {
        const string blockerName = "PauseInputBlocker";
        Transform parent = transform.parent;
        if (parent == null)
        {
            return;
        }

        if (inputBlocker == null)
        {
            Transform existingBlocker = parent.Find(blockerName);
            inputBlocker = existingBlocker != null
                ? existingBlocker.gameObject
                : new GameObject(blockerName, typeof(RectTransform), typeof(Image), typeof(Button));
        }

        inputBlocker.transform.SetParent(parent, false);
        inputBlocker.transform.SetAsLastSibling();
        transform.SetAsLastSibling();
        inputBlocker.SetActive(true);

        Canvas blockerCanvas = inputBlocker.GetComponent<Canvas>();
        if (blockerCanvas == null)
        {
            blockerCanvas = inputBlocker.AddComponent<Canvas>();
        }
        blockerCanvas.overrideSorting = true;
        blockerCanvas.sortingOrder = InputBlockerSortingOrder;

        GraphicRaycaster blockerRaycaster = inputBlocker.GetComponent<GraphicRaycaster>();
        if (blockerRaycaster == null)
        {
            blockerRaycaster = inputBlocker.AddComponent<GraphicRaycaster>();
        }

        Canvas panelCanvas = GetComponent<Canvas>();
        if (panelCanvas == null)
        {
            panelCanvas = gameObject.AddComponent<Canvas>();
        }
        panelCanvas.overrideSorting = true;
        panelCanvas.sortingOrder = InputBlockerSortingOrder + 1;

        GraphicRaycaster panelRaycaster = GetComponent<GraphicRaycaster>();
        if (panelRaycaster == null)
        {
            panelRaycaster = gameObject.AddComponent<GraphicRaycaster>();
        }

        RectTransform blockerRect = inputBlocker.transform as RectTransform;
        blockerRect.anchorMin = Vector2.zero;
        blockerRect.anchorMax = Vector2.one;
        blockerRect.offsetMin = Vector2.zero;
        blockerRect.offsetMax = Vector2.zero;

        Image blockerImage = inputBlocker.GetComponent<Image>();
        if (blockerImage == null)
        {
            blockerImage = inputBlocker.AddComponent<Image>();
        }
        blockerImage.color = new Color(0f, 0f, 0f, 0f);
        blockerImage.raycastTarget = true;

        Button blockerButton = inputBlocker.GetComponent<Button>();
        if (blockerButton == null)
        {
            blockerButton = inputBlocker.AddComponent<Button>();
        }
        blockerButton.transition = Selectable.Transition.None;
        blockerButton.onClick.RemoveListener(OnCloseBtnClick);
        blockerButton.onClick.AddListener(OnCloseBtnClick);
    }

    private void HideInputBlocker()
    {
        if (inputBlocker != null)
        {
            inputBlocker.SetActive(false);
        }
    }

    #endregion
    
    #region 按钮事件处理
    
    /// <summary>
    /// 保存按钮点击
    /// </summary>
    private void OnSaveBtnClick()
    {
        // 检查是否可以打开保存系统面板
        if (!GameStateManager.GetInstance().CanOpenPanel(GameState.SaveLoad))
        {
            Debug.LogWarning("[PausePanel] 当前状态不允许打开保存系统面板");
            return;
        }
        
        // 【Bug修复】截图已经在打开PausePanel之前完成，这里不需要再次截屏
        OpenSaveLoadPanelWithDarkFade(SaveLoadPanel.Mode.Save);
    }
    
    /// <summary>
    /// 加载按钮点击
    /// </summary>
    private void OnLoadBtnClick()
    {
        // 检查是否可以打开保存系统面板
        if (!GameStateManager.GetInstance().CanOpenPanel(GameState.SaveLoad))
        {
            Debug.LogWarning("[PausePanel] 当前状态不允许打开保存系统面板");
            return;
        }
        
        OpenSaveLoadPanelWithDarkFade(SaveLoadPanel.Mode.Load);
    }

    private void OpenSaveLoadPanelWithDarkFade(SaveLoadPanel.Mode mode)
    {
        UIManager.GetInstance().ShowPanelWithDarkFade<SaveLoadPanel>(
            "SaveLoadPanel",
            VNProjectConfig.Instance.UI_SaveLoadPath,
            E_UI_Layer.Top,
            panel =>
            {
                if (panel == null)
                    return;

                // 黑场完全覆盖后再隐藏暂停面板，避免过渡期间短暂露出游戏画面。
                StopSlideAnimation();
                ResetSlideAnimationState();
                HideInputBlocker();
                gameObject.SetActive(false);

                panel.SetOpenedFromMainMenu(false);
                panel.SetMode(mode);
            });
    }
    
    /// <summary>
    /// 设置按钮点击
    /// </summary>
    private void OnSettingsBtnClick()
    {
        // 检查是否可以打开设置面板
        if (!GameStateManager.GetInstance().CanOpenPanel(GameState.Settings))
        {
            Debug.LogWarning("[PausePanel] 当前状态不允许打开设置面板");
            return;
        }
        
        // 隐藏暂停面板（不关闭，保持Pause状态）
        StopSlideAnimation();
        ResetSlideAnimationState();
        HideInputBlocker();
        gameObject.SetActive(false);
        
        // 打开设置面板
        string path = VNProjectConfig.Instance != null ? VNProjectConfig.Instance.UI_SettingsPath : "VNPrefabs/UI/Settings";
        if (string.IsNullOrEmpty(path)) path = "VNPrefabs/UI/Settings";
        UIManager.GetInstance().ShowPanel<SettingsPanel>("SettingsPanel", path, E_UI_Layer.Top, null);
    }
    
    /// <summary>
    /// 退出按钮点击 - 返回主菜单场景
    /// </summary>
    private void OnExitBtnClick()
    {
        VNManager.GetInstance().AutoSaveGame();

        // 关闭暂停面板
        UIManager.GetInstance().HidePanel("PausePanel");
        
        // 恢复游戏状态
        if (GameStateManager.GetInstance() != null && 
            GameStateManager.GetInstance().CurrentState == GameState.Pause)
        {
            GameStateManager.GetInstance().RestoreState();
            PrimeTween.Tween.StopAll();
            VNAPI.ClearAllEffects();
            PoolManager.GetInstance().Clear();
        }
        

        // 加载主菜单场景
        SceneManager.LoadScene("VNMainMenu");
        
        Debug.Log("[PausePanel] 返回主菜单场景");
    }

    /// <summary>
    /// 关闭按钮点击
    /// </summary>
    private void OnCloseBtnClick()
    {
        StopSlideAnimation();
        ResetSlideAnimationState();
        HideInputBlocker();
        gameObject.SetActive(false);

        // 恢复游戏状态
        if (GameStateManager.GetInstance() != null &&
            GameStateManager.GetInstance().CurrentState == GameState.Pause)
        {
            GameStateManager.GetInstance().RestoreState();
        }
    }

        #endregion
    }

