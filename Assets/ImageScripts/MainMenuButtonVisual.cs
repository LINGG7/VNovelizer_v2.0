using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

[DisallowMultipleComponent]
[RequireComponent(typeof(Button))]
[RequireComponent(typeof(Image))]
public class MainMenuButtonVisual : MonoBehaviour,
    IPointerEnterHandler,
    IPointerExitHandler,
    IPointerDownHandler,
    IPointerUpHandler,
    ISelectHandler,
    IDeselectHandler
{
    private const string GlowLayerName = "GlowLayer";

    [Header("References")]
    [SerializeField] private Button button;
    [SerializeField] private Image backgroundImage;
    [SerializeField] private Image glowLayer;
    [SerializeField] private TMP_Text[] labels;

    [Header("Timing")]
    [SerializeField] private float stateLerpSpeed = 9f;
    [SerializeField] private Vector2 flickerIntervalRange = new Vector2(1.2f, 3.5f);
    [SerializeField] private Vector2Int flickerCountRange = new Vector2Int(2, 5);

    [Header("Alpha")]
    [SerializeField] private float normalFrameAlpha = 0.48f;
    [SerializeField] private float highlightedFrameAlpha = 0.9f;
    [SerializeField] private float pressedFrameAlpha = 0.62f;
    [SerializeField] private float disabledFrameAlpha = 0.28f;
    [SerializeField] private float normalGlowAlpha = 0.12f;
    [SerializeField] private float highlightedGlowAlpha = 0.42f;
    [SerializeField] private float disabledGlowAlpha = 0.05f;

    [Header("Colors")]
    [SerializeField] private Color normalFrameColor = new Color(0.18f, 0.72f, 0.94f, 1f);
    [SerializeField] private Color highlightedFrameColor = new Color(0.55f, 0.94f, 1f, 1f);
    [SerializeField] private Color disabledFrameColor = new Color(0.22f, 0.36f, 0.45f, 1f);
    [SerializeField] private Color normalTextColor = new Color(0.74f, 0.93f, 1f, 1f);
    [SerializeField] private Color highlightedTextColor = Color.white;
    [SerializeField] private Color disabledTextColor = new Color(0.38f, 0.55f, 0.64f, 0.82f);

    private RectTransform rectTransform;
    private Vector3 baseScale;
    private bool isHovered;
    private bool isPressed;
    private bool isSelected;
    private bool isFlickering;
    private float flickerFactor = 1f;

    private void Awake()
    {
        CacheReferences();
        EnsureGeneratedLayers();
        baseScale = transform.localScale;
        ApplyImmediateState();
    }

    private void OnEnable()
    {
        CacheReferences();
        EnsureGeneratedLayers();
        if (baseScale == Vector3.zero)
            baseScale = transform.localScale == Vector3.zero ? Vector3.one : transform.localScale;
        StartCoroutine(FlickerRoutine());
    }

    private void OnDisable()
    {
        StopAllCoroutines();
        isHovered = false;
        isPressed = false;
        isSelected = false;
        isFlickering = false;
        flickerFactor = 1f;
        if (transform != null)
            transform.localScale = baseScale == Vector3.zero ? Vector3.one : baseScale;
    }

    private void Update()
    {
        ApplyAnimatedState();
    }

    public void OnPointerEnter(PointerEventData eventData) => isHovered = true;
    public void OnPointerExit(PointerEventData eventData)
    {
        isHovered = false;
        isPressed = false;
    }

    public void OnPointerDown(PointerEventData eventData) => isPressed = true;
    public void OnPointerUp(PointerEventData eventData) => isPressed = false;
    public void OnSelect(BaseEventData eventData) => isSelected = true;
    public void OnDeselect(BaseEventData eventData)
    {
        isSelected = false;
        isPressed = false;
    }

    private void CacheReferences()
    {
        if (button == null)
            button = GetComponent<Button>();
        if (backgroundImage == null)
            backgroundImage = GetComponent<Image>();
        if (rectTransform == null)
            rectTransform = GetComponent<RectTransform>();
        if (labels == null || labels.Length == 0)
            labels = GetComponentsInChildren<TMP_Text>(true);
    }

    private void EnsureGeneratedLayers()
    {
        if (backgroundImage == null || rectTransform == null)
            return;

        if (glowLayer == null)
            glowLayer = FindOrCreateLayer(GlowLayerName, true);

        ConfigureGlowLayer();
        backgroundImage.type = Image.Type.Sliced;
    }

    private Image FindOrCreateLayer(string layerName, bool useFrameSprite)
    {
        Transform existing = transform.Find(layerName);
        GameObject layerObject = existing != null
            ? existing.gameObject
            : new GameObject(layerName, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));

        if (existing == null)
            layerObject.transform.SetParent(transform, false);

        layerObject.layer = gameObject.layer;
        layerObject.transform.SetAsFirstSibling();

        Image image = layerObject.GetComponent<Image>();
        image.raycastTarget = false;
        image.sprite = useFrameSprite ? backgroundImage.sprite : null;
        image.type = useFrameSprite ? Image.Type.Sliced : Image.Type.Simple;
        image.preserveAspect = false;
        return image;
    }

    private void ConfigureGlowLayer()
    {
        RectTransform glowRect = glowLayer.rectTransform;
        glowRect.anchorMin = Vector2.zero;
        glowRect.anchorMax = Vector2.one;
        glowRect.offsetMin = new Vector2(-8f, -7f);
        glowRect.offsetMax = new Vector2(8f, 7f);
        glowRect.pivot = new Vector2(0.5f, 0.5f);
    }

    private IEnumerator FlickerRoutine()
    {
        yield return new WaitForSecondsRealtime(Random.Range(0f, 0.8f));

        while (true)
        {
            yield return new WaitForSecondsRealtime(Random.Range(flickerIntervalRange.x, flickerIntervalRange.y));

            isFlickering = true;
            int count = Random.Range(flickerCountRange.x, flickerCountRange.y + 1);
            for (int i = 0; i < count; i++)
            {
                flickerFactor = Random.Range(0.18f, 0.42f);
                yield return new WaitForSecondsRealtime(Random.Range(0.025f, 0.075f));
                flickerFactor = Random.Range(0.85f, 1.18f);
                yield return new WaitForSecondsRealtime(Random.Range(0.025f, 0.08f));
            }

            flickerFactor = 1f;
            isFlickering = false;
        }
    }

    private void ApplyImmediateState()
    {
        ApplyState(1f);
    }

    private void ApplyAnimatedState()
    {
        float t = Time.unscaledDeltaTime * stateLerpSpeed;
        ApplyState(t);
    }

    private void ApplyState(float lerpT)
    {
        bool interactable = button == null || button.interactable;
        bool active = interactable && (isHovered || isSelected);

        Color frameColor = interactable
            ? (active ? highlightedFrameColor : normalFrameColor)
            : disabledFrameColor;

        Color textColor = interactable
            ? (active ? highlightedTextColor : normalTextColor)
            : disabledTextColor;

        float frameAlpha = interactable
            ? (isPressed ? pressedFrameAlpha : active ? highlightedFrameAlpha : normalFrameAlpha)
            : disabledFrameAlpha;

        float glowAlpha = interactable
            ? (active ? highlightedGlowAlpha : normalGlowAlpha)
            : disabledGlowAlpha;

        float flicker = isFlickering ? flickerFactor : 1f;

        SetImageColor(backgroundImage, frameColor, frameAlpha * flicker, lerpT);
        SetImageColor(glowLayer, highlightedFrameColor, glowAlpha * flicker, lerpT);

        foreach (TMP_Text label in labels)
        {
            if (label == null)
                continue;

            label.color = Color.Lerp(label.color, textColor, lerpT);
        }

        Vector3 targetScale = isPressed && interactable ? baseScale * 0.98f : baseScale;
        transform.localScale = Vector3.Lerp(transform.localScale, targetScale, lerpT);
    }

    private static void SetImageColor(Image image, Color color, float alpha, float lerpT)
    {
        if (image == null)
            return;

        color.a = Mathf.Clamp01(alpha);
        image.color = Color.Lerp(image.color, color, lerpT);
    }
}
