using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Music list item.
/// </summary>
public class MusicSlot : MonoBehaviour
{
    private Button button;
    private Image backgroundImage;
    private TextMeshProUGUI nameText;
    private Image normalFrame;
    private Image selectedFrame;
    private Image lockIcon;

    public VNMusic musicData;
    private System.Action<VNMusic> onClickCallback;
    private bool isUnlocked;

    private readonly Color normalBackgroundColor = new Color(0.03f, 0.05f, 0.08f, 0.78f);
    private readonly Color selectedBackgroundColor = new Color(0.16f, 0.10f, 0.07f, 0.92f);
    private readonly Color normalFrameColor = new Color(0.72f, 0.54f, 0.32f, 0.24f);
    private readonly Color selectedFrameColor = new Color(0.95f, 0.72f, 0.42f, 0.95f);
    private readonly Color normalTextColor = new Color(0.78f, 0.73f, 0.65f, 1f);
    private readonly Color selectedTextColor = new Color(0.96f, 0.78f, 0.48f, 1f);
    private readonly Color lockedTextColor = new Color(0.45f, 0.45f, 0.45f, 1f);

    public void Init(VNMusic music, bool isUnlocked, System.Action<VNMusic> onClickCallback)
    {
        musicData = music;
        this.isUnlocked = isUnlocked;
        this.onClickCallback = onClickCallback;

        button = GetComponent<Button>();
        backgroundImage = GetComponent<Image>();
        EnsureVisualHierarchy();

        if (nameText != null && music != null)
        {
            nameText.text = music.name;
        }

        if (button != null)
        {
            button.interactable = isUnlocked;
            button.onClick.RemoveAllListeners();
            button.onClick.AddListener(OnClick);
        }

        UpdateVisualState(false);
    }

    public void Unlock()
    {
        isUnlocked = true;

        if (button != null)
        {
            button.interactable = true;
        }

        UpdateVisualState(false);
    }

    public void SetSelected(bool selected)
    {
        UpdateVisualState(selected);
    }

    private void OnClick()
    {
        if (onClickCallback != null && musicData != null)
        {
            onClickCallback(musicData);
        }
    }

    private void UpdateVisualState(bool selected)
    {
        EnsureVisualHierarchy();

        if (backgroundImage != null)
        {
            backgroundImage.color = selected ? selectedBackgroundColor : normalBackgroundColor;
        }

        if (normalFrame != null)
        {
            normalFrame.gameObject.SetActive(!selected);
            normalFrame.color = normalFrameColor;
        }

        if (selectedFrame != null)
        {
            selectedFrame.gameObject.SetActive(selected);
            selectedFrame.color = selectedFrameColor;
        }

        if (nameText != null)
        {
            nameText.color = !isUnlocked ? lockedTextColor : (selected ? selectedTextColor : normalTextColor);
        }

        if (lockIcon != null)
        {
            lockIcon.gameObject.SetActive(!isUnlocked);
        }

        if (button != null)
        {
            ColorBlock colors = button.colors;
            colors.normalColor = selected ? selectedBackgroundColor : normalBackgroundColor;
            colors.highlightedColor = selected ? selectedBackgroundColor : new Color(0.08f, 0.08f, 0.10f, 0.9f);
            colors.pressedColor = selectedBackgroundColor;
            colors.selectedColor = selectedBackgroundColor;
            colors.disabledColor = new Color(0.08f, 0.08f, 0.08f, 0.45f);
            colors.colorMultiplier = 1f;
            colors.fadeDuration = 0.1f;
            button.colors = colors;
        }
    }

    private void EnsureVisualHierarchy()
    {
        if (backgroundImage == null)
        {
            backgroundImage = GetComponent<Image>();
        }

        if (normalFrame == null)
        {
            normalFrame = GetOrCreateFrame("NormalFrame");
        }

        if (selectedFrame == null)
        {
            selectedFrame = GetOrCreateFrame("SelectedFrame");
        }

        if (lockIcon == null)
        {
            Transform lockTransform = transform.Find("Lock");
            if (lockTransform != null)
            {
                lockIcon = lockTransform.GetComponent<Image>();
            }
        }

        if (nameText == null)
        {
            nameText = GetComponentInChildren<TextMeshProUGUI>();
        }

        if (normalFrame != null)
        {
            normalFrame.transform.SetAsFirstSibling();
        }

        if (selectedFrame != null)
        {
            selectedFrame.transform.SetAsFirstSibling();
        }

        if (lockIcon != null)
        {
            lockIcon.transform.SetAsLastSibling();
        }

        if (nameText != null)
        {
            nameText.transform.SetAsLastSibling();
        }
    }

    private Image GetOrCreateFrame(string frameName)
    {
        Transform frameTransform = transform.Find(frameName);
        GameObject frameObj;

        if (frameTransform == null)
        {
            frameObj = new GameObject(frameName, typeof(RectTransform), typeof(Image));
            frameObj.transform.SetParent(transform, false);
        }
        else
        {
            frameObj = frameTransform.gameObject;
        }

        RectTransform rect = frameObj.transform as RectTransform;
        if (rect != null)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            rect.pivot = new Vector2(0.5f, 0.5f);
        }

        Image frameImage = frameObj.GetComponent<Image>();
        if (frameImage == null)
        {
            frameImage = frameObj.AddComponent<Image>();
        }

        frameImage.raycastTarget = false;
        frameImage.sprite = backgroundImage != null ? backgroundImage.sprite : frameImage.sprite;
        frameImage.type = Image.Type.Sliced;
        frameImage.fillCenter = false;
        return frameImage;
    }
}
