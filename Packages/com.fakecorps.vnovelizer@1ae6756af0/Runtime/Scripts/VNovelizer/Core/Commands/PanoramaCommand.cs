using System.Collections;
using System.Globalization;
using UnityEngine;
using VNovelizer.Core.UI;
using VNovelizer.Core.Utils;

namespace VNovelizer.Core.Commands
{
    public class PanoramaCommand : VNCommand
    {
        public override string CommandName { get { return "panorama"; } }

        private PanoramaPanel activePanel;
        private VNGameplayPanel activeGameplayPanel;
        private bool isRunning;
        private bool hasRestored;

        public override bool Execute(string args)
        {
            return !string.IsNullOrWhiteSpace(args);
        }

        public override IEnumerator ExecuteAsync(string args)
        {
            if (!TryParseArgs(args, out string panoramaName, out float startYaw, out float startPitch))
            {
                Debug.LogError("[PanoramaCommand] 参数格式错误，正确格式：panorama(bgName, startYaw, startPitch)");
                VNCommandBridge.AdvanceAfterCommands();
                yield break;
            }

            isRunning = true;
            hasRestored = false;
            activeGameplayPanel = UIManager.GetInstance().GetPanel<VNGameplayPanel>("VNGameplayPanel");
            if (activeGameplayPanel == null)
            {
                Debug.LogError("[PanoramaCommand] 未找到 VNGameplayPanel，已跳过全景观察。");
                FinishAndAdvance();
                yield break;
            }

            activeGameplayPanel.SetPanoramaObservationActive(true);
            GameStateManager.GetInstance().SetState(GameState.Panorama);

            string fullPath = VNProjectConfig.Instance.BackgroundResPath + "/" + panoramaName;
            Texture panoramaTexture = null;
            yield return LoadPanoramaTexture(fullPath, texture => panoramaTexture = texture);

            if (!isRunning)
            {
                yield break;
            }

            if (panoramaTexture == null)
            {
                Debug.LogError($"[PanoramaCommand] 全景贴图加载失败: {panoramaName} (路径: {fullPath})");
                FinishAndAdvance();
                yield break;
            }

            bool closed = false;
            activePanel = PanoramaPanel.Create(activeGameplayPanel.GetPanoramaLayerRoot(), true);
            activePanel.Show(panoramaTexture, startYaw, startPitch, () =>
            {
                closed = true;
            });

            while (isRunning && !closed)
            {
                yield return null;
            }

            FinishAndAdvance();
        }

        public override void Interrupt()
        {
            if (!isRunning) return;

            Debug.Log("[PanoramaCommand] 全景观察被中断，关闭观察层。");
            if (activePanel != null)
            {
                activePanel.Close();
            }
            else
            {
                FinishAndAdvance();
            }
        }

        private IEnumerator LoadPanoramaTexture(string fullPath, System.Action<Texture> onLoaded)
        {
            bool textureLoadFinished = false;
            Texture2D loadedTexture = null;

            ResourcesManager.GetInstance().LoadOptionalAsync<Texture2D>(fullPath, texture =>
            {
                loadedTexture = texture;
                textureLoadFinished = true;
            });

            while (!textureLoadFinished)
            {
                yield return null;
            }

            if (loadedTexture != null)
            {
                onLoaded?.Invoke(loadedTexture);
                yield break;
            }

            bool spriteLoadFinished = false;
            Sprite loadedSprite = null;

            ResourcesManager.GetInstance().LoadOptionalAsync<Sprite>(fullPath, sprite =>
            {
                loadedSprite = sprite;
                spriteLoadFinished = true;
            });

            while (!spriteLoadFinished)
            {
                yield return null;
            }

            onLoaded?.Invoke(loadedSprite != null ? loadedSprite.texture : null);
        }

        private bool TryParseArgs(string args, out string panoramaName, out float startYaw, out float startPitch)
        {
            panoramaName = null;
            startYaw = 0f;
            startPitch = 0f;

            if (string.IsNullOrWhiteSpace(args))
            {
                return false;
            }

            string[] parts = args.Split(',');
            panoramaName = parts[0].Trim();
            if (string.IsNullOrEmpty(panoramaName))
            {
                return false;
            }

            if (parts.Length > 1 && !TryParseFloat(parts[1].Trim(), out startYaw))
            {
                return false;
            }

            if (parts.Length > 2 && !TryParseFloat(parts[2].Trim(), out startPitch))
            {
                return false;
            }

            if (parts.Length > 3)
            {
                return false;
            }

            return true;
        }

        private bool TryParseFloat(string rawValue, out float value)
        {
            return float.TryParse(rawValue, NumberStyles.Float, CultureInfo.InvariantCulture, out value) ||
                   float.TryParse(rawValue, out value);
        }

        private void FinishAndAdvance()
        {
            if (hasRestored) return;
            hasRestored = true;
            isRunning = false;

            if (activePanel != null)
            {
                Object.Destroy(activePanel.gameObject);
                activePanel = null;
            }

            if (activeGameplayPanel != null)
            {
                activeGameplayPanel.SetPanoramaObservationActive(false);
                activeGameplayPanel = null;
            }

            if (GameStateManager.GetInstance().CurrentState == GameState.Panorama)
            {
                GameStateManager.GetInstance().SetState(GameState.Gameplay);
            }

            VNCommandBridge.AdvanceAfterCommands();
        }
    }
}
