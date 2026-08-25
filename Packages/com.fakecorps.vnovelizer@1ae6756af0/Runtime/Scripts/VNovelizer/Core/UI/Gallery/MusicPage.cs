using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;

/// <summary>
/// 音乐厅页面
/// </summary>
public class MusicPage : MonoBehaviour
{
    private const float MusicListBottomScrollPaddingRatio = 0.12f;
    private const float MusicListMinBottomScrollPadding = 80f;

    // 左侧：音乐列表
    private ScrollRect musicListScrollView;
    private Transform musicListContent;
    private GameObject musicSlotPrefab;

    // 右侧：播放控制
                     private Image musicPictureImage; // 音乐封面图片
    [SerializeField] private Sprite defaultImage;
    [SerializeField] private Button prevButton; // 上一首
    [SerializeField] private Button playPauseButton; // 播放/暂停
    private Image playPauseButtonImage;
    [SerializeField] private Sprite PlayImage;
    [SerializeField] private Sprite PauseImage;
    [SerializeField] private Button nextButton; // 下一首
    [SerializeField] private Slider progressSlider; // 播放进度条
    [SerializeField] private Slider volumeSlider; // 音量进度条
    [SerializeField] private TextMeshProUGUI progressText; // 播放进度文本（如：2:45/3:12）
    
    // 数据
    private MusicDataContainer musicDataContainer;
    private GlobalData globalData;
    private List<VNMusic> allMusicData = new List<VNMusic>();
    private List<MusicSlot> musicSlots = new List<MusicSlot>();
    
    // 播放状态
    private AudioSource audioSource;
    private VNMusic currentMusic;
    private int currentMusicIndex = -1;
    private int musicLoadVersion;
    private int pictureLoadVersion;
    private bool isPlaying = false;
    private float currentVolume = 1f;
    private bool isDraggingProgress = false; // 是否正在拖拽进度条
    private bool isUpdatingProgressSlider = false;
    private float playbackUiTime = 0f;
    
