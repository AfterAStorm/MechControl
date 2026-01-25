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
        public abstract class JointGroup
        {
            public JointConfiguration Configuration = null;
            public List<FetchedBlock> AllBlocks = new List<FetchedBlock>();

            public List<Joint> AllJoints = new List<Joint>();

            public bool Enabled = true;

            public abstract void SetConfiguration(object config);
            public abstract void ApplyConfiguration();

            public void ToggleEnabled(bool enabled, bool set=true)
            {
                if (Enabled && !enabled)
                {
                    foreach (var joint in AllJoints)
                    {
                        joint.SetRPM(0);
                    }
                }
                if (set)
                    Enabled = enabled;
            }

            protected void AddAllBlock(FetchedBlock block)
            {
                AllBlocks.Add(block);
            }

            public virtual bool AddBlock(FetchedBlock block)
            {
                return false;
            }
        }
    }
}
