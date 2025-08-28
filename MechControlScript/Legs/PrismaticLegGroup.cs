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
        public class PrismaticLegGroup : TriLegGroup
        {
            protected virtual LegAngles LegAnglesMultiplier => LegAngles.One;
            protected virtual LegAngles LeftAnglesMultiplier => new LegAngles(-1, 1, 1, 0, 1);
            protected virtual LegAngles RightAnglesMultiplier => new LegAngles(1, 1, 1, 0, 1);

            protected List<IMyPistonBase> LeftKneePistons = new List<IMyPistonBase>();
            protected List<IMyPistonBase> RightKneePistons = new List<IMyPistonBase>();

            float XOffset;
            float YOffset;
            float ZOffset;

            float StrafeDistance;
            float StandingHeight;
            float StepLength;
            float StepHeight;
            float CrouchHeight;

            public override bool AddBlock(FetchedBlock block)
            {
                switch (block.Type)
                {
                    case BlockType.Knee:
                        if (!(block.Block is IMyPistonBase))
                            return false;
                        AddLeftRightBlock(LeftKneePistons, RightKneePistons, block.Block as IMyPistonBase, block.Side);
                        return true;
                }
                return base.AddBlock(block);
            }

            public override void Initialize()
            {
                base.Initialize();
                ThighLength = Configuration.ThighLength ?? Math.Max(FindJointLength(LeftHipJoints, LeftKneePistons), FindJointLength(RightHipJoints, RightKneePistons));
                CalfLength = RightKneePistons.Count * RightKneePistons[0].HighestPosition;

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
            private Vector3D max;

            public override void Update(MovementInfo info)
            {
                base.Update(info);
                Log("# L/R Knee Pis:", LeftKneePistons.Count, "/", RightKneePistons.Count);
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
                    - cameraOffsetTween.Item1
                    + StandingHeight
                    - CrouchHeight * CrouchWaitTime
                    - Math.Max(Math.Sin(2 * AnimationStep * Math.PI + Math.PI / 2d), 0) * (StepHeight - cameraOffsetTween.Item1) * Math.Abs(AbsMax(info.Walk, AbsMax(info.Strafe, info.Turn)));
                z = ZOffset
                    + (-Math.Sign(info.Strafe) * Math.Sin(2 * AnimationStep * Math.PI)) * StrafeDistance * Math.Abs(info.Strafe)
                    + StrafeDistance * Math.Abs(info.Strafe);

                if (customTarget != Vector3D.Zero)
                {
                    x = customTarget.X;
                    y = customTarget.Y;
                }
                float pistonOffset = RightKneePistons.Count * 1 * RightKneePistons[0].CubeGrid.GridSize + 0.0315f * RightKneePistons.Count;
                Log($"min: {RightKneePistons[0].LowestPosition}");
                y = Math.Sqrt(Math.Pow(y, 2) + Math.Pow(x, 2) + Math.Pow(z, 2));
                foreach (var piston in LeftKneePistons)
                {
                    piston.MoveToPosition((float)y - ThighLength - pistonOffset, 10f);
                }
                double angle = Math.Atan(x / y).ToDegrees();
                LegAngles leftAngles = new LegAngles(angle, 0, -angle);//InverseKinematics.Calculate2Joint2D(ThighLength, CalfLength, x, y);

                double len = Math.Sqrt(Math.Pow(y, 2) + Math.Pow(z, 2));
                double strafe = Math.Asin(-z / len);

                leftAngles.StrafeDegrees = strafe.ToDegrees();

                max = Vector3D.Max(max, new Vector3D(x, y, z));
                Log("Left  Target:");
                Log("X:", x);
                Log("Y:", y);
                Log("Z:", z);

                // right
                x =
                    AnimationDirectionMultiplier * Math.Sin(2 * AnimationStepOffset * Math.PI) * StepLength * -info.Walk
                    - XOffset;
                y = YOffset
                    - cameraOffsetTween.Item2
                    + StandingHeight
                    - CrouchHeight * CrouchWaitTime
                    - Math.Max(Math.Sin(2 * AnimationStepOffset * Math.PI + Math.PI / 2d), 0) * (StepHeight - cameraOffsetTween.Item2) * Math.Abs(AbsMax(info.Walk, AbsMax(info.Strafe, info.Turn)));
                z = ZOffset
                    + (-Math.Sign(info.Strafe) * Math.Sin(2 * AnimationStep * Math.PI)) * StrafeDistance * Math.Abs(info.Strafe)
                    + StrafeDistance * Math.Abs(info.Strafe);

                if (customTarget != Vector3D.Zero)
                {
                    x = customTarget.X;
                    y = customTarget.Y;
                }
                y = Math.Sqrt(Math.Pow(y, 2) + Math.Pow(x, 2) + Math.Pow(z, 2));
                foreach (var piston in RightKneePistons)
                {
                    piston.MoveToPosition((float)y - ThighLength - pistonOffset, 10f);
                }
                angle = Math.Atan(x / y).ToDegrees();
                LegAngles rightAngles = new LegAngles(angle, 0, -angle); //InverseKinematics.Calculate2Joint2D(ThighLength, CalfLength, x, y);

                len = Math.Sqrt(Math.Pow(y, 2) + Math.Pow(z, 2));
                strafe = Math.Asin(-z / len);

                rightAngles.StrafeDegrees = strafe.ToDegrees();

                Log("Right Target:");
                Log("X:", x);
                Log("Y:", y);
                Log("Z:", z);

                LegAngles leftAnglesFinal  = flyingAngles * LeftAnglesMultiplier  * new LegAngles(1, 1, 1, 1, -1) + LegAnglesOffset * LeftAnglesMultiplier  + LegAnglesMultiplier * LeftAnglesMultiplier  * leftAngles;
                LegAngles rightAnglesFinal = flyingAngles * RightAnglesMultiplier                                 + LegAnglesOffset * RightAnglesMultiplier + LegAnglesMultiplier * RightAnglesMultiplier * rightAngles;
                SetAngles(leftAnglesFinal, rightAnglesFinal);
                UpdateHydraulics();
            }
        }
    }
}
