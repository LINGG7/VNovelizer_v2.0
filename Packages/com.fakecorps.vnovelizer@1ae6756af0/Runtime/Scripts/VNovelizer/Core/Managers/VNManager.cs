using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.IO;
using VNovelizer.Core.Commands;
using UnityEngine.SceneManagement;
using UnityEngine.Events;
using VNovelizer.Core.API; // 引用 API 以便调用 ClearAllEffects
using VNovelizer.Core.Localization;
using VNovelizer.Core;
using VNovelizer.Core.Diagnostics;
using System.Text.RegularExpressions;

/// <summary>
/// 视觉小说核心管理器 (终极预演版)
/// </summary>
public class VNManager : BaseManager<VNManager>
{
    private const int AutoSaveSlotIndex = 0;
    private const string ToBeContinuedNoteFlag = "ToBeContinued";
    private const string ToBeContinuedPanelPath = "VNovelizerRes/VNPrefabs/UI/ToBeContinued";
    private const string StartupBackgroundTaskID = "startup_background";

    // === 核心数据 ===
    public List<StoryLine> StoryLines { get; private set; } = new List<StoryLine>();
    public Dictionary<string, int> LineIDIndexMap { get; private set; } = new Dictionary<string, int>();

    // 当前行索引
    public int CurrentLineIndex { get; set; } = -1;

    // --- 状态变量 ---
    private StoryLine lastLine = null;
    private string currentBG = null;
    private string currentBGM = null;
    private string lastAutoSaveScreenshotPath = "";
    private string currentScriptName;
    private Dictionary<string, string> currentCharacters = new Dictionary<string, string>();
    private Dictionary<string, float> currentCharactersScaleX = new Dictionary<string, float>();
    private readonly HashSet<string> recordedScriptTags = new HashSet<string>();
    private bool isToBeContinuedPanelShowing;
    private int lastSelectBCLineIndex = -1;
    private readonly Dictionary<int, HashSet<string>> selectBCRecordedTagSnapshots = new Dictionary<int, HashSet<string>>();
    private readonly List<string> pendingDebugScriptTags = new List<string>();
    private bool hasPendingDebugScriptTags = false;
    private const string ScriptTagFlagPrefix = "vn.scriptTag.";
    /// <summary>读档后首帧播放：CSV 立绘列为空时，使用存档恢复的 currentCharacters，避免误清空槽位。</summary>
    private bool _usePersistedCharacterSlotsWhenCsvCharCellsEmpty;

    private readonly Dictionary<string, string> _dialogueEventScratch = new Dictionary<string, string>(2);
    private readonly Dictionary<string, string> _headProfileEventScratch = new Dictionary<string, string>(2);

    // 【新增】特效状态追踪
    private HashSet<string> activeEffects = new HashSet<string>();

    //【26-3-19新增】游戏界面加载回调
    private bool isGameplayPanelLoadCallbackFired = false;

    // 游戏状态
    private bool isAutoPlaying = false;
    private bool isSkipping = false;
    private bool isTextDisplaying = false;

    // 【新增】回放模式相关变量
    private bool isReplayMode = false;
    private string replayEndLineID = "";
    private bool wasMainMenuVisibleBeforeReplay = false; // 记录回放前主菜单是否可见

    // 跨场景数据
    private string pendingScriptName;
    private string pendingLineID;
    private bool pendingIgnoreChoiceWhenFastForward;
    private SaveData pendingSaveData; // 【新增】用于跨场景加载存档
    private SaveData currentLoadingSaveData; // 【新增】当前正在加载的存档数据
    private int currentLoadingTargetIndex; // 【新增】当前正在加载的目标行索引
    private bool isListeningSceneLoad = false;
    private UnityAction onGameStartedCallback; // 【新增】游戏启动完成后的回调

    // 配置
    private bool isVoiceEnabled = true;
    private bool isTextSpeedEnabled = true;

    // 协程
    private Coroutine _flowCoroutine;
    private Coroutine _autoPlayCoroutine;
    private Coroutine _autoSaveBackgroundPreviewCoroutine;

    //(3-29)文本行间转场
    private bool _advanceAfterCommandsRequested = false;
    private bool _stopCommandsForEndingChoiceRequested = false;
    private string _pendingPostTextCommand = "";
    private int _pendingPostTextCommandLineIndex = -1;

    public VNManager()
    {
        if (!isListeningSceneLoad)
        {
            SceneManager.sceneLoaded += OnSceneLoaded;
            isListeningSceneLoad = true;
        }

        MonoManager.GetInstance().AddApplicationQuitListener(OnApplicationQuit);
        MonoManager.GetInstance().AddApplicationPauseListener(OnApplicationPause);
    }

    /// <summary>
    /// 启动游戏（会切换到VNGamePlay场景）
    /// </summary>
    /// <param name="scriptFileName">剧本文件名</param>
    /// <param name="startLineID">起始行ID（可选）</param>
    /// <param name="onGameStarted">游戏启动完成后的回调函数（可选）</param>
    public void StartGame(string scriptFileName, string startLineID = "", UnityAction onGameStarted = null, bool ignoreChoiceWhenFastForward = false)
    {
        this.pendingScriptName = scriptFileName;
        this.pendingLineID = startLineID;
        this.pendingIgnoreChoiceWhenFastForward = ignoreChoiceWhenFastForward;
        this.onGameStartedCallback = onGameStarted;

        if (SceneManager.GetActiveScene().name != "VNGamePlay")
        {
            SceneManager.LoadScene("VNGamePlay");
        }
        else
        {
            RunGameLogic();
        }
    }

    /// <summary>
    /// 在当前场景中启动游戏（不切换场景）
    /// 会在当前场景检查并创建Canvas
    /// </summary>
    /// <param name="scriptFileName">剧本文件名</param>
    /// <param name="startLineID">起始行ID（可选）</param>
    /// <param name="onGameStarted">游戏启动完成后的回调函数（可选）</param>
    public void StartGameOnScene(string scriptFileName, string startLineID = "", UnityAction onGameStarted = null, bool ignoreChoiceWhenFastForward = false)
    {
        this.pendingScriptName = scriptFileName;
        this.pendingLineID = startLineID;
        this.pendingIgnoreChoiceWhenFastForward = ignoreChoiceWhenFastForward;
        this.onGameStartedCallback = onGameStarted;

        // 确保UIManager已初始化，这样会检查并创建Canvas
        UIManager.GetInstance().Init();

        // 直接运行游戏逻辑，不切换场景
        RunGameLogic();
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (scene.name == "VNGamePlay")
        {
            // 优先处理存档加载
            if (pendingSaveData != null)
            {
                ContinueGameInternal(pendingSaveData);
                pendingSaveData = null;
            }
            // 然后处理新游戏
            else if (!string.IsNullOrEmpty(pendingScriptName))
            {
                RunGameLogic();
            }
        }
    }

    private void RunGameLogic()
    {
        VNDebug.LogVerbose($"[VNManager] RunGameLogic 开始。剧本: {pendingScriptName}, 目标行: {pendingLineID}");

        InitializeManager();

        // 【新增】显示加载进度面板
        ShowLoadingPanelAndStartGame();
    }

    /// <summary>
    /// 显示加载面板并开始游戏加载流程
    /// </summary>
    private void ShowLoadingPanelAndStartGame()
    {
        LoadingProgressManager.GetInstance().ClearAllTasks(false);

        // 1. 显示加载进度面板
        UIManager.GetInstance().ShowPanel<LoadingProgressPanel>(
            "LoadingProgressPanel",
            VNProjectConfig.Instance.UI_LoadingPath,
            E_UI_Layer.System,
            (loadingPanel) =>
            {
                // 加载面板显示成功后，开始加载流程
                StartGameLoading();
            }
        );
    }

    /// <summary>
    /// 开始游戏加载流程（带进度跟踪）
    /// </summary>
    private void StartGameLoading()
    {

        isGameplayPanelLoadCallbackFired = false;

        LoadingProgressManager progressManager = LoadingProgressManager.GetInstance();

        // 注册加载任务
        string scriptTaskID = "load_script";
        string uiTaskID = "ui_VNGameplayPanel"; // 使用UIManager自动注册的任务ID

        progressManager.RegisterTask(scriptTaskID, $"加载剧本: {pendingScriptName}", 0.4f); // 权重40%
        // 先注册UI任务（如果还没注册），设置正确的权重
        // UIManager在ShowPanel时会检查任务是否已存在，如果存在就不重复注册
        if (progressManager.GetTaskProgress(uiTaskID) < 0)
        {
            progressManager.RegisterTask(uiTaskID, "加载游戏界面", 0.6f); // 权重60%
        }
        else
        {
            // 如果已经注册，更新权重和名称
            var uiTask = progressManager.GetTask(uiTaskID);
            if (uiTask != null)
            {
                uiTask.Weight = 0.6f;
                uiTask.TaskName = "加载游戏界面";
                // 触发进度更新以刷新显示
                progressManager.UpdateTaskProgress(uiTaskID, uiTask.Progress);
            }
        }

        // 监听所有任务完成
        progressManager.OnAllTasksCompleted += OnGameLoadingCompleted;

        // 使用协程来加载，让进度更新有时间刷新UI
        MonoManager.GetInstance().StartCoroutine(LoadScriptWithProgress(scriptTaskID));
    }



    /// <summary>
    /// 游戏加载完成回调
    /// </summary>
    private void OnGameLoadingCompleted()
    {
        LoadingProgressManager progressManager = LoadingProgressManager.GetInstance();
        progressManager.OnAllTasksCompleted -= OnGameLoadingCompleted;

        LoadingProgressPanel loadingPanel = UIManager.GetInstance().GetPanel<LoadingProgressPanel>("LoadingProgressPanel");
        if (loadingPanel != null)
        {
            loadingPanel.CancelInvoke("HideMe");
        }

        if (progressManager.GetTaskProgress(StartupBackgroundTaskID) < 0f)
        {
            progressManager.RegisterTask(StartupBackgroundTaskID, "加载首张背景", 0.2f);
        }
        else
        {
            progressManager.UpdateTaskName(StartupBackgroundTaskID, "加载首张背景");
            progressManager.UpdateTaskProgress(StartupBackgroundTaskID, 0f);
        }

        // 取消 ClearAllTasks，等待任务队列完成再进 DelayedStartGameplay
        MonoManager.GetInstance().StartCoroutine(WaitLoadingQueueThenStartGameplay());
    }

    /// <summary>
    /// 延迟启动游戏逻辑（确保UI完全初始化）
    /// </summary>
    /// <summary>
    /// 延迟启动游戏逻辑（确保UI完全初始化）
    /// </summary>


    private void RunGameLogic_OLD()
    {
        // 此方法已废弃，保留作为参考
        VNDebug.LogVerbose($"[VNManager] RunGameLogic 开始。剧本: {pendingScriptName}, 目标行: {pendingLineID}");

        InitializeManager();

        // 【新增】跨剧本加载时清空历史记录（新游戏或切换剧本）
        // 在设置新剧本名之前，检查是否是切换剧本
        string previousScriptName = this.currentScriptName;
        bool isNewScript = string.IsNullOrEmpty(previousScriptName) || previousScriptName != pendingScriptName;

        if (isNewScript)
        {
            // 新游戏或切换剧本，清空历史记录
            ClearHistoryLog();
            ClearRecordedScriptTags();
            VNDebug.LogVerbose($"[VNManager] 检测到新剧本或首次启动，已清空历史记录。旧剧本: {previousScriptName}, 新剧本: {pendingScriptName}");
        }

        ApplyPendingDebugScriptTags();

        this.currentScriptName = pendingScriptName;

        // 1. 加载剧本数据 (纯数据操作)
        CommandManager.GetInstance().ExecuteCommand($"loadscript({pendingScriptName})");

        ResetState();

        // 2. 计算目标行索引 (暂不预演，只算位置)
        int targetIndex = 0;
        if (!string.IsNullOrEmpty(pendingLineID))
        {
            string cleanID = pendingLineID.Trim();
            if (LineIDIndexMap.ContainsKey(cleanID))
            {
                targetIndex = LineIDIndexMap[cleanID];
            }
            else
            {
                Debug.LogError($"[VNManager] 找不到指定的行号 ID: {cleanID}，将从头开始。");
                targetIndex = 0;
            }
        }
        // 3. 显示 UI (异步过程)
        if (StoryLines.Count > 0)
        {
            UIManager.GetInstance().ShowPanel<VNGameplayPanel>("VNGameplayPanel", VNProjectConfig.Instance.UI_VNGamePlayPath, E_UI_Layer.Middle, (panel) =>
            {
                // 【修复】确保游戏状态设置为 Gameplay（场景回放时需要）
                GameStateManager.GetInstance().SetState(GameState.Gameplay);

                // A. 强力清理 UI 现场
                VNAPI.ClearAllEffects(); // 确保 EffectLayer 是空的
                EventCenter.GetInstance().EventTrigger(VNGameEvents.HideCharacter, "Left");
                EventCenter.GetInstance().EventTrigger(VNGameEvents.HideCharacter, "Mid");
                EventCenter.GetInstance().EventTrigger(VNGameEvents.HideCharacter, "Right");

                // 【修复】快进到目标行，如果遇到 choice 命令则停止
                bool encounteredChoice = false;
                if (targetIndex > 0)
                {
                    VNDebug.LogVerbose($"[VNManager] UI就绪，开始预演至索引: {targetIndex}");
                    encounteredChoice = FastForwardToLine(targetIndex, pendingIgnoreChoiceWhenFastForward);
                }

                // C. 设置当前行（如果遇到 choice，FastForwardToLine 已经设置了 CurrentLineIndex，不需要覆盖）
                if (!encounteredChoice)
                {
                    CurrentLineIndex = targetIndex;
                }

                // D. 同步立绘显示 (FastForward 更新了 currentCharacters 数据，现在应用到 UI)
                foreach (var kvp in currentCharacters)
                {
                    string[] parts = kvp.Value.Split('_');
                    if (parts.Length >= 2)
                    {
                        Dictionary<string, string> info = new Dictionary<string, string>
                        {
                            { "position", kvp.Key }, { "characterID", parts[0] }, { "emotion", parts[1] }
                        };
                        EventCenter.GetInstance().EventTrigger(VNGameEvents.ShowCharacter, info);

                        // 【修复】同步翻转状态：如果该位置有保存的翻转状态，应用完整的scale（考虑profile.scale和翻转状态）
                        string posCode = NormalizePositionCode(kvp.Key);
                        if (currentCharactersScaleX.ContainsKey(posCode))
                        {
                            float savedScaleX = currentCharactersScaleX[posCode];

                            // 获取角色的CharacterProfile，以获取profile.scale
                            string characterID = parts[0];
                            CharacterProfile profile = CharacterResManager.GetInstance().GetCharacterProfile(characterID);

                            if (profile != null)
                            {
                                // 计算正确的scale：profile.scale * savedScaleX
                                float profileScale = profile.scale > 0 ? profile.scale : 1.0f;
                                Vector3 scale = Vector3.one * profileScale;
                                scale.x = savedScaleX * profileScale; // 翻转时也要应用profile的scale

                                RectTransform charRect = VNAPI.GetCharRect(posCode);
                                if (charRect != null)
                                {
                                    charRect.localScale = scale;
                                    VNDebug.LogVerbose($"[VNManager] 同步位置 {kvp.Key}({posCode}) 的完整scale - ProfileScale: {profileScale}, Flip: {savedScaleX}, FinalScale: {scale}");
                                }
                            }
                            else
                            {
                                // 如果找不到profile，使用旧的逻辑（只应用翻转状态）
                                RectTransform charRect = VNAPI.GetCharRect(posCode);
                                if (charRect != null)
                                {
                                    Vector3 scale = charRect.localScale;
                                    scale.x = savedScaleX;
                                    charRect.localScale = scale;
                                    VNDebug.LogVerboseWarning($"[VNManager] 找不到角色 {characterID} 的Profile，只应用翻转状态: {savedScaleX}");
                                }
                            }
                        }
                    }
                }

                // E. 同步背景显示
                if (!string.IsNullOrEmpty(currentBG))
                {
                    EventCenter.GetInstance().EventTrigger(VNGameEvents.ChangeBackground, currentBG);
                }

                // F. 正式播放
                PlayCurrentLine();

                // G. 【新增】调用游戏启动完成回调
                if (onGameStartedCallback != null)
                {
                    onGameStartedCallback.Invoke();
                    onGameStartedCallback = null; // 调用后清空，避免重复调用
                }
            });
        }
        else
        {
            Debug.LogError("[VNManager] 剧本加载失败，无法启动游戏。");
            // 即使失败也调用回调，让用户知道启动失败
            if (onGameStartedCallback != null)
            {
                onGameStartedCallback.Invoke();
                onGameStartedCallback = null;
            }
        }

        // 清理参数
        pendingScriptName = null;
        pendingLineID = null;
        pendingIgnoreChoiceWhenFastForward = false;
    }

