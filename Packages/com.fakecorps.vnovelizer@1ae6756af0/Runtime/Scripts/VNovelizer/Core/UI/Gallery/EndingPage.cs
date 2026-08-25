using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class EndingPage : MonoBehaviour
{
    private GridLayoutGroup endingGridLayout;
    private Transform endingContentTransform;
    [SerializeField] private Button endingPrevPageButton;
    [SerializeField] private Button endingNextPageButton;
    [SerializeField] private TextMeshProUGUI endingPageText;

    private int currentEndingPage = 0;
    private const int ENDING_PER_PAGE = 12;

    private EndingDataContainer endingDataContainer;
    private GlobalData globalData;
    private readonly List<EndingSlot> endingSlots = new List<EndingSlot>();
    private List<VNEnding> allEndingData = new List<VNEnding>();

    private GameObject endingSlotPrefab;
    private GameObject endingSlotDetailPrefab;
    private EndingSlotDetail endingSlotDetail;
    private int detailSpriteLoadVersion;

    private void Awake()
    {
        Transform endingContainer = transform.Find("EndingSlotContainer");
        if (endingContainer == null)
        {
            endingContainer = transform.Find("SceneSlotContainer");
        }

        if (endingContainer != null)
        {
            endingGridLayout = endingContainer.GetComponent<GridLayoutGroup>();
            endingContentTransform = endingContainer;
        }

        Transform pagination = transform.Find("Sc_Pagination");
        if (pagination != null)
        {
            endingPrevPageButton = pagination.Find("ScPrevBtn")?.GetComponent<Button>();
            endingNextPageButton = pagination.Find("ScNextBtn")?.GetComponent<Button>();
            endingPageText = pagination.Find("ScPageText")?.GetComponent<TextMeshProUGUI>();
        }

        if (endingPrevPageButton != null) endingPrevPageButton.onClick.AddListener(OnEndingPrevPage);
        if (endingNextPageButton != null) endingNextPageButton.onClick.AddListener(OnEndingNextPage);

        EventCenter.GetInstance().AddEventListener<string>("EndingUnlocked", OnEndingUnlocked);
    }

    public void Initialize()
    {
        globalData = GlobalDataManager.GetInstance().GetGlobalData();
        LoadEndingDataContainer();
        LoadEndingGallery();
    }

    public void Show()
    {
        gameObject.SetActive(true);
        Initialize();
    }

    public void Hide()
    {
        if (endingSlotDetail != null)
        {
            endingSlotDetail.Hide();
        }

        gameObject.SetActive(false);
        ClearEndingSlots();
    }

    private void OnDestroy()
    {
        EventCenter.GetInstance().RemoveEventListener<string>("EndingUnlocked", OnEndingUnlocked);
    }

    private void LoadEndingDataContainer()
    {
        string path = VNProjectConfig.Instance.Ending_DataPath + "/EndingDataContainer";
        endingDataContainer = ResourcesManager.GetInstance().Load<EndingDataContainer>(path);

        if (endingDataContainer == null)
        {
            Debug.LogWarning($"[EndingPage] EndingDataContainer not found: {path}");
            allEndingData = new List<VNEnding>();
        }
        else
        {
            allEndingData = new List<VNEnding>(endingDataContainer.endingList);
        }
    }

    private void LoadEndingGallery()
    {
        ClearEndingSlots();

        if (endingContentTransform == null)
        {
            Debug.LogError("[EndingPage] Ending content container not found.");
            return;
        }

        if (endingSlotPrefab == null)
        {
            string loadPath = VNProjectConfig.Instance.UI_GalleryPath + "/Ending";
            endingSlotPrefab = ResourcesManager.GetInstance().Load<GameObject>(loadPath + "/EndingSlot");
        }

        if (endingSlotPrefab == null)
        {
            Debug.LogError("[EndingPage] EndingSlot prefab not found.");
            return;
        }

        UpdateEndingPage();
    }

    private void UpdateEndingPage()
    {
        if (endingContentTransform == null || endingSlotPrefab == null) return;

        foreach (Transform child in endingContentTransform)
        {
            Destroy(child.gameObject);
        }
        endingSlots.Clear();

        int totalPages = Mathf.CeilToInt((float)allEndingData.Count / ENDING_PER_PAGE);
        if (totalPages == 0) totalPages = 1;

        if (endingPageText != null)
        {
            endingPageText.text = $"{currentEndingPage + 1}/{totalPages}";
        }

        if (endingPrevPageButton != null)
        {
            endingPrevPageButton.interactable = currentEndingPage > 0;
        }
        if (endingNextPageButton != null)
        {
            endingNextPageButton.interactable = currentEndingPage < totalPages - 1;
        }

        int startIndex = currentEndingPage * ENDING_PER_PAGE;
        int endIndex = Mathf.Min(startIndex + ENDING_PER_PAGE, allEndingData.Count);

        for (int i = startIndex; i < endIndex; i++)
        {
            VNEnding endingData = allEndingData[i];
            if (endingData != null)
            {
                CreateEndingSlot(endingData);
            }
        }
    }

    private void CreateEndingSlot(VNEnding endingData)
    {
        if (endingSlotPrefab == null || endingContentTransform == null) return;

        if (endingData == null)
        {
            Debug.LogWarning("[EndingPage] VNEnding is null, skip slot.");
            return;
        }

        GameObject slotObj = Instantiate(endingSlotPrefab, endingContentTransform);
        EndingSlot slot = slotObj.GetComponent<EndingSlot>();
        if (slot == null)
        {
            slot = slotObj.AddComponent<EndingSlot>();
        }

        bool isUnlocked = false;
        if (globalData != null && globalData.UnlockedEndings != null && !string.IsNullOrEmpty(endingData.EndingID))
        {
            isUnlocked = globalData.UnlockedEndings.Contains(endingData.EndingID);
        }

        if (endingData.isUnLocked && !isUnlocked && !string.IsNullOrEmpty(endingData.EndingID))
        {
            GlobalDataManager.GetInstance().UnlockEnding(endingData.EndingID);
            isUnlocked = true;
        }

        slot.Init(endingData, isUnlocked, OnEndingSlotClick);
        endingSlots.Add(slot);
    }

    private void OnEndingSlotClick(VNEnding endingData, bool isUnlocked)
    {
        if (endingData != null)
        {
            ShowEndingSlotDetail(endingData, isUnlocked);
        }
    }

    private void ShowEndingSlotDetail(VNEnding endingData, bool isUnlocked)
    {
        EnsureEndingSlotDetail();
        if (endingSlotDetail != null)
        {
            Sprite fallbackSprite = EndingImageLoader.GetFallbackSprite(endingData, isUnlocked);
            endingSlotDetail.Show(endingData, isUnlocked, fallbackSprite);

            string spritePath = EndingImageLoader.GetSpritePath(endingData, isUnlocked);
            int version = ++detailSpriteLoadVersion;
            if (!string.IsNullOrEmpty(spritePath))
            {
                StartCoroutine(EndingImageLoader.LoadSpriteAsync(endingData, isUnlocked, sprite =>
                {
                    if (this == null || version != detailSpriteLoadVersion || endingSlotDetail == null)
                    {
                        return;
                    }

                    endingSlotDetail.SetSprite(sprite);
                }));
            }
        }
    }

    private void EnsureEndingSlotDetail()
    {
        if (endingSlotDetail != null)
        {
            return;
        }

        Transform existing = transform.Find("EndingSlotDetail");
        if (existing == null)
        {
            existing = transform.Find("SceneSlotDetail");
        }

        if (existing != null)
        {
            endingSlotDetail = existing.GetComponent<EndingSlotDetail>();
            if (endingSlotDetail == null)
            {
                endingSlotDetail = existing.gameObject.AddComponent<EndingSlotDetail>();
            }
            return;
        }

        if (endingSlotDetailPrefab == null)
        {
            string loadPath = VNProjectConfig.Instance.UI_GalleryPath + "/Ending";
            endingSlotDetailPrefab = ResourcesManager.GetInstance().Load<GameObject>(loadPath + "/EndingSlotDetail");
        }

        if (endingSlotDetailPrefab == null)
        {
            Debug.LogError("[EndingPage] EndingSlotDetail prefab not found.");
            return;
        }

        GameObject detailObj = Instantiate(endingSlotDetailPrefab, transform);
        detailObj.name = "EndingSlotDetail";
        endingSlotDetail = detailObj.GetComponent<EndingSlotDetail>();
        if (endingSlotDetail == null)
        {
            endingSlotDetail = detailObj.AddComponent<EndingSlotDetail>();
        }
    }

    private void OnEndingPrevPage()
    {
        if (currentEndingPage > 0)
        {
            currentEndingPage--;
            UpdateEndingPage();
        }
    }

    private void OnEndingNextPage()
    {
        int totalPages = Mathf.CeilToInt((float)allEndingData.Count / ENDING_PER_PAGE);
        if (totalPages == 0) totalPages = 1;

        if (currentEndingPage < totalPages - 1)
        {
            currentEndingPage++;
            UpdateEndingPage();
        }
    }

    private void ClearEndingSlots()
    {
        if (endingContentTransform != null)
        {
            foreach (Transform child in endingContentTransform)
            {
                Destroy(child.gameObject);
            }
        }
        endingSlots.Clear();
    }

    private void OnEndingUnlocked(string endingID)
    {
        foreach (EndingSlot slot in endingSlots)
        {
            if (slot != null && slot.endingData != null && slot.endingData.EndingID == endingID)
            {
                slot.Unlock();
                break;
            }
        }
    }
}
