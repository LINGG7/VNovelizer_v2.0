using System.Collections;
using UnityEngine;
using VNovelizer.Core.API;
using VNovelizer.Core.Commands;

namespace VNovelizer.Project.Commands
{
    public class PlayFilterCommand : VNCommand
    {
        private const string FilterResourcePath = "VNovelizerRes/VFX/PostProcess";
        private const string FilterStatePrefix = "filter:";

        public override string CommandName => "playfilter";

        public override bool Execute(string args)
        {
            if (!TryGetFilterName(args, out string filterName)) return false;

            VNAPI.RegisterEffect(GetStateKey(filterName));

            Transform parent = VNAPI.GetEffectLayer();
            if (parent == null) return false;

            string objName = GetObjectName(filterName);
            if (parent.Find(objName) != null) return true;

            string path = $"{FilterResourcePath}/{filterName}";
            GameObject prefab = Resources.Load<GameObject>(path);
            if (prefab == null)
            {
                Debug.LogError($"[PlayFilter] Filter not found: {path}");
                return false;
            }

            GameObject go = Object.Instantiate(prefab, parent, false);
            go.name = objName;
            StretchToParent(go);
            return true;
        }

        public override IEnumerator ExecuteAsync(string args)
        {
            Execute(args);
            yield break;
        }

        public override void Simulate(string args)
        {
            if (TryGetFilterName(args, out string filterName))
            {
                VNAPI.RegisterEffect(GetStateKey(filterName));
            }
        }

        public static bool TryParseStateKey(string stateKey, out string filterName)
        {
            filterName = null;
            if (string.IsNullOrWhiteSpace(stateKey)) return false;
            if (!stateKey.StartsWith(FilterStatePrefix, System.StringComparison.OrdinalIgnoreCase)) return false;

            filterName = stateKey.Substring(FilterStatePrefix.Length).Trim();
            return !string.IsNullOrEmpty(filterName);
        }

        internal static string GetStateKey(string filterName) => FilterStatePrefix + filterName.Trim();

        internal static string GetObjectName(string filterName) => "VNFilter_" + filterName.Trim();

        internal static bool TryGetFilterName(string args, out string filterName)
        {
            filterName = args == null ? null : args.Trim();
            return !string.IsNullOrEmpty(filterName);
        }

        internal static void StretchToParent(GameObject go)
        {
            RectTransform rect = go.GetComponent<RectTransform>();
            if (rect == null) return;

            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            rect.localScale = Vector3.one;
            rect.anchoredPosition = Vector2.zero;
        }
    }
}
