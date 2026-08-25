using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

public static class ScriptParser
{
    public class ScriptData
    {
        public List<StoryLine> Lines = new List<StoryLine>();
        public Dictionary<string, int> IDMap = new Dictionary<string, int>();
    }

    public static ScriptData Parse(string fileName)
    {
        string loadPath = GetLoadPath(fileName);
        Debug.Log($"[ScriptParser] Loading script: {loadPath}");

        TextAsset csvFile = Resources.Load<TextAsset>(loadPath);
        if (csvFile == null)
        {
            csvFile = ResourcesManager.GetInstance().Load<TextAsset>(loadPath);
        }

        return ParseTextAsset(loadPath, csvFile);
    }

    public static IEnumerator ParseAsync(string fileName, Action<ScriptData> callback)
    {
        string loadPath = GetLoadPath(fileName);
        Debug.Log($"[ScriptParser] Loading script async: {loadPath}");

        TextAsset csvFile = Resources.Load<TextAsset>(loadPath);
        if (csvFile != null)
        {
            Debug.Log($"[ScriptParser] Loaded local script from Resources: {loadPath}");
            callback?.Invoke(ParseTextAsset(loadPath, csvFile));
            yield break;
        }

        bool completed = false;

        ResourcesManager.GetInstance().LoadAsync<TextAsset>(loadPath, asset =>
        {
            csvFile = asset;
            completed = true;
        });

        while (!completed)
        {
            yield return null;
        }

        callback?.Invoke(ParseTextAsset(loadPath, csvFile));
    }

    private static string GetLoadPath(string fileName)
    {
        string configPath = VNProjectConfig.Instance.VNScriptResPath;
        return configPath + "/" + fileName;
    }

    private static ScriptData ParseTextAsset(string loadPath, TextAsset csvFile)
    {
        ScriptData data = new ScriptData();

        if (csvFile == null)
        {
            Debug.LogError($"[ScriptParser] Script file not found: {loadPath}");
            return null;
        }

        string[] lines = SplitCSVLines(csvFile.text);
        bool isFirstLine = true;

        for (int i = 0; i < lines.Length; i++)
        {
            string line = lines[i].Trim();
            if (string.IsNullOrEmpty(line)) continue;

            if (isFirstLine)
            {
                isFirstLine = false;
                continue;
            }

            string[] columns = SplitCSV(line);
            if (columns.Length >= 12)
            {
                StoryLine storyLine = new StoryLine
                {
                    ID = columns[0].Trim(),
                    Speaker = columns[1].Trim(),
                    HeadProfile = columns[2].Trim(),
                    CharLeft = columns[3].Trim(),
                    CharMid = columns[4].Trim(),
                    CharRight = columns[5].Trim(),
                    Text = columns[6].Trim(),
                    Background = columns[7].Trim(),
                    BGM = columns[8].Trim(),
                    Voice = columns[9].Trim(),
                    Command = columns[10].Trim(),
                    Note = columns[11].Trim()
                };

                data.Lines.Add(storyLine);

                if (!string.IsNullOrEmpty(storyLine.ID))
                {
                    data.IDMap[storyLine.ID] = data.Lines.Count - 1;
                }
            }
        }

        return data;
    }

    private static string[] SplitCSVLines(string csvContent)
    {
        List<string> lines = new List<string>();
        bool inQuotes = false;
        StringBuilder currentLine = new StringBuilder();

        for (int i = 0; i < csvContent.Length; i++)
        {
            char c = csvContent[i];
            char nextChar = (i + 1 < csvContent.Length) ? csvContent[i + 1] : '\0';

            if (c == '"')
            {
                if (inQuotes && nextChar == '"')
                {
                    // Preserve the escaped quote pair for SplitCSV. This method only
                    // separates records; decoding CSV field escapes here would make
                    // SplitCSV treat the restored JSON quotes as field delimiters.
                    currentLine.Append('"');
                    currentLine.Append('"');
                    i++;
                }
                else
                {
                    inQuotes = !inQuotes;
                    currentLine.Append(c);
                }
            }
            else if ((c == '\n' || c == '\r') && !inQuotes)
            {
                if (c == '\r' && nextChar == '\n')
                {
                    i++;
                }

                if (currentLine.Length > 0)
                {
                    lines.Add(currentLine.ToString());
                    currentLine.Clear();
                }
            }
            else
            {
                currentLine.Append(c);
            }
        }

        if (currentLine.Length > 0)
        {
            lines.Add(currentLine.ToString());
        }

        return lines.ToArray();
    }

    private static string[] SplitCSV(string line)
    {
        List<string> fields = new List<string>();
        bool inQuotes = false;
        StringBuilder currentField = new StringBuilder();

        for (int i = 0; i < line.Length; i++)
        {
            char c = line[i];
            char nextChar = (i + 1 < line.Length) ? line[i + 1] : '\0';

            if (c == '"')
            {
                if (inQuotes && nextChar == '"')
                {
                    currentField.Append('"');
                    i++;
                }
                else
                {
                    inQuotes = !inQuotes;
                }
            }
            else if (c == ',' && !inQuotes)
            {
                fields.Add(currentField.ToString());
                currentField.Clear();
            }
            else
            {
                currentField.Append(c);
            }
        }

        fields.Add(currentField.ToString());
        return fields.ToArray();
    }
}