    private void Awake()
    {
        // 获取左侧音乐列表控件
        Transform musicListTransform = transform.Find("MusicList");
        if (musicListTransform != null)
        {
            musicListScrollView = musicListTransform.GetComponent<ScrollRect>();
            if (musicListScrollView != null)
            {
                musicListContent = musicListScrollView.content;
                ConfigureMusicListScrollView();
            }
        }
        
        // 获取右侧播放控制控件
        Transform rightPanel = transform.Find("RightPanel");
        if (rightPanel != null)
        {
            // 封面图片
            Transform pictureTransform = rightPanel.Find("MusicCover");
            if (pictureTransform != null)
            {
                musicPictureImage = pictureTransform.GetComponent<Image>();
                if (defaultImage == null && musicPictureImage != null)
                {
                    defaultImage = musicPictureImage.sprite;
                }
            }
            
            // 控制按钮
            Transform controlsTransform = rightPanel.Find("Controls");
            if (controlsTransform != null)
            {
                prevButton = controlsTransform.Find("M_PrevBtn")?.GetComponent<Button>();
                playPauseButton = controlsTransform.Find("PlayPauseBtn")?.GetComponent<Button>();
                nextButton = controlsTransform.Find("M_NextBtn")?.GetComponent<Button>();
                CachePlayPauseButtonImage();
            }
            
            // 进度条
            Transform progressTransform = rightPanel.Find("Progress");
            if (progressTransform != null)
            {
                progressSlider = progressTransform.Find("ProgressSlider")?.GetComponent<Slider>();
                progressText = progressTransform.Find("ProgressText")?.GetComponent<TextMeshProUGUI>();
            }
            
            // 音量控制
            Transform volumeTransform = rightPanel.Find("Volume");
            if (volumeTransform != null)
            {
                volumeSlider = volumeTransform.Find("VolumeSlider")?.GetComponent<Slider>();
                if (volumeSlider == null)
                {
                    // 尝试直接在Volume对象上获取Slider组件
                    volumeSlider = volumeTransform.GetComponent<Slider>();
                }
            }
        }
        
        // 创建AudioSource用于播放音乐
        audioSource = gameObject.AddComponent<AudioSource>();
        audioSource.loop = true; // 循环播放
        audioSource.playOnAwake = false;
        audioSource.volume = currentVolume; // 初始化音量
        
        // 绑定事件
        if (prevButton != null) prevButton.onClick.AddListener(OnPrevButtonClick);
        if (playPauseButton != null) playPauseButton.onClick.AddListener(OnPlayPauseButtonClick);
        if (nextButton != null) nextButton.onClick.AddListener(OnNextButtonClick);
        
        if (progressSlider != null)
        {
            progressSlider.onValueChanged.AddListener(OnProgressSliderChanged);
            
            // 添加拖拽开始和结束事件
            EventTrigger trigger = progressSlider.gameObject.GetComponent<EventTrigger>();
            if (trigger == null)
            {
                trigger = progressSlider.gameObject.AddComponent<EventTrigger>();
            }
            
            // 拖拽开始
            EventTrigger.Entry pointerDown = new EventTrigger.Entry();
            pointerDown.eventID = EventTriggerType.PointerDown;
            pointerDown.callback.AddListener((data) => { isDraggingProgress = true; });
            trigger.triggers.Add(pointerDown);
            
            // 拖拽结束
            EventTrigger.Entry pointerUp = new EventTrigger.Entry();
            pointerUp.eventID = EventTriggerType.PointerUp;
            pointerUp.callback.AddListener((data) => { isDraggingProgress = false; });
            trigger.triggers.Add(pointerUp);
        }
        
        if (volumeSlider != null)
        {
            // 先设置初始值，再绑定事件（避免触发事件）
            volumeSlider.value = currentVolume;
            volumeSlider.onValueChanged.AddListener(OnVolumeSliderChanged);
        }
        
        // 监听音乐解锁事件
        EventCenter.GetInstance().AddEventListener<string>("MusicUnlocked", OnMusicUnlocked);
        UpdatePlayPauseButton();
    }
    
    /// <summary>
    /// 初始化音乐页面
    /// </summary>
    public void Initialize()
    {
        // 加载全局数据
        globalData = GlobalDataManager.GetInstance().GetGlobalData();
        
        // 加载音乐数据容器
        LoadMusicDataContainer();
        
        // 加载音乐列表
        LoadMusicList();
        
        // 确保音量滑块和AudioSource同步
        if (volumeSlider != null && audioSource != null)
        {
            // 如果VolumeSlider的值不是currentVolume，同步它
            if (Mathf.Abs(volumeSlider.value - currentVolume) > 0.01f)
            {
                currentVolume = volumeSlider.value;
                audioSource.volume = currentVolume;
            }
            else
            {
                // 否则，用currentVolume更新VolumeSlider
                volumeSlider.value = currentVolume;
            }
        }
    }
    
    /// <summary>
    /// 显示音乐页面
    /// </summary>
    public void Show()
    {
        gameObject.SetActive(true);
        Initialize();
    }
    
    /// <summary>
    /// 隐藏音乐页面
    /// </summary>
    public void Hide()
    {
        gameObject.SetActive(false);
        StopMusic();
        ClearMusicList();
    }
    
    private void Update()
    {
        // 更新播放进度（只有在未拖拽时才更新）
        if (isPlaying && audioSource != null && audioSource.clip != null)
        {
            AdvancePlaybackUiTime();
            if (!isDraggingProgress)
            {
                UpdateProgress();
            }
        }
    }
    
    private void OnDestroy()
    {
        // 移除事件监听
        EventCenter.GetInstance().RemoveEventListener<string>("MusicUnlocked", OnMusicUnlocked);
        
        // 停止播放
        StopMusic();
    }
    
