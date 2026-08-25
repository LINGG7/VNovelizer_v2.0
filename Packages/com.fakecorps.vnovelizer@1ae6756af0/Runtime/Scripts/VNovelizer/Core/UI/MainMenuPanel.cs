using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 主界面面板
/// </summary>
public class MainMenuPanel : BasePanel
{
    #region 私有变量
    private bool _isStartingGame = false;
    private bool _isNewGameConfirmationPending = false;
    private const int AutoSaveSlotIndex = 0;

    [Header("Enter Game Burn Transition")]
    [SerializeField, Min(2f)] private float enterLoadingBurnDuration = 4f;
    [SerializeField, Range(0.25f, 2f)] private float enterLoadingBurnDensity = 1f;
    [SerializeField, Range(0.75f, 1.5f)] private float enterLoadingBurnMaxRadius = 1.12f;
    [SerializeField] private Vector2 enterLoadingBurnCenter = new Vector2(0.5f, 0.5f);
    [SerializeField, Range(0f, 0.2f)] private float enterLoadingBurnNoiseStrength = 0.075f;
    [SerializeField, Range(0.08f, 0.45f)] private float enterLoadingBurnFlameHeight = 0.29f;
    [SerializeField, Range(0f, 0.35f)] private float enterLoadingBurnFlameCurlStrength = 0.16f;
    [SerializeField, Range(0.5f, 1.5f)] private float enterLoadingBurnTongueActivity = 1f;
    [SerializeField, Range(0.5f, 2f)] private float enterLoadingBurnTongueSpeed = 1.2f;
    [SerializeField, Min(1.3f)] private float enterLoadingBurnSpreadFinishTime = 3f;
    [SerializeField, Range(0.5f, 5f)] private float enterLoadingBurnEmberAmountMultiplier = 5f;

    #endregion
    
    #region UI控件引用

    [SerializeField] private Button newGameBtn;
    [SerializeField] private Button loadGameBtn;
    [SerializeField] private Button galleryBtn;
    [SerializeField] private Button settingsBtn;
    [SerializeField] private Button quitBtn;
    private CanvasGroup mainMenuCanvasGroup;
    private MainMenuBurnTransition burnTransition;
    
    #endregion
    
    #region 初始化
    
    protected override void Awake()
    {
        base.Awake();
        
        UIManager.GetInstance().Init();
        
        // 初始化控件
        InitializeControls();
        
        // 绑定事件
        BindEvents();
    }
    
    private void ShowRemoteContentFailure(string scriptName, string lineID, RemoteContentPreloadManager preloadManager)
    {
        LoadingProgressPanel loadingPanel = UIManager.GetInstance().GetPanel<LoadingProgressPanel>("LoadingProgressPanel");
        if (loadingPanel != null)
        {
            loadingPanel.ShowRemoteContentFailure(
                preloadManager.LastError,
                () => RetryRemoteContentDownload(scriptName, lineID),
                ExitRemoteContentDownload,
                preloadManager.LastOperationResult);
        }

        Debug.LogWarning($"[MainMenuPanel] CDN resource preload failed: {preloadManager.LastError}");
    }

    private void RetryRemoteContentDownload(string scriptName, string lineID)
    {
        LoadingProgressPanel loadingPanel = UIManager.GetInstance().GetPanel<LoadingProgressPanel>("LoadingProgressPanel");
        if (loadingPanel != null)
        {
            loadingPanel.ClearFailureState();
        }

        RemoteContentPreloadManager preloadManager = RemoteContentPreloadManager.GetInstance();
        preloadManager.RetryRequiredContent();
        StartCoroutine(WaitForRemoteContentRetry(scriptName, lineID, preloadManager));
    }

    private IEnumerator WaitForRemoteContentRetry(string scriptName, string lineID, RemoteContentPreloadManager preloadManager)
    {
        yield return preloadManager.WaitUntilReady();

        if (!preloadManager.IsReady)
        {
            ShowRemoteContentFailure(scriptName, lineID, preloadManager);
            yield break;
        }

        LoadingProgressManager.GetInstance().ClearAllTasks(false);
        VNManager.GetInstance().StartGame(scriptName, lineID);
    }

