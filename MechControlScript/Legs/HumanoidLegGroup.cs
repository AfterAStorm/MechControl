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
        public class HumanoidLegGroup : LegGroup
        {
            protected override LegAngles CalculateAngles(double step)
            {
                step = step.Modulo(1);
                bool crouch = Animation == Animation.Crouch || Animation == Animation.CrouchWalk;

                // shamelessly borrowed from https://opentextbooks.clemson.edu/wangrobotics/chapter/inverse-kinematics/
                // the equations anyway
                // x and y are flipped, yay!

                // Should cache this, but pff
                double thighLength = CalculatedThighLength; /*Configuration.ThighLength >= 0 ? Configuration.ThighLength :
                    (LeftHipStators.Count > 0 && LeftKneeStators.Count > 0 ? Vector3.Distance(LeftHipStators[0].Stator.GetPosition(), LeftKneeStators[0].Stator.GetPosition()) :
                    (RightHipStators.Count > 0 && RightKneeStators.Count > 0 ? Vector3.Distance(RightHipStators[0].Stator.GetPosition(), RightKneeStators[0].Stator.GetPosition()) : 0));//2.5f;*/
                double calfLength = CalculatedCalfLength; /*Configuration.CalfLength >= 0 ? Configuration.CalfLength :
                    (LeftFootStators.Count > 0 && LeftKneeStators.Count > 0 ? Vector3.Distance(LeftFootStators[0].Stator.GetPosition(), LeftKneeStators[0].Stator.GetPosition()) :
                    (RightFootStators.Count > 0 && RightKneeStators.Count > 0 ? Vector3.Distance(RightFootStators[0].Stator.GetPosition(), RightKneeStators[0].Stator.GetPosition()) : 0));//2.5f;*/

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
                double stepPercent = step; // / 4d;
                double sin = Math.Sin(stepPercent * 2 * Math.PI);
                double cos = Math.Cos(stepPercent * 2 * Math.PI);

                /*double x = sin * 1.5d * Configuration.StepLengthMultiplier + (Animation == Animation.Walk || Animation == Animation.CrouchWalk ? AccelerationLean : StandingLean);
                double y = MathHelper.Clamp(((maxDistance) - cos * (maxDistance) + StandingHeight - (crouch ? 1 : 0)), double.MinValue, 3d + StandingHeight - (crouch ? 1 : 0));*/

                double crouchAdditive = -.9 * CrouchWaitTime;//crouch ? -MathHelper.Clamp(CrouchWaitTime, 0, .9) : 0; //-.9d : 0;
                Log($"crouchAdditive: {crouchAdditive}; isTurn: {Animation == Animation.Turn}");

                double stepHeight = Configuration.StepHeight - .5d;
                double standingHeight = StandingHeight - .35d;

                double lean = (Animation == Animation.Walk || Animation == Animation.CrouchWalk ? AccelerationLean : StandingLean) + .5;
                double x =
                    sin * Configuration.StepLength + lean;
                double y = MathHelper.Clamp(
                    // value
                    cos * (stepHeight + 1) + .5 + 2 + standingHeight + 1.5,

                    // min/max
                    0,
                    2 + stepHeight + standingHeight + 1
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

            protected virtual double KneeOffset => 15;
            protected virtual LegAngles LegAnglesMultiplier => LegAngles.MinusOne;
            protected virtual LegAngles LeftAnglesMultiplier => new LegAngles(1, -1, -1);
            protected virtual LegAngles RightAnglesMultiplier => new LegAngles(-1, -1, -1);

            public override void Update(MovementInfo info)
            {
                base.Update(info);
                Log($"- HumanoidLegGroup Update -");
                Log($"Step: {AnimationStep}");
                Log($"Info: {info.Direction} {info.Movement}");

                LegAngles leftAngles, rightAngles;
                switch (Animation)
                {
                    default:
                    case Animation.Crouch:
                    case Animation.Idle:
                        AnimationStep = 0;
                        leftAngles = CalculateAngles(0);
                        rightAngles = CalculateAngles(0);
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
                }

                Log("LegAnglesMultiplier:", LegAnglesMultiplier.HipDegrees, LegAnglesMultiplier.KneeDegrees, LegAnglesMultiplier.FeetDegrees);
                leftAngles *= LegAnglesMultiplier;
                rightAngles *= LegAnglesMultiplier;

                leftAngles.KneeDegrees += KneeOffset;
                rightAngles.KneeDegrees += KneeOffset;

                leftAngles.FeetDegrees = -(leftAngles.HipDegrees + leftAngles.KneeDegrees);
                rightAngles.FeetDegrees = -(rightAngles.HipDegrees + rightAngles.KneeDegrees);

                SetAngles(leftAngles * LeftAnglesMultiplier, rightAngles * RightAnglesMultiplier);
                HandlePistons();
            }
        }
    }
}
