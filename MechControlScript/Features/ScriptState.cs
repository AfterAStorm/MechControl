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
using static IngameScript.Program;

namespace IngameScript
{
    partial class Program
    {
        struct StateCheckpoint
        {
            public string Name;
            public Func<Program, object> Getter;
            public Action<Program, MyIniValue> Setter;
        }

        public class ScriptState // don't ask me why this is a class and not just a part of Program... ssshhhh
        {
            private readonly List<StateCheckpoint> checkpoints = new List<StateCheckpoint>()
            {
                new StateCheckpoint()
                {
                    Name = "crouchOverride",
                    Getter = (p) => p.crouchOverride,
                    Setter = (p, v) => p.crouchOverride = v.ToBoolean(p.crouchOverride)
                },
                new StateCheckpoint()
                {
                    Name = "thrustersEnabled",
                    Getter = (p) => p.thrustersEnabled,
                    Setter = (p, v) => p.thrustersEnabled = v.ToBoolean(p.thrustersEnabled)
                },
                new StateCheckpoint()
                {
                    Name = "thrustersMode",
                    Getter = (p) => (int)p.thrusterBehavior,
                    Setter = (p, v) => p.thrusterBehavior = (ThrusterMode)v.ToInt32((int)p.thrusterBehavior)
                },
                new StateCheckpoint()
                {
                    Name = "vtolEnabled",
                    Getter = (p) => p.thrustersVtol,
                    Setter = (p, v) => p.thrustersVtol = v.ToBoolean(p.thrustersVtol)
                },
                new StateCheckpoint()
                {
                    Name = "legsEnabled",
                    Getter = (p) => p.legsEnabled,
                    Setter = (p, v) => p.legsEnabled = v.ToBoolean(p.legsEnabled)
                },
                new StateCheckpoint()
                {
                    Name = "armsEnabled",
                    Getter = (p) => p.armsEnabled,
                    Setter = (p, v) => p.armsEnabled = v.ToBoolean(p.armsEnabled)
                },
                new StateCheckpoint()
                {
                    Name = "stabilizationEnabled",
                    Getter = (p) => p.stabilizationEnabled,
                    Setter = (p, v) => p.stabilizationEnabled = v.ToBoolean(p.stabilizationEnabled)
                },
            };

            private readonly Program program;

            public ScriptState(Program program)
            {
                this.program = program;
            }

            public string Serialize()
            {
                MyIni serializer = new MyIni();
                serializer.Clear();
                serializer.AddSection("State");
                foreach (var c in checkpoints)
                {
                    var value = c.Getter(program);
                    if (value is bool)
                        serializer.Set("State", c.Name, (bool)value);
                    else if (value is int)
                        serializer.Set("State", c.Name, (int)value);
                    else
                        Log("unsupported state type");
                }
                //serializer.Set("State", "crouched", crouchOverride);
                //serializer.Set("State", "thrustersEnabled", thrustersEnabled);
                //serializer.Set("State", "thrustersMode", (int)thrusterBehavior);
                return serializer.ToString();
            }

            public void Parse(string ini)
            {
                MyIni serializer = new MyIni();
                serializer.Clear();
                serializer.TryParse(ini);
                foreach (var c in checkpoints)
                    c.Setter(program, serializer.Get("State", c.Name));
                //crouchOverride = serializer.Get("State", "crouched").ToBoolean(false);
                //thrustersEnabled = serializer.Get("State", "thrustersEnabled").ToBoolean(false);
                //thrusterBehavior = (ThrusterMode)serializer.Get("State", "thrustersMode").ToInt32((int)thrusterBehavior);
            }
        }
    }
}
