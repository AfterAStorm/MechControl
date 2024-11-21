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
        public class InverseKinematics
        {
            public static LegAngles CalculateLeg(double thighLength, double calfLength, double x, double y)
            {
                double distance = Math.Sqrt(Math.Pow(x, 2) + Math.Pow(y, 2));

                double atan = Math.Atan2(x, y).ToDegrees();

                if (thighLength + calfLength < distance) // leg isn't long enough D:
                    return new LegAngles()
                    {
                        HipDegrees = atan,
                        KneeDegrees = 0,
                        FeetDegrees = 0
                    };

                double cosAngle0 =
                    (Math.Pow(distance, 2) + Math.Pow(thighLength, 2) - Math.Pow(calfLength, 2))
                    /
                    (2 * distance * thighLength);
                double angle0 = Math.Acos(cosAngle0).ToDegrees();

                double cosAngle1 =
                    (Math.Pow(calfLength, 2) + Math.Pow(thighLength, 2) - Math.Pow(distance, 2))
                    /
                    (2 * calfLength * thighLength);
                double angle1 = Math.Acos(cosAngle1).ToDegrees();

                return new LegAngles()
                {
                    HipDegrees = (atan - angle0),
                    KneeDegrees = (180 - angle1),
                    FeetDegrees = 0
                };
            }
        }
    }
}
