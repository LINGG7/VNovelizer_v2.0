using System;
using UnityEditor;
using UnityEngine;
using VNovelizer.Core.Diagnostics;

namespace VNovelizer.Editor
{
    /// <summary>A read-only, fixed-row virtualized view of the running script.</summary>
    public sealed class VNScriptPositionWindow : EditorWindow
    {
        private const float RowHeight = 24f;
        [SerializeField] private bool followExecution = true;
        private VNManager manager;
        private VNScriptDebugSnapshot snapshot;
        private Vector2 listScroll;
        private Vector2 detailScroll;
        private int selectedIndex = -1;
        private bool scrollToCurrent;
        private bool acceptingPlayUpdates;
        private bool dirty = true;
        private double nextRefresh;
        private GUIStyle detailStyle;
        private bool branchMode = true;
        private VNScriptDebugBranchPause branchPause;
        private Vector2 branchScroll;

        [MenuItem("Tools/VNovelizer/剧本定位")]
        public static void Open()
        {
            GetWindow<VNScriptPositionWindow>("剧本定位").Show();
        }

        private void OnEnable()
        {
            minSize = new Vector2(500, 360);
            acceptingPlayUpdates = EditorApplication.isPlaying;
            EditorApplication.update += UpdateSnapshot;
            EditorApplication.playModeStateChanged += OnPlayModeChanged;
            AssemblyReloadEvents.beforeAssemblyReload += DetachManager;
            dirty = true;
            UpdateSnapshot();
        }

        private void OnDisable()
        {
            EditorApplication.update -= UpdateSnapshot;
            EditorApplication.playModeStateChanged -= OnPlayModeChanged;
            AssemblyReloadEvents.beforeAssemblyReload -= DetachManager;
            manager?.DebugSetBranchMode(false);
            DetachManager();
        }

        private void DetachManager()
        {
            if (manager != null) manager.DebugPositionChanged -= MarkDirty;
            manager = null;
        }

        private void MarkDirty() => dirty = true;

        private void OnPlayModeChanged(PlayModeStateChange state)
        {
            acceptingPlayUpdates = state == PlayModeStateChange.EnteredPlayMode;
            branchMode = true;
            branchPause = null;
            DetachManager();
            snapshot = default;
            selectedIndex = -1;
            listScroll = detailScroll = Vector2.zero;
            dirty = true;
            UpdateSnapshot();
            Repaint();
        }

        private void UpdateSnapshot()
        {
            if (!acceptingPlayUpdates || !EditorApplication.isPlaying) return;
            VNManager active = VNManager.DebugInstance;
            if (!ReferenceEquals(active, manager))
            {
                DetachManager();
                manager = active;
                if (manager != null) manager.DebugPositionChanged += MarkDirty;
                dirty = true;
            }
            manager?.DebugSetBranchMode(branchMode);
            if (!dirty && EditorApplication.timeSinceStartup < nextRefresh) return;
            nextRefresh = EditorApplication.timeSinceStartup + 0.1;
            dirty = false;
            if (manager == null)
            {
                if (snapshot.Lines != null) { snapshot = default; selectedIndex = -1; }
                Repaint();
                return;
            }

            var nextBranch = manager.DebugBranchPause;
            bool branchChanged = !ReferenceEquals(branchPause, nextBranch);
            branchPause = nextBranch;
            if (branchChanged) branchScroll = Vector2.zero;
            var next = manager.GetDebugSnapshot();
            bool scriptChanged = !ReferenceEquals(next.Lines, snapshot.Lines);
            bool positionChanged = scriptChanged || next.CurrentIndex != snapshot.CurrentIndex;
            bool changed = branchChanged || positionChanged || next.Status != snapshot.Status || next.ScriptName != snapshot.ScriptName;
            snapshot = next;
            if (scriptChanged)
            {
                selectedIndex = -1;
                listScroll = detailScroll = Vector2.zero;
            }
            if (positionChanged && followExecution && snapshot.CurrentLine != null) GoToCurrent();
            if (changed) Repaint();
        }

        private void GoToCurrent()
        {
            if (snapshot.CurrentLine == null) return;
            selectedIndex = snapshot.CurrentIndex;
            detailScroll = Vector2.zero;
            scrollToCurrent = true;
            Repaint();
        }

