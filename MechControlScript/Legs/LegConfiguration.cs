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

        public enum JointVariableType
        {
            Percentage,
            Blocks,
            Meters
        }

        public struct JointVariable
        {
            public JointVariableType Type;
            public float Value;

            public JointVariable(string value)
            {
                string suffix = value.Substring(value.Length - 1, 1);
                switch (suffix.ToLower())
                {
                    case "%":
                        Type = JointVariableType.Percentage;
                        break;
                    case "b":
                        Type = JointVariableType.Blocks;
                        break;
                    default:
                    case "m":
                        Type = JointVariableType.Meters;
                        break;
                }
                string remaining = suffix.All(char.IsDigit) ? value : value.Substring(0, value.Length - 1);
                if (!float.TryParse(remaining, out Value))
                {
                    Value = 0;
                }
            }

            public JointVariable(JointVariableType type, float value)
            {
                Type = type;
                Value = value;
            }

            public override string ToString()
            {
                string suffix = "";
                switch (Type)
                {
                    case JointVariableType.Percentage: suffix = "%"; break;
                    case JointVariableType.Blocks: suffix = "b"; break;
                    case JointVariableType.Meters: suffix = "m"; break;
                }
                return Value + suffix;
            }

            public override int GetHashCode()
            {
                return ToString().GetHashCode();
            }

            public float GetMetersOf(float gridSize, float minMeters, float maxMeters)
            {
                switch (Type)
                {
                    case JointVariableType.Percentage:
                        return (float)MathHelper.Lerp(minMeters, maxMeters, Value / 100d);
                    case JointVariableType.Blocks:
                        return minMeters + Value * gridSize;
                    case JointVariableType.Meters:
                        return minMeters + Value;
                }
                return minMeters + Value;
            }

        }

        /// <summary>
        /// Holds configuration information
        /// Each configuration has a numerical id starting at one (default)
        /// </summary>
        public class LegConfiguration : JointConfiguration
        {

            #region # - Properties

            public static readonly LegConfiguration DEFAULT = Create();

            private static MyIni ini;

            public int LegType;
            public bool HipsInverted = false, KneesInverted = false, FeetInverted = false, QuadInverted = false; // we define = false because they aren't set anymore (deprecated) TODO: REMOVE
            public double HipOffsets, KneeOffsets, FootOffsets, QuadOffsets, StrafeOffsets;
            public JointVariable VariableXOffset, VariableYOffset, VariableZOffset;

            public float? ThighLength = null, CalfLength = null, AnkleLength = null;

            public double StepLength;
            public double StepHeight;
            public JointVariable VariableStepLength, VariableStepHeight, VariableCrouchHeight;

            public double AnimationSpeed;// => WalkCycleSpeed;
            public double CrouchSpeed;

            public bool IndependantStep => IndependantStepEnabled;
            public bool VtolActive = true;

            public JointVariable VariableStandingHeight, VariableStandingDistance, VariableStrafeDistance;

            private int defaultValue;
            public bool Default => defaultValue <= 0;

            #endregion

            #region # - Methods

            public override int GetJointType()
            {
                return LegType;
            }

            public override bool Equals(object obj)
            {
                Log($"Comparing LegConfiguration {this} with {obj}");
                LegConfiguration a = (LegConfiguration)obj;
                return GetHashCode() == a.GetHashCode();
            }

            public override int GetHashCode()
            {
                return (
                    LegType.GetHashCode() * 2 +
                    HipOffsets.GetHashCode() * 3 +
                    KneeOffsets.GetHashCode() * 4 +
                    FootOffsets.GetHashCode() * 5 +
                    QuadOffsets.GetHashCode() * 6 +
                    StrafeOffsets.GetHashCode() * 7 +
                    VariableXOffset.GetHashCode() * 8 +
                    VariableYOffset.GetHashCode() * 9 +
                    VariableZOffset.GetHashCode() * 10 +
                    VariableStepLength.GetHashCode() * 11 +
                    VariableStepHeight.GetHashCode() * 12 +
                    VariableStandingHeight.GetHashCode() * 13 +
                    VariableStrafeDistance.GetHashCode() * 14 +
                    VariableStandingDistance.GetHashCode() * 15 +
                    ThighLength.GetHashCode() * 16 +
                    CalfLength.GetHashCode() * 17 +
                    AnkleLength.GetHashCode() * 18 +
                    AnimationSpeed.GetHashCode() * 19 +
                    CrouchSpeed.GetHashCode() * 20 +
                    VariableCrouchHeight.GetHashCode() * 21 + // rip order... whatever
                    VtolActive.GetHashCode() * 22
                ) % int.MaxValue;
            }

            public override string ToCustomDataString()
            {
                ini.Clear();
                ini.Set("Leg", "LegType", LegType);
                ini.SetComment("Leg", "LegType", "1 = Humanoid\n2 = Chicken walker\n3 = Spideroid\n4 = Crab\n5 = Digitigrade\n6 = Prismatic");

                ini.Set("Leg", "StandingHeight", VariableStandingHeight.ToString());
                /*ini.Set("Leg", "XOffset", XOffset);
                ini.Set("Leg", "YOffset", YOffset);
                ini.Set("Leg", "ZOffset", ZOffset);*/
                ini.Set("Leg", "StandingLean", VariableXOffset.ToString());//ini.Set("Leg", "XOffset", VariableXOffset.ToString());
                //ini.Set("Leg", "YOffset", VariableYOffset.ToString());
                ini.Set("Leg", "StandingWidth", VariableZOffset.ToString());//ini.Set("Leg", "ZOffset", VariableZOffset.ToString());

                // StrafeOffsets?

                //ini.Set("Leg", "StepLength", StepLength);
                //ini.Set("Leg", "StepHeight", StepHeight);
                ini.Set("Leg", "StepLength", VariableStepLength.ToString());
                ini.Set("Leg", "StepHeight", VariableStepHeight.ToString());
                //ini.Set("Leg", "StandingHeight", VariableStandingHeight.ToString());
                if (LegType > 2 && LegType < 6)
                    ini.Set("Leg", "StandingDistance", VariableStandingDistance.ToString());
                ini.Set("Leg", "StrafeWidth", VariableStrafeDistance.ToString());//ini.Set("Leg", "StrafeDistance", VariableStrafeDistance.ToString());
                ini.Set("Leg", "CrouchHeight", VariableCrouchHeight.ToString());
                //ini.SetComment("Leg", "StepLength", "How far forwards/backwards and up/down legs step\n0.5 is half, 1 is default, 2 is double");

                ini.Set("Leg", "WalkSpeed", AnimationSpeed);
                ini.Set("Leg", "CrouchSpeed", CrouchSpeed);
                //ini.Set("Leg", "IndependantStep", IndependantStep);
                //ini.SetComment("Leg", "WalkSpeed", "How fast legs walk and crouch");

                ini.Set("Leg", "HipOffsets", HipOffsets);
                ini.SetComment("Leg", "HipOffsets", "Advanced options, change at your discretion");
                //ini.SetComment("Leg", "HipOffsets", "The joints' offsets (in degrees)");
                ini.Set("Leg", "KneeOffsets", KneeOffsets);
                ini.Set("Leg", "FootOffsets", FootOffsets);
                if (LegType > 2)
                    ini.Set("Leg", "QuadOffsets", QuadOffsets);
                ini.Set("Leg", "StrafeOffsets", StrafeOffsets);

                ini.Set("Leg", "ThighLength", FromAutoFloat(ThighLength));
                ini.Set("Leg", "CalfLength", FromAutoFloat(CalfLength));
                if (LegType > 2 && LegType < 6)
                    ini.Set("Leg", "AnkleLength", FromAutoFloat(AnkleLength));
                //ini.SetComment("Leg", "ThighLength", "Change theoretical apendage lengths");

                ini.Set("Leg", "VtolActive", VtolActive);
                ini.SetSectionComment("Leg", $"Leg (group {Id}) settings. These change all of the joints in the same group.");
                return ini.ToString();
            }

            public static float? ToAutoFloat(string str)
            {
                if (string.IsNullOrEmpty(str) || str.Equals("auto", StringComparison.OrdinalIgnoreCase))
                    return null;
                float result;
                if (float.TryParse(str, out result))
                {
                    return result;
                }
                return null;
            }

            public static string FromAutoFloat(float? value)
            {
                if (value != null)
                    return value.Value.ToString();
                return "auto";
            }

            public static LegConfiguration Parse(MyIni ini)
            {
                LegConfiguration config = new LegConfiguration
                {
                    LegType = ini.Get("Leg", "LegType").ToInt32(1),

                    HipOffsets = ini.Get("Leg", "HipOffsets").ToDouble(0),
                    KneeOffsets = ini.Get("Leg", "KneeOffsets").ToDouble(0),
                    FootOffsets = ini.Get("Leg", "FootOffsets").ToDouble(0),
                    QuadOffsets = ini.Get("Leg", "QuadOffsets").ToDouble(0),
                    StrafeOffsets = ini.Get("Leg", "StrafeOffsets").ToDouble(0),
                    /*XOffset = ini.Get("Leg", "XOffset").ToDouble(0),
                    YOffset = ini.Get("Leg", "YOffset").ToDouble(0),
                    ZOffset = ini.Get("Leg", "ZOffset").ToDouble(0),*/
                    VariableXOffset = new JointVariable(ini.Get("Leg", "StandingLean").ToString("0%")),//new JointVariable(ini.Get("Leg", "XOffset").ToString("0%")),
                    //VariableYOffset = new JointVariable(ini.Get("Leg", "YOffset").ToString("0%")),
                    VariableZOffset = new JointVariable(ini.Get("Leg", "StandingWidth").ToString("0%")),//new JointVariable(ini.Get("Leg", "ZOffset").ToString("0%")),

                    /*HipsInverted = ini.Get("Leg", "HipsInverted").ToBoolean(),
                    KneesInverted = ini.Get("Leg", "KneesInverted").ToBoolean(),
                    FeetInverted = ini.Get("Leg", "FeetInverted").ToBoolean(),
                    QuadInverted = ini.Get("Leg", "QuadInverted").ToBoolean(),*/

                    ThighLength = ToAutoFloat(ini.Get("Leg", "ThighLength").ToString("auto")),
                    CalfLength = ToAutoFloat(ini.Get("Leg", "CalfLength").ToString("auto")),
                    AnkleLength = ToAutoFloat(ini.Get("Leg", "AnkleLength").ToString("auto")),

                    StepLength = ini.Get("Leg", "StepLength").ToDouble(1),
                    VariableStepLength = new JointVariable(ini.Get("Leg", "StepLength").ToString("45%")),
                    StepHeight = ini.Get("Leg", "StepHeight").ToDouble(1),
                    VariableStepHeight = new JointVariable(ini.Get("Leg", "StepHeight").ToString("20%")),
                    VariableStandingHeight = new JointVariable(ini.Get("Leg", "StandingHeight").ToString("90%")),
                    VariableStandingDistance = new JointVariable(ini.Get("Leg", "StandingDistance").ToString("75%")),
                    VariableStrafeDistance = new JointVariable(ini.Get("Leg", "StrafeWidth").ToString("25%")),//new JointVariable(ini.Get("Leg", "StrafeDistance").ToString("25%")),
                    VariableCrouchHeight = new JointVariable(ini.Get("Leg", "CrouchHeight").ToString("20%")),

                    AnimationSpeed = ini.Get("Leg", "WalkSpeed").ToDouble(1),
                    CrouchSpeed = ini.Get("Leg", "CrouchSpeed").ToDouble(1),
                    //IndependantStep = ini.Get("Leg", "IndependantStep").ToBoolean(false),

                    VtolActive = ini.Get("Leg", "VtolActive").ToBoolean(true),

                    defaultValue = 1
                };
                return config;
            }

            public static LegConfiguration Parse(string iniData)
            {
                ini = ini ?? new MyIni();
                ini.Clear();
                bool parsed = ini.TryParse(iniData);
                //if (!parsed)
                //    return null;
                return Parse(ini);
            }

            public static LegConfiguration Create()
            {
                return Parse("");
            }

            #endregion

        }
    }
}
