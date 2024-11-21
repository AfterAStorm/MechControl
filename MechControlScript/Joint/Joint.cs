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
        public class Joint
        {
            public IMyMotorStator Stator;

            public double Minimum => Stator.LowerLimitDeg;
            public double Maximum => Stator.UpperLimitDeg;
            public bool IsHinge => Stator.BlockDefinition.SubtypeName.Contains("Hinge");
            public bool IsRotor => !IsHinge;

            public Joint(FetchedBlock block)
            {
                Stator = block.Block as IMyMotorStator;
            }

            public double ClampDegrees(double angle)
            {
                double current = Stator.Angle.ToDegrees();
                if (IsHinge)
                    return angle.ClampHinge() - current; // lock between -90 to 90; aka angle = angle - current
                else
                {
                    double closestDirection = (angle.Modulo(360) - current + 540).Modulo(360) - 180; // find the closest direction to the target angle; thank you https://math.stackexchange.com/a/2898118 :D*/
                    // ^\ does require the more correct Modulo (in my opinion) that accounts for negative numbers
                    // check min/max
                    /*double min = Stator.LowerLimitDeg;
                    double max = Stator.UpperLimitDeg;
                    if (Math.Abs(min) != float.MaxValue && current + closestDirection >= min && closestDirection < 0)
                    {
                        return -closestDirection; // turn back to positive, to go the other way
                    }
                    else if (Math.Abs(max) != float.MaxValue && current + closestDirection <= max && closestDirection > 0)
                    {
                        return -closestDirection; // turn back to negative, to go the other way
                    }*/
                    return closestDirection;
                }
            }

            public float GetRPMFor(double angle)
            {
                angle = ClampDegrees(angle);

                return (float)angle.Clamp(-MaxRPM, MaxRPM);
            }

            public void SetRPM(float rotationsPerMinute)
            {
                Stator.TargetVelocityRPM = rotationsPerMinute;// * .9f;
            }

            public void SetAngle(double angle)
            {
                SetRPM(GetRPMFor(angle));
                //Stator.RotorLock = (Stator.Angle - ClampDegrees(angle)).Absolute() < 2d;
            }
        }
    }
}
