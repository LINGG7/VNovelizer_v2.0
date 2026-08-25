using System.Collections;
using VNovelizer.Core.API;
using VNovelizer.Core.Commands;

namespace VNovelizer.Project.Commands
{
    public class MemoryOnCommand : VNCommand
    {
        private const string OldFilmOverlayName = "MemoryOldFilmOverlay";
        private const string LegacyFilterName = "MemoryFilterOverlay";
        private const string LegacyTVSnowOverlayName = "MemoryTVSnowOverlay";
        private const string LegacyDustEffectName = "MemoryDustEffect";

        public override string CommandName => "memoryon";

        public override bool Execute(string args)
        {
            CommandManager.GetInstance().ExecuteCommand($"stopfilter({TapeOnCommand.OverlayName})");
            CommandManager.GetInstance().ExecuteCommand($"stopfilter({LegacyFilterName})");
            CommandManager.GetInstance().ExecuteCommand($"stopfilter({LegacyTVSnowOverlayName})");
            CommandManager.GetInstance().ExecuteCommand($"stopparticle({LegacyDustEffectName})");
            return CommandManager.GetInstance().ExecuteCommand($"playfilter({OldFilmOverlayName})");
        }

        public override IEnumerator ExecuteAsync(string args)
        {
            Execute(args);
            yield break;
        }

        public override void Simulate(string args)
        {
            VNAPI.UnregisterEffect(PlayFilterCommand.GetStateKey(TapeOnCommand.OverlayName));
            VNAPI.UnregisterEffect(PlayFilterCommand.GetStateKey(LegacyFilterName));
            VNAPI.UnregisterEffect(PlayFilterCommand.GetStateKey(LegacyTVSnowOverlayName));
            VNAPI.UnregisterEffect(LegacyDustEffectName);
            VNAPI.RegisterEffect(PlayFilterCommand.GetStateKey(OldFilmOverlayName));
        }
    }
}
