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
        // TODO: this is poorly organized, all logic should be handled here!
        // TODO: although technically the legs should handle it themself, since it's globally manipulatable, it's a "feature"!
        static bool magnetsEnabled = true;

        void ToggleMagnetsEnabled(bool enabled)
        {
            if (magnetsEnabled && !enabled)
                foreach (var group in legs.Values)
                {
                    var trigroup = group as TriLegGroup;
                    if (trigroup != null)
                    {
                        foreach (var mag in trigroup.LeftMagnets.Concat(trigroup.RightMagnets))
                        {
                            mag.AutoLock = false;
                            mag.Unlock();
                        }
                    }
                }
            magnetsEnabled = enabled;
        }
    }
}
