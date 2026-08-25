using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.Linq;
using System.Reflection;
using System;
using VNovelizer.Core.Diagnostics;

namespace VNovelizer.Core.Commands
{
    /// <summary>
    /// 命令基类，所有具体命令都继承自这个类
    /// </summary>
    public abstract class VNCommand
    {
        /// <summary>
        /// 命令名称
        /// </summary>
        public abstract string CommandName { get; }

        /// <summary>
        /// 执行命令
        /// </summary>
        public abstract bool Execute(string args);

        /// <summary>
        /// 异步执行命令
        /// </summary>
        public virtual IEnumerator ExecuteAsync(string args)
        {
            Execute(args);
            yield break;
        }

        /// <summary>
        /// 为一次异步执行创建独立的命令实例。
        /// 有特殊构造需求的自定义命令可以重写此方法。
        /// </summary>
        public virtual VNCommand CreateExecutionInstance()
        {
            try
            {
                return (VNCommand)Activator.CreateInstance(GetType());
            }
            catch (Exception e)
            {
                Debug.LogError($"[{CommandName}] 无法创建独立执行实例: {e.Message}");
                return null;
            }
        }

        /// <summary>
        /// [新增] 中断命令接口
        /// 当玩家点击屏幕需要跳过当前演出时调用
        /// </summary>
        public virtual void Interrupt() { }

        public virtual void Simulate(string args) { }
    }

    /// <summary>
    /// 命令管理器，负责注册、执行和中断命令
    /// </summary>
    public class CommandManager : BaseManager<CommandManager>
    {
        // 命令映射表
        private Dictionary<string, VNCommand> _commandMap = new Dictionary<string, VNCommand>();

        // [新增] 正在运行的命令列表
        private List<VNCommand> _runningCommands = new List<VNCommand>();

        // 正在运行的并行组
        private readonly List<ParallelExecutionGroup> _runningParallelGroups = new List<ParallelExecutionGroup>();

        // 每次中断都会推进代次，串行调度器据此停止后续命令
        private int _interruptGeneration;

        // [新增] 是否有命令正在运行
        public bool IsRunning => _runningCommands.Count > 0 || _runningParallelGroups.Count > 0;

        private sealed class ParallelExecutionGroup
        {
            public readonly List<Coroutine> Coroutines = new List<Coroutine>();
            public int Remaining;
            public bool Interrupted;
        }

        /// <summary>
        /// 初始化
        /// </summary>
        public void Init()
        {
            RegisterDefaultCommands();
            RegisterCustomCommandsViaReflection();
        }

        private void RegisterDefaultCommands()
        {
            RegisterCommand(new LoadScriptCommand());
            RegisterCommand(new UnlockCGCommand());
            RegisterCommand(new UnlockMusicCommand());
            RegisterCommand(new UnlockSceneCommand());
            RegisterCommand(new UnlockEndingCommand());
            RegisterCommand(new ConfigCommand());
            RegisterCommand(new ShakeCommand());
            RegisterCommand(new WaitCommand());
            RegisterCommand(new JumpCommand());
            RegisterCommand(new SetBoolFlagCommand());
            RegisterCommand(new SetIntFlagCommand());
            RegisterCommand(new SetStringFlagCommand());
            RegisterCommand(new CharJumpCommand());
            RegisterCommand(new ChoiceCommand());
            RegisterCommand(new BgFadeCommand());
            RegisterCommand(new PanoramaCommand());
            RegisterCommand(new PanoramaChoiceCommand());
            RegisterCommand(new SetTextSpeedCommand());
            RegisterCommand(new SetAutoSpeedCommand());
            RegisterCommand(new TColorCommand());
            RegisterCommand(new TSizeCommand());
            RegisterCommand(new CharFadeInCommand());
            RegisterCommand(new CharFadeOutCommand());
            RegisterCommand(new CharFlipCommand());
            RegisterCommand(new CharMoveCommand());
            RegisterCommand(new CharScaleCommand());
            RegisterCommand(new SetCharTransCommand());
            RegisterCommand(new PlaySFXCommand());
            RegisterCommand(new PlayVideoCommand());
            RegisterCommand(new PlayParticleCommand());
            RegisterCommand(new StopParticleCommand());
            RegisterCommand(new ShowPromptCommand());
            RegisterCommand(new PlayAnimCommand());
            RegisterCommand(new StopAnimCommand());

        }

