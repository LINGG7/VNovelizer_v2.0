using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class EndingSlot : MonoBehaviour
{
    private Image image;
    private TextMeshProUGUI endingText;
    private Button button;

    public VNEnding endingData;
    public bool isUnlocked;
    private System.Action<VNEnding, bool> onClickCallback;
    private int spriteLoadVersion;

    public void Init(VNEnding endingData, bool isUnlocked, System.Action<VNEnding, bool> onClickCallback)
    {
        this.endingData = endingData;
        this.isUnlocked = isUnlocked;
        this.onClickCallback = onClickCallback;

        button = GetComponent<Button>();
        if (button == null)
        {
            Debug.LogError("[EndingSlot] Button component not found.");
        }

        Transform imageTransform = transform.Find("Image");
        if (imageTransform != null)
        {
            image = imageTransform.GetComponent<Image>();
        }

        if (image == null)
        {
            image = GetComponentInChildren<Image>();
        }

        Transform textTransform = transform.Find("EndingText");
        if (textTransform != null)
        {
            endingText = textTransform.GetComponent<TextMeshProUGUI>();
        }

        if (endingText == null)
        {
            endingText = GetComponentInChildren<TextMeshProUGUI>(true);
        }

        if (image != null)
        {
            image.raycastTarget = false;
        }

        UpdateVisual();

        if (button != null)
        {
            button.interactable = true;
            button.onClick.RemoveAllListeners();
            button.onClick.AddListener(OnClick);

            Image buttonImage = button.GetComponent<Image>();
            if (buttonImage != null)
            {
                buttonImage.raycastTarget = true;
            }
        }
    }

    public void Unlock()
    {
        isUnlocked = true;
        UpdateVisual();
    }

    private void UpdateVisual()
    {
        if (image != null)
        {
            Sprite fallbackSprite = EndingImageLoader.GetFallbackSprite(endingData, isUnlocked);
            ApplySprite(fallbackSprite);
            image.enabled = true;

            string spritePath = EndingImageLoader.GetSpritePath(endingData, isUnlocked);
            int version = ++spriteLoadVersion;
            if (!string.IsNullOrEmpty(spritePath))
            {
                StartCoroutine(EndingImageLoader.LoadSpriteAsync(endingData, isUnlocked, sprite =>
                {
                    if (this == null || version != spriteLoadVersion)
                    {
                        return;
                    }

                    ApplySprite(sprite);
                }));
            }
        }

        if (endingText != null)
        {
            endingText.text = GetCurrentText();
        }
    }

    private void ApplySprite(Sprite sprite)
    {
        image.sprite = sprite;
        image.color = sprite != null ? Color.white : new Color(0.3f, 0.3f, 0.3f, 1f);
    }

    private string GetCurrentText()
    {
        if (endingData == null)
        {
            return string.Empty;
        }

        string text = isUnlocked ? endingData.UnlockedText : endingData.LockedText;
        if (!string.IsNullOrWhiteSpace(text))
        {
            return text;
        }

        return string.IsNullOrWhiteSpace(endingData.EndingID) ? "Ending" : endingData.EndingID;
    }

    private void OnClick()
    {
        if (onClickCallback != null && endingData != null)
        {
            onClickCallback(endingData, isUnlocked);
        }
    }
}
