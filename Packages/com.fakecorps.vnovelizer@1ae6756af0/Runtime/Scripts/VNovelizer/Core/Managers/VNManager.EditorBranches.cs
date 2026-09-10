#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using VNovelizer.Core.Diagnostics;

namespace VNovelizer.Core.Diagnostics
{
    public sealed class VNScriptDebugBranchOption
    {
        public int Index { get; }
        public VNScriptDebugLine Line { get; }
        public string Condition { get; }
        public bool? Matches { get; }
        internal StoryLine Source { get; }
        internal string Note { get; }

        public VNScriptDebugBranchOption(int index, StoryLine source, string condition, bool? matches)
        {
            Index = index;
            Source = source;
            Note = source?.Note;
            Line = new VNScriptDebugLine(source);
            Condition = condition;
            Matches = matches;
        }
    }

    /// <summary>A one-use request token. null target means evaluate the original conditions.</summary>
    public sealed class VNScriptDebugBranchPause
    {
        public Guid Token { get; } = Guid.NewGuid();
        public string ScriptName { get; }
        public int ParentIndex { get; }
        public string ParentID { get; }
        public string Tags { get; }
        public IReadOnlyList<VNScriptDebugBranchOption> Options { get; }
        public bool ResumeQueued { get; private set; }
        private bool consumed;
        private int? target;

        public VNScriptDebugBranchPause(string script, int parent, string parentID, string tags,
            IEnumerable<VNScriptDebugBranchOption> options)
        {
            ScriptName = script;
            ParentIndex = parent;
            ParentID = parentID;
            Tags = tags;
            Options = new List<VNScriptDebugBranchOption>(options).AsReadOnly();
        }

        public bool TryQueueResume(Guid token, int? targetIndex)
        {
            if (Token != token || ResumeQueued || consumed) return false;
            if (targetIndex.HasValue)
            {
                bool found = false;
                foreach (var option in Options) if (option.Index == targetIndex.Value) found = true;
                if (!found) return false;
            }
            target = targetIndex;
            ResumeQueued = true;
            return true;
        }

        public bool TryConsumeResume(out int? targetIndex)
        {
            targetIndex = null;
            if (!ResumeQueued || consumed) return false;
            consumed = true;
            targetIndex = target;
            return true;
        }
    }
}

public partial class VNManager
{
    private bool debugBranchMode;
    private VNScriptDebugBranchPause debugBranchPause;
    private List<StoryLine> debugBranchSource;
    private StoryLine debugBranchParent;
    private string debugBranchParentNote;
    private bool debugBranchSkipAnimations;
    private int debugBypassBranchIndex = -1;

    public static bool DebugBranchSceneIsActive => Application.isPlaying &&
        SceneManager.GetActiveScene().name == "VNDebugScene";
    public bool DebugIsWaitingForBranch => debugBranchPause != null;
    public VNScriptDebugBranchPause DebugBranchPause => debugBranchPause;

    public void DebugSetBranchMode(bool enabled)
    {
        debugBranchMode = enabled && DebugBranchSceneIsActive;
        if (!debugBranchMode && debugBranchPause != null)
            debugBranchPause.TryQueueResume(debugBranchPause.Token, null);
    }

    public void DebugCancelBranchRequest()
    {
        debugBranchPause = null;
        debugBranchSource = null;
        debugBranchParent = null;
        debugBypassBranchIndex = -1;
        if (debugPhase == "等待手选分支")
        {
            debugPhase = "分支调试已取消";
            debugCurrentIndex = -1;
        }
    }

    private bool DebugTryPauseBranch(bool skipAnimations)
    {
        if (debugBypassBranchIndex == CurrentLineIndex)
        {
            debugBypassBranchIndex = -1;
            return false;
        }
        if (debugBranchPause != null) return true;
        if (!debugBranchMode || !DebugBranchSceneIsActive) return false;

        var options = DebugGetBranchOptions(CurrentLineIndex);
        var tags = new HashSet<string>(recordedScriptTags);
        // Match HasRecordedScriptTag: include true flags restored by other runtime paths.
        var flags = GlobalDataManager.GetInstance().GetGlobalData()?.Flags;
        if (flags != null)
            foreach (var pair in flags)
                if (pair.Value && pair.Key.StartsWith(ScriptTagFlagPrefix, StringComparison.Ordinal))
                    tags.Add(pair.Key.Substring(ScriptTagFlagPrefix.Length));
        var sortedTags = new List<string>(tags);
        sortedTags.Sort(StringComparer.Ordinal);
        debugBranchSource = StoryLines;
        debugBranchParent = StoryLines[CurrentLineIndex];
        debugBranchParentNote = debugBranchParent.Note;
        debugBranchSkipAnimations = skipAnimations;
        debugBranchPause = new VNScriptDebugBranchPause(currentScriptName, CurrentLineIndex,
            debugBranchParent.ID, string.Join(", ", sortedTags), options);
        if (_autoPlayCoroutine != null)
        {
            MonoManager.GetInstance().StopCoroutine(_autoPlayCoroutine);
            _autoPlayCoroutine = null;
        }
        debugCurrentIndex = CurrentLineIndex;
        debugScene = SceneManager.GetActiveScene();
        debugPhase = "等待手选分支";
        DebugNotify();
        return true;
    }

