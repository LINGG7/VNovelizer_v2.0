using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// Controls the two-row, three-column save-slot pages.
/// </summary>
public sealed class SaveSlotHorizontalScroller : MonoBehaviour, IBeginDragHandler, IEndDragHandler, IScrollHandler
{
    private const int ColumnsPerPage = 3;
    private const int RowsPerPage = 2;
    private const int SlotsPerPage = ColumnsPerPage * RowsPerPage;
    private const float SnapDuration = 0.2f;
    private const float SnapDelay = 0.15f;
    private const float ForceSnapDelay = 0.4f;
    private const float SettledVelocity = 80f;

    private ScrollRect scrollRect;
    private RectTransform content;
    private RectTransform viewport;
    private GridLayoutGroup layoutGroup;
    private CanvasGroup contentCanvasGroup;
    private Button previousButton;
    private Button nextButton;
    private Coroutine layoutCoroutine;
    private Coroutine snapCoroutine;
    private bool isDragging;
    private bool snapPending;
    private float lastInputTime;
    private int currentPage;
    private int pageCount;
    private float pageWidth;

    public void Configure(ScrollRect targetScrollRect, Button previous, Button next)
    {
        Unbind();

        scrollRect = targetScrollRect;
        content = scrollRect != null ? scrollRect.content : null;
        viewport = scrollRect != null ? scrollRect.viewport : null;
        layoutGroup = content != null ? content.GetComponent<GridLayoutGroup>() : null;
        contentCanvasGroup = content != null ? content.GetComponent<CanvasGroup>() : null;
        if (content != null && contentCanvasGroup == null)
            contentCanvasGroup = content.gameObject.AddComponent<CanvasGroup>();
        previousButton = previous;
        nextButton = next;

        if (layoutGroup != null)
        {
            layoutGroup.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            layoutGroup.constraintCount = ColumnsPerPage;
            layoutGroup.startAxis = GridLayoutGroup.Axis.Horizontal;
            layoutGroup.enabled = false;
        }

        if (scrollRect != null)
        {
            scrollRect.horizontal = true;
            scrollRect.vertical = false;
            scrollRect.onValueChanged.AddListener(OnScrollValueChanged);
        }

        if (previousButton != null)
            previousButton.onClick.AddListener(ScrollPrevious);
        if (nextButton != null)
            nextButton.onClick.AddListener(ScrollNext);

        UpdateNavigationButtons();
    }

    public void RefreshLayout(bool scrollToEnd)
    {
        if (layoutCoroutine != null)
            StopCoroutine(layoutCoroutine);

        if (scrollRect != null)
            scrollRect.StopMovement();

        SetContentVisible(false);
        layoutCoroutine = StartCoroutine(RefreshLayoutAfterFrame(scrollToEnd));
    }

    public void ScrollToStart()
    {
        currentPage = 0;
        RefreshLayout(false);
    }

    public void OnBeginDrag(PointerEventData eventData)
    {
        isDragging = true;
        snapPending = false;
        StopSnapAnimation();
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        isDragging = false;
        QueueSnap();
    }

    public void OnScroll(PointerEventData eventData)
    {
        if (scrollRect == null)
            return;

        // Save/load uses a horizontal strip, so vertical wheel input moves it horizontally.
        scrollRect.horizontalNormalizedPosition = Mathf.Clamp01(
            scrollRect.horizontalNormalizedPosition - eventData.scrollDelta.y * 0.1f);
        QueueSnap();
    }

    private void Update()
    {
        if (!snapPending || isDragging || scrollRect == null)
            return;

        float idleTime = Time.unscaledTime - lastInputTime;
        bool movementSettled = Mathf.Abs(scrollRect.velocity.x) <= SettledVelocity;
        if (idleTime >= ForceSnapDelay || (idleTime >= SnapDelay && movementSettled))
        {
            snapPending = false;
            SnapToNearestPage();
        }
    }

    private IEnumerator RefreshLayoutAfterFrame(bool scrollToEnd)
    {
        yield return null;
        Canvas.ForceUpdateCanvases();

        ConfigureGrid();
        pageCount = Mathf.Max(1, Mathf.CeilToInt(GetActiveSlotCount() / (float)SlotsPerPage));
        currentPage = scrollToEnd ? pageCount - 1 : Mathf.Clamp(currentPage, 0, pageCount - 1);

        ApplyContentSize();
        LayoutSlots();

        SetContentVisible(true);
        Canvas.ForceUpdateCanvases();
        SetNormalizedPosition(GetNormalizedPosition(currentPage));

        layoutCoroutine = null;
        UpdateNavigationButtons();
    }

