#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using VNovelizer.Core.Commands;
using VNovelizer.Core.Diagnostics;

namespace VNovelizer.Core.Diagnostics
{
    /// <summary>Immutable source data; runtime inheritance must not change the inspector's script.</summary>
    public sealed class VNScriptDebugLine
    {
        public string ID { get; }
        public string Speaker { get; }
        public string Text { get; }
        public string Command { get; }
        // The current CSV parser does not retain physical source-line mappings.
        public int? SourceLineNumber => null;
        public string FullContent { get; }
        public string Summary { get; }

        public VNScriptDebugLine(StoryLine line)
        {
            ID = line?.ID ?? "";
            Speaker = line?.Speaker ?? "";
            Text = line?.Text ?? "";
            Command = line?.Command ?? "";
            string preview = string.IsNullOrEmpty(Text) ? Command : Text;
            if (!string.IsNullOrEmpty(Text) && !string.IsNullOrEmpty(Command)) preview += "  |  " + Command;
            if (string.IsNullOrEmpty(preview)) preview = "（场景数据或分支控制）";
            Summary = preview.Replace('\r', ' ').Replace('\n', ' ');
            if (Summary.Length > 240) Summary = Summary.Substring(0, 240) + "…";
            FullContent = $"ID: {ID}\n角色: {Speaker}\n台词: {Text}\n指令: {Command}\n" +
                $"头像: {line?.HeadProfile}\n左立绘: {line?.CharLeft}\n中立绘: {line?.CharMid}\n右立绘: {line?.CharRight}\n" +
                $"背景: {line?.Background}\nBGM: {line?.BGM}\n语音: {line?.Voice}\n备注: {line?.Note}";
        }
    }

    public readonly struct VNScriptDebugSnapshot
    {
        public string ScriptName { get; }
        public IReadOnlyList<VNScriptDebugLine> Lines { get; }
        public int CurrentIndex { get; }
        public string Status { get; }
        public VNScriptDebugLine CurrentLine => Lines != null && CurrentIndex >= 0 && CurrentIndex < Lines.Count ? Lines[CurrentIndex] : null;

        public VNScriptDebugSnapshot(string scriptName, IReadOnlyList<VNScriptDebugLine> lines, int index, string status)
        {
            ScriptName = scriptName;
            Lines = lines;
            CurrentIndex = index;
            Status = status;
        }

        public string CopyPositionText() => CurrentLine == null ? "" :
            $"剧本: {ScriptName}\n语句 ID: {CurrentLine.ID}\n源文件行号: 不可用\n状态: {Status}\n{CurrentLine.FullContent}";
    }
}

public partial class VNManager
{
    // Never call GetInstance from an editor observer: it creates runtime objects.
    public static VNManager DebugInstance { get; private set; }
    public event Action DebugPositionChanged;
    private IReadOnlyList<VNScriptDebugLine> debugLines = Array.AsReadOnly(Array.Empty<VNScriptDebugLine>());
    private string debugScriptName = "";
    private string debugPhase = "等待加载";
    private int debugCurrentIndex = -1;
    private Scene debugScene;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetDebugSession()
    {
        DebugInstance?.DebugCancelBranchRequest();
        if (DebugInstance != null) DebugInstance.debugBranchMode = false;
        DebugInstance = null;
    }

    private void DebugNotify()
    {
        // An editor observer must never interrupt story execution.
        if (DebugPositionChanged == null) return;
        foreach (Action observer in DebugPositionChanged.GetInvocationList())
        {
            try { observer(); }
            catch (Exception exception) { Debug.LogException(exception); }
        }
    }

    public void DebugBeginLoading(string scriptName)
    {
        DebugCancelBranchRequest();
        DebugInstance = this;
        debugScriptName = scriptName ?? "";
        debugLines = Array.AsReadOnly(Array.Empty<VNScriptDebugLine>());
        debugCurrentIndex = -1;
        debugPhase = "等待加载";
        DebugNotify();
    }

    public void DebugLoadFailed(string scriptName)
    {
        DebugBeginLoading(scriptName);
        debugPhase = "剧本加载失败";
        DebugNotify();
    }

    private void DebugSetScriptData()
    {
        DebugCancelBranchRequest();
        DebugInstance = this;
        debugScriptName = currentScriptName ?? "";
        var rows = new List<VNScriptDebugLine>(StoryLines.Count);
        foreach (StoryLine line in StoryLines) rows.Add(new VNScriptDebugLine(line));
        debugLines = rows.AsReadOnly();
        debugCurrentIndex = -1;
        debugPhase = "已加载，等待执行";
        DebugNotify();
    }

    private void DebugBeginLine()
    {
        DebugInstance = this;
        debugCurrentIndex = CurrentLineIndex;
        debugScene = SceneManager.GetActiveScene();
        debugPhase = null;
        DebugNotify();
    }

    private void DebugEndExecution()
    {
        DebugCancelBranchRequest();
        debugCurrentIndex = -1;
        debugPhase = "执行结束";
        DebugNotify();
    }

    public VNScriptDebugSnapshot GetDebugSnapshot()
    {
        int index = debugCurrentIndex;
        string status = debugPhase;
        if (status == null)
        {
            var scene = debugScene;
            var panel = UIManager.GetInstance().GetPanel<VNGameplayPanel>("VNGameplayPanel");
            if (!scene.IsValid() || !scene.isLoaded || panel == null || !panel.gameObject.activeInHierarchy)
            {
                index = -1;
                status = "执行已停止（场景或游戏界面已关闭）";
            }
            else
            {
                GameState state = GameStateManager.GetInstance().CurrentState;
                if (isToBeContinuedPanelShowing) status = "待续";
                else if (state == GameState.Choice) status = "等待选择";
                else if (state != GameState.Gameplay && state != GameState.AutoPlay) status = "暂停 / " + state;
                else if (panel.IsTextTyping()) status = "打字中";
                else if (CommandManager.GetInstance().IsRunning || _flowCoroutine != null) status = "执行指令";
                else if (isAutoPlaying) status = "等待自动播放";
                else status = "等待点击";
            }
        }
        return new VNScriptDebugSnapshot(debugScriptName, debugLines, index, status);
    }
}
#endif