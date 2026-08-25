using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.InputSystem;

/// <summary>
/// 历史记录面板 (优化版)
/// </summary>
public class HistoryPanel : BasePanel
{
    // UI组件
    [SerializeField] private Button closeButton;
    private ScrollRect historyScrollView;
    private Transform contentTransform;

    // 记录当前正在显示的 Item 列表，以便回收
    private List<GameObject> activeItems = new List<GameObject>();
    private int pendingItemCount;
    private Coroutine scrollToBottomCoroutine;

    // 预制体加载路径 (作为对象池的 Key)
    private string itemResPath;

    protected override void Awake()
    {
        base.Awake();

        //获取组件
        closeButton = GetControl<Button>("H_Close");
        historyScrollView = GetControl<ScrollRect>("H_Scroll View");

        if (closeButton != null)
            closeButton.onClick.AddListener(OnCloseButtonClick);
        else
            Debug.LogError("[HistoryPanel] 找不到 Close Button (H_Close)!");

        if (historyScrollView != null)
            contentTransform = historyScrollView.content;
        else
            Debug.LogError("[HistoryPanel] 找不到 ScrollRect (H_Scroll View)!");

        itemResPath = VNProjectConfig.Instance.UI_HistoryPath + "/HistoryItem";
        Debug.Log($"[HistoryPanel] 预制体加载路径: {itemResPath}");
    }

    protected override void OnEnable()
    {
        base.OnEnable();
        
        // 每次打开面板时设置状态
        GameStateManager.GetInstance().SetState(GameState.History);
        
        // 每次打开面板时刷新数据
        RefreshHistoryDisplay();
    }
    
    private void Update()
    {
        // 当HistoryPanel打开时，鼠标滚轮用于滚动ScrollView，而不是打开/关闭面板
        if (gameObject.activeSelf && historyScrollView != null)
        {
            // 检测鼠标滚轮输入（使用新版Input System）
            if (Mouse.current != null)
            {
                Vector2 scrollDelta = Mouse.current.scroll.ReadValue();
                
                // 如果滚轮有输入，手动滚动ScrollView
                if (scrollDelta.y != 0f)
                {
                    // 计算新的滚动位置
                    float scrollSpeed = 0.1f; // 滚动速度，可以根据需要调整
                    float currentPosition = historyScrollView.verticalNormalizedPosition;
                    float newPosition = currentPosition + (scrollDelta.y * scrollSpeed);
                    
                    // 限制在0-1范围内（0=底部，1=顶部）
                    newPosition = Mathf.Clamp01(newPosition);
                    
                    // 应用滚动
                    historyScrollView.verticalNormalizedPosition = newPosition;
                }
            }
        }
    }

    /// <summary>
    /// 刷新历史记录显示
    /// </summary>
    private void RefreshHistoryDisplay()
    {
        if (contentTransform == null) return;

        // 1. 回收旧对象到对象池
        for (int i = activeItems.Count - 1; i >= 0; i--)
        {
            GameObject item = activeItems[i];
            // 推回对象池，Key 为资源路径
            PoolManager.GetInstance().PushObj(itemResPath, item);
        }
        activeItems.Clear();

        // 2. 从 GlobalDataManager 获取所有历史数据
        List<HistoryEntry> logs = GlobalDataManager.GetInstance().GetHistoryLog();
        int logCount = logs != null ? logs.Count : 0;
        Debug.Log($"[HistoryPanel] 读取到 {logCount} 条历史记录");

        pendingItemCount = logCount;
        if (logCount == 0)
        {
            QueueScrollToBottom();
            return;
        }

        // 3. 生成新条目
        for (int i = 0; i < logCount; i++)
        {
            int itemIndex = i;
            HistoryEntry current = logs[i];
            HistoryEntry prev = (i > 0) ? logs[i - 1] : null;

            // 从对象池获取对象 (异步/同步)
            PoolManager.GetInstance().GetObj(itemResPath, (obj) =>
            {
                if (obj != null)
                {
                    // 初始化 Item，并保证异步回调完成顺序不会打乱历史记录顺序。
                    SetupHistoryItem(obj, current, prev);
                    obj.transform.SetSiblingIndex(itemIndex);
                    activeItems.Add(obj);
                }
                else
                {
                    Debug.LogError($"[HistoryPanel] 无法创建第 {itemIndex} 条历史记录");
                }

                pendingItemCount--;
                if (pendingItemCount == 0)
                    QueueScrollToBottom();
            });
        }
    }