    private void InitializeManager()
    {
        GlobalDataManager.GetInstance().Init();
        UIManager.GetInstance().Init();
        CharacterResManager.GetInstance();
        ResourcesManager.GetInstance();
        EventCenter.GetInstance();
        MonoManager.GetInstance();
        MusicManager.GetInstance();
        VoiceManager.GetInstance();
        SaveManager.GetInstance();
        StaminaManager.GetInstance().Init();
        if (StaminaManager.IsEnabled)
            RewardedAdManager.GetInstance().Init();

        // 【Bug修复】清理音效列表，防止场景切换时引用已销毁的对象
        MusicManager.GetInstance().ClearAllSFX();

        CommandManager.GetInstance().Init();
        EventCenter.GetInstance().AddEventListener(VNGameEvents.TypingFinished, OnTypingFinished);
    }

    private void OnTypingFinished()
    {
        VNDebug.LogVerbose($"[TypingTrace][OnTypingFinished] currentLineIndex={CurrentLineIndex}, flowRunning={_flowCoroutine != null}, cmdRunning={CommandManager.GetInstance().IsRunning}, isTextDisplaying(before)={isTextDisplaying}");
        isTextDisplaying = false;
        CheckAndTriggerAutoPlay();
        RefreshContinueIconState();
    }

    private void ResetState()
    {
        currentBG = "";
        currentBGM = "";
        currentCharacters.Clear();
        activeEffects.Clear();
        VNAPI.ClearAllEffects(); // 物理清空特效
        currentCharactersScaleX.Clear();
        isVoiceEnabled = true;
        lastLine = null;
    }

    /// <summary>
    /// 清空历史记录（用于新游戏或跨剧本加载）
    /// </summary>
    private void ClearHistoryLog()
    {
        GlobalDataManager.GetInstance().ClearHistoryLog();
        VNDebug.LogVerbose("[VNManager] 已清空历史记录");
    }

    /// <summary>
    /// 全量状态预演 (核心逻辑)
    /// </summary>
    /// <summary>
    /// 快进到目标行
    /// </summary>
    /// <param name="targetIndex">目标行索引</param>
    /// <param name="ignoreChoice">是否忽略 choice 命令（用于 jump 命令强制跳转）</param>
    /// <returns>如果遇到 choice 命令返回 true，否则返回 false</returns>
    public bool FastForwardToLine(int targetIndex, bool ignoreChoice = false)
    {
        ResetState();
        VNAPI.ClearAllEffects(); // 物理清空
        activeEffects.Clear();
        if (targetIndex <= 0) return false;

        bool encounteredChoice = false;

        // 模拟运行
        for (int i = 0; i < targetIndex; i++)
        {
            if (i >= StoryLines.Count) break;
            StoryLine line = StoryLines[i];

            if (IsHiddenBranchControlLine(line) && TryGetLineLevel(line, out int hiddenLevel))
            {
                int hiddenTargetIndex = FindMatchedChildBranchIndex(i, hiddenLevel);
                if (hiddenTargetIndex < 0)
                    hiddenTargetIndex = FindNextSameLevelIndex(i, hiddenLevel);
                if (hiddenTargetIndex < 0 && !HasChildBranchConditions(i, hiddenLevel))
                    hiddenTargetIndex = FindFirstChildLevelIndex(i, hiddenLevel);

                if (hiddenTargetIndex < 0)
                    break;

                Debug.Log($"[VNManager][NoteBranch][FastForward] Hidden line ID={line.ID}, index={i}, level={hiddenLevel}, tags=[{GetRecordedScriptTagsForDebug()}], resolvedIndex={hiddenTargetIndex}, resolvedID={(hiddenTargetIndex >= 0 && hiddenTargetIndex < StoryLines.Count ? StoryLines[hiddenTargetIndex].ID : "")}");
                i = hiddenTargetIndex - 1;
                continue;
            }

            // 【修复】检查是否包含 choice 命令，如果包含则停止快进（除非 ignoreChoice 为 true）
            if (!ignoreChoice && !string.IsNullOrEmpty(line.Command) && ContainsChoiceCommand(line.Command))
            {
                // 遇到选项命令，停止快进，设置当前行索引为包含 choice 的行
                CurrentLineIndex = i;
                encounteredChoice = true;
                VNDebug.LogVerbose($"[VNManager] 快进过程中遇到选项命令，停止在第 {i} 行 (ID: {line.ID})");

                // 先应用当前行的状态（背景、立绘、BGM等）
                if (!string.IsNullOrEmpty(line.Background)) currentBG = line.Background;
                if (!string.IsNullOrEmpty(line.BGM))
                {
                    if (line.BGM == "stop") currentBGM = "";
                    else if (line.BGM != "pause" && line.BGM != "resume") currentBGM = line.BGM;
                }
                SimulateCharacterUpdate("Left", line.CharLeft);
                SimulateCharacterUpdate("Mid", line.CharMid);
                SimulateCharacterUpdate("Right", line.CharRight);
                if (line.Voice == "false") isVoiceEnabled = false;
                else if (!string.IsNullOrEmpty(line.Voice)) isVoiceEnabled = true;
                lastLine = line;

                // 先应用其他命令（不包括 choice）
                string otherCommands = ExtractNonChoiceCommands(line.Command);
                if (!string.IsNullOrEmpty(otherCommands))
                {
                    CommandManager.GetInstance().SimulateCommands(otherCommands);
                }

                // 停止快进循环
                break;
            }

            // 1. 基础属性
            if (!string.IsNullOrEmpty(line.Background)) currentBG = line.Background;

            // 2. BGM (只记录状态，不播放)
            if (!string.IsNullOrEmpty(line.BGM))
            {
                if (line.BGM == "stop") currentBGM = "";
                else if (line.BGM != "pause" && line.BGM != "resume") currentBGM = line.BGM;
            }

            // 3. 立绘
            SimulateCharacterUpdate("Left", line.CharLeft);
            SimulateCharacterUpdate("Mid", line.CharMid);
            SimulateCharacterUpdate("Right", line.CharRight);

            // 4. 语音
            if (line.Voice == "false") isVoiceEnabled = false;
            else if (!string.IsNullOrEmpty(line.Voice)) isVoiceEnabled = true;

            // 5. Command 模拟 (特效、Flags 等)
            if (!string.IsNullOrEmpty(line.Command))
            {
                CommandManager.GetInstance().SimulateCommands(line.Command);
            }

            lastLine = line;
        }

        // 预演结束，应用 BGM 和 特效（只有完全快进到目标行时才应用）
        // 注意：如果遇到 choice 命令，CurrentLineIndex 已经被设置为包含 choice 的行，此时不应用特效
        if (!encounteredChoice)
        {
            // 没有遇到 choice，正常快进到目标行，应用 BGM 和特效
            if (!string.IsNullOrEmpty(currentBGM))
                MusicManager.GetInstance().PlayBGM(currentBGM);
            else
                MusicManager.GetInstance().StopBGM();

            List<string> effectsToRestore = new List<string>(activeEffects);

            foreach (var effect in effectsToRestore)
            {
                RestoreEffect(effect);
            }
        }
        else
        {
            // 遇到 choice，只应用 BGM（因为已经处理了当前行的状态）
            if (!string.IsNullOrEmpty(currentBGM))
                MusicManager.GetInstance().PlayBGM(currentBGM);
            else
                MusicManager.GetInstance().StopBGM();
        }

        return encounteredChoice;
    }

    /// <summary>
    /// 检查命令字符串中是否包含 choice 命令
    /// </summary>
    private bool ContainsChoiceCommand(string commandString)
    {
        if (string.IsNullOrEmpty(commandString)) return false;

        // 检查是否包含 choice( 命令（不区分大小写）
        string lowerCommand = commandString.ToLower();
        return lowerCommand.Contains("choice(");
    }

    /// <summary>
    /// 提取除了 choice 之外的其他命令
    /// </summary>
    private string ExtractNonChoiceCommands(string commandString)
    {
        if (string.IsNullOrEmpty(commandString)) return "";

        // 分割命令（使用 & 分隔符）
        string[] commands = commandString.Split('&');
        System.Collections.Generic.List<string> nonChoiceCommands = new System.Collections.Generic.List<string>();

        foreach (string cmd in commands)
        {
            string trimmedCmd = cmd.Trim();
            if (string.IsNullOrEmpty(trimmedCmd)) continue;

            // 检查是否是 choice 命令
            int startIndex = trimmedCmd.IndexOf('(');
            if (startIndex > 0)
            {
                string cmdName = trimmedCmd.Substring(0, startIndex).Trim().ToLower();
                if (cmdName != "choice")
                {
                    nonChoiceCommands.Add(trimmedCmd);
                }
            }
            else
            {
                // 没有括号的命令也保留
                if (!trimmedCmd.ToLower().StartsWith("choice"))
                {
                    nonChoiceCommands.Add(trimmedCmd);
                }
            }
        }

        // 重新组合命令字符串
        return string.Join("&", nonChoiceCommands);
    }

    private void SimulateCharacterUpdate(string pos, string data)
    {
        string normalizedPos = pos;
        string normalizedPosCode = NormalizePositionCode(pos);

        // 空槽：清除该位置（与运行时「空=隐藏」一致，避免快进后仍保留旧立绘状态）
        if (string.IsNullOrEmpty(data))
        {
            if (currentCharacters.ContainsKey(normalizedPos)) currentCharacters.Remove(normalizedPos);
            if (currentCharactersScaleX.ContainsKey(normalizedPosCode)) currentCharactersScaleX.Remove(normalizedPosCode);
            return;
        }

        if (data == "hide")
        {
            if (currentCharacters.ContainsKey(normalizedPos)) currentCharacters.Remove(normalizedPos);
            if (currentCharactersScaleX.ContainsKey(normalizedPosCode)) currentCharactersScaleX.Remove(normalizedPosCode);
        }
        else
        {
            // 如果是新角色，初始化翻转状态为默认值（朝右）
            if (!currentCharacters.ContainsKey(normalizedPos))
            {
                currentCharactersScaleX[normalizedPosCode] = 1f;
            }
            currentCharacters[normalizedPos] = data;
        }
    }

    // 位置代码转换工具函数
    private string NormalizePositionCode(string pos)
    {
        if (string.IsNullOrEmpty(pos)) return pos;
        string upper = pos.ToUpper();
        if (upper == "LEFT" || upper == "L") return "L";
        if (upper == "MID" || upper == "MIDDLE" || upper == "M") return "M";
        if (upper == "RIGHT" || upper == "R") return "R";
        return pos; // 未知格式，原样返回
    }

    // 特效状态管理 API
    public void RegisterEffect(string name) { if (!activeEffects.Contains(name)) activeEffects.Add(name); }
    public void UnregisterEffect(string name) { if (activeEffects.Contains(name)) activeEffects.Remove(name); }
    public List<string> GetActiveEffects() { return new List<string>(activeEffects); }

    // 恢复特效 (物理生成)
    private void RestoreEffect(string effectName)
    {
        string commandString = effectName.StartsWith("filter:", System.StringComparison.OrdinalIgnoreCase)
            ? $"playfilter({effectName.Substring("filter:".Length)})"
            : $"playparticle({effectName})";
        CommandManager.GetInstance().ExecuteCommand(commandString);

        VNDebug.LogVerbose($"[VNManager] 自动恢复特效: {commandString}");
    }

    public void SetScriptData(List<StoryLine> lines, Dictionary<string, int> idMap, string scriptName)
    {
        this.StoryLines = lines;
        this.LineIDIndexMap = idMap;
        this.CurrentLineIndex = 0;
        this.lastLine = null;
        this.lastSelectBCLineIndex = -1;
        this.selectBCRecordedTagSnapshots.Clear();
        ClearPendingPostTextCommand();
        this.currentScriptName = scriptName;
    }

    public string GetCurrentScriptName()
    {
        return currentScriptName;
    }

    /// <summary>
    /// 继续游戏（加载存档）
    /// </summary>
    public void ContinueGame(SaveData saveData)
    {
        // 【核心修复】先检查场景，如果不在VNGamePlay场景，先加载场景
        if (SceneManager.GetActiveScene().name != "VNGamePlay")
        {
            // 保存存档数据，等待场景加载完成后再恢复
            pendingSaveData = saveData;
            SceneManager.LoadScene("VNGamePlay");
            return;
        }

        // 如果已经在VNGamePlay场景，直接执行
        ContinueGameInternal(saveData);
    }

    /// <summary>
    /// 继续游戏的内部实现（场景已准备好）
    /// </summary>
    private void ContinueGameInternal(SaveData saveData)
    {
        // 【新增】显示加载进度面板
        ShowLoadingPanelAndContinueGame(saveData);
    }

    /// <summary>
    /// 显示加载面板并继续游戏
    /// </summary>
    private void ShowLoadingPanelAndContinueGame(SaveData saveData)
    {
        LoadingProgressManager.GetInstance().ClearAllTasks(false);

        // 1. 显示加载进度面板
        UIManager.GetInstance().ShowPanel<LoadingProgressPanel>(
            "LoadingProgressPanel",
            VNProjectConfig.Instance.UI_LoadingPath,
            E_UI_Layer.System,
            (loadingPanel) =>
            {
                // 加载面板显示成功后，开始加载流程
                ContinueGameLoading(saveData);
            }
        );
    }

    /// <summary>
    /// 继续游戏的加载流程（带进度跟踪）
    /// </summary>
    private void ContinueGameLoading(SaveData saveData)
    {
        isGameplayPanelLoadCallbackFired = false;

        LoadingProgressManager progressManager = LoadingProgressManager.GetInstance();

        // 注册加载任务
        string scriptTaskID = "load_script_continue";
        string uiTaskID = "ui_VNGameplayPanel"; // 使用UIManager自动注册的任务ID

        progressManager.RegisterTask(scriptTaskID, $"加载存档: {saveData.ScriptFileName}", 0.4f); // 权重40%
        // 先注册UI任务（如果还没注册），设置正确的权重
        if (progressManager.GetTaskProgress(uiTaskID) < 0)
        {
            progressManager.RegisterTask(uiTaskID, "加载游戏界面", 0.6f); // 权重60%
        }
        else
        {
            // 如果已经注册，更新权重和名称
            var uiTask = progressManager.GetTask(uiTaskID);
            if (uiTask != null)
            {
                uiTask.Weight = 0.6f;
                uiTask.TaskName = "加载游戏界面";
                // 触发进度更新以刷新显示
                progressManager.UpdateTaskProgress(uiTaskID, uiTask.Progress);
            }
        }

        // 监听所有任务完成
        progressManager.OnAllTasksCompleted += OnContinueGameLoadingCompleted;

        // 使用协程来加载，让进度更新有时间刷新UI
        MonoManager.GetInstance().StartCoroutine(LoadScriptForContinueWithProgress(scriptTaskID, saveData));
    }



    /// <summary>
    /// 继续游戏加载完成回调
    /// </summary>
    private void OnContinueGameLoadingCompleted()
    {
        LoadingProgressManager progressManager = LoadingProgressManager.GetInstance();
        progressManager.OnAllTasksCompleted -= OnContinueGameLoadingCompleted;

        // 隐藏加载面板
        // UIManager.GetInstance().HidePanel("LoadingProgressPanel");

        // 清理加载任务
        // progressManager.ClearAllTasks();

        // 延迟一帧，确保UI完全初始化
        MonoManager.GetInstance().StartCoroutine(WaitLoadingQueueThenContinueGameplay());
    }



