using System.Collections;
using UnityEngine;
using VNovelizer.Core.API;

namespace VNovelizer.Core.Commands
{
    public class ShakeCommand : VNCommand
    {
        public override string CommandName { get { return "shake"; } }

        private const float DefaultDuration = 0.5f;
        private const float DefaultFrequency = 60f;
        private const float DefaultIntensity = 10f;

        public override bool Execute(string args)
        {
            if (string.IsNullOrEmpty(args))
            {
                Debug.LogError("[ShakeCommand] Args cannot be empty. Format: shake(target,duration,intensity[,interval,count])");
                return false;
            }

            string[] parts = args.Split(',');
            if (parts.Length < 1)
            {
                Debug.LogError("[ShakeCommand] Missing target. Supported targets: screen, L/M/R, dialogue");
                return false;
            }

            string target = parts[0].Trim().ToLower();
            float duration = DefaultDuration;
            float intensity = DefaultIntensity;
            float frequency = DefaultFrequency;
            ShakeDirection direction = ShakeDirection.XY;
            float interval = 0f;
            int count = 1;

            if (parts.Length >= 2)
                TryParseFloat(parts[1], ref duration);
            if (parts.Length >= 3)
                TryParseFloat(parts[2], ref intensity);

            ParseOptionalArgs(parts, ref frequency, ref direction, ref interval, ref count);

            duration = Mathf.Max(0f, duration);
            intensity = Mathf.Max(0f, intensity);
            frequency = Mathf.Max(0f, frequency);
            interval = Mathf.Max(0f, interval);
            count = Mathf.Max(1, count);

            var panel = UIManager.GetInstance().GetPanel<VNGameplayPanel>("VNGameplayPanel");
            if (panel == null)
            {
                Debug.LogError("[ShakeCommand] VNGameplayPanel not found.");
                return false;
            }

            Transform targetTransform = ResolveTargetTransform(target, panel);
            if (targetTransform == null)
            {
                return false;
            }

            MonoManager.GetInstance().StartCoroutine(
                ShakeUICoroutine(targetTransform, duration, intensity, frequency, direction, interval, count));

            Debug.Log($"[ShakeCommand] Start shake: target={target}, duration={duration}, intensity={intensity}, frequency={frequency}, direction={direction}, interval={interval}, count={count}");
            return true;
        }

        private void ParseOptionalArgs(string[] parts, ref float frequency, ref ShakeDirection direction, ref float interval, ref int count)
        {
            if (parts.Length < 4)
                return;

            string fourth = parts[3].Trim();
            bool fourthIsNumber = float.TryParse(fourth, out float fourthNumber);

            if (!fourthIsNumber)
            {
                direction = ParseDirection(fourth);
            }
            else
            {
                frequency = fourthNumber;
            }

            if (parts.Length < 5)
                return;

            string fifth = parts[4].Trim();
            bool fifthIsInt = int.TryParse(fifth, out int fifthCount);

            if (fourthIsNumber && fifthIsInt)
            {
                interval = fourthNumber;
                count = fifthCount;
                frequency = DefaultFrequency;
                direction = ShakeDirection.XY;
            }
            else
            {
                direction = ParseDirection(fifth);
            }

            if (parts.Length >= 6)
                TryParseFloat(parts[5], ref interval);
            if (parts.Length >= 7)
                int.TryParse(parts[6].Trim(), out count);
        }

        private Transform ResolveTargetTransform(string target, VNGameplayPanel panel)
        {
            if (target == "screen")
            {
                return panel.transform;
            }

            if (target == "l" || target == "m" || target == "r" ||
                target == "left" || target == "mid" || target == "middle" || target == "right")
            {
                string posCode = NormalizePositionCode(target);
                RectTransform charRect = VNAPI.GetCharRect(posCode);
                if (charRect == null)
                {
                    Debug.LogError($"[ShakeCommand] Character at position '{target}' was not found.");
                    return null;
                }

                return charRect;
            }

            if (target == "dialogue")
            {
                RectTransform dialogueBoxRect = panel.GetDialogueBoxRect();
                if (dialogueBoxRect == null)
                {
                    Debug.LogError("[ShakeCommand] Dialogue box was not found.");
                    return null;
                }

                return dialogueBoxRect;
            }

            Debug.LogError($"[ShakeCommand] Unknown target: {target}. Supported targets: screen, L/M/R, dialogue");
            return null;
        }

