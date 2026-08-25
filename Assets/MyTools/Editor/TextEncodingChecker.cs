using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

public static class TextEncodingChecker
{
    private static readonly string[] ScanRoots =
    {
        "Assets",
        "Packages/com.fakecorps.vnovelizer@1ae6756af0"
    };

    private static readonly HashSet<string> TextExtensions = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        ".asmdef",
        ".asset",
        ".cginc",
        ".compute",
        ".cs",
        ".csv",
        ".json",
        ".mat",
        ".md",
        ".meta",
        ".prefab",
        ".shader",
        ".txt",
        ".unity",
        ".uss",
        ".uxml",
        ".xml",
        ".yaml"
    };

    private static readonly string[] MojibakeMarkers =
    {
        "\uFFFD",             // Replacement character.
        "\u93C3\u4F7A\u6AE7", // "pangbai" after bad decoding.
        "\u9473\u5C7E",       // "bei..." common bad decoding prefix.
        "\u93C3",
        "\u9473",
        "\u95BC",
        "\u95B9",
        "\u7E3E",
        "\u9420",
        "\u936B",
        "\u93C8",
        "\u95BA",
        "\u5A11\u64C3",
    };

    [MenuItem("Tools/Encoding Checker/Scan Text Assets")]
    public static void ScanTextAssets()
    {
        List<string> invalidUtf8Files = new List<string>();
        List<string> suspiciousFiles = new List<string>();
        int checkedCount = 0;

        foreach (string root in ScanRoots)
        {
            if (!Directory.Exists(root))
            {
                continue;
            }

            foreach (string file in Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories))
            {
                if (!ShouldScan(file))
                {
                    continue;
                }

                checkedCount++;

                byte[] bytes = File.ReadAllBytes(file);
                if (!TryDecodeUtf8(bytes, out string text, out string error))
                {
                    invalidUtf8Files.Add($"{NormalizePath(file)} :: {error}");
                    continue;
                }

                string suspiciousMarker = FindSuspiciousMarker(text, out int lineNumber, out string linePreview);
                if (!string.IsNullOrEmpty(suspiciousMarker))
                {
                    suspiciousFiles.Add($"{NormalizePath(file)}:{lineNumber} :: marker={EscapeForLog(suspiciousMarker)} :: {linePreview}");
                }
            }
        }

        WriteReport(checkedCount, invalidUtf8Files, suspiciousFiles);
    }

    private static bool ShouldScan(string file)
    {
        string normalized = NormalizePath(file);
        if (normalized.Contains("/Library/") ||
            normalized.Contains("/Temp/") ||
            normalized.Contains("/Obj/") ||
            normalized.Contains("/Build/") ||
            normalized.Contains("/Logs/"))
        {
            return false;
        }

        return TextExtensions.Contains(Path.GetExtension(file));
    }

    private static bool TryDecodeUtf8(byte[] bytes, out string text, out string error)
    {
        try
        {
            UTF8Encoding strictUtf8 = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true);
            text = strictUtf8.GetString(bytes);
            error = null;
            return true;
        }
        catch (DecoderFallbackException ex)
        {
            text = null;
            error = ex.Message;
            return false;
        }
    }

    private static string FindSuspiciousMarker(string text, out int lineNumber, out string linePreview)
    {
        lineNumber = 0;
        linePreview = null;

        foreach (string marker in MojibakeMarkers)
        {
            int index = text.IndexOf(marker, StringComparison.Ordinal);
            if (index >= 0)
            {
                lineNumber = GetLineNumber(text, index);
                linePreview = GetLinePreview(text, index);
                return marker;
            }
        }

        return null;
    }

    private static int GetLineNumber(string text, int index)
    {
        int line = 1;
        for (int i = 0; i < index && i < text.Length; i++)
        {
            if (text[i] == '\n')
            {
                line++;
            }
        }

        return line;
    }

    private static string GetLinePreview(string text, int index)
    {
        int start = text.LastIndexOf('\n', Math.Max(0, index));
        start = start < 0 ? 0 : start + 1;

        int end = text.IndexOf('\n', index);
        end = end < 0 ? text.Length : end;

        string line = text.Substring(start, end - start).Trim();
        const int maxLength = 160;
        if (line.Length > maxLength)
        {
            line = line.Substring(0, maxLength) + "...";
        }

        return line;
    }

    private static void WriteReport(int checkedCount, List<string> invalidUtf8Files, List<string> suspiciousFiles)
    {
        StringBuilder report = new StringBuilder();
        report.AppendLine("Text Encoding Checker Report");
        report.AppendLine($"Checked files: {checkedCount}");
        report.AppendLine($"Invalid UTF-8 files: {invalidUtf8Files.Count}");
        report.AppendLine($"Suspicious mojibake files: {suspiciousFiles.Count}");
        report.AppendLine();

        AppendSection(report, "Invalid UTF-8", invalidUtf8Files);
        AppendSection(report, "Suspicious Mojibake", suspiciousFiles);

        string reportPath = Path.Combine("Temp", "TextEncodingCheckerReport.txt");
        Directory.CreateDirectory(Path.GetDirectoryName(reportPath));
        File.WriteAllText(reportPath, report.ToString(), new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));

        if (invalidUtf8Files.Count == 0 && suspiciousFiles.Count == 0)
        {
            Debug.Log($"[TextEncodingChecker] OK. Checked {checkedCount} files. Report: {reportPath}");
        }
        else
        {
            Debug.LogWarning($"[TextEncodingChecker] Found {invalidUtf8Files.Count} invalid UTF-8 files and {suspiciousFiles.Count} suspicious files. Report: {reportPath}");
        }
    }

    private static void AppendSection(StringBuilder report, string title, List<string> items)
    {
        report.AppendLine($"[{title}]");
        if (items.Count == 0)
        {
            report.AppendLine("None");
            report.AppendLine();
            return;
        }

        foreach (string item in items)
        {
            report.AppendLine(item);
        }

        report.AppendLine();
    }

    private static string NormalizePath(string path)
    {
        return path.Replace('\\', '/');
    }

    private static string EscapeForLog(string value)
    {
        StringBuilder escaped = new StringBuilder();
        foreach (char ch in value)
        {
            escaped.Append("\\u");
            escaped.Append(((int)ch).ToString("X4"));
        }

        return escaped.ToString();
    }
}