    private void ExitRemoteContentDownload()
    {
        LoadingProgressManager.GetInstance().ClearAllTasks(false);
        UIManager.GetInstance().HidePanel("LoadingProgressPanel");
        gameObject.SetActive(true);
        ResetMainMenuFade();
        RefreshSaveButtonState();
        SetMenuInteractable(true);
        _isStartingGame = false;
    }

    /// <summary>
    /// 初始化控件
    /// </summary>
    private void InitializeControls()
    {
        newGameBtn = GetControl<Button>("NewGameBtn");
        loadGameBtn = GetControl<Button>("LoadGameBtn");
        galleryBtn = GetControl<Button>("GalleryBtn");
        settingsBtn = GetControl<Button>("SettingsBtn");
        quitBtn = GetControl<Button>("QuitBtn");
        EnsureCanvasGroup();
        
        // 检查关键控件是否存在
        if (newGameBtn == null)
            Debug.LogError("[MainMenuPanel] 找不到 NewGameBtn 按钮！");
        if (loadGameBtn == null)
            Debug.LogError("[MainMenuPanel] 找不到 LoadGameBtn 按钮！");
        if (galleryBtn == null)
            Debug.LogWarning("[MainMenuPanel] 找不到 GalleryBtn 按钮（可选）");
        if (settingsBtn == null)
            Debug.LogError("[MainMenuPanel] 找不到 SettingsBtn 按钮！");
        if (quitBtn == null)
            Debug.LogError("[MainMenuPanel] 找不到 QuitBtn 按钮！");
    }
    
    /// <summary>
    /// 绑定事件
    /// </summary>
    private void BindEvents()
    {
        if (newGameBtn != null)
            newGameBtn.onClick.AddListener(OnNewGameBtnClick);
        if (loadGameBtn != null)
            loadGameBtn.onClick.AddListener(OnLoadGameBtnClick);
        if (galleryBtn != null)
            galleryBtn.onClick.AddListener(OnGalleryBtnClick);
        if (settingsBtn != null)
            settingsBtn.onClick.AddListener(OnSettingsBtnClick);
        if (quitBtn != null)
            quitBtn.onClick.AddListener(OnQuitBtnClick);
    }
    
    /// <summary>
    /// 解绑事件（用于清理）
    /// </summary>
    private void UnbindEvents()
    {
        if (newGameBtn != null)
            newGameBtn.onClick.RemoveListener(OnNewGameBtnClick);
        if (loadGameBtn != null)
            loadGameBtn.onClick.RemoveListener(OnLoadGameBtnClick);
        if (galleryBtn != null)
            galleryBtn.onClick.RemoveListener(OnGalleryBtnClick);
        if (settingsBtn != null)
            settingsBtn.onClick.RemoveListener(OnSettingsBtnClick);
        if (quitBtn != null)
            quitBtn.onClick.RemoveListener(OnQuitBtnClick);
    }
    
    #endregion
    
    #region Unity生命周期
    
    protected override void OnEnable()
    {
        base.OnEnable();
        
        // 确保UIManager已初始化（主菜单场景可能没有初始化UIManager）
        if (UIManager.GetInstance() != null && UIManager.GetInstance().canvas == null)
        {
            UIManager.GetInstance().Init();
        }
        
        // 每次打开面板时刷新存档状态
        ResetMainMenuFade();
        RefreshSaveButtonState();
        StartRemoteContentPreload();
    }
    
    public override void ShowMe()
    {
        GameStateManager.GetInstance().ResetToGameplay();
        gameObject.SetActive(true);
        ResetMainMenuFade();
        RefreshSaveButtonState();
        StartRemoteContentPreload();
    }
    
    public override void HideMe()
    {
        gameObject.SetActive(false);
    }
    
    private void OnDestroy()
    {
        // 清理事件监听
        UnbindEvents();
    }

    private void StartRemoteContentPreload()
    {
        RemoteContentPreloadManager.GetInstance().StartMainMenuPreload();
    }
    
    #endregion
    
    #region 按钮事件处理
    
    /// <summary>
    /// 新游戏按钮点击
    /// </summary>
    // private void OnNewGameBtnClick() //你没协程啊？
    // {
    //     if (VNManager.GetInstance() == null)
    //     {
    //         Debug.LogError("[MainMenuPanel] VNManager 未初始化！");
    //         return;
    //     }
    //     
    //     // 从配置中读取默认剧本名称和行ID
    //     string defaultScriptName = "Test101"; // 默认值
    //     string defaultLineID = ""; // 默认从开头开始
    //     
    //     if (VNProjectConfig.Instance != null)
    //     {
    //         defaultScriptName = string.IsNullOrEmpty(VNProjectConfig.Instance.DefaultScriptName) 
    //             ? "Test101" 
    //             : VNProjectConfig.Instance.DefaultScriptName;
    //         defaultLineID = VNProjectConfig.Instance.DefaultLineID ?? "";
    //     }
    //     else
    //     {
    //         Debug.LogWarning("[MainMenuPanel] VNProjectConfig 未找到，使用默认值");
    //     }
    //     
    //     // 隐藏主菜单（VNManager.StartGame() 会自动显示游戏面板）
    //     UIManager.GetInstance().HidePanel("MainMenuPanel");
    //     
    //     // 启动游戏（VNManager.StartGame() 内部会调用 ShowPanel<VNGameplayPanel>）
    //     VNManager.GetInstance().StartGame(defaultScriptName, defaultLineID);
    //     
    //     Debug.Log($"[MainMenuPanel] 开始新游戏: 剧本={defaultScriptName}, 行ID={defaultLineID}");
    // }
    
    
    /// <summary>
    /// 游戏按钮点击
    /// </summary>
    private void OnNewGameBtnClick()
    {
        if (_isStartingGame || _isNewGameConfirmationPending)
            return;

        if (VNManager.GetInstance() == null)
        {
            Debug.LogError("[MainMenuPanel] VNManager 未初始化！");
            return;
        }

        // 从配置中读取默认剧本名称和行ID
        string defaultScriptName = "Test101";
        string defaultLineID = "";

        if (VNProjectConfig.Instance != null)
        {
            defaultScriptName = string.IsNullOrEmpty(VNProjectConfig.Instance.DefaultScriptName)
                ? "Test101"
                : VNProjectConfig.Instance.DefaultScriptName;
            defaultLineID = VNProjectConfig.Instance.DefaultLineID ?? "";
        }
        else
        {
            Debug.LogWarning("[MainMenuPanel] VNProjectConfig 未找到，使用默认值");
        }

        SaveManager saveManager = SaveManager.GetInstance();
        if (saveManager != null && saveManager.IsSaveExists(AutoSaveSlotIndex))
        {
            ShowNewGameOverwriteConfirmation(defaultScriptName, defaultLineID);
            return;
        }

        StartCoroutine(StartNewGameFlow(defaultScriptName, defaultLineID));
    }

