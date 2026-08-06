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
        /// Map [A,B] to [C,D] of x
        /// </summary>
        /// <param name="x"></param>
        /// <param name="r1_min"></param>
        /// <param name="r1_max"></param>
        /// <param name="r2_min"></param>
        /// <param name="r2_max"></param>
        /// <returns></returns>
        static double MapRange(double x, double r1_min, double r1_max, double r2_min, double r2_max)
        {
            return (x - r1_min) * (r2_max - r2_min) / (r1_max - r1_min) + r2_min;
        }

        /// <summary>
        /// Approximate a raycast normal (since ingame-scripts don't have access to those)
        /// </summary>
        /// <param name="a"></param>
        /// <param name="b"></param>
        /// <param name="up"></param>
        /// <returns></returns>
        /*static Vector3D ApproximateNormal(Vector3D a, Vector3D b, Vector3D up)
        {
            Singleton.buildTools.DrawPoint(a, Color.Red, 0.03f, 0.2f);
            Singleton.buildTools.DrawPoint(b, Color.Blue, 0.03f, 0.2f);

            Vector3D dir = Vector3D.Normalize(b - a);
            Vector3D upProjected = up - dir * Vector3D.Dot(up, dir);
            if (upProjected.LengthSquared() < 1e-6)
            {
                upProjected = Vector3D.CalculatePerpendicularVector(dir);
            }
            upProjected.Normalize();

            Vector3D right = Vector3D.Cross(upProjected, dir);
            right.Normalize();
            Vector3D normal = Vector3D.Cross(dir, right);
            normal.Normalize();

            Singleton.buildTools.DrawVector((a + b) / 2, (a + b) / 2 + normal, Color.Green, 0.03f, 0.2f);

            return normal;
        }*/

        /// <summary>
        /// Approximate a raycast normal (since ingame-scripts don't have access to those)
        /// </summary>
        /// <param name="a"></param
        /// <param name="b"></param>
        /// <param name="c"></param>
        /// <param name="up"></param>
        /// <returns>The approximated normal</returns>
        static Vector3D ApproximateNormal3(Vector3D a, Vector3D b, Vector3D c, Vector3D up)
        {
            Singleton.buildTools.DrawPoint(a, Color.Red, 0.03f, 0.2f);
            Singleton.buildTools.DrawPoint(b, Color.Green, 0.03f, 0.2f);
            Singleton.buildTools.DrawPoint(c, Color.Blue, 0.03f, 0.2f);
            /*Vector3 origA = a;

            a.Normalize();
            b.Normalize();

            Vector3D normal = Vector3D.Cross(a, b);
            normal.Normalize();

            if (Vector3D.Dot(normal, up) < 0)
                normal *= -1;

            */

            Vector3D normal = Vector3D.Normalize(Vector3D.Cross(b - a, c - a));
            if (Vector3D.Dot(normal, up) < 0)
            {
                normal *= -1f;
            }

            Singleton.buildTools.DrawVector((a + b + c) / 3f, (a + b + c) / 3f + normal, Color.Green, 0.03f, 0.2f);

            return normal;
        }

        public static double DetermineDirectionLimits(double current, double target, double min, double max, out double delta_cw, out double delta_ccw)
        {
            // 1. Calculate the raw shortest distances
            double normCurrent = (current % 360d + 360d) % 360d;
            double normTarget = (target % 360d + 360d) % 360d;

            delta_cw = (normTarget - normCurrent) % 360d;
            if (delta_cw < 0d)
                delta_cw += 360d;

            if (Math.Abs(delta_cw) < 1e-5 || Math.Abs(delta_cw - 360d) < 1e-5)
            {
                delta_cw = 0d;
                delta_ccw = 0d;
                return 0d;
            }

            delta_ccw = delta_cw - 360d;

            // --- THE FIX ---
            // If the engine reports -30 as 330, unwrap it back to -30 BEFORE checking limits.
            bool hasMin = min >= -360.5d;
            bool hasMax = max <= 360.5d;

            double physical_current = current;
            if (hasMin && hasMax)
            {
                // If current is technically out of bounds high, but subtracting 360 puts it safely in bounds
                if (physical_current > max && (physical_current - 360d) >= min)
                    physical_current -= 360d;
                // If current is technically out of bounds low, but adding 360 puts it safely in bounds
                else if (physical_current < min && (physical_current + 360d) <= max)
                    physical_current += 360d;
            }

            // 2. Predict using the phase-corrected physical angle
            double predicted_cw = physical_current + delta_cw;
            double predicted_ccw = physical_current + delta_ccw;

            // 3. Check against limits
            const double EPSILON = 1e-4;
            bool cw_blocked = hasMax && (predicted_cw > max + EPSILON);
            bool ccw_blocked = hasMin && (predicted_ccw < min - EPSILON);

            // 4. Decide Direction
            if (cw_blocked == ccw_blocked)
                return Math.Abs(delta_cw) <= Math.Abs(delta_ccw) ? 1d : -1d;

            if (cw_blocked)
                return -1d;

            return 1d;
        }

        public static double DetermineDirectionLimits1(double current, double target, double min, double max, out double delta_cw, out double delta_ccw)
        {
            // 360 - 89.9
            current = (current % 360d + 360d) % 360d;
            target = (target % 360d + 360d) % 360d;

            bool hasMin = min >= -360.5d;
            bool hasMax = max <= 360.5d;

            // "fix" min/max relational to target
            // more of a very weak piece of duct tape
            // but it gets the job done!
            if (hasMin && Math.Abs(target - min) < 0.03d)
            {
                target = min + 0.01d;
            }
            if (hasMax && Math.Abs(target - max) < 0.03d)
            {
                target = max - 0.01d;
            }

            delta_cw = (target - current) % 360d;
            if (delta_cw < 0d)
                delta_cw += 360d;

            if (Math.Abs(target - current) < 1e-5 || Math.Abs(delta_cw) < 1e-5) {
                delta_cw = 0d;
                delta_ccw = 0d;
                return 0d;
            }

            delta_ccw = delta_cw - 360d;

            double predicted_cw = current + delta_cw;
            double predicted_ccw = current + delta_ccw;

            if (!hasMin && !hasMax)
                return Math.Abs(delta_cw) <= Math.Abs(delta_ccw) ? 1d : -1d;

            bool cw_blocked = false;
            bool ccw_blocked = false;
            if (hasMin && hasMax)
            {

                double deadzoneStart = max.Modulo(360d);
                double deadzoneEnd = min.Modulo(360d);

                double relDeadStartCW = (deadzoneStart - current + 360) % 360d;
                double relDeadEndCW = (deadzoneEnd - current + 360) % 360d;
                if (relDeadStartCW < relDeadEndCW)
                    cw_blocked = (relDeadEndCW < delta_cw);
                else
                    cw_blocked = (relDeadStartCW < delta_cw || relDeadEndCW > delta_cw);

                double abs_ccw = Math.Abs(delta_ccw);
                double relDeadStartCCW = (current - deadzoneEnd + 360) % 360d;
                double relDeadEndCCW = (current - deadzoneStart + 360) % 360d;
                if (relDeadStartCCW < relDeadEndCCW)
                    ccw_blocked = (relDeadEndCCW < delta_ccw);
                else
                    ccw_blocked = (relDeadStartCCW < abs_ccw || relDeadEndCCW > abs_ccw);
            }
            else if (hasMin)
            {
                double relMin = min.Modulo(360d);

                double distToMin = (current - relMin + 360d) % 360d;
                ccw_blocked = Math.Abs(delta_ccw) >= distToMin;
            }
            else if (hasMax)
            {
                double relMax = max.Modulo(360d);

                double distToMax = (relMax - current + 360d) % 360d;
                cw_blocked = delta_cw >= distToMax;
            }

            if (cw_blocked && ccw_blocked)
                return Math.Abs(delta_cw) <= Math.Abs(delta_ccw) ? 1d : -1d; //target > current ? 1d : -1d; // both blocked, guesstimate
            if (cw_blocked) return -1d; // cw blocked
            if (ccw_blocked) return 1d; // ccw blocked
            return Math.Abs(delta_cw) <= Math.Abs(delta_ccw) ? 1d : -1d; // pick shortest
        }
    }
}
