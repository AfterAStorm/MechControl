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
        bool HandleBoolArgument(bool current, string arg)
        {
            if (string.IsNullOrEmpty(arg) || arg.Equals("toggle"))
                return !current;
            if (arg.Equals("on") || arg.Equals("true"))
                return true;
            if (arg.Equals("off") || arg.Equals("false"))
                return false;
            return current;
        }

        double TryParseDouble(string arg)
        {
            double value;
            bool parsed = double.TryParse(arg, out value);
            return parsed ? value : 0f;
        }

        double HandleDoubleArgument(double current, string arg)
        {
            switch (arg.Substring(0, 1))
            {
                default:
                case "=": return TryParseDouble(arg.Substring(1));
                case "+": return current + TryParseDouble(arg.Substring(1));
                case "-": return current - TryParseDouble(arg.Substring(1));
            }
        }

        float TryParseFloat(string arg)
        {
            float value;
            bool parsed = float.TryParse(arg, out value);
            return parsed ? value : 0f;
        }

        float HandleFloatArgument(float current, string arg)
        {
            switch (arg.Substring(0, 1))
            {
                default:
                case "=": return TryParseFloat(arg.Substring(1));
                case "+": return current + TryParseFloat(arg.Substring(1));
                case "-": return current - TryParseFloat(arg.Substring(1));
            }
        }

        int TryParseInt(string arg)
        {
            int value;
            bool parsed = int.TryParse(arg, out value);
            return parsed ? value : 0;
        }

        int HandleIntArgument(int current, string arg)
        {
            switch (arg.Substring(0, 1))
            {
                default:
                case "=": return TryParseInt(arg.Substring(1));
                case "+": return current + TryParseInt(arg.Substring(1));
                case "-": return current - TryParseInt(arg.Substring(1));
            }
        }

        void HandleCommand(string command)
        {
            string[] arguments = command.Split(' ');
            string arg = arguments.Length > 1 ? string.Join(" ", arguments.Skip(1)) : null;
            switch (arguments[0].ToLower())
            {
                // Core -- integral even //
                default:
                case "reload":
                    Reload();
                    break;

                // Debug //
                case "debug":
                    debugMode = !debugMode;
                    break;

                // Setup & Utilities //
                case "setup":
                    setupMode = HandleBoolArgument(setupMode, arg);
                    lastSetupModeTick = GetUnixTime();
                    break;

                case "autorename":
                    if (arg == null)
                        AutoRenameBlocks("{tag}");
                    else
                        AutoRenameBlocks(arg);
                    break;

                case "autotag":
                    TryAutoTag();
                    break;

                case "autotype":
                    AutoRetype(HandleIntArgument(1, arg));
                    break;

                // Movement //
                case "crouch":
                    crouchOverride = HandleBoolArgument(crouchOverride, arg);
                    break;

                case "walk":
                    // if already moving, halt
                    if ((movementOverride * Vector3.Forward).Z != 0)
                        movementOverride *= new Vector3(1, 1, 0); // halt
                    else
                        switch (arg.ToLower().Trim())
                        {
                            default:
                            case "for":
                            case "forward":
                                movementOverride.Z = Vector3.Forward.Z;
                                break;
                            case "back":
                            case "backward":
                                movementOverride.Z = Vector3.Backward.Z;
                                break;

                        }
                    break;

                case "halt":
                    movementOverride = Vector3.Zero;
                    break;

                case "turn":
                    turnOverride = HandleFloatArgument(turnOverride, arg);
                    break;

                case "thrusters":
                    thrustersEnabled = HandleBoolArgument(thrustersEnabled, arg);
                    break;

                case "hover":
                    if (arg == null)
                        ThrusterBehavior = (ThrusterMode)(((int)ThrusterBehavior + 1) % 2);
                    else
                        ThrusterBehavior = HandleBoolArgument(ThrusterBehavior == ThrusterMode.Hover, arg) ? ThrusterMode.Hover : ThrusterMode.Override;
                    break;

                // Settings
                case "stepspeed":
                    WalkCycleSpeed = HandleFloatArgument(WalkCycleSpeed, arg);
                    break;

                case "lean":
                    StandingLean = HandleDoubleArgument(StandingLean, arg);
                    AccelerationLean = HandleDoubleArgument(AccelerationLean, arg);
                    break;
                case "standinglean":
                    StandingLean = HandleDoubleArgument(StandingLean, arg);
                    break;
                case "accelerationlean":
                    AccelerationLean = HandleDoubleArgument(AccelerationLean, arg);
                    break;

                case "standingheight":
                    StandingHeight = HandleFloatArgument(StandingHeight, arg);
                    break;
                case "steplength":
                    foreach (var group in legs.Values)
                        group.Configuration.StepLength = HandleDoubleArgument(group.Configuration.StepLength, arg);
                    break;
                case "stepheight":
                    foreach (var group in legs.Values)
                        group.Configuration.StepHeight = HandleDoubleArgument(group.Configuration.StepLength, arg);
                    break;

                case "autohalt":
                    AccelerationLean = HandleDoubleArgument(AccelerationLean, arg);
                    break;

                // Joints & Arms
                case "twist":
                    targetTorsoTwistAngle = HandleDoubleArgument(0, arg).Modulo(360);
                    break;

                case "arm":
                    foreach (var arm in arms.Values)
                        arm.ToZero();
                    break;

                case "armcontrol":
                    armsEnabled = HandleBoolArgument(armsEnabled, arg);
                    break;

                // Fun //
                case "limp":
                    ToggleLimp(HandleBoolArgument(isLimp, arg));
                    break;
            }
        }
    }
}
