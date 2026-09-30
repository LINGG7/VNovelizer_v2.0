using System.Collections;
using UnityEngine;

namespace VNovelizer.Core.Commands
{
    /// <summary>
    /// 播放音效命令
    /// 格式：playsfx(名称, 次数)
    /// 示例1：playsfx(click, 1) -> 播放一次点击音效
    /// 示例2：playsfx(click, 3) -> 播放3次点击音效
    /// 示例3：playsfx(click) -> 播放一次（默认）
    /// </summary>
    public class PlaySFXCommand : VNCommand
    {
        private bool interrupted;
        private MusicManager.SFXPlayback activePlayback;

        public override void Interrupt()
        {
            interrupted = true;
            MusicManager.GetInstance().CancelSFX(activePlayback);
        }

        public override string CommandName { get { return "playsfx"; } }

        public override bool Execute(string args)
        {
            // 音效播放是异步的，需要协程支持
            MonoManager.GetInstance().StartCoroutine(ExecuteAsync(args));
            return true;
        }

        public override IEnumerator ExecuteAsync(string args)
        {
            interrupted = false;
            if (string.IsNullOrWhiteSpace(args))
            {
                Debug.LogError("[PlaySFX] 参数不能为空");
                yield break;
            }

            // 解析参数：名称, 次数
            string[] parts = args.Split(',');
            string sfxName = parts[0].Trim();
            if (string.IsNullOrEmpty(sfxName)) yield break;
            int times = 1; // 默认播放1次

            if (parts.Length >= 2)
            {
                if (!int.TryParse(parts[1].Trim(), out times))
                {
                    Debug.LogWarning($"[PlaySFX] 次数参数解析失败，使用默认值1。参数: {parts[1]}");
                    times = 1;
                }
            }

            if (times <= 0)
            {
                Debug.LogWarning($"[PlaySFX] 播放次数必须大于0，当前值: {times}");
                yield break;
            }

            Debug.Log($"[PlaySFX] 准备播放音效: {sfxName}, 次数: {times}");

            MusicManager music = MusicManager.GetInstance();
            try
            {
                for (int i = 0; i < times && !interrupted; i++)
                {
                    activePlayback = music.PlaySFXTracked(sfxName, false);
                    float preparingTime = 0f;
                    float playingTime = 0f;
                    while (!interrupted && !activePlayback.IsDone)
                    {
                        yield return null;
                        if (interrupted || activePlayback.IsDone) break;
                        if (AudioListener.pause || !Application.isFocused) continue;
                        if (activePlayback.HasStarted) playingTime += Time.unscaledDeltaTime;
                        else preparingTime += Time.unscaledDeltaTime;
                        if (preparingTime >= 15f ||
                            playingTime >= activePlayback.ExpectedDuration + 3f)
                        {
                            music.CancelSFX(activePlayback);
                            Debug.LogWarning($"[PlaySFX] 音效 {sfxName} 等待超时，已取消播放");
                            yield break;
                        }
                    }
                    if (interrupted) yield break;
                    if (!activePlayback.Succeeded)
                    {
                        Debug.LogWarning($"[PlaySFX] 音效 {sfxName} 未完成播放，终止本次命令");
                        yield break;
                    }
                    activePlayback = null;
                    if (i < times - 1)
                    {
                        float gap = 0f;
                        while (gap < 0.05f && !interrupted)
                        {
                            yield return null;
                            if (!AudioListener.pause && Application.isFocused)
                                gap += Time.unscaledDeltaTime;
                        }
                    }
                }
            }
            finally
            {
                music.CancelSFX(activePlayback);
                activePlayback = null;
            }
            if (interrupted) yield break;
            Debug.Log($"[PlaySFX] 音效 {sfxName} 播放完成，共播放 {times} 次");
        }

        public override void Simulate(string args)
        {
            Debug.Log($"[PlaySFX.Simulate] 模拟播放音效: {args}");
        }
    }
}