        private string NormalizePositionCode(string posCode)
        {
            if (string.IsNullOrEmpty(posCode)) return posCode;
            string lower = posCode.ToLower();
            if (lower == "left" || lower == "l") return "L";
            if (lower == "mid" || lower == "middle" || lower == "m") return "M";
            if (lower == "right" || lower == "r") return "R";
            return posCode;
        }

        private enum ShakeDirection
        {
            XY,
            X,
            Y
        }

        private ShakeDirection ParseDirection(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return ShakeDirection.XY;

            string normalized = value.Trim().ToLower();
            if (normalized == "x" || normalized == "horizontal" || normalized == "h" || normalized == "left-right")
                return ShakeDirection.X;
            if (normalized == "y" || normalized == "vertical" || normalized == "v" || normalized == "up-down")
                return ShakeDirection.Y;

            return ShakeDirection.XY;
        }

        private void TryParseFloat(string value, ref float target)
        {
            if (float.TryParse(value.Trim(), out float parsed))
                target = parsed;
        }

        private IEnumerator ShakeUICoroutine(
            Transform targetTransform,
            float duration,
            float intensity,
            float frequency,
            ShakeDirection direction,
            float interval,
            int count)
        {
            if (targetTransform == null) yield break;

            RectTransform rect = targetTransform.GetComponent<RectTransform>();
            if (rect == null) yield break;

            Vector2 originalPos = rect.anchoredPosition;

            for (int shakeIndex = 0; shakeIndex < count; shakeIndex++)
            {
                yield return ShakeOnce(rect, targetTransform, originalPos, duration, intensity, frequency, direction);

                if (rect == null || targetTransform == null)
                    yield break;

                rect.anchoredPosition = originalPos;

                if (shakeIndex < count - 1 && interval > 0f)
                    yield return new WaitForSeconds(interval);
            }
        }

        private IEnumerator ShakeOnce(
            RectTransform rect,
            Transform targetTransform,
            Vector2 originalPos,
            float duration,
            float intensity,
            float frequency,
            ShakeDirection direction)
        {
            float elapsedTime = 0f;
            float sampleInterval = frequency > 0f ? 1f / frequency : 0f;
            float sampleTimer = 0f;
            Vector2 currentOffset = Vector2.zero;

            while (elapsedTime < duration)
            {
                if (rect == null || targetTransform == null)
                {
                    Debug.LogWarning("[ShakeCommand] RectTransform was destroyed during shake.");
                    yield break;
                }

                if (sampleTimer <= 0f)
                {
                    currentOffset = GenerateShakeOffset(intensity, direction);
                    sampleTimer = sampleInterval;
                }

                try
                {
                    rect.anchoredPosition = new Vector2(
                        originalPos.x + currentOffset.x,
                        originalPos.y + currentOffset.y);
                }
                catch (MissingReferenceException)
                {
                    Debug.LogWarning("[ShakeCommand] RectTransform was destroyed during shake.");
                    yield break;
                }

                elapsedTime += Time.deltaTime;
                sampleTimer -= Time.deltaTime;
                yield return null;
            }

            if (rect != null && targetTransform != null)
            {
                try
                {
                    rect.anchoredPosition = originalPos;
                }
                catch (MissingReferenceException)
                {
                    Debug.LogWarning("[ShakeCommand] RectTransform was destroyed when shake ended.");
                }
            }
        }

        private Vector2 GenerateShakeOffset(float intensity, ShakeDirection direction)
        {
            switch (direction)
            {
                case ShakeDirection.X:
                    return new Vector2(Random.Range(-intensity, intensity), 0f);
                case ShakeDirection.Y:
                    return new Vector2(0f, Random.Range(-intensity, intensity));
                default:
                    return new Vector2(Random.Range(-intensity, intensity), Random.Range(-intensity, intensity));
            }
        }
    }
}