    /// <summary>
    /// 加载音乐数据容器
    /// </summary>
    private void LoadMusicDataContainer()
    {
        string path = VNProjectConfig.Instance.Music_DataPath + "/MusicDataContainer";
        musicDataContainer = ResourcesManager.GetInstance().Load<MusicDataContainer>(path);
        
        if (musicDataContainer == null)
        {
            Debug.LogWarning($"[MusicPage] 未找到音乐数据容器: {path}");
            allMusicData = new List<VNMusic>();
        }
        else
        {
            allMusicData = new List<VNMusic>(musicDataContainer.musicList);
        }
    }
    
    /// <summary>
    /// 加载音乐列表
    /// </summary>
    private void LoadMusicList()
    {
        ClearMusicList();
        
        if (musicListContent == null)
        {
            Debug.LogError("[MusicPage] 音乐列表内容容器未找到");
            return;
        }
        
        // 加载音乐槽位预制体
        if (musicSlotPrefab == null)
        {
            string loadPath = VNProjectConfig.Instance.UI_GalleryPath + "/Music";
            musicSlotPrefab = ResourcesManager.GetInstance().Load<GameObject>(loadPath + "/MusicSlot");
        }
        
        if (musicSlotPrefab == null)
        {
            Debug.LogError("[MusicPage] 音乐槽位预制体未找到");
            return;
        }
        
        // 创建音乐槽位
        for (int i = 0; i < allMusicData.Count; i++)
        {
            VNMusic music = allMusicData[i];
            if (music != null)
            {
                CreateMusicSlot(music, i);
            }
        }

        RefreshMusicListLayout();
        StartCoroutine(RefreshMusicListLayoutNextFrame());
    }

    private void ConfigureMusicListScrollView()
    {
        if (musicListScrollView == null)
        {
            return;
        }

        musicListScrollView.horizontal = false;
        musicListScrollView.vertical = true;
        musicListScrollView.movementType = ScrollRect.MovementType.Elastic;

        RectTransform viewport = musicListScrollView.viewport;
        if (viewport != null)
        {
            viewport.anchorMin = Vector2.zero;
            viewport.anchorMax = Vector2.one;
            viewport.offsetMin = Vector2.zero;
            viewport.offsetMax = Vector2.zero;
        }

        RectTransform contentRect = musicListScrollView.content;
        if (contentRect != null)
        {
            contentRect.anchorMin = new Vector2(0f, 1f);
            contentRect.anchorMax = new Vector2(1f, 1f);
            contentRect.pivot = new Vector2(0.5f, 1f);
            contentRect.anchoredPosition = Vector2.zero;
        }
    }