    /// <summary>
    /// 从存档恢复游戏状态（UI已准备好）
    /// </summary>
    private void RestoreGameStateFromSave(SaveData saveData, int targetIndex)
    {
        // 清理UI现场
        VNAPI.ClearAllEffects();
        EventCenter.GetInstance().EventTrigger(VNGameEvents.HideCharacter, "Left");
        EventCenter.GetInstance().EventTrigger(VNGameEvents.HideCharacter, "Mid");
        EventCenter.GetInstance().EventTrigger(VNGameEvents.HideCharacter, "Right");

        // 【修复】如果目标行索引大于0，需要预演到该位置，如果遇到 choice 命令则停止
        bool encounteredChoice = false;
        if (targetIndex > 0)
        {
            VNDebug.LogVerbose($"[VNManager] 从存档恢复，预演至索引: {targetIndex}");
            encounteredChoice = FastForwardToLine(targetIndex);
        }

        // 设置当前行（如果遇到 choice，FastForwardToLine 已经设置了 CurrentLineIndex，不需要覆盖）
        if (!encounteredChoice && targetIndex >= 0)
        {
            CurrentLineIndex = targetIndex;
        }

        // FastForwardToLine only simulates up to the line before the saved line.
        // Re-apply the exact snapshot so saves made after commands on the current
        // line do not keep stale state from the previous line.
        currentBG = saveData.CurrentBG;
        currentBGM = saveData.CurrentBGM;
        Debug.Log($"[VNManager][BGM] RestoreGameStateFromSave snapshot. script='{saveData.ScriptFileName}', lineID='{saveData.LineID}', savedBGM='{saveData.CurrentBGM}', currentBGM='{currentBGM}'");

        // 恢复背景
        if (!string.IsNullOrEmpty(currentBG) && currentBG != "hide" && currentBG != "black")
        {
            EventCenter.GetInstance().EventTrigger(VNGameEvents.ChangeBackground, currentBG);
        }
        else if (currentBG == "black")
        {
            EventCenter.GetInstance().EventTrigger(VNGameEvents.ChangeBackground, "black");
        }
        else if (currentBG == "hide")
        {
            EventCenter.GetInstance().EventTrigger(VNGameEvents.HideBackground);
        }

        // 恢复BGM
        if (!string.IsNullOrEmpty(currentBGM))
        {
            Debug.Log($"[VNManager][BGM] Restore will play BGM '{currentBGM}'.");
            MusicManager.GetInstance().PlayBGM(currentBGM);
        }
        else
        {
            Debug.Log("[VNManager][BGM] Restore found empty BGM, stopping BGM.");
            MusicManager.GetInstance().StopBGM();
        }

        // 恢复立绘
        Dictionary<string, string> charactersToRestore = saveData.Characters != null
            ? new Dictionary<string, string>(saveData.Characters)
            : new Dictionary<string, string>();
        currentCharacters.Clear();
        foreach (var kvp in charactersToRestore)
        {
            UpdateCharacter(kvp.Key, kvp.Value);
        }

        // 恢复特效（在UI准备好后）
        VNAPI.ClearAllEffects();
        activeEffects.Clear();
        if (saveData.ActiveEffects != null)
        {
            foreach (var effect in saveData.ActiveEffects)
            {
                activeEffects.Add(effect);
                RestoreEffect(effect);
            }
        }

        // 设置当前行索引并播放（首帧允许用存档槽位补全 CSV 空立绘，与「无行际继承」不冲突：存档是显式快照）
        CurrentLineIndex = targetIndex;
        _usePersistedCharacterSlotsWhenCsvCharCellsEmpty = true;
        PlayCurrentLine();
    }

    private void PlayCurrentLine()
    {
        if (!ResolveHiddenBranchControlLine())
            return;

        if (CurrentLineIndex < 0 || CurrentLineIndex >= StoryLines.Count)
        {
            _usePersistedCharacterSlotsWhenCsvCharCellsEmpty = false;

            if (isReplayMode)
            {
                EndReplay();
            }
            return;
        }

        if (_autoPlayCoroutine != null)
        {
            MonoManager.GetInstance().StopCoroutine(_autoPlayCoroutine);
            _autoPlayCoroutine = null;
        }


        var gameplayPanel = UIManager.GetInstance().GetPanel<VNGameplayPanel>("VNGameplayPanel");
        if (gameplayPanel != null)
        {
            gameplayPanel.RestoreDefaultCharTransforms();
            gameplayPanel.RestoreDefaultTextProperties();
        }

        StoryLine currentLine = StoryLines[CurrentLineIndex];
        TrackSelectBCLine(currentLine);
        RecordScriptTagFromNote(currentLine.Note);
        ApplyInheritance(currentLine);
        lastLine = currentLine;

        PrepareFadeInCharacters(currentLine.Command);
        bool backgroundChanged = UpdateVisualState(currentLine);
        UpdateAudioState(currentLine);
        UpdateDialogue(currentLine);

        GlobalDataManager.GetInstance().AddReadLineID(currentLine.ID);
        AutoSaveCurrentLine(backgroundChanged);

        if (TryShowToBeContinuedPanelForLine(currentLine))
        {
            _usePersistedCharacterSlotsWhenCsvCharCellsEmpty = false;
            return;
        }

        // if (!string.IsNullOrEmpty(currentLine.Command))
        // {
        //     _flowCoroutine = MonoManager.GetInstance().StartCoroutine(ExecuteActionsAndContinue(currentLine.Command));
        // }
        // else
        // {
        //     CheckAndTriggerAutoPlay();
        // }
        if (!string.IsNullOrEmpty(currentLine.Command))
        {
            ClearAdvanceAfterCommandsRequest();
            ClearStopCommandsForEndingChoiceRequest();
            string commandToRunNow = PrepareCommandForCurrentLine(currentLine);
            if (!string.IsNullOrEmpty(commandToRunNow))
            {
                _flowCoroutine = MonoManager.GetInstance().StartCoroutine(ExecuteActionsAndContinue(commandToRunNow));
            }
            else
            {
                RefreshContinueIconState();
                CheckAndTriggerAutoPlay();
            }
        }
        else
        {
            ClearPendingPostTextCommand();
            CheckAndTriggerAutoPlay();
        }

        _usePersistedCharacterSlotsWhenCsvCharCellsEmpty = false;
    }

    private void PrepareFadeInCharacters(string commandString)
    {
        if (string.IsNullOrEmpty(commandString)) return;

        string[] commands = commandString.Split('&');
        foreach (string command in commands)
        {
            string trimmedCommand = command.Trim();
            int startIndex = trimmedCommand.IndexOf('(');
            int endIndex = trimmedCommand.LastIndexOf(')');
            if (startIndex <= 0 || endIndex <= startIndex) continue;

            string commandName = trimmedCommand.Substring(0, startIndex).Trim().ToLower();
            if (commandName != "charfadein") continue;

            string args = trimmedCommand.Substring(startIndex + 1, endIndex - startIndex - 1);
            string[] parts = args.Split(',');
            if (parts.Length == 0) continue;

            string posCode = parts[0].Trim();
            RectTransform targetRect = VNAPI.GetCharRect(posCode);
            if (targetRect == null || targetRect.gameObject == null) continue;

            CanvasGroup canvasGroup = targetRect.GetComponent<CanvasGroup>();
            if (canvasGroup == null) canvasGroup = targetRect.gameObject.AddComponent<CanvasGroup>();
            canvasGroup.alpha = 0f;
        }
    }



    private void CheckAndTriggerAutoPlay()
    {
        GameStateManager stateManager = GameStateManager.GetInstance();
        if (stateManager != null && stateManager.CurrentState == GameState.Choice)
        {
            // 在 Choice 状态下，等待玩家选择，不触发自动播放
            return;
        }

        // 检查打字机效果是否完成
        bool isTextTyping = false;
        var gameplayPanel = UIManager.GetInstance().GetPanel<VNGameplayPanel>("VNGameplayPanel");
        if (gameplayPanel != null)
        {
            isTextTyping = gameplayPanel.IsTextTyping();
        }

        // 检查语音是否正在播放
        bool isVoicePlaying = false;
        if (VoiceManager.GetInstance() != null)
        {
            isVoicePlaying = VoiceManager.GetInstance().IsVoicePlaying();
        }

        // 只有当打字机效果完成、语音播放完毕、命令执行完毕、流程协程完毕时，才能触发自动播放
        bool isBusy = isTextDisplaying || isTextTyping || isVoicePlaying ||
                      CommandManager.GetInstance().IsRunning || _flowCoroutine != null;

        if (isAutoPlaying && !isBusy)
        {
            float delay = GlobalDataManager.GetInstance().GetGlobalData().AutoSpeed;
            VNDebug.LogVerbose($"[VNManager] 自动播放触发 - 延迟时间: {delay}秒");
            _autoPlayCoroutine = MonoManager.GetInstance().StartCoroutine(AutoPlayCountdown(delay));
        }
    }

    private void RefreshContinueIconState()
    {
        var gameplayPanel = UIManager.GetInstance().GetPanel<VNGameplayPanel>("VNGameplayPanel");
        if (gameplayPanel != null)
        {
            gameplayPanel.RefreshContinueIconState();
        }
    }

    public void CheckAutoPlay()
    {
        CheckAndTriggerAutoPlay();
    }


    public void NextLine()
    {
        AdvanceToNextLine(false);
    }

    public void NextLineWithoutAnimation()
    {
        AdvanceToNextLine(true);
    }

    private void AdvanceToNextLine(bool skipAnimations)
    {
        VNDebug.LogVerbose($"[VNManager] Max line" + StoryLines.Count);
        VNDebug.LogVerbose($"[TypingTrace][AdvanceToNextLine] enter skipAnimations={skipAnimations}, currentLineIndex={CurrentLineIndex}, isTextDisplaying={isTextDisplaying}, flowRunning={_flowCoroutine != null}, cmdRunning={CommandManager.GetInstance().IsRunning}");
        // 【修复】检查游戏状态，如果是 Choice 状态，不应该继续前进
        GameStateManager stateManager = GameStateManager.GetInstance();
        if (stateManager != null && stateManager.CurrentState == GameState.Choice)
        {
            // 在 Choice 状态下，等待玩家选择，不继续前进
            VNDebug.LogVerbose("[VNManager] 当前处于 Choice 状态，等待玩家选择，暂停前进");
            return;
        }

        if (isToBeContinuedPanelShowing)
        {
            return;
        }

        if (isTextDisplaying)
        {
            VNDebug.LogVerbose($"[TypingTrace][AdvanceToNextLine] trigger DisplayAllText skipAnimations={skipAnimations}");
            EventCenter.GetInstance().EventTrigger(VNGameEvents.DisplayAllText);

            if (!skipAnimations)
            {
                VNDebug.LogVerbose("[TypingTrace][AdvanceToNextLine] return after DisplayAllText because skipAnimations=false");
                return;
            }
        }

        bool isCmdRunning = CommandManager.GetInstance().IsRunning;
        bool isFlowRunning = _flowCoroutine != null;

        if (isCmdRunning || isFlowRunning)
        {
            VNDebug.LogVerbose($"[TypingTrace][AdvanceToNextLine] block because current line flow/cmd still running, skipAnimations={skipAnimations}, isCmdRunning={isCmdRunning}, isFlowRunning={isFlowRunning}");
            CheckAndTriggerAutoPlay();
            return;
        }

        if (TryPlayPendingPostTextCommand())
        {
            return;
        }

        if (_autoPlayCoroutine != null)
        {
            MonoManager.GetInstance().StopCoroutine(_autoPlayCoroutine);
            _autoPlayCoroutine = null;
        }

        // 【新增】检查回放结束条件（在播放下一行之前检查上一行是否是结束行）
        if (isReplayMode && lastLine != null && !string.IsNullOrEmpty(replayEndLineID) && lastLine.ID == replayEndLineID)
        {
            EndReplay();
            return;
        }

        if (!TryConsumeStaminaForCurrentLine())
            return;

        CurrentLineIndex++;

        if (skipAnimations)
        {
            PlayCurrentLineImmediately();
        }
        else
        {
            PlayCurrentLine();
        }
    }

    private bool TryConsumeStaminaForCurrentLine()
    {
        if (isReplayMode || CurrentLineIndex < 0 || CurrentLineIndex >= StoryLines.Count)
            return true;

        StoryLine currentLine = StoryLines[CurrentLineIndex];
        if (!ShouldConsumeStamina(currentLine))
            return true;

        StaminaManager staminaManager = StaminaManager.GetInstance();
        staminaManager.Init();

        if (!staminaManager.HasEnough(1))
        {
            if (isAutoPlaying)
                ToggleAutoPlay();

            StaminaRecoveryPopup.Show();
            return false;
        }

        return staminaManager.TryConsume(1);
    }

    private bool ShouldConsumeStamina(StoryLine line)
    {
        if (line == null)
            return false;

        if (!string.IsNullOrWhiteSpace(line.Text))
            return true;

        return VNLocalizationService.IsEnabled() &&
               !string.IsNullOrEmpty(currentScriptName) &&
               !string.IsNullOrEmpty(line.ID) &&
               VNLocalizationService.TryGetText(currentScriptName, line.ID, out var localizedText) &&
               !string.IsNullOrWhiteSpace(localizedText);
    }

    private void PlayCurrentLineImmediately()
    {
        if (!ResolveHiddenBranchControlLine())
            return;

        if (CurrentLineIndex < 0 || CurrentLineIndex >= StoryLines.Count)
        {
            if (isReplayMode)
            {
                EndReplay();
            }
            return;
        }

        if (_autoPlayCoroutine != null)
        {
            MonoManager.GetInstance().StopCoroutine(_autoPlayCoroutine);
            _autoPlayCoroutine = null;
        }

        var gameplayPanel = UIManager.GetInstance().GetPanel<VNGameplayPanel>("VNGameplayPanel");
        if (gameplayPanel != null)
        {
            gameplayPanel.RestoreDefaultCharTransforms();
            gameplayPanel.RestoreDefaultTextProperties();
        }

        StoryLine currentLine = StoryLines[CurrentLineIndex];
        TrackSelectBCLine(currentLine);
        RecordScriptTagFromNote(currentLine.Note);
        ApplyInheritance(currentLine);
        lastLine = currentLine;

        bool backgroundChanged = UpdateVisualState(currentLine);
        UpdateAudioState(currentLine);
        UpdateDialogue(currentLine);
        EventCenter.GetInstance().EventTrigger(VNGameEvents.DisplayAllText);

        GlobalDataManager.GetInstance().AddReadLineID(currentLine.ID);
        AutoSaveCurrentLine(backgroundChanged);

        if (TryShowToBeContinuedPanelForLine(currentLine))
        {
            return;
        }

        int preIndex = CurrentLineIndex;
        if (!string.IsNullOrEmpty(currentLine.Command))
        {
            CommandManager.GetInstance().SimulateCommands(currentLine.Command);
        }

        if (CurrentLineIndex != preIndex)
        {
            PlayCurrentLineImmediately();
        }
        else
        {
            CheckAndTriggerAutoPlay();
        }
    }

    private void ApplyInheritance(StoryLine currentLine)
    {
        // Speaker / Text / HeadProfile / 立绘三槽：不继承，以本行 CSV 为准（立绘空槽在 UpdateCharacter 中视为隐藏）。

        // 背景、BGM 相关：仍继承 Manager 当前状态（空单元格沿用上一有效背景）。
        if (string.IsNullOrEmpty(currentLine.Background))
            currentLine.Background = this.currentBG;

        // 语音：未填时仍按 isVoiceEnabled 自动生成路径（与「场景氛围继承」策略一致，减轻配音表负担）
        // 逻辑：没填->自动生成；填false->关；填其他->开
        if (string.IsNullOrEmpty(currentLine.Voice))
        {
            if (!isVoiceEnabled)
            {
                currentLine.Voice = "";
            }
            else
            {
                // 只有当有 ID 时才自动生成，防止空行报错
                if (!string.IsNullOrEmpty(currentLine.ID))
                    currentLine.Voice = Path.GetDirectoryName(currentLine.ID) + "/" + currentLine.ID + ".mp3";
            }
        }
        else if (currentLine.Voice.ToLower() == "false")
        {
            isVoiceEnabled = false;
            currentLine.Voice = "";
        }
        else
        {
            isVoiceEnabled = true; // 有明确设置语音文件名，则开启
        }
    }

