using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class EndingChoicePanel : BasePanel
{
    [SerializeField] private Image unlockedImage;
    [SerializeField] private TMP_Text unlockedText;
    [SerializeField] private Button reselectButton;
    [SerializeField] private Button returnTitleButton;

    private CanvasGroup canvasGroup;
    private VNEnding currentEnding;
    private int endingSpriteLoadVersion;

    protected override void Awake()
    {
        base.Awake();
        BindControls();
    }

    public void ShowEnding(VNEnding ending)
    {
        currentEnding = ending;
        BindControls();
        ApplyEndingData();
        ShowMe();
    }

    public override void ShowMe()
    {
        gameObject.SetActive(true);
        transform.SetAsLastSibling();

        if (canvasGroup == null)
        {
            canvasGroup = GetComponent<CanvasGroup>();
        }
        if (canvasGroup == null)
        {
            canvasGroup = gameObject.AddComponent<CanvasGroup>();
        }

        canvasGroup.alpha = 1f;
        canvasGroup.interactable = true;
        canvasGroup.blocksRaycasts = true;
    }

    public override void HideMe()
    {
        gameObject.SetActive(false);
    }

    protected override void OnButtonClick(string buttonName)
    {
        if (buttonName == "ReSelectBtn")
        {
            OnReSelectClicked();
        }
        else if (buttonName == "ReturnTitleBtn")
        {
            OnReturnTitleClicked();
        }
    }

    private void BindControls()
    {
        if (unlockedImage == null) unlockedImage = GetControl<Image>("UnlockedImage");
        if (unlockedText == null) unlockedText = GetControl<TMP_Text>("UnlockedText");
        if (reselectButton == null) reselectButton = GetControl<Button>("ReSelectBtn");
        if (returnTitleButton == null) returnTitleButton = GetControl<Button>("ReturnTitleBtn");
        if (canvasGroup == null) canvasGroup = GetComponent<CanvasGroup>();
    }

    private void ApplyEndingData()
    {
        if (currentEnding == null)
        {
            return;
        }

        if (unlockedImage != null)
        {
            Sprite fallbackSprite = EndingImageLoader.GetFallbackSprite(currentEnding, true);
            ApplyEndingSprite(fallbackSprite);

            string spritePath = EndingImageLoader.GetSpritePath(currentEnding, true);
            int version = ++endingSpriteLoadVersion;
            if (!string.IsNullOrEmpty(spritePath))
            {
                StartCoroutine(EndingImageLoader.LoadSpriteAsync(currentEnding, true, sprite =>
                {
                    if (this == null || version != endingSpriteLoadVersion)
                    {
                        return;
                    }

                    ApplyEndingSprite(sprite);
                }));
            }
        }

        if (unlockedText != null)
        {
            unlockedText.text = !string.IsNullOrWhiteSpace(currentEnding.UnlockedText)
                ? currentEnding.UnlockedText
                : currentEnding.EndingID;
        }
    }

    private void ApplyEndingSprite(Sprite sprite)
    {
        if (unlockedImage == null)
        {
            return;
        }

        unlockedImage.sprite = sprite;
        unlockedImage.color = sprite != null ? Color.white : new Color(0.18f, 0.18f, 0.18f, 1f);
        unlockedImage.preserveAspect = true;
    }

    private void OnReSelectClicked()
    {
        UIManager.GetInstance().HidePanel("EndingChoicePanel");

        if (!VNManager.GetInstance().ReturnToLastSelectBC())
        {
            Debug.LogWarning("[EndingChoicePanel] No previous type:selectBC line found.");
        }
    }

    private void OnReturnTitleClicked()
    {
        VNManager.GetInstance().AutoSaveGame();
        UIManager.GetInstance().HidePanel("EndingChoicePanel");
        VNManager.GetInstance().ReturnToTitleFromEndingChoice();
    }
}
