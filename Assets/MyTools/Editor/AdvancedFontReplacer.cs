using UnityEngine;
using UnityEditor;
using TMPro;
using UnityEngine.UI;
using System.Collections.Generic;

public class AdvancedFontReplacer : EditorWindow
{
    public TMP_FontAsset newTMPFont;
    public Font newLegacyFont;

    [MenuItem("Tools/全项目字体替换器")]
    public static void ShowWindow()
    {
        GetWindow<AdvancedFontReplacer>("字体替换器");
    }

    void OnGUI()
    {
        GUILayout.Label("设置新字体", EditorStyles.boldLabel);
        newTMPFont = (TMP_FontAsset)EditorGUILayout.ObjectField("新 TMP 字体", newTMPFont, typeof(TMP_FontAsset), false);
        newLegacyFont = (Font)EditorGUILayout.ObjectField("新 Legacy 字体", newLegacyFont, typeof(Font), false);

        EditorGUILayout.Space();

        if (GUILayout.Button("替换所有预制体 (Prefabs) 中的字体"))
        {
            ReplaceInPrefabs();
        }

        if (GUILayout.Button("替换当前场景中的字体"))
        {
            ReplaceInScene();
        }
    }

    // 核心逻辑：替换预制体
    void ReplaceInPrefabs()
    {
        string[] ids = AssetDatabase.FindAssets("t:Prefab");
        int count = 0;

        foreach (string id in ids)
        {
            string path = AssetDatabase.GUIDToAssetPath(id);

            // 【关键修改 1】：排除掉 Packages 文件夹下的只读资源
            // 只处理路径以 "Assets/" 开头的资源
            if (!path.StartsWith("Assets/"))
            {
                continue;
            }

            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (prefab == null) continue;

            // 【关键修改 2】：检查预制体是否可以编辑（防止模型预制体等变体报错）
            bool isImmutable = (prefab.hideFlags & HideFlags.NotEditable) != 0;
            if (isImmutable) continue;

            bool isDirty = false;

            // 处理 TextMeshPro
            if (newTMPFont != null)
            {
                TMP_Text[] texts = prefab.GetComponentsInChildren<TMP_Text>(true);
                foreach (var t in texts)
                {
                    // 只有当字体不一致时才修改，减少不必要的保存
                    if (t.font != newTMPFont)
                    {
                        t.font = newTMPFont;
                        isDirty = true;
                    }
                }
            }

            // 处理 Legacy Text
            if (newLegacyFont != null)
            {
                Text[] legacyTexts = prefab.GetComponentsInChildren<Text>(true);
                foreach (var t in legacyTexts)
                {
                    if (t.font != newLegacyFont)
                    {
                        t.font = newLegacyFont;
                        isDirty = true;
                    }
                }
            }

            if (isDirty)
            {
                EditorUtility.SetDirty(prefab);
                // 使用 Try-Catch 保护，防止个别顽固文件导致整个流程中断
                try
                {
                    PrefabUtility.SavePrefabAsset(prefab);
                    count++;
                }
                catch (System.Exception e)
                {
                    Debug.LogWarning($"无法保存预制体: {path}. 错误: {e.Message}");
                }
            }
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log($"成功修改了 {count} 个可编辑的预制体！");
        EditorUtility.DisplayDialog("完成", $"已处理 {count} 个预制体", "OK");
    }

    void ReplaceInScene()
    {
        // 逻辑与之前相同
        TMP_Text[] allTMP = FindObjectsOfType<TMP_Text>(true);
        foreach (var t in allTMP) { if (newTMPFont) t.font = newTMPFont; EditorUtility.SetDirty(t); }

        Text[] allLegacy = FindObjectsOfType<Text>(true);
        foreach (var t in allLegacy) { if (newLegacyFont) t.font = newLegacyFont; EditorUtility.SetDirty(t); }

        Debug.Log("场景字体替换完成");
    }
}