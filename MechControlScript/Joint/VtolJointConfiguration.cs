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
        public struct VtolJointConfiguration
        {
            public static readonly VtolJointConfiguration DEFAULT = new VtolJointConfiguration()
            {
                Offset = 0,
                Multiplier = 1
            };

            public double Offset;
            public double Multiplier;
            private string Name;

            public static VtolJointConfiguration Parse(FetchedBlock block)
            {
                MyIni ini = new MyIni();
                ini.TryParse(block.Block.CustomData, "Joint");
                return new VtolJointConfiguration()
                {
                    Name = block.Block.CustomName,
                    Offset = ini.Get("Joint", "VtolOffset").ToDouble(0),
                    Multiplier = ini.Get("Joint", "Multiplier").ToDouble(1)
                };
            }

            public void Save(MyIni ini)
            {
                ini.Set("Joint", "VtolOffset", Offset);
                ini.SetComment("Joint", "VtolOffset", "Specifies where the joint's \"zero\" is (for vtol)");
                ini.Set("Joint", "Multiplier", Multiplier);
                ini.SetComment("Joint", "Multiplier", "Specifies a speed multiplier for this stator");

                ini.SetSectionComment("Joint", $"Joint ({Name}) settings. Only this block will be affected.");
            }
        }
    }
}