    private string PrepareCommandForCurrentLine(StoryLine line)
    {
        ClearPendingPostTextCommand();

        if (!ShouldDeferCommandsUntilTextConfirmed(line))
        {
            return line != null ? line.Command : "";
        }

        List<string> commandsToRunNow = new List<string>();
        List<string> commandsToRunAfterText = new List<string>();

        string[] commands = line.Command.Split('&');
        foreach (string command in commands)
        {
            string trimmedCommand = command.Trim();
            if (string.IsNullOrEmpty(trimmedCommand))
            {
                continue;
            }

            if (ShouldDeferCommandUntilTextConfirmed(trimmedCommand))
            {
                commandsToRunAfterText.Add(trimmedCommand);
            }
            else
            {
                commandsToRunNow.Add(trimmedCommand);
            }
        }

        if (commandsToRunAfterText.Count > 0)
        {
            _pendingPostTextCommand = string.Join("&", commandsToRunAfterText);
            _pendingPostTextCommandLineIndex = CurrentLineIndex;
        }

        return string.Join("&", commandsToRunNow);
    }

    private bool ShouldDeferCommandsUntilTextConfirmed(StoryLine line)
    {
        if (line == null || string.IsNullOrWhiteSpace(line.Text) || string.IsNullOrWhiteSpace(line.Command))
        {
            return false;
        }

        string[] commands = line.Command.Split('&');
        foreach (string command in commands)
        {
            if (ShouldDeferCommandUntilTextConfirmed(command.Trim()))
            {
                return true;
            }
        }

        return false;
    }

    private bool ShouldDeferCommandUntilTextConfirmed(string command)
    {
        return IsCommandName(command, "loadscript") ||
               IsCommandName(command, "jump");
    }

    private bool IsCommandName(string command, string commandName)
    {
        if (string.IsNullOrWhiteSpace(command) || string.IsNullOrWhiteSpace(commandName))
        {
            return false;
        }

        int startIndex = command.IndexOf('(');
        if (startIndex <= 0)
        {
            return false;
        }

        string name = command.Substring(0, startIndex).Trim();
        return string.Equals(name, commandName, System.StringComparison.OrdinalIgnoreCase);
    }

    private bool TryPlayPendingPostTextCommand()
    {
        if (string.IsNullOrEmpty(_pendingPostTextCommand) ||
            _pendingPostTextCommandLineIndex != CurrentLineIndex)
        {
            return false;
        }

        string command = _pendingPostTextCommand;
        ClearPendingPostTextCommand();
        ClearAdvanceAfterCommandsRequest();
        ClearStopCommandsForEndingChoiceRequest();
        _flowCoroutine = MonoManager.GetInstance().StartCoroutine(ExecuteActionsAndContinue(command));
        return true;
    }

    private void ClearPendingPostTextCommand()
    {
        _pendingPostTextCommand = "";
        _pendingPostTextCommandLineIndex = -1;
    }

    private bool UpdateVisualState(StoryLine currentLine)
    {
        string previousBG = currentBG;

        if (!string.IsNullOrEmpty(currentLine.Background) && currentLine.Background != "hide" && currentLine.Background != "black")
        {
            currentBG = currentLine.Background;
            EventCenter.GetInstance().EventTrigger(VNGameEvents.ChangeBackground, currentLine.Background);
        }
        else if (currentLine.Background == "black")
        {
            currentBG = "black";
            EventCenter.GetInstance().EventTrigger(VNGameEvents.ChangeBackground, "black");
        }
        else if (currentLine.Background == "hide")
        {
            currentBG = "hide";
            EventCenter.GetInstance().EventTrigger(VNGameEvents.HideBackground);
        }

        string ResolveCharForSlot(string csvValue, string slotKey)
        {
            if (!string.IsNullOrEmpty(csvValue)) return csvValue;
            if (_usePersistedCharacterSlotsWhenCsvCharCellsEmpty &&
                currentCharacters.TryGetValue(slotKey, out var persisted) &&
                !string.IsNullOrEmpty(persisted))
                return persisted;
            return csvValue;
        }

        UpdateCharacter("Left", ResolveCharForSlot(currentLine.CharLeft, "Left"));
        UpdateCharacter("Mid", ResolveCharForSlot(currentLine.CharMid, "Mid"));
        UpdateCharacter("Right", ResolveCharForSlot(currentLine.CharRight, "Right"));

        return previousBG != currentBG;
    }

    private void UpdateCharacter(string position, string charData)
    {
        // 空槽与 hide 等价：不继承上一行立绘，必须每行显式填写才会显示
        if (string.IsNullOrEmpty(charData) || charData == "hide")
        {
            EventCenter.GetInstance().EventTrigger(VNGameEvents.HideCharacter, position);
            if (this.currentCharacters.ContainsKey(position))
                this.currentCharacters.Remove(position);
            // 隐藏时不清除翻转状态，保持状态以便后续恢复
        }
        else if (!string.IsNullOrEmpty(charData))
        {
            string[] parts = charData.Split('_');
            if (parts.Length >= 2)
            {
                this.currentCharacters[position] = charData;
                Dictionary<string, string> info = new Dictionary<string, string>
                {
                    { "position", position }, { "characterID", parts[0] }, { "emotion", parts[1] }
                };
                EventCenter.GetInstance().EventTrigger(VNGameEvents.ShowCharacter, info);

                // 【修复】如果该位置有保存的翻转状态，应用完整的scale（考虑profile.scale和翻转状态）
                string posCode = NormalizePositionCode(position);
                if (currentCharactersScaleX.ContainsKey(posCode))
                {
                    float savedScaleX = currentCharactersScaleX[posCode];

                    // 获取角色的CharacterProfile，以获取profile.scale
                    string characterID = parts[0];
                    CharacterProfile profile = CharacterResManager.GetInstance().GetCharacterProfile(characterID);

                    if (profile != null)
                    {
                        // 计算正确的scale：profile.scale * savedScaleX
                        float profileScale = profile.scale > 0 ? profile.scale : 1.0f;
                        Vector3 scale = Vector3.one * profileScale;
                        scale.x = savedScaleX * profileScale; // 翻转时也要应用profile的scale

                        RectTransform charRect = VNAPI.GetCharRect(posCode);
                        if (charRect != null)
                        {
                            charRect.localScale = scale;
                            VNDebug.LogVerbose($"[VNManager] 应用位置 {position}({posCode}) 的完整scale - ProfileScale: {profileScale}, Flip: {savedScaleX}, FinalScale: {scale}");
                        }
                    }
                    else
                    {
                        // 如果找不到profile，使用旧的逻辑（只应用翻转状态）
                        RectTransform charRect = VNAPI.GetCharRect(posCode);
                        if (charRect != null)
                        {
                            Vector3 scale = charRect.localScale;
                            scale.x = savedScaleX;
                            charRect.localScale = scale;
                            VNDebug.LogVerboseWarning($"[VNManager] 找不到角色 {characterID} 的Profile，只应用翻转状态: {savedScaleX}");
                        }
                    }
                }
            }
        }
    }

    private void UpdateAudioState(StoryLine currentLine)
    {
        if (!string.IsNullOrEmpty(currentLine.BGM))
        {
            if (currentLine.BGM == "stop") { MusicManager.GetInstance().StopBGM(); currentBGM = ""; }
            else if (currentLine.BGM == "pause") MusicManager.GetInstance().PauseBGM();
            else if (currentLine.BGM == "resume") MusicManager.GetInstance().PlayBGM(currentBGM);
            else
            {
                // 【修复】如果新 BGM 和当前 BGM 相同，跳过播放，避免重复播放导致不连贯
                if (currentLine.BGM != currentBGM)
                {
                    MusicManager.GetInstance().PlayBGM(currentLine.BGM);
                    currentBGM = currentLine.BGM;
                }
                else
                {
                    VNDebug.LogVerbose($"[VNManager] BGM {currentLine.BGM} 已在播放，跳过重复播放");
                }
            }
        }

        if (!string.IsNullOrEmpty(currentLine.Voice))
        {
            // 检查VoiceManager是否已初始化
            if (VoiceManager.GetInstance() != null)
            {
                // 检查语音路径是否有效（不包含无效字符）
                string voicePath = currentLine.Voice.Trim();
                if (!string.IsNullOrEmpty(voicePath) && !voicePath.Contains("://"))
                {
                    VoiceManager.GetInstance().PlayVoice(voicePath);
                }
                else
                {
                    Debug.LogWarning($"[VNManager] 无效的语音路径: {voicePath}");
                }
            }
            else
            {
                Debug.LogWarning("[VNManager] VoiceManager未初始化，无法播放语音");
            }
        }
    }

    private void UpdateDialogue(StoryLine currentLine)
    {
        string finalSpeaker = currentLine.Speaker;
        string finalText = currentLine.Text;

        // 启用本地化：每行独立解析，不在行与行之间继承译文（空/缺失则按配置回退 CSV）
        if (VNLocalizationService.IsEnabled())
        {
            bool fallbackToCsv = VNProjectConfig.Instance != null && VNProjectConfig.Instance.FallbackToCsvWhenMissing;

            if (VNLocalizationService.TryGetSpeaker(currentScriptName, currentLine.ID, out var localizedSpeaker) && !string.IsNullOrEmpty(localizedSpeaker))
                finalSpeaker = localizedSpeaker;
            else
                finalSpeaker = fallbackToCsv ? currentLine.Speaker : "";

            if (VNLocalizationService.TryGetText(currentScriptName, currentLine.ID, out var localizedText) && !string.IsNullOrEmpty(localizedText))
                finalText = localizedText;
            else
                finalText = fallbackToCsv ? currentLine.Text : "";
        }

        _dialogueEventScratch.Clear();
        _dialogueEventScratch[VNGameEvents.KeySpeaker] = finalSpeaker;
        _dialogueEventScratch[VNGameEvents.KeyText] = finalText;
        EventCenter.GetInstance().EventTrigger(VNGameEvents.UpdateDialogue, _dialogueEventScratch);

        _headProfileEventScratch.Clear();
        _headProfileEventScratch[VNGameEvents.KeyHeadProfile] = string.IsNullOrEmpty(currentLine.HeadProfile) ? "hide" : currentLine.HeadProfile;
        _headProfileEventScratch[VNGameEvents.KeySpeaker] = finalSpeaker;
        EventCenter.GetInstance().EventTrigger(VNGameEvents.UpdateHeadProfile, _headProfileEventScratch);

        // isTextDisplaying = true;
        // AddHistoryEntry(finalSpeaker, finalText, currentLine.Voice);
        isTextDisplaying = true;

        bool skipHistory = HasNoteFlag(currentLine.Note, "nohistory");
        if (!skipHistory)
        {
            AddHistoryEntry(finalSpeaker, finalText, currentLine.Voice);
        }
    }

    private bool HasNoteFlag(string note, string flag)
    {
        if (string.IsNullOrWhiteSpace(note) || string.IsNullOrWhiteSpace(flag))
            return false;

        string marker = $"[{flag}]";
        if (note.IndexOf(marker, System.StringComparison.OrdinalIgnoreCase) >= 0)
            return true;

        return Regex.IsMatch(
            note,
            $@"(?<![A-Za-z0-9_]){Regex.Escape(flag)}(?![A-Za-z0-9_])",
            RegexOptions.IgnoreCase);
    }

    private bool TryShowToBeContinuedPanelForLine(StoryLine line)
    {
        if (line == null || !HasNoteFlag(line.Note, ToBeContinuedNoteFlag))
        {
            return false;
        }

        EventCenter.GetInstance().EventTrigger(VNGameEvents.DisplayAllText);
        ShowToBeContinuedPanel();
        return true;
    }

    private void ShowToBeContinuedPanel()
    {
        if (isToBeContinuedPanelShowing)
        {
            return;
        }

        isToBeContinuedPanelShowing = true;
        isAutoPlaying = false;
        isSkipping = false;
        isTextDisplaying = false;

        if (_autoPlayCoroutine != null)
        {
            MonoManager.GetInstance().StopCoroutine(_autoPlayCoroutine);
            _autoPlayCoroutine = null;
        }

        ClearPendingPostTextCommand();
        ClearAdvanceAfterCommandsRequest();
        ClearStopCommandsForEndingChoiceRequest();

        UIManager.GetInstance().ShowPanel<ToBeContinuedPanel>(
            "ToBeContinuedPanel",
            ToBeContinuedPanelPath,
            E_UI_Layer.System,
            null);
    }

    private bool ResolveHiddenBranchControlLine()
    {
        if (CurrentLineIndex < 0 || CurrentLineIndex >= StoryLines.Count)
            return true;

        int guard = StoryLines.Count + 1;

        while (CurrentLineIndex >= 0 && CurrentLineIndex < StoryLines.Count && guard-- > 0)
        {
            StoryLine line = StoryLines[CurrentLineIndex];
            if (!IsHiddenBranchControlLine(line))
                return true;

            if (!TryGetLineLevel(line, out int parentLevel))
            {
                CurrentLineIndex++;
                continue;
            }

            int targetIndex = FindMatchedChildBranchIndex(CurrentLineIndex, parentLevel);
            if (targetIndex < 0)
                targetIndex = FindNextSameLevelIndex(CurrentLineIndex, parentLevel);
            if (targetIndex < 0 && !HasChildBranchConditions(CurrentLineIndex, parentLevel))
                targetIndex = FindFirstChildLevelIndex(CurrentLineIndex, parentLevel);

            if (targetIndex < 0)
            {
                CurrentLineIndex = StoryLines.Count;
                return true;
            }

            VNDebug.LogVerbose($"[VNManager] Hidden branch line {line.ID} resolved to index {targetIndex}");
            Debug.Log($"[VNManager][NoteBranch] Hidden line ID={line.ID}, index={CurrentLineIndex}, level={parentLevel}, tags=[{GetRecordedScriptTagsForDebug()}], resolvedIndex={targetIndex}, resolvedID={(targetIndex >= 0 && targetIndex < StoryLines.Count ? StoryLines[targetIndex].ID : "")}");
            CurrentLineIndex = targetIndex;
        }

        if (guard <= 0)
            Debug.LogError("[VNManager] Hidden branch resolution exceeded guard limit.");

        return CurrentLineIndex >= 0 && CurrentLineIndex < StoryLines.Count;
    }

    private bool IsHiddenBranchControlLine(StoryLine line)
    {
        if (line == null)
            return false;

        string sourceType = GetNoteValue(line.Note, "\u6e90\u7c7b\u578b");
        return string.Equals(sourceType, "type:exeBC", System.StringComparison.OrdinalIgnoreCase) ||
               (!string.IsNullOrEmpty(line.Note) &&
                line.Note.IndexOf("type:exeBC", System.StringComparison.OrdinalIgnoreCase) >= 0);
    }

