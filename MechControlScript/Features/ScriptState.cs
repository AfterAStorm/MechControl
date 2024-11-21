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
        public class ScriptState
        {
            public string Serialize()
            {
                MyIni serializer = new MyIni();
                serializer.Clear();
                serializer.AddSection("State");
                serializer.Set("State", "crouched", crouchOverride);
                return serializer.ToString();
            }

            public void Parse(string ini)
            {
                MyIni serializer = new MyIni();
                serializer.Clear();
                serializer.TryParse(ini);
                crouchOverride = serializer.Get("State", "crouched").ToBoolean(false);
            }
        }
    }
}
