using Sandbox.Game.EntityComponents;
using Sandbox.ModAPI.Ingame;
using Sandbox.ModAPI.Interfaces;
using SpaceEngineers.Game.ModAPI.Ingame;
using System.Collections.Generic;
using System.Collections;
using System.Linq;
using System.Text;
using System;
using VRage.Collections;
using VRage.Game.Components;
using VRage.Game.GUI.TextPanel;
using VRage.Game.ModAPI.Ingame.Utilities;
using VRage.Game.ModAPI.Ingame;
using VRage.Game.ObjectBuilders.Definitions;
using VRage.Game;
using VRage;
using VRageMath;
using System.Collections.Immutable;

namespace IngameScript
{
	partial class Program
	{
        public class HumanoidLegGroup : TriLegGroup
        {

            public static LegConfiguration GlobalDefaultConfiguration { get; set; } = new LegConfiguration()
            {
                VariableStandingHeight = new JointVariable(JointVariableType.Percentage, 90f),
                VariableXOffset = new JointVariable(JointVariableType.Percentage, 0),
                VariableZOffset = new JointVariable(JointVariableType.Percentage, 0),
                VariableStepLength = new JointVariable(JointVariableType.Percentage, 30f),
                VariableStepHeight = new JointVariable(JointVariableType.Percentage, 10f),
                VariableTurnLength = new JointVariable(JointVariableType.Percentage, 15f),
                VariableStrafeDistance = new JointVariable(JointVariableType.Percentage, 25f),
                VariableCrouchHeight = new JointVariable(JointVariableType.Percentage, 10f),
                VariableAnimationSpeed = new JointVariable(JointVariableType.Percentage, 100f),
                VariableCrouchSpeed = new JointVariable(JointVariableType.Percentage, 100f),

                HipOffsets = 0,
                KneeOffsets = 0,
                FootOffsets = 0,
                QuadOffsets = 0,
                StrafeOffsets = 0,
                VtolActive = true
            };

            public override LegConfiguration DefaultConfiguration { get; set; } = GlobalDefaultConfiguration;

            protected virtual LegAngles LegAnglesMultiplier => LegAngles.One;
            protected virtual LegAngles LeftAnglesMultiplier => new LegAngles(-1, 1, 1, 0, 1, 1);
            protected virtual LegAngles RightAnglesMultiplier => new LegAngles(1, 1, 1, 0, 1, -1);

            float XOffset, YOffset, ZOffset;

            float StrafeDistance, StandingHeight, StepLength, StepHeight, CrouchHeight, TurnAngle;

            public override void Initialize()
            {
                base.Initialize();

                float radius = (float)(ThighLength + CalfLength);

                XOffset = Configuration.VariableXOffset.GetMetersOf(GridSize, 0, radius);
                YOffset = Configuration.VariableYOffset.GetMetersOf(GridSize, 0, radius);
                ZOffset = Configuration.VariableZOffset.GetMetersOf(GridSize, 0, radius);

                StandingHeight = Configuration.VariableStandingHeight.GetMetersOf(GridSize, 0, radius);

                if (StandingHeight > radius)
                {
                    StaticWarn("Out of Bounds: Standing Height", $"The standing height of leg group {Configuration.Id} is out of bounds, current/maximum: {StandingHeight:f3}m/{radius:f3}m");
                }

                // x^2 + y^2 = r^2
                float maxStepLength = (float)Math.Sqrt(Math.Pow(radius, 2) - Math.Pow(StandingHeight, 2)); // sqrt(r^2 - y^2) = x

                StepLength = Configuration.VariableStepLength.GetMetersOf(GridSize, 0, maxStepLength);
                StrafeDistance = Configuration.VariableStrafeDistance.GetMetersOf(GridSize, 0, maxStepLength);
                StepHeight = Configuration.VariableStepHeight.GetMetersOf(GridSize, 0, StandingHeight);
                CrouchHeight = Configuration.VariableCrouchHeight.GetMetersOf(GridSize, 0, StandingHeight);

                TurnAngle = Configuration.VariableTurnLength.GetMetersOf(1f, 0f, 90f);

                // max step height in relation to angles!
                double maxStepHeight = AreAnyJointsHinges(LeftKneeJoints.Concat(RightKneeJoints)) ? StandingHeight - InverseKinematics.FindDistanceWhereKnee(ThighLength, CalfLength, Math.PI / 2f) : StandingHeight;

                if (StepLength > maxStepLength)
                {
                    StaticWarn("Out of Bounds: Step Length", $"The step length of leg group {Configuration.Id} is out of bounds, current/maximum: {StepLength:f3}m/{maxStepLength:f3}m");
                }

                if (StepHeight > StandingHeight)
                {
                    StaticWarn("Out of Bounds: Step Height", $"The step height of leg group {Configuration.Id} is out of bounds, current/maximum: {StepHeight:f3}m/{StandingHeight:f3}m");
                }
                else if (StepHeight > maxStepHeight)
                {
                    StaticWarn("Out of Bounds: Step Height", $"The step height of leg group {Configuration.Id} is out of bounds, current/maximum: {StepHeight:f3}m/{maxStepHeight:f3}m (knee hinge limits)");
                }
                else if (CrouchHeight > maxStepHeight)
                {
                    StaticWarn("Out of Bounds: Crouch Height", $"The crouch height of leg group {Configuration.Id} is out of bounds, current/maximum {CrouchHeight}m/{maxStepHeight:f3}m (knee hinge limits)");
                }
                else if (CrouchHeight + StepHeight > StandingHeight)
                {
                    StaticWarn("Out of Bounds: Crouch Height", $"The crouch height of leg group {Configuration.Id} is out of bounds, current/maximum {CrouchHeight}m/{StandingHeight - StepHeight:f3}m");
                }
                // should check, but it will be invalid for all hinges since that's the default
            }

