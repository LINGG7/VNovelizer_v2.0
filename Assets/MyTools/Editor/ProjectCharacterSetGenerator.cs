using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;
using TMPro;

public class ProjectCharacterSetGenerator : EditorWindow
{
    private const string DefaultOutputPath = "Assets/TextMesh Pro/Resources/Fonts & Materials/ProjectCharacters.txt";
    private const string DefaultFontAssetPath = "Assets/TextMesh Pro/Resources/Fonts & Materials/ChillKai SDF 2048 v2.asset";
    private const string DefaultSourceFontPath = "Assets/ResourcesRemoved/ChillKai.ttf";

    private string outputPath = DefaultOutputPath;
    private bool scanCsvScripts = true;
    private bool scanExcelScripts = true;
    private bool scanUiPrefabs = true;
    private bool scanLocalization = true;
    private bool scanProjectConfig = true;
    private bool includeAscii = true;
    private bool includeCommonPunctuation = true;

    [MenuItem("Tools/VNovelizer/Generate Project Character Set")]
    public static void ShowWindow()
    {
        GetWindow<ProjectCharacterSetGenerator>("Character Set");
    }

    [MenuItem("Tools/VNovelizer/Generate Project Character Set Now")]
    public static void GenerateWithDefaults()
    {
        CharacterSetResult result = Generate(
            DefaultOutputPath,
            scanCsvScripts: true,
            scanExcelScripts: true,
            scanUiPrefabs: true,
            scanLocalization: true,
            scanProjectConfig: true,
            includeAscii: true,
            includeCommonPunctuation: true);

        ShowResult(result);
    }

    [MenuItem("Tools/VNovelizer/一键扫描并重建 TMP 字体")]
    public static void ScanAndRebuildFontAssetNow()
    {
        CharacterSetResult result = Generate(
            DefaultOutputPath,
            scanCsvScripts: true,
            scanExcelScripts: true,
            scanUiPrefabs: true,
            scanLocalization: true,
            scanProjectConfig: true,
            includeAscii: true,
            includeCommonPunctuation: true);

        TMP_FontAsset fontAsset = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(DefaultFontAssetPath);
        if (fontAsset == null)
        {
            EditorUtility.DisplayDialog("TMP Font Rebuild", $"找不到字体资产：{DefaultFontAssetPath}", "OK");
            return;
        }

        Font sourceFont = ResolveSourceFont(fontAsset);
        if (sourceFont == null)
        {
            EditorUtility.DisplayDialog(
                "TMP Font Rebuild",
                $"目标 TMP 字体没有关联源字体文件，并且找不到默认源字体：{DefaultSourceFontPath}",
                "OK");
            return;
        }

        string backupPath = DefaultFontAssetPath + ".before-rebuild.bak";
        File.Copy(DefaultFontAssetPath, backupPath, true);

        Undo.RecordObject(fontAsset, "Rebuild TMP Font Asset");
        // TMP 3.x persists newly generated glyph atlases as sub-assets.
        SetSourceFontEditorReference(fontAsset, sourceFont);
        fontAsset.isMultiAtlasTexturesEnabled = true;
        fontAsset.atlasPopulationMode = AtlasPopulationMode.Dynamic;
        fontAsset.ClearFontAssetData(false);

        string generatedCharacters = File.ReadAllText(DefaultOutputPath, Encoding.UTF8);
        bool success = fontAsset.TryAddCharacters(generatedCharacters, out string missingCharacters);
        fontAsset.atlasPopulationMode = AtlasPopulationMode.Static;
        EditorUtility.SetDirty(fontAsset);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        string missing = string.IsNullOrEmpty(missingCharacters) ? "无" : missingCharacters;
        string message =
            $"字符集：{result.CharacterCount}\n" +
            $"扫描文件：{result.ScannedFileCount}\n" +
            $"字体资产：{DefaultFontAssetPath}\n" +
            $"源字体：{sourceFont.name}\n" +
            $"字体未包含字符：{missing}\n" +
            $"备份：{backupPath}";

        if (!success)
        {
            Debug.LogWarning($"[ProjectCharacterSetGenerator] TMP 字体重建未完全成功，缺失字符：{missing}");
        }
        else
        {
            Debug.Log($"[ProjectCharacterSetGenerator] TMP 字体重建完成：{message}");
        }

        EditorUtility.DisplayDialog("TMP Font Rebuild", message, "OK");
    }

