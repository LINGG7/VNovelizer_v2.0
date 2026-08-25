using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using TMPro;

public class SaveLoadPanel : BasePanel
{
    // 面板类型
    public enum Mode { Save, Load }

    // UI组件
    private Button closeButton;
    private TextMeshProUGUI modeTitle;
    private ScrollRect saveSlotsScrollRect;
    private SaveSlotHorizontalScroller horizontalScroller;
    private Transform saveSlotsContainer;

    // 状态
    private Mode currentMode = Mode.Save;
    private int MAX_SAVE_SLOTS => SaveManager.GetInstance().GetMaxSaveSlots();

    // 存档槽位预制体
    private GameObject saveSlotPrefab;

    // 存档数据（延迟初始化，在Awake中根据MAX_SAVE_SLOTS创建）
    private SaveData[] saveDatas;

    //读取存档协程变量
    private bool _isLoadingGame = false;
    private bool _openedFromMainMenu;
    private bool _isClosing;

    public void SetOpenedFromMainMenu(bool openedFromMainMenu)
    {
        _openedFromMainMenu = openedFromMainMenu;
    }
    
    protected override void Awake()
    {
        base.Awake();

        // 获取组件
        closeButton = GetControl<Button>("CloseButton");
        modeTitle = GetControl<TextMeshProUGUI>("ModeTitle");
        Transform scrollViewTransform = transform.Find("SaveSlotsScrollView");
        if (scrollViewTransform != null)
        {
            saveSlotsScrollRect = scrollViewTransform.GetComponent<ScrollRect>();
            saveSlotsContainer = scrollViewTransform.Find("Viewport/SaveSlotsContainer");

            horizontalScroller = scrollViewTransform.GetComponent<SaveSlotHorizontalScroller>();
            if (horizontalScroller == null)
            {
                horizontalScroller = scrollViewTransform.gameObject.AddComponent<SaveSlotHorizontalScroller>();
            }

            Button previousButton = transform.Find("Pagination/PrevPage")?.GetComponent<Button>();
            Button nextButton = transform.Find("Pagination/NextPage")?.GetComponent<Button>();
            horizontalScroller.Configure(saveSlotsScrollRect, previousButton, nextButton);
        }

        // 兼容尚未升级的旧预制体，便于定位配置问题。
        if (saveSlotsContainer == null)
        {
            saveSlotsContainer = transform.Find("SaveSlotsContainer");
        }

        // 绑定事件
        closeButton.onClick.AddListener(OnCloseButtonClick);

        // 初始化存档数据数组（根据实际的最大槽位数）
        int maxSlots = SaveManager.GetInstance().GetMaxSaveSlots();
        saveDatas = new SaveData[maxSlots];
        Debug.Log($"[SaveLoadPanel] 初始化存档数据数组，最大槽位数: {maxSlots}");

        // 加载存档槽位预制体
        string loadPath = VNProjectConfig.Instance.UI_SaveLoadPath;
        saveSlotPrefab = ResourcesManager.GetInstance().Load<GameObject>(loadPath + "/SaveSlot");
    }

    protected override void OnEnable()
    {
        base.OnEnable();
        
        // 每次打开面板时设置状态（而不是在Awake中，因为ShowPanel可能复用已存在的面板）
        // 检查是否可以打开
        if (!GameStateManager.GetInstance().CanOpenPanel(GameState.SaveLoad))
        {
            Debug.LogWarning("[SaveLoadPanel] 当前状态不允许打开保存系统面板，已关闭");
            gameObject.SetActive(false);
            return;
        }
        
        // 如果当前状态是Pause，使用PushState（嵌套状态）
        // 否则使用SetState（普通状态切换）
        GameState currentState = GameStateManager.GetInstance().CurrentState;
        if (currentState == GameState.Pause)
        {
            GameStateManager.GetInstance().PushState(GameState.SaveLoad);
        }
        else
        {
            GameStateManager.GetInstance().SetState(GameState.SaveLoad);
        }
    }

    /// <summary>
    /// 设置面板模式
    /// </summary>
    /// <param name="mode">模式</param>
    public void SetMode(Mode mode)
    {
        currentMode = mode;
        modeTitle.text = currentMode == Mode.Save ? "保存存档" : "加载存档";

        // 加载存档数据
        LoadAllSaveDatas();

        // 更新列表
        UpdateSaveSlots();
        horizontalScroller?.ScrollToStart();
    }

