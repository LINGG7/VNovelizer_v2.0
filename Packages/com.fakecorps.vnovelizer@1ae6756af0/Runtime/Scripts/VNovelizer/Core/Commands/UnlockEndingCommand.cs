using UnityEngine;

namespace VNovelizer.Core.Commands
{
    public class UnlockEndingCommand : VNCommand
    {
        public override string CommandName { get { return "unlockending"; } }

        public override bool Execute(string args)
        {
            string endingID = NormalizeEndingID(args);
            if (string.IsNullOrEmpty(endingID))
            {
                Debug.LogError("UnlockEnding command argument cannot be empty. Usage: unlockending(EndingID)");
                return false;
            }

            GlobalDataManager.GetInstance().UnlockEnding(endingID);
            ShowEndingChoice(endingID);
            VNManager.GetInstance().RequestStopCommandsForEndingChoice();
            return true;
        }

        private void ShowEndingChoice(string endingID)
        {
            VNEnding ending = LoadEnding(endingID);
            if (ending == null)
            {
                ending = new VNEnding(endingID);
                ending.UnlockedText = endingID;
            }

            GameStateManager.GetInstance().SetState(GameState.Choice);
            UIManager.GetInstance().HidePanel("MainMenuPanel");
            UIManager.GetInstance().HidePanel("SaveLoadPanel");
            UIManager.GetInstance().HidePanel("LoadingProgressPanel");

            string panelPath = VNProjectConfig.Instance != null && !string.IsNullOrEmpty(VNProjectConfig.Instance.UI_EndingChoicePath)
                ? VNProjectConfig.Instance.UI_EndingChoicePath
                : "VNovelizerRes/VNPrefabs/UI/EndingChoice";

            UIManager.GetInstance().ShowPanel<EndingChoicePanel>(
                "EndingChoicePanel",
                panelPath,
                E_UI_Layer.Top,
                panel => panel.ShowEnding(ending)
            );
        }

        private VNEnding LoadEnding(string endingID)
        {
            if (VNProjectConfig.Instance == null)
            {
                return null;
            }

            string path = VNProjectConfig.Instance.Ending_DataPath + "/EndingDataContainer";
            EndingDataContainer container = ResourcesManager.GetInstance().Load<EndingDataContainer>(path);
            return container != null ? container.GetEndingByID(endingID) : null;
        }

        private string NormalizeEndingID(string args)
        {
            if (string.IsNullOrEmpty(args))
            {
                return string.Empty;
            }

            string endingID = args.Trim();

            if (endingID.StartsWith("(") && endingID.EndsWith(")") && endingID.Length > 2)
            {
                endingID = endingID.Substring(1, endingID.Length - 2).Trim();
            }

            return endingID;
        }
    }
}