    private int FindMatchedChildBranchIndex(int parentIndex, int parentLevel)
    {
        int childLevel = parentLevel + 1;
        int fallbackCandidateIndex = -1;
        StoryLine parentLine = parentIndex >= 0 && parentIndex < StoryLines.Count ? StoryLines[parentIndex] : null;
        Debug.Log($"[VNManager][NoteBranch] Match start parentID={parentLine?.ID}, parentIndex={parentIndex}, parentLevel={parentLevel}, childLevel={childLevel}, tags=[{GetRecordedScriptTagsForDebug()}]");

        for (int i = parentIndex + 1; i < StoryLines.Count; i++)
        {
            StoryLine candidate = StoryLines[i];
            if (!TryGetLineLevel(candidate, out int level))
                continue;

            if (level <= parentLevel)
                break;

            if (level != childLevel)
                continue;

            if (TryEvaluateWhereList(candidate.Note, out bool isMatch, out string conditionSummary))
            {
                Debug.Log($"[VNManager][NoteBranch] Candidate ID={candidate.ID}, index={i}, level={level}, conditions=[{conditionSummary}], matched={isMatch}, note={candidate.Note}");
                if (isMatch)
                {
                    Debug.Log($"[VNManager][NoteBranch] Matched candidate ID={candidate.ID} by wherelist");
                    return i;
                }

                continue;
            }

            // A wherelist that is present but unsupported/malformed must never be
            // reduced to "any mentioned tag exists". That legacy interpretation can
            // invert target=0 conditions and select the wrong branch.
            if (!string.IsNullOrWhiteSpace(GetNoteValue(candidate.Note, "wherelist")))
            {
                Debug.LogError($"[VNManager][NoteBranch] Skipping candidate ID={candidate.ID} because its wherelist could not be evaluated.");
                continue;
            }

            // Compatibility fallback for old notes that contain condition tags but no
            // complete wherelist payload.
            List<string> conditionTags = new List<string>(ExtractConditionTags(candidate.Note));
            Debug.Log($"[VNManager][NoteBranch] Candidate ID={candidate.ID}, index={i}, level={level}, legacyConditions=[{string.Join(",", conditionTags)}], note={candidate.Note}");

            if (conditionTags.Count == 0)
            {
                if (fallbackCandidateIndex < 0)
                {
                    fallbackCandidateIndex = i;
                    Debug.Log($"[VNManager][NoteBranch] Candidate ID={candidate.ID} recorded as unconditional fallback.");
                }

                continue;
            }

            foreach (string conditionTag in conditionTags)
            {
                string normalizedConditionTag = NormalizeScriptTag(conditionTag);
                if (!HasRecordedScriptTag(normalizedConditionTag))
                    continue;

                Debug.Log($"[VNManager][NoteBranch] Matched legacy candidate ID={candidate.ID} by tag={normalizedConditionTag}");
                return i;
            }
        }

        if (fallbackCandidateIndex >= 0)
        {
            Debug.Log($"[VNManager][NoteBranch] Using unconditional fallback candidate ID={StoryLines[fallbackCandidateIndex].ID}, index={fallbackCandidateIndex}");
            return fallbackCandidateIndex;
        }

        Debug.Log($"[VNManager][NoteBranch] No matched child for parentID={parentLine?.ID}");
        return -1;
    }

    private bool HasChildBranchConditions(int parentIndex, int parentLevel)
    {
        int childLevel = parentLevel + 1;

        for (int i = parentIndex + 1; i < StoryLines.Count; i++)
        {
            StoryLine candidate = StoryLines[i];
            if (!TryGetLineLevel(candidate, out int level))
                continue;

            if (level <= parentLevel)
                break;

            if (level != childLevel)
                continue;

            foreach (string conditionTag in ExtractConditionTags(candidate.Note))
            {
                if (!string.IsNullOrEmpty(NormalizeScriptTag(conditionTag)))
                    return true;
            }
        }

        return false;
    }

    private int FindFirstChildLevelIndex(int parentIndex, int parentLevel)
    {
        int childLevel = parentLevel + 1;

        for (int i = parentIndex + 1; i < StoryLines.Count; i++)
        {
            if (!TryGetLineLevel(StoryLines[i], out int level))
                continue;

            if (level <= parentLevel)
                break;

            if (level == childLevel)
                return i;
        }

        return -1;
    }

    private int FindNextSameLevelIndex(int currentIndex, int level)
    {
        for (int i = currentIndex + 1; i < StoryLines.Count; i++)
        {
            if (!TryGetLineLevel(StoryLines[i], out int candidateLevel))
                continue;

            if (candidateLevel == level)
                return i;

            if (candidateLevel < level)
                break;
        }

        return -1;
    }

    private IEnumerable<string> ExtractConditionTags(string note)
    {
        if (string.IsNullOrWhiteSpace(note))
            yield break;

        foreach (Match conditionBlock in Regex.Matches(note, "\"?conditions\"?\\s*:\\s*\\[(.*?)\\]", RegexOptions.Singleline))
        {
            string block = conditionBlock.Groups.Count > 1 ? conditionBlock.Groups[1].Value : "";
            foreach (Match match in Regex.Matches(block, "\"?valuedata\"?\\s*:\\s*(?:\"([^\"]*)\"|([^,}\\]]+))"))
            {
                if (match.Groups.Count > 2)
                {
                    string rawTag = match.Groups[1].Success ? match.Groups[1].Value : match.Groups[2].Value;
                    string tag = NormalizeScriptTag(rawTag);
                    if (!string.IsNullOrEmpty(tag))
                        yield return tag;
                }
            }
        }
    }

    [System.Serializable]
    private sealed class NoteWhereList
    {
        public string wheretype;
        public NoteWhereData[] wheredatas;
    }

    [System.Serializable]
    private sealed class NoteWhereData
    {
        public string way;
        public NoteWhereValue[] conditions;
        public NoteWhereValue[] targets;
    }

    [System.Serializable]
    private sealed class NoteWhereValue
    {
        public string valuetype;
        public string valuedata;
    }

    /// <summary>
    /// Evaluates VNovelizer's wherelist format for tag conditions.
    /// wheretype 1 joins entries with OR; wheretype 2 joins them with AND.
    /// A target value of 1 expects the tag to exist, while 0 expects it not to exist.
    /// </summary>
    private bool TryEvaluateWhereList(string note, out bool matches, out string summary)
    {
        matches = false;
        summary = "";

        string json = GetNoteValue(note, "wherelist");
        if (string.IsNullOrWhiteSpace(json))
            return false;

        NoteWhereList whereList;
        try
        {
            whereList = JsonUtility.FromJson<NoteWhereList>(json);
        }
        catch (System.Exception exception)
        {
            Debug.LogWarning($"[VNManager][NoteBranch] Invalid wherelist JSON: {exception.Message}; note={note}");
            return false;
        }

        if (whereList == null || whereList.wheredatas == null || whereList.wheredatas.Length == 0)
            return false;

        bool joinWithAnd;
        if (whereList.wheretype == "1")
            joinWithAnd = false;
        else if (whereList.wheretype == "2")
            joinWithAnd = true;
        else
            return false;

        List<bool> results = new List<bool>();
        List<string> descriptions = new List<string>();

        foreach (NoteWhereData whereData in whereList.wheredatas)
        {
            if (whereData == null || whereData.conditions == null || whereData.conditions.Length == 0 ||
                whereData.targets == null || whereData.targets.Length == 0)
                return false;

            NoteWhereValue condition = whereData.conditions[0];
            NoteWhereValue target = whereData.targets[0];
            if (condition == null || target == null || condition.valuetype != "4" ||
                !TryParseWhereBoolean(target.valuedata, out bool expected))
                return false;

            string tag = NormalizeScriptTag(condition.valuedata);
            if (string.IsNullOrEmpty(tag))
                return false;

            bool actual = HasRecordedScriptTag(tag);
            bool comparison;
            switch ((whereData.way ?? "=").Trim())
            {
                case "=":
                case "==":
                    comparison = actual == expected;
                    break;
                case "!=":
                case "<>":
                case "≠":
                    comparison = actual != expected;
                    break;
                default:
                    return false;
            }

            results.Add(comparison);
            descriptions.Add($"{tag} {whereData.way} {(expected ? 1 : 0)} => {comparison}");
        }

        matches = joinWithAnd;
        foreach (bool result in results)
        {
            if (joinWithAnd)
                matches &= result;
            else
                matches |= result;
        }

        summary = $"{(joinWithAnd ? "AND" : "OR")}: {string.Join(", ", descriptions)}";
        return true;
    }

    private static bool TryParseWhereBoolean(string value, out bool result)
    {
        value = value == null ? "" : value.Trim().Trim('"', '\'');
        if (value == "1")
        {
            result = true;
            return true;
        }

        if (value == "0")
        {
            result = false;
            return true;
        }

        return bool.TryParse(value, out result);
    }

    private void RecordScriptTagFromNote(string note)
    {
        string tag = NormalizeScriptTag(GetNoteValue(note, "tag"));
        if (string.IsNullOrEmpty(tag))
            return;

        recordedScriptTags.Add(tag);
        GlobalDataManager.GetInstance().SetBoolFlag(ScriptTagFlagPrefix + tag, true);
        VNDebug.LogVerbose($"[VNManager] Recorded script tag: {tag}");
        Debug.Log($"[VNManager][NoteBranch] Recorded tag={tag}, allTags=[{GetRecordedScriptTagsForDebug()}], note={note}");
    }

    public void SetDebugScriptTags(IEnumerable<string> tags)
    {
        pendingDebugScriptTags.Clear();
        hasPendingDebugScriptTags = true;

        if (tags == null)
            return;

        foreach (string rawTag in tags)
        {
            string tag = NormalizeScriptTag(rawTag);
            if (!string.IsNullOrEmpty(tag) && !pendingDebugScriptTags.Contains(tag))
                pendingDebugScriptTags.Add(tag);
        }
    }

    private void ApplyPendingDebugScriptTags()
    {
        if (!hasPendingDebugScriptTags)
            return;

        ClearRecordedScriptTags();

        foreach (string tag in pendingDebugScriptTags)
        {
            recordedScriptTags.Add(tag);
            GlobalDataManager.GetInstance().SetBoolFlag(ScriptTagFlagPrefix + tag, true);
            VNDebug.LogVerbose($"[VNManager] Applied debug script tag: {tag}");
            Debug.Log($"[VNManager][NoteBranch] Applied debug tag={tag}");
        }

        pendingDebugScriptTags.Clear();
        hasPendingDebugScriptTags = false;
        Debug.Log($"[VNManager][NoteBranch] Debug tags ready: [{GetRecordedScriptTagsForDebug()}]");
    }

    private void ClearRecordedScriptTags()
    {
        recordedScriptTags.Clear();
        Debug.Log("[VNManager][NoteBranch] Cleared recorded script tags.");

        var data = GlobalDataManager.GetInstance().GetGlobalData();
        if (data == null || data.Flags == null)
            return;

        List<string> keysToRemove = new List<string>();
        foreach (var kvp in data.Flags)
        {
            if (kvp.Key.StartsWith(ScriptTagFlagPrefix, System.StringComparison.Ordinal))
                keysToRemove.Add(kvp.Key);
        }

        foreach (string key in keysToRemove)
            data.Flags.Remove(key);
    }

    private void LoadRecordedScriptTagsFromGlobalFlags()
    {
        recordedScriptTags.Clear();

        var data = GlobalDataManager.GetInstance().GetGlobalData();
        if (data == null || data.Flags == null)
            return;

        foreach (var kvp in data.Flags)
        {
            if (kvp.Value && kvp.Key.StartsWith(ScriptTagFlagPrefix, System.StringComparison.Ordinal))
                recordedScriptTags.Add(kvp.Key.Substring(ScriptTagFlagPrefix.Length));
        }
    }

    private bool HasRecordedScriptTag(string tag)
    {
        tag = NormalizeScriptTag(tag);
        if (string.IsNullOrEmpty(tag))
            return false;

        bool hasTag = recordedScriptTags.Contains(tag) ||
                      GlobalDataManager.GetInstance().GetBoolFlag(ScriptTagFlagPrefix + tag);
        Debug.Log($"[VNManager][NoteBranch] Check tag={tag}, result={hasTag}, allTags=[{GetRecordedScriptTagsForDebug()}]");
        return hasTag;
    }

    private string GetRecordedScriptTagsForDebug()
    {
        return string.Join(",", recordedScriptTags);
    }

    private string NormalizeScriptTag(string tag)
    {
        if (string.IsNullOrWhiteSpace(tag))
            return "";

        tag = tag.Trim().Trim('"', '\'');
        const string prefix = "tag=";
        if (tag.StartsWith(prefix, System.StringComparison.OrdinalIgnoreCase))
            tag = tag.Substring(prefix.Length).Trim().Trim('"', '\'');

        return tag;
    }

    private bool TryGetLineLevel(StoryLine line, out int level)
    {
        level = 0;
        if (line == null)
            return false;

        string value = GetNoteValue(line.Note, "\u5c42\u7ea7");
        if (int.TryParse(value, out level))
            return true;

        if (string.IsNullOrEmpty(line.Note))
            return false;

        Match match = Regex.Match(line.Note, @"(?:\u5c42\u7ea7|灞傜骇)\s*=\s*(\d+)");
        return match.Success && int.TryParse(match.Groups[1].Value, out level);
    }

    private string GetNoteValue(string note, string key)
    {
        if (string.IsNullOrWhiteSpace(note) || string.IsNullOrWhiteSpace(key))
            return "";

        string marker = key + "=";
        int start = note.IndexOf(marker, System.StringComparison.OrdinalIgnoreCase);
        if (start < 0)
            return "";

        start += marker.Length;
        int end = note.IndexOf(';', start);
        if (end < 0)
            end = note.Length;

        return note.Substring(start, end - start).Trim();
    }

    private void TrackSelectBCLine(StoryLine line)
    {
        if (line == null || !IsSelectBCLine(line))
        {
            return;
        }

        lastSelectBCLineIndex = CurrentLineIndex;
        selectBCRecordedTagSnapshots[CurrentLineIndex] = new HashSet<string>(recordedScriptTags);
        VNDebug.LogVerbose($"[VNManager] Recorded selectBC line index: {CurrentLineIndex}, ID={line.ID}");
    }

    private bool IsSelectBCLine(StoryLine line)
    {
        if (line == null)
        {
            return false;
        }

        string sourceType = GetNoteValue(line.Note, "\u6e90\u7c7b\u578b");
        return string.Equals(sourceType, "type:selectBC", System.StringComparison.OrdinalIgnoreCase) ||
               (!string.IsNullOrEmpty(line.Note) &&
                line.Note.IndexOf("type:selectBC", System.StringComparison.OrdinalIgnoreCase) >= 0);
    }

    public bool ReturnToLastSelectBC()
    {
        int targetIndex = ResolveLastSelectBCIndex();
        if (targetIndex < 0)
        {
            return false;
        }

        StopCurrentFlowForEndingChoice();
        RestoreSelectBCSnapshot(targetIndex);
        ClearStopCommandsForEndingChoiceRequest();
        GameStateManager.GetInstance().SetState(GameState.Gameplay);
        CurrentLineIndex = targetIndex;
        lastLine = null;
        PlayCurrentLine();
        return true;
    }

    private int ResolveLastSelectBCIndex()
    {
        if (lastSelectBCLineIndex >= 0 &&
            lastSelectBCLineIndex < StoryLines.Count &&
            IsSelectBCLine(StoryLines[lastSelectBCLineIndex]))
        {
            return lastSelectBCLineIndex;
        }

        int startIndex = Mathf.Min(CurrentLineIndex, StoryLines.Count - 1);
        for (int i = startIndex; i >= 0; i--)
        {
            if (IsSelectBCLine(StoryLines[i]))
            {
                lastSelectBCLineIndex = i;
                if (!selectBCRecordedTagSnapshots.ContainsKey(i))
                {
                    selectBCRecordedTagSnapshots[i] = new HashSet<string>();
                }
                VNDebug.LogVerbose($"[VNManager] Found selectBC by backward scan: index={i}, ID={StoryLines[i].ID}");
                return i;
            }
        }

        return -1;
    }

    private void RestoreSelectBCSnapshot(int lineIndex)
    {
        ClearRecordedScriptTags();

        if (!selectBCRecordedTagSnapshots.TryGetValue(lineIndex, out HashSet<string> snapshot) || snapshot == null)
        {
            return;
        }

        foreach (string tag in snapshot)
        {
            if (string.IsNullOrEmpty(tag))
            {
                continue;
            }

            recordedScriptTags.Add(tag);
            GlobalDataManager.GetInstance().SetBoolFlag(ScriptTagFlagPrefix + tag, true);
        }
    }

    public void ReturnToTitleFromEndingChoice()
    {
        StopCurrentFlowForEndingChoice();
        GameStateManager.GetInstance().SetState(GameState.Gameplay);

        UIManager.GetInstance().HidePanel("ChoicePanel");
        UIManager.GetInstance().HidePanel("PausePanel");
        UIManager.GetInstance().HidePanel("VNGameplayPanel");

        string mainMenuPath = VNProjectConfig.Instance != null && !string.IsNullOrEmpty(VNProjectConfig.Instance.UI_MainMenuPath)
            ? VNProjectConfig.Instance.UI_MainMenuPath
            : "VNovelizerRes/VNPrefabs/UI/MainMenu";

        UIManager.GetInstance().ShowPanel<MainMenuPanel>("MainMenuPanel", mainMenuPath, E_UI_Layer.Middle, null);
    }

