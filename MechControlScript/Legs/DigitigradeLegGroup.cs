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
        public class DigitigradeLegGroup : QuadLegGroup
        {
            public override LegConfiguration DefaultConfiguration { get; set; } = new LegConfiguration()
            {
                VariableStandingHeight = new JointVariable(JointVariableType.Percentage, 90f),
                VariableXOffset = new JointVariable(JointVariableType.Percentage, 0),
                VariableZOffset = new JointVariable(JointVariableType.Percentage, 0),
                VariableStepLength = new JointVariable(JointVariableType.Percentage, 30f),
                VariableStepHeight = new JointVariable(JointVariableType.Percentage, 10f),
                VariableStandingDistance = new JointVariable(JointVariableType.Percentage, 25f),
                VariableTurnLength = new JointVariable(JointVariableType.Percentage, 15f),
                VariableStrafeDistance = new JointVariable(JointVariableType.Percentage, 25f),
                VariableCrouchHeight = new JointVariable(JointVariableType.Percentage, 10f),
                AnimationSpeed = 1f,
                CrouchSpeed = 1f,

                HipOffsets = 0,
                KneeOffsets = 0,
                FootOffsets = 0,
                QuadOffsets = 0,
                StrafeOffsets = 0,
                VtolActive = true
            };

            protected virtual LegAngles LegAnglesMultiplier => LegAngles.One;
            protected virtual LegAngles LeftAnglesMultiplier => new LegAngles(-1, 1, 1, 1, 1);
            protected virtual LegAngles RightAnglesMultiplier => new LegAngles(1, 1, 1, 1, -1);

            float Radius, RadiusAnkle;

            float XOffset;
            float YOffset;
            float ZOffset;

            float StandingHeight, StandingDistance, StrafeDistance;
            float StepLength;
            float StepHeight;
            float CrouchHeight;
            float TurnAngle;

            public override void Initialize()
            {
                base.Initialize();

                float radius = (float)(ThighLength + CalfLength);
                float radiusAnkle = (float)(ThighLength + CalfLength + AnkleLength);
                Radius = radius;
                RadiusAnkle = radiusAnkle;

                XOffset = Configuration.VariableXOffset.GetMetersOf(GridSize, 0, radius);
                YOffset = Configuration.VariableYOffset.GetMetersOf(GridSize, 0, 0);
                ZOffset = Configuration.VariableZOffset.GetMetersOf(GridSize, 0, 0);

                StandingHeight = Configuration.VariableStandingHeight.GetMetersOf(GridSize, 0, radiusAnkle);
                float thighCalfStandingHeight = StandingHeight - AnkleLength;
                float maxWidthLength = (float)Math.Sqrt(Math.Pow(radius, 2) - Math.Pow(thighCalfStandingHeight, 2));
                StandingDistance = Configuration.VariableStandingDistance.GetMetersOf(GridSize, 0, maxWidthLength);

                TurnAngle = Configuration.VariableTurnLength.GetMetersOf(1f, 0f, 90f);

                if (StandingHeight > radiusAnkle)
                {
                    StaticWarn("Out of Bounds: Standing Height", $"The standing height of leg group {Configuration.Id} is out of bounds, current/maximum: {StandingHeight:f3}m/{radius:f3}m");
                }

                // x^2 + y^2 = r^2
                float maxStepLength = (float)Math.Sqrt(Math.Pow(radiusAnkle, 2) - Math.Pow(StandingHeight, 2)); // sqrt(r^2 - y^2) = x

                StepLength = Configuration.VariableStepLength.GetMetersOf(GridSize, 0, maxStepLength);
                StrafeDistance = Configuration.VariableStepLength.GetMetersOf(GridSize, 0, maxStepLength);
                StepHeight = Configuration.VariableStepHeight.GetMetersOf(GridSize, 0, StandingHeight);
                CrouchHeight = Configuration.VariableCrouchHeight.GetMetersOf(GridSize, 0, StandingHeight);
                
                if (StepLength > maxStepLength)
                {
                    StaticWarn("Out of Bounds: Step Length", $"The step length of leg group {Configuration.Id} is out of bounds, current/maximum: {StepLength:f3}m/{maxStepLength:f3}m");
                }

                if (StepHeight > StandingHeight)
                {
                    StaticWarn("Out of Bounds: Step Height", $"The step height of leg group {Configuration.Id} is out of bounds, current/maximum: {StepHeight:f3}m/{StandingHeight:f3}m");
                }
                else if (CrouchHeight + StepHeight > StandingHeight)
                {
                    StaticWarn("Out of Bounds: Crouch Height", $"The crouch height of leg group {Configuration.Id} is out of bounds, current/maximum {CrouchHeight}m/{StandingHeight - StepHeight:f3}m");
                }
            }

            private double x, y, z;

            public override void Update(MovementInfo info)
            {
                base.Update(info);
                Log("Step:", AnimationStep, AnimationStepOffset);
                var cameraOffsets = UpdateCameras();
                Log("Camera Offsets:", cameraOffsets.Item1, cameraOffsets.Item2);

                LegAngles flyingAngles = new LegAngles();
                if (info.Flying && Configuration.VtolActive)
                {
                    flyingAngles.HipDegrees = flyingOffset.X * 45;
                    flyingAngles.StrafeDegrees = flyingOffset.Z * 45;
                }
                Log("Flying Angles:", flyingAngles.HipDegrees, flyingAngles.StrafeDegrees);

                // left
                x =
                    AnimationDirectionMultiplier * Math.Sin(2 * AnimationStep * Math.PI) * StepLength * -info.Walk
                    - XOffset;
                y = YOffset
                    - MathHelper.Clamp(cameraOffsets.Item1, -StandingHeight, StandingHeight)
                    + StandingHeight
                    - CrouchHeight * CrouchWaitTime
                    - Math.Max(Math.Sin(2 * AnimationStep * Math.PI + Math.PI / 2d), 0) * StepHeight * Math.Abs(AbsMax(info.Walk, AbsMax(info.Strafe, info.Turn)));
                z = ZOffset
                    + (-Math.Sign(info.Strafe) * Math.Sin(2 * AnimationStep * Math.PI)) * StrafeDistance * Math.Abs(info.Strafe)
                    + StrafeDistance * Math.Abs(info.Strafe);

                if (customTarget != Vector3D.Zero)
                {
                    x = customTarget.X;
                    y = customTarget.Y;
                }

                double width = StandingDistance - (StandingDistance / 2f * Math.Sin(2 * AnimationStep * Math.PI));
                double offset = Math.Sqrt(-Math.Pow(width, 2) + Math.Pow(AnkleLength, 2));

                x += width;
                y -= offset;
                LegAngles leftAngles = InverseKinematics.Calculate2Joint2D(ThighLength, CalfLength, x, y);
                leftAngles.FeetDegrees += Math.Acos(width / AnkleLength).ToDegrees() - 90;
                leftAngles.QuadDegrees -= Math.Acos(width / AnkleLength).ToDegrees() - 90;

                double len = Math.Sqrt(Math.Pow(y, 2) + Math.Pow(z, 2));
                double strafe = Math.Asin(-z / len);
                leftAngles.StrafeDegrees = strafe.ToDegrees();
                leftAngles.TurnDegrees = info.Turn * TurnAngle * Math.Sin(AnimationStep * Math.PI * 2);

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

                // right

                // a^2 + b^2 = c^2
                // a^2 = c^2 - b^2

                double distanceFromCenter = Math.Sqrt(Math.Pow(AnkleLength, 2) - Math.Pow(StandingDistance, 2));
                Log("distanceFromCenter:", distanceFromCenter);

                x =
                    AnimationDirectionMultiplier * Math.Sin(2 * AnimationStepOffset * Math.PI) * StepLength * -info.Walk
                    - XOffset;
                y = YOffset
                    - MathHelper.Clamp(cameraOffsets.Item2, -StandingHeight, StandingHeight)
                    + StandingHeight
                    - CrouchHeight * CrouchWaitTime
                    - Math.Max(Math.Sin(2 * AnimationStepOffset * Math.PI + Math.PI / 2d), 0) * StepHeight * Math.Abs(AbsMax(info.Walk, AbsMax(info.Strafe, info.Turn)));
                z = ZOffset
                    + (-Math.Sign(info.Strafe) * Math.Sin(2 * AnimationStep * Math.PI)) * StrafeDistance * Math.Abs(info.Strafe)
                    + StrafeDistance * Math.Abs(info.Strafe);

                if (customTarget != Vector3D.Zero)
                {
                    x = customTarget.X;
                    y = customTarget.Y;
                }

                width = StandingDistance - (StandingDistance / 2f * Math.Sin(2 * AnimationStepOffset * Math.PI));
                offset = Math.Sqrt(-Math.Pow(width, 2) + Math.Pow(AnkleLength, 2));

                x += width;
                y -= offset;
                LegAngles rightAngles = InverseKinematics.Calculate2Joint2D(ThighLength, CalfLength, x, y);
                rightAngles.FeetDegrees += Math.Acos(width / AnkleLength).ToDegrees() - 90;
                rightAngles.QuadDegrees -= Math.Acos(width / AnkleLength).ToDegrees() - 90;

                len = Math.Sqrt(Math.Pow(y, 2) + Math.Pow(z, 2));
                strafe = Math.Asin(-z / len);
                rightAngles.StrafeDegrees = strafe.ToDegrees();
                rightAngles.TurnDegrees = info.Turn * TurnAngle * Math.Sin(AnimationStep * Math.PI * 2);

                /*
                double a = Math.Sqrt(Math.Pow(fakeX, 2) + Math.Pow(fakeY, 2));
                double b = AnkleLength;
                double c = Math.Sqrt(Math.Pow(x, 2) + Math.Pow(y, 2));
                double angle = Math.Acos(
                    (Math.Pow(a, 2) + Math.Pow(b, 2) - Math.Pow(c, 2))
                    /
                    (2 * a * b)
                ).ToDegrees();
                Log("solved angle:", angle);
                double littleGuy = (180 - rightAngles.HipDegrees.Absolute() - rightAngles.KneeDegrees.Absolute());
                double finalAngle = angle - littleGuy;
                Log("final angle:", finalAngle);
                rightAngles.FeetDegrees -= finalAngle;
                rightAngles.QuadDegrees = finalAngle;

                // remaining
                // sin(theta) = opp/hyp
                // theta = asin(opp/hyp)
                /*double offset = Math.Asin((y - AnkleLength) / AnkleLength).ToDegrees();
                rightAngles.FeetDegrees -= offset;
                rightAngles.QuadDegrees = offset;*/
                //double sideLength = */

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

                SetAngles(
                    flyingAngles * LeftAnglesMultiplier  + LegAnglesOffset * LeftAnglesMultiplier  + LegAnglesMultiplier * LeftAnglesMultiplier  * leftAngles,
                    flyingAngles * RightAnglesMultiplier + LegAnglesOffset * RightAnglesMultiplier + LegAnglesMultiplier * RightAnglesMultiplier * rightAngles
                );
                UpdateMagnets(info);
                UpdateHydraulics();
            }
        }
    }
}
