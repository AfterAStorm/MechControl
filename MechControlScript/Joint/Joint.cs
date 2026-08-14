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

            public float Minimum => Stator.LowerLimitDeg;
            public float Maximum => Stator.UpperLimitDeg;
            public float MinimumRad => Stator.LowerLimitRad;
            public float MaximumRad => Stator.UpperLimitRad;
            public bool IsHinge => Stator.BlockDefinition.SubtypeName.Contains("Hinge");
            public bool IsRotor => !IsHinge;

            public float InvertedMultiplier => Source.Inverted ? -1f : 1f;

            public FetchedBlock Source { get; private set; }

            public Joint(FetchedBlock block)
            {
                Source = block;
                Stator = block.Block as IMyMotorStator;
            }

            public double ClampDegrees(double angle)
            {
                double current = Stator.Angle.ToDegrees();
                if (IsHinge)
                    return angle.ClampHinge() - current; // lock between -90 to 90; aka angle = angle - current
                else
                {
                    double min = Stator.LowerLimitDeg;
                    double max = Stator.UpperLimitDeg;
                    if (min < -360.5d && max > 360.5d)
                    {
                        return (angle.Modulo(360) - current + 540).Modulo(360) - 180;
                    }
                    //double directDirection = angle - current;
                    //double closestDirection = (angle.Modulo(360) - current + 540).Modulo(360) - 180; // find the closest direction to the target angle; thank you https://math.stackexchange.com/a/2898118 :D*/

                    double delta_cw, delta_ccw;
                    double dir = DetermineDirectionLimits(current, angle, min, max, out delta_cw, out delta_ccw);
                    double chosen_delta = dir > 0d ? delta_cw : delta_ccw;

                    double predicted = current + chosen_delta;
                    if (predicted < min)
                        return min - current;
                    else if (predicted > max)
                        return max - current;
                    return chosen_delta;

                    /*double delta = 0;
                    if (Math.Abs(directDirection - closestDirection) > 0.02f)
                    {
                        double predictedPos = current + closestDirection;
                        if (predictedPos < min || predictedPos > max)
                        {
                            delta = directDirection;
                        }
                        else
                            delta = closestDirection;
                    }
                    else
                    {
                        delta = directDirection;
                    }

                    double predictedFinal = current + delta;
                    if (predictedFinal < min)
                        return min - current;
                    else if (predictedFinal > max)
                        return max - current;*/
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
                    //return predictedFinal;
                }
            }

            public override bool Equals(object obj)
            {
                if (!(obj is Joint))
                    return false;
                return Stator.Equals((obj as Joint)?.Stator);
            }

            public override int GetHashCode()
            {
                return Stator.GetHashCode();
            }

            public float GetRPMFor(double angle)
            {
                float angleDifference = (float)ClampDegrees(angle);

                // we want pos = pos + (target - pos)
                // but reality grants pos + dt(target - pos)
                // so we counter it with pos + dt(target-pos)/dt
                // and we convert from deg/s to rpm which is division by 6

                return angleDifference; // (float)(moveInfo.Delta * 6f); // * 6f to account for RPM
                /*angle =*/ //return (float)ClampDegrees(angle);

                //return (float)angle.Clamp(-MaxRPM, MaxRPM);
            }

            public void SetRPM(float rotationsPerMinute)
            {
                if (float.IsNaN(rotationsPerMinute) || float.IsInfinity(rotationsPerMinute))
                    rotationsPerMinute = 0;
                Stator.TargetVelocityRPM = rotationsPerMinute;// * .9f;
            }

            //const float Kp2 = 10f;
            //const float SMOOTHING_STEPS = 5f;

            public void SetAngle(double angleDegrees, double speedMultiplier=1)
            {
                var delta = GetRPMFor(angleDegrees);
                if (Math.Abs(delta) < 0.01f)
                {
                    SetRPM(0);
                    return;
                }
                SetRPM(delta * (float)speedMultiplier);
                //SetRPM(delta / ((float)moveInfo.Delta * 6f * SMOOTHING_STEPS) * (float)speedMultiplier);
                //Stator.RotateToAngle(MyRotationDirection.AUTO, (float)angle, float.MaxValue); // too snappy and doesn't allow for compensation for overshooting; limits are also wacky
                //Stator.RotorLock = (Stator.Angle - ClampDegrees(angle)).Absolute() < 2d;
            }

            public double Kp = 1;//10;
            public double Ki = 0;
            public double Kd = 0;//2;

            private double _integral = 0;
            private double _lastError = double.NegativeInfinity;

            public void SetAnglePID(double angleDegrees)
            {
                var delta = GetRPMFor(angleDegrees).ToRadians();
                if (Math.Abs(delta) < 0.01f)
                {
                    SetRPM(0);
                    _integral = 0;
                    _lastError = 0;
                    return;
                }

                double dt = 1 / 60d;
                double error = delta;
                if (_lastError == double.NegativeInfinity)
                {
                    _lastError = error;
                }
                double p = Kp * error;
                _integral += delta * dt;
                _integral = MathHelper.Clamp(_integral, -10d, 10d);
                double i = Ki * _integral;
                double errorRate = (error - _lastError) / dt;
                double d = Kd * errorRate;

                _lastError = error;
                Stator.TargetVelocityRad = (float)(p + i + d);
            }
        }
    }
}