    private void SetContentVisible(bool visible)
    {
        if (contentCanvasGroup == null)
            return;

        contentCanvasGroup.alpha = visible ? 1f : 0f;
        contentCanvasGroup.interactable = visible;
        contentCanvasGroup.blocksRaycasts = visible;
    }

    private void ConfigureGrid()
    {
        if (layoutGroup == null)
            return;

        layoutGroup.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
        layoutGroup.constraintCount = ColumnsPerPage;
        layoutGroup.startAxis = GridLayoutGroup.Axis.Horizontal;
        layoutGroup.childAlignment = TextAnchor.UpperLeft;
        layoutGroup.enabled = false;
        pageWidth = Mathf.Max(1f, GetPageWidth());
    }

    private void ApplyContentSize()
    {
        if (content == null || viewport == null)
            return;

        float height = GetGridHeight();
        content.anchorMin = new Vector2(0f, 0.5f);
        content.anchorMax = new Vector2(0f, 0.5f);
        content.pivot = new Vector2(0f, 0.5f);
        content.anchoredPosition = new Vector2(0f, content.anchoredPosition.y);
        content.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, pageCount * pageWidth);
        content.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, height);
    }

    private void LayoutSlots()
    {
        if (content == null)
            return;

        Vector2 cellSize = layoutGroup != null ? layoutGroup.cellSize : new Vector2(360f, 220f);
        Vector2 spacing = layoutGroup != null ? layoutGroup.spacing : new Vector2(40f, 40f);
        RectOffset padding = layoutGroup != null ? layoutGroup.padding : new RectOffset();
        float gridWidth = padding.horizontal + ColumnsPerPage * cellSize.x +
                          (ColumnsPerPage - 1) * spacing.x;
        float pageInset = Mathf.Max(0f, (pageWidth - gridWidth) * 0.5f);
        int slotIndex = 0;

        for (int i = 0; i < content.childCount; i++)
        {
            RectTransform child = content.GetChild(i) as RectTransform;
            if (child == null || !child.gameObject.activeSelf)
                continue;

            int page = slotIndex / SlotsPerPage;
            int indexInPage = slotIndex % SlotsPerPage;
            int row = indexInPage / ColumnsPerPage;
            int column = indexInPage % ColumnsPerPage;

            child.anchorMin = new Vector2(0f, 1f);
            child.anchorMax = new Vector2(0f, 1f);
            child.pivot = new Vector2(0.5f, 0.5f);
            child.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, cellSize.x);
            child.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, cellSize.y);
            child.anchoredPosition = new Vector2(
                page * pageWidth + pageInset + padding.left + column * (cellSize.x + spacing.x) + cellSize.x * 0.5f,
                -(padding.top + row * (cellSize.y + spacing.y) + cellSize.y * 0.5f));

            slotIndex++;
        }
    }

    private void ScrollPrevious()
    {
        MoveToPage(GetNearestPage() - 1);
    }

    private void ScrollNext()
    {
        MoveToPage(GetNearestPage() + 1);
    }

    private void MoveToPage(int page)
    {
        if (pageCount <= 1)
            return;

        currentPage = Mathf.Clamp(page, 0, pageCount - 1);
        StartSnapAnimation(GetNormalizedPosition(currentPage));
    }

    private void SnapToNearestPage()
    {
        if (pageCount <= 1 || GetScrollableWidth() <= 0.5f)
        {
            currentPage = 0;
            SetNormalizedPosition(0f);
            UpdateNavigationButtons();
            return;
        }

        currentPage = GetNearestPage();
        StartSnapAnimation(GetNormalizedPosition(currentPage));
    }

    private int GetNearestPage()
    {
        if (pageCount <= 1 || GetScrollableWidth() <= 0.5f)
            return 0;

        float pageOffset = Mathf.Clamp01(scrollRect.horizontalNormalizedPosition) * GetScrollableWidth();
        return Mathf.Clamp(Mathf.RoundToInt(pageOffset / pageWidth), 0, pageCount - 1);
    }

    private float GetNormalizedPosition(int page)
    {
        float scrollableWidth = GetScrollableWidth();
        if (scrollableWidth <= 0f)
            return 0f;

        return Mathf.Clamp01(page * pageWidth / scrollableWidth);
    }

    private float GetPageWidth()
    {
        if (viewport == null)
            return 1160f;

        float cellWidth = layoutGroup != null ? layoutGroup.cellSize.x : 360f;
        float spacing = layoutGroup != null ? layoutGroup.spacing.x : 40f;
        float padding = layoutGroup != null ? layoutGroup.padding.horizontal : 0f;
        return Mathf.Max(viewport.rect.width, padding + ColumnsPerPage * (cellWidth + spacing));
    }

    private float GetGridHeight()
    {
        float cellHeight = layoutGroup != null ? layoutGroup.cellSize.y : 220f;
        float spacing = layoutGroup != null ? layoutGroup.spacing.y : 40f;
        return (RowsPerPage * cellHeight) + (RowsPerPage - 1) * spacing +
               (layoutGroup != null ? layoutGroup.padding.vertical : 0f);
    }

    private int GetActiveSlotCount()
    {
        if (content == null)
            return 0;

        int count = 0;
        for (int i = 0; i < content.childCount; i++)
        {
            if (content.GetChild(i).gameObject.activeSelf)
                count++;
        }

        return count;
    }

    private bool HasScrollableContent()
    {
        return scrollRect != null && content != null && viewport != null &&
               pageCount > 1 && GetScrollableWidth() > 0.5f;
    }

    private float GetScrollableWidth()
    {
        if (content == null || viewport == null)
            return 0f;

        return Mathf.Max(0f, content.rect.width - viewport.rect.width);
    }

    private void StartSnapAnimation(float targetPosition)
    {
        StopSnapAnimation();
        if (isActiveAndEnabled)
            snapCoroutine = StartCoroutine(AnimateSnap(Mathf.Clamp01(targetPosition)));
        else
            SetNormalizedPosition(targetPosition);
    }

    private IEnumerator AnimateSnap(float targetPosition)
    {
        if (scrollRect == null)
            yield break;

        scrollRect.StopMovement();
        float startPosition = scrollRect.horizontalNormalizedPosition;
        float elapsed = 0f;

        while (elapsed < SnapDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            float progress = Mathf.Clamp01(elapsed / SnapDuration);
            float easedProgress = 1f - Mathf.Pow(1f - progress, 3f);
            SetNormalizedPosition(Mathf.LerpUnclamped(startPosition, targetPosition, easedProgress));
            yield return null;
        }

        SetNormalizedPosition(targetPosition);
        snapCoroutine = null;
        UpdateNavigationButtons();
    }

    private void SetNormalizedPosition(float position)
    {
        if (scrollRect != null)
            scrollRect.horizontalNormalizedPosition = Mathf.Clamp01(position);
    }

    private void QueueSnap()
    {
        lastInputTime = Time.unscaledTime;
        snapPending = true;
        StopSnapAnimation();
    }

    private void OnScrollValueChanged(Vector2 position)
    {
        UpdateNavigationButtons();
    }

    private void UpdateNavigationButtons()
    {
        bool canScroll = HasScrollableContent();
        if (previousButton != null)
            previousButton.interactable = canScroll && currentPage > 0;
        if (nextButton != null)
            nextButton.interactable = canScroll && currentPage < pageCount - 1;
    }

    private void StopSnapAnimation()
    {
        if (snapCoroutine != null)
        {
            StopCoroutine(snapCoroutine);
            snapCoroutine = null;
        }
    }

    private void Unbind()
    {
        if (scrollRect != null)
            scrollRect.onValueChanged.RemoveListener(OnScrollValueChanged);
        if (previousButton != null)
            previousButton.onClick.RemoveListener(ScrollPrevious);
        if (nextButton != null)
            nextButton.onClick.RemoveListener(ScrollNext);
    }

    private void OnDisable()
    {
        if (layoutCoroutine != null)
        {
            StopCoroutine(layoutCoroutine);
            layoutCoroutine = null;
        }

        snapPending = false;
        isDragging = false;
        StopSnapAnimation();
        SetContentVisible(true);
    }

    private void OnDestroy()
    {
        Unbind();
    }
}