    private void RefreshMusicListLayout()
    {
        if (musicListScrollView == null || musicListContent == null)
        {
            return;
        }

        RectTransform contentRect = musicListContent as RectTransform;
        if (contentRect == null)
        {
            return;
        }

        Canvas.ForceUpdateCanvases();
        LayoutRebuilder.ForceRebuildLayoutImmediate(contentRect);

        RectTransform viewport = musicListScrollView.viewport;
        float contentHeight = CalculateMusicListContentHeight(contentRect, viewport);
        if (viewport != null)
        {
            contentHeight = Mathf.Max(contentHeight, viewport.rect.height);
        }

        contentRect.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, contentHeight);
        contentRect.anchoredPosition = Vector2.zero;
        LayoutRebuilder.ForceRebuildLayoutImmediate(contentRect);
        musicListScrollView.StopMovement();
        musicListScrollView.verticalNormalizedPosition = 1f;
        musicListScrollView.SetLayoutVertical();
        musicListScrollView.Rebuild(CanvasUpdate.PostLayout);
    }

    private IEnumerator RefreshMusicListLayoutNextFrame()
    {
        yield return null;
        RefreshMusicListLayout();
    }

    private float CalculateMusicListContentHeight(RectTransform contentRect, RectTransform viewport)
    {
        VerticalLayoutGroup layoutGroup = contentRect.GetComponent<VerticalLayoutGroup>();
        float height = 0f;
        float spacing = 0f;
        int childCount = musicSlots.Count;

        if (layoutGroup != null)
        {
            height += layoutGroup.padding.top + layoutGroup.padding.bottom;
            spacing = layoutGroup.spacing;
        }

        height += GetMusicSlotHeight() * childCount;

        if (childCount > 1)
        {
            height += spacing * (childCount - 1);
        }

        if (childCount > 0)
        {
            float viewportHeight = viewport != null ? viewport.rect.height : 0f;
            height += Mathf.Max(MusicListMinBottomScrollPadding, viewportHeight * MusicListBottomScrollPaddingRatio);
        }

        return height;
    }

    private float GetMusicSlotHeight()
    {
        RectTransform prefabRect = musicSlotPrefab != null ? musicSlotPrefab.GetComponent<RectTransform>() : null;
        if (prefabRect != null && prefabRect.rect.height > 0f)
        {
            return prefabRect.rect.height;
        }
        if (prefabRect != null && prefabRect.sizeDelta.y > 0f)
        {
            return prefabRect.sizeDelta.y;
        }

        foreach (MusicSlot slot in musicSlots)
        {
            if (slot == null)
            {
                continue;
            }

            RectTransform slotRect = slot.GetComponent<RectTransform>();
            if (slotRect != null && slotRect.rect.height > 0f)
            {
                return slotRect.rect.height;
            }
            if (slotRect != null && slotRect.sizeDelta.y > 0f)
            {
                return slotRect.sizeDelta.y;
            }
        }

        return 50f;
    }
    
    /// <summary>
    /// 创建音乐槽位
    /// </summary>
    private void CreateMusicSlot(VNMusic music, int index)
    {
        if (musicSlotPrefab == null || musicListContent == null) return;
        
        if (music == null)
        {
            Debug.LogWarning("[MusicPage] VNMusic为null，跳过创建槽位");
            return;
        }
        
        GameObject slotObj = Instantiate(musicSlotPrefab, musicListContent);
        
        // 确保RectTransform设置正确（用于布局）
        RectTransform slotRect = slotObj.GetComponent<RectTransform>();
        if (slotRect != null)
        {
            // 重置变换属性
            slotRect.localScale = Vector3.one;
            slotRect.localRotation = Quaternion.identity;
            
            LayoutGroup layoutGroup = musicListContent.GetComponent<LayoutGroup>();
            if (layoutGroup == null)
            {
                // 没有布局组件，需要手动设置位置
                slotRect.anchoredPosition = new Vector2(0, -index * 50); // 假设每个slot高度为50
            }
            else
            {
                LayoutRebuilder.MarkLayoutForRebuild(musicListContent as RectTransform);
            }
        }
        
        MusicSlot slot = slotObj.GetComponent<MusicSlot>();
        if (slot == null)
        {
            slot = slotObj.AddComponent<MusicSlot>();
        }
        
        // 检查是否已解锁
        bool isUnlocked = false;
        if (globalData != null && globalData.UnlockedMusic != null && !string.IsNullOrEmpty(music.name))
        {
            isUnlocked = globalData.UnlockedMusic.Contains(music.name);
        }
        
        // 同步编辑器中的调试设置
        if (music.isUnlocked && !isUnlocked && globalData != null && globalData.UnlockedMusic != null)
        {
            if (!string.IsNullOrEmpty(music.name))
            {
                globalData.UnlockedMusic.Add(music.name);
                isUnlocked = true;
                GlobalDataManager.GetInstance().UnlockMusic(music.name); // 这会保存到文件
                Debug.Log($"[MusicPage] 同步音乐解锁状态: {music.name}");
            }
        }
        
        // 初始化音乐槽位
        slot.Init(music, isUnlocked, OnMusicSlotClick);
        
        musicSlots.Add(slot);
    }
    
    /// <summary>
    /// 音乐槽位点击事件
    /// </summary>
    private void OnMusicSlotClick(VNMusic music)
    {
        if (!MusicAssetLoader.HasPlayableClip(music))
        {
            Debug.LogWarning("[MusicPage] Music data or audio clip is null.");
            return;
        }

        int index = allMusicData.IndexOf(music);
        if (index >= 0)
        {
            PlayMusic(index);
        }
    }

    private void PlayMusic(int index)
    {
        if (index < 0 || index >= allMusicData.Count) return;

        currentMusic = allMusicData[index];
        currentMusicIndex = index;

        if (!MusicAssetLoader.HasPlayableClip(currentMusic))
        {
            Debug.LogWarning("[MusicPage] Music data or audio clip is null.");
            return;
        }

        StopMusic();
        UpdateMusicPicture();
        UpdatePlayPauseButton();
        UpdateMusicSlotSelection();

        int version = ++musicLoadVersion;
        StartCoroutine(MusicAssetLoader.LoadAudioClipAsync(currentMusic, clip =>
        {
            if (this == null || version != musicLoadVersion || currentMusic != allMusicData[index])
            {
                return;
            }

            if (clip == null)
            {
                Debug.LogWarning("[MusicPage] Failed to load music clip.");
                return;
            }

            audioSource.clip = clip;
            StartCurrentClipPlayback();
        }));
    }

    private void StartCurrentClipPlayback()
    {
        if (audioSource == null || audioSource.clip == null)
        {
            return;
        }

        audioSource.volume = currentVolume;
        if (playbackUiTime >= audioSource.clip.length)
        {
            playbackUiTime = 0f;
        }

        try
        {
            audioSource.time = playbackUiTime;
        }
        catch (System.Exception e)
        {
            Debug.LogWarning($"[MusicPage] Failed to set audio time before playback: {e.Message}");
        }

        audioSource.Play();
        isPlaying = true;
        UpdatePlayPauseButton();
        UpdateProgress();
    }

    private bool IsMusicPlayable(VNMusic music)
    {
        if (!MusicAssetLoader.HasPlayableClip(music)) return false;
        if (music.isUnlocked) return true;

        return globalData != null &&
               globalData.UnlockedMusic != null &&
               !string.IsNullOrEmpty(music.name) &&
               globalData.UnlockedMusic.Contains(music.name);
    }
    private int FindPlayableMusicIndex(int startIndex, int direction)
    {
        if (allMusicData.Count == 0) return -1;

        int step = direction >= 0 ? 1 : -1;
        int index = ((startIndex % allMusicData.Count) + allMusicData.Count) % allMusicData.Count;

        for (int i = 0; i < allMusicData.Count; i++)
        {
            if (IsMusicPlayable(allMusicData[index]))
            {
                return index;
            }

            index = (index + step + allMusicData.Count) % allMusicData.Count;
        }

        return -1;
    }
    
    /// <summary>
    /// 停止播放
    /// </summary>
    private void StopMusic()
    {
        musicLoadVersion++;
        if (audioSource != null && audioSource.isPlaying)
        {
            audioSource.Stop();
        }
        playbackUiTime = 0f;
        isPlaying = false;
        UpdateProgress();
        UpdatePlayPauseButton();
    }
    
    /// <summary>
    /// 上一首
    /// </summary>
    private void OnPrevButtonClick()
    {
        if (allMusicData.Count == 0) return;

        int startIndex = currentMusicIndex < 0 ? allMusicData.Count - 1 : currentMusicIndex - 1;
        int playableIndex = FindPlayableMusicIndex(startIndex, -1);
        if (playableIndex >= 0)
        {
            PlayMusic(playableIndex);
        }
    }
    
    /// <summary>
    /// 播放/暂停
    /// </summary>
    private void OnPlayPauseButtonClick()
    {
        if (!MusicAssetLoader.HasPlayableClip(currentMusic))
        {
            // 如果没有选中音乐，播放第一首
            if (allMusicData.Count > 0)
            {
                int playableIndex = FindPlayableMusicIndex(0, 1);
                if (playableIndex >= 0)
                {
                    PlayMusic(playableIndex);
                }
            }
            return;
        }
        
        if (isPlaying && audioSource.isPlaying)
        {

            audioSource.Pause();
           
            isPlaying = false;
        }
        else
        {
            // 播放
            if (audioSource.clip == null)
            {
                int playableIndex = FindPlayableMusicIndex(currentMusicIndex, 1);
                if (playableIndex >= 0)
                {
                    PlayMusic(playableIndex);
                }
            }
            else
            {
                if (playbackUiTime <= 0.01f || playbackUiTime >= audioSource.clip.length)
                {
                    StartCurrentClipPlayback();
                    return;
                }

                audioSource.UnPause();
                isPlaying = true;
            }
        }
        
        UpdatePlayPauseButton();
    }
    
    /// <summary>
    /// 下一首
    /// </summary>
    private void OnNextButtonClick()
    {
        if (allMusicData.Count == 0) return;

        int startIndex = currentMusicIndex < 0 ? 0 : currentMusicIndex + 1;
        int playableIndex = FindPlayableMusicIndex(startIndex, 1);
        if (playableIndex >= 0)
        {
            PlayMusic(playableIndex);
        }
    }
    
    /// <summary>
    /// 进度条值改变
    /// </summary>
    private void OnProgressSliderChanged(float value)
    {
        if (isUpdatingProgressSlider)
        {
            return;
        }

        if (audioSource != null && audioSource.clip != null)
        {
            // 无论是拖拽还是点击，都更新播放位置
            playbackUiTime = SliderValueToProgress(value) * audioSource.clip.length;
            try
            {
                audioSource.time = playbackUiTime;
            }
            catch (System.Exception e)
            {
                Debug.LogWarning($"[MusicPage] Failed to seek audio: {e.Message}");
            }
            UpdateProgressText();
        }
    }
    
    /// <summary>
    /// 音量条值改变
    /// </summary>
    private void OnVolumeSliderChanged(float value)
    {
        currentVolume = Mathf.Clamp01(value); // 确保值在0-1范围内
        if (audioSource != null)
        {
            audioSource.volume = currentVolume;
        }
    }
    
    /// <summary>
    /// 更新音乐封面图片
    /// </summary>
    private void UpdateMusicPicture()
    {
        if (musicPictureImage != null && currentMusic != null)
        {
            musicPictureImage.sprite = MusicAssetLoader.GetFallbackPicture(currentMusic, defaultImage);
            musicPictureImage.color = Color.white;

            string picturePath = MusicAssetLoader.GetPicturePath(currentMusic);
            int version = ++pictureLoadVersion;
            if (!string.IsNullOrEmpty(picturePath))
            {
                StartCoroutine(MusicAssetLoader.LoadPictureAsync(currentMusic, defaultImage, sprite =>
                {
                    if (this == null || version != pictureLoadVersion || musicPictureImage == null)
                    {
                        return;
                    }

                    musicPictureImage.sprite = sprite;
                    musicPictureImage.color = Color.white;
                }));
            }
        }
    }
    
    /// <summary>
    /// 更新播放/暂停按钮
    /// </summary>
    private void CachePlayPauseButtonImage()
    {
        if (playPauseButton == null)
        {
            playPauseButtonImage = null;
            return;
        }

        playPauseButtonImage = playPauseButton.targetGraphic as Image;
        if (playPauseButtonImage == null)
        {
            playPauseButtonImage = playPauseButton.image;
        }
    }

    private void UpdatePlayPauseButton()
    {
        if (playPauseButton == null) return;
        if (playPauseButtonImage == null)
        {
            CachePlayPauseButtonImage();
        }

        if (playPauseButtonImage == null) return;

        if (isPlaying)
        {
            if (PauseImage != null)
            {
                playPauseButtonImage.sprite = PauseImage;
            }
        }
        else
        {
            if (PlayImage != null)
            {
                playPauseButtonImage.sprite = PlayImage;
            }
        }
        // 可以在这里更新按钮的图标或文本
        // 例如：TextMeshProUGUI buttonText = playPauseButton.GetComponentInChildren<TextMeshProUGUI>();
        // if (buttonText != null) buttonText.text = isPlaying ? "暂停" : "播放";
    }
    
    /// <summary>
    /// 更新播放进度
    /// </summary>
    private void UpdateProgress()
    {
        if (audioSource == null || audioSource.clip == null) return;
        
        // 更新进度条
        if (progressSlider != null)
        {
            float progress = Mathf.Clamp01(GetCurrentPlaybackTime() / audioSource.clip.length);
            isUpdatingProgressSlider = true;
            progressSlider.SetValueWithoutNotify(ProgressToSliderValue(progress));
            isUpdatingProgressSlider = false;
        }
        
        // 更新进度文本
        UpdateProgressText();
    }
    
    /// <summary>
    /// 更新进度文本
    /// </summary>
    private void UpdateProgressText()
    {
        if (progressText == null || audioSource == null || audioSource.clip == null) return;
        
        int currentSeconds = Mathf.FloorToInt(GetCurrentPlaybackTime());
        int totalSeconds = Mathf.FloorToInt(audioSource.clip.length);
        
        string currentTime = FormatTime(currentSeconds);
        string totalTime = FormatTime(totalSeconds);
        
        progressText.text = $"{currentTime}/{totalTime}";
    }

    private void AdvancePlaybackUiTime()
    {
        if (audioSource == null || audioSource.clip == null)
        {
            playbackUiTime = 0f;
            return;
        }

        float clipLength = audioSource.clip.length;
        if (clipLength <= 0f)
        {
            playbackUiTime = 0f;
            return;
        }

        playbackUiTime += Time.unscaledDeltaTime;
        if (audioSource.loop)
        {
            playbackUiTime %= clipLength;
        }
        else
        {
            playbackUiTime = Mathf.Min(playbackUiTime, clipLength);
        }
    }

    private float GetCurrentPlaybackTime()
    {
        if (audioSource == null || audioSource.clip == null)
        {
            return 0f;
        }

        return Mathf.Clamp(playbackUiTime, 0f, audioSource.clip.length);
    }

    private float SliderValueToProgress(float value)
    {
        if (progressSlider == null)
        {
            return Mathf.Clamp01(value);
        }

        float range = progressSlider.maxValue - progressSlider.minValue;
        if (Mathf.Approximately(range, 0f))
        {
            return 0f;
        }

        return Mathf.Clamp01((value - progressSlider.minValue) / range);
    }

    private float ProgressToSliderValue(float progress)
    {
        if (progressSlider == null)
        {
            return Mathf.Clamp01(progress);
        }

        return Mathf.Lerp(progressSlider.minValue, progressSlider.maxValue, Mathf.Clamp01(progress));
    }
    
    /// <summary>
    /// 格式化时间（秒转分:秒）
    /// </summary>
    private string FormatTime(int seconds)
    {
        int minutes = seconds / 60;
        int secs = seconds % 60;
        return $"{minutes}:{secs:D2}";
    }
    
    /// <summary>
    /// 清理音乐列表
    /// </summary>
    private void ClearMusicList()
    {
        if (musicListContent != null)
        {
            foreach (Transform child in musicListContent)
            {
                Destroy(child.gameObject);
            }
        }
        musicSlots.Clear();
    }

    private void UpdateMusicSlotSelection()
    {
        for (int i = 0; i < musicSlots.Count; i++)
        {
            MusicSlot slot = musicSlots[i];
            if (slot != null)
            {
                slot.SetSelected(i == currentMusicIndex);
            }
        }
    }
    
    /// <summary>
    /// 音乐解锁事件处理
    /// </summary>
    private void OnMusicUnlocked(string musicName)
    {
        // 更新音乐槽位状态
        foreach (MusicSlot slot in musicSlots)
        {
            if (slot != null && slot.musicData != null && slot.musicData.name == musicName)
            {
                slot.Unlock();
                break;
            }
        }
    }
}

