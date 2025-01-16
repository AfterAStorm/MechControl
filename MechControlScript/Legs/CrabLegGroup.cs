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
        public class CrabLegGroup : LegGroup
        {
            protected virtual LegAngles LegAnglesMultiplier => LegAngles.One;
            protected virtual LegAngles LeftAnglesMultiplier => new LegAngles(-1, 1, 1, 1, 1);
            protected virtual LegAngles RightAnglesMultiplier => new LegAngles(1, 1, 1, 1, -1);

            protected virtual LegAngles LegAnglesOffset => new LegAngles(0, 0, 0, 0);

            public override void Update(MovementInfo info)
            {
                base.Update(info);
                Log($"Step: {AnimationStep}");
                Log($"Info: {info.Direction} {info.Movement}");

                // get lengths
                double thighLength = CalculatedThighLength;
                double calfLength = CalculatedCalfLength;

                // calculate offsets
                double baseHeight = InverseKinematics.FindDistanceWhereKnee(thighLength, calfLength, MathHelper.ToRadians(65)) * Configuration.StandingHeight; //Math.Sqrt(2) * 1.25 * Configuration.StandingHeight;
                double maxHeight = InverseKinematics.FindDistanceWhereKnee(thighLength, calfLength, MathHelper.ToRadians(80));
                double crouchHeight = InverseKinematics.FindDistanceWhereKnee(thighLength, calfLength, MathHelper.ToRadians(90));

                double deltaStepHeight = -(maxHeight - baseHeight); // find the difference to reach said target angle
                double deltaCrouchHeight = -(crouchHeight - baseHeight) * CrouchWaitTime;

                // calculate values
                double inverseMultiplier = LegAnglesMultiplier.HipDegrees;
                double leftX = Configuration.Lean * inverseMultiplier;
                double rightX = Configuration.Lean * inverseMultiplier;
                double leftY = baseHeight;
                double rightY = baseHeight;
                double leftZ = 0;
                double rightZ = 0;
                double leftStrafe = 0;
                double rightStrafe = 0;

                double leftStep = AnimationStep + IdOffset;
                double rightStep = AnimationStepOffset + IdOffset;

                if (info.Idle)
                {

                }

                if (info.Walking)
                {
                    OffsetLegs = true;
                    leftX += Math.Max(-Math.Sin(Math.PI * 2 * leftStep + Math.PI / 2), 0) * .5 * Configuration.StepHeight;
                    rightX += Math.Max(-Math.Sin(Math.PI * 2 * rightStep + Math.PI / 2), 0) * .5 * Configuration.StepHeight;
                    leftZ += -Math.Sin(Math.PI * 2 * leftStep) * .5 * Configuration.StepLength;
                    rightZ += -Math.Sin(Math.PI * 2 * rightStep) * .5 * Configuration.StepLength;
                    /*leftY -= Math.Max(-Math.Sin(Math.PI * 2 * leftStep + Math.PI / 2), 0) * deltaStepHeight;
                    rightY -= Math.Max(-Math.Sin(Math.PI * 2 * rightStep + Math.PI / 2), 0) * deltaStepHeight;*/
                }

                if (info.Turning)
                {
                    OffsetLegs = true;
                    leftX += Math.Max(-Math.Sin(Math.PI * 2 * leftStep + Math.PI / 2), 0) * .5 * Configuration.StepHeight;
                    rightX += Math.Max(-Math.Sin(Math.PI * 2 * rightStep + Math.PI / 2), 0) * .5 * Configuration.StepHeight;
                    leftZ += -Math.Sin(Math.PI * 2 * leftStep) * .5 * Configuration.StepLength;
                    rightZ += Math.Sin(Math.PI * 2 * rightStep) * .5 * Configuration.StepLength;
                }

                if (info.Strafing)
                {
                    leftX += Math.Max(-Math.Sin(Math.PI * 2 * leftStep + Math.PI / 2), 0) * .5 * Configuration.StepHeight;
                    rightX += Math.Max(-Math.Sin(Math.PI * 2 * rightStep + Math.PI / 2), 0) * .5 * Configuration.StepHeight;
                    leftY += -Math.Sin(Math.PI * 2 * leftStep) * .5 * Configuration.StepLength;
                    rightY += -Math.Sin(Math.PI * 2 * leftStep) * .5 * Configuration.StepLength;
                }

                leftX += deltaCrouchHeight;
                rightX += deltaCrouchHeight;

                // calculate ik
                Log($"left: {leftX} {leftY}");
                Log($"right: {rightX} {rightY}");
                LegAngles left = InverseKinematics.Calculate2Joint3D(thighLength, calfLength, leftX, leftY, leftZ);
                LegAngles right = InverseKinematics.Calculate2Joint3D(thighLength, calfLength, rightX, rightY, rightZ);

                /*left.FeetDegrees = left.KneeDegrees;
                left.KneeDegrees = left.HipDegrees;
                right.FeetDegrees = right.KneeDegrees;
                right.KneeDegrees = right.HipDegrees;

                left.HipDegrees = Math.Atan2(leftZ, leftY).ToDegrees();
                right.HipDegrees = Math.Atan2(rightZ, rightY).ToDegrees();*/

                left.QuadDegrees = -left.KneeDegrees - left.FeetDegrees + 90;
                right.QuadDegrees = -right.KneeDegrees - right.FeetDegrees + 90;

                left.StrafeDegrees = leftStrafe;
                right.StrafeDegrees = rightStrafe;

                SetAngles(
                    left * LeftAnglesMultiplier + LegAnglesOffset,
                    right * RightAnglesMultiplier + LegAnglesOffset
                );
                HandlePistons();
            }

            /*protected LegAngles CalculateAngles(double step, Vector3 forwardsDeltaVec, Vector3 movementVec, bool left, bool invertedCrouch = false)
            {
                //step = step.Modulo(4);
                double turnRotation = 1; // when it's negative, the step goes backwards too, so it doesn't actual matter :p //Math.Sign(movementVec.Y);
                double leftInverse = left ? -1d : 1d;
                double idOffset = IdOffset == 0 ? 1 : -1;

                LegAngles angles = new LegAngles();

                double stepPercent = step.Modulo(1);
                double sin = Math.Sin(stepPercent * 2 * Math.PI);
                double cos = Math.Cos(stepPercent * 2 * Math.PI);

                //Log($"thigh, calf calc: {thigh}, {calf}");
                Log($"turn rotation: {turnRotation}");

                double x = 3;
                double y = .65 + (StandingHeight - .95f);

                // y+ is down
                // x+ is ????

                if (Animation.IsTurn()) // turn
                    y += MathHelper.Clamp(cos, -1, 0);//MathHelper.Clamp(cos * , 0, 1) * 1;
                else if (Animation.IsWalk() && Math.Abs(movementVec.X) > 0) // strafe
                {
                    //x += (sin) * 1 * leftInverse;
                    //y += (cos) * .5;
                    // x += MathHelper.Clamp(sin, -1, 1) * 1;
                    // y += -MathHelper.Clamp(cos, 0, 1);
                    //  x -= .5d - (sin * .5d);
                    x -= /*Math.Sign(movementVec.X) **/ /*leftInverse * sin * .85 + .85;//.5 + sin * leftInverse * .5;//-= .5d - ((cos * leftInverse) * .5d);
                    y += -MathHelper.Clamp(cos + .5, 0, 1) * .5d * Configuration.StepHeight;
                }
                else if (Animation.IsWalk()) // walk
                {
                    x += (cos) * -.5;//* leftInverse * .75;
                    y += (cos - 1) * .3 * Configuration.StepHeight * StandingHeight;
                }

                /* DANCE
                 * x += -Math.Abs(sin * leftInverse);
                   y += cos * .5;
                 */

            /*y -= 1.25 * CrouchWaitTime;
            if (jumping && !crouched)
                y += 1;
            double thigh = CalculatedThighLength;//Configuration.ThighLength;//2.5; // TODO: calculate
            double calf = CalculatedCalfLength;//Configuration.CalfLength + .25;//2.75;
            //                                            2.5  , 2.75
            //y = 1 + MathHelper.Clamp(cos * .5, 0, .5) * 2;
            //x = sin * .5 + 3.5;
            LegAngles ik = InverseKinematics.CalculateLegOld(thigh, calf, x, y);//InverseKinematics.CalculateLeg(Configuration.ThighLength, Configuration.CalfLength, 1, 1);

            if (Animation.IsTurn())
                angles.HipDegrees = DirectionMultiplier * sin * turnRotation * 15;
            else
                angles.HipDegrees =
                    -sin * DirectionMultiplier
                    * leftInverse
                    * (10 + 5 * Configuration.StepLength)
                    - (CrouchWaitTime * Configuration.HipOffsets * .5 * (invertedCrouch ? -1 : 1));
            angles.HipDegrees *= (1 - Math.Abs(Math.Sign(movementVec.X)));
            angles.KneeDegrees = -(ik.HipDegrees);
            angles.FeetDegrees = -(ik.KneeDegrees - 180);
            angles.QuadDegrees = 90 - angles.KneeDegrees - angles.FeetDegrees;

            return angles;
        }

        public override void Initialize()
        {
            base.Initialize();

            CalculatedThighLength = Configuration.ThighLength > 0 ? Configuration.ThighLength : FindCalfLength();
            CalculatedCalfLength = Configuration.CalfLength > 0 ? Configuration.CalfLength : FindQuadLength();
        }

        protected virtual double DirectionMultiplier => 1d;
        protected virtual LegAngles LegAnglesOffset => LegAngles.Zero;

        public override void Update(MovementInfo info)
        {
            base.Update(info);
            Log($"- CrabLegGroup Update -");
            Log($"Step: {AnimationStep}");
            Log($"Info: {info.Direction} {info.Movement}");

            LegAngles leftAngles, rightAngles;
            switch (Animation)
            {
                default:
                case Animation.Crouch:
                case Animation.Idle:
                    AnimationStep = 0;
                    leftAngles = CalculateAngles(0, info.Movement, info.Direction, false, false);
                    rightAngles = CalculateAngles(0, info.Movement, info.Direction, false, true);
                    break;
                case Animation.CrouchTurn:
                case Animation.Turn:
                    OffsetLegs = true;
                    leftAngles = CalculateAngles(AnimationStep + IdOffset, info.Movement, info.Direction, true, false);
                    rightAngles = CalculateAngles(AnimationStepOffset + IdOffset, info.Movement, info.Direction, false, false);
                    break;
                case Animation.CrouchWalk:
                case Animation.Walk:
                    OffsetLegs = true;
                    leftAngles = CalculateAngles(AnimationStep + IdOffset, info.Movement, info.Direction, true, false);
                    rightAngles = CalculateAngles(AnimationStepOffset + IdOffset, info.Movement, info.Direction, false, false);
                    break;
            }

            leftAngles += LegAnglesOffset;
            rightAngles += LegAnglesOffset;
            SetAngles(leftAngles, rightAngles * new LegAngles(1, 1, 1, 1));
        }

        public override void Update(Vector3 forwardsDeltaVec, Vector3 movementVector, double delta)
        {
            //double forwardsDelta = forwardsDeltaVec.Z;
            base.Update(forwardsDeltaVec, movementVector, delta);
            Log($"Step: {AnimationStep} {Animation} {delta}");

            LegAngles leftAngles, rightAngles;
            switch (Animation)
            {
                default:
                case Animation.Crouch:
                case Animation.Idle:
                    AnimationStep = 0;
                    leftAngles = CalculateAngles(AnimationStep, forwardsDeltaVec, movementVector, false);
                    rightAngles = CalculateAngles(AnimationStep, forwardsDeltaVec, movementVector, false, true);
                    break;
                case Animation.CrouchTurn:
                case Animation.Turn:
                    OffsetLegs = true;
                    leftAngles = CalculateAngles(AnimationStep + IdOffset, forwardsDeltaVec, movementVector, true, false);
                    rightAngles = CalculateAngles(AnimationStepOffset + IdOffset, forwardsDeltaVec, movementVector, false, false);
                    break;
                case Animation.CrouchWalk:
                case Animation.Walk:
                    leftAngles = CalculateAngles(AnimationStep + IdOffset, forwardsDeltaVec, movementVector, true);
                    rightAngles = CalculateAngles(AnimationStepOffset + IdOffset, forwardsDeltaVec, movementVector, false);
                    break;
            }

            Log("Inverse Spideroid (right):", rightAngles.HipDegrees, rightAngles.KneeDegrees, rightAngles.FeetDegrees, rightAngles.QuadDegrees);
            Log("Inverse Spideroid (left):", leftAngles.HipDegrees, leftAngles.KneeDegrees, leftAngles.FeetDegrees, leftAngles.QuadDegrees);
            SetAngles(leftAngles * new LegAngles(1, 1, 1, 1), rightAngles * new LegAngles(1, 1, -1, 1));
        }*/

            /*protected override void SetAnglesOf(List<LegJoint> leftStators, List<LegJoint> rightStators, double leftAngle, double rightAngle, double offset)
             {
                 foreach (var motor in leftStators)
                     motor.SetAngle(leftAngle * motor.Configuration.InversedMultiplier - (offset + motor.Configuration.Offset) * -1);
                 foreach (var motor in rightStators)
                     motor.SetAngle(rightAngle * motor.Configuration.InversedMultiplier - (offset + motor.Configuration.Offset));
             }*/
        }
    }
}