    public void ReturnToMainMenuFromToBeContinued()
    {
        isToBeContinuedPanelShowing = false;

        StopCurrentFlowForEndingChoice();
        GameStateManager.GetInstance().SetState(GameState.Gameplay);
        VNAPI.ClearAllEffects();
        PoolManager.GetInstance().Clear();

        UIManager.GetInstance().HidePanel("ToBeContinuedPanel");
        UIManager.GetInstance().HidePanel("ChoicePanel");
        UIManager.GetInstance().HidePanel("PausePanel");
        UIManager.GetInstance().HidePanel("VNGameplayPanel");

        string mainMenuPath = VNProjectConfig.Instance != null && !string.IsNullOrEmpty(VNProjectConfig.Instance.UI_MainMenuPath)
            ? VNProjectConfig.Instance.UI_MainMenuPath
            : "VNovelizerRes/VNPrefabs/UI/MainMenu";

        UIManager.GetInstance().ShowPanel<MainMenuPanel>("MainMenuPanel", mainMenuPath, E_UI_Layer.Middle, null);
    }

    private void StopCurrentFlowForEndingChoice()
    {
        if (_flowCoroutine != null)
        {
            MonoManager.GetInstance().StopCoroutine(_flowCoroutine);
            _flowCoroutine = null;
        }

        if (_autoPlayCoroutine != null)
        {
            MonoManager.GetInstance().StopCoroutine(_autoPlayCoroutine);
            _autoPlayCoroutine = null;
        }

        CommandManager.GetInstance().InterruptAll();
        ClearAdvanceAfterCommandsRequest();
        isAutoPlaying = false;
        isSkipping = false;
        isTextDisplaying = false;
    }


    public void UpdateCurrentBG_OnlyData(string bgName)
    {
        bool backgroundChanged = this.currentBG != bgName;
        this.currentBG = bgName;

        if (backgroundChanged)
        {
            RefreshAutoSavePreviewForCurrentBackground();
        }
    }

    public void ToggleAutoPlay()
    {
        isAutoPlaying = !isAutoPlaying;
        if (isAutoPlaying)
        {
            VNDebug.LogVerbose("[VNManager] 自动播放已开启");
        }
        else
        {
            VNDebug.LogVerbose("[VNManager] 自动播放已关闭");
        }
        EventCenter.GetInstance().EventTrigger(VNGameEvents.ToggleAutoPlay, isAutoPlaying);
        CheckAndTriggerAutoPlay();
    }

    public void ToggleSkip()
    {
        isSkipping = !isSkipping;
        EventCenter.GetInstance().EventTrigger(VNGameEvents.ToggleSkip, isSkipping);
    }

    public void SaveGame(int slotIndex)
    {
        string screenshotPath = SaveManager.GetInstance().SaveCachedScreenshot(slotIndex);
        SaveData saveData = BuildSaveData(slotIndex, screenshotPath);
        SaveManager.GetInstance().SaveGame(slotIndex, saveData);
    }

    private SaveData BuildSaveData(int slotIndex, string screenshotPath)
    {
        SaveData saveData = new SaveData();
        saveData.ScriptFileName = this.currentScriptName;
        saveData.LineID = lastLine != null ? lastLine.ID : "";
        saveData.CurrentBG = this.currentBG;
        saveData.CurrentBGM = GetCurrentBGMForSave();
        Debug.Log($"[VNManager][BGM] SaveGame slot={slotIndex}, script='{saveData.ScriptFileName}', lineID='{saveData.LineID}', managerBGM='{currentBGM}', musicManagerBGM='{MusicManager.GetInstance().GetCurrentBGMName()}', savedBGM='{saveData.CurrentBGM}'");
        saveData.Characters = new Dictionary<string, string>(this.currentCharacters);
        saveData.CharacterScaleX = new Dictionary<string, float>(this.currentCharactersScaleX);
        saveData.Flags = new Dictionary<string, bool>(GlobalDataManager.GetInstance().GetGlobalData().Flags);
        saveData.IntFlags = new Dictionary<string, int>(GlobalDataManager.GetInstance().GetGlobalData().IntFlags);
        saveData.StringFlags = new Dictionary<string, string>(GlobalDataManager.GetInstance().GetGlobalData().StringFlags);
        saveData.ActiveEffects = new List<string>(this.activeEffects); // 保存特效

        // 保存历史记录
        List<HistoryEntry> historyLog = GlobalDataManager.GetInstance().GetHistoryLog();
        if (historyLog != null)
        {
            saveData.HistoryLog = new List<HistoryEntry>(historyLog);
            VNDebug.LogVerbose($"[VNManager] 保存了 {historyLog.Count} 条历史记录");

            // 验证历史记录数据
            if (historyLog.Count > 0)
            {
                var firstEntry = historyLog[0];
                VNDebug.LogVerbose($"[VNManager] 第一条历史记录示例 - Speaker: {firstEntry?.Speaker ?? "null"}, Text: {firstEntry?.Text?.Substring(0, Mathf.Min(20, firstEntry.Text?.Length ?? 0)) ?? "null"}");
            }
        }
        else
        {
            saveData.HistoryLog = new List<HistoryEntry>();
            Debug.LogWarning("[VNManager] 历史记录为null，已初始化为空列表");
        }

        saveData.SaveTime = System.DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
        saveData.ScreenshotPath = screenshotPath ?? "";
        return saveData;
    }

    public void AutoSaveGame()
    {
        if (!CanAutoSave())
        {
            return;
        }

        SaveAutoSaveDataOnly(GetLastAutoSaveScreenshotPath());
        Debug.Log("[VNManager] 自动存档已保存到第一个存档槽");
    }

    private void AutoSaveCurrentLine(bool refreshScreenshot)
    {
        if (isReplayMode || !CanAutoSave())
        {
            return;
        }

        string currentScreenshotPath = GetLastAutoSaveScreenshotPath();
        bool needsPreviewImage = refreshScreenshot || string.IsNullOrEmpty(currentScreenshotPath);

        if (needsPreviewImage && CanUseBackgroundPreview(currentBG))
        {
            if (_autoSaveBackgroundPreviewCoroutine != null)
            {
                MonoManager.GetInstance().StopCoroutine(_autoSaveBackgroundPreviewCoroutine);
            }

            string backgroundPath = currentBG;
            _autoSaveBackgroundPreviewCoroutine = MonoManager.GetInstance().StartCoroutine(SaveAutoSaveBackgroundPreviewAndData(backgroundPath));
            return;
        }

        if (_autoSaveBackgroundPreviewCoroutine != null)
        {
            return;
        }

        SaveAutoSaveDataOnly(currentScreenshotPath);
    }

    private void SaveAutoSaveDataOnly(string screenshotPath)
    {
        SaveData saveData = BuildSaveData(AutoSaveSlotIndex, screenshotPath);
        SaveManager.GetInstance().SaveGame(AutoSaveSlotIndex, saveData);
    }

    private void RefreshAutoSavePreviewForCurrentBackground()
    {
        if (isReplayMode || !CanAutoSave() || !CanUseBackgroundPreview(currentBG))
        {
            return;
        }

        if (_autoSaveBackgroundPreviewCoroutine != null)
        {
            MonoManager.GetInstance().StopCoroutine(_autoSaveBackgroundPreviewCoroutine);
        }

        string backgroundPath = currentBG;
        _autoSaveBackgroundPreviewCoroutine = MonoManager.GetInstance().StartCoroutine(SaveAutoSaveBackgroundPreviewAndData(backgroundPath));
    }

    private IEnumerator SaveAutoSaveBackgroundPreviewAndData(string backgroundPath)
    {
        string screenshotPath = GetLastAutoSaveScreenshotPath();
        yield return SaveBackgroundPreview(backgroundPath, path => screenshotPath = path);

        if (!string.IsNullOrEmpty(screenshotPath))
        {
            lastAutoSaveScreenshotPath = screenshotPath;
        }

        SaveAutoSaveDataOnly(screenshotPath);
        _autoSaveBackgroundPreviewCoroutine = null;
    }

    private IEnumerator SaveBackgroundPreview(string backgroundPath, System.Action<string> onSaved)
    {
        List<string> candidatePaths = GetBackgroundCandidatePaths(backgroundPath);

        foreach (string path in candidatePaths)
        {
            Texture2D previewTexture = null;
            yield return LoadBackgroundPreviewTexture(path, texture => previewTexture = texture);

            if (previewTexture == null)
            {
                Debug.LogWarning($"[VNManager] Auto-save background preview load failed: {path}");
                continue;
            }

            string screenshotPath = SaveManager.GetInstance().SaveScreenshot(AutoSaveSlotIndex, previewTexture);
            Object.Destroy(previewTexture);
            Debug.Log($"[VNManager] Auto-save background preview saved: {backgroundPath} -> {screenshotPath}");
            onSaved?.Invoke(screenshotPath);
            yield break;
        }

        onSaved?.Invoke(GetLastAutoSaveScreenshotPath());
    }

    private IEnumerator LoadBackgroundPreviewTexture(string path, System.Action<Texture2D> onLoaded)
    {
        bool spriteLoaded = false;
        Sprite spriteResult = null;

        ResourcesManager.GetInstance().LoadOptionalAsync<Sprite>(path, sprite =>
        {
            spriteResult = sprite;
            spriteLoaded = true;
        });

        while (!spriteLoaded)
        {
            yield return null;
        }

        if (spriteResult != null)
        {
            onLoaded?.Invoke(CreateReadableTextureCopy(spriteResult));
            yield break;
        }

        bool textureLoaded = false;
        Texture2D textureResult = null;

        ResourcesManager.GetInstance().LoadOptionalAsync<Texture2D>(path, texture =>
        {
            textureResult = texture;
            textureLoaded = true;
        });

        while (!textureLoaded)
        {
            yield return null;
        }

        onLoaded?.Invoke(textureResult != null ? CreateReadableTextureCopy(textureResult) : null);
    }

    private Texture2D CreateReadableTextureCopy(Texture source)
    {
        if (source == null)
        {
            return null;
        }

        RenderTexture previous = RenderTexture.active;
        RenderTexture renderTexture = RenderTexture.GetTemporary(source.width, source.height, 0, RenderTextureFormat.ARGB32);
        Graphics.Blit(source, renderTexture);
        RenderTexture.active = renderTexture;

        Texture2D readableTexture = new Texture2D(source.width, source.height, TextureFormat.RGBA32, false);
        readableTexture.ReadPixels(new Rect(0, 0, source.width, source.height), 0, 0);
        readableTexture.Apply();

        RenderTexture.active = previous;
        RenderTexture.ReleaseTemporary(renderTexture);
        return readableTexture;
    }

    private Texture2D CreateReadableTextureCopy(Sprite sprite)
    {
        if (sprite == null || sprite.texture == null)
        {
            return null;
        }

        Rect spriteRect = sprite.textureRect;
        Texture2D textureCopy = CreateReadableTextureCopy(sprite.texture);
        if (textureCopy == null)
        {
            return null;
        }

        if (Mathf.Approximately(spriteRect.width, textureCopy.width) &&
            Mathf.Approximately(spriteRect.height, textureCopy.height) &&
            Mathf.Approximately(spriteRect.x, 0f) &&
            Mathf.Approximately(spriteRect.y, 0f))
        {
            return textureCopy;
        }

        Color[] pixels = textureCopy.GetPixels(
            Mathf.RoundToInt(spriteRect.x),
            Mathf.RoundToInt(spriteRect.y),
            Mathf.RoundToInt(spriteRect.width),
            Mathf.RoundToInt(spriteRect.height));

        Texture2D croppedTexture = new Texture2D(Mathf.RoundToInt(spriteRect.width), Mathf.RoundToInt(spriteRect.height), TextureFormat.RGBA32, false);
        croppedTexture.SetPixels(pixels);
        croppedTexture.Apply();
        Object.Destroy(textureCopy);
        return croppedTexture;
    }

    private bool CanUseBackgroundPreview(string backgroundPath)
    {
        return !string.IsNullOrEmpty(backgroundPath) &&
               backgroundPath != "hide" &&
               backgroundPath != "black";
    }

    private List<string> GetBackgroundCandidatePaths(string backgroundPath)
    {
        List<string> paths = new List<string>();

        if (backgroundPath.Contains("/"))
        {
            paths.Add(backgroundPath);
            return paths;
        }

        if (VNProjectConfig.Instance != null && !string.IsNullOrEmpty(VNProjectConfig.Instance.BackgroundResPath))
        {
            paths.Add(VNProjectConfig.Instance.BackgroundResPath + "/" + backgroundPath);
        }

        paths.Add("Backgrounds/" + backgroundPath);
        return paths;
    }

    private string GetLastAutoSaveScreenshotPath()
    {
        if (!string.IsNullOrEmpty(lastAutoSaveScreenshotPath))
        {
            return lastAutoSaveScreenshotPath;
        }

        SaveData existingAutoSave = SaveManager.GetInstance().LoadGame(AutoSaveSlotIndex);
        if (existingAutoSave != null && !string.IsNullOrEmpty(existingAutoSave.ScreenshotPath))
        {
            lastAutoSaveScreenshotPath = existingAutoSave.ScreenshotPath;
        }

        return lastAutoSaveScreenshotPath;
    }

    private bool CanAutoSave()
    {
        return !string.IsNullOrEmpty(currentScriptName) && lastLine != null;
    }

    private string GetCurrentBGMForSave()
    {
        if (!string.IsNullOrEmpty(currentBGM))
        {
            return currentBGM;
        }

        string playingBGM = MusicManager.GetInstance().GetCurrentBGMName();
        return string.IsNullOrEmpty(playingBGM) ? "" : playingBGM;
    }

    private void OnApplicationQuit()
    {
        AutoSaveGame();
    }

    private void OnApplicationPause(bool pauseStatus)
    {
        if (pauseStatus)
        {
            AutoSaveGame();
        }
    }

    private void AddHistoryEntry(string speaker, string text, string voiceID)
    {
        GlobalDataManager.GetInstance().AddHistoryLog(speaker, text, voiceID);
        HistoryEntry entry = new HistoryEntry(speaker, text, voiceID);
        EventCenter.GetInstance().EventTrigger(VNGameEvents.AddHistoryEntry, entry);
    }

    public void ExecuteChoiceCommand(string command)
    {
        if (!string.IsNullOrEmpty(command))
            MonoManager.GetInstance().StartCoroutine(ExecuteActionsAndContinue(command));
        else
            PlayCurrentLine();
    }

    public bool IsAutoPlaying() { return isAutoPlaying; }
    public bool IsSkipping() { return isSkipping; }
    public bool IsTextDisplaying() { return isTextDisplaying; }
    public bool IsFlowRunning() { return _flowCoroutine != null; }

    public void SetConfig(string key, string value)
    {
        switch (key.ToLower())
        {
            case "voice": isVoiceEnabled = value.ToLower() == "true"; break;
            case "textspeed": isTextSpeedEnabled = value.ToLower() == "true"; break;
        }
    }

    #region 协程区
    /// <summary>
    /// 加载等待协程
    /// </summary>
    /// <returns></returns>
    private IEnumerator WaitLoadingQueueThenContinueGameplay()
    {
        LoadingProgressManager progressManager = LoadingProgressManager.GetInstance();

        const string scriptTaskID = "load_script_continue";
        const string uiTaskID = "ui_VNGameplayPanel";
        const int maxWaitFrames = 120;

        VNGameplayPanel gameplayPanel = null;

        for (int i = 0; i < maxWaitFrames; i++)
        {
            float scriptProgress = progressManager.GetTaskProgress(scriptTaskID);
            float uiProgress = progressManager.GetTaskProgress(uiTaskID);

            bool scriptDone = scriptProgress >= 1f || scriptProgress < 0f;
            bool uiDone = uiProgress >= 1f || uiProgress < 0f;

            gameplayPanel = UIManager.GetInstance().GetPanel<VNGameplayPanel>("VNGameplayPanel");
            bool panelReady =
                isGameplayPanelLoadCallbackFired &&
                gameplayPanel != null &&
                gameplayPanel.gameObject != null &&
                gameplayPanel.gameObject.activeInHierarchy &&
                gameplayPanel.IsInitialized;

            if (scriptDone && uiDone && panelReady)
            {
                VNDebug.LogVerbose($"[VNManager] 加载任务与 VNGameplayPanel 均已就绪（ContinueGame），等待了 {i + 1} 帧");
                break;
            }

            yield return null;
        }

        gameplayPanel = UIManager.GetInstance().GetPanel<VNGameplayPanel>("VNGameplayPanel");
        if (gameplayPanel == null || gameplayPanel.gameObject == null || !gameplayPanel.gameObject.activeInHierarchy)
        {
            Debug.LogError("[VNManager] 无法获取VNGameplayPanel，继续游戏失败");

            UIManager.GetInstance().HidePanel("LoadingProgressPanel");
            progressManager.ClearAllTasks();
            currentLoadingSaveData = null;

            yield break;
        }

        UIManager.GetInstance().HidePanel("LoadingProgressPanel");
        progressManager.ClearAllTasks();

        yield return DelayedContinueGameplay();
        StartOptionalContentPreloadAfterGameplayStarted();
    }