        private void RegisterCustomCommandsViaReflection()
        {
            var assemblies = AppDomain.CurrentDomain.GetAssemblies();

            foreach (var assembly in assemblies)
            {

                string name = assembly.GetName().Name;
                if (name.StartsWith("Unity") || name.StartsWith("System") || name.StartsWith("mscorlib"))
                    continue;

                var commandTypes = assembly.GetTypes()
                    .Where(type => type.IsSubclassOf(typeof(VNCommand)) && !type.IsAbstract);

                foreach (var type in commandTypes)
                {
                    try
                    {
                        VNCommand cmdInstance = (VNCommand)Activator.CreateInstance(type);

                        if (cmdInstance != null && !string.IsNullOrEmpty(cmdInstance.CommandName))
                        {
                            string cmdNameKey = cmdInstance.CommandName.ToLower();

                            if (!_commandMap.ContainsKey(cmdNameKey))
                            {
                                RegisterCommand(cmdInstance);
                                Debug.Log($"[CommandManager] 自动注册命令成功 {type.Name} => {cmdNameKey}");
                            }
                        }
                    }
                    catch (Exception e)
                    {
                        Debug.LogError($"[CommandManager] 自动注册命令失败 {type.Name}: {e.Message}");
                    }
                }
            }
        }

        public void RegisterCommand(VNCommand command)
        {
            if (command != null && !string.IsNullOrEmpty(command.CommandName))
            {
                string commandName = command.CommandName.ToLower();
                _commandMap[commandName] = command;
            }
        }

        public bool ExecuteCommand(string commandString)
        {
            if (!TryParseCommand(commandString, out string cmd, out string args))
            {
                Debug.LogWarning($"指令格式错误: {commandString}");
                return false;
            }

            if (string.Equals(cmd, "parallel", StringComparison.OrdinalIgnoreCase))
            {
                Debug.LogWarning("parallel 指令只能通过 ExecuteCommandsAsync 异步执行。");
                return false;
            }

            return ExecuteSingleCommand(cmd, args);
        }

        public void SimulateCommands(string commandString)
        {
            if (string.IsNullOrEmpty(commandString)) return;

            if (!TrySplitTopLevel(commandString, '&', out List<string> actions))
            {
                Debug.LogWarning($"指令括号不匹配，无法模拟: {commandString}");
                return;
            }

            foreach (string action in actions)
            {
                SimulateAction(action);
            }
        }

        private void SimulateAction(string action)
        {
            if (string.IsNullOrWhiteSpace(action)) return;

            if (!TryParseCommand(action, out string cmd, out string args))
            {
                Debug.LogWarning($"指令格式错误，跳过模拟: {action}");
                return;
            }

            string commandName = cmd.ToLowerInvariant();
            if (commandName == "parallel")
            {
                if (!TrySplitTopLevel(args, ';', out List<string> parallelActions))
                {
                    Debug.LogWarning($"parallel 指令括号不匹配，跳过模拟: {action}");
                    return;
                }

                foreach (string parallelAction in parallelActions)
                {
                    SimulateAction(parallelAction);
                }
                return;
            }

            if (_commandMap.TryGetValue(commandName, out VNCommand command))
            {
                command.Simulate(args);
            }
            else
            {
                Debug.LogWarning($"未找到命令: {cmd}");
            }
        }

