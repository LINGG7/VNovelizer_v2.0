using PrimeTween;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class StaminaRecoveryPopup : MonoBehaviour
{
    private const string PopupPrefabPath = "VNovelizerRes/VNPrefabs/UI/Monetization/StaminaRecoveryPopup";

    [SerializeField] private Button watchAdButton;
    [SerializeField] private Button closeButton;
    [SerializeField] private TMP_Text staminaText;
    [SerializeField] private TMP_Text statusText;
    [SerializeField] private RectTransform panelTransform;
    [SerializeField] private float enterStartScale = 0.85f;
    [SerializeField] private float enterDuration = 0.18f;
    private bool isWatchingAd;
    private bool pushedGameState;
    private Tween enterTween;

    public static void Show()
    {
        StaminaRecoveryPopup popup = Object.FindFirstObjectByType<StaminaRecoveryPopup>(FindObjectsInactive.Include);
        if (popup == null)
        {
            popup = CreatePopup();
        }

        popup.ShowInternal();
    }

    private static StaminaRecoveryPopup CreatePopup()
    {
        UIManager.GetInstance().Init();
        Transform parent = UIManager.GetInstance().GetLayerFather(E_UI_Layer.System);
        if (parent == null)
            parent = UIManager.GetInstance().canvas;

        GameObject prefab = ResourcesManager.GetInstance().Load<GameObject>(PopupPrefabPath);
        if (prefab != null)
        {
            GameObject instance = Object.Instantiate(prefab, parent, false);
            instance.name = "StaminaRecoveryPopup";

            StaminaRecoveryPopup prefabPopup = instance.GetComponent<StaminaRecoveryPopup>();
            if (prefabPopup == null)
                prefabPopup = instance.AddComponent<StaminaRecoveryPopup>();

            prefabPopup.BindPrefabReferences();
            instance.SetActive(false);
            return prefabPopup;
        }

        GameObject overlay = new GameObject("StaminaRecoveryPopup", typeof(RectTransform), typeof(CanvasGroup), typeof(Image));
        overlay.transform.SetParent(parent, false);

        RectTransform overlayRect = overlay.GetComponent<RectTransform>();
        overlayRect.anchorMin = Vector2.zero;
        overlayRect.anchorMax = Vector2.one;
        overlayRect.offsetMin = Vector2.zero;
        overlayRect.offsetMax = Vector2.zero;

        Image overlayImage = overlay.GetComponent<Image>();
        overlayImage.color = new Color(0f, 0f, 0f, 0.55f);

        StaminaRecoveryPopup popup = overlay.AddComponent<StaminaRecoveryPopup>();
        popup.BuildContent(overlay.transform);
        overlay.SetActive(false);
        return popup;
    }

    private void BuildContent(Transform parent)
    {
        GameObject panel = new GameObject("Panel", typeof(RectTransform), typeof(Image));
        panel.transform.SetParent(parent, false);

        RectTransform panelRect = panel.GetComponent<RectTransform>();
        panelTransform = panelRect;
        panelRect.anchorMin = new Vector2(0.5f, 0.5f);
        panelRect.anchorMax = new Vector2(0.5f, 0.5f);
        panelRect.pivot = new Vector2(0.5f, 0.5f);
        panelRect.sizeDelta = new Vector2(500f, 300f);
        panelRect.anchoredPosition = Vector2.zero;

        Image panelImage = panel.GetComponent<Image>();
        panelImage.color = new Color(0.12f, 0.12f, 0.15f, 0.96f);

        TMP_Text title = CreateText(panel.transform, "Title", 28, FontStyles.Bold, TextAlignmentOptions.Center);
        SetRect(title.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -48f), new Vector2(440f, 44f));
        title.text = "\u4f53\u529b\u4e0d\u8db3";

        TMP_Text desc = CreateText(panel.transform, "Description", 20, FontStyles.Normal, TextAlignmentOptions.Center);
        SetRect(desc.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -102f), new Vector2(430f, 58f));
        desc.text = "\u89c2\u770b\u5956\u52b1\u5e7f\u544a\u53ef\u6062\u590d\u4f53\u529b";

        staminaText = CreateText(panel.transform, "StaminaText", 22, FontStyles.Bold, TextAlignmentOptions.Center);
        SetRect(staminaText.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 10f), new Vector2(300f, 40f));

        statusText = CreateText(panel.transform, "StatusText", 16, FontStyles.Normal, TextAlignmentOptions.Center);
        SetRect(statusText.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, -32f), new Vector2(420f, 30f));
        statusText.color = new Color(1f, 0.82f, 0.42f, 1f);

        watchAdButton = CreateButton(panel.transform, "WatchAdButton", "\u89c2\u770b\u5e7f\u544a");
        SetRect(watchAdButton.GetComponent<RectTransform>(), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(-82f, 54f), new Vector2(160f, 48f));
        watchAdButton.onClick.AddListener(OnWatchAdClicked);

        closeButton = CreateButton(panel.transform, "CloseButton", "\u5173\u95ed");
        SetRect(closeButton.GetComponent<RectTransform>(), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(92f, 54f), new Vector2(120f, 48f));
        closeButton.onClick.AddListener(Hide);
    }

    private void BindPrefabReferences()
    {
        if (watchAdButton == null)
            watchAdButton = transform.Find("Panel/WatchAdButton")?.GetComponent<Button>();

        if (closeButton == null)
            closeButton = transform.Find("Panel/CloseButton")?.GetComponent<Button>();

        if (staminaText == null)
            staminaText = transform.Find("Panel/StaminaText")?.GetComponent<TMP_Text>();

        if (statusText == null)
            statusText = transform.Find("Panel/StatusText")?.GetComponent<TMP_Text>();

        if (panelTransform == null)
            panelTransform = transform.Find("Panel")?.GetComponent<RectTransform>();

        if (watchAdButton != null)
        {
            watchAdButton.onClick.RemoveListener(OnWatchAdClicked);
            watchAdButton.onClick.AddListener(OnWatchAdClicked);
        }

        if (closeButton != null)
        {
            closeButton.onClick.RemoveListener(Hide);
            closeButton.onClick.AddListener(Hide);
        }
    }

    private void ShowInternal()
    {
        BindPrefabReferences();

        if (!gameObject.activeSelf && GameStateManager.GetInstance().CanInteractGameplay())
        {
            GameStateManager.GetInstance().PushState(GameState.System);
            pushedGameState = true;
        }

        isWatchingAd = false;
        gameObject.SetActive(true);
        transform.SetAsLastSibling();
        PlayEnterAnimation();
        UpdateStaminaText();
        SetButtonInteractable(true);
        if (statusText != null)
            statusText.text = "";
    }

    private void PlayEnterAnimation()
    {
        if (panelTransform == null)
            return;

        if (enterTween.isAlive)
            enterTween.Stop();

        float startScale = Mathf.Max(0.01f, enterStartScale);
        float duration = Mathf.Max(0.01f, enterDuration);
        panelTransform.localScale = Vector3.one * startScale;
        enterTween = Tween.Scale(panelTransform, Vector3.one * startScale, Vector3.one, duration, Ease.OutBack, useUnscaledTime: true);
    }

    private void Hide()
    {
        if (isWatchingAd)
            return;

        RestoreGameStateIfNeeded();
        if (enterTween.isAlive)
            enterTween.Stop();
        gameObject.SetActive(false);
    }

    private void OnWatchAdClicked()
    {
        if (isWatchingAd)
            return;

        isWatchingAd = true;
        SetButtonInteractable(false);
        if (statusText != null)
            statusText.text = "\u5e7f\u544a\u52a0\u8f7d\u4e2d...";

        RewardedAdManager.GetInstance().ShowRewardedAd(OnAdRewarded, OnAdFailed);
    }

    private void OnAdRewarded()
    {
        StaminaManager.GetInstance().RestoreRecoveryAmount();
        UpdateStaminaText();
        if (statusText != null)
            statusText.text = "\u4f53\u529b\u5df2\u6062\u590d";
        isWatchingAd = false;
        RestoreGameStateIfNeeded();
        if (enterTween.isAlive)
            enterTween.Stop();
        gameObject.SetActive(false);
    }

    private void OnAdFailed()
    {
        if (statusText != null)
            statusText.text = "\u5e7f\u544a\u64ad\u653e\u5931\u8d25\uff0c\u8bf7\u7a0d\u540e\u518d\u8bd5";
        isWatchingAd = false;
        SetButtonInteractable(true);
    }

    private void UpdateStaminaText()
    {
        StaminaManager stamina = StaminaManager.GetInstance();
        stamina.Init();
        if (staminaText != null)
            staminaText.text = $"{stamina.CurrentStamina}/{stamina.MaxStamina}";
    }

    private void SetButtonInteractable(bool interactable)
    {
        if (watchAdButton != null)
            watchAdButton.interactable = interactable;

        if (closeButton != null)
            closeButton.interactable = interactable;
    }

    private void RestoreGameStateIfNeeded()
    {
        if (!pushedGameState)
            return;

        pushedGameState = false;
        GameStateManager.GetInstance().PopState();
    }

    private static TMP_Text CreateText(Transform parent, string name, int fontSize, FontStyles fontStyle, TextAlignmentOptions alignment)
    {
        GameObject obj = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
        obj.transform.SetParent(parent, false);

        TMP_Text text = obj.GetComponent<TMP_Text>();
        text.fontSize = fontSize;
        text.fontStyle = fontStyle;
        text.alignment = alignment;
        text.color = Color.white;
        text.enableWordWrapping = true;
        ApplyDefaultTMPFont(text);
        return text;
    }

    private static void ApplyDefaultTMPFont(TMP_Text text)
    {
        if (text == null || TMP_Settings.defaultFontAsset == null)
            return;

        text.font = TMP_Settings.defaultFontAsset;
        text.SetAllDirty();
    }

    private static Button CreateButton(Transform parent, string name, string label)
    {
        GameObject obj = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
        obj.transform.SetParent(parent, false);

        Image image = obj.GetComponent<Image>();
        image.color = new Color(0.9f, 0.72f, 0.28f, 1f);

        Button button = obj.GetComponent<Button>();
        button.targetGraphic = image;

        TMP_Text text = CreateText(obj.transform, "Text", 20, FontStyles.Bold, TextAlignmentOptions.Center);
        SetRect(text.rectTransform, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
        text.text = label;
        text.color = new Color(0.1f, 0.08f, 0.05f, 1f);
        return button;
    }

    private static void SetRect(RectTransform rect, Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot, Vector2 anchoredPosition, Vector2 sizeDelta)
    {
        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.pivot = pivot;
        rect.anchoredPosition = anchoredPosition;
        rect.sizeDelta = sizeDelta;
    }
}
