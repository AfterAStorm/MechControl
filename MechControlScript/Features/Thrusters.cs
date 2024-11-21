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
        List<IMyThrust> thrusters = new List<IMyThrust>();

        bool thrustersEnabled = true;

        public void FetchThrusters()
        {
            thrusters.Clear();
            thrusters.AddRange(BlockFinder.GetBlocksOfType<IMyThrust>()
                .Select(BlockFetcher.ParseBlock)
                .Where(f => f.HasValue)
                .Select(f => f.Value)
                .Where(f => f.Type == BlockType.Thruster)
                .Select(f => f.Block as IMyThrust));
        }

        public void UpdateThrusters()
        {
            foreach (IMyThrust thruster in thrusters)
            {
                thruster.ThrustOverridePercentage = (moveInput.Y > 0 && ThrusterBehavior == ThrusterMode.Override) ? 1 : 0;
                thruster.Enabled = thrustersEnabled && ThrusterBehavior == ThrusterMode.Override ? moveInput.Y > 0 : thruster.Enabled; //thrustersEnabled && (moveInput.Y > 0 || ThrusterBehavior == ThrusterMode.Hover);
            }
        }
    }
}