        private bool ExecuteSingleCommand(string cmd, string args)
        {
            if (string.IsNullOrEmpty(cmd)) return false;

            string commandName = cmd.ToLowerInvariant();
            if (_commandMap.TryGetValue(commandName, out VNCommand command))
            {
                // 同步执行不计入 _runningCommands，因为它是瞬间完成的
                return command.Execute(args);
            }

            Debug.LogWarning($"未找到命令: {cmd}");
            return false;
        }

        private bool TryCreateExecutionInstance(string commandName, out VNCommand command)
        {
            command = null;
            if (!_commandMap.TryGetValue(commandName, out VNCommand prototype))
            {
                return false;
            }

            command = prototype.CreateExecutionInstance();
            return command != null;
        }

        /// <summary>
        /// 异步执行单个命令。每次调用使用独立实例，以支持同类型命令并行。
        /// </summary>
        public IEnumerator ExecuteSingleCommandAsync(string cmd, string args)
        {
            if (string.IsNullOrEmpty(cmd)) yield break;

            string commandName = cmd.Trim().ToLowerInvariant();
            if (commandName == "parallel")
            {
                yield return ExecuteParallelAsync(args);
                yield break;
            }

            if (!TryCreateExecutionInstance(commandName, out VNCommand command))
            {
                Debug.LogWarning($"未找到命令或无法创建执行实例: {cmd}");
                yield break;
            }

            VNDebug.LogVerbose($"[TypingTrace][CommandManager.ExecuteSingleCommandAsync] start cmd={commandName}, args={args}");
            _runningCommands.Add(command);
            try
            {
                yield return command.ExecuteAsync(args);
                VNDebug.LogVerbose($"[TypingTrace][CommandManager.ExecuteSingleCommandAsync] finish cmd={commandName}, args={args}");
            }
            finally
            {
                _runningCommands.Remove(command);
            }
        }

        public void ExecuteCommands(string commandString)
        {
            if (string.IsNullOrEmpty(commandString)) return;

            if (!TrySplitTopLevel(commandString, '&', out List<string> actions))
            {
                Debug.LogWarning($"指令括号不匹配，无法执行: {commandString}");
                return;
            }

            foreach (string action in actions)
            {
                string trimmedAction = action.Trim();
                if (!string.IsNullOrEmpty(trimmedAction)) ExecuteCommand(trimmedAction);
            }
        }

        /// <summary>
        /// 从代码异步执行一条普通或 parallel 组合指令。
        /// </summary>
        public IEnumerator ExecuteCommandAsync(string commandString)
        {
            if (string.IsNullOrWhiteSpace(commandString)) yield break;
            yield return ExecuteActionAsync(commandString.Trim());
        }

        public IEnumerator ExecuteCommandsAsync(string commandString)
        {
            if (string.IsNullOrEmpty(commandString)) yield break;

            VNDebug.LogVerbose($"[TypingTrace][CommandManager.ExecuteCommandsAsync] commandString={commandString}");

            if (!TrySplitTopLevel(commandString, '&', out List<string> actions))
            {
                Debug.LogWarning($"指令括号不匹配，无法执行: {commandString}");
                yield break;
            }

            int executionGeneration = _interruptGeneration;
            foreach (string action in actions)
            {
                string trimmedAction = action.Trim();
                if (!string.IsNullOrEmpty(trimmedAction))
                {
                    yield return ExecuteActionAsync(trimmedAction);

                    if (executionGeneration != _interruptGeneration)
                    {
                        yield break;
                    }

                    if (VNManager.GetInstance().ShouldStopCommandsForEndingChoice())
                    {
                        yield break;
                    }
                }
            }
        }

        private IEnumerator ExecuteActionAsync(string action)
        {
            if (!TryParseCommand(action, out string cmd, out string args))
            {
                Debug.LogWarning($"指令格式错误，已跳过: {action}");
                yield break;
            }

            yield return ExecuteSingleCommandAsync(cmd, args);
        }

