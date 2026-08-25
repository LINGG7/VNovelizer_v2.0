using System.Collections;
using VNovelizer.Core.API;
using VNovelizer.Core.Commands;

namespace VNovelizer.Project.Commands
{
    public class TapeOffCommand : VNCommand
    {
        public override string CommandName => "tapeoff";

        public override bool Execute(string args)
        {
            return CommandManager.GetInstance().ExecuteCommand($"stopfilter({TapeOnCommand.OverlayName})");
        }

        public override IEnumerator ExecuteAsync(string args)
        {
            Execute(args);
            yield break;
        }

        public override void Simulate(string args)
        {
            VNAPI.UnregisterEffect(PlayFilterCommand.GetStateKey(TapeOnCommand.OverlayName));
        }
    }
}
