using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.IO;
using UnityEngine.Events; // 引入以使用 UnityAction

/// <summary>
/// 存档槽位组件 (挂载在 SaveSlot 预制体上)
/// </summary>
public class SaveSlot : MonoBehaviour
{
    private const int AutoSaveSlotIndex = 0;

    // UI组件
    [SerializeField] private Image screenshotImage;
    [SerializeField] private Button deleteButton; // 删除按钮
    [SerializeField] private TextMeshProUGUI slotText;
    [SerializeField] private TextMeshProUGUI dateText;
    [SerializeField] private Button slotButton; // 整个 Slot 的按钮
    [SerializeField] private Sprite addSlotSprite;

    // 运行时数据
    private int slotIndex;
    private SaveData saveData;
    private SaveLoadPanel.Mode mode;
    private UnityAction<int> onClickCallback;
    private UnityAction<int> onDeleteCallback;
    private string pendingScreenshotPath;
    private Coroutine loadScreenshotCoroutine;
    private bool isAddSlot;

    // 自我初始化
    private void Awake()
    {
        if (slotButton == null) slotButton = GetComponent<Button>();

        // 自动查找子组件（防止 Inspector 漏拖）
        if (slotText == null) slotText = transform.Find("SlotText")?.GetComponent<TextMeshProUGUI>();
        if (dateText == null) dateText = transform.Find("DateText")?.GetComponent<TextMeshProUGUI>();
        if (screenshotImage == null) screenshotImage = transform.Find("Screenshot")?.GetComponent<Image>();
        if (deleteButton == null) deleteButton = transform.Find("DeleteButton")?.GetComponent<Button>();
    }

    private void OnEnable()
    {
        if (!string.IsNullOrEmpty(pendingScreenshotPath))
        {
            StartScreenshotLoad(pendingScreenshotPath);
        }
    }

    private void OnDisable()
    {
        if (loadScreenshotCoroutine != null)
        {
            StopCoroutine(loadScreenshotCoroutine);
            loadScreenshotCoroutine = null;
        }
    }

    /// <summary>
    /// 初始化存档槽位
    /// </summary>
    public void Init(int index, SaveData data, SaveLoadPanel.Mode mode,
                     UnityAction<int> onClick, UnityAction<int> onDelete)
    {
        isAddSlot = false;
        this.slotIndex = index;
        this.saveData = data;
        this.mode = mode;
        this.onClickCallback = onClick;
        this.onDeleteCallback = onDelete;

        // 绑定点击事件
        if (slotButton != null)
        {
            slotButton.onClick.RemoveAllListeners();
            slotButton.onClick.AddListener(OnSlotClick);
        }

        // 绑定删除事件
        if (deleteButton != null)
        {
            deleteButton.onClick.RemoveAllListeners();
            deleteButton.onClick.AddListener(OnDeleteClick);
        }

        // 更新显示内容
        UpdateDisplay();
    }

    /// <summary>
    /// 初始化新增存档槽位
    /// </summary>
    public void InitAddSlot(int index, UnityAction<int> onAdd)
    {
        isAddSlot = true;
        slotIndex = index;
        saveData = null;
        mode = SaveLoadPanel.Mode.Save;
        onClickCallback = onAdd;
        onDeleteCallback = null;

        if (slotButton != null)
        {
            slotButton.onClick.RemoveAllListeners();
            slotButton.onClick.AddListener(OnSlotClick);
            slotButton.interactable = true;
        }

        if (deleteButton != null)
        {
            deleteButton.onClick.RemoveAllListeners();
            deleteButton.gameObject.SetActive(false);
        }

        if (screenshotImage != null)
        {
            screenshotImage.gameObject.SetActive(true);
            screenshotImage.sprite = addSlotSprite;
            screenshotImage.color = Color.white;
            screenshotImage.preserveAspect = true;
        }

        if (dateText != null)
        {
            dateText.gameObject.SetActive(false);
        }

        if (slotText != null)
        {
            Transform slotLabelContainer = slotText.transform.parent;
            if (addSlotSprite != null)
            {
                slotLabelContainer.gameObject.SetActive(false);
            }
            else
            {
                // 资源引用缺失时保留文本回退，避免新增槽位完全不可识别。
                slotText.text = "+";
                slotText.alignment = TextAlignmentOptions.Center;
                slotText.fontSize = 72;
            }
        }
    }

