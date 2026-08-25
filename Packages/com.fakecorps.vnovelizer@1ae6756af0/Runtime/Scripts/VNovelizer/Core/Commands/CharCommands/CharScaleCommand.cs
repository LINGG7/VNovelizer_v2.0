using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using VNovelizer.Core.API;
using VNovelizer.Core.Diagnostics;
using PrimeTween;

namespace VNovelizer.Core.Commands
{
    /// <summary>
    /// Character scale command.
    /// Format: charscale(scale, duration)
    /// Optional format: charscale(position, scale, duration)
    /// </summary>
    public class CharScaleCommand : VNCommand
    {
        public override string CommandName { get { return "charscale"; } }

        private const float DefaultDuration = 0.5f;
        private static readonly string[] AllPositions = { "L", "M", "R" };

        private readonly List<Tween> _scaleTweens = new List<Tween>();
        private readonly List<RectTransform> _targets = new List<RectTransform>();
        private readonly List<Vector3> _targetScales = new List<Vector3>();

        public override bool Execute(string args)
        {
            return true;
        }

        public override IEnumerator ExecuteAsync(string args)
        {
            if (string.IsNullOrEmpty(args)) yield break;
            VNDebug.LogVerbose($"[TypingTrace][CharScale.ExecuteAsync] start args={args}");

            if (!TryParseArgs(args, out string[] positions, out float targetScale, out float duration))
            {
                yield break;
            }

            var panel = UIManager.GetInstance().GetPanel<VNGameplayPanel>("VNGameplayPanel");
            if (panel == null)
            {
                Debug.LogError("[CharScale] 找不到 VNGameplayPanel");
                yield break;
            }

            _scaleTweens.Clear();
            _targets.Clear();
            _targetScales.Clear();

            foreach (string posCode in positions)
            {
                RectTransform target = VNAPI.GetCharRect(posCode);
                if (target == null || target.gameObject == null || !target.gameObject.activeSelf)
                {
                    VNDebug.LogVerbose($"[TypingTrace][CharScale.ExecuteAsync] skip posCode={posCode}");
                    continue;
                }

                panel.SaveDefaultCharTransform(posCode);

                Vector3 startScale = target.localScale;
                float signX = Mathf.Sign(startScale.x);
                if (Mathf.Approximately(signX, 0f)) signX = 1f;

                Vector3 endScale = new Vector3(signX * Mathf.Abs(targetScale), Mathf.Abs(targetScale), 1f);
                _targets.Add(target);
                _targetScales.Add(endScale);

                Tween tween = Tween.Custom(startScale, endScale, duration,
                    onValueChange: newScale =>
                    {
                        if (target != null && target.gameObject != null)
                        {
                            target.localScale = newScale;
                        }
                    },
                    ease: Ease.OutQuad);

                _scaleTweens.Add(tween);
            }

            for (int i = 0; i < _scaleTweens.Count; i++)
            {
                yield return _scaleTweens[i].ToYieldInstruction();
            }

            VNDebug.LogVerbose($"[TypingTrace][CharScale.ExecuteAsync] completed count={_scaleTweens.Count}, scale={targetScale}, duration={duration}");
            _scaleTweens.Clear();
            _targets.Clear();
            _targetScales.Clear();
        }

        public override void Interrupt()
        {
            VNDebug.LogVerbose($"[TypingTrace][CharScale.Interrupt] count={_scaleTweens.Count}");

            for (int i = 0; i < _scaleTweens.Count; i++)
            {
                if (_scaleTweens[i].isAlive)
                {
                    _scaleTweens[i].Complete();
                }
            }

            for (int i = 0; i < _targets.Count && i < _targetScales.Count; i++)
            {
                RectTransform target = _targets[i];
                if (target != null && target.gameObject != null)
                {
                    target.localScale = _targetScales[i];
                }
            }

            _scaleTweens.Clear();
            _targets.Clear();
            _targetScales.Clear();
        }

        public override void Simulate(string args)
        {
            if (string.IsNullOrEmpty(args)) return;
            if (!TryParseArgs(args, out string[] positions, out float targetScale, out float duration)) return;

            foreach (string posCode in positions)
            {
                string charData = VNManager.GetInstance().GetCharacterData(posCode);
                if (string.IsNullOrEmpty(charData) || charData == "hide")
                {
                    Debug.LogWarning($"[CharScale.Simulate] 位置 {posCode} 没有角色，跳过缩放");
                    continue;
                }

                Debug.Log($"[CharScale.Simulate] 位置 {posCode} 将在 {duration} 秒内缩放到 {targetScale}");
            }
        }

        private bool TryParseArgs(string args, out string[] positions, out float targetScale, out float duration)
        {
            positions = AllPositions;
            targetScale = 1f;
            duration = DefaultDuration;

            string[] parts = args.Split(',');
            if (parts.Length < 2)
            {
                Debug.LogError("[CharScale] 参数不足，格式：charscale(scale, duration) 或 charscale(position, scale, duration)");
                return false;
            }

            if (parts.Length >= 3 && IsPositionCode(parts[0].Trim()))
            {
                positions = new[] { parts[0].Trim() };

                if (!float.TryParse(parts[1].Trim(), out targetScale))
                {
                    Debug.LogError($"[CharScale] 无法解析缩放比例: {parts[1]}");
                    return false;
                }

                if (!float.TryParse(parts[2].Trim(), out duration))
                {
                    Debug.LogError($"[CharScale] 无法解析缩放时间: {parts[2]}");
                    return false;
                }
            }
            else
            {
                if (!float.TryParse(parts[0].Trim(), out targetScale))
                {
                    Debug.LogError($"[CharScale] 无法解析缩放比例: {parts[0]}");
                    return false;
                }

                if (!float.TryParse(parts[1].Trim(), out duration))
                {
                    Debug.LogError($"[CharScale] 无法解析缩放时间: {parts[1]}");
                    return false;
                }
            }

            if (targetScale < 0f)
            {
                targetScale = Mathf.Abs(targetScale);
            }

            if (duration < 0f)
            {
                duration = 0f;
            }

            return true;
        }

        private bool IsPositionCode(string value)
        {
            if (string.IsNullOrEmpty(value)) return false;
            string upper = value.ToUpper();
            return upper == "L" || upper == "LEFT" ||
                   upper == "M" || upper == "MID" || upper == "MIDDLE" ||
                   upper == "R" || upper == "RIGHT";
        }
    }
}