    private static Font ResolveSourceFont(TMP_FontAsset fontAsset)
    {
        SerializedObject serializedFontAsset = new SerializedObject(fontAsset);
        SerializedProperty editorReference = serializedFontAsset.FindProperty("m_SourceFontFile_EditorRef");
        Font sourceFont = editorReference?.objectReferenceValue as Font;
        if (sourceFont != null)
        {
            return sourceFont;
        }

        SerializedProperty guidProperty = serializedFontAsset.FindProperty("m_SourceFontFileGUID");
        string sourceGuid = guidProperty?.stringValue;
        if (!string.IsNullOrEmpty(sourceGuid))
        {
            string sourcePath = AssetDatabase.GUIDToAssetPath(sourceGuid);
            sourceFont = AssetDatabase.LoadAssetAtPath<Font>(sourcePath);
            if (sourceFont != null)
            {
                return sourceFont;
            }
        }

        return AssetDatabase.LoadAssetAtPath<Font>(DefaultSourceFontPath);
    }

    private static void SetSourceFontEditorReference(TMP_FontAsset fontAsset, Font sourceFont)
    {
        SerializedObject serializedFontAsset = new SerializedObject(fontAsset);
        SerializedProperty editorReference = serializedFontAsset.FindProperty("m_SourceFontFile_EditorRef");
        SerializedProperty guidProperty = serializedFontAsset.FindProperty("m_SourceFontFileGUID");

        if (editorReference != null)
        {
            editorReference.objectReferenceValue = sourceFont;
        }

        if (guidProperty != null)
        {
            guidProperty.stringValue = AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(sourceFont));
        }

