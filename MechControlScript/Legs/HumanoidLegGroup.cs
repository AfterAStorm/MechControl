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

            public new LegConfiguration DefaultConfiguration = new LegConfiguration()
            {
                VariableStandingHeight = new JointVariable(JointVariableType.Percentage, 90f),
                VariableXOffset = new JointVariable(JointVariableType.Percentage, 0),
                VariableZOffset = new JointVariable(JointVariableType.Percentage, 0),
                VariableStepLength = new JointVariable(JointVariableType.Percentage, 30f),
                VariableStepHeight = new JointVariable(JointVariableType.Percentage, 10f),
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
                    - XOffset * AnimationDirectionMultiplier;
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

                // right
                x =
                    AnimationDirectionMultiplier * Math.Sin(2 * AnimationStepOffset * Math.PI) * StepLength * -info.Walk
                    - XOffset * AnimationDirectionMultiplier;
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

                LegAngles leftAnglesFinal  = flyingAngles * LeftAnglesMultiplier  * new LegAngles(1, 1, 1, 1, -1) + LegAnglesOffset * LeftAnglesMultiplier  + LegAnglesMultiplier * LeftAnglesMultiplier  * leftAngles;
                LegAngles rightAnglesFinal = flyingAngles * RightAnglesMultiplier                                 + LegAnglesOffset * RightAnglesMultiplier + LegAnglesMultiplier * RightAnglesMultiplier * rightAngles;
                SetAngles(leftAnglesFinal, rightAnglesFinal);
                Log("set angles!");

                // hydraulics
                UpdateHydraulics();
                /*if (Hydraulics.Count == 0)
                    return; // save perf
                            // TODO: cache groups and use "getter" Func to get leg angles instead of creating new objects every iteration?
                List<HydraulicGroup> leftHydraulics = new List<HydraulicGroup>()
                {
                    new HydraulicGroup(LeftHipJoints[0].Stator, leftAnglesFinal.HipRadians),
                    new HydraulicGroup(LeftKneeJoints[0].Stator, leftAnglesFinal.KneeRadians),
                    new HydraulicGroup(LeftFootJoints[0].Stator, leftAnglesFinal.FeetRadians),
                };
                List<HydraulicGroup> rightHydraulics = new List<HydraulicGroup>()
                {
                    new HydraulicGroup(RightHipJoints[0].Stator, rightAnglesFinal.HipRadians),
                    new HydraulicGroup(RightKneeJoints[0].Stator, rightAnglesFinal.KneeRadians),
                    new HydraulicGroup(RightFootJoints[0].Stator, rightAnglesFinal.FeetRadians),
                }; //* /

                Log("has hydraulics!");
                foreach (var hy in Hydraulics.Where(h => h.Valid))
                {
                    Log($"Update {hy.Source.Name}");
                    Log($"Update {hy.TopStator.CustomName} {hy.BottomStator.CustomName}");
                    hy.TopPosition = hy.TopStator.Top.WorldMatrix;
                    hy.BottomPosition = hy.BottomStator.Top.WorldMatrix;
                    HydraulicGroup group;
                    for (int index = 0; index < leftHydraulics.Count; index++)
                    {
                        group = leftHydraulics[index];
                        if (group.Grid.Equals(hy.TopGrid))
                        {
                            Log($"found top grid! {hy.Source.Name} {index}");
                            HandleHydraulic(hy, leftHydraulics, index, true);
                        }
                        if (group.Grid.Equals(hy.BottomGrid))
                        {
                            Log($"found bottom grid! {hy.Source.Name} {index}");
                            HandleHydraulic(hy, leftHydraulics, index, false);
                        }
                        group = rightHydraulics[index];
                        if (group.Grid.Equals(hy.TopGrid))
                        {
                            Log($"found top grid! {hy.Source.Name} {index}");
                            HandleHydraulic(hy, rightHydraulics, index, true);
                        }
                        if (group.Grid.Equals(hy.BottomGrid))
                        {
                            Log($"found bottom grid! {hy.Source.Name} {index}");
                            HandleHydraulic(hy, rightHydraulics, index, false);
                        }
                    }
                    /*Log("distance:", Vector3D.Distance(hy.TopPosition.Translation, hy.BottomPosition.Translation), Math.Abs(Vector3D.Dot(Base6Directions.GetIntVector(hy.TopStator.Top.Orientation.Up), hy.TopPosition.Translation) - Vector3D.Dot(Base6Directions.GetIntVector(hy.TopStator.Top.Orientation.Up), hy.BottomPosition.Translation)));
                    Log("dist:", hy.BottomStator.CustomName, hy.BottomDistance, hy.TopStator.CustomName, hy.TopDistance, "inter:", hy.IntermediateDistance);* /
                    hy.Update();
                    // assume grids
                    // bottom = KR --> FR
                    // top = HR --> KR
                    /*double angle1 = (rightAnglesFinal.HipRadians - RightHipJoints[0].Stator.Angle);
                    MatrixD pos1 = GetRotatedPosition(hy.TopStator.WorldMatrix, RightHipJoints[0].Stator.WorldMatrix, angle1, Vector3D.Up);//RightHipJoints[0].Stator.WorldMatrix.Up);

                    MatrixD pos2a = GetRotatedPosition(RightKneeJoints[0].Stator.WorldMatrix, RightHipJoints[0].Stator.WorldMatrix, angle1, Vector3D.Up);//, RightHipJoints[0].Stator.WorldMatrix.Up);
                    MatrixD pos2b = GetRotatedPosition(hy.BottomStator.WorldMatrix, RightHipJoints[0].Stator.WorldMatrix, angle1, Vector3D.Up);//, RightHipJoints[0].Stator.WorldMatrix.Up);
                    double angle2 = (rightAnglesFinal.KneeRadians - RightKneeJoints[0].Stator.Angle);
                    MatrixD pos2 = GetRotatedPosition(pos2b, pos2a, angle2, Vector3D.Right);//, pos2b.Up);

                    Log("angles", angle1, angle2);
                    Log("pos:", pos1.Translation - hy.TopStator.WorldMatrix.Translation);
                    Log("pos:", pos2.Translation - hy.TopStator.WorldMatrix.Translation);
                    Log("pos1 - pos2:", Vector3D.Distance(pos1.Translation, pos2.Translation));
                    double distance = Vector3D.Distance(pos1.Translation, pos2.Translation) - 1.5d;
                    hy.Piston.MoveToPosition((float)distance, 10f);* /
                } //*/
            }

            /*void HandleHydraulic(Hydraulic hy, List<HydraulicGroup> groups, int index, bool top)
            {
                var grid = top ? hy.TopGrid : hy.BottomGrid;
                var stator = top ? hy.TopStator : hy.BottomStator;

                MatrixD statorPos = stator.WorldMatrix;
                MatrixD referencePos = MatrixD.Identity;
                for (int i = 0; i < index + 1; i++)
                {
                    var group = groups[i];
                    var angle = group.Target; // already rotated in HydraulicGroup :3 (offset from rotor's current rotation)

                    if (i > 0)
                    {
                        // rotate reference
                        referencePos = GetRotatedPosition(group.Reference.WorldMatrix, referencePos, groups[i - 1].Target, groups[i - 1].Axis);
                    }
                    else
                        referencePos = group.Reference.WorldMatrix;

                    statorPos = GetRotatedPosition(statorPos, referencePos, angle, group.Axis);

                    if (i == index)
                    {
                        // target group
                        if (top)
                            hy.TopPosition = statorPos;
                        else
                            hy.BottomPosition = statorPos;
                    }
                }
            }

            MatrixD GetRotatedPosition(MatrixD position, MatrixD pivot, double angle, Vector3D axis) // radians
            {
                return position * MatrixD.Invert(pivot) * MatrixD.CreateFromAxisAngle(axis, angle) * pivot; //pivot * MatrixD.CreateFromAxisAngle(axis, angle) * inverted * position; //Vector3D.Transform(position, transform);
            }*/
        }
    }
}
