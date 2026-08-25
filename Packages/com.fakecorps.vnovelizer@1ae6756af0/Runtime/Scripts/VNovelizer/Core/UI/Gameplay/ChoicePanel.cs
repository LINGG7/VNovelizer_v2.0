using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class ChoicePanel : BasePanel
{
    private Transform container;
    private GameObject choiceItemPrefab;
    private List<GameObject> activeItems = new List<GameObject>();
    private CanvasGroup canvasGroup;

    protected override void Awake()
    {
        base.Awake();
        container = transform.Find("ChoiceContainer");
        canvasGroup = GetComponent<CanvasGroup>();
        if (canvasGroup == null)
            canvasGroup = gameObject.AddComponent<CanvasGroup>();
        // 记得把路径配置到 VNProjectConfig 或者硬编码
        choiceItemPrefab = ResourcesManager.GetInstance().Load<GameObject>(VNProjectConfig.Instance.UI_ChoicePath + "/ChoiceItem");
    }

    /// <summary>
    /// 显示选项
    /// </summary>
    /// <param name="choices">选项数据列表 (Text, CommandString)</param>
    public void ShowChoices(List<ChoiceData> choices)
    {
        EnsurePanelVisible();

        // 清理旧按钮
        foreach (var item in activeItems) Destroy(item);
        activeItems.Clear();

        // 生成新按钮
        foreach (var data in choices)
        {
            GameObject btnObj = Instantiate(choiceItemPrefab, container);
            activeItems.Add(btnObj);

            SetupChoiceItem(btnObj, data.Text, data.Command, data.SelectedFlagKey);
        }

        ShowMe();
    }

    private void OnChoiceClicked(string command, string selectedFlagKey)
    {
        if (!string.IsNullOrEmpty(selectedFlagKey))
        {
            GlobalDataManager.GetInstance().SetBoolFlag(selectedFlagKey, true);
        }

        // 关闭面板
        UIManager.GetInstance().HidePanel("ChoicePanel");

        GameStateManager.GetInstance().SetState(GameState.Gameplay);

        // 注意：这里需要调用 VNManager 或 CommandManager 来执行
        if (!string.IsNullOrEmpty(command))
        {

            VNManager.GetInstance().ExecuteChoiceCommand(command);
        }
        else
        {
            // 如果选项没配命令（比如只是“继续”），那就直接下一行
            VNManager.GetInstance().NextLine();
        }
    }

    // 在 ChoicePanel.cs 中添加/修改

    public void AddChoice(string text, string command)
    {
        AddChoice(text, command, "");
    }

    public void AddChoice(string text, string command, string selectedFlagKey)
    {
        EnsurePanelVisible();

        // 确保 Container 存在
        if (container == null) container = transform.Find("ChoiceContainer");
        if (choiceItemPrefab == null)
            choiceItemPrefab = ResourcesManager.GetInstance().Load<GameObject>(VNProjectConfig.Instance.UI_ChoicePath + "/ChoiceItem");

        if (choiceItemPrefab == null || container == null)
        {
            Debug.LogError("[ChoicePanel] Choice prefab or container is missing.");
            return;
        }

        GameObject btnObj = Instantiate(choiceItemPrefab, container);
        activeItems.Add(btnObj);
        btnObj.SetActive(true);

        SetupChoiceItem(btnObj, text, command, selectedFlagKey);

        LayoutRebuilder.ForceRebuildLayoutImmediate(container as RectTransform);
        ShowMe();
    }

    private void SetupChoiceItem(GameObject btnObj, string text, string command, string selectedFlagKey)
    {
        TMP_Text textComp = btnObj.GetComponentInChildren<TMP_Text>();
        if (textComp != null) textComp.text = text;

        Transform selected = FindChildRecursive(btnObj.transform, "Selected");
        if (selected != null)
        {
            bool isSelected = !string.IsNullOrEmpty(selectedFlagKey) &&
                              GlobalDataManager.GetInstance().GetBoolFlag(selectedFlagKey);
            selected.gameObject.SetActive(isSelected);
        }

        Button btn = btnObj.GetComponent<Button>();
        if (btn == null)
        {
            Debug.LogWarning("[ChoicePanel] Choice item is missing a Button component.");
            return;
        }

        btn.onClick.RemoveAllListeners();
        btn.onClick.AddListener(() => OnChoiceClicked(command, selectedFlagKey));
    }

    private Transform FindChildRecursive(Transform root, string childName)
    {
        if (root == null) return null;

        Transform directChild = root.Find(childName);
        if (directChild != null) return directChild;

        foreach (Transform child in root)
        {
            Transform found = FindChildRecursive(child, childName);
            if (found != null) return found;
        }

        return null;
    }

    private void EnsurePanelVisible()
    {
        gameObject.SetActive(true);
        transform.SetAsLastSibling();

        if (canvasGroup == null)
            canvasGroup = GetComponent<CanvasGroup>();
        if (canvasGroup == null)
            canvasGroup = gameObject.AddComponent<CanvasGroup>();

        canvasGroup.alpha = 1f;
        canvasGroup.interactable = true;
        canvasGroup.blocksRaycasts = true;
    }

}

// 简单的数据结构
public class ChoiceData
{
    public string Text;
    public string Command;
    public string SelectedFlagKey;
}
