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
    public static class AnimationEnumExtensions
    {
        internal static bool IsIdle(this Program.Animation animation) => animation == Program.Animation.Idle || animation == Program.Animation.Crouch;
        internal static bool IsWalk(this Program.Animation animation) => animation == Program.Animation.Walk || animation == Program.Animation.CrouchWalk;
        internal static bool IsCrouch(this Program.Animation animation) => animation == Program.Animation.Crouch || animation == Program.Animation.CrouchWalk || animation == Program.Animation.CrouchTurn;
        internal static bool IsTurn(this Program.Animation animation) => animation == Program.Animation.Turn || animation == Program.Animation.CrouchTurn;
    }
}
