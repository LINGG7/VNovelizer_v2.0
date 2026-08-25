using System.Collections;
using VNovelizer.Core.API;
using VNovelizer.Core.Commands;

namespace VNovelizer.Project.Commands
{
    public class MemoryOffCommand : VNCommand
    {
        private const string OldFilmOverlayName = "MemoryOldFilmOverlay";
        private const string LegacyFilterName = "MemoryFilterOverlay";
        private const string LegacyTVSnowOverlayName = "MemoryTVSnowOverlay";
        private const string LegacyDustEffectName = "MemoryDustEffect";

        public override string CommandName => "memoryoff";

        public override bool Execute(string args)
        {
            bool oldFilmStopped = CommandManager.GetInstance().ExecuteCommand($"stopfilter({OldFilmOverlayName})");
            CommandManager.GetInstance().ExecuteCommand($"stopfilter({LegacyFilterName})");
            CommandManager.GetInstance().ExecuteCommand($"stopfilter({LegacyTVSnowOverlayName})");
            CommandManager.GetInstance().ExecuteCommand($"stopparticle({LegacyDustEffectName})");
            return oldFilmStopped;
        }

        public override IEnumerator ExecuteAsync(string args)
        {
            Execute(args);
            yield break;
        }

        public override void Simulate(string args)
        {
            VNAPI.UnregisterEffect(PlayFilterCommand.GetStateKey(OldFilmOverlayName));
            VNAPI.UnregisterEffect(PlayFilterCommand.GetStateKey(LegacyFilterName));
            VNAPI.UnregisterEffect(PlayFilterCommand.GetStateKey(LegacyTVSnowOverlayName));
            VNAPI.UnregisterEffect(LegacyDustEffectName);
        }
    }
}