    /// <summary>
    /// 加载所有存档数据
    /// </summary>
    private void LoadAllSaveDatas()
    {
        // 确保数组已初始化且长度正确
        int maxSlots = MAX_SAVE_SLOTS;
        if (saveDatas == null || saveDatas.Length != maxSlots)
        {
            saveDatas = new SaveData[maxSlots];
            Debug.LogWarning($"[SaveLoadPanel] 存档数据数组未初始化或长度不匹配，已重新初始化: {maxSlots}");
        }
        
        for (int i = 0; i < maxSlots; i++)
        {
            saveDatas[i] = SaveManager.GetInstance().LoadGame(i);
        }
    }

    /// <summary>
    /// 更新存档列表
    /// </summary>
    private void UpdateSaveSlots(bool scrollToEnd = false)
    {
        if (saveSlotsContainer == null)
        {
            Debug.LogError("[SaveLoadPanel] 找不到 SaveSlotsContainer");
            return;
        }

        // 清空现有槽位
        foreach (Transform child in saveSlotsContainer)
        {
            Destroy(child.gameObject);
        }

        // 自动存档固定显示，即使当前还没有自动存档数据。
        CreateSaveSlot(0, saveDatas[0]);

        // 手动存档只显示已有数据，不再创建空白占位槽。
        int nextEmptySlotIndex = -1;
        for (int i = 1; i < MAX_SAVE_SLOTS; i++)
        {
            if (saveDatas[i] != null)
            {
                CreateSaveSlot(i, saveDatas[i]);
            }
            else if (nextEmptySlotIndex < 0)
            {
                nextEmptySlotIndex = i;
            }
        }

        // 保存模式末尾仅保留一个“+”，并复用最小的空槽位编号。
        if (currentMode == Mode.Save && nextEmptySlotIndex >= 0)
        {
            GameObject addSlotObject = Instantiate(saveSlotPrefab, saveSlotsContainer);
            SaveSlot addSlot = addSlotObject.GetComponent<SaveSlot>();
            if (addSlot == null) addSlot = addSlotObject.AddComponent<SaveSlot>();
            addSlot.InitAddSlot(nextEmptySlotIndex, OnAddSlotClick);
        }

        horizontalScroller?.RefreshLayout(scrollToEnd);
    }

    private void CreateSaveSlot(int slotIndex, SaveData saveData)
    {
        GameObject slotObject = Instantiate(saveSlotPrefab, saveSlotsContainer);
        SaveSlot slot = slotObject.GetComponent<SaveSlot>();
        if (slot == null) slot = slotObject.AddComponent<SaveSlot>();
        slot.Init(slotIndex, saveData, currentMode, OnSaveSlotClick, OnDeleteSlotClick);
    }

    private void OnAddSlotClick(int slotIndex)
    {
        if (currentMode != Mode.Save ||
            slotIndex <= 0 ||
            slotIndex >= MAX_SAVE_SLOTS ||
            saveDatas[slotIndex] != null)
        {
            return;
        }

        VNManager.GetInstance().SaveGame(slotIndex);
        saveDatas[slotIndex] = SaveManager.GetInstance().LoadGame(slotIndex);
        UpdateSaveSlots(true);
    }

    /// <summary>
    /// 存档槽位点击事件
    /// </summary>
    // private void OnSaveSlotClick(int slotIndex)
    // {
    //     if (currentMode == Mode.Save)
    //     {
    //         // 保存游戏
    //         VNManager.GetInstance().SaveGame(slotIndex);
    //
    //         // 更新存档数据
    //         saveDatas[slotIndex] = SaveManager.GetInstance().LoadGame(slotIndex);
    //         UpdatePage();
    //     }
    //     else
    //     {
    //         // 加载游戏
    //         SaveData saveData = saveDatas[slotIndex];
    //         if (saveData != null)
    //         {
    //             // 【Bug修复】加载存档时，需要关闭所有面板并恢复Gameplay状态
    //             GameStateManager stateManager = GameStateManager.GetInstance();
    //             
    //             // 检查是否是从Pause状态打开的（栈中有状态）
    //             bool wasFromPause = !stateManager.IsStateStackEmpty();
    //             
    //             // 关闭SaveLoadPanel
    //             UIManager.GetInstance().HidePanel("SaveLoadPanel");
    //             
    //             // 恢复状态
    //             if (stateManager.CurrentState == GameState.SaveLoad)
    //             {
    //                 // 如果栈中有状态，说明是从Pause打开的
    //                 if (wasFromPause)
    //                 {
    //                     // PopState回到Pause
    //                     stateManager.PopState();
    //                     
    //                     // 关闭PausePanel
    //                     UIManager.GetInstance().HidePanel("PausePanel");
    //                     
    //                     // 直接设置为Gameplay（因为加载存档后应该进入游戏状态）
    //                     stateManager.SetState(GameState.Gameplay);
    //                 }
    //                 else
    //                 {
    //                     // 不是从Pause打开的，直接RestoreState
    //                     stateManager.RestoreState();
    //                 }
    //             }
    //             else
    //             {
    //                 stateManager.RestoreState();
    //             }
    //             
    //             // 确保状态是Gameplay（加载存档后应该进入游戏状态）
    //             if (stateManager.CurrentState != GameState.Gameplay && stateManager.CurrentState != GameState.AutoPlay)
    //             {
    //                 stateManager.SetState(GameState.Gameplay);
    //             }
    //             
    //             // 加载存档（这会处理场景切换等）
    //             VNManager.GetInstance().ContinueGame(saveData);
    //         }
    //         else
    //         {
    //             Debug.Log($"Slot {slotIndex + 1} 是空的，无法加载。");
    //         }
    //     }
    // }
    private void OnSaveSlotClick(int slotIndex)
    {
        if (currentMode != Mode.Load || _isLoadingGame)
            return;

        SaveData saveData = saveDatas[slotIndex];
        if (saveData != null)
        {
            StartCoroutine(LoadGameFlow(saveData));
        }
        else
        {
            Debug.Log($"Slot {slotIndex} 是空的，无法加载。");
        }
    }
    /// <summary>
    /// 加载存档协程
    /// </summary>
    /// <param name="saveData"></param>
    /// <returns></returns>
    private IEnumerator LoadGameFlow(SaveData saveData)
    {
        _isLoadingGame = true;

        // 记录当前是否是从 Pause 打开的
        GameStateManager stateManager = GameStateManager.GetInstance();
        bool wasFromPause = !stateManager.IsStateStackEmpty();

        if (TryGetMainMenuPanelForLoading(out MainMenuPanel mainMenuPanel))
        {
            mainMenuPanel.StartCoroutine(LoadGameFromMainMenuFlow(mainMenuPanel, saveData, stateManager, wasFromPause));
            yield break;
        }

        // 先显示常驻 loading
        UIManager.GetInstance().ShowPanel<LoadingProgressPanel>(
            "LoadingProgressPanel",
            VNProjectConfig.Instance.UI_LoadingPath,
            E_UI_Layer.System,
            null
        );

        // 强制刷新并等待一帧，让 loading 先真正显示出来
        Canvas.ForceUpdateCanvases();
        yield return null;
        yield return new WaitForEndOfFrame();

        // 恢复状态
        if (stateManager.CurrentState == GameState.SaveLoad)
        {
            if (wasFromPause)
            {
                // 先退回 Pause
                stateManager.PopState();
                // 然后明确切到 Gameplay
                stateManager.SetState(GameState.Gameplay);
            }
            else
            {
                stateManager.RestoreState();
            }
        }
        else
        {
            stateManager.RestoreState();
        }

        // 保证状态正确
        if (stateManager.CurrentState != GameState.Gameplay &&
            stateManager.CurrentState != GameState.AutoPlay)
        {
            stateManager.SetState(GameState.Gameplay);
        }

        //正式继续游戏（这里会走加载存档、场景恢复等逻辑）
        yield return WaitRemoteContentReadyBeforeGameplayLoad();
        Debug.Log("[SaveLoadPanel] CDN资源准备完成，开始进入读档加载流程");
        VNManager.GetInstance().ContinueGame(saveData);

        // ContinueGame 已经启动后再销毁当前面板，避免中途停止本协程
        UIManager.GetInstance().HidePanel("SaveLoadPanel");
        if (wasFromPause)
        {
            UIManager.GetInstance().HidePanel("PausePanel");
        }
    }

    private IEnumerator LoadGameFromMainMenuFlow(MainMenuPanel mainMenuPanel, SaveData saveData, GameStateManager stateManager, bool wasFromPause)
    {
        UIManager.GetInstance().HidePanel("SaveLoadPanel");
        RestoreStateAfterSaveLoadClose(stateManager, wasFromPause);

        yield return mainMenuPanel.PlayEnterLoadingTransition();

        UIManager.GetInstance().ShowPanel<LoadingProgressPanel>(
            "LoadingProgressPanel",
            VNProjectConfig.Instance.UI_LoadingPath,
            E_UI_Layer.System,
            null
        );

        Canvas.ForceUpdateCanvases();
        yield return null;
        yield return new WaitForEndOfFrame();

        EnsureGameplayState(stateManager);
        yield return WaitRemoteContentReadyBeforeGameplayLoad();
        Debug.Log("[SaveLoadPanel] CDN资源准备完成，开始进入主菜单读档加载流程");
        VNManager.GetInstance().ContinueGame(saveData);
        mainMenuPanel.HideMe();
    }