    /// <summary>
    /// 更新显示
    /// </summary>
    private void UpdateDisplay()
    {
        bool isAutoSaveSlot = slotIndex == AutoSaveSlotIndex;

        if (slotText != null)
        {
            if(isAutoSaveSlot)
            {
                slotText.text = $"自动";
            }
            else
            {
                slotText.text = $"{slotIndex}";
            }
        }

        if (saveData != null)
        {
            // --- 有存档数据 ---
            if (dateText != null) dateText.text = saveData.SaveTime;

            string chapterName = Path.GetFileNameWithoutExtension(saveData.ScriptFileName);

            // 加载截图
            if (screenshotImage != null)
            {
                if (!string.IsNullOrEmpty(saveData.ScreenshotPath))
                {
                    StartScreenshotLoad(saveData.ScreenshotPath);
                }
                else
                {
                    SetDefaultScreenshot();
                }
            }

            // 保存模式只允许通过“+”创建新存档；已有存档仅在读取模式可点击。
            if (slotButton != null) slotButton.interactable = mode == SaveLoadPanel.Mode.Load;

            // 显示删除按钮
            if (deleteButton != null) deleteButton.gameObject.SetActive(!isAutoSaveSlot);
        }
        else
        {
            // --- 空槽位 ---
            if (dateText != null) dateText.text = "[空]";
            if (screenshotImage != null) SetDefaultScreenshot();

            // 普通空槽位只用于展示（当前仅可能是尚无数据的自动存档）。
            if (slotButton != null)
                slotButton.interactable = false;

            // 隐藏删除按钮
            if (deleteButton != null) deleteButton.gameObject.SetActive(false);
        }
    }

    private void StartScreenshotLoad(string path)
    {
        pendingScreenshotPath = path;

        if (!isActiveAndEnabled)
            return;

        pendingScreenshotPath = null;

        if (loadScreenshotCoroutine != null)
        {
            StopCoroutine(loadScreenshotCoroutine);
        }

        loadScreenshotCoroutine = StartCoroutine(LoadScreenshot(path));
    }

    private void SetDefaultScreenshot()
    {
        if (screenshotImage != null)
        {
            screenshotImage.color = Color.gray;
            screenshotImage.sprite = null;
        }
    }

    /// <summary>
    /// 异步加载本地截图
    /// </summary>
    private IEnumerator LoadScreenshot(string path)
    {
        if (string.IsNullOrEmpty(path))
        {
            SetDefaultScreenshot();
            loadScreenshotCoroutine = null;
            yield break;
        }

        yield return null;

        Texture2D texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
        try
        {
            Texture2D savedTexture = SaveManager.GetInstance().GetScreenshot(slotIndex, saveData);
            if (savedTexture == null)
            {
                Object.Destroy(texture);
                SetDefaultScreenshot();
                loadScreenshotCoroutine = null;
                yield break;
            }

            Object.Destroy(texture);
            texture = savedTexture;
        }
        catch (System.Exception e)
        {
            Object.Destroy(texture);
            Debug.LogWarning($"[SaveSlot] Screenshot load failed: {e.Message}");
            SetDefaultScreenshot();
            loadScreenshotCoroutine = null;
            yield break;
        }

        if (screenshotImage != null)
        {
            Sprite oldSprite = screenshotImage.sprite;
            Texture oldTexture = oldSprite != null ? oldSprite.texture : null;

            Sprite sprite = Sprite.Create(texture, new Rect(0, 0, texture.width, texture.height), new Vector2(0.5f, 0.5f));
            screenshotImage.sprite = sprite;
            screenshotImage.color = Color.white;

            if (oldSprite != null)
            {
                Object.Destroy(oldSprite);
            }

            if (oldTexture != null)
            {
                Object.Destroy(oldTexture);
            }
        }
        else
        {
            Object.Destroy(texture);
        }

        loadScreenshotCoroutine = null;
    }

    private void OnSlotClick()
    {
        if (!isAddSlot && mode == SaveLoadPanel.Mode.Save)
            return;

        onClickCallback?.Invoke(slotIndex);
    }

    private void OnDeleteClick()
    {
        onDeleteCallback?.Invoke(slotIndex);
    }
}
