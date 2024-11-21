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
        public struct ArmJointConfiguration
        {
            public static readonly ArmJointConfiguration DEFAULT = new ArmJointConfiguration()
            {
                Inversed = false,
                Offset = 0,
                Multiplier = 1
            };

            public bool Inversed;
            public double Offset;
            public double Multiplier;
            public double InversedMultiplier => Inversed ? -1 : 1;
            private string Name;

            public static ArmJointConfiguration Parse(FetchedBlock block)
            {
                MyIni ini = new MyIni();
                ini.TryParse(block.Block.CustomData, "Joint");
                return new ArmJointConfiguration()
                {
                    Name = block.Block.CustomName,
                    Inversed = block.Inverted,
                    Offset = ini.Get("Joint", "Offset").ToDouble(0),
                    Multiplier = ini.Get("Joint", "Multiplier").ToDouble(1)
                };
            }

            public string ToCustomDataString()
            {
                MyIni ini = new MyIni();
                ini.Set("Joint", "Offset", Offset);
                ini.SetComment("Joint", "Offset", "Specifies where the joint's \"zero\" is");
                ini.Set("Joint", "Multiplier", Multiplier);
                ini.SetComment("Joint", "Multiplier", "How much movement affects this stator");

                ini.SetSectionComment("Joint", $"Joint ({Name}) settings. Only this block will be affected.");
                return ini.ToString();
            }
        }
    }
}