    private IEnumerable<int> DebugGetDirectBranchIndices(int parentIndex)
    {
        if (!TryGetLineLevel(StoryLines[parentIndex], out int parentLevel)) yield break;
        for (int i = parentIndex + 1; i < StoryLines.Count; i++)
        {
            if (!TryGetLineLevel(StoryLines[i], out int level)) continue;
            if (level <= parentLevel) yield break;
            if (level == parentLevel + 1) yield return i;
        }
    }

    private List<VNScriptDebugBranchOption> DebugGetBranchOptions(int parentIndex)
    {
        var options = new List<VNScriptDebugBranchOption>();
        foreach (int i in DebugGetDirectBranchIndices(parentIndex))
        {
            StoryLine line = StoryLines[i];
            bool? matches;
            string condition;
            if (TryEvaluateWhereList(line.Note, out bool match, out string summary))
            {
                matches = match;
                condition = summary;
            }
            else if (!string.IsNullOrWhiteSpace(GetNoteValue(line.Note, "wherelist")))
            {
                matches = null;
                condition = "无法解析条件（正常判定会跳过；仍可手选）";
            }
            else
            {
                var legacy = new List<string>(ExtractConditionTags(line.Note));
                if (legacy.Count == 0)
                {
                    matches = null;
                    condition = "无条件回退（无其他分支命中时使用首个回退分支）";
                }
                else
                {
                    bool any = false;
                    foreach (string tag in legacy) any |= HasRecordedScriptTag(tag);
                    matches = any;
                    condition = "旧版 OR: " + string.Join(", ", legacy);
                }
            }
            options.Add(new VNScriptDebugBranchOption(i, line, condition, matches));
        }
        return options;
    }

    public bool DebugQueueBranchResume(Guid token, int? targetIndex)
    {
        return DebugBranchRequestIsValid() && debugBranchPause.TryQueueResume(token, targetIndex);
    }

    private bool DebugBranchRequestIsValid()
    {
        if (debugBranchPause == null || !DebugBranchSceneIsActive || !debugScene.IsValid() || !debugScene.isLoaded)
            return false;
        int index = debugBranchPause.ParentIndex;
        if (!ReferenceEquals(StoryLines, debugBranchSource) || currentScriptName != debugBranchPause.ScriptName ||
            CurrentLineIndex != index || index < 0 || index >= StoryLines.Count ||
            !ReferenceEquals(StoryLines[index], debugBranchParent) || debugBranchParent.Note != debugBranchParentNote)
            return false;
        var directChildren = new HashSet<int>(DebugGetDirectBranchIndices(index));
        if (directChildren.Count != debugBranchPause.Options.Count) return false;
        foreach (var option in debugBranchPause.Options)
            if (!directChildren.Contains(option.Index) || option.Index < 0 || option.Index >= StoryLines.Count ||
                !ReferenceEquals(StoryLines[option.Index], option.Source) || option.Source.Note != option.Note)
                return false;
        return true;
    }

    /// <summary>Called by the editor update pump, never from OnGUI.</summary>
    public void DebugProcessBranchRequest()
    {
        if (debugBranchPause == null) return;
        if (!DebugBranchRequestIsValid())
        {
            DebugCancelBranchRequest();
            DebugNotify();
            return;
        }
        if (!debugBranchPause.TryConsumeResume(out int? target)) return;
        bool skipAnimations = debugBranchSkipAnimations;
        int parent = debugBranchPause.ParentIndex;
        DebugCancelBranchRequest();
        debugPhase = null;
        if (target.HasValue) CurrentLineIndex = target.Value;
        else debugBypassBranchIndex = parent;
        if (skipAnimations) PlayCurrentLineImmediately();
        else PlayCurrentLine();
    }
}
#endif