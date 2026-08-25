using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Unity.VisualScripting;

/// <summary>
/// 加载进度面板
/// 用于显示游戏加载进度
/// </summary>
public class LoadingProgressPanel : BasePanel
{
    #region UI控件引用
    
    [Header("进度条组件")]
    [SerializeField] private Image progressBarFill;        // 进度条填充图片
    [SerializeField] private Slider progressSlider;       // 进度条滑块（可选，如果使用Slider）
    
    [Header("文本组件")]
    [SerializeField] private TMP_Text progressText;     // 进度百分比文本（如：50%）
    [SerializeField] private TMP_Text taskNameText;     // 当前任务名称文本
    [SerializeField] private TMP_Text detailText;       // 详细信息文本（可选）
    
    [Header("其他组件")]
    [SerializeField] private GameObject loadingIcon;         // 加载图标（可选，用于旋转动画）
    [SerializeField] private float iconRotationSpeed = 180f; // 图标旋转速度（度/秒）
    
    #endregion
    
    #region 私有变量
    
    private LoadingProgressManager progressManager;
    private bool isListening = false;
    private GameObject failureActionsRoot;
    private Button retryButton;
    private Button exitButton;
    
    #endregion
    
    #region 初始化
    
    protected override void Awake()
    {
        base.Awake();
        
        // 如果没有在Inspector中指定，尝试自动查找
        InitializeComponents();
        
        // 初始化进度管理器
        progressManager = LoadingProgressManager.GetInstance();
    }
    
    /// <summary>
    /// 初始化组件（如果Inspector中未指定，尝试自动查找）
    /// </summary>
    private void InitializeComponents()
    {
        Debug.Log("Initializing ");
        // 尝试查找进度条
        if (progressBarFill == null)
        {
            progressBarFill = transform.Find("ProgressBar/Fill")?.GetComponent<Image>();
        }
        if (progressSlider == null)
        {
            progressSlider = transform.Find("ProgressBar")?.GetComponent<Slider>();
            if (progressSlider != null && progressBarFill == null)
            {
                progressBarFill = progressSlider.fillRect?.GetComponent<Image>();
            }
        }
        
        // 尝试查找文本组件（使用多种方式）
        if (progressText == null)
        {
            //使用BasePanel的GetControl方法
            progressText = GetControl<TMP_Text>("ProgressText");
            //或者直接Find
            if (progressText == null)
            {
                Transform progressTextTransform = transform.Find("ProgressText");
                if (progressTextTransform != null)
                {
                    progressText = progressTextTransform.GetComponent<TMP_Text>();
                }
            }
        }
        
        if (taskNameText == null)
        {
            //使用BasePanel的GetControl方法
            taskNameText = GetControl<TMP_Text>("TaskNameText");
            //直接Find
            if (taskNameText == null)
            {
                Transform taskNameTextTransform = transform.Find("TaskNameText");
                if (taskNameTextTransform != null)
                {
                    taskNameText = taskNameTextTransform.GetComponent<TMP_Text>();
                }
            }
        }
        
        if (detailText == null)
        {
            //使用BasePanel的GetControl方法
            detailText = GetControl<TMP_Text>("DetailText");
            //直接Find
            if (detailText == null)
            {
                Transform detailTextTransform = transform.Find("DetailText");
                if (detailTextTransform != null)
                {
                    detailText = detailTextTransform.GetComponent<TMP_Text>();
                }
            }
        }
        
        // 尝试查找加载图标
        if (loadingIcon == null)
        {
            loadingIcon = transform.Find("LoadingIcon")?.gameObject;
        }
        
        // 调试日志：输出组件查找结果
        Debug.Log($"[LoadingProgressPanel] 组件初始化结果:\n" +
                  $"  progressBarFill: {(progressBarFill != null ? "✓" : "✗")}\n" +
                  $"  progressSlider: {(progressSlider != null ? "✓" : "✗")}\n" +
                  $"  progressText: {(progressText != null ? "✓" : "✗")}\n" +
                  $"  taskNameText: {(taskNameText != null ? "✓" : "✗")}\n" +
                  $"  detailText: {(detailText != null ? "✓" : "✗")}\n" +
                  $"  loadingIcon: {(loadingIcon != null ? "✓" : "✗")}");
    }
    
