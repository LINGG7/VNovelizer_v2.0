using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;

public class DebugManager : MonoBehaviour
{
    [SerializeField] private Button startBtn;
    [SerializeField] private TMP_InputField scriptInput;
    [SerializeField] private TMP_InputField lineIdInput;
    [SerializeField] private TMP_InputField tagInput;
    [SerializeField] private bool ignoreChoiceWhenJumpToLine = false;
    [SerializeField] private string defaultScriptName = "sntzm_main1";
    [SerializeField] private string defaultDebugTags = "";

    private const string PREF_KEY_SCRIPT = "Debug_LastScriptName";
    private const string PREF_KEY_LINEID = "Debug_LastLineID";
    private const string PREF_KEY_TAGS = "Debug_LastTags";

    void Start()
    {
        EnsureTagInput();

        if (scriptInput != null)
        {
            string savedScriptName = NormalizeScriptName(PlayerPrefs.GetString(PREF_KEY_SCRIPT, defaultScriptName));
            scriptInput.text = ScriptExists(savedScriptName) ? savedScriptName : defaultScriptName;
        }

        if (lineIdInput != null)
            lineIdInput.text = PlayerPrefs.GetString(PREF_KEY_LINEID, "");

        if (tagInput != null)
            tagInput.text = PlayerPrefs.GetString(PREF_KEY_TAGS, defaultDebugTags);

        if (startBtn != null)
            startBtn.onClick.AddListener(OnStartDebug);

        Debug.Log(Application.persistentDataPath);
    }

    private void OnStartDebug()
    {
        string scriptName = NormalizeScriptName(scriptInput != null ? scriptInput.text : "");
        string lineID = lineIdInput != null ? lineIdInput.text.Trim() : "";
        string debugTags = tagInput != null ? tagInput.text.Trim() : defaultDebugTags;

        if (string.IsNullOrEmpty(scriptName))
        {
            Debug.LogError("[DebugManager] Please enter a script name.");
            return;
        }

        if (!ScriptExists(scriptName))
        {
            Debug.LogError($"[DebugManager] Script not found: {scriptName}. Expected path: Resources/{VNProjectConfig.Instance.VNScriptResPath}/{scriptName}.csv");
            return;
        }

        PlayerPrefs.SetString(PREF_KEY_SCRIPT, scriptName);
        PlayerPrefs.SetString(PREF_KEY_LINEID, lineID);
        PlayerPrefs.SetString(PREF_KEY_TAGS, debugTags);
        PlayerPrefs.Save();

        VNManager.GetInstance().SetDebugScriptTags(ParseTags(debugTags));
        HideDebugControls();
        VNManager.GetInstance().StartGameOnScene(scriptName, lineID, null, ignoreChoiceWhenJumpToLine);
    }

    private string NormalizeScriptName(string scriptName)
    {
        if (string.IsNullOrWhiteSpace(scriptName))
            return "";

        scriptName = scriptName.Trim();
        if (scriptName.EndsWith(".csv", System.StringComparison.OrdinalIgnoreCase))
            scriptName = scriptName.Substring(0, scriptName.Length - 4);

        return scriptName;
    }

    private bool ScriptExists(string scriptName)
    {
        if (string.IsNullOrWhiteSpace(scriptName))
            return false;

        string path = VNProjectConfig.Instance.VNScriptResPath + "/" + scriptName;
        return Resources.Load<TextAsset>(path) != null;
    }

    private IEnumerable<string> ParseTags(string tagText)
    {
        if (string.IsNullOrWhiteSpace(tagText))
            yield break;

        char[] separators = { ',', ';', '\uFF0C', '\uFF1B', '|', ' ', '\t', '\r', '\n' };
        string[] tags = tagText.Split(separators, System.StringSplitOptions.RemoveEmptyEntries);
        foreach (string rawTag in tags)
        {
            string tag = rawTag.Trim();
            if (!string.IsNullOrEmpty(tag))
                yield return tag;
        }
    }

    private void EnsureTagInput()
    {
        if (tagInput != null || lineIdInput == null || lineIdInput.transform.parent == null)
            return;

        Transform lineRow = lineIdInput.transform.parent;
        GameObject tagRow = Instantiate(lineRow.gameObject, lineRow.parent);
        tagRow.name = "Tags";
        tagRow.transform.SetSiblingIndex(lineRow.GetSiblingIndex() + 1);
        PositionGeneratedTagRow(lineRow, tagRow.transform);

        tagInput = tagRow.GetComponentInChildren<TMP_InputField>(true);
        TMP_Text placeholderText = null;
        if (tagInput != null)
        {
            tagInput.text = "";
            tagInput.contentType = TMP_InputField.ContentType.Standard;
            tagInput.lineType = TMP_InputField.LineType.SingleLine;
            tagInput.characterLimit = 0;

            placeholderText = tagInput.placeholder as TMP_Text;
            if (placeholderText != null)
                placeholderText.text = "Tags";

            if (tagInput.textComponent != null)
                tagInput.textComponent.text = "";
        }

        TMP_Text[] labels = tagRow.GetComponentsInChildren<TMP_Text>(true);
        foreach (TMP_Text label in labels)
        {
            if (tagInput != null && (label == tagInput.textComponent || label == placeholderText))
                continue;

            if (!string.IsNullOrWhiteSpace(label.text))
            {
                label.text = "Tags";
                break;
            }
        }
    }

    private void PositionGeneratedTagRow(Transform lineRow, Transform tagRow)
    {
        RectTransform lineRect = lineRow as RectTransform;
        RectTransform tagRect = tagRow as RectTransform;
        if (lineRect == null || tagRect == null)
            return;

        LayoutGroup layoutGroup = lineRow.parent != null ? lineRow.parent.GetComponent<LayoutGroup>() : null;
        if (layoutGroup != null)
        {
            LayoutRebuilder.ForceRebuildLayoutImmediate(lineRow.parent as RectTransform);
            return;
        }

        float rowGap = Mathf.Max(lineRect.rect.height, 36f) + 12f;
        tagRect.anchorMin = lineRect.anchorMin;
        tagRect.anchorMax = lineRect.anchorMax;
        tagRect.pivot = lineRect.pivot;
        tagRect.sizeDelta = lineRect.sizeDelta;
        tagRect.anchoredPosition = lineRect.anchoredPosition + new Vector2(0f, -rowGap);
    }

    private void HideDebugControls()
    {
        SetRowActive(scriptInput, false);
        SetRowActive(lineIdInput, false);
        SetRowActive(tagInput, false);

        if (startBtn != null)
            startBtn.gameObject.SetActive(false);
    }

    private void SetRowActive(TMP_InputField input, bool active)
    {
        if (input == null)
            return;

        Transform row = input.transform.parent;
        if (row != null)
            row.gameObject.SetActive(active);
        else
            input.gameObject.SetActive(active);
    }
}
