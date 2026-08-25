using System.Collections;
using UnityEngine;
using VNovelizer.Core.API;
using VNovelizer.Core.Commands;

namespace VNovelizer.Project.Commands
{
    public class StopFilterCommand : VNCommand
    {
        public override string CommandName => "stopfilter";

        public override bool Execute(string args)
        {
            if (!PlayFilterCommand.TryGetFilterName(args, out string filterName)) return false;

            VNAPI.UnregisterEffect(PlayFilterCommand.GetStateKey(filterName));

            Transform parent = VNAPI.GetEffectLayer();
            if (parent == null) return true;

            Transform target = parent.Find(PlayFilterCommand.GetObjectName(filterName));
            if (target != null)
            {
                Object.Destroy(target.gameObject);
            }

            return true;
        }

        public override IEnumerator ExecuteAsync(string args)
        {
            Execute(args);
            yield break;
        }

        public override void Simulate(string args)
        {
            if (PlayFilterCommand.TryGetFilterName(args, out string filterName))
            {
                VNAPI.UnregisterEffect(PlayFilterCommand.GetStateKey(filterName));
            }
        }
    }
}