    public override void ShowMe()
    {
        base.ShowMe();
        CancelInvoke(nameof(HideMe));
        
        // 在显示时再次尝试初始化组件（因为面板可能是在Awake之后才被激活的）
        InitializeComponents();
        
        // 初始化显示（在监听前先设置初始状态）
        ClearFailureState();
        UpdateProgress(0f, "准备加载...", 0f);
        
        // 开始监听进度更新
        if (!isListening)
        {
            StartListening();
        }
        
        // 立即获取一次当前进度（如果有任务已注册）
        LoadingProgressManager progressMgr = LoadingProgressManager.GetInstance();
        float currentProgress = progressMgr.GetTotalProgress();
        if (currentProgress > 0f)
        {
            LoadingTask mainTask = progressMgr.GetCurrentMainTask();
            if (mainTask != null)
            {
                UpdateProgress(currentProgress, mainTask.TaskName, mainTask.Progress);
            }
        }
    }
    
    public override void HideMe()
    {
        CancelInvoke(nameof(HideMe));
        base.HideMe();
        
        // 停止监听
        if (isListening)
        {
            StopListening();
        }
    }
    
    #endregion
    
    #region 事件监听
    
    /// <summary>
    /// 开始监听进度更新
    /// </summary>
    private void StartListening()
    {
        if (isListening) return;
        
        //使用回调
        progressManager.OnProgressUpdated += OnProgressUpdated;
        progressManager.OnAllTasksCompleted += OnAllTasksCompleted;
        
        //或者使用EventCenter（项目现有的事件系统）
        EventCenter.GetInstance().AddEventListener<LoadingProgressInfo>("LoadingProgressUpdated", OnProgressUpdated);
        EventCenter.GetInstance().AddEventListener("LoadingAllTasksCompleted", OnAllTasksCompleted);
        
        isListening = true;
        Debug.Log("[LoadingProgressPanel] 开始监听加载进度");
    }
    
    /// <summary>
    /// 停止监听进度更新
    /// </summary>
    private void StopListening()
    {
        if (!isListening) return;
        
        // 移除回调
        progressManager.OnProgressUpdated -= OnProgressUpdated;
        progressManager.OnAllTasksCompleted -= OnAllTasksCompleted;
        
        // 移除EventCenter监听
        EventCenter.GetInstance().RemoveEventListener<LoadingProgressInfo>("LoadingProgressUpdated", OnProgressUpdated);
        EventCenter.GetInstance().RemoveEventListener("LoadingAllTasksCompleted", OnAllTasksCompleted);
        
        isListening = false;
        Debug.Log("[LoadingProgressPanel] 停止监听加载进度");
    }
    
    #endregion
    
    #region 进度更新处理
    
    /// <summary>
    /// 进度更新回调
    /// </summary>
    private void OnProgressUpdated(LoadingProgressInfo info)
    {
        Debug.Log($"[LoadingProgressPanel] 收到进度更新事件: 总进度={info.TotalProgress:F2}, 任务={info.CurrentTaskName}, 任务进度={info.CurrentTaskProgress:F2}");
        UpdateProgress(
            info.TotalProgress,
            info.CurrentTaskName,
            info.CurrentTaskProgress,
            info.ActiveTaskCount
        );
    }
    
    /// <summary>
    /// 所有任务完成回调
    /// </summary>
    private void OnAllTasksCompleted()
    {
        Debug.Log("[LoadingProgressPanel] 所有加载任务已完成");
        
        // 确保进度条显示100%
        UpdateProgress(1f, "加载完成", 1f);
        
        // 延迟一下在隐藏
        CancelInvoke(nameof(HideMe));
        Invoke(nameof(HideMe), 1f);
    }
    
