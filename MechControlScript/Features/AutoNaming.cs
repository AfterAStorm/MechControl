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
            { BlockType.Hip , BlockType.Knee },
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
            IMyShipController reference = cockpits.Count > 0 ? cockpits.First() : null;
            if (reference == null)
            {
                Log("No reference for autotag");
                return;
            }

            List<IMyMotorStator> allStators = BlockFinder.GetBlocksOfType<IMyMotorStator>();
            var stators = allStators.Where(stator => stator.CubeGrid == Me.CubeGrid);
            Dictionary<float, MyTuple<List<IMyMotorStator>, List<IMyMotorStator>>> groups = new Dictionary<float, MyTuple<List<IMyMotorStator>, List<IMyMotorStator>>>(); 
            foreach (var stator in stators)
            {
                float dot = Vector3.Dot(stator.GetPosition() - reference.GetPosition(), reference.WorldMatrix.Left);
                BlockSide side = dot > 0 ? BlockSide.Left : BlockSide.Right;

                float distance = Vector3.Dot(reference.WorldMatrix.Forward, reference.GetPosition()) - Vector3.Dot(reference.WorldMatrix.Forward, stator.GetPosition());

                // check if close enough key
                bool found = false;
                foreach (var kv in groups)
                {
                    if (Math.Abs(kv.Key - distance) < .1f)
                    {
                        found = true;
                        if (side == BlockSide.Left)
                            groups[kv.Key].Item1.Add(stator);
                        else if (side == BlockSide.Right)
                            groups[kv.Key].Item2.Add(stator);
                        break;
                    }
                }

                if (!found)
                {
                    // create instead
                    groups[distance] = new MyTuple<List<IMyMotorStator>, List<IMyMotorStator>>(new List<IMyMotorStator>(), new List<IMyMotorStator>());
                        if (side == BlockSide.Left)
                        groups[distance].Item1.Add(stator);
                    else if (side == BlockSide.Right)
                        groups[distance].Item2.Add(stator);
                }

                //string suffix = $"{ToInitial(side)}{"
                //stator.CustomName = $"Joint {ToInitial(side)}{}"

            }
            List<float> distances = groups.Keys.ToList();
            distances.Sort();

            int num = 1;
            foreach (var distance in distances)
            {
                var leftRight = groups[distance];

                string suffix = $"{ToInitial(BlockSide.Left)}{num}+";
                foreach (var left in leftRight.Item1)
                {
                    left.CustomName = $"{ToInitial(BlockType.Hip)}{suffix}";
                    IterateThroughJoint(allStators, BlockType.Hip, left, suffix);
                }

                suffix = $"{ToInitial(BlockSide.Right)}{num}+";
                foreach (var right in leftRight.Item2)
                {
                    right.CustomName = $"{ToInitial(BlockType.Hip)}{suffix}";
                    IterateThroughJoint(allStators, BlockType.Hip, right, suffix);
                }

                num++;
            }
            /*foreach (var pair in legs)
            {
                var group = pair.Value;
                //group.AALeftHipStators.ForEach(j => IterateThroughJoint(stators, BlockType.Hip, j.Stator, $"L{pair.Key}+"));
                //group.AARightHipStators.ForEach(j => IterateThroughJoint(stators, BlockType.Hip, j.Stator, $"R{pair.Key}+"));
            }*/
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
                group.AllBlocks.ForEach(b => b.Block.CustomData = group.Configuration.ToCustomDataString());
            }
            Reload();
        }
    }
}
