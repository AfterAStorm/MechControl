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

            public JointVariable(string value, bool fromMultiplier=false)
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
                    case "m":
                        Type = JointVariableType.Meters;
                        break;
                    default:
                        value += ' '; // add suffix so remaining parses correctly
                        if (fromMultiplier)
                        {
                            Type = JointVariableType.Percentage; // handled after parse
                        }
                        else
                        {
                            Type = JointVariableType.Meters;
                        }
                        break;
                }
                string remaining = suffix.All(char.IsDigit) ? value : value.Substring(0, value.Length - 1);
                if (!float.TryParse(remaining, out Value))
                {
                    Value = 0;
                }
                if (suffix != "%" && Type == JointVariableType.Percentage && fromMultiplier)
                {
                    Value *= 100f;
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
    }
}