    private void ShowNewGameOverwriteConfirmation(string scriptName, string lineID)
    {
        _isNewGameConfirmationPending = true;
        string confirmPath = VNProjectConfig.Instance != null
            ? VNProjectConfig.Instance.UI_ConfirmPath
            : "VNovelizerRes/VNPrefabs/UI/Confirm";

        UIManager.GetInstance().ShowPanel<ConfirmPanel>(
            "ConfirmPanel",
            confirmPath,
            E_UI_Layer.System,
            panel =>
            {
                if (panel == null)
                {
                    _isNewGameConfirmationPending = false;
                    SetMenuInteractable(true);
                    RefreshSaveButtonState();
                    Debug.LogError("[MainMenuPanel] 无法显示自动存档覆盖确认弹窗！");
                    return;
                }

                panel.Show(
                    "开始新游戏",
                    "会覆盖当前自动存档，是否继续？",
                    () =>
                    {
                        _isNewGameConfirmationPending = false;
                        if (!_isStartingGame)
                            StartCoroutine(StartNewGameFlow(scriptName, lineID));
                    },
                    () =>
                    {
                        _isNewGameConfirmationPending = false;
                        SetMenuInteractable(true);
                        RefreshSaveButtonState();
                    },
                    "继续",
                    "取消");
            });
    }
    /// <summary>
    /// 协程方法，按照顺序执行事件流，私有方法
    /// </summary>
    /// <param name="scriptName"></param>
    /// <param name="lineID"></param>
    /// <returns></returns>
    private IEnumerator StartNewGameFlow(string scriptName, string lineID)
    {
        _isStartingGame = true;

        // 先禁用主菜单交互，防止重复点击
        SetMenuInteractable(false);

        Debug.Log($"[MainMenuPanel] 开始新游戏流程: 剧本={scriptName}, 行ID={lineID}");

        // 1. 先显示常驻加载界面
        yield return PlayEnterLoadingTransition();

        UIManager.GetInstance().ShowPanel<LoadingProgressPanel>(
            "LoadingProgressPanel",
            VNProjectConfig.Instance.UI_LoadingPath,
            E_UI_Layer.System,
            null
        );

        // 2. 强制刷新 UI，并至少等一帧，让 loading 真正显示到屏幕上
        Canvas.ForceUpdateCanvases();
        yield return null;
        yield return new WaitForEndOfFrame();

        // 3. 此时再隐藏主菜单自己，但不要用 HidePanel 销毁
        SetMainMenuVisibleDuringLoading(false);

        // 4. 再开始游戏逻辑
        RemoteContentPreloadManager preloadManager = RemoteContentPreloadManager.GetInstance();
        if (preloadManager != null && !preloadManager.IsReady)
        {
            yield return preloadManager.WaitUntilReady();
        }

        if (preloadManager != null && !preloadManager.IsReady)
        {
            ShowRemoteContentFailure(scriptName, lineID, preloadManager);
            yield break;
        }

        LoadingProgressManager.GetInstance().ClearAllTasks(false);

        Debug.Log("[MainMenuPanel] CDN资源准备完成，开始进入游戏加载流程");
        VNManager.GetInstance().StartGame(scriptName, lineID);
        HideMe();
    }

    private void SetMainMenuVisibleDuringLoading(bool visible)
    {
        EnsureCanvasGroup();
        mainMenuCanvasGroup.alpha = visible ? 1f : 0f;
        mainMenuCanvasGroup.interactable = visible;
        mainMenuCanvasGroup.blocksRaycasts = visible;
    }
    /// <summary>
    /// 关闭目录的点击功能，私有方法
    /// </summary>
    /// <param name="interactable"></param>
    
    private void SetMenuInteractable(bool interactable)
    {
        if (newGameBtn != null) newGameBtn.interactable = interactable;
        if (loadGameBtn != null) loadGameBtn.interactable = interactable;
        if (galleryBtn != null) galleryBtn.interactable = interactable;
        if (settingsBtn != null) settingsBtn.interactable = interactable;
        if (quitBtn != null) quitBtn.interactable = interactable;
    }

    public IEnumerator PlayEnterLoadingTransition()
    {
        SetMenuInteractable(false);

        EnsureCanvasGroup();
        mainMenuCanvasGroup.alpha = 1f;
        mainMenuCanvasGroup.interactable = false;
        mainMenuCanvasGroup.blocksRaycasts = false;

        EnsureBurnTransition();
        burnTransition.Configure(
            enterLoadingBurnDuration,
            enterLoadingBurnDensity,
            enterLoadingBurnMaxRadius,
            enterLoadingBurnCenter,
            enterLoadingBurnNoiseStrength,
            enterLoadingBurnFlameHeight,
            enterLoadingBurnFlameCurlStrength,
            enterLoadingBurnSpreadFinishTime,
            enterLoadingBurnEmberAmountMultiplier,
            enterLoadingBurnTongueActivity,
            enterLoadingBurnTongueSpeed);
        yield return burnTransition.Play(transform as RectTransform);
    }

    private void EnsureCanvasGroup()
    {
        if (mainMenuCanvasGroup != null)
            return;

        mainMenuCanvasGroup = GetComponent<CanvasGroup>();
        if (mainMenuCanvasGroup == null)
            mainMenuCanvasGroup = gameObject.AddComponent<CanvasGroup>();
    }

    private void EnsureBurnTransition()
    {
        if (burnTransition != null)
            return;

        burnTransition = GetComponent<MainMenuBurnTransition>();
        if (burnTransition == null)
            burnTransition = gameObject.AddComponent<MainMenuBurnTransition>();
    }

