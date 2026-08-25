using System.Collections;
using System;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.Events;

public class ConfirmPanel : BasePanel
{
    [SerializeField]private TextMeshProUGUI messageText;
    [SerializeField]private Button yesBtn;
    [SerializeField]private Button noBtn;
    private TextMeshProUGUI yesButtonText;
    private TextMeshProUGUI noButtonText;

    [Header("Popup Animation")]
    [SerializeField] private bool enablePopupAnimation = true;
    [SerializeField] private float popupDuration = 0.18f;
    [SerializeField] private float popupStartScale = 0.92f;

    private UnityAction onConfirmCallback;
    private UnityAction onCancelCallback;
    private UnityAction onOkCallback;
    private CanvasGroup popupCanvasGroup;
    private RectTransform popupRect;
    private Vector3 popupOriginalScale = Vector3.one;
    private Coroutine popupAnimationCoroutine;
    private GameObject inputBlocker;
    private const int InputBlockerSortingOrder = 12;
    private const int PanelSortingOrder = 13;

    protected override void Awake()
    {
        base.Awake();
        InitializePopupAnimation();

        messageText = GetControl<TextMeshProUGUI>("Message");
        yesBtn = GetControl<Button>("Yes");
        noBtn = GetControl<Button>("No");
        yesButtonText = yesBtn != null ? yesBtn.GetComponentInChildren<TextMeshProUGUI>(true) : null;
        noButtonText = noBtn != null ? noBtn.GetComponentInChildren<TextMeshProUGUI>(true) : null;

        yesBtn.onClick.AddListener(OnYesClick);
        noBtn.onClick.AddListener(OnNoClick);
    }

    private void InitializePopupAnimation()
    {
        popupRect = transform as RectTransform;
        popupOriginalScale = transform.localScale;
        popupCanvasGroup = GetComponent<CanvasGroup>();
        if (popupCanvasGroup == null)
        {
            popupCanvasGroup = gameObject.AddComponent<CanvasGroup>();
        }
    }

    /// <summary>
    /// 显示弹窗
    /// </summary>
    /// <param name="title">标题</param>
    /// <param name="message">内容</param>
    /// <param name="onConfirm">点击确定的回调</param>
    /// <param name="onCancel">点击取消的回调(可选)</param>
    /// <param name="confirmText">确定按钮文案</param>
    /// <param name="cancelText">取消按钮文案</param>
    public void Show(
        string title,
        string message,
        UnityAction onConfirm,
        UnityAction onCancel = null,
        string confirmText = "确定",
        string cancelText = "取消")
    {
        EnsureInputBlocker();
        messageText.text = message;
        if (yesButtonText != null)
            yesButtonText.text = confirmText;
        if (noButtonText != null)
            noButtonText.text = cancelText;

        onConfirmCallback = onConfirm;
        onCancelCallback = onCancel;

        ShowMe();
    }

    private void EnsureInputBlocker()
    {
        const string blockerName = "ConfirmInputBlocker";
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
        panelCanvas.sortingOrder = PanelSortingOrder;

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
        blockerButton.onClick.RemoveListener(OnNoClick);
        blockerButton.onClick.AddListener(OnNoClick);
    }

    private void HideInputBlocker()
    {
        if (inputBlocker != null)
        {
            inputBlocker.SetActive(false);
        }
    }

    public override void ShowMe()
    {
        gameObject.SetActive(true);
        PlayPopupAnimation();
    }

    private void PlayPopupAnimation()
    {
        if (!enablePopupAnimation)
        {
            ResetPopupAnimationState();
            return;
        }

        if (popupCanvasGroup == null || popupRect == null)
        {
            InitializePopupAnimation();
        }

        if (popupAnimationCoroutine != null)
        {
            StopCoroutine(popupAnimationCoroutine);
        }

        popupAnimationCoroutine = StartCoroutine(PopupAnimationCoroutine());
    }

    private IEnumerator PopupAnimationCoroutine()
    {
        float duration = Mathf.Max(0.01f, popupDuration);
        float elapsed = 0f;
        Vector3 startScale = popupOriginalScale * Mathf.Clamp(popupStartScale, 0.01f, 1f);

        popupCanvasGroup.alpha = 0f;
        popupCanvasGroup.interactable = false;
        popupCanvasGroup.blocksRaycasts = false;
        popupRect.localScale = startScale;

        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            float eased = EaseOutBack(t);

            popupCanvasGroup.alpha = t;
            popupRect.localScale = Vector3.LerpUnclamped(startScale, popupOriginalScale, eased);
            yield return null;
        }

        popupAnimationCoroutine = null;
        ResetPopupAnimationState();
    }

    private float EaseOutBack(float t)
    {
        const float c1 = 1.70158f;
        const float c3 = c1 + 1f;
        return 1f + c3 * Mathf.Pow(t - 1f, 3f) + c1 * Mathf.Pow(t - 1f, 2f);
    }

    private void ResetPopupAnimationState()
    {
        if (popupCanvasGroup != null)
        {
            popupCanvasGroup.alpha = 1f;
            popupCanvasGroup.interactable = true;
            popupCanvasGroup.blocksRaycasts = true;
        }

        if (popupRect != null)
        {
            popupRect.localScale = popupOriginalScale;
        }
    }

    public override void HideMe()
    {
        if (popupAnimationCoroutine != null)
        {
            StopCoroutine(popupAnimationCoroutine);
            popupAnimationCoroutine = null;
        }
        ResetPopupAnimationState();

        HideInputBlocker();
        gameObject.SetActive(false);
    }

    private void OnYesClick()
    {
        onConfirmCallback?.Invoke();
        ClosePanel();
    }

    private void OnNoClick()
    {
        onCancelCallback?.Invoke();
        ClosePanel();
    }

    private void ClosePanel()
    {
        HideInputBlocker();
        UIManager.GetInstance().HidePanel("ConfirmPanel");
    }

    private void OnDestroy()
    {
        HideInputBlocker();
    }
}