    /// <summary>
    /// 带进度更新的剧本加载协程
    /// </summary>
    private System.Collections.IEnumerator LoadScriptWithProgress(string scriptTaskID)
    {
        LoadingProgressManager progressManager = LoadingProgressManager.GetInstance();

        // 【新增】跨剧本加载时清空历史记录（新游戏或切换剧本）
        // 在设置新剧本名之前，检查是否是切换剧本
        string previousScriptName = this.currentScriptName;
        bool isNewScript = string.IsNullOrEmpty(previousScriptName) || previousScriptName != pendingScriptName;

        if (isNewScript)
        {
            // 新游戏或切换剧本，清空历史记录
            ClearHistoryLog();
            ClearRecordedScriptTags();
            VNDebug.LogVerbose($"[VNManager] 检测到新剧本或首次启动，已清空历史记录。旧剧本: {previousScriptName}, 新剧本: {pendingScriptName}");
        }

        ApplyPendingDebugScriptTags();

        this.currentScriptName = pendingScriptName;

        // 1. 加载剧本数据 (纯数据操作，同步加载，但用协程分步更新进度)
        progressManager.UpdateTaskProgress(scriptTaskID, 0.1f); // 开始加载
        yield return null; // 等待一帧，让UI更新

        progressManager.UpdateTaskProgress(scriptTaskID, 0.3f); // 解析中
        yield return null; // 等待一帧，让UI更新

        yield return CharacterResManager.GetInstance().InitAsync();
        yield return CommandManager.GetInstance().ExecuteSingleCommandAsync("loadscript", pendingScriptName);
        progressManager.UpdateTaskProgress(scriptTaskID, 0.7f); // 加载中
        yield return null; // 等待一帧，让UI更新

        ResetState();
        progressManager.UpdateTaskProgress(scriptTaskID, 0.9f); // 即将完成
        yield return null; // 等待一帧，让UI更新

        progressManager.CompleteTask(scriptTaskID); // 剧本加载完成

        // 2. 计算目标行索引 (暂不预演，只算位置)
        int targetIndex = 0;
        if (!string.IsNullOrEmpty(pendingLineID))
        {
            string cleanID = pendingLineID.Trim();
            if (LineIDIndexMap.ContainsKey(cleanID))
            {
                targetIndex = LineIDIndexMap[cleanID];
            }
            else
            {
                Debug.LogError($"[VNManager] 找不到指定的行号 ID: {cleanID}，将从头开始。");
                targetIndex = 0;
            }
        }

        // 3. 显示 UI (异步过程，UIManager会自动注册并跟踪进度)
        if (StoryLines.Count > 0)
        {
            // UIManager会自动注册任务 "ui_VNGameplayPanel"，我们只需要等待它完成
            UIManager.GetInstance().ShowPanel<VNGameplayPanel>("VNGameplayPanel", VNProjectConfig.Instance.UI_VNGamePlayPath, E_UI_Layer.Middle, (panel) =>
            {
                isGameplayPanelLoadCallbackFired = true;
                VNDebug.LogVerbose("[VNManager] VNGameplayPanel 的 ShowPanel 回调已触发");
            });
        }
        else
        {
            Debug.LogError("[VNManager] 剧本加载失败，无法启动游戏。");

            // 清理并隐藏加载面板
            progressManager.OnAllTasksCompleted -= OnGameLoadingCompleted;
            progressManager.ClearAllTasks();
            UIManager.GetInstance().HidePanel("LoadingProgressPanel");

            // 调用失败回调
            if (onGameStartedCallback != null)
            {
                onGameStartedCallback.Invoke();
                onGameStartedCallback = null;
            }
        }
    }


    private IEnumerator DelayedStartGameplay()
    {
        // VNGameplayPanel gameplayPanel = UIManager.GetInstance().GetPanel<VNGameplayPanel>("VNGameplayPanel");
        // // VNGameplayPanel拿不到，考虑文件缺失的情况
        // if (gameplayPanel == null)
        // {
        //     Debug.LogError("[VNManager] 无法获取VNGameplayPanel，游戏启动失败");
        //
        //     if (onGameStartedCallback != null)
        //     {
        //         onGameStartedCallback.Invoke();
        //         onGameStartedCallback = null;
        //     }
        //
        //     yield break;
        // }

        VNGameplayPanel gameplayPanel = UIManager.GetInstance().GetPanel<VNGameplayPanel>("VNGameplayPanel");

        // 如果还没拿到，强制再创建一次
        if (gameplayPanel == null)
        {
            Debug.LogWarning("[VNManager] DelayedStartGameplay 时未找到 VNGameplayPanel，尝试强制补建...");

            UIManager.GetInstance().ShowPanel<VNGameplayPanel>(
                "VNGameplayPanel",
                VNProjectConfig.Instance.UI_VNGamePlayPath,
                E_UI_Layer.Middle,
                (panel) =>
                {
                    VNDebug.LogVerbose("[VNManager] VNGameplayPanel 强制补建回调成功（StartGame）");
                }
            );

            // 最多再等 30 帧
            for (int i = 0; i < 30; i++)
            {
                gameplayPanel = UIManager.GetInstance().GetPanel<VNGameplayPanel>("VNGameplayPanel");
                if (gameplayPanel != null &&
                    gameplayPanel.gameObject != null &&
                    gameplayPanel.gameObject.activeInHierarchy)
                {
                    VNDebug.LogVerbose($"[VNManager] 强制补建后成功获取 VNGameplayPanel（StartGame），等待了 {i + 1} 帧");
                    break;
                }

                yield return null;
            }
        }

        //再尝试重新拿
        gameplayPanel = UIManager.GetInstance().GetPanel<VNGameplayPanel>("VNGameplayPanel");
        if (gameplayPanel == null || gameplayPanel.gameObject == null || !gameplayPanel.gameObject.activeInHierarchy)
        {
            Debug.LogError("[VNManager] 无法获取VNGameplayPanel，游戏启动失败");

            UIManager.GetInstance().HidePanel("LoadingProgressPanel");
            LoadingProgressManager.GetInstance().ClearAllTasks();

            if (onGameStartedCallback != null)
            {
                onGameStartedCallback.Invoke();
                onGameStartedCallback = null;
            }

            yield break;
        }


        for (int i = 0; i < 30 && !gameplayPanel.IsInitialized; i++)
            yield return null;

        if (!gameplayPanel.IsInitialized)
        {
            Debug.LogError("[VNManager] VNGameplayPanel did not finish initialization before gameplay start.");
            UIManager.GetInstance().HidePanel("LoadingProgressPanel");
            LoadingProgressManager.GetInstance().ClearAllTasks();
            yield break;
        }

        // 计算目标行索引
        int targetIndex = 0;
        if (!string.IsNullOrEmpty(pendingLineID))
        {
            string cleanID = pendingLineID.Trim();
            if (LineIDIndexMap.ContainsKey(cleanID))
            {
                targetIndex = LineIDIndexMap[cleanID];
            }
            else
            {
                targetIndex = 0;
            }
        }
        // 确保游戏状态设置为 Gameplay
        GameStateManager.GetInstance().SetState(GameState.Gameplay);
        // 强力清理 UI 现场
        VNAPI.ClearAllEffects();
        EventCenter.GetInstance().EventTrigger(VNGameEvents.HideCharacter, "Left");
        EventCenter.GetInstance().EventTrigger(VNGameEvents.HideCharacter, "Mid");
        EventCenter.GetInstance().EventTrigger(VNGameEvents.HideCharacter, "Right");
        // 快进到目标行，如果遇到 choice 命令则停止
        bool encounteredChoice = false;
        if (targetIndex > 0)
        {
            VNDebug.LogVerbose($"[VNManager] UI就绪，开始预演至索引: {targetIndex}");
            encounteredChoice = FastForwardToLine(targetIndex, pendingIgnoreChoiceWhenFastForward);
        }
        // 设置当前行
        if (!encounteredChoice)
        {
            CurrentLineIndex = targetIndex;
        }
        // 同步立绘显示
        foreach (var kvp in currentCharacters)
        {
            string[] parts = kvp.Value.Split('_');
            if (parts.Length >= 2)
            {
                Dictionary<string, object> info = new Dictionary<string, object>
            {
                { "position", kvp.Key },
                { "characterID", parts[0] },
                { "emotion", parts[1] }
            };

                EventCenter.GetInstance().EventTrigger(VNGameEvents.ShowCharacter, info);

                string posCode = NormalizePositionCode(kvp.Key);
                if (currentCharactersScaleX.ContainsKey(posCode))
                {
                    float savedScaleX = currentCharactersScaleX[posCode];
                    string characterID = parts[0];
                    CharacterProfile profile = CharacterResManager.GetInstance().GetCharacterProfile(characterID);

                    if (profile != null)
                    {
                        float profileScale = profile.scale > 0 ? profile.scale : 1.0f;
                        Vector3 scale = Vector3.one * profileScale;
                        scale.x = savedScaleX * profileScale;

                        RectTransform charRect = VNAPI.GetCharRect(posCode);
                        if (charRect != null)
                        {
                            charRect.localScale = scale;
                        }
                    }
                    else
                    {
                        RectTransform charRect = VNAPI.GetCharRect(posCode);
                        if (charRect != null)
                        {
                            Vector3 scale = charRect.localScale;
                            scale.x = savedScaleX;
                            charRect.localScale = scale;
                        }
                    }
                }
            }
        }

        yield return PreloadStartupBackgroundBeforeFirstLine(gameplayPanel);

        UIManager.GetInstance().HidePanel("LoadingProgressPanel");
        LoadingProgressManager.GetInstance().ClearAllTasks();

        // 正式播放
        PlayCurrentLine();
        // 启动完成回调
        if (onGameStartedCallback != null)
        {
            onGameStartedCallback.Invoke();
            onGameStartedCallback = null;
        }

        pendingScriptName = null;
        pendingLineID = null;
        pendingIgnoreChoiceWhenFastForward = false;
    }

    private IEnumerator PreloadStartupBackgroundBeforeFirstLine(VNGameplayPanel gameplayPanel)
    {
        LoadingProgressManager progressManager = LoadingProgressManager.GetInstance();
        string startupBackground = ResolveStartupBackgroundForCurrentLine();

        if (string.IsNullOrEmpty(startupBackground) || startupBackground == "hide")
        {
            CompleteStartupBackgroundTask(progressManager);
            yield break;
        }

        if (progressManager.GetTaskProgress(StartupBackgroundTaskID) >= 0f)
        {
            progressManager.UpdateTaskName(StartupBackgroundTaskID, $"加载首张背景: {startupBackground}");
            progressManager.UpdateTaskProgress(StartupBackgroundTaskID, 0.1f);
        }

        if (gameplayPanel != null)
        {
            yield return gameplayPanel.PreloadAndApplyBackgroundAsync(startupBackground);
        }

        CompleteStartupBackgroundTask(progressManager);
    }

    private string ResolveStartupBackgroundForCurrentLine()
    {
        if (!string.IsNullOrEmpty(currentBG))
        {
            return currentBG;
        }

        if (CurrentLineIndex >= 0 && CurrentLineIndex < StoryLines.Count)
        {
            StoryLine currentLine = StoryLines[CurrentLineIndex];
            if (currentLine != null && !string.IsNullOrEmpty(currentLine.Background))
            {
                return currentLine.Background;
            }
        }

        return "";
    }

    private void CompleteStartupBackgroundTask(LoadingProgressManager progressManager)
    {
        if (progressManager != null && progressManager.GetTaskProgress(StartupBackgroundTaskID) >= 0f)
        {
            progressManager.CompleteTask(StartupBackgroundTaskID);
        }
    }

    /// <summary>
    /// 带进度更新的继续游戏剧本加载协程
    /// </summary>
    private System.Collections.IEnumerator LoadScriptForContinueWithProgress(string scriptTaskID, SaveData saveData)
    {
        LoadingProgressManager progressManager = LoadingProgressManager.GetInstance();

        // 【Bug修复】清理pending变量，避免与新游戏逻辑冲突
        pendingScriptName = null;
        pendingLineID = null;

        // 【Bug修复】确保游戏状态是Gameplay
        if (GameStateManager.GetInstance().CurrentState != GameState.Gameplay &&
            GameStateManager.GetInstance().CurrentState != GameState.AutoPlay)
        {
            GameStateManager.GetInstance().SetState(GameState.Gameplay);
        }

        InitializeManager();

        // 1. 加载剧本数据
        progressManager.UpdateTaskProgress(scriptTaskID, 0.1f);
        yield return null; // 等待一帧，让UI更新

        progressManager.UpdateTaskProgress(scriptTaskID, 0.3f);
        yield return null; // 等待一帧，让UI更新

        yield return CharacterResManager.GetInstance().InitAsync();

        ScriptParser.ScriptData scriptData = null;
        yield return ScriptParser.ParseAsync(saveData.ScriptFileName, data => scriptData = data);
        if (scriptData != null)
        {
            SetScriptData(scriptData.Lines, scriptData.IDMap, saveData.ScriptFileName);
            progressManager.UpdateTaskProgress(scriptTaskID, 0.7f);
            yield return null; // 等待一帧，让UI更新
        }
        else
        {
            Debug.LogError($"无法加载存档: {saveData.ScriptFileName}");
            progressManager.CompleteTask(scriptTaskID);
            progressManager.OnAllTasksCompleted -= OnContinueGameLoadingCompleted;
            progressManager.ClearAllTasks();
            UIManager.GetInstance().HidePanel("LoadingProgressPanel");
            yield break;
        }

        progressManager.UpdateTaskProgress(scriptTaskID, 0.9f);
        yield return null; // 等待一帧，让UI更新

        progressManager.CompleteTask(scriptTaskID);

        // 恢复游戏状态数据
        currentBG = saveData.CurrentBG;
        currentBGM = saveData.CurrentBGM;

        // 恢复特效状态（先清空，再恢复）
        VNAPI.ClearAllEffects();
        activeEffects.Clear();

        // 恢复历史记录（在恢复特效前）
        if (saveData.HistoryLog != null && saveData.HistoryLog.Count > 0)
        {
            GlobalDataManager.GetInstance().RestoreHistoryLog(saveData.HistoryLog);
            VNDebug.LogVerbose($"[VNManager] 已恢复 {saveData.HistoryLog.Count} 条历史记录");
        }
        else
        {
            // 如果存档中没有历史记录，清空当前的历史记录（防止残留）
            GlobalDataManager.GetInstance().ClearHistoryLog();
        }

        // 恢复标志
        if (saveData.Flags != null)
        {
            GlobalDataManager.GetInstance().GetGlobalData().Flags = new Dictionary<string, bool>(saveData.Flags);
        }

        if (saveData.IntFlags != null)
        {
            GlobalDataManager.GetInstance().GetGlobalData().IntFlags = new Dictionary<string, int>(saveData.IntFlags);
        }

        if (saveData.StringFlags != null)
        {
            GlobalDataManager.GetInstance().GetGlobalData().StringFlags = new Dictionary<string, string>(saveData.StringFlags);
        }
        LoadRecordedScriptTagsFromGlobalFlags();

        // 恢复立绘数据
        currentCharactersScaleX.Clear();
        if (saveData.CharacterScaleX != null)
        {
            this.currentCharactersScaleX = new Dictionary<string, float>(saveData.CharacterScaleX);
        }

        // 计算目标行索引
        int targetIndex = 0;
        if (!string.IsNullOrEmpty(saveData.LineID) && LineIDIndexMap.ContainsKey(saveData.LineID))
        {
            targetIndex = LineIDIndexMap[saveData.LineID];
        }

        // 保存到成员变量，供DelayedContinueGameplay使用
        currentLoadingSaveData = saveData;
        currentLoadingTargetIndex = targetIndex;

        // 2. 显示 UI (异步过程，UIManager会自动注册并跟踪进度)
        if (StoryLines.Count > 0)
        {
            UIManager.GetInstance().ShowPanel<VNGameplayPanel>("VNGameplayPanel", VNProjectConfig.Instance.UI_VNGamePlayPath, E_UI_Layer.Middle, (panel) =>
            {
                // UI加载完成，UIManager会自动完成任务
                // 注意：这里不立即执行游戏逻辑，等待OnContinueGameLoadingCompleted回调
                isGameplayPanelLoadCallbackFired = true;
                VNDebug.LogVerbose("[VNManager] VNGameplayPanel 的 ShowPanel 回调已触发（ContinueGame）");
            });
        }
        else
        {
            Debug.LogError("[VNManager] 剧本数据为空，无法继续游戏");

            // 清理并隐藏加载面板
            progressManager.OnAllTasksCompleted -= OnContinueGameLoadingCompleted;
            progressManager.ClearAllTasks();
            UIManager.GetInstance().HidePanel("LoadingProgressPanel");

            // 清理临时数据
            currentLoadingSaveData = null;
        }
    }

