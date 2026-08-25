using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class ToBeContinuedPanel : BasePanel
{
    private const string Message = "未完待续......";

    [SerializeField] private TextMeshProUGUI messageText;
    [SerializeField] private Button returnTitleBtn;
    [SerializeField] private RectTransform popupRoot;

    [Header("Popup Animation")]
    [SerializeField] private bool enablePopupAnimation = true;
    [SerializeField] private float popupDuration = 0.2f;
    [SerializeField] private float popupStartScale = 0.9f;

    private CanvasGroup canvasGroup;
    private Vector3 popupOriginalScale = Vector3.one;
    private Coroutine popupAnimationCoroutine;
    private bool isReturning;

    protected override void Awake()
    {
        base.Awake();

        EnsureLayout();
        InitializePopupAnimation();

        messageText.text = Message;
        returnTitleBtn.onClick.RemoveListener(OnReturnTitleClick);
        returnTitleBtn.onClick.AddListener(OnReturnTitleClick);
    }

    public override void ShowMe()
    {
        gameObject.SetActive(true);
        isReturning = false;
        if (returnTitleBtn != null)
        {
            returnTitleBtn.interactable = true;
        }

        PlayPopupAnimation();
    }

    public override void HideMe()
    {
        StopPopupAnimation();
        gameObject.SetActive(false);
    }

    private void EnsureLayout()
    {
        RectTransform rootRect = transform as RectTransform;
        if (rootRect != null)
        {
            rootRect.anchorMin = Vector2.zero;
            rootRect.anchorMax = Vector2.one;
            rootRect.offsetMin = Vector2.zero;
            rootRect.offsetMax = Vector2.zero;
            rootRect.localScale = Vector3.one;
        }

        Image backdrop = GetComponent<Image>();
        bool createdBackdrop = backdrop == null;
        if (backdrop == null)
        {
            backdrop = gameObject.AddComponent<Image>();
        }
        if (createdBackdrop)
        {
            backdrop.color = new Color(0f, 0f, 0f, 0.68f);
        }
        backdrop.raycastTarget = true;

        popupRoot = popupRoot != null ? popupRoot : FindOrCreateRect("Popup", transform);
        popupRoot.anchorMin = new Vector2(0.5f, 0.5f);
        popupRoot.anchorMax = new Vector2(0.5f, 0.5f);
        popupRoot.pivot = new Vector2(0.5f, 0.5f);
        popupRoot.anchoredPosition = Vector2.zero;
        popupRoot.sizeDelta = new Vector2(720f, 300f);
        popupRoot.localScale = Vector3.one;

        Image popupImage = popupRoot.GetComponent<Image>();
        bool createdPopupImage = popupImage == null;
        if (popupImage == null)
        {
            popupImage = popupRoot.gameObject.AddComponent<Image>();
        }
        if (createdPopupImage)
        {
            popupImage.color = new Color(0.08f, 0.09f, 0.11f, 0.96f);
        }
        popupImage.raycastTarget = true;

        bool createdMessageText = messageText == null;
        messageText = messageText != null ? messageText : FindOrCreateText("MessageText", popupRoot);
        RectTransform messageRect = messageText.rectTransform;
        messageRect.anchorMin = new Vector2(0f, 0.45f);
        messageRect.anchorMax = new Vector2(1f, 1f);
        messageRect.offsetMin = new Vector2(48f, 0f);
        messageRect.offsetMax = new Vector2(-48f, -36f);
        messageText.alignment = TextAlignmentOptions.Center;
        messageText.fontSize = 42f;
        if (createdMessageText)
        {
            messageText.color = Color.white;
        }
        messageText.enableAutoSizing = false;

        returnTitleBtn = returnTitleBtn != null ? returnTitleBtn : FindOrCreateButton("ReturnTitleBtn", popupRoot);
        RectTransform buttonRect = returnTitleBtn.transform as RectTransform;
        buttonRect.anchorMin = new Vector2(0.5f, 0f);
        buttonRect.anchorMax = new Vector2(0.5f, 0f);
        buttonRect.pivot = new Vector2(0.5f, 0f);
        buttonRect.anchoredPosition = new Vector2(0f, 42f);
        buttonRect.sizeDelta = new Vector2(220f, 64f);

        Image buttonImage = returnTitleBtn.GetComponent<Image>();
        bool createdButtonImage = buttonImage == null;
        if (buttonImage == null)
        {
            buttonImage = returnTitleBtn.gameObject.AddComponent<Image>();
        }
        if (createdButtonImage)
        {
            buttonImage.color = new Color(0.95f, 0.95f, 0.92f, 1f);
        }
        buttonImage.raycastTarget = true;
        returnTitleBtn.targetGraphic = buttonImage;

        TextMeshProUGUI label = returnTitleBtn.GetComponentInChildren<TextMeshProUGUI>(true);
        bool createdLabel = label == null;
        if (label == null)
        {
            label = FindOrCreateText("Label", buttonRect);
        }

        RectTransform labelRect = label.rectTransform;
        labelRect.anchorMin = Vector2.zero;
        labelRect.anchorMax = Vector2.one;
        labelRect.offsetMin = Vector2.zero;
        labelRect.offsetMax = Vector2.zero;
        label.text = "返回标题";
        label.alignment = TextAlignmentOptions.Center;
        label.fontSize = 28f;
        if (createdLabel)
        {
            label.color = new Color(0.08f, 0.09f, 0.11f, 1f);
        }
    }

    private RectTransform FindOrCreateRect(string objectName, Transform parent)
    {
        Transform existing = parent.Find(objectName);
        if (existing != null)
        {
            return existing as RectTransform;
        }

        GameObject go = new GameObject(objectName, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        return go.transform as RectTransform;
    }

    private TextMeshProUGUI FindOrCreateText(string objectName, Transform parent)
    {
        Transform existing = parent.Find(objectName);
        if (existing != null)
        {
            TextMeshProUGUI existingText = existing.GetComponent<TextMeshProUGUI>();
            if (existingText != null)
            {
                return existingText;
            }
        }

        GameObject go = new GameObject(objectName, typeof(RectTransform), typeof(TextMeshProUGUI));
        go.transform.SetParent(parent, false);
        return go.GetComponent<TextMeshProUGUI>();
    }

    private Button FindOrCreateButton(string objectName, Transform parent)
    {
        Transform existing = parent.Find(objectName);
        if (existing != null)
        {
            Button existingButton = existing.GetComponent<Button>();
            if (existingButton != null)
            {
                return existingButton;
            }
        }

        GameObject go = new GameObject(objectName, typeof(RectTransform), typeof(Image), typeof(Button));
        go.transform.SetParent(parent, false);
        return go.GetComponent<Button>();
    }

    private void InitializePopupAnimation()
    {
        canvasGroup = GetComponent<CanvasGroup>();
        if (canvasGroup == null)
        {
            canvasGroup = gameObject.AddComponent<CanvasGroup>();
        }

        popupOriginalScale = popupRoot != null ? popupRoot.localScale : Vector3.one;
    }

    private void PlayPopupAnimation()
    {
        if (!enablePopupAnimation)
        {
            ResetPopupAnimationState();
            return;
        }

        StopPopupAnimation();
        popupAnimationCoroutine = StartCoroutine(PopupAnimationCoroutine());
    }

    private IEnumerator PopupAnimationCoroutine()
    {
        float duration = Mathf.Max(0.01f, popupDuration);
        float elapsed = 0f;
        Vector3 startScale = popupOriginalScale * Mathf.Clamp(popupStartScale, 0.01f, 1f);

        canvasGroup.alpha = 0f;
        canvasGroup.interactable = false;
        canvasGroup.blocksRaycasts = false;
        popupRoot.localScale = startScale;

        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            float eased = EaseOutBack(t);

            canvasGroup.alpha = t;
            popupRoot.localScale = Vector3.LerpUnclamped(startScale, popupOriginalScale, eased);
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
        if (canvasGroup != null)
        {
            canvasGroup.alpha = 1f;
            canvasGroup.interactable = true;
            canvasGroup.blocksRaycasts = true;
        }

        if (popupRoot != null)
        {
            popupRoot.localScale = popupOriginalScale;
        }
    }

    private void StopPopupAnimation()
    {
        if (popupAnimationCoroutine != null)
        {
            StopCoroutine(popupAnimationCoroutine);
            popupAnimationCoroutine = null;
        }

        ResetPopupAnimationState();
    }

    private void OnReturnTitleClick()
    {
        if (isReturning)
        {
            return;
        }

        isReturning = true;
        returnTitleBtn.interactable = false;
        StartCoroutine(ReturnToTitleCoroutine());
    }

    private IEnumerator ReturnToTitleCoroutine()
    {
        SaveManager.GetInstance().CaptureCurrentScreen();
        yield return new WaitForEndOfFrame();
        yield return null;

        VNManager vnManager = VNManager.GetInstance();
        vnManager.AutoSaveGame();
        vnManager.ReturnToMainMenuFromToBeContinued();
    }
}