    private IEnumerator WaitRemoteContentReadyBeforeGameplayLoad()
    {
        RemoteContentPreloadManager preloadManager = RemoteContentPreloadManager.GetInstance();
        if (preloadManager != null && !preloadManager.IsReady)
        {
            yield return preloadManager.WaitUntilReady();
        }

        LoadingProgressManager.GetInstance().ClearAllTasks(false);
    }

    private bool TryGetMainMenuPanelForLoading(out MainMenuPanel mainMenuPanel)
    {
        mainMenuPanel = null;

        Scene activeScene = SceneManager.GetActiveScene();
        string sceneName = activeScene.name;
        if (sceneName != "VNMainMenu" && sceneName != "VNMainMenuScene")
            return false;

        MainMenuPanel[] mainMenuPanels = FindObjectsByType<MainMenuPanel>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        foreach (MainMenuPanel panel in mainMenuPanels)
        {
            if (panel != null && panel.gameObject.scene == activeScene)
            {
                mainMenuPanel = panel;
                return true;
            }
        }

        return false;
    }

    private void RestoreStateAfterSaveLoadClose(GameStateManager stateManager, bool wasFromPause)
    {
        if (stateManager.CurrentState == GameState.SaveLoad)
        {
            if (wasFromPause)
            {
                stateManager.PopState();
            }
            else
            {
                stateManager.RestoreState();
            }
        }
        else
        {
            stateManager.RestoreState();
        }
    }

    private void EnsureGameplayState(GameStateManager stateManager)
    {
        if (stateManager.CurrentState != GameState.Gameplay &&
            stateManager.CurrentState != GameState.AutoPlay)
        {
            stateManager.SetState(GameState.Gameplay);
        }
    }
    
    
    /// <summary>
    /// 存档删除点击事件
    /// </summary>
    private void OnDeleteSlotClick(int slotIndex)
    {
        // 弹出确认框
        string loadPath = VNProjectConfig.Instance.UI_ConfirmPath;
        string confirmPath = loadPath;

        UIManager.GetInstance().ShowPanel<ConfirmPanel>("ConfirmPanel", confirmPath, E_UI_Layer.System, (panel) =>
        {
            panel.Show(
                "Delete",
                $"确定需要删除「存档{slotIndex}」 吗?",
                () => {
                    // 确定删除
                    PerformDelete(slotIndex);
                },
                null // 取消无需操作
            );
        });
    }

    /// <summary>
    /// 执行删除操作
    /// </summary>
    private void PerformDelete(int slotIndex)
    {
        // 删除文件
        SaveManager.GetInstance().DeleteSave(slotIndex);
        // 清空内存数据
        saveDatas[slotIndex] = null;
        // 刷新界面
        UpdateSaveSlots();
    }

    // 按钮点击事件
    private void OnCloseButtonClick()
    {
        if (_isClosing)
        {
            return;
        }

        if (!_openedFromMainMenu)
        {
            CloseImmediately();
            return;
        }

        _isClosing = true;
        bool started = UIManager.GetInstance().HidePanelWithDarkFade(
            "SaveLoadPanel",
            RestoreStateAfterClose);
        if (!started)
        {
            _isClosing = false;
        }
    }

    private void CloseImmediately()
    {
        UIManager.GetInstance().HidePanel("SaveLoadPanel");
        RestoreStateAfterClose();
    }

    private void RestoreStateAfterClose()
    {
        // 如果当前状态是SaveLoad，检查是否是从Pause打开的（栈中有状态）
        // 如果是，使用PopState；否则使用RestoreState
        GameStateManager stateManager = GameStateManager.GetInstance();
        if (stateManager.CurrentState == GameState.SaveLoad)
        {
            // 尝试从栈中弹出状态（如果是从Pause打开的）
            stateManager.PopState();
        }
        else
        {
            stateManager.RestoreState();
        }
        
        // 如果恢复后的状态是Pause，重新显示PausePanel
        if (GameStateManager.GetInstance().CurrentState == GameState.Pause)
        {
            string path = VNProjectConfig.Instance != null ? VNProjectConfig.Instance.UI_PausePath : "VNPrefabs/UI/Pause";
            if (string.IsNullOrEmpty(path)) path = "VNPrefabs/UI/Pause";
            UIManager.GetInstance().ShowPanel<PausePanel>("PausePanel", path, E_UI_Layer.System, null);
        }
    }

    private void OnDestroy()
    {
        // 面板被Destroy时，如果当前状态是SaveLoad，需要恢复游戏状态
        if (GameStateManager.GetInstance() != null && 
            GameStateManager.GetInstance().CurrentState == GameState.SaveLoad)
        {
            // 尝试从栈中弹出状态（如果是从Pause打开的）
            GameStateManager.GetInstance().PopState();
            Debug.Log("[SaveLoadPanel] 面板被Destroy，已恢复游戏状态");
        }
    }

}