        private void OnGUI()
        {
            using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
            {
                bool follow = GUILayout.Toggle(followExecution, "跟随执行", EditorStyles.toolbarButton);
                if (follow != followExecution)
                {
                    followExecution = follow;
                    if (follow) GoToCurrent();
                }
                using (new EditorGUI.DisabledScope(snapshot.Lines == null || snapshot.CurrentLine == null || !acceptingPlayUpdates))
                {
                    if (GUILayout.Button("回到当前语句", EditorStyles.toolbarButton)) GoToCurrent();
                    if (GUILayout.Button("复制当前位置", EditorStyles.toolbarButton))
                        EditorGUIUtility.systemCopyBuffer = snapshot.CopyPositionText();
                }
                using (new EditorGUI.DisabledScope(!acceptingPlayUpdates || !VNManager.DebugBranchSceneIsActive))
                {
                    branchMode = GUILayout.Toggle(branchMode, new GUIContent("exeBC 手选调试", "仅 VNDebugScene：遇到 exeBC 暂停，手选本次路线，不补写 tag。"), EditorStyles.toolbarButton);
                }
                GUILayout.FlexibleSpace();
            }

            if (!acceptingPlayUpdates || !EditorApplication.isPlaying)
            {
                EditorGUILayout.HelpBox("未运行。进入 Play 模式，并在 VNDebugScene 中启动剧本。", MessageType.Info);
                return;
            }
            if (manager == null || snapshot.Lines == null)
            {
                EditorGUILayout.HelpBox("未找到执行器，等待启动剧本。", MessageType.Info);
                return;
            }

            EditorGUILayout.LabelField("剧本", snapshot.ScriptName);
            EditorGUILayout.LabelField("状态", EditorApplication.isPaused ? "编辑器已暂停 / " + snapshot.Status : snapshot.Status);
            EditorGUILayout.LabelField("当前语句 ID", snapshot.CurrentLine?.ID ?? "—");
            EditorGUILayout.LabelField("源文件行号", "不可用（解析器未保留源文件行号）");
            EditorGUILayout.LabelField($"已加载 {snapshot.Lines.Count} 条  ·  ▶ 当前执行  ·  蓝色为选中查看", EditorStyles.miniLabel);

            if (snapshot.Lines.Count == 0)
            {
                EditorGUILayout.HelpBox(snapshot.Status, MessageType.Info);
                return;
            }

            float detailHeight = Mathf.Clamp(position.height * 0.3f, 95f, 230f);
            Rect viewport = GUILayoutUtility.GetRect(0, 100000, 60, 100000,
                GUILayout.ExpandWidth(true), GUILayout.ExpandHeight(true));
            DrawVirtualList(viewport);

            if (branchPause != null)
            {
                DrawBranchControls(detailHeight);
                return;
            }

            EditorGUILayout.LabelField("选中语句完整内容（源剧本）", EditorStyles.boldLabel);
            detailScroll = EditorGUILayout.BeginScrollView(detailScroll, GUILayout.Height(detailHeight));
            if (selectedIndex >= 0 && selectedIndex < snapshot.Lines.Count)
            {
                if (detailStyle == null) detailStyle = new GUIStyle(EditorStyles.textArea) { wordWrap = true };
                // SelectableLabel can use focused/on states; set all states for consistent contrast.
                Color detailTextColor = EditorGUIUtility.isProSkin
                    ? new Color(0.9f, 0.9f, 0.9f)
                    : new Color(0.1f, 0.1f, 0.1f);
                detailStyle.normal.textColor = detailTextColor;
                detailStyle.hover.textColor = detailTextColor;
                detailStyle.active.textColor = detailTextColor;
                detailStyle.focused.textColor = detailTextColor;
                detailStyle.onNormal.textColor = detailTextColor;
                detailStyle.onHover.textColor = detailTextColor;
                detailStyle.onActive.textColor = detailTextColor;
                detailStyle.onFocused.textColor = detailTextColor;
                string content = snapshot.Lines[selectedIndex].FullContent;
                float height = detailStyle.CalcHeight(new GUIContent(content), Mathf.Max(100, position.width - 40));
                EditorGUILayout.SelectableLabel(content, detailStyle, GUILayout.Height(Mathf.Max(height, detailHeight - 8)));
            }
            else EditorGUILayout.LabelField("选择一条语句查看完整内容。");
            EditorGUILayout.EndScrollView();
        }