        serializedFontAsset.ApplyModifiedPropertiesWithoutUndo();
    }

    private void OnGUI()
    {
        EditorGUILayout.LabelField("Project Character Set", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox(
            "Generates a text file for TextMeshPro Font Asset Creator. Run this after adding or changing scripts/UI text.",
            MessageType.Info);

        outputPath = EditorGUILayout.TextField("Output Path", outputPath);
        EditorGUILayout.Space();

        scanCsvScripts = EditorGUILayout.ToggleLeft("Scan CSV scripts", scanCsvScripts);
        scanExcelScripts = EditorGUILayout.ToggleLeft("Scan Excel scripts (.xlsx)", scanExcelScripts);
        scanUiPrefabs = EditorGUILayout.ToggleLeft("Scan UI prefabs", scanUiPrefabs);
        scanLocalization = EditorGUILayout.ToggleLeft("Scan localization assets", scanLocalization);
        scanProjectConfig = EditorGUILayout.ToggleLeft("Scan VNProjectConfig", scanProjectConfig);
        includeAscii = EditorGUILayout.ToggleLeft("Include ASCII", includeAscii);
        includeCommonPunctuation = EditorGUILayout.ToggleLeft("Include common punctuation", includeCommonPunctuation);

        EditorGUILayout.Space();
        if (GUILayout.Button("Generate", GUILayout.Height(32)))
        {
            CharacterSetResult result = Generate(
                outputPath,
                scanCsvScripts,
                scanExcelScripts,
                scanUiPrefabs,
                scanLocalization,
                scanProjectConfig,
                includeAscii,
                includeCommonPunctuation);

            ShowResult(result);
        }
    }

    private static CharacterSetResult Generate(
        string outputPath,
        bool scanCsvScripts,
        bool scanExcelScripts,
        bool scanUiPrefabs,
        bool scanLocalization,
        bool scanProjectConfig,
        bool includeAscii,
        bool includeCommonPunctuation)
    {
        SortedSet<char> characters = new SortedSet<char>();
        List<string> warnings = new List<string>();
        int scannedFiles = 0;

        if (includeAscii)
        {
            AddRange(characters, "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789");
        }

        if (includeCommonPunctuation)
        {
            AddRange(characters, " \t\n\r.,!?;:'\"()[]{}<>+-*/=_%#@$&|\\~`^");
            AddRange(characters, "，。！？；：“”‘’（）【】《》、·…—-～￥");
        }

        if (scanCsvScripts)
        {
            ScanTextFolder("Assets/Resources/VNovelizerRes/VNScripts", new[] { ".csv" }, characters, ref scannedFiles, warnings);
        }

        if (scanExcelScripts)
        {
            ScanExcelFolder("Assets/Resources/VNovelizerRes/ExcelVNScripts", characters, ref scannedFiles, warnings);
        }

        if (scanUiPrefabs)
        {
            ScanTextFolder("Assets/Resources/VNovelizerRes/VNPrefabs/UI", new[] { ".prefab" }, characters, ref scannedFiles, warnings);
        }

        if (scanLocalization)
        {
            ScanTextFolder("Assets/Localization", new[] { ".asset", ".json", ".txt", ".csv" }, characters, ref scannedFiles, warnings);
        }

        if (scanProjectConfig)
        {
            ScanTextFile("Assets/Resources/VNProjectConfig.asset", characters, ref scannedFiles, warnings);
        }

        string directory = Path.GetDirectoryName(outputPath);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        File.WriteAllText(outputPath, new string(characters.ToArray()), new UTF8Encoding(false));
        AssetDatabase.ImportAsset(outputPath);
        AssetDatabase.Refresh();

        return new CharacterSetResult(outputPath, characters.Count, scannedFiles, warnings);
    }

    private static void ScanTextFolder(
        string folder,
        string[] extensions,
        SortedSet<char> characters,
        ref int scannedFiles,
        List<string> warnings)
    {
        if (!Directory.Exists(folder))
        {
            return;
        }

        foreach (string file in Directory.EnumerateFiles(folder, "*", SearchOption.AllDirectories))
        {
            if (file.EndsWith(".meta", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (!extensions.Contains(Path.GetExtension(file), StringComparer.OrdinalIgnoreCase))
            {
                continue;
            }

            ScanTextFile(file, characters, ref scannedFiles, warnings);
        }
    }

    private static void ScanTextFile(
        string file,
        SortedSet<char> characters,
        ref int scannedFiles,
        List<string> warnings)
    {
        if (!File.Exists(file))
        {
            return;
        }

        try
        {
            string text = File.ReadAllText(file, Encoding.UTF8);
            AddText(characters, text);
            AddEscapedUnicodeText(characters, text);
            scannedFiles++;
        }
        catch (Exception ex)
        {
            warnings.Add($"{file}: {ex.Message}");
        }
    }

    private static void ScanExcelFolder(
        string folder,
        SortedSet<char> characters,
        ref int scannedFiles,
        List<string> warnings)
    {
        if (!Directory.Exists(folder))
        {
            return;
        }

        foreach (string file in Directory.EnumerateFiles(folder, "*.xlsx", SearchOption.AllDirectories))
        {
            if (Path.GetFileName(file).StartsWith("~$", StringComparison.Ordinal))
            {
                continue;
            }

            try
            {
                AddExcelText(file, characters);
                scannedFiles++;
            }
            catch (Exception ex)
            {
                warnings.Add($"{file}: {ex.Message}");
            }
        }
    }

    private static void AddExcelText(string file, SortedSet<char> characters)
    {
        using (ZipArchive archive = ZipFile.OpenRead(file))
        {
            foreach (ZipArchiveEntry entry in archive.Entries)
            {
                if (!entry.FullName.StartsWith("xl/sharedStrings.xml", StringComparison.OrdinalIgnoreCase) &&
                    !entry.FullName.StartsWith("xl/worksheets/", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                using (Stream stream = entry.Open())
                using (StreamReader reader = new StreamReader(stream, Encoding.UTF8))
                {
                    string xml = reader.ReadToEnd();
                    string noTags = Regex.Replace(xml, "<[^>]+>", string.Empty);
                    AddText(characters, WebUtility.HtmlDecode(noTags));
                }
            }
        }
    }

    private static void AddEscapedUnicodeText(SortedSet<char> characters, string text)
    {
        foreach (Match match in Regex.Matches(text, @"\\u([0-9a-fA-F]{4})"))
        {
            int code = Convert.ToInt32(match.Groups[1].Value, 16);
            AddChar(characters, (char)code);
        }
    }

    private static void AddRange(SortedSet<char> characters, string text)
    {
        foreach (char c in text)
        {
            characters.Add(c);
        }
    }

    private static void AddText(SortedSet<char> characters, string text)
    {
        foreach (char c in text)
        {
            AddChar(characters, c);
        }
    }

    private static void AddChar(SortedSet<char> characters, char c)
    {
        if (char.IsControl(c) && c != '\n' && c != '\r' && c != '\t')
        {
            return;
        }

        int code = c;
        bool keep =
            code >= 0x20 && code <= 0x9FFF ||
            code >= 0xF900 && code <= 0xFAFF ||
            code >= 0x2000 && code <= 0x206F ||
            code >= 0x3000 && code <= 0x303F ||
            code >= 0xFF00 && code <= 0xFFEF;

        if (keep)
        {
            characters.Add(c);
        }
    }

    private static void ShowResult(CharacterSetResult result)
    {
        string message = $"Wrote: {result.OutputPath}\nCharacters: {result.CharacterCount}\nScanned files: {result.ScannedFileCount}";
        if (result.Warnings.Count > 0)
        {
            message += $"\nWarnings: {result.Warnings.Count}. See Console.";
            foreach (string warning in result.Warnings)
            {
                Debug.LogWarning($"[ProjectCharacterSetGenerator] {warning}");
            }
        }

        Debug.Log($"[ProjectCharacterSetGenerator] {message}");
        EditorUtility.DisplayDialog("Character Set Generated", message, "OK");
    }

    private readonly struct CharacterSetResult
    {
        public readonly string OutputPath;
        public readonly int CharacterCount;
        public readonly int ScannedFileCount;
        public readonly List<string> Warnings;

        public CharacterSetResult(string outputPath, int characterCount, int scannedFileCount, List<string> warnings)
        {
            OutputPath = outputPath;
            CharacterCount = characterCount;
            ScannedFileCount = scannedFileCount;
            Warnings = warnings;
        }
    }
}
