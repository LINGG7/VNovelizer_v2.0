using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

public class MusicManager : BaseManager<MusicManager>
{
    // BGM 组件（BGM 全局唯一，不需要池子）
    private AudioSource BGM = null;
    private float BGMVolume = 1f;
    private string currentPlayingBGM = null; // 【新增】记录当前正在播放的 BGM 名称
    private string pendingBGM = null;
    private Coroutine fadeCoroutine = null;
    private int bgmRequestVersion = 0;
    private bool bgmPaused = false;
    private float fadeVolumeFactor = 1f;
    private const float BGMFadeDuration = 0.6f;

    // SFX 列表（用于在 Update 里检测播放是否结束）
    private List<AudioSource> SFXList = new List<AudioSource>();
    private float SFXVolume = 1f;

    public MusicManager()
    {
        MonoManager.GetInstance().AddUpdateListener(Update);
    }

    private void Update()
    {
        // 每帧检测音效是否播放完毕
        CheckSFXEnd();
    }

    #region 背景音乐 BGM 
    public void ChangeBGMVolume(float volume)
    {
        BGMVolume = volume;
        if (BGM == null) return;
        BGM.volume = BGMVolume * fadeVolumeFactor;
    }

    public void PlayBGM(string name)
    {
        // 先规范化名称，防止空白字符串导致加载目录本身
        if (!string.IsNullOrEmpty(name))
        {
            name = name.Trim();
        }

        if (string.IsNullOrEmpty(name))
        {
            Debug.LogWarning("[MusicManager] PlayBGM 被调用，但传入的 BGM 名称为空或仅包含空白，已跳过播放。");
            return;
        }

        // 【修复】如果新 BGM 和当前正在播放的 BGM 相同，则跳过播放，避免重复播放导致不连贯
        Debug.Log($"[MusicManager][BGM] PlayBGM requested. name='{name}', currentPlayingBGM='{currentPlayingBGM}', hasSource={BGM != null}, isPlaying={(BGM != null && BGM.isPlaying)}, hasClip={(BGM != null && BGM.clip != null)}");

        if (!string.IsNullOrEmpty(name) && name == currentPlayingBGM && BGM != null && BGM.clip != null)
        {
            if (bgmPaused)
            {
                bgmPaused = false;
                BGM.UnPause();
            }
            Debug.Log($"[MusicManager] BGM {name} 已在播放，跳过重复播放");
            return;
        }

        CancelFadeAndRestoreVolume();
        int requestVersion = ++bgmRequestVersion;
        pendingBGM = name;
        if (BGM == null)
        {
            GameObject obj = new GameObject("BGM_Player");
            BGM = obj.AddComponent<AudioSource>();
        }
        string loadPath = VNProjectConfig.Instance.BgmResPath;
        Debug.Log($"[MusicManager][BGM] Loading AudioClip. path='{loadPath}/{name}'");
        ResourcesManager.GetInstance().LoadAsync<AudioClip>(loadPath +"/" + name, (clip) =>
        {
            if (requestVersion != bgmRequestVersion || pendingBGM != name)
            {
                return;
            }

            if (clip == null)
            {
                Debug.LogError($"[MusicManager][BGM] AudioClip load returned null. path='{loadPath}/{name}', name='{name}'");
                pendingBGM = null;
                return;
            }

            pendingBGM = null;
            currentPlayingBGM = name;
            StartBGMTransition(clip, requestVersion);
        });
    }

    public void PauseBGM()
    {
        bgmPaused = true;
        if (BGM != null) BGM.Pause();
        // 注意：暂停时不清空 currentPlayingBGM，因为 resume 时需要知道播放什么
    }

    public void StopBGM()
    {
        ++bgmRequestVersion;
        pendingBGM = null;
        currentPlayingBGM = null; // 【新增】停止时清空当前播放的 BGM
        bgmPaused = false;

        if (BGM == null || BGM.clip == null)
        {
            if (BGM != null) BGM.Stop();
            fadeVolumeFactor = 1f;
            return;
        }

        StartFadeOutAndStop(bgmRequestVersion);
    }

    public string GetCurrentBGMName()
    {
        return currentPlayingBGM;
    }

    private void CancelFadeAndRestoreVolume()
    {
        if (fadeCoroutine != null)
        {
            MonoManager.GetInstance().StopCoroutine(fadeCoroutine);
            fadeCoroutine = null;
        }

        if (BGM != null && BGM.clip != null)
        {
            fadeVolumeFactor = 1f;
            BGM.volume = BGMVolume;
        }
    }

    private void StartBGMTransition(AudioClip clip, int requestVersion)
    {
        if (fadeCoroutine != null)
        {
            MonoManager.GetInstance().StopCoroutine(fadeCoroutine);
            fadeCoroutine = null;
        }

        bool hasCurrentClip = BGM != null && BGM.clip != null;
        if (!hasCurrentClip)
        {
            BGM.clip = clip;
            BGM.volume = 0f;
            BGM.loop = true;
            BGM.Play();
            fadeCoroutine = MonoManager.GetInstance().StartCoroutine(FadeIn(requestVersion));
            return;
        }

        fadeCoroutine = MonoManager.GetInstance().StartCoroutine(FadeOutReplaceAndFadeIn(clip, requestVersion));
    }

    private IEnumerator FadeOutReplaceAndFadeIn(AudioClip nextClip, int requestVersion)
    {
        float startFactor = BGMVolume > 0.0001f ? Mathf.Clamp01(BGM.volume / BGMVolume) : 0f;
        yield return FadeVolume(startFactor, 0f, requestVersion);
        if (requestVersion != bgmRequestVersion || BGM == null)
        {
            yield break;
        }

        BGM.Stop();
        BGM.clip = nextClip;
        BGM.loop = true;
        BGM.volume = 0f;
        fadeVolumeFactor = 0f;
        BGM.Play();
        yield return FadeVolume(0f, 1f, requestVersion);
        if (requestVersion == bgmRequestVersion)
        {
            fadeVolumeFactor = 1f;
        }
    }

    private IEnumerator FadeIn(int requestVersion)
    {
        yield return FadeVolume(0f, 1f, requestVersion);
        if (requestVersion == bgmRequestVersion)
        {
            fadeVolumeFactor = 1f;
        }
    }

