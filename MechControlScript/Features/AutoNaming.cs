using Sandbox.Game.EntityComponents;
using Sandbox.ModAPI.Ingame;
using Sandbox.ModAPI.Interfaces;
using SpaceEngineers.Game.ModAPI.Ingame;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text;
using VRage;
using VRage.Collections;
using VRage.Game;
using VRage.Game.Components;
using VRage.Game.GUI.TextPanel;
using VRage.Game.ModAPI.Ingame;
using VRage.Game.ModAPI.Ingame.Utilities;
using VRage.Game.ObjectBuilders.Definitions;
using VRageMath;

namespace IngameScript
{
    partial class Program
    {
        Dictionary<BlockType, BlockType> jointHierarchy = new Dictionary<BlockType, BlockType>()
        {
            { BlockType.Hip, BlockType.Knee },
            { BlockType.Knee, BlockType.Foot },
            { BlockType.Foot, BlockType.Quad },
        };

        void IterateThroughJoint(List<IMyMotorStator> stators, BlockType type, IMyMotorStator block, string suffix)
        {
            // HR1+
            // KR1+
            bool hasNext = jointHierarchy.ContainsKey(type);
            if (!hasNext)
                return;
            BlockType next = jointHierarchy[type];
            stators.Where(b => b.CubeGrid == block.TopGrid).ToList().ForEach(stator =>
            {
                //if (stator.CustomName.Contains("+") || stator.CustomName.Contains("-"))
                //    return;
                //stator.CustomName += $" {ToInitial(next)}{suffix}";
                stator.CustomName = $"Joint {ToInitial(next)}{suffix}";
                IterateThroughJoint(stators, next, stator, suffix);
            });
        }

        public void TryAutoTag()
        {
            Reload(); // catchup on all configs
            List<IMyMotorStator> stators = BlockFinder.GetBlocksOfType<IMyMotorStator>();
            foreach (var pair in legs)
            {
                var group = pair.Value;
                group.AALeftHipStators.ForEach(j => IterateThroughJoint(stators, BlockType.Hip, j.Stator, $"L{pair.Key}+"));
                group.AARightHipStators.ForEach(j => IterateThroughJoint(stators, BlockType.Hip, j.Stator, $"R{pair.Key}+"));
            }
            Reload();
        }

        string ToGroupName(int group)
        {
            int totalGroups = legs.Count;
            if (group == 1)
                return "Front";
            if (group == totalGroups)
                return "Back";
            return "Middle";
        }

        public void AutoRenameBlocks(string format)
        {
            Reload(); // catchup on all configs
            if (!format.Contains("{tag}"))
                format += " {tag}";
            List<FetchedBlock> stators = BlockFinder.GetBlocksOfType<IMyMotorStator>().Select(BlockFetcher.ParseBlock).Where(p => p.HasValue).Select(p => p.Value).ToList();
            stators.ForEach(b =>
            {
                if (!BlockFetcher.IsLegJoint(b))
                    return; // HR1+
                b.Block.CustomName = format
                    .Replace("{type}", ToName(b.Type))
                    .Replace("{side}", ToName(b.Side))
                    .Replace("{block}", b.Block.BlockDefinition.SubtypeName.Contains("Hinge") ? "Hinge" : "Rotor")
                    .Replace("{group}", b.Group.ToString())
                    .Replace("{groupname}", ToGroupName(b.Group))
                    .Replace("{tag}", $"{ToInitial(b.Type)}{ToInitial(b.Side)}{b.Group}{(b.Inverted ? "-" : "+")}");
            });
            Reload();
        }

        public void AutoRetype(int type)
        {
            Reload(); // catchup on all configs
            foreach (var pair in legs)
            {
                var group = pair.Value;
                group.Configuration.LegType = type;
                group.AllBlocks.ForEach(b => b.CustomData = group.Configuration.ToCustomDataString());
            }
            Reload();
        }
    }
}