    private void QueueScrollToBottom()
    {
        if (scrollToBottomCoroutine != null)
            StopCoroutine(scrollToBottomCoroutine);

        scrollToBottomCoroutine = StartCoroutine(ScrollToBottom());
    }

    /// <summary>
    /// 设置单个历史条目的显示内容
    /// </summary>
    private void SetupHistoryItem(GameObject itemObj, HistoryEntry entry, HistoryEntry prevEntry)
    {
        // 设置父物体
        itemObj.transform.SetParent(contentTransform);

        // 重置变换属性 (非常重要，对象池取出来的可能会乱)
        itemObj.transform.localScale = Vector3.one;
        itemObj.transform.localPosition = new Vector3(itemObj.transform.localPosition.x, itemObj.transform.localPosition.y, 0);
        itemObj.transform.localRotation = Quaternion.identity;

        // 查找子组件 (根据你的Prefab层级结构)
        Transform speakerBox = itemObj.transform.Find("H_SpeakerBox");
        // 注意：这里用 GetControl<TMP_Text> 可能找不到子物体的组件，建议直接 GetComponent
        TMP_Text speakerText = speakerBox.Find("H_SpeakerText").GetComponent<TMP_Text>();

        Transform contentTrans = itemObj.transform.Find("H_Content");
        TMP_Text dialogueText = contentTrans.Find("H_DialogueBox/H_Dialogue").GetComponent<TMP_Text>();
        Button replayButton = contentTrans.Find("H_Replay").GetComponent<Button>();

        //处理 Speaker 重复
        bool isSameSpeaker = (prevEntry != null && prevEntry.Speaker == entry.Speaker);

        if (isSameSpeaker)
        {
            speakerBox.gameObject.SetActive(false);
        }
        else
        {
            speakerBox.gameObject.SetActive(true);
            speakerText.text = entry.Speaker;
        }

        // 填充对话内容
        dialogueText.text = entry.Text;

        // 处理 Replay 按钮 
        // 先移除旧的监听器，防止复用时点击一次触发多次
        replayButton.onClick.RemoveAllListeners();

        replayButton.gameObject.SetActive(false);

        // 强制刷新布局
        LayoutRebuilder.ForceRebuildLayoutImmediate(itemObj.GetComponent<RectTransform>());
    }

    /// <summary>
    /// 滚动到底部
    /// </summary>
    private IEnumerator ScrollToBottom()
    {
        // 所有对象池回调完成后，再给布局系统一帧计算条目和 Content 的尺寸。
        yield return new WaitUntil(() => pendingItemCount <= 0);
        yield return null;
        Canvas.ForceUpdateCanvases();

        // 再次强制刷新 Content 的布局，确保 Content 高度正确
        if (contentTransform != null)
            LayoutRebuilder.ForceRebuildLayoutImmediate(contentTransform.GetComponent<RectTransform>());

        Canvas.ForceUpdateCanvases();
        yield return new WaitForEndOfFrame();

        // 滚动到底部 (0 = 底部, 1 = 顶部)
        if (historyScrollView != null)
        {
            historyScrollView.StopMovement();
            historyScrollView.verticalNormalizedPosition = 0f;
        }

        scrollToBottomCoroutine = null;
    }

    private void OnCloseButtonClick()
    {
        Debug.Log("我进来了");
        UIManager.GetInstance().HidePanel("HistoryPanel");
        GameStateManager.GetInstance().RestoreState();
    }

    private void OnDestroy()
    {
        // 清理资源
        activeItems.Clear();
        if (GameStateManager.GetInstance() != null && 
            GameStateManager.GetInstance().CurrentState == GameState.History)
        {
            GameStateManager.GetInstance().RestoreState();
            Debug.Log("[HistoryPanel] 面板被Destroy，已恢复游戏状态");
        }
    }
}

/// <summary>
/// 历史记录条目数据结构 (需要放在 HistoryPanel 外面或作为内部类)
/// 注意：使用公共字段而不是属性，以确保LitJson能正确序列化
/// </summary>
[System.Serializable]
public class HistoryEntry
{
    public string Speaker;
    public string Text;
    public string VoiceID;

    public HistoryEntry() { }

    public HistoryEntry(string speaker, string text, string voiceID = null)
    {
        Speaker = speaker;
        Text = text;
        VoiceID = voiceID;
    }
}