    private IEnumerator FadeVolume(float from, float to, int requestVersion)
    {
        float elapsed = 0f;
        while (elapsed < BGMFadeDuration)
        {
            if (requestVersion != bgmRequestVersion)
            {
                yield break;
            }

            if (BGM == null)
            {
                yield break;
            }

            if (!bgmPaused)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / BGMFadeDuration);
                fadeVolumeFactor = Mathf.Lerp(from, to, t);
                BGM.volume = BGMVolume * fadeVolumeFactor;
            }
            yield return null;
        }

        if (BGM == null)
        {
            yield break;
        }

        fadeVolumeFactor = to;
        BGM.volume = BGMVolume * fadeVolumeFactor;
    }

    private void StartFadeOutAndStop(int requestVersion)
    {
        if (fadeCoroutine != null)
        {
            MonoManager.GetInstance().StopCoroutine(fadeCoroutine);
            fadeCoroutine = null;
        }
        fadeCoroutine = MonoManager.GetInstance().StartCoroutine(FadeOutAndStop(requestVersion));
    }

    private IEnumerator FadeOutAndStop(int requestVersion)
    {
        float startFactor = BGMVolume > 0.0001f ? Mathf.Clamp01(BGM.volume / BGMVolume) : 0f;
        yield return FadeVolume(startFactor, 0f, requestVersion);
        if (requestVersion == bgmRequestVersion && BGM != null)
        {
            BGM.Stop();
            BGM.clip = null;
            fadeVolumeFactor = 1f;
        }
    }
    #endregion

    #region 音效 SFX 

    public void ChangeSFXVolume(float volume)
    {
        SFXVolume = volume;
        for (int i = 0; i < SFXList.Count; ++i)
        {
            SFXList[i].volume = SFXVolume;
        }
    }

    // 播放音效
    public void PlaySFX(string name, bool isLoop, UnityAction<AudioSource> callBack = null)
    {
        string loadPath = VNProjectConfig.Instance.SFXResPath;
        ResourcesManager.GetInstance().LoadAsync<AudioClip>(loadPath +"/" + name, (clip) =>
        {
            PoolManager.GetInstance().GetObj("VNovelizerRes/VNPrefabs/Gameplay/SoundObj", (obj) =>
            {
                AudioSource source = obj.GetComponent<AudioSource>();

                if (source == null) source = obj.AddComponent<AudioSource>();

                source.clip = clip;
                source.volume = SFXVolume;
                source.loop = isLoop;
                source.Play();

                SFXList.Add(source);

                if (callBack != null)
                {
                    callBack(source);
                }
            });
        });
    }

    // 停止并回收音效
    public void StopSFX(AudioSource source)
    {
        if (source == null)
        {
            Debug.LogWarning("[MusicManager] StopSFX: source 为 null");
            return;
        }

        if (SFXList.Contains(source))
        {
            SFXList.Remove(source);
            try
            {
                source.Stop();
                source.clip = null;

               
                GameObject sourceObj = null;
                try
                {
                    sourceObj = source.gameObject;
                }
                catch (MissingReferenceException)
                {
                    Debug.LogWarning("[MusicManager] StopSFX: 音效对象已被销毁");
                    return;
                }

                if (sourceObj != null)
                {
                    PoolManager.GetInstance().PushObj("Music/SoundObj", sourceObj);
                }
            }
            catch (MissingReferenceException)
            {
                Debug.LogWarning("[MusicManager] StopSFX: 音效对象在操作过程中被销毁");
            }
        }
    }

    // 检测音效是否结束
    private void CheckSFXEnd()
    {
        for (int i = SFXList.Count - 1; i >= 0; --i)
        {
            AudioSource source = SFXList[i];
            
            //检查 AudioSource 和 GameObject 是否已被销毁
            if (source == null)
            {
                SFXList.RemoveAt(i);
                continue;
            }

            //使用 try-catch 来安全地检查 GameObject 是否有效
            GameObject sourceObj = null;
            try
            {
                sourceObj = source.gameObject;
                if (sourceObj == null)
                {
                    SFXList.RemoveAt(i);
                    continue;
                }
            }
            catch (MissingReferenceException)
            {
                // 对象已被销毁
                SFXList.RemoveAt(i);
                continue;
            }

            //检查 AudioSource 是否仍在播放
            bool isPlaying = false;
            try
            {
                isPlaying = source.isPlaying;
            }
            catch (MissingReferenceException)
            {
                // 对象已被销毁
                SFXList.RemoveAt(i);
                continue;
            }

            if (!isPlaying)
            {
                // 停止并回收
                try
                {
                    source.Stop();
                    source.clip = null; // 清理引用，防止内存泄漏

                    // 再次验证对象是否有效，防止已销毁的对象被推回对象池
                    if (source != null && sourceObj != null)
                    {
                        // 还给对象池（PushObj 内部会进行安全检查）
                        PoolManager.GetInstance().PushObj("Music/SoundObj", sourceObj);
                    }
                }
                catch (MissingReferenceException)
                {
                    // 对象在操作过程中被销毁，直接移除即可
                    Debug.LogWarning("[MusicManager] 音效对象在回收过程中被销毁");
                }

                // 从列表中移除
                SFXList.RemoveAt(i);
            }
        }
    }
    
    /// <summary>
    /// 清理所有音效（用于场景切换时）
    /// </summary>
    public void ClearAllSFX()
    {
        for (int i = SFXList.Count - 1; i >= 0; --i)
        {
            if (SFXList[i] != null && SFXList[i].gameObject != null)
            {
                SFXList[i].Stop();
                SFXList[i].clip = null;
                // 不推回对象池，因为场景切换时对象可能已被销毁
            }
        }
        SFXList.Clear();
    }
    #endregion
}
