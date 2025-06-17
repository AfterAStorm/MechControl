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
            if (string.IsNullOrEmpty(arg))
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
            if (string.IsNullOrEmpty(arg))
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
            if (string.IsNullOrEmpty(arg))
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
            if (string.IsNullOrEmpty(arg))
                return current;
            JointVariable replace = new JointVariable(arg.Trim('=', '+', '-'));
            if (replace.Type != current.Type) // if not same type, just do a =
            {
                arg = "="; // force replacement
            }
            switch (arg.Substring(0, 1))
            {
                default: return current;
                case "=": return replace;
                case "+": return new JointVariable(current.Type, replace.Value + current.Value);
                case "-": return new JointVariable(current.Type, replace.Value - current.Value);
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
                    if ((movementOverride * Vector3.Forward).LengthSquared() != 0)
                        movementOverride *= Vector3.Zero;//Vector3.One - Vector3.Forward; // halt
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
                    ThrusterMode lastMode = ThrusterBehavior;
                    if (arg == null)
                        ThrusterBehavior = (ThrusterMode)(((int)ThrusterBehavior + 1) % 2);
                    else
                        ThrusterBehavior = HandleBoolArgument(ThrusterBehavior == ThrusterMode.Hover, arg) ? ThrusterMode.Hover : ThrusterMode.Override;
                    if (ThrusterBehavior == ThrusterMode.Hover && lastMode != ThrusterMode.Hover)
                        thrustersEnabled = true;
                    break;

                // Settings
                case "apply":
                    foreach (var group in legs.Values)
                        group.ApplyConfiguration();
                    break;

                case "stepspeed":
                    WalkCycleSpeed = HandleFloatArgument(WalkCycleSpeed, arg);
                    break;
                case "crouchspeed":
                    CrouchSpeed = HandleFloatArgument(CrouchSpeed, arg);
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
                case "strafedistance":
                    foreach (var group in legs.Values)
                        group.Configuration.VariableStrafeDistance = HandleVariable(group.Configuration.VariableStrafeDistance, arg);
                    ReinitializeLegs();
                    break;
                case "xoffset":
                    foreach (var group in legs.Values)
                        group.Configuration.VariableXOffset = HandleVariable(group.Configuration.VariableXOffset, arg);
                    ReinitializeLegs();
                    break;
                case "yoffset":
                    foreach (var group in legs.Values)
                        group.Configuration.VariableYOffset = HandleVariable(group.Configuration.VariableYOffset, arg);
                    ReinitializeLegs();
                    break;
                case "zoffset":
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

                case "arm":
                    foreach (var arm in arms.Values)
                        arm.ToZero();
                    break;

                case "legcontrol":
                    legsEnabled = HandleBoolArgument(legsEnabled, arg);
                    break;

                case "armcontrol":
                    armsEnabled = HandleBoolArgument(armsEnabled, arg);
                    break;

                // Fun //
                case "limp":
                    ToggleLimp(HandleBoolArgument(isLimp, arg));
                    break;

                // Test Leg //
                case "ik":
                    testLegX = TryParseFloat(arguments[1]);
                    testLegY = TryParseFloat(arguments[2]);
                    testLegZ = TryParseFloat(arguments[3]);
                    customTarget = new Vector3D(testLegX, testLegY, testLegZ);
                    break;
            }
        }
    }
}
