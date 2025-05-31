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
        public static class BlockFetcher
        {
            private static readonly System.Text.RegularExpressions.Regex NamePattern = new System.Text.RegularExpressions.Regex(@"^([^lr]*)([lr]{1}|left{1}|right{1})?([0-9]+)?([-+]{1})?$");

            static int parsedId;

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

            private static readonly List<BlockType> DoesntRequireSide = new List<BlockType>()
            {
                BlockType.Pitch,
                BlockType.Yaw,
                BlockType.Roll,
                BlockType.Magnet, // arm landing gear

                BlockType.TorsoTwist,

                BlockType.GyroscopeAzimuth,
                BlockType.GyroscopeElevation,
                BlockType.GyroscopeRoll,
                BlockType.GyroscopeStabilization,
                BlockType.GyroscopeStop,
                BlockType.Thruster
            };

            public static FetchedBlock? ParseBlock(IMyTerminalBlock block)
            {
                // Check each segment of the name: "Left Leg - HL" is ["Left", "Leg", "-", "HL"]
                foreach (var segment in block.CustomName.ToLower().Split(' '))
                {
                    var match = NamePattern.Match(segment);
                    if (!match.Success)
                        continue; // invalid segment

                    // Parse the type
                    BlockType? blockType = null;
                    switch (match.Groups[1].Value.Replace("+", "").Replace("-", "")) // the replace is beacuse i cannot regex for some reason D:
                    {
                        /* Leg */
                        case "h":
                            //case "hip":
                            if (!(block is IMyMotorStator) && !(block is IMyPistonBase))
                                break; // Liars!
                            blockType = BlockType.Hip;
                            break;
                        case "k":
                            //case "knee":
                            if (!(block is IMyMotorStator) && !(block is IMyPistonBase))
                                break; // Liars!
                            blockType = BlockType.Knee;
                            break;
                        case "f":
                            //case "fp":
                            //case "foot":
                            //case "feet":
                            if (!(block is IMyMotorStator) && !(block is IMyPistonBase))
                                break; // Liars!
                            blockType = BlockType.Foot;
                            break;
                        case "q":
                            if (!(block is IMyMotorStator))
                                break; // Liars!
                            blockType = BlockType.Quad;
                            break;
                        case "s":
                            if (!(block is IMyMotorStator))
                                break;
                            blockType = BlockType.Strafe;
                            break;
                        /* Arm */
                        case "ap":
                            if (!(block is IMyMotorStator))
                                break; // Liars!
                            blockType = BlockType.Pitch;
                            break;
                        case "ay":
                            if (!(block is IMyMotorStator))
                                break; // Liars!
                            blockType = BlockType.Yaw;
                            break;
                        case "ar":
                            if (!(block is IMyMotorStator))
                                break; // Liars!
                            blockType = BlockType.Roll;
                            break;
                        case "alg":
                        case "amg":
                            if (!(block is IMyLandingGear))
                                break; // Liars!
                            blockType = BlockType.Magnet;
                            break;
                        /* Other */
                        case "tt":
                            if (!(block is IMyMotorStator))
                                break; // Liars!
                            blockType = BlockType.TorsoTwist;
                            break;
                        case "gy": // y for yaw
                            //case "ga":
                            if (!(block is IMyMotorStator) && !(block is IMyGyro))
                                break; // Liars!
                            blockType = BlockType.GyroscopeAzimuth;
                            break;
                        case "gp": // p for pitch
                            //case "ge":
                            if (!(block is IMyMotorStator) && !(block is IMyGyro))
                                break; // Liars!
                            blockType = BlockType.GyroscopeElevation;
                            break;
                        case "g": // we are technically looking for "GR" but we have to check for the r (becomes BlockSide) later (in Program) because the R will get eaten by the regex
                            if (!(block is IMyMotorStator) && !(block is IMyGyro))
                                break; // Liars!
                            blockType = BlockType.GyroscopeRoll;
                            break;
                        case "gg": // g for gyro
                            if (!(block is IMyGyro))
                                break; // Liars!
                            blockType = BlockType.GyroscopeStabilization;
                            break;
                        case "gs": // gs fro gyro stop
                            if (!(block is IMyGyro))
                                break;
                            blockType = BlockType.GyroscopeStop;
                            break;
                        case "mg":
                        case "lg":
                            if (!(block is IMyLandingGear))
                                break; // Liars!
                            blockType = BlockType.LandingGear;
                            break;
                        case "th":
                            if (!(block is IMyThrust))
                                break;
                            blockType = BlockType.Thruster;
                            break;
                        case "c":
                            blockType = BlockType.Camera;
                            break;
                    }
                    if (!blockType.HasValue)
                        continue; // invalid
                    //Log($"{block.CustomName} Got block type!", blockType.Value);

                    // Parse the side
                    BlockSide? side = null;
                    switch (match.Groups[2].Value)
                    {
                        case "l":
                        case "left":
                            side = BlockSide.Left;
                            break;
                        case "r":
                        case "right":
                            side = BlockSide.Right;
                            break;
                    }
                    if (!side.HasValue && !DoesntRequireSide.Contains(blockType.Value))
                        continue; // invalid side
                    //Log("Past side");

                    // Parse the group it's in
                    bool parsed = int.TryParse(match.Groups[3].Value, out parsedId);
                    if (!parsed) // if it fails it might output zero anyway, i'm not sure
                        parsedId = 1;

                    // Parse the ini
                    MyIni ini = new MyIni();
                    if (!ini.TryParse(block.CustomData))
                        ini = null;

                    // require a + or -, guh
                    if (!(match.Groups[4].Value.Equals("-") || match.Groups[1].Value.EndsWith("-")) && !(match.Groups[4].Value.Equals("+") || match.Groups[1].Value.EndsWith("+")))
                        continue;

                    return new FetchedBlock()
                    {
                        Block = block,
                        Type = blockType.Value,
                        Side = side ?? BlockSide.Left,
                        Group = parsedId,
                        Inverted = match.Groups[4].Value.Equals("-") || match.Groups[1].Value.EndsWith("-"),
                        Ini = ini,

                        Name = match.Groups[0].Value

                        //AttachToLeg = blockType.Value != BlockType.TorsoTwist
                    };
                }
                return null;
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

            public static bool IsForLeg(FetchedBlock block)
            {
                switch (block.Type)
                {
                    case BlockType.Hip:
                    case BlockType.Knee:
                    case BlockType.Foot:
                    case BlockType.Quad:
                    case BlockType.Strafe:
                    case BlockType.LandingGear:
                        return true;
                    default:
                        return false;
                }
            }

            public static bool IsForArm(FetchedBlock block)
            {
                switch (block.Type)
                {
                    case BlockType.Yaw:
                    case BlockType.Pitch:
                        return true;
                    default:
                        return false;
                }
            }

            public static void FetchGroups<T, T2>(ref Dictionary<int, T> groups, Dictionary<int, T2> previousConfigs, Func<FetchedBlock, bool> valid, Func<int, T> create, Func<MyIni, T2> parseConfig, Action<FetchedBlock, T> add) where T : JointGroup where T2 : JointConfiguration
            {
                groups.Clear();
                List<FetchedBlock> blocks = BlockFinder.GetBlocksOfType<IMyTerminalBlock>() // get everything
                    .Select(ParseBlock) // turn them into FetchedBlock?
                    .Where(v => v.HasValue) // check if they were valid
                    .Select(v => v.Value) // turn them into FetchedBlock
                    .Where(valid) // check if they are "valid" for this group type
                    .ToList();

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
                        add(block, groups[block.Group]);
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
                    JointConfiguration previousConfiguration = previousConfigs.GetValueOrDefault(block.Group, default(T2));
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
                    leg.SetConfiguration(currentConfiguration);
                    add(block, leg);
                    groups.Add(block.Group, leg);
                }

                foreach (var block in reiterate.Concat(reiterateLater))
                {
                    if (groups.ContainsKey(block.Group)) // the leg was already created! go ahead and add it
                    {
                        Log($"(reiter) Leg already exists {block.Block}");
                        add(block, groups[block.Group]);
                        continue;
                    }

                    // create leg
                    JointConfiguration currentConfiguration = parseConfig(block.Ini);
                    currentConfiguration.Id = block.Group;
                    Log($"(reiter) Creating new leg {block.Block}");

                    var leg = create(currentConfiguration.GetJointType());
                    leg.SetConfiguration(currentConfiguration);
                    add(block, leg);
                    groups.Add(block.Group, leg);
                }
            }

            public static void AddToLeg(FetchedBlock block, LegGroup leg) // adds a fetched block to the leg
            {
                Log($"AddToLeg Block {block.Block.CustomName} as {block.Type}");
                leg.AddBlock(block);
                switch (block.Type)
                {
                    case BlockType.Hip:
                    case BlockType.Knee:
                    case BlockType.Foot: // if its a joint, create it and add it appropriately
                    case BlockType.Quad:
                    case BlockType.Strafe:
                        if (block.Block is IMyPistonBase)
                        {
                            if (block.Side == BlockSide.Left)
                                leg.LeftPistons.Add(block);
                            else
                                leg.RightPistons.Add(block);
                            break;
                        }

                        LegJoint joint = new LegJoint(block);
                        switch (block.Type)
                        {
                            case BlockType.Hip:
                                if (block.Side == BlockSide.Left)
                                    leg.AALeftHipStators.Add(joint);
                                else
                                    leg.AARightHipStators.Add(joint);
                                break;
                            case BlockType.Knee:
                                if (block.Side == BlockSide.Left)
                                    leg.AALeftKneeStators.Add(joint);
                                else
                                    leg.AARightKneeStators.Add(joint);
                                break;
                            case BlockType.Foot:
                                if (block.Side == BlockSide.Left)
                                    leg.AALeftFootStators.Add(joint);
                                else
                                    leg.AARightFootStators.Add(joint);
                                break;
                            case BlockType.Quad:
                                if (block.Side == BlockSide.Left)
                                    leg.AALeftQuadStators.Add(joint);
                                else
                                    leg.AARightQuadStators.Add(joint);
                                break;
                            case BlockType.Strafe:
                                if (block.Side == BlockSide.Left)
                                    leg.AALeftStrafeStators.Add(joint);
                                else
                                    leg.AARightStrafeStators.Add(joint);
                                break;
                        }
                        break;
                    case BlockType.LandingGear: // otherwise just add it normally
                        if (block.Side == BlockSide.Left)
                            leg.LeftGears.Add(block.Block as IMyLandingGear);
                        else
                            leg.RightGears.Add(block.Block as IMyLandingGear);
                        break;
                    default:
                        return;
                }
                block.Block.CustomData = leg.Configuration.ToCustomDataString(); // set new configuration
            }

            public static void AddToArm(FetchedBlock block, ArmGroup arm)
            {
                ArmJointConfiguration jointConfig = ArmJointConfiguration.Parse(block);
                Log($"AddToArm block: {block.Block.CustomData}");
                Log($"offset: {jointConfig.Offset}");

                switch (block.Type)
                {
                    case BlockType.Pitch:
                        arm.PitchJoints.Add(new ArmJoint(block, jointConfig));
                        break;
                    case BlockType.Yaw:
                        arm.YawJoints.Add(new ArmJoint(block, jointConfig));
                        break;
                    /*case BlockType.Roll:
                        arm.RollJoints.Add(new ArmJoint(block, jointConfig));
                        break;*/
                    case BlockType.Magnet:
                        arm.Magnets.Add(block.Block as IMyLandingGear);
                        break;
                    default:
                        return;
                }
                block.Block.CustomData = arm.Configuration.ToCustomDataString() + "" + jointConfig.ToCustomDataString();
            }
        }
    }
}