    /// <summary>
    /// 更新进度显示
    /// </summary>
    /// <param name="totalProgress">总进度（0-1）</param>
    /// <param name="taskName">当前任务名称</param>
    /// <param name="taskProgress">当前任务进度（0-1）</param>
    /// <param name="activeTaskCount">活跃任务数量</param>
    private void UpdateProgress(float totalProgress, string taskName, float taskProgress, int activeTaskCount = 0)
    {
        Debug.Log($"[LoadingProgressPanel] UpdateProgress 被调用: 总进度={totalProgress:F2}, 任务={taskName}");
        
        // 如果组件为null，再次尝试初始化（防止异步加载导致的问题）
        if (progressText == null || taskNameText == null || detailText == null || 
            (progressBarFill == null && progressSlider == null))
        {
            Debug.LogWarning("[LoadingProgressPanel] 检测到组件为null，重新初始化...");
            InitializeComponents();
        }
        
        // 更新进度条
        if (progressBarFill != null)
        {
            progressBarFill.fillAmount = totalProgress;
        }
        else if (progressSlider != null)
        {
            progressSlider.value = totalProgress;
           
        }
        else
        {
            Debug.LogWarning("[LoadingProgressPanel] 进度条组件未找到！请检查UI预制体中的ProgressBar或ProgressBar/Fill");
        }
        
        // 更新进度文本
        if (progressText != null)
        {
            string progressTextValue = $"{totalProgress * 100:F1}%";
            progressText.text = progressTextValue;
           
        }
        else
        {
            Debug.LogWarning("[LoadingProgressPanel] ProgressText组件未找到！请检查UI预制体中的ProgressText GameObject");
        }
        
        // 更新任务名称文本
        if (taskNameText != null)
        {
            taskNameText.text = taskName;
            
        }
        else
        {
            Debug.LogWarning("[LoadingProgressPanel] TaskNameText组件未找到！请检查UI预制体中的TaskNameText GameObject");
        }
        
        // 更新详细信息文本（可选）
        if (detailText != null)
        {
            if (activeTaskCount > 0)
            {
                detailText.text = $"正在加载 ({activeTaskCount} 个任务进行中)";
            }
            else
            {
                detailText.text = "";
            }
           
        }
        // DetailText是可选的，不输出警告
    }
    
    #endregion
    
    public void ShowRemoteContentFailure(
        string message,
        System.Action onRetry,
        System.Action onExit,
        RemoteContentOperationResult? operationResult = null)
    {
        CancelInvoke(nameof(HideMe));
        InitializeComponents();
        EnsureFailureActions();

        string displayMessage = string.IsNullOrEmpty(message)
            ? "资源下载失败，请检查网络后重试"
            : message;

        UpdateProgress(progressManager != null ? progressManager.GetTotalProgress() : 0f, displayMessage, 0f);

        if (detailText != null)
        {
            detailText.text = BuildRemoteContentFailureDetail(operationResult);
        }

        if (loadingIcon != null)
        {
            loadingIcon.SetActive(false);
        }

        if (failureActionsRoot != null)
        {
            failureActionsRoot.SetActive(true);
        }

        if (retryButton != null)
        {
            retryButton.onClick.RemoveAllListeners();
            retryButton.onClick.AddListener(() => onRetry?.Invoke());
        }

        if (exitButton != null)
        {
            exitButton.onClick.RemoveAllListeners();
            exitButton.onClick.AddListener(() => onExit?.Invoke());
        }
    }

    private string BuildRemoteContentFailureDetail(RemoteContentOperationResult? operationResult)
    {
        if (!operationResult.HasValue)
        {
            return "请检查网络连接，然后重试下载。";
        }

        RemoteContentOperationResult result = operationResult.Value;
        string attempt = result.Attempt > 0 ? $" 已尝试 {result.Attempt} 次。" : "";
        switch (result.ErrorKind)
        {
            case RemoteContentErrorKind.Offline:
                return "设备当前离线，恢复网络后可直接重试。" + attempt;
            case RemoteContentErrorKind.Timeout:
                return "网络响应较慢，请切换网络或稍后重试。" + attempt;
            case RemoteContentErrorKind.ResourceMissing:
                return "资源版本可能尚未完整发布，请稍后重试或联系维护人员。";
            case RemoteContentErrorKind.IntegrityFailure:
                return "缓存资源校验失败，重试时只会重新获取受影响的资源包。" + attempt;
            case RemoteContentErrorKind.StorageFailure:
                return "请释放设备存储空间或清理资源缓存后再试。";
            case RemoteContentErrorKind.ServerUnavailable:
                return "CDN 暂时不可达，可切换网络后重试。" + attempt;
            default:
                return "请检查网络连接，然后重试下载。" + attempt;
        }
    }

