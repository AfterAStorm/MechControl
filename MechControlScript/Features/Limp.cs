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
        static bool isLimp = false;

        void ToggleLimp(bool limp)
        {
            isLimp = limp;
            foreach (var group in legs.Values)
                group.AllBlocks.ForEach(b =>
                {
                    if (b.Block is IMyFunctionalBlock)
                        (b.Block as IMyFunctionalBlock).Enabled = !limp;
                });
        }
    }
}