    private void ResetMainMenuFade()
    {
        EnsureCanvasGroup();
        mainMenuCanvasGroup.alpha = 1f;
        mainMenuCanvasGroup.interactable = true;
        mainMenuCanvasGroup.blocksRaycasts = true;
        if (burnTransition != null)
            burnTransition.ResetTransition();
    }
    
    /// <summary>
    /// 加载游戏按钮点击
    /// </summary>
    private void OnLoadGameBtnClick()
    {
        if (!loadGameBtn.interactable)
        {
            Debug.LogWarning("[MainMenuPanel] 没有可用的存档");
            return;
        }
        
        // 显示存档加载面板
        UIManager.GetInstance().ShowPanelWithDarkFade<SaveLoadPanel>(
            "SaveLoadPanel", 
            VNProjectConfig.Instance.UI_SaveLoadPath, 
            E_UI_Layer.Middle, 
            (panel) =>
            {
                if (panel != null)
                {
                    panel.SetOpenedFromMainMenu(true);
                    panel.SetMode(SaveLoadPanel.Mode.Load);
                }
            }
        );
    }
    
    /// <summary>
    /// 画廊按钮点击
    /// </summary>
    private void OnGalleryBtnClick()
    {
        // 显示画廊面板
        // 注意：GalleryPanel 的路径可能需要从配置中读取
        string galleryPath = VNProjectConfig.Instance.UI_GalleryPath; // 临时使用Settings路径
        UIManager.GetInstance().ShowPanelWithDarkFade<GalleryPanel>(
            "GalleryPanel", 
            galleryPath, 
            E_UI_Layer.Middle, 
            panel =>
            {
                if (panel != null)
                {
                    panel.SetOpenedFromMainMenu(true);
                }
            }
        );
    }
    
    /// <summary>
    /// 设置按钮点击
    /// </summary>
    private void OnSettingsBtnClick()
    {
        // 显示设置面板
        UIManager.GetInstance().ShowPanel<SettingsPanel>(
            "SettingsPanel", 
            VNProjectConfig.Instance.UI_SettingsPath, 
            E_UI_Layer.Middle, 
            null
        );
    }
    
    /// <summary>
    /// 退出按钮点击
    /// </summary>
    private void OnQuitBtnClick()
    {
        // 显示确认对话框
        string confirmPath = VNProjectConfig.Instance.UI_ConfirmPath;
        UIManager.GetInstance().ShowPanel<ConfirmPanel>(
            "ConfirmPanel", 
            confirmPath, 
            E_UI_Layer.System, 
            (panel) =>
            {
                if (panel != null)
                {
                    panel.Show(
                        "退出游戏",
                        "确定要退出游戏吗？",
                        () =>
                        {
                            // 确定退出
                            Debug.Log("[MainMenuPanel] 退出游戏");
                            #if UNITY_WEBGL && !UNITY_EDITOR
                            MuteUnityAudioForWebGLQuit();
                            #else
                            Application.Quit();
                            
                            // 在编辑器中，Application.Quit() 不会生效，使用这个替代
                            #if UNITY_EDITOR
                            UnityEditor.EditorApplication.isPlaying = false;
                            #endif
                            #endif
                        },
                        null // 取消无需操作
                    );
                }
            }
        );
    }

    private void MuteUnityAudioForWebGLQuit()
    {
        AudioListener.pause = true;
        AudioListener.volume = 0f;
        Debug.Log("[MainMenuPanel] WebGL quit requested: Unity audio muted.");
    }
    
    #endregion
    
    #region 辅助方法
    
    /// <summary>
    /// 刷新存档按钮状态
    /// </summary>
    private void RefreshSaveButtonState()
    {
        if (loadGameBtn == null) return;
        
        // 检查是否存在存档
        bool hasSave = false;
        if (SaveManager.GetInstance() != null)
        {
            hasSave = SaveManager.GetInstance().HasAnySave();
        }
        
        loadGameBtn.interactable = hasSave;
        
        if (hasSave)
        {
            Debug.Log("[MainMenuPanel] 检测到存档，加载按钮已启用");
        }
        else
        {
            Debug.Log("[MainMenuPanel] 未检测到存档，加载按钮已禁用");
        }
    }
    
    #endregion
}
