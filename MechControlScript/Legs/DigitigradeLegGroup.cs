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
        public class DigitigradeLegGroup : LegGroup
        {
            protected override LegAngles CalculateAngles(double step)
            {
                step *= -1;
                step = step.Modulo(1);
                bool crouch = Animation == Animation.Crouch || Animation == Animation.CrouchWalk;

                // shamelessly borrowed from https://opentextbooks.clemson.edu/wangrobotics/chapter/inverse-kinematics/
                // the equations anyway
                // x and y are flipped, yay!

                // Should cache this, but pff
                double thighLength = Configuration.ThighLength >= 0 ? Configuration.ThighLength :
                    (LeftHipStators.Count > 0 && LeftKneeStators.Count > 0 ? Vector3.Distance(LeftHipStators[0].Stator.GetPosition(), LeftKneeStators[0].Stator.GetPosition()) :
                    (RightHipStators.Count > 0 && RightKneeStators.Count > 0 ? Vector3.Distance(RightHipStators[0].Stator.GetPosition(), RightKneeStators[0].Stator.GetPosition()) : 0));//2.5f;
                double calfLength = Configuration.CalfLength >= 0 ? Configuration.CalfLength :
                    (LeftFootStators.Count > 0 && LeftKneeStators.Count > 0 ? Vector3.Distance(LeftFootStators[0].Stator.GetPosition(), LeftKneeStators[0].Stator.GetPosition()) :
                    (RightFootStators.Count > 0 && RightKneeStators.Count > 0 ? Vector3.Distance(RightFootStators[0].Stator.GetPosition(), RightKneeStators[0].Stator.GetPosition()) : 0));//2.5f;

                double maxDistance = (thighLength + calfLength);
                //Log($"Lengths: {thighLength} {calfLength}");

                // with sin, 0-2 is 0 to 1 to 0, while 2-4 is 0 to -1 to 0
                //        /\        .     1
                //       /  \      .
                // _____/____\____.__     0
                //     .      \  /
                //    .        \/        -1
                //      0  1  2 3 4

                // with cos, 0-2 is 1 to 0 to -1, while 2-4 is -1 to 0 to 1
                //     .\        /.       1
                //    .  \      /  .
                //  _.____\____/___ .     0
                //  .      \  /      .
                // .        \/        .  -1
                //      0 1  2 3 4

                // Instead of converting (step / 4 * 360) to radians, we just multiply the number of radians instead! It's TECHNICALLY more accurate too (the output of Sin/Cos at least)!
                double stepPercent = step;
                double sin = Math.Sin(stepPercent * 2 * Math.PI);
                double cos = Math.Cos(stepPercent * 2 * Math.PI);

                /*double x = sin * 1.5d * Configuration.StepLengthMultiplier + (Animation == Animation.Walk || Animation == Animation.CrouchWalk ? AccelerationLean : StandingLean);
                double y = MathHelper.Clamp(((maxDistance) - cos * (maxDistance) + StandingHeight - (crouch ? 1 : 0)), double.MinValue, 3d + StandingHeight - (crouch ? 1 : 0));*/

                double crouchAdditive = -MathHelper.Clamp(CrouchWaitTime, 0, .5);//crouch ? -MathHelper.Clamp(CrouchWaitTime, 0, .9) : 0; //-.9d : 0;
                Log($"crouchAdditive: {crouchAdditive}; isTurn: {Animation == Animation.Turn}");

                double stepHeight = Configuration.StepHeight - .6d;
                double standingHeight = StandingHeight - .35d;

                double lean = (Animation == Animation.Walk || Animation == Animation.CrouchWalk ? AccelerationLean : StandingLean) + .5;
                double x =
                    -sin * Configuration.StepLength + lean;
                double y = MathHelper.Clamp(
                    // value
                    cos * (1) + .5 + 2 + standingHeight + 1.5 + .25,

                    // min/max
                    0,
                    2 + stepHeight + standingHeight + 1 + .25
                ) + crouchAdditive;

                if (Animation == Animation.Turn) // makes math above redudant, but rarely used so it's fine!
                {
                    x = lean;
                    y = MathHelper.Clamp(
                        cos * (stepHeight + 0.5d) + 2 + .5 + standingHeight + 1,
                        0,
                        2 + stepHeight + standingHeight + 1);
                    //y = MathHelper.Clamp(sin, double.MinValue, 0) * (maxDistance) + maxDistance;
                }

                if (jumping && !crouch)
                {
                    x = lean;
                    y = thighLength + calfLength;
                }

                Log($"xy: {x}, {y}, {maxDistance};;;{standingHeight}");

                return InverseKinematics.CalculateLeg(thighLength, calfLength, x, y);

                /*double targetY = Math.Sin((stepPercent * 360).ToRadians()) * 1.5f * Configuration.StepLengthMultiplier - 1; // x  //Math.Sin((AnimationStep / 4 * 360).ToRadians()) * 3;
                double targetX = (MathHelper.Clamp(3 - Math.Cos((stepPercent * 360).ToRadians()) * 2f, float.MinValue, 2.5f) + 1 - (crouch ? 2 : 0)) * StandingHeight; // y  //Math.Cos((AnimationStep / 4 * 360).ToRadians()) * 3;
                if (Animation == Animation.Turn) // makes math above redudant, but rarely used so it's fine!
                {
                    targetY = -1;
                    targetX = MathHelper.Clamp(Math.Sin((stepPercent * 360).ToRadians()), double.MinValue, 0) * 1.5f + 3;
                }

                Log(targetY, targetX);
                double distance = Math.Sqrt(
                        Math.Pow(targetX - 0, 2) +
                        Math.Pow(targetY - 0, 2)
                );
                distance = MathHelper.Clamp(distance, 0, maxDistance);

                double diameter = Math.Atan(targetY / targetX);
                double leftDiameter = Math.Acos(
                    (Math.Pow(thighLength, 2) + Math.Pow(distance, 2) - Math.Pow(calfLength, 2))
                    /
                    (2 * thighLength * distance)
                );
                double hipAngle = diameter - leftDiameter;

                double diameter2 = Math.Acos(
                    (Math.Pow(thighLength, 2) + Math.Pow(calfLength, 2) - Math.Pow(distance, 2))
                    /
                    (2 * thighLength * calfLength)
                );
                double kneeAngle = Math.PI - diameter2;

                double footDeg =
                    (180 - hipAngle.ToDegrees() - kneeAngle.ToDegrees());

                return new LegAngles() { 
                    HipDegrees = hipAngle.ToDegrees(),
                    KneeDegrees = kneeAngle.ToDegrees(),
                    FeetDegrees = footDeg
                };*/
                /*double hipDeg =
                    (!crouch ? -60 : -70) + Math.Sin(step / 4 * Math.PI * 2) * 15d * (crouch ? .5d : 1d);
                double kneeDeg =
                    hipDeg - Math.Cos(step / 4 * Math.PI * 2) * 20d * (crouch ? .5d : 1d) - (!crouch ? 30d : 40d);
                double footDeg =
                    (kneeDeg - hipDeg);
                return new double[] { hipDeg, kneeDeg, footDeg };*/
            }

            public override void Update(MovementInfo info)
            {
                base.Update(info);
                Log($"- DigitigradeLegGroup Update -");
                Log($"Step: {AnimationStep}");
                Log($"Info: {info.Direction} {info.Movement}");

                LegAngles leftAngles, rightAngles;
                switch (Animation)
                {
                    default:
                    case Animation.Crouch:
                    case Animation.Idle:
                        leftAngles = CalculateAngles(AnimationStep + (OffsetLegs ? IdOffset : 0));
                        rightAngles = CalculateAngles(AnimationStepOffset + (OffsetLegs ? IdOffset : 0));
                        break;
                    case Animation.CrouchTurn:
                    case Animation.Turn:
                        OffsetLegs = false;
                        leftAngles = CalculateAngles(AnimationStep + IdOffset);
                        OffsetLegs = true;
                        rightAngles = CalculateAngles(AnimationStepOffset + IdOffset);
                        break;
                    case Animation.CrouchWalk:
                    case Animation.Walk:
                        OffsetLegs = true;
                        leftAngles = CalculateAngles(AnimationStep + IdOffset);
                        rightAngles = CalculateAngles(AnimationStepOffset + IdOffset);
                        break;
                    case Animation.Force:
                        leftAngles = CalculateAngles(AnimationStep);
                        rightAngles = CalculateAngles(AnimationStep);
                        break;
                }

                leftAngles *= new LegAngles(-1, -1, 1);
                rightAngles *= new LegAngles(-1, -1, 1);

                //leftAngles.FeetRadians = Math.PI - leftAngles.HipRadians - leftAngles.KneeRadians;
                //rightAngles.FeetRadians = Math.PI - rightAngles.HipRadians - rightAngles.KneeRadians;
                leftAngles.KneeDegrees -= 10;
                rightAngles.KneeDegrees -= 10;

                double foot = leftAngles.FeetDegrees;

                leftAngles.FeetDegrees = -(leftAngles.HipDegrees + leftAngles.KneeDegrees);//180 - leftAngles.HipDegrees.Absolute180() - leftAngles.KneeDegrees.Absolute180();
                rightAngles.FeetDegrees = -(rightAngles.HipDegrees + rightAngles.KneeDegrees);

                /*double stepMultiplier = AnimationStep.Modulo(2) / 2;
                if (stepMultiplier > .5)
                    stepMultiplier = (.5 - stepMultiplier) * 2;
                else
                    stepMultiplier *= 2;*/

                leftAngles.FeetDegrees *= -leftAngles.KneeDegrees / 45 * .5;
                rightAngles.FeetDegrees *= -rightAngles.KneeDegrees / 45 * .5;
                leftAngles.FeetDegrees += 25;
                rightAngles.FeetDegrees += 25;

                leftAngles.QuadDegrees = (leftAngles.HipDegrees + leftAngles.KneeDegrees + leftAngles.FeetDegrees);
                rightAngles.QuadDegrees = -(rightAngles.HipDegrees + rightAngles.KneeDegrees + rightAngles.FeetDegrees);

                Log("Digitigrade (left):", leftAngles.HipDegrees, leftAngles.KneeDegrees, leftAngles.FeetDegrees);
                Log("Digitigrade (right):", rightAngles.HipDegrees, rightAngles.KneeDegrees, rightAngles.FeetDegrees);
                SetAngles(leftAngles * new LegAngles(1, -1, -1, 1), rightAngles * new LegAngles(-1, -1, -1, -1));
            }

            public override void Update(Vector3 forwardsDeltaVec, Vector3 movementVector, double delta)
            {
                double forwardsDelta = forwardsDeltaVec.Z;
                forwardsDelta *= -1;
                base.Update(forwardsDeltaVec, movementVector, delta);
                Log($"Step: {AnimationStep} {Animation} {delta}");

                if (!Animation.IsCrouch())
                    CrouchWaitTime = Math.Max(0, jumping ? 0 : CrouchWaitTime - delta * 2);
                else
                    CrouchWaitTime = Math.Min(.9d, CrouchWaitTime + delta * 2);
                Log($"CrouchWaitTime: {CrouchWaitTime}");

                LegAngles leftAngles, rightAngles;
                switch (Animation)
                {
                    default:
                    case Animation.Crouch:
                    case Animation.Idle:
                        /*if ((AnimationStep - 0).Absolute() < .025 || (AnimationStep - 2).Absolute() < .025 || AnimationWaitTime <= 0)
                        {
                            AnimationStep = 0;
                            AnimationWaitTime = 0;
                        }
                        else
                        {
                            OffsetLegs = true;
                            Animation = Animation.Walk;
                        }*/
                        AnimationStep = 0;
                        AnimationWaitTime = 0;
                        leftAngles = CalculateAngles(AnimationStep + (OffsetLegs ? IdOffset : 0));
                        rightAngles = CalculateAngles(AnimationStepOffset + (OffsetLegs ? IdOffset : 0));
                        foreach (IMyLandingGear lg in LeftGears.Concat(RightGears))
                        {
                            lg.Enabled = true;
                            lg.AutoLock = true;
                        }
                        break;
                    case Animation.CrouchTurn:
                    case Animation.Turn:
                        AnimationStep += delta * 2;
                        leftAngles = CalculateAngles(AnimationStep + IdOffset);
                        OffsetLegs = true;
                        rightAngles = CalculateAngles(AnimationStepOffset + IdOffset);
                        break;
                    case Animation.CrouchWalk:
                    case Animation.Walk:
                        if (AnimationWaitTime == 0)
                            AnimationStep = 0d;
                        AnimationWaitTime += forwardsDelta * Configuration.AnimationSpeed;
                        //double absWaitTime = Math.Abs(AnimationWaitTime);
                        /*if (absWaitTime < 2f)
                        {
                            foreach (IMyLandingGear lg in LeftGears.Concat(RightGears))
                            {
                                lg.Enabled = true;
                                lg.AutoLock = false;
                                lg.Unlock();
                            }
                            leftAngles = CalculateAngles(AnimationStep);
                            rightAngles = CalculateAngles(AnimationStepOffset);//(AnimationWaitTime + 2f) % 4);
                            break;
                        }*/
                        /*else if (absWaitTime <= 2.3f)
                        {
                            leftAngles = CalculateAngles(AnimationStep);
                            rightAngles = CalculateAngles(3f * (AnimationWaitTime / Math.Abs(AnimationWaitTime)));
                            break;
                        }*/
                        // else
                        leftAngles = CalculateAngles(AnimationStep + IdOffset);
                        rightAngles = CalculateAngles(AnimationStepOffset + IdOffset);

                        /* this might be wrong now
                         *   3
                         * 2   4
                         *   1
                         * */

                        bool leftGears = AnimationStep < 3f && AnimationStep > 1f;
                        foreach (IMyLandingGear lg in LeftGears)
                        {
                            if (!leftGears)
                                lg.Unlock();
                            lg.Enabled = true;
                            lg.AutoLock = true;
                        }

                        foreach (IMyLandingGear lg in RightGears)
                        {
                            if (leftGears)
                                lg.Unlock();
                            lg.Enabled = true;
                            lg.AutoLock = true;
                        }
                        break;
                    case Animation.Force:
                        leftAngles = CalculateAngles(AnimationStep);
                        rightAngles = CalculateAngles(AnimationStep);
                        break;
                }

                leftAngles *= new LegAngles(-1, -1, 1);
                rightAngles *= new LegAngles(-1, -1, 1);

                //leftAngles.FeetRadians = Math.PI - leftAngles.HipRadians - leftAngles.KneeRadians;
                //rightAngles.FeetRadians = Math.PI - rightAngles.HipRadians - rightAngles.KneeRadians;
                leftAngles.KneeDegrees -= 10;
                rightAngles.KneeDegrees -= 10;

                double foot = leftAngles.FeetDegrees;

                leftAngles.FeetDegrees = -(leftAngles.HipDegrees + leftAngles.KneeDegrees);//180 - leftAngles.HipDegrees.Absolute180() - leftAngles.KneeDegrees.Absolute180();
                rightAngles.FeetDegrees = -(rightAngles.HipDegrees + rightAngles.KneeDegrees);

                double stepMultiplier = AnimationStep.Modulo(2) / 2;
                if (stepMultiplier > .5)
                    stepMultiplier = (.5 - stepMultiplier) * 2;
                else
                    stepMultiplier *= 2;

                leftAngles.FeetDegrees *= -leftAngles.KneeDegrees / 45 * .5;
                rightAngles.FeetDegrees *= -rightAngles.KneeDegrees / 45 * .5;
                leftAngles.FeetDegrees += 25;
                rightAngles.FeetDegrees += 25;

                leftAngles.QuadDegrees = (leftAngles.HipDegrees + leftAngles.KneeDegrees + leftAngles.FeetDegrees);
                rightAngles.QuadDegrees = -(rightAngles.HipDegrees + rightAngles.KneeDegrees + rightAngles.FeetDegrees);

                Log("Digitigrade (left):", leftAngles.HipDegrees, leftAngles.KneeDegrees, leftAngles.FeetDegrees);
                Log("Digitigrade (right):", rightAngles.HipDegrees, rightAngles.KneeDegrees, rightAngles.FeetDegrees);
                SetAngles(leftAngles * new LegAngles(1, -1, 1, 1), rightAngles * new LegAngles(-1, 1, -1, 1));
            }
        }
    }
}
