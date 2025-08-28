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

        public class TestLegGroup : TriLegGroup
        {
            /*protected virtual LegAngles LegAnglesMultiplier => LegAngles.One;
            protected virtual LegAngles LeftAnglesMultiplier => new LegAngles(-1, 1, 1, 0, 1);
            protected virtual LegAngles RightAnglesMultiplier => new LegAngles(1, 1, 1, 0, 1);

            float XOffset;
            float YOffset;
            float ZOffset;

            float StrafeDistance;
            float StandingHeight;
            float StepLength;
            float StepHeight;
            float CrouchHeight;

            public override void Initialize()
            {
                base.Initialize();

                float radius = (float)(ThighLength + CalfLength);

                XOffset = Configuration.VariableXOffset.GetMetersOf(GridSize, 0, radius);
                YOffset = Configuration.VariableYOffset.GetMetersOf(GridSize, 0, 0);
                ZOffset = Configuration.VariableZOffset.GetMetersOf(GridSize, 0, 0);

                StandingHeight = Configuration.VariableStandingHeight.GetMetersOf(GridSize, 0, radius);

                if (StandingHeight > radius)
                {
                    StaticWarn("Out of Bounds: Standing Height", $"The standing height of leg group {Configuration.Id} is out of bounds, maximum: {radius:f3}m");
                }

                // x^2 + y^2 = r^2
                float maxStepLength = (float)Math.Sqrt(Math.Pow(radius, 2) - Math.Pow(StandingHeight, 2)); // sqrt(r^2 - y^2) = x

                StepLength = Configuration.VariableStepLength.GetMetersOf(GridSize, 0, maxStepLength);
                StrafeDistance = Configuration.VariableStrafeDistance.GetMetersOf(GridSize, 0, maxStepLength);
                StepHeight = Configuration.VariableStepHeight.GetMetersOf(GridSize, 0, StandingHeight);
                CrouchHeight = Configuration.VariableCrouchHeight.GetMetersOf(GridSize, 0, StandingHeight);

                if (StepLength > maxStepLength)
                {
                    StaticWarn("Out of Bounds: Step Length", $"The step length of leg group {Configuration.Id} is out of bounds, maximum: {maxStepLength:f3}m");
                }

                if (StepHeight > StandingHeight)
                {
                    StaticWarn("Out of Bounds: Step Height", $"The step height of leg group {Configuration.Id} is out of bounds, maximum: {StandingHeight:f3}m");
                }
                else if (CrouchHeight + StepHeight > StandingHeight)
                {
                    StaticWarn("Out of Bounds: Crouch Height", $"The crouch height of leg group {Configuration.Id} is out of bounds, maximum {StandingHeight - StepHeight:f3}m");
                }
            }

            private double x, y, z;
            Vector3D leftSpot, rightSpot;
            private Vector3D max;

            public override void Update(MovementInfo info)
            {
                base.Update(info);
                Log("Step:", AnimationStep, AnimationStepOffset);

                IMyCameraBlock leftCamera = LeftCameras[0];
                leftCamera.EnableRaycast = true;
                MyDetectedEntityInfo leftHit = leftCamera.Raycast(20);
                if (!leftHit.IsEmpty() && !ignoreEntities.Contains(leftHit.Type))
                {
                    leftSpot = leftHit.HitPosition.Value;
                }
                double leftDifference = Vector3D.Dot(baseGravity.Normalized(), baseCameraSpot) - Vector3D.Dot(baseGravity.Normalized(), leftSpot); //baseCameraDistance - leftDistance;

                Log("left difference:", baseCameraSpot, leftSpot, baseGravity, leftDifference);

                // left
                x =
                    AnimationDirectionMultiplier * Math.Sin(2 * AnimationStep * Math.PI) * StepLength * -info.Walk
                    + XOffset;
                y = YOffset
                    + StandingHeight - leftDifference
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
                LegAngles leftAngles = InverseKinematics.Calculate2Joint2D(ThighLength, CalfLength, x, y);

                double len = Math.Sqrt(Math.Pow(y, 2) + Math.Pow(z, 2));
                double strafe = Math.Asin(-z / len);

                leftAngles.StrafeDegrees = strafe.ToDegrees();

                max = Vector3D.Max(max, new Vector3D(x, y, z));
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
                Log("max   :", max);

                IMyCameraBlock rightCamera = RightCameras[0];
                rightCamera.EnableRaycast = true;
                MyDetectedEntityInfo rightHit = rightCamera.Raycast(20);
                if (!rightHit.IsEmpty() && !ignoreEntities.Contains(rightHit.Type))
                {
                    rightSpot = rightHit.HitPosition.Value;
                }
                double rightDifference = Vector3D.Dot(baseGravity.Normalized(), baseCameraSpot) - Vector3D.Dot(baseGravity.Normalized(), rightSpot); //baseCameraDistance - leftDistance;
                Log("right difference:", baseCameraSpot, rightSpot, baseGravity, rightDifference);

                // right
                x =
                    AnimationDirectionMultiplier * Math.Sin(2 * AnimationStepOffset * Math.PI) * StepLength * -info.Walk
                    + XOffset;
                y = YOffset
                    + StandingHeight - rightDifference
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
                LegAngles rightAngles = InverseKinematics.Calculate2Joint2D(ThighLength, CalfLength, x, y);

                len = Math.Sqrt(Math.Pow(y, 2) + Math.Pow(z, 2));
                strafe = Math.Asin(-z / len);

                rightAngles.StrafeDegrees = strafe.ToDegrees();

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

                SetAngles(
                    LegAnglesOffset * LeftAnglesMultiplier + LegAnglesMultiplier * LeftAnglesMultiplier * leftAngles,
                    LegAnglesOffset * RightAnglesMultiplier + LegAnglesMultiplier * RightAnglesMultiplier * rightAngles
                );
            }*/
        }
    }
}
