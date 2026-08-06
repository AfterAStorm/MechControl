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


        Dictionary<char, BlockType> charToBlockType = new Dictionary<char, BlockType>()
        {
            { 'h', BlockType.Hip },
            { 'k', BlockType.Knee },
            { 'f', BlockType.Foot },
            { 'q', BlockType.Quad },
            { 't', BlockType.Turn },
            { 's', BlockType.Strafe },
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
                stator.CustomName = $"{(AutoTagIsHinge(stator) ? "Hinge" : "Rotor")} {ToInitial(next)}{suffix}";
                IterateThroughJoint(stators, next, stator, suffix);
            });
        }

        private void RecursiveAutoTag(Dictionary<IMyCubeGrid, HashSet<IMyMotorStator>> gridMap, IMyCubeGrid grid, BlockSide side, int group, BlockType[] orders, int orderIndex)
        {
            if (orderIndex > orders.Length - 1)
                return;
            BlockType order = orders[orderIndex];

            if (!gridMap.ContainsKey(grid))
                return;
            HashSet<IMyMotorStator> stators = gridMap[grid];
            gridMap.Remove(grid);

            HashSet<IMyCubeGrid> nextGrids = new HashSet<IMyCubeGrid>();
            foreach (var stator in stators)
            {
                stator.CustomName = $"{(AutoTagIsHinge(stator) ? "Hinge" : "Rotor")} {ToInitial(order)}{ToInitial(side)}{group}+";
                nextGrids.Add(stator.CubeGrid);
                nextGrids.Add(stator.TopGrid);
            }

            foreach (var next in nextGrids)
                RecursiveAutoTag(gridMap, next, side, group, orders, orderIndex + 1);
        }

        private bool AutoTagIsHinge(IMyMotorStator stator)
        {
            return stator.BlockDefinition.SubtypeName.Contains("Hinge");
        }

        public void TryAutoTagNew(string orders)
        {
            IMyShipController reference = cockpits.Count > 0 ? cockpits.First() : null;
            if (reference == null)
            {
                Reload(); // catchup on all configs
                Log("No reference for autotag");
                return;
            }

            BlockType[] blockTypes = new BlockType[orders.Length];
            for (int i = 0; i < orders.Length; i++)
            {
                char c = orders[i];
                if (charToBlockType.ContainsKey(c))
                    blockTypes[i] = charToBlockType[c];
                else
                    blockTypes[i] = BlockType.Quad;
            }

            Dictionary<IMyCubeGrid, HashSet<IMyMotorStator>> gridMap = new Dictionary<IMyCubeGrid, HashSet<IMyMotorStator>>();
            GridTerminalSystem.GetBlocksOfType<IMyMotorStator>(null, (s) =>
            {
                if (!reference.IsSameConstructAs(s))
                    return false;
                if (!gridMap.ContainsKey(s.CubeGrid))
                    gridMap.Add(s.CubeGrid, new HashSet<IMyMotorStator>());
                gridMap[s.CubeGrid].Add(s);
                if (s.TopGrid != null)
                {
                    if (!gridMap.ContainsKey(s.TopGrid))
                        gridMap.Add(s.TopGrid, new HashSet<IMyMotorStator>());
                    gridMap[s.TopGrid].Add(s);
                }
                return false;
            });
            if (!gridMap.ContainsKey(reference.CubeGrid))
                return; // only possible with no joints
            Dictionary<IMyCubeGrid, float> leftMap = new Dictionary<IMyCubeGrid, float>();
            Dictionary<IMyCubeGrid, float> rightMap = new Dictionary<IMyCubeGrid, float>();
            foreach (var stator in gridMap[reference.CubeGrid])
            {
                IMyCubeGrid remoteGrid = stator.CubeGrid == reference.CubeGrid ? stator.TopGrid : stator.CubeGrid;
                if (remoteGrid == null)
                    continue;

                float dot = Vector3.Dot(stator.GetPosition() - reference.GetPosition(), reference.WorldMatrix.Left);
                bool left = dot > 0;
                var map = left ? leftMap : rightMap;

                if (map.ContainsKey(remoteGrid))
                    continue;

                float distance = Vector3.Dot(reference.WorldMatrix.Forward, reference.GetPosition()) - Vector3.Dot(reference.WorldMatrix.Forward, stator.GetPosition());
                map.Add(remoteGrid, distance);
            }

            //RecursiveAutoTag(gridMap, reference.CubeGrid, blockTypes, 0);
        }

        public void TryAutoTag(string _)
        {
            //Reload(); // catchup on all configs
            IMyShipController reference = cockpits.Count > 0 ? cockpits.First() : null;
            if (reference == null)
            {
                Reload(); // catchup on all configs
                Log("No reference for autotag");
                return;
            }

            List<IMyMotorStator> allStators = blockFinder.GetBlocksOfType<IMyMotorStator>();
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
                    left.CustomName = $"{(AutoTagIsHinge(left) ? "Hinge" : "Rotor")} {ToInitial(BlockType.Hip)}{suffix}";
                    IterateThroughJoint(allStators, BlockType.Hip, left, suffix);
                }

                suffix = $"{ToInitial(BlockSide.Right)}{num}+";
                foreach (var right in leftRight.Item2)
                {
                    right.CustomName = $"{(AutoTagIsHinge(right) ? "Hinge" : "Rotor")} {ToInitial(BlockType.Hip)}{suffix}";
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
            //Reload(); // catchup on all configs
            if (!format.Contains("{tag}"))
                format += " {tag}";
            List<FetchedBlock> stators = blockFetcher.CachedBlocks; //blockFinder.GetBlocksOfType<IMyMotorStator>().SelectMany(blockFetcher.ParseBlock).ToList();
            stators.ForEach(b =>
            {
                //if (!BlockFetcher.IsLegJoint(b))
                //    return; // HR1+
                if (!legs.ContainsKey(b.Group))
                    return;
                var leg = legs[b.Group];
                if (!leg.AllBlocks.Contains(b))
                    return;
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
            //Reload(); // catchup on all configs
            foreach (var pair in legs)
            {
                var group = pair.Value;
                group.Configuration.LegType = type;
                group.ApplyConfiguration();
            }
            Reload();
        }
    }
}