        private IEnumerator ExecuteParallelAsync(string args)
        {
            if (!TrySplitTopLevel(args, ';', out List<string> splitActions))
            {
                Debug.LogWarning($"parallel 指令括号不匹配，已跳过: {args}");
                yield break;
            }

            List<string> actions = splitActions
                .Select(action => action.Trim())
                .Where(action => !string.IsNullOrEmpty(action))
                .ToList();

            if (actions.Count == 0)
            {
                Debug.LogWarning("parallel 指令中没有可执行的成员。");
                yield break;
            }

            var group = new ParallelExecutionGroup { Remaining = actions.Count };
            _runningParallelGroups.Add(group);

            foreach (string action in actions)
            {
                Coroutine coroutine = MonoManager.GetInstance().StartCoroutine(
                    RunParallelAction(action, group));
                group.Coroutines.Add(coroutine);
            }

            while (group.Remaining > 0 && !group.Interrupted)
            {
                yield return null;
            }

            _runningParallelGroups.Remove(group);
        }

        private IEnumerator RunParallelAction(string action, ParallelExecutionGroup group)
        {
            try
            {
                yield return ExecuteActionAsync(action);
            }
            finally
            {
                group.Remaining = Math.Max(0, group.Remaining - 1);
            }
        }

        private static bool TryParseCommand(string commandString, out string cmd, out string args)
        {
            cmd = "";
            args = "";
            if (string.IsNullOrWhiteSpace(commandString)) return false;

            string trimmed = commandString.Trim();
            int startIndex = trimmed.IndexOf('(');
            if (startIndex <= 0 || trimmed[trimmed.Length - 1] != ')') return false;

            int depth = 0;
            for (int i = startIndex; i < trimmed.Length; i++)
            {
                char current = trimmed[i];
                if (current == '(')
                {
                    depth++;
                }
                else if (current == ')')
                {
                    depth--;
                    if (depth < 0 || (depth == 0 && i != trimmed.Length - 1))
                    {
                        return false;
                    }
                }
            }

            if (depth != 0) return false;

            cmd = trimmed.Substring(0, startIndex).Trim();
            if (string.IsNullOrEmpty(cmd)) return false;

            args = trimmed.Substring(startIndex + 1, trimmed.Length - startIndex - 2);
            return true;
        }

        private static bool TrySplitTopLevel(string value, char separator, out List<string> parts)
        {
            parts = new List<string>();
            if (value == null) return false;

            int depth = 0;
            int partStart = 0;
            for (int i = 0; i < value.Length; i++)
            {
                char current = value[i];
                if (current == '(')
                {
                    depth++;
                }
                else if (current == ')')
                {
                    depth--;
                    if (depth < 0) return false;
                }
                else if (current == separator && depth == 0)
                {
                    parts.Add(value.Substring(partStart, i - partStart));
                    partStart = i + 1;
                }
            }

            if (depth != 0) return false;
            parts.Add(value.Substring(partStart));
            return true;
        }

        // [新增] 中断所有命令
        public void InterruptAll()
        {
            _interruptGeneration++;

            if (_runningCommands.Count == 0 && _runningParallelGroups.Count == 0) return;

            VNDebug.LogVerbose($"[TypingTrace][CommandManager.InterruptAll] commands={_runningCommands.Count}, parallelGroups={_runningParallelGroups.Count}, names={string.Join(",", _runningCommands.Select(c => c.CommandName))}");

            VNCommand[] commands = _runningCommands.ToArray();
            ParallelExecutionGroup[] groups = _runningParallelGroups.ToArray();

            for (int i = commands.Length - 1; i >= 0; i--)
            {
                commands[i].Interrupt();
            }

            foreach (ParallelExecutionGroup group in groups)
            {
                group.Interrupted = true;
                foreach (Coroutine coroutine in group.Coroutines)
                {
                    MonoManager.GetInstance().StopCoroutine(coroutine);
                }
                group.Coroutines.Clear();
                group.Remaining = 0;
            }

            _runningCommands.Clear();
            _runningParallelGroups.Clear();
        }
    }
}
