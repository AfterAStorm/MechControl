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
            return double.TryParse(arg, out value) ? value : 0f;
        }

        double HandleDoubleArgument(double current, string arg)
        {
            if (string.IsNullOrEmpty(arg) || arg.Length <= 1)
                return current;
            switch (arg.Substring(0, 1))
            {
                default: return current;
                case "=": return TryParseDouble(arg.Substring(1));
                case "+": return current + TryParseDouble(arg.Substring(1));
                case "-": return current - TryParseDouble(arg.Substring(1));
            }
        }

        float TryParseFloat(string arg)
        {
            float value;
            return float.TryParse(arg, out value) ? value : 0f;
        }

        float HandleFloatArgument(float current, string arg)
        {
            if (string.IsNullOrEmpty(arg) || arg.Length <= 1)
                return current;
            switch (arg.Substring(0, 1))
            {
                default: return current;
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
            if (string.IsNullOrEmpty(arg) || arg.Length <= 1)
                return current;
            switch (arg.Substring(0, 1))
            {
                default: return TryParseInt(arg);
                case "=": return TryParseInt(arg.Substring(1));
                case "+": return current + TryParseInt(arg.Substring(1));
                case "-": return current - TryParseInt(arg.Substring(1));
            }
        }

        JointVariable HandleVariable(JointVariable current, string arg)
        {
            if (string.IsNullOrEmpty(arg) || arg.Length <= 1)
                return current;
            JointVariable replace = new JointVariable(arg.Substring(1));//arg.Trim('=', '+', '-'));
            if (replace.Type != current.Type) // if not same type, just do a =
            {
                arg = "="; // force replacement
            }
            switch (arg.Substring(0, 1))
            {
                default: return current;
                case "=": return replace;
                case "+": return new JointVariable(current.Type, current.Value + replace.Value);
                case "-": return new JointVariable(current.Type, current.Value - replace.Value);
            }
        }

        void HandleJointGroupToggle<T>(string[] arguments, Dictionary<int, T> groups) where T : JointGroup
        {
            if (arguments.Length <= 1)
            {
                foreach (var agroup in groups.Values)
                {
                    agroup.ToggleEnabled(!agroup.Enabled); // default toggle
                }
                return;
            }
            int id = TryParseInt(arguments[1]);
            if (id <= 0)
            {
                if (arguments[1].ToLowerInvariant().Equals("all"))
                {
                    foreach (var allgroup in groups.Values)
                    {
                        allgroup.ToggleEnabled(HandleBoolArgument(allgroup.Enabled, arguments.Length == 3 ? arguments[2] : null));
                    }
                }
                else
                {
                    // old system
                    foreach (var agroup in groups.Values)
                    {
                        agroup.ToggleEnabled(HandleBoolArgument(agroup.Enabled, arguments.Length >= 2 ? arguments[1] : null));
                    }
                }
                return;
            }
            if (!groups.ContainsKey(id))
                return;
            var group = groups[id];
            if (arguments.Length == 3) // <id> <toggle>
            {
                group.ToggleEnabled(HandleBoolArgument(group.Enabled, arguments[2]));
            }
            else if (arguments.Length == 2) // <id>
            {
                group.ToggleEnabled(!group.Enabled);
            }
        }

        void ReinitializeLegs()
        {
            foreach (var leg in legs.Values)
                leg.Initialize();
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

                case "debugstep":
                    customAnimationStep = HandleDoubleArgument(customAnimationStep, arg);
                    break;

                case "debugtarget":
                    if (arguments.Length < 4)
                        return; // requires [3]
                    customTarget = new Vector3D(TryParseFloat(arguments[1]), TryParseFloat(arguments[2]), TryParseFloat(arguments[3]));
                    break;

                // Setup & Utilities //
                case "setup":
                    setupMode = HandleBoolArgument(setupMode, arg);
                    lastSetupModeTick = GetUnixTime();
                    break;

                case "defaults":
                    useLegDefaults = HandleBoolArgument(useLegDefaults, arg);
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
                    /*if ((movementOverride * Vector3.Forward).LengthSquared() != 0)
                        break; // movementOverride *= Vector3.Zero;//Vector3.One - Vector3.Forward; // halt
                    else
                        switch (arg == null ? "" : arg.ToLower().Trim())
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

                        }*/
                    switch (arguments.Length > 1 ? arguments[1]?.ToLower().Trim() : "toggle")
                    {
                        default:
                        case "toggle":
                            if ((movementOverride * Vector3.Forward).LengthSquared() != 0)
                                movementOverride *= (Vector3.UnitX + Vector3.UnitY);//Vector3.One - Vector3.Forward; // halt
                            else if (arguments.Length > 2)
                            {
                                switch (arguments[2])
                                {
                                    case "forwards":
                                    case "forward":
                                        movementOverride.Z = -1;
                                        break;
                                    case "backwards":
                                    case "backward":
                                        movementOverride.Z = 1;
                                        break;
                                }
                            }
                            else
                                movementOverride.Z = -1;
                            break;
                        case "forwards":
                        case "forward":
                            movementOverride.Z = -1;
                            break;
                        case "backwards":
                        case "backward":
                            movementOverride.Z = 1;
                            break;
                        case "halt":
                            movementOverride.Z = 0;
                            break;
                    }
                    break;

                case "strafe":
                    switch (arg?.ToLower().Trim())
                    {
                        case "left":
                            movementOverride.X = -1;
                            break;
                        case "right":
                            movementOverride.X = 1;
                            break;
                        default:
                        case "halt":
                            movementOverride.X = 0;
                            break;
                    }
                    break;

                case "halt":
                    movementOverride = Vector3.Zero;
                    turnOverride = 0;
                    break;

                case "turn":
                    switch (arg?.ToLower().Trim())
                    {
                        case "left":
                            turnOverride = -1;
                            break;
                        case "right":
                            turnOverride = 1;
                            break;
                        case "halt":
                            turnOverride = 0;
                            break;
                        default:
                            turnOverride = HandleFloatArgument(turnOverride, arg);
                            break;
                    }
                    break;

                case "thrusters":
                    ToggleThrustersEnabled(HandleBoolArgument(thrustersEnabled, arg));
                    break;

                case "vtol":
                    ToggleVtolEnabled(HandleBoolArgument(thrustersVtol, arg));
                    break;

                case "hover":
                    thrusterBehavior = HandleBoolArgument(thrusterBehavior == ThrusterMode.Hover, arg) ? ThrusterMode.Hover : ThrusterMode.Override;
                    break;

                // Settings
                case "apply":
                    foreach (var group in legs.Values)
                        group.ApplyConfiguration();
                    break;

                case "stepspeed":
                    foreach (var group in legs.Values)
                        group.Configuration.AnimationSpeed = HandleDoubleArgument(group.Configuration.AnimationSpeed, arg);
                    break;
                case "crouchspeed":
                    foreach (var group in legs.Values)
                        group.Configuration.CrouchSpeed = HandleDoubleArgument(group.Configuration.CrouchSpeed, arg);
                    break;

                /*case "lean":
                    StandingLean = HandleDoubleArgument(StandingLean, arg);
                    AccelerationLean = HandleDoubleArgument(AccelerationLean, arg);
                    break;*/
                /*case "standinglean":
                    StandingLean = HandleDoubleArgument(StandingLean, arg);
                    break;
                case "accelerationlean":
                    AccelerationLean = HandleDoubleArgument(AccelerationLean, arg);
                    break;*/

                case "standingheight":
                    //StandingHeight = HandleFloatArgument(StandingHeight, arg);
                    foreach (var group in legs.Values)
                        group.Configuration.VariableStandingHeight = HandleVariable(group.Configuration.VariableStandingHeight, arg);
                    ReinitializeLegs();
                    break;
                case "standingdistance":
                    foreach (var group in legs.Values)
                        group.Configuration.VariableStandingDistance = HandleVariable(group.Configuration.VariableStandingDistance, arg);
                    ReinitializeLegs();
                    break;
                case "steplength":
                    foreach (var group in legs.Values)
                        group.Configuration.VariableStepLength = HandleVariable(group.Configuration.VariableStepLength, arg);
                    ReinitializeLegs();
                    break;
                case "stepheight":
                    foreach (var group in legs.Values)
                        group.Configuration.VariableStepHeight = HandleVariable(group.Configuration.VariableStepHeight, arg);
                    ReinitializeLegs();
                    break;
                case "crouchheight":
                    foreach (var group in legs.Values)
                        group.Configuration.VariableCrouchHeight = HandleVariable(group.Configuration.VariableCrouchHeight, arg);
                    ReinitializeLegs();
                    break;
                case "strafelength":
                    foreach (var group in legs.Values)
                        group.Configuration.VariableStrafeDistance = HandleVariable(group.Configuration.VariableStrafeDistance, arg);
                    ReinitializeLegs();
                    break;
                case "standinglean"://case "xoffset":
                    foreach (var group in legs.Values)
                        group.Configuration.VariableXOffset = HandleVariable(group.Configuration.VariableXOffset, arg);
                    ReinitializeLegs();
                    break;
                /*case "yoffset": // "standing height" clone
                    foreach (var group in legs.Values)
                        group.Configuration.VariableYOffset = HandleVariable(group.Configuration.VariableYOffset, arg);
                    ReinitializeLegs();
                    break;*/
                case "standingwidth"://case "zoffset":
                    foreach (var group in legs.Values)
                        group.Configuration.VariableZOffset = HandleVariable(group.Configuration.VariableZOffset, arg);
                    ReinitializeLegs();
                    break;

                case "autohalt":
                    AutoHalt = HandleBoolArgument(AutoHalt, arg);
                    break;

                // Joints & Arms
                case "twist":
                    targetTorsoTwistAngle = HandleDoubleArgument(torsoTwistStators.Average(j => j.Stator.Angle).ToDegrees(), arg).Modulo(360);
                    break;

                case "armsreset":
                    foreach (var arm in arms.Values)
                        arm.ToZero(); // TODO: select legs with 1 / 2 / all
                    break;

                case "legs"://control":
                    HandleJointGroupToggle(arguments, legs);
                    //ToggleLegsEnabled(HandleBoolArgument(legsEnabled, arg));
                    break;

                case "arms"://control":
                    HandleJointGroupToggle(arguments, arms);
                    //ToggleArmsEnabled(HandleBoolArgument(armsEnabled, arg));
                    break;

                case "stabilization"://control":
                    ToggleStabilizationEnabled(HandleBoolArgument(stabilizationEnabled, arg));
                    break;

                case "magnets":
                    ToggleMagnetsEnabled(HandleBoolArgument(magnetsEnabled, arg));
                    break;

                // Fun //
                case "limp":
                    ToggleLimp(HandleBoolArgument(isLimp, arg));
                    break;
            }
        }
    }
}
