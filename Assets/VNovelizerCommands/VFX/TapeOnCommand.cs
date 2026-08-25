using System.Collections;
using VNovelizer.Core.API;
using VNovelizer.Core.Commands;

namespace VNovelizer.Project.Commands
{
    public class TapeOnCommand : VNCommand
    {
        internal const string OverlayName = "TapePlaybackOverlay";

        private const string OldFilmOverlayName = "MemoryOldFilmOverlay";
        private const string LegacyFilterName = "MemoryFilterOverlay";
        private const string LegacyTVSnowOverlayName = "MemoryTVSnowOverlay";
        private const string LegacyDustEffectName = "MemoryDustEffect";

        public override string CommandName => "tapeon";

        public override bool Execute(string args)
        {
            CommandManager.GetInstance().ExecuteCommand("memoryoff()");
            return CommandManager.GetInstance().ExecuteCommand($"playfilter({OverlayName})");
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
            VNAPI.RegisterEffect(PlayFilterCommand.GetStateKey(OverlayName));
        }
    }
}
