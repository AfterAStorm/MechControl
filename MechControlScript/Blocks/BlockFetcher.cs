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
        public class BlockFetcher
        {
            //private static readonly System.Text.RegularExpressions.Regex OldNamePattern = new System.Text.RegularExpressions.Regex(@"^([^lr]*)([lr]{1})?([0-9]+)?([-+]{1})?$");
            private static readonly System.Text.RegularExpressions.Regex NamePattern = new System.Text.RegularExpressions.Regex(@"^([^0-9-+]*)([0-9]+)?([-+]{1})$");

            public List<FetchedBlock> CachedBlocks = new List<FetchedBlock>();
            public Dictionary<BlockType, HashSet<FetchedBlock>> CachedBlocksByType = new Dictionary<BlockType, HashSet<FetchedBlock>>();
            private List<IMyTerminalBlock> allBlocks = new List<IMyTerminalBlock>();

            static int parsedId;

            /// <summary>
            /// The current block the block fetcher is checking during Invalidate(ion)
            /// </summary>
            public int CurrentBlock { get; private set; }

            /// <summary>
            /// The total blocks on the grid
            /// </summary>
            public int TotalBlocks { get; private set; }

            private BlockFinder finder;
            private ConfigManager configManager;

            public BlockFetcher(BlockFinder finder, ConfigManager configManager)
            {
                this.finder = finder;
                this.configManager = configManager;
            }

            public static LegGroup CreateLegFromType(int type)
            {
                switch (type)
                {
                    case 0:
                    case 1:
                        return new HumanoidLegGroup();
                    case 2:
                        return new ChickenWalkerLegGroup();
                    case 3:
                        return new SpideroidLegGroup();
                    case 4:
                        return new CrabLegGroup();
                    case 5:
                        return new DigitigradeLegGroup();
                    case 6:
                        return new PrismaticLegGroup();
                    case 9:
                        return new TestLegGroup();
                    default:
                        StaticWarn("Leg Type Not Supported!", $"Leg type {type} is not supported!");
                        return new HumanoidLegGroup();
                        //throw new Exception($"Leg type {type} not implemented!");
                }
            }

            public static ArmGroup CreateArmFromType(int type)
            {
                return new ArmGroup();
            }

            private struct BlockRequirements
            {
                public BlockType Type;
                public bool RequiresSide;
                public bool RequiresId;
                public string[] ValidTypes;

                public BlockRequirements(BlockType type, bool requiresSide, bool requiresId, params string[] types)
                {
                    Type = type;
                    RequiresSide = requiresSide;
                    RequiresId = requiresId;
                    ValidTypes = types;
                }

                public bool IsValidType(object type) // isn't it strange that structs have methods? aren't they supposed to be *data containers*?!
                {
                    // MAKE SURE THIS IS ALWAYS IN SYNC WITH ParseBlock FIRST REDUNDANCY CHECK!
                    foreach (var validType in ValidTypes)
                    {
                        switch (validType)
                        {
                            case "Stator":
                                if (type as IMyMotorStator != null)
                                    return true;
                                break;
                            case "Gyro":
                                if (type as IMyGyro != null)
                                    return true;
                                break;
                            case "Thrust":
                                if (type as IMyThrust != null)
                                    return true;
                                break;
                            case "Camera":
                                if (type as IMyCameraBlock != null)
                                    return true;
                                break;
                            case "Piston":
                                if (type as IMyPistonBase != null)
                                    return true;
                                break;
                            case "Magnet":
                                if (type as IMyLandingGear != null)
                                    return true;
                                break;
                            case "AIFlight":
                                if (type as IMyFlightMovementBlock != null)
                                    return true;
                                break;
                        }
                    }
                    return false;
                }
            }

            /*private static readonly string[] blockTypes = new string[]
            {
                "Stator", "Piston", "Thrust", "Gyro", "Camera", "Piston", "Magnet"
            };*/

            //BlockRequirements anyBlockRequirement = new BlockRequirements(BlockType.Hip, false, false, blockTypes);

            private static readonly Dictionary<string, BlockRequirements> blockRequirements = new Dictionary<string, BlockRequirements>() {
                // legs
                { "h" , new BlockRequirements(BlockType.Hip,  true, true, "Stator") },
                { "k" , new BlockRequirements(BlockType.Knee, true, true, "Stator", "Piston") },
                { "f" , new BlockRequirements(BlockType.Foot, true, true, "Stator") },
                { "q" , new BlockRequirements(BlockType.Quad, true, true, "Stator") },
                { "s" , new BlockRequirements(BlockType.Strafe, true, true, "Stator") },
                { "t" , new BlockRequirements(BlockType.Turn, true, true, "Stator") },

                // arms
                { "ay", new BlockRequirements(BlockType.ArmYaw, false, true, "Stator") },
                { "ap", new BlockRequirements(BlockType.ArmPitch, false, true, "Stator") },
                { "ar", new BlockRequirements(BlockType.ArmRoll, false, true, "Stator") },

                // thrusters
                { "th", new BlockRequirements(BlockType.Thruster, false, false, "Thrust") },

                { "vp", new BlockRequirements(BlockType.VtolElevation, false, false, "Stator") }, // mouse
                { "vy", new BlockRequirements(BlockType.VtolAzimuth, false, false, "Stator") },

                { "vt", new BlockRequirements(BlockType.VtolTurn, false, false, "Stator") }, // keyboard
                { "vf", new BlockRequirements(BlockType.VtolForward, false, false, "Stator") },
                { "vs", new BlockRequirements(BlockType.VtolStrafe, false, false, "Stator") },
                { "vv", new BlockRequirements(BlockType.VtolVertical, false, false, "Stator") },

                // stabilization
                { "gy", new BlockRequirements(BlockType.GyroscopeAzimuth, false, false, "Stator", "Gyro") },
                { "gp", new BlockRequirements(BlockType.GyroscopeElevation, false, false, "Stator", "Gyro") },
                { "gr", new BlockRequirements(BlockType.GyroscopeRoll, false, false, "Stator", "Gyro") },
                { "gs", new BlockRequirements(BlockType.GyroscopeStop, false, false, "Stator", "Gyro") },
                { "gg", new BlockRequirements(BlockType.GyroscopeStabilization, false, false, "Gyro") },
                { "ai", new BlockRequirements(BlockType.AI, false, false, "AIFlight") },

                // misc
                { "tt", new BlockRequirements(BlockType.TorsoTwist, false, false, "Stator") },
                { "c" , new BlockRequirements(BlockType.Camera, true, true, "Camera") },
                { "hy", new BlockRequirements(BlockType.Hydraulic, false, true, "Piston") },
                { "m" , new BlockRequirements(BlockType.Magnet, true, true, "Magnet") },
                { "am", new BlockRequirements(BlockType.Animatable, false, true, "Stator", "Piston") }
            };

            public IEnumerator Invalidate()
            {
                configManager.Clear();
                CachedBlocks.Clear();
                CachedBlocksByType.Clear();
                // is this better than just re-allocating the list and .ToList()? no idea!
                //CachedBlocks.AddRange(finder.GetBlocksOfType<IMyTerminalBlock>().SelectMany(ParseBlock));
                int maxInstructions = Singleton.Runtime.MaxInstructionCount - 1000;
                Singleton.GridTerminalSystem.GetBlocksOfType(allBlocks, Singleton.Me.IsSameConstructAs);//(t) => t.IsSameConstructAs(Singleton.Me));
                TotalBlocks = allBlocks.Count;
                for (int i = 0; i < allBlocks.Count; i++)
                {
                    CurrentBlock = i;
                    var block = allBlocks[i];

                    ParseBlock(block);
                    CachedBlocks.AddRange(blocks);
                    foreach (var subblock in blocks)
                    {
                        if (!CachedBlocksByType.ContainsKey(subblock.Type))
                            CachedBlocksByType.Add(subblock.Type, new HashSet<FetchedBlock>());
                        CachedBlocksByType[subblock.Type].Add(subblock);
                    }

                    if (Singleton.Runtime.CurrentInstructionCount > maxInstructions)
                        yield return null;
                }

                allBlocks.Clear();
                yield return null;
            }

            public IEnumerable<FetchedBlock> GetBlocks(params BlockType[] type)
            {
                //return CachedBlocks.Where(fb => type.Contains(fb.Type));
                foreach (var atype in type)
                {
                    if (!CachedBlocksByType.ContainsKey(atype))
                        continue;
                    foreach (var block in CachedBlocksByType[atype])
                        yield return block;
                }
            }

            public IEnumerable<FetchedBlock> GetBlocks(IMyTerminalBlock block)
            {
                return CachedBlocks.Where(fb => fb.Block.Equals(block));
            }

            readonly List<FetchedBlock> blocks = new List<FetchedBlock>();

            public List<FetchedBlock> ParseBlock(IMyTerminalBlock block)
            {
                //if (!anyBlockRequirement.IsValidType(block))
                //    return blocks;
                blocks.Clear();
                if (!(block is IMyMotorStator) && !(block is IMyGyro) && !(block is IMyThrust) && !(block is IMyCameraBlock) && !(block is IMyPistonBase) && !(block is IMyLandingGear) && !(block is IMyFlightMovementBlock))
                {
                    return blocks;
                }

                foreach (var tagged in block.CustomName.ToLower().Split(' '))
                {
                    var match = NamePattern.Match(tagged);
                    if (!match.Success)
                        continue; // not a validly formatted tag

                    string tag = match.Groups[1].Value;

                    // search for requirements
                    if (!blockRequirements.ContainsKey(tag) && !blockRequirements.ContainsKey(tag.TrimEnd('l', 'r'))) // gross, but it works
                        continue; // invalid tag
                    BlockRequirements requirements = blockRequirements.ContainsKey(tag) ? blockRequirements[tag] : blockRequirements[tag.TrimEnd('l', 'r')];
                    if (!requirements.IsValidType(block))
                        continue; // invalid block type

                    BlockSide? side = null;
                    switch (tag.Substring(tag.Length - 1, 1))
                    {
                        case "l":
                            side = BlockSide.Left;
                            break;
                        case "r":
                            side = BlockSide.Right;
                            break;
                    }
                    if (!side.HasValue && requirements.RequiresSide)
                        continue; // missing side

                    bool parsed = int.TryParse(match.Groups[2].Value, out parsedId);
                    if (!parsed || !requirements.RequiresId) // if it fails it might output zero anyway, i'm not sure
                        parsedId = 1;

                    if (!match.Groups[3].Value.Equals("+") && !match.Groups[3].Value.Equals("-"))
                        continue; // must include + or -!

                    MyIni ini = configManager.GetConfiguration(block);

                    blocks.Add(new FetchedBlock()
                    {
                        Block = block,
                        Type = requirements.Type,
                        Side = side ?? BlockSide.Left,
                        Group = parsedId,
                        Inverted = match.Groups[3].Value.Equals("-"),
                        Ini = ini,
                        Name = match.Groups[0].Value
                    });
                }

                return blocks;
            }

            public static bool IsLegJoint(FetchedBlock block) // pretty sure this is duplicate of IsForLeg, but is used in AutoNaming instead of fetching blocks...?
            {
                switch (block.Type)
                {
                    case BlockType.Hip:
                    case BlockType.Knee:
                    case BlockType.Foot:
                    case BlockType.Quad:
                    case BlockType.Strafe:
                        return true;
                    default:
                        return false;
                }
            }

            public void FetchGroups<T, T2>(ref Dictionary<int, T> groups, Dictionary<int, T2> previousConfigs, Func<int, T> create, Func<MyIni, T2> parseConfig) where T : JointGroup where T2 : JointConfiguration
            {
                Log("FetchGroups", typeof(T2).Name);
                groups.Clear();
                List<FetchedBlock> blocks = CachedBlocks;/*BlockFinder.GetBlocksOfType<IMyTerminalBlock>() // get everything
                    .Select(ParseBlockOne) // turn them into FetchedBlock?
                    .Where(v => v.HasValue) // check if they were valid
                    .Select(v => v.Value) // turn them into FetchedBlock
                    .Where(valid) // check if they are "valid" for this group type
                    .ToList();*/

                // we have a list of blocks
                // we have a list of the previous configurations
                // we loop through all current blocks and check for a different config than previous
                // if we find one, we create a leg and start adding blocks to it
                // :later: we loop through blocks that had the same config, and check for the leg+add and/or create the leg anyway
                // :later2: we loop through blocks that didn't have a valid config, and leg+add or create the leg anyway

                List<FetchedBlock> reiterate = new List<FetchedBlock>();
                List<FetchedBlock> reiterateLater = new List<FetchedBlock>();

                List<string> sections = new List<string>();
                // we know each "block" is valid for this group type
                foreach (var block in blocks)
                {
                    if (groups.ContainsKey(block.Group)) // the leg was already created! go ahead and add it
                    {
                        groups[block.Group].AddBlock(block); // if this fails, it won't iterate or anything again so it doesn't matter
                        continue;
                    }

                    if (block.Ini == null) // the block doesn't have a valid configuration, so we can worry about it last
                    {
                        Log($"Ini is null {block.Block}");
                        reiterateLater.Add(block);
                        continue;
                    }
                    sections.Clear();
                    block.Ini.GetSections(sections);
                    if (sections.Count <= 0) // the block doesn't have a valid configuration, so we can worry about it last
                    {
                        Log($"Ini has no sections {block.Block}");
                        reiterateLater.Add(block);
                        continue;
                    }

                    // check configs
                    JointConfiguration previousConfiguration = previousConfigs.GetValueOrDefault(block.Group, null);
                    JointConfiguration currentConfiguration = parseConfig(block.Ini);
                    if (previousConfiguration == null || previousConfiguration.Equals(currentConfiguration)) // the configs are the same, so check later
                    {
                        Log($"Configuration isn't different! {block.Block} {previousConfiguration} {currentConfiguration}");
                        reiterate.Add(block);
                        continue;
                    }

                    Log($"New configuration! {block.Block}");
                    // create leg
                    Log($"Creating new leg {block.Block}");
                    currentConfiguration.Id = block.Group;
                    var leg = create(currentConfiguration.GetJointType());
                    Log($"created leg type: {leg}");
                    if (!leg.AddBlock(block)) // if it's not a valid joint for a leg, don't use its config
                    {
                        Log("not a valid leg block!");
                        continue; // this also means the leg will be created, but we filter them later so we don't waste update ticks
                    }
                    groups.Add(block.Group, leg);
                    leg.SetConfiguration(currentConfiguration);
                }

                foreach (var block in reiterate.Concat(reiterateLater))
                {
                    if (groups.ContainsKey(block.Group)) // the leg was already created! go ahead and add it
                    {
                        Log($"(reiter) Leg already exists {block.Block}");
                        groups[block.Group].AddBlock(block); // as above, it doesn't matter if this fails
                        continue;
                    }

                    // create leg
                    JointConfiguration currentConfiguration = parseConfig(block.Ini);
                    currentConfiguration.Id = block.Group;
                    Log($"(reiter) Creating new leg {block.Block}");

                    var leg = create(currentConfiguration.GetJointType());
                    Log($"created leg type: {leg}");
                    if (!leg.AddBlock(block))
                    {
                        Log("(reiter) not a valid leg block!");
                        continue; // and as above, not valid for leg, so start iterating through legs until it works
                    }
                    groups.Add(block.Group, leg);
                    leg.SetConfiguration(currentConfiguration);
                }

                List<int> invalidGroups = new List<int>();
                foreach (var group in groups)
                    if (group.Value.AllBlocks.Count == 0)
                        invalidGroups.Add(group.Key);
                foreach (var invalid in invalidGroups)
                    groups.Remove(invalid);

                //foreach (var group in groups.Values)
                //    group.ApplyConfiguration();
            }
        }
    }
}
