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
        // Probably should've been a [Flags] but then the elegant switch statement wouldn't work so ¯\_(ツ)_/¯
        public enum Animation
        {
            Idle,
            Walk,
            Turn,
            CrouchTurn,
            Crouch,
            CrouchWalk,
            Strafe,
            Flight,

            Force,
        }
    }
}
