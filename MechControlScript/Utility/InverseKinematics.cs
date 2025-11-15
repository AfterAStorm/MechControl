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
            public static LegAngles CalculateLegOld(double thighLength, double calfLength, double x, double y)
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

            /// <summary>
            /// Calculate leg angles for a 2 jointed leg, such as humanoid
            /// </summary>
            /// <remarks>The quad angle is always returned as zero</remarks>
            /// <param name="thighLength"></param>
            /// <param name="calfLength"></param>
            /// <param name="x">x+ is right (forwards), x- is left (backwards)</param>
            /// <param name="y">y+ is down, y- is up</param>
            /// <returns></returns>
            public static LegAngles Calculate2Joint2D(double thighLength, double calfLength, double x, double y)
            {
                LegAngles angles = new LegAngles();

                // find difference from zero
                double distance2 = Math.Pow(x, 2) + Math.Pow(y, 2);
                double distance = Math.Sqrt(distance2);

                // get angle to point
                double atan = Math.Atan2(x, y); // inverse x so x+ is "forwards"

                // check if the length is too far for the joint to reach
                if (thighLength + calfLength < distance)
                {
                    angles.HipDegrees = atan.ToDegrees();
                    angles.FeetDegrees = -angles.HipDegrees;
                    return angles;
                }

                double thighLength2 = Math.Pow(thighLength, 2);
                double calfLength2 = Math.Pow(calfLength, 2);

                // calculate ratio and then angle
                // i pulled this from some source, it works!
                // something something inverse cos rule or whatever

                // edit: it uses the law of cosine to solve for gamma
                double cosAngle0 =
                    (distance2 + thighLength2 - calfLength2) / (2 * distance * thighLength);
                double angle0 = Math.Acos(cosAngle0);

                double cosAngle1 =
                    (calfLength2 + thighLength2 - distance2) / (2 * calfLength * thighLength);
                double angle1 = Math.Acos(cosAngle1);

                // assign values
                angles.HipDegrees = atan.ToDegrees() - angle0.ToDegrees();
                angles.KneeDegrees = 180 - angle1.ToDegrees();
                angles.FeetDegrees = -angles.HipDegrees - angles.KneeDegrees; // undo above

                return angles;
            }

            /// <summary>
            /// Calculate leg angles for a 3 jointed leg, such as spider
            /// It's secretly a sideways 2 jointed leg, :p
            /// </summary>
            /// <remarks>The quad angle is always returned as zero</remarks>
            /// <param name="thighLength"></param>
            /// <param name="calfLength"></param>
            /// <param name="x">x+ is right (forwards), x- is left (backwards)</param>
            /// <param name="y">y+ is down, y- is up</param>
            /// <param name="z">z</param>
            /// <returns></returns>
            public static LegAngles Calculate2Joint3D(double thighLength, double calfLength, double x, double y, double z, double zOffset=0)
            {

                // calculate extra y space because z move it
                // y is in/out
                // x is up/down
                // z is for/backward
                // we turn the y and z length into right triangle
                // and solve for hypotenuse
                // and take that minus y to get the extra length
                // and add that to our current y to offset it correctly
                //z *= Math.Cos(zOffset * Math.PI / 180);
                //double zRadians = zOffset.ToRadians();
                //z -= Math.Sin(zRadians) * Math.Sqrt(2) * 2;
                var side = Math.Sqrt(Math.Pow(y, 2) + Math.Pow(z, 2));
                //var remaining = side - y;
                //y += remaining;
                y = side;

                // y = y + (side - y)
                // y = side

                // cos(x) = adj/hyp
                // hypcos(x) = adj

                var angles = Calculate2Joint2D(thighLength, calfLength, x, y);

                // waterfall of angles
                angles.FeetDegrees = angles.KneeDegrees;
                angles.KneeDegrees = angles.HipDegrees;
                Log($"atan2: {z} / {y}");
                angles.HipDegrees = Math.Atan2(z, y).ToDegrees();// + zOffset;
                return angles;
            }

            /// <summary>
            /// Find the required distance where the knee is x radians
            /// </summary>
            /// <param name="kneeRadians"></param>
            /// <returns></returns>
            public static double FindDistanceWhereKnee(double thighLength, double calfLength, double kneeRadians)
            {
                // to solve:
                // top = calfLength^2 + thighLength^2 - x^2
                // bottom = 2 * calfLength * thighLength
                // pi - kneeRadians = cos^-1([top - x] / [bottom])
                // 

                // find top/bottom
                double topLeft = Math.Pow(calfLength, 2) + Math.Pow(thighLength, 2);
                double bottom = 2 * calfLength * thighLength;

                // find pi - kneeRadians
                double targetAngle = Math.PI - kneeRadians;

                // remove acos and use inverse + multiply bottom + get -x^2 by itself
                // [top - x^2] / [bottom] = cos(targetAngle)
                double right = -Math.Cos(targetAngle) * bottom + topLeft;
                double answer = Math.Sqrt(right);

                return answer;
            }
        }
    }
}
