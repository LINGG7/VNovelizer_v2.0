using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class SceneSlotDetail : MonoBehaviour
{
    [SerializeField] private Image sceneImage;
    [SerializeField] private TextMeshProUGUI detailText;
    [SerializeField] private Button closeButton;
    [SerializeField] private Button blockerButton;
    [SerializeField] private float fadeInDuration = 0.18f;

    private Image rootImage;
    private Image blockerImage;
    private CanvasGroup canvasGroup;
    private Coroutine fadeCoroutine;

    private void Awake()
    {
        BindReferences();
        BindEvents();
    }

    public void Show(VNScene sceneData, Sprite sprite)
    {
        if (sceneData == null)
        {
            return;
        }

        gameObject.SetActive(true);
        transform.SetAsLastSibling();
        EnsureOverlayVisual();
        PlayFadeIn();

        SetSprite(sprite);

        if (detailText != null)
        {
            detailText.text = BuildDetailText(sceneData);
        }
    }

    public void SetSprite(Sprite sprite)
    {
        if (sceneImage == null)
        {
            BindReferences();
        }

        if (sceneImage == null)
        {
            return;
        }

        sceneImage.sprite = sprite;
        sceneImage.color = sprite != null ? Color.white : new Color(0.24f, 0.24f, 0.24f, 1f);
        sceneImage.preserveAspect = true;
    }

    public void Hide()
    {
        StopFade();
        if (canvasGroup != null)
        {
            canvasGroup.alpha = 1f;
        }

        gameObject.SetActive(false);
    }

    private string BuildDetailText(VNScene sceneData)
    {
        if (!string.IsNullOrWhiteSpace(sceneData.DetailText))
        {
            return sceneData.DetailText;
        }

        if (!string.IsNullOrWhiteSpace(sceneData.ScriptName))
        {
            return sceneData.ScriptName;
        }

        if (!string.IsNullOrWhiteSpace(sceneData.VNscriptID))
        {
            return sceneData.VNscriptID;
        }

        return "Scene Detail";
    }

    private void BindReferences()
    {
        Transform imageTransform = transform.Find("Panel/Image");
        if (imageTransform != null)
        {
            sceneImage = imageTransform.GetComponent<Image>();
        }

        if (detailText == null)
        {
            Transform textTransform = transform.Find("Panel/DetailText");
            if (textTransform != null)
            {
                detailText = textTransform.GetComponent<TextMeshProUGUI>();
            }
        }

        if (closeButton == null)
        {
            Transform closeTransform = transform.Find("Panel/CloseBtn");
            if (closeTransform != null)
            {
                closeButton = closeTransform.GetComponent<Button>();
            }
        }

        if (blockerButton == null)
        {
            blockerButton = EnsureBlocker().GetComponent<Button>();
        }

        rootImage = GetComponent<Image>();
        canvasGroup = GetComponent<CanvasGroup>();
        if (canvasGroup == null)
        {
            canvasGroup = gameObject.AddComponent<CanvasGroup>();
        }
        canvasGroup.alpha = 1f;
        canvasGroup.interactable = true;
        canvasGroup.blocksRaycasts = true;

        EnsureOverlayVisual();
    }

    private void BindEvents()
    {
        EnsureBlocker();

        if (blockerButton != null)
        {
            blockerButton.transition = Selectable.Transition.None;
            blockerButton.onClick.RemoveAllListeners();
            blockerButton.onClick.AddListener(Hide);
        }

        if (closeButton != null)
        {
            closeButton.onClick.RemoveAllListeners();
            closeButton.onClick.AddListener(Hide);
        }
    }

    private void EnsureOverlayVisual()
    {
        DisableRootImage();
        EnsureBlockerVisual();
    }

    private void DisableRootImage()
    {
        if (rootImage == null)
        {
            rootImage = GetComponent<Image>();
        }

        if (rootImage != null)
        {
            rootImage.enabled = false;
            rootImage.raycastTarget = false;
        }
    }

    private GameObject EnsureBlocker()
    {
        Transform blockerTransform = transform.Find("Blocker");
        GameObject blockerObj;

        if (blockerTransform == null)
        {
            blockerObj = new GameObject("Blocker", typeof(RectTransform), typeof(Image), typeof(Button));
            blockerObj.transform.SetParent(transform, false);
        }
        else
        {
            blockerObj = blockerTransform.gameObject;
        }

        blockerObj.transform.SetAsFirstSibling();

        RectTransform blockerRect = blockerObj.transform as RectTransform;
        if (blockerRect != null)
        {
            blockerRect.anchorMin = Vector2.zero;
            blockerRect.anchorMax = Vector2.one;
            blockerRect.offsetMin = Vector2.zero;
            blockerRect.offsetMax = Vector2.zero;
            blockerRect.pivot = new Vector2(0.5f, 0.5f);
        }

        blockerImage = blockerObj.GetComponent<Image>();
        if (blockerImage == null)
        {
            blockerImage = blockerObj.AddComponent<Image>();
        }

        blockerButton = blockerObj.GetComponent<Button>();
        if (blockerButton == null)
        {
            blockerButton = blockerObj.AddComponent<Button>();
        }

        return blockerObj;
    }

    private void EnsureBlockerVisual()
    {
        if (blockerImage == null)
        {
            EnsureBlocker();
        }

        if (blockerImage != null)
        {
            blockerImage.sprite = null;
            blockerImage.color = new Color(0f, 0f, 0f, 0.72f);
            blockerImage.raycastTarget = true;
        }
    }

    private void PlayFadeIn()
    {
        if (canvasGroup == null)
        {
            return;
        }

        StopFade();
        canvasGroup.alpha = 0f;
        fadeCoroutine = StartCoroutine(FadeCanvasGroup(0f, 1f));
    }

    private IEnumerator FadeCanvasGroup(float from, float to)
    {
        float elapsed = 0f;
        while (elapsed < fadeInDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = fadeInDuration <= 0f ? 1f : Mathf.Clamp01(elapsed / fadeInDuration);
            canvasGroup.alpha = Mathf.Lerp(from, to, t);
            yield return null;
        }

        canvasGroup.alpha = to;
        fadeCoroutine = null;
    }

    private void StopFade()
    {
        if (fadeCoroutine != null)
        {
            StopCoroutine(fadeCoroutine);
            fadeCoroutine = null;
        }
    }
}