            private double x, y, z;

            double leftilt = 0;
            double righttilt = 0;

            public override void Update(MovementInfo info)
            {
                base.Update(info);
                Log("Step:", AnimationStep, AnimationStepOffset);
                var cameraOffsets = UpdateCameras();
                Log("Camera Offsets:", cameraOffsets.Item1, cameraOffsets.Item2);

                Log("turn ang", TurnAngle);

                LegAngles flyingAngles = new LegAngles();
                if (info.Flying && Configuration.VtolActive)
                {
                    flyingAngles.HipDegrees = flyingOffset.X * 45;
                    flyingAngles.StrafeDegrees = flyingOffset.Z * 45;
                }
                Log("Flying Angles:", flyingAngles.HipDegrees, flyingAngles.StrafeDegrees);

                leftilt = MathHelper.Clamp(GetLeftTiltA().ToDegrees(), -30, 30);

                float radius = (float)(ThighLength + CalfLength);

                // left
                x =
                    AnimationDirectionMultiplier * Math.Sin(2 * AnimationStep * Math.PI) * StepLength * -info.Walk
                    - XOffset * AnimationDirectionMultiplier;
                y = YOffset
                    - MathHelper.Clamp(cameraOffsets.Item1, -StandingHeight, StandingHeight)
                    + StandingHeight
                    - CrouchHeight * CrouchWaitTime
                    - Math.Max(Math.Sin(2 * AnimationStep * Math.PI + Math.PI / 2d), 0) * (StepHeight - MathHelper.Clamp(cameraOffsets.Item1, -StepHeight, StepHeight)) * Math.Abs(AbsMax(info.Walk, AbsMax(info.Strafe, info.Turn)))
                    //+ MathHelper.Min(Math.Abs(leftilt) * 0.03f, 0.3f);
                    + MathHelper.Min(0.3f, radius * Math.Cos(leftilt.ToRadians()) - radius);
                z = ZOffset
                    + (-Math.Sign(info.Strafe) * Math.Sin(2 * AnimationStep * Math.PI)) * StrafeDistance * Math.Abs(info.Strafe)
                    + StrafeDistance * Math.Abs(info.Strafe);

                if (customTarget != Vector3D.Zero)
                {
                    x = customTarget.X;
                    y = customTarget.Y;
                }
                LegAngles leftAngles = InverseKinematics.Calculate2Joint2D(ThighLength, CalfLength, x, y);

                double len = Math.Sqrt(Math.Pow(y, 2) + Math.Pow(z, 2));
                double strafe = Math.Asin(-z / len);

                leftAngles.StrafeDegrees = strafe.ToDegrees();
                leftAngles.TurnDegrees = info.Turn * -TurnAngle * Math.Sin(AnimationStep * Math.PI * 2);

                //leftilt += (GetLeftTiltA() - leftilt) / 1f;
                double multiplier = 1d;// leftilt < 10d ? 1.15d : 1.15f - 0.3f * MathHelper.Clamp((leftilt - 10d) / (60d - 10d), 0d, 1d);
                leftAngles.HipDegrees += leftilt * multiplier;

                Log("Left  Target:");
                Log("X:", x);
                Log("Y:", y);
                Log("Z:", z);
                Log("Left  Angles:");
                Log("Hip   :", leftAngles.HipDegrees);
                Log("Knee  :", leftAngles.KneeDegrees);
                Log("Foot  :", leftAngles.FeetDegrees);
                Log("Quad  :", leftAngles.QuadDegrees);
                Log("Strafe:", leftAngles.StrafeDegrees);
                Log("Turn  :", leftAngles.TurnDegrees);
                Log("leftilt:", leftilt);

                righttilt = MathHelper.Clamp(GetRightTiltA().ToDegrees(), -30, 30);

                // right
                x =
                    AnimationDirectionMultiplier * Math.Sin(2 * AnimationStepOffset * Math.PI) * StepLength * -info.Walk
                    - XOffset * AnimationDirectionMultiplier;
                y = YOffset
                    - MathHelper.Clamp(cameraOffsets.Item2, -StandingHeight, StandingHeight)
                    + StandingHeight
                    - CrouchHeight * CrouchWaitTime
                    - Math.Max(Math.Sin(2 * AnimationStepOffset * Math.PI + Math.PI / 2d), 0) * (StepHeight - MathHelper.Clamp(cameraOffsets.Item2, -StepHeight, StepHeight)) * Math.Abs(AbsMax(info.Walk, AbsMax(info.Strafe, info.Turn)))
                    //+ MathHelper.Min(Math.Abs(righttilt) * 0.025f, 0.3f);
                    + MathHelper.Min(0.3f, radius * Math.Cos(righttilt.ToRadians()) - radius);
                z = ZOffset
                    + (-Math.Sign(info.Strafe) * Math.Sin(2 * AnimationStep * Math.PI)) * StrafeDistance * Math.Abs(info.Strafe)
                    + StrafeDistance * Math.Abs(info.Strafe);

                if (customTarget != Vector3D.Zero)
                {
                    x = customTarget.X;
                    y = customTarget.Y;
                }
                LegAngles rightAngles = InverseKinematics.Calculate2Joint2D(ThighLength, CalfLength, x, y);

                len = Math.Sqrt(Math.Pow(y, 2) + Math.Pow(z, 2));
                strafe = Math.Asin(-z / len);

                rightAngles.StrafeDegrees = strafe.ToDegrees();
                rightAngles.TurnDegrees = -info.Turn * TurnAngle * Math.Sin(AnimationStepOffset * Math.PI * 2);

                //righttilt += (GetRightTiltA() - righttilt) / 1f;
                //multiplier = righttilt < 10d ? 1.15d : 1.15f - 0.3f * MathHelper.Clamp((righttilt - 10d) / (60d - 10d), 0d, 1d); 
                rightAngles.HipDegrees += righttilt * multiplier;

                Log("Right Target:");
                Log("X:", x);
                Log("Y:", y);
                Log("Z:", z);
                Log("Right Angles:");
                Log("Hip   :", rightAngles.HipDegrees);
                Log("Knee  :", rightAngles.KneeDegrees);
                Log("Foot  :", rightAngles.FeetDegrees);
                Log("Quad  :", rightAngles.QuadDegrees);
                Log("Strafe:", rightAngles.StrafeDegrees);
                Log("Turn  :", rightAngles.TurnDegrees);
                Log("righttilt:", righttilt);

                //if (LeftMagnets.Any(m => m.IsLocked))
                //    leftAngles.HipDegrees = double.PositiveInfinity;
                    //leftAngles = new LegAngles(LeftHipJoints.First().Stator.Angle.ToDegrees(), LeftKneeJoints.First().Stator.Angle.ToDegrees(), LeftFootJoints.First().Stator.Angle.ToDegrees(), 0, 0, LeftTurnJoints.First().Stator.Angle.ToDegrees());
                //if (RightMagnets.Any(m => m.IsLocked))
                //    rightAngles.HipDegrees = double.PositiveInfinity;
                    //rightAngles = new LegAngles(RightHipJoints.First().Stator.Angle.ToDegrees(), RightKneeJoints.First().Stator.Angle.ToDegrees(), RightFootJoints.First().Stator.Angle.ToDegrees(), 0, 0, RightTurnJoints.First().Stator.Angle.ToDegrees());

                LegAngles leftAnglesFinal  =
                      flyingAngles * LeftAnglesMultiplier * new LegAngles(1, 1, 1, 1, -1, 1)
                    + LegAnglesOffset * LeftAnglesMultiplier  * new LegAngles(1, 1, 1, 1, -1, 1)
                    + LegAnglesMultiplier * LeftAnglesMultiplier  * leftAngles;
                LegAngles rightAnglesFinal =
                      flyingAngles * RightAnglesMultiplier
                    + LegAnglesOffset * RightAnglesMultiplier * new LegAngles(1, 1, 1, 1, -1, -1)
                    + LegAnglesMultiplier * RightAnglesMultiplier * rightAngles;

                /*if (magnetJumping)
                {
                    leftAnglesFinal *= LegAngles.One * (1 - magnetJumpProgress);
                    rightAnglesFinal *= LegAngles.One * (1 - magnetJumpProgress);
                }*/
                SetAngles(leftAnglesFinal, rightAnglesFinal);
                //rightAnglesFinal.FeetDegrees += normalangle.ToDegrees();

                UpdateMagnets(info);
                UpdateHydraulics();
            }
        }
    }
}
