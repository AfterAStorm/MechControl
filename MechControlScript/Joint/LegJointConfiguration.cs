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
        public struct LegJointConfiguration
        {
            public static readonly LegJointConfiguration DEFAULT = new LegJointConfiguration()
            {
                Inversed = false,
                Offset = 0,
            };

            public bool Inversed;
            public double Offset;
            public double InversedMultiplier => Inversed ? -1 : 1;
            public JointVariable InfluenceVariable;
            public double Influence => InfluenceVariable.GetMetersOf(1f, 0f, 1f);
            private string Name;

            public static LegJointConfiguration Parse(FetchedBlock block)
            {
                var ini = block.Ini;
                return new LegJointConfiguration()
                {
                    Name = block.Block.CustomName,
                    Inversed = block.Inverted,
                    Offset = ini.Get("Joint", "Offset").ToDouble(0),
                    InfluenceVariable = new JointVariable(ini.Get("Joint", "Influence").ToString("100%"))
                };
            }

            public void Save(MyIni ini)
            {
                //ini.DeleteSection("Joint");
                ini.Set("Joint", "Offset", Offset);
                ini.SetComment("Joint", "Offset", "Specifies where the joint's \"zero\" is");
                ini.Set("Joint", "Influence", InfluenceVariable.ToString());
                ini.SetComment("Joint", "Influence", "How much movement affects this stator");

                ini.SetSectionComment("Joint", $"Joint ({Name}) settings. Only this block will be affected.");
                //return ini.ToString();
            }
        }
    }
}