    public void ClearFailureState()
    {
        if (failureActionsRoot != null)
        {
            failureActionsRoot.SetActive(false);
        }

        if (loadingIcon != null)
        {
            loadingIcon.SetActive(true);
        }
    }

    private void EnsureFailureActions()
    {
        if (failureActionsRoot != null)
        {
            return;
        }

        GameObject root = new GameObject("FailureActions", typeof(RectTransform), typeof(HorizontalLayoutGroup));
        root.layer = gameObject.layer;
        root.transform.SetParent(transform, false);

        RectTransform rootRect = root.GetComponent<RectTransform>();
        rootRect.anchorMin = new Vector2(0.5f, 0f);
        rootRect.anchorMax = new Vector2(0.5f, 0f);
        rootRect.pivot = new Vector2(0.5f, 0f);
        rootRect.anchoredPosition = new Vector2(0f, 72f);
        rootRect.sizeDelta = new Vector2(360f, 48f);

        HorizontalLayoutGroup layout = root.GetComponent<HorizontalLayoutGroup>();
        layout.childAlignment = TextAnchor.MiddleCenter;
        layout.spacing = 24f;
        layout.childForceExpandWidth = false;
        layout.childForceExpandHeight = true;

        retryButton = CreateFailureButton("RetryButton", "重试", root.transform);
        exitButton = CreateFailureButton("ExitButton", "退出", root.transform);
        failureActionsRoot = root;
        failureActionsRoot.SetActive(false);
    }

    private Button CreateFailureButton(string objectName, string label, Transform parent)
    {
        GameObject buttonObject = new GameObject(objectName, typeof(RectTransform), typeof(Image), typeof(Button), typeof(LayoutElement));
        buttonObject.layer = gameObject.layer;
        buttonObject.transform.SetParent(parent, false);

        RectTransform rect = buttonObject.GetComponent<RectTransform>();
        rect.sizeDelta = new Vector2(128f, 44f);

        Image image = buttonObject.GetComponent<Image>();
        image.color = new Color(0.08f, 0.10f, 0.12f, 0.92f);

        LayoutElement layout = buttonObject.GetComponent<LayoutElement>();
        layout.preferredWidth = 128f;
        layout.preferredHeight = 44f;

        GameObject labelObject = new GameObject("Label", typeof(RectTransform), typeof(TextMeshProUGUI));
        labelObject.layer = gameObject.layer;
        labelObject.transform.SetParent(buttonObject.transform, false);

        RectTransform labelRect = labelObject.GetComponent<RectTransform>();
        labelRect.anchorMin = Vector2.zero;
        labelRect.anchorMax = Vector2.one;
        labelRect.offsetMin = Vector2.zero;
        labelRect.offsetMax = Vector2.zero;

        TextMeshProUGUI text = labelObject.GetComponent<TextMeshProUGUI>();
        text.text = label;
        text.fontSize = 22f;
        text.alignment = TextAlignmentOptions.Center;
        text.color = Color.white;
        text.raycastTarget = false;

        return buttonObject.GetComponent<Button>();
    }

    #region 动画更新
    
    private void Update()
    {
        // 旋转加载图标
        if (loadingIcon != null && loadingIcon.activeSelf)
        {
            loadingIcon.transform.Rotate(0, 0, -iconRotationSpeed * Time.deltaTime);
        }
    }
    
    #endregion
    
    #region 清理
    
    private void OnDestroy()
    {
        // 确保在销毁时移除监听
        if (isListening)
        {
            StopListening();
        }
    }
    
    #endregion
}