        private void DrawBranchControls(float height)
        {
            EditorGUILayout.LabelField("exeBC " + branchPause.ParentID + " · 等待手选分支", EditorStyles.boldLabel);
            using (new EditorGUI.DisabledScope(branchPause.ResumeQueued))
            {
                if (GUILayout.Button("按原条件继续"))
                {
                    manager.DebugQueueBranchResume(branchPause.Token, null);
                    Repaint();
                }
                branchScroll = EditorGUILayout.BeginScrollView(branchScroll, GUILayout.Height(height));
                EditorGUILayout.LabelField("当前 tag: " + (string.IsNullOrEmpty(branchPause.Tags) ? "（无）" : branchPause.Tags), EditorStyles.wordWrappedLabel);
                if (branchPause.Options.Count == 0)
                    EditorGUILayout.HelpBox("未找到可手选的直接子分支，请按原条件继续。", MessageType.Info);
                foreach (var option in branchPause.Options)
                {
                    using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
                    {
                        using (new EditorGUILayout.HorizontalScope())
                        {
                            string match = option.Matches.HasValue ? (option.Matches.Value ? "条件满足" : "条件不满足") : "特殊条件";
                            EditorGUILayout.LabelField("ID: " + option.Line.ID + " · " + match, EditorStyles.boldLabel);
                            if (GUILayout.Button("本次进入", GUILayout.Width(78)))
                            {
                                manager.DebugQueueBranchResume(branchPause.Token, option.Index);
                                Repaint();
                            }
                        }
                        EditorGUILayout.LabelField(option.Line.Speaker + "  " + option.Line.Summary, EditorStyles.wordWrappedLabel);
                        EditorGUILayout.LabelField(option.Condition, EditorStyles.wordWrappedLabel);
                    }
                }
                EditorGUILayout.EndScrollView();
            }
        }
        private void DrawVirtualList(Rect viewport)
        {
            int count = snapshot.Lines.Count;
            float width = Mathf.Max(100, viewport.width - 18);
            if (scrollToCurrent && viewport.height > 0 && Event.current.type == EventType.Repaint)
            {
                listScroll.y = Mathf.Clamp(snapshot.CurrentIndex * RowHeight - viewport.height * 0.5f,
                    0, Mathf.Max(0, count * RowHeight - viewport.height));
                scrollToCurrent = false;
            }
            listScroll = GUI.BeginScrollView(viewport, listScroll, new Rect(0, 0, width, count * RowHeight));
            int first = Mathf.Clamp(Mathf.FloorToInt(listScroll.y / RowHeight), 0, count - 1);
            int end = Mathf.Min(count, first + Mathf.CeilToInt(viewport.height / RowHeight) + 1);
            for (int i = first; i < end; i++)
            {
                var line = snapshot.Lines[i];
                Rect row = new Rect(0, i * RowHeight, width, RowHeight);
                bool current = i == snapshot.CurrentIndex;
                if (i == selectedIndex) EditorGUI.DrawRect(row, new Color(0.2f, 0.45f, 0.75f, 0.35f));
                else if ((i & 1) == 0) EditorGUI.DrawRect(row, new Color(0.5f, 0.5f, 0.5f, 0.08f));
                if (current) EditorGUI.DrawRect(new Rect(0, row.y, 3, RowHeight), new Color(0.2f, 0.8f, 0.45f));
                GUI.Label(new Rect(5, row.y + 2, 125, RowHeight - 4),
                    new GUIContent((current ? "▶ " : "") + "ID: " + line.ID, line.ID));
                GUI.Label(new Rect(132, row.y + 2, 90, RowHeight - 4), new GUIContent(line.Speaker, line.Speaker));
                GUI.Label(new Rect(226, row.y + 2, Mathf.Max(10, width - 230), RowHeight - 4), line.Summary);
                if (Event.current.type == EventType.MouseDown && Event.current.button == 0 && row.Contains(Event.current.mousePosition))
                {
                    selectedIndex = i;
                    detailScroll = Vector2.zero;
                    GUI.FocusControl(null);
                    Event.current.Use();
                    Repaint();
                }
            }
            GUI.EndScrollView();
        }
    }
}
namespace VNovelizer.Editor
{
    // This pump outlives the window so closing it can resume the original branch safely.
    [InitializeOnLoad]
    internal static class VNBranchDebugUpdate
    {
        static VNBranchDebugUpdate()
        {
            EditorApplication.update += Update;
            EditorApplication.playModeStateChanged += OnPlayModeChanged;
            AssemblyReloadEvents.beforeAssemblyReload += Cancel;
        }

        private static void Update()
        {
            if (EditorApplication.isPlaying && !EditorApplication.isPaused)
                VNManager.DebugInstance?.DebugProcessBranchRequest();
        }

        private static void OnPlayModeChanged(PlayModeStateChange state)
        {
            if (state == PlayModeStateChange.ExitingPlayMode || state == PlayModeStateChange.EnteredEditMode) Cancel();
        }

        private static void Cancel()
        {
            var manager = VNManager.DebugInstance;
            if (manager == null) return;
            manager.DebugCancelBranchRequest();
            manager.DebugSetBranchMode(false);
        }
    }
}