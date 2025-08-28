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
        /// <summary>
        /// Structure containing leg angles
        /// </summary>
        public struct LegAngles
        {
            public static LegAngles Zero = new LegAngles(0, 0, 0, 0, 0);
            public static LegAngles One = new LegAngles(1, 1, 1, 1, 1);
            public static LegAngles MinusOne = new LegAngles(-1, -1, -1, -1, -1);

            public double HipDegrees;
            public double KneeDegrees;
            public double FeetDegrees;
            public double QuadDegrees;
            public double StrafeDegrees;

            public double HipRadians => HipDegrees.ToRadians();
            public double KneeRadians => KneeDegrees.ToRadians();
            public double FeetRadians => FeetDegrees.ToRadians();
            public double QuadRadians => QuadDegrees.ToRadians();
            public double StrafeRadians => StrafeDegrees.ToRadians();

            public LegAngles(double hip, double knee, double feet, double quad, double strafe = 0)
            {
                HipDegrees = hip;
                KneeDegrees = knee;
                FeetDegrees = feet;
                QuadDegrees = quad;
                StrafeDegrees = strafe;
            }

            public LegAngles(double hip, double knee, double feet)
            {
                HipDegrees = hip;
                KneeDegrees = knee;
                FeetDegrees = feet;
                QuadDegrees = 0;
                StrafeDegrees = 0;
            }

            public void Shift()
            {
                QuadDegrees = FeetDegrees;
                FeetDegrees = KneeDegrees;
                KneeDegrees = HipDegrees;
                HipDegrees = 0;
            }

            public static LegAngles operator +(LegAngles left, LegAngles right) => new LegAngles(left.HipDegrees + right.HipDegrees, left.KneeDegrees + right.KneeDegrees, left.FeetDegrees + right.FeetDegrees, left.QuadDegrees + right.QuadDegrees, left.StrafeDegrees + right.StrafeDegrees);
            public static LegAngles operator *(LegAngles left, LegAngles right) => new LegAngles(left.HipDegrees * right.HipDegrees, left.KneeDegrees * right.KneeDegrees, left.FeetDegrees * right.FeetDegrees, left.QuadDegrees * right.QuadDegrees, left.StrafeDegrees * right.StrafeDegrees);
        }
    }
}