    /// <summary>
    /// 延迟继续游戏逻辑（确保UI完全初始化）
    /// </summary>
    private System.Collections.IEnumerator DelayedContinueGameplay()
    {
        // yield return null; // 等待一帧

        // 获取游戏面板
        VNGameplayPanel gameplayPanel = UIManager.GetInstance().GetPanel<VNGameplayPanel>("VNGameplayPanel");
        if (gameplayPanel == null)
        {
            Debug.LogError("[VNManager] 无法获取VNGameplayPanel，继续游戏失败");
            // currentLoadingSaveData = null;
            yield break;
        }

        // 检查是否有保存的存档数据
        if (currentLoadingSaveData == null)
        {
            Debug.LogError("[VNManager] 存档数据丢失，继续游戏失败");
            yield break;
        }

        // 【修复】确保游戏状态设置为 Gameplay（加载存档时需要）
        GameStateManager.GetInstance().SetState(GameState.Gameplay);

        // 恢复游戏状态
        RestoreGameStateFromSave(currentLoadingSaveData, currentLoadingTargetIndex);

        // 清理临时数据
        currentLoadingSaveData = null;
    }



    // private IEnumerator ExecuteActionsAndContinue(string actionString)
    // {
    //     int preIndex = CurrentLineIndex;
    //     yield return CommandManager.GetInstance().ExecuteCommandsAsync(actionString);
    //     _flowCoroutine = null;
    //
    //     // 【修复】检查游戏状态，如果是 Choice 状态，不应该继续前进或触发自动播放
    //     GameStateManager stateManager = GameStateManager.GetInstance();
    //     if (stateManager != null && stateManager.CurrentState == GameState.Choice)
    //     {
    //         // 在 Choice 状态下，等待玩家选择，不继续前进
    //         VNDebug.LogVerbose("[VNManager] 命令执行完成，当前处于 Choice 状态，停止继续前进");
    //         yield return null;
    //     }
    //
    //     if (CurrentLineIndex != preIndex) PlayCurrentLine();
    //     else CheckAndTriggerAutoPlay();
    // }
    private IEnumerator ExecuteActionsAndContinue(string actionString)
    {
        int preIndex = CurrentLineIndex;
        VNDebug.LogVerbose($"[TypingTrace][ExecuteActionsAndContinue] start currentLineIndex={CurrentLineIndex}, actionString={actionString}");

        yield return CommandManager.GetInstance().ExecuteCommandsAsync(actionString);

        _flowCoroutine = null;

        bool shouldAdvanceAfterCommands = ConsumeAdvanceAfterCommandsRequest();
        VNDebug.LogVerbose($"[TypingTrace][ExecuteActionsAndContinue] finished currentLineIndex={CurrentLineIndex}, preIndex={preIndex}, shouldAdvanceAfterCommands={shouldAdvanceAfterCommands}");

        GameStateManager stateManager = GameStateManager.GetInstance();
        if (stateManager != null && stateManager.CurrentState == GameState.Choice)
        {
            VNDebug.LogVerbose("[VNManager] 命令执行完成，当前处于 Choice 状态，停止继续前进");
            yield break;
        }

        // 如果命令过程中已经改了行号（例如 jump），优先播放新位置
        if (CurrentLineIndex != preIndex)
        {
            PlayCurrentLine();
            yield break;
        }

        // 如果某个命令登记了“命令全部执行完后自动前进”
        if (shouldAdvanceAfterCommands)
        {
            AdvanceToNextLine(false);
            yield break;
        }

        RefreshContinueIconState();
        CheckAndTriggerAutoPlay();
    }

    private IEnumerator AutoPlayCountdown(float delay)
    {
        var gameplayPanel = UIManager.GetInstance().GetPanel<VNGameplayPanel>("VNGameplayPanel");
        bool isTextTyping = false;
        bool isVoicePlaying = false;

        // 第一步：等待打字机效果和语音播放都完成（以慢的为准）
        VNDebug.LogVerbose("[VNManager] 自动播放等待中：等待打字机效果和语音播放完成...");
        while (true)
        {
            // 检查打字机效果
            if (gameplayPanel != null)
            {
                isTextTyping = gameplayPanel.IsTextTyping();
            }

            // 检查语音播放
            if (VoiceManager.GetInstance() != null)
            {
                isVoicePlaying = VoiceManager.GetInstance().IsVoicePlaying();
                VNDebug.LogVerbose("Voice: " + isVoicePlaying);
            }

            // 如果两者都完成，跳出循环
            if (!isTextTyping && !isVoicePlaying)
            {
                VNDebug.LogVerbose("[VNManager] 打字机效果和语音播放已完成，等待额外延迟后进入下一行");
                break;
            }

            // 等待一帧后继续检查
            yield return null;
        }

        // 第二步：等待AutoSpeed时间后进入下一行
        yield return new WaitForSeconds(delay);

        VNDebug.LogVerbose($"[VNManager] 自动播放进入下一行 (行索引: {CurrentLineIndex + 1})");
        _autoPlayCoroutine = null;
        AdvanceToNextLine(false);
    }

    private IEnumerator WaitLoadingQueueThenStartGameplay()
    {
        LoadingProgressManager progressManager = LoadingProgressManager.GetInstance();

        const string scriptTaskID = "load_script";
        const string uiTaskID = "ui_VNGameplayPanel";
        const int maxWaitFrames = 120;

        VNGameplayPanel gameplayPanel = null;

        for (int i = 0; i < maxWaitFrames; i++)
        {
            float scriptProgress = progressManager.GetTaskProgress(scriptTaskID);
            float uiProgress = progressManager.GetTaskProgress(uiTaskID);

            bool scriptDone = scriptProgress >= 1f || scriptProgress < 0f;
            bool uiDone = uiProgress >= 1f || uiProgress < 0f;

            gameplayPanel = UIManager.GetInstance().GetPanel<VNGameplayPanel>("VNGameplayPanel");
            bool panelReady =
                isGameplayPanelLoadCallbackFired &&
                gameplayPanel != null &&
                gameplayPanel.gameObject != null &&
                gameplayPanel.gameObject.activeInHierarchy &&
                gameplayPanel.IsInitialized;

            if (scriptDone && uiDone && panelReady)
            {
                VNDebug.LogVerbose($"[VNManager] 加载任务与 VNGameplayPanel 均已就绪，等待了 {i + 1} 帧");
                break;
            }

            yield return null;
        }

        gameplayPanel = UIManager.GetInstance().GetPanel<VNGameplayPanel>("VNGameplayPanel");
        if (gameplayPanel == null || gameplayPanel.gameObject == null || !gameplayPanel.gameObject.activeInHierarchy)
        {
            Debug.LogError("[VNManager] 加载任务已结束，但仍无法获取 VNGameplayPanel，游戏启动失败");

            UIManager.GetInstance().HidePanel("LoadingProgressPanel");
            progressManager.ClearAllTasks();

            if (onGameStartedCallback != null)
            {
                onGameStartedCallback.Invoke();
                onGameStartedCallback = null;
            }

            yield break;
        }

        yield return DelayedStartGameplay();
        StartOptionalContentPreloadAfterGameplayStarted();
    }

    private void StartOptionalContentPreloadAfterGameplayStarted()
    {
        RemoteContentPreloadManager.GetInstance().StartOptionalContentPreloadAfterGameplayStarted();
    }


    #endregion



    #region API供外部调用
    public void RequestAdvanceAfterCommands()
    {
        _advanceAfterCommandsRequested = true;
    }

    public void ClearAdvanceAfterCommandsRequest()
    {
        _advanceAfterCommandsRequested = false;
    }

    public bool ConsumeAdvanceAfterCommandsRequest()
    {
        bool result = _advanceAfterCommandsRequested;
        _advanceAfterCommandsRequested = false;
        return result;
    }

    public void RequestStopCommandsForEndingChoice()
    {
        _stopCommandsForEndingChoiceRequested = true;
    }

    public void ClearStopCommandsForEndingChoiceRequest()
    {
        _stopCommandsForEndingChoiceRequested = false;
    }

    public bool ShouldStopCommandsForEndingChoice()
    {
        return _stopCommandsForEndingChoiceRequested;
    }

    public float GetCharacterScaleX(string posCode)
    {
        string normalized = NormalizePositionCode(posCode);
        if (currentCharactersScaleX.ContainsKey(normalized))
            return currentCharactersScaleX[normalized];
        return 1f; // 默认朝右
    }

    // 【新增】设置角色 ScaleX 的 API (供 Command 调用)
    public void SetCharacterScaleX(string posCode, float scaleX)
    {
        string normalized = NormalizePositionCode(posCode);
        currentCharactersScaleX[normalized] = scaleX;
    }

    // 获取角色数据 (方便 CharFlip.Simulate 内部获取当前 CharID_Emotion)
    public string GetCharacterData(string posCode)
    {
        string normalized = NormalizePositionCode(posCode);
        // 需要同时检查 "L"/"M"/"R" 和 "Left"/"Mid"/"Right" 两种格式
        if (currentCharacters.ContainsKey(normalized))
            return currentCharacters[normalized];
        // 如果 normalized 是 "L"，也检查 "Left"
        if (normalized == "L" && currentCharacters.ContainsKey("Left"))
            return currentCharacters["Left"];
        if (normalized == "M" && currentCharacters.ContainsKey("Mid"))
            return currentCharacters["Mid"];
        if (normalized == "R" && currentCharacters.ContainsKey("Right"))
            return currentCharacters["Right"];
        return "";
    }

    /// <summary>
    /// 启动场景回放
    /// </summary>
    /// <param name="scriptName">剧本文件名</param>
    /// <param name="startID">开始行ID</param>
    /// <param name="endID">结束行ID</param>
    /// <param name="wasMainMenuVisible">回放前主菜单是否可见（可选，默认false）</param>
    public void StartSceneReplay(string scriptName, string startID, string endID, bool wasMainMenuVisible = false)
    {
        isReplayMode = true;
        replayEndLineID = endID;

        // 【修复】记录主菜单是否可见（用于回放结束后恢复）
        wasMainMenuVisibleBeforeReplay = wasMainMenuVisible;
        VNDebug.LogVerbose($"[VNManager] 记录主菜单状态: {wasMainMenuVisibleBeforeReplay}");

        // 复用 StartGameOnScene 逻辑，但带上回放标记
        StartGameOnScene(scriptName, startID, () =>
        {
            VNDebug.LogVerbose($"[VNManager] 场景回放已启动: {scriptName}, 从 {startID} 到 {endID}");
        });
    }

    /// <summary>
    /// 结束场景回放
    /// </summary>
    private void EndReplay()
    {
        VNDebug.LogVerbose("[VNManager] 场景回放结束，开始清理状态");


        ResetReplayState();

        isReplayMode = false;
        replayEndLineID = "";

        PrimeTween.Tween.StopAll();
        VNAPI.ClearAllEffects();
        PoolManager.GetInstance().Clear();

        // 关闭游戏面板
        UIManager.GetInstance().HidePanel("VNGameplayPanel");

        // 【修复2】重新显示画廊面板（如果之前被隐藏了）
        GalleryPanel galleryPanel = UIManager.GetInstance().GetPanel<GalleryPanel>("GalleryPanel");
        if (galleryPanel != null)
        {
            // 面板已存在，直接显示
            galleryPanel.gameObject.SetActive(true);
            galleryPanel.SwitchPage(GalleryPanel.GalleryPage.Scene);
        }
        else
        {
            // 面板不存在，重新加载
            string galleryPath = VNProjectConfig.Instance != null
                ? VNProjectConfig.Instance.UI_GalleryPath
                : "VNPrefabs/UI/Gallery";
            if (string.IsNullOrEmpty(galleryPath)) galleryPath = "VNPrefabs/UI/Gallery";

            UIManager.GetInstance().ShowPanel<GalleryPanel>("GalleryPanel", galleryPath, E_UI_Layer.Middle, (panel) =>
            {
                // 切换到场景回放页面
                if (panel != null)
                {
                    panel.SwitchPage(GalleryPanel.GalleryPage.Scene);
                }
            });
        }

        // 【修复3】恢复主菜单面板（如果回放前是可见的）
        VNDebug.LogVerbose($"[VNManager] 检查是否需要恢复主菜单: wasMainMenuVisibleBeforeReplay = {wasMainMenuVisibleBeforeReplay}");
        if (wasMainMenuVisibleBeforeReplay)
        {
            MainMenuPanel mainMenuPanel = UIManager.GetInstance().GetPanel<MainMenuPanel>("MainMenuPanel");
            if (mainMenuPanel != null)
            {
                mainMenuPanel.gameObject.SetActive(true);
                VNDebug.LogVerbose("[VNManager] 主菜单面板已恢复显示");
            }
            else
            {
                Debug.LogWarning("[VNManager] 主菜单面板不存在，无法恢复");
            }
            wasMainMenuVisibleBeforeReplay = false; // 重置标志
        }
        else
        {
            VNDebug.LogVerbose("[VNManager] 回放前主菜单不可见，不恢复");
        }
    }

    /// <summary>
    /// 重置场景回放状态（清理所有回放产生的状态和效果）
    /// </summary>
    private void ResetReplayState()
    {
        VNDebug.LogVerbose("[VNManager] 开始重置场景回放状态");

        // 1. 停止BGM
        MusicManager.GetInstance().StopBGM();
        currentBGM = "";

        // 2. 停止所有音效
        MusicManager.GetInstance().ClearAllSFX();

        // 3. 停止语音
        if (VoiceManager.GetInstance() != null)
        {
            VoiceManager.GetInstance().StopVoice();
        }

        // 4. 清理所有特效
        VNAPI.ClearAllEffects();
        activeEffects.Clear();

        // 5. 隐藏所有角色
        EventCenter.GetInstance().EventTrigger(VNGameEvents.HideCharacter, "Left");
        EventCenter.GetInstance().EventTrigger(VNGameEvents.HideCharacter, "Mid");
        EventCenter.GetInstance().EventTrigger(VNGameEvents.HideCharacter, "Right");
        currentCharacters.Clear();
        currentCharactersScaleX.Clear();

        // 6. 重置背景（可选：设置为黑色或隐藏）
        // EventCenter.GetInstance().EventTrigger(VNGameEvents.ChangeBackground, "black");
        currentBG = "";

        // 7. 重置游戏状态变量
        isAutoPlaying = false;
        isSkipping = false;
        isTextDisplaying = false;
        isVoiceEnabled = true;
        lastLine = null;

        // 8. 停止所有协程
        if (_flowCoroutine != null)
        {
            MonoManager.GetInstance().StopCoroutine(_flowCoroutine);
            _flowCoroutine = null;
        }
        if (_autoPlayCoroutine != null)
        {
            MonoManager.GetInstance().StopCoroutine(_autoPlayCoroutine);
            _autoPlayCoroutine = null;
        }

        // 9. 中断所有命令
        CommandManager.GetInstance().InterruptAll();

        // 10. 恢复TimeScale（如果被快进修改了）
        Time.timeScale = 1f;

        VNDebug.LogVerbose("[VNManager] 场景回放状态重置完成");
    }
    #endregion
}
