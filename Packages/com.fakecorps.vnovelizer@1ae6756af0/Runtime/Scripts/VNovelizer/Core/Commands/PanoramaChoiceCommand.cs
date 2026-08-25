using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;
using VNovelizer.Core.UI;
using VNovelizer.Core.Utils;

namespace VNovelizer.Core.Commands
{
    public class PanoramaChoiceCommand : VNCommand
    {
        public override string CommandName { get { return "panoramaChoice"; } }

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
            if (!TryParseArgs(args, out string panoramaName, out float startYaw, out float startPitch, out List<PanoramaHotspot> hotspots))
            {
                Debug.LogError("[PanoramaChoiceCommand] 参数格式错误，正确格式：panoramaChoice(bgName, startYaw, startPitch, label|yaw|pitch|command; label|yaw|pitch|command)");
                VNCommandBridge.AdvanceAfterCommands();
                yield break;
            }

            isRunning = true;
            hasRestored = false;
            activeGameplayPanel = UIManager.GetInstance().GetPanel<VNGameplayPanel>("VNGameplayPanel");
            if (activeGameplayPanel == null)
            {
                Debug.LogError("[PanoramaChoiceCommand] 未找到 VNGameplayPanel，已跳过全景选择。");
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
                Debug.LogError($"[PanoramaChoiceCommand] 全景贴图加载失败: {panoramaName} (路径: {fullPath})");
                FinishAndAdvance();
                yield break;
            }

            bool closed = false;
            bool hasHotspotSelection = false;
            string selectedCommand = null;

            activePanel = PanoramaPanel.Create(activeGameplayPanel.GetPanoramaLayerRoot(), false);
            activePanel.Show(panoramaTexture, startYaw, startPitch, () =>
            {
                closed = true;
            }, hotspots, command =>
            {
                hasHotspotSelection = true;
                selectedCommand = command;
            });

            while (isRunning && !closed)
            {
                yield return null;
            }

            if (hasHotspotSelection && !string.IsNullOrWhiteSpace(selectedCommand))
            {
                FinishWithoutAdvance();
                MonoManager.GetInstance().StartCoroutine(ExecuteSelectedCommandNextFrame(selectedCommand));
                yield break;
            }

            FinishAndAdvance();
        }

        public override void Interrupt()
        {
            if (!isRunning) return;

            Debug.Log("[PanoramaChoiceCommand] 全景选择被中断，关闭选择层。");
            if (activePanel != null)
            {
                activePanel.Close();
            }
            else
            {
                FinishAndAdvance();
            }
        }

        private IEnumerator ExecuteSelectedCommandNextFrame(string command)
        {
            yield return null;
            VNManager.GetInstance().ExecuteChoiceCommand(command);
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

        private bool TryParseArgs(string args, out string panoramaName, out float startYaw, out float startPitch, out List<PanoramaHotspot> hotspots)
        {
            panoramaName = null;
            startYaw = 0f;
            startPitch = 0f;
            hotspots = new List<PanoramaHotspot>();

            if (string.IsNullOrWhiteSpace(args))
            {
                return false;
            }

            string[] parts = SplitFirstArgs(args, 4);
            if (parts.Length < 1)
            {
                return false;
            }

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
                ParseHotspots(parts[3], hotspots);
            }

            return true;
        }

        private string[] SplitFirstArgs(string args, int maxParts)
        {
            List<string> parts = new List<string>();
            int startIndex = 0;

            for (int i = 0; i < args.Length && parts.Count < maxParts - 1; i++)
            {
                if (args[i] == ',')
                {
                    parts.Add(args.Substring(startIndex, i - startIndex));
                    startIndex = i + 1;
                }
            }

            parts.Add(args.Substring(startIndex));
            return parts.ToArray();
        }

        private void ParseHotspots(string rawHotspots, List<PanoramaHotspot> hotspots)
        {
            if (string.IsNullOrWhiteSpace(rawHotspots))
            {
                return;
            }

            string[] entries = rawHotspots.Split(';');
            for (int i = 0; i < entries.Length && hotspots.Count < 3; i++)
            {
                string entry = entries[i].Trim();
                if (string.IsNullOrEmpty(entry))
                {
                    continue;
                }

                string[] fields = entry.Split('|');
                if (fields.Length < 4)
                {
                    Debug.LogWarning($"[PanoramaChoiceCommand] 热点参数不足，已忽略: {entry}");
                    continue;
                }

                if (!TryParseFloat(fields[1].Trim(), out float yaw) ||
                    !TryParseFloat(fields[2].Trim(), out float pitch))
                {
                    Debug.LogWarning($"[PanoramaChoiceCommand] 热点 yaw/pitch 格式错误，已忽略: {entry}");
                    continue;
                }

                hotspots.Add(new PanoramaHotspot
                {
                    Label = fields[0].Trim(),
                    Yaw = yaw,
                    Pitch = pitch,
                    Command = fields[3].Trim()
                });
            }
        }

        private bool TryParseFloat(string rawValue, out float value)
        {
            return float.TryParse(rawValue, NumberStyles.Float, CultureInfo.InvariantCulture, out value) ||
                   float.TryParse(rawValue, out value);
        }

        private void FinishAndAdvance()
        {
            FinishWithoutAdvance();
            VNCommandBridge.AdvanceAfterCommands();
        }

        private void FinishWithoutAdvance()
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
        }
    }
}
