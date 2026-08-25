using System.Collections;
using UnityEngine;

namespace VNovelizer.Core.Commands
{
    public class LoadScriptCommand : VNCommand
    {
        public override string CommandName { get { return "loadscript"; } }

        public override bool Execute(string args)
        {
            if (!TryParseArgs(args, out string scriptName, out string startID))
            {
                return false;
            }

            var scriptData = ScriptParser.Parse(scriptName);
            return ApplyLoadedScript(scriptName, startID, scriptData);
        }

        public override IEnumerator ExecuteAsync(string args)
        {
            if (!TryParseArgs(args, out string scriptName, out string startID))
            {
                yield break;
            }

            ScriptParser.ScriptData scriptData = null;
            yield return ScriptParser.ParseAsync(scriptName, data => scriptData = data);
            ApplyLoadedScript(scriptName, startID, scriptData);
        }

        private bool TryParseArgs(string args, out string scriptName, out string startID)
        {
            scriptName = null;
            startID = null;

            if (string.IsNullOrEmpty(args))
            {
                Debug.LogError("LoadScript command args cannot be empty");
                return false;
            }

            string[] parts = args.Split(',');
            scriptName = parts[0].Trim();
            startID = parts.Length >= 2 ? parts[1].Trim() : null;
            return !string.IsNullOrEmpty(scriptName);
        }

        private bool ApplyLoadedScript(string scriptName, string startID, ScriptParser.ScriptData scriptData)
        {
            if (scriptData == null || scriptData.Lines.Count == 0)
            {
                Debug.LogError($"[LoadScript] Failed to load script: {scriptName}");
                return false;
            }

            VNManager manager = VNManager.GetInstance();
            manager.SetScriptData(scriptData.Lines, scriptData.IDMap, scriptName);
            Debug.Log($"[LoadScript] Loaded script: {scriptName}");

            if (!string.IsNullOrEmpty(startID))
            {
                if (manager.LineIDIndexMap.TryGetValue(startID, out int index))
                {
                    manager.CurrentLineIndex = index;
                    Debug.Log($"[LoadScript] Positioned script: {scriptName}, startID={startID}, index={index} (no preview)");
                }
                else
                {
                    Debug.LogWarning($"[LoadScript] StartID not found: {startID}. Starting from the beginning.");
                    manager.CurrentLineIndex = 0;
                }
            }
            else
            {
                manager.CurrentLineIndex = 0;
                Debug.Log($"[LoadScript] Positioned script: {scriptName}, index=0 (no preview)");
            }

            return true;
        }
    }
}
