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
using static IngameScript.Program;

namespace IngameScript
{
    partial class Program
    {
        public class SpideroidLegGroup : QuadLegGroup
        {
            //public override double AnimationSpeedMultiplier => -1;
            //protected void LegAngles LegAnglesOffset => new LegAngles(0, 90, 0, 0);
            protected virtual LegAngles LocalLegAnglesOffset => new LegAngles(0, 90, 0, 0);

            protected float StandingHeight;
            protected float StandingDistance;
            protected float StrafeDistance;
            protected float StepLength;
            protected float StepHeight;
            protected float CrouchHeight;
            protected float Radius;

            protected float XOffset;
            protected float YOffset;
            protected float ZOffset;

            public override void Initialize()
            {
                base.Initialize();

                Radius = (float)(CalfLength + AnkleLength);
                StandingDistance = Configuration.VariableStandingDistance.GetMetersOf(GridSize, 0, Radius);
                float remainingRadius = Radius - StandingDistance;
                StandingHeight = Configuration.VariableStandingHeight.GetMetersOf(GridSize, 0, Radius);

                if (StandingDistance > Radius)
                {
                    StaticWarn("Out of Bounds: Standing Height", $"The standing height of leg group {Configuration.Id} is out of bounds, maximum: {Radius:f3}m");
                }

                ZOffset = Configuration.VariableZOffset.GetMetersOf(GridSize, 0, Radius).AlwaysANumber();

                // x^2 + y^2 + z^2 = r^2
                // account for standing height
                Radius = (float)Math.Sqrt(Math.Pow(Radius, 2) - Math.Pow(StandingHeight, 2));

                // x^2 + y^2 = r^2
                float maxLength = (float)Math.Sqrt(Math.Pow(Radius, 2) - Math.Pow(StandingDistance, 2)).AlwaysANumber(); // sqrt(r^2 - y^2) = x

                if (Radius < StandingDistance)
                {
                    StaticWarn("Out of Bounds: Standing Distance", $"The standing distance of leg group {Configuration.Id} is out of bounds, current/maximum: {StandingDistance:f3}m/{Radius:f3}m");
                }

                StepLength = Configuration.VariableStepLength.GetMetersOf(GridSize, 0, maxLength);
                XOffset = Configuration.VariableXOffset.GetMetersOf(GridSize, 0, maxLength);
                YOffset = Configuration.VariableYOffset.GetMetersOf(GridSize, 0, maxLength);
                StepHeight = Configuration.VariableStepHeight.GetMetersOf(GridSize, 0, maxLength);
                StrafeDistance = Configuration.VariableStrafeDistance.GetMetersOf(GridSize, 0, remainingRadius);
                CrouchHeight = Configuration.VariableCrouchHeight.GetMetersOf(GridSize, 0, maxLength);

                if (StepLength > maxLength)
                {
                    StaticWarn("Out of Bounds: Step Length", $"The step length of leg group {Configuration.Id} is out of bounds, current/maximum: {StepLength:f3}m/{maxLength:f3}m");
                }

                if (StepHeight > maxLength)
                {
                    StaticWarn("Out of Bounds: Step Height", $"The step height of leg group {Configuration.Id} is out of bounds, current/maximum: {StepHeight:f3}m/{maxLength:f3}m");
                }
                else if (CrouchHeight + StepHeight > maxLength)
                {
                    StaticWarn("Out of Bounds: Crouch Height", $"The crouch height of leg group {Configuration.Id} is out of bounds, current/maximum {CrouchHeight + StepHeight:f3}m/{maxLength - StepHeight:f3}m");
                }
            }

            // y: up/down
            // x: forward/back
            // z: left/right
            protected double x, y, z;

            public override void Update(MovementInfo info)
            {
                base.Update(info);
                Log("Step:", AnimationStep, AnimationStepOffset);

                Log("StandingHeight:", StandingHeight);
                Log("StandingDistance:", StandingDistance);
                Log("StrafeDistance:", StrafeDistance);
                Log("StepLength:", StepLength);
                Log("StepHeight:", StepHeight);
                Log("CrouchHeight:", CrouchHeight);
                Log("Radius:", Radius);
                Log("XOffset:", XOffset);
                Log("YOffset:", YOffset);
                Log("ZOffset:", ZOffset);

                var cameraOffsets = UpdateCameras();

                // left
                x = XOffset * AnimationDirectionMultiplier
                    + AnimationDirectionMultiplier * Math.Sin(2 * AnimationStep * Math.PI) * StepLength * AbsMax(-info.Walk, info.Turn) * (AbsMax(info.Walk, info.Turn) == info.Turn ? -1 : 1);

                y = YOffset
                    - cameraOffsets.Item1
                    + StandingHeight
                    - CrouchHeight * CrouchWaitTime
                    - Math.Max(-Math.Sin(2 * AnimationStep * Math.PI + Math.PI / 2d), 0) * StepHeight * Math.Abs(AbsMax(info.Walk, AbsMax(info.Strafe, info.Turn)));

                z = ZOffset
                    + StandingDistance
                    + (Math.Sign(info.Strafe) * Math.Sin(2 * AnimationStep * Math.PI)) * StrafeDistance * Math.Abs(info.Strafe);
                    //+ StrafeDistance * Math.Abs(info.Strafe);

                if (customTarget != Vector3D.Zero)
                {
                    x = customTarget.X;
                    y = customTarget.Y;
                    z = customTarget.Z;
                }
                
                LegAngles leftAngles = InverseKinematics.Calculate2Joint3D(CalfLength, AnkleLength, y, z, x);
                leftAngles.QuadDegrees = -leftAngles.KneeDegrees - leftAngles.FeetDegrees + 90;
                //right.QuadDegrees = -right.KneeDegrees - right.FeetDegrees + 90;
                Log("Left  Target:");
                Log("X:", x);
                Log("Y:", y);
                Log("Z:", z);
                Log("Left  Angles:");
                Log("Hip :", leftAngles.HipDegrees);
                Log("Knee:", leftAngles.KneeDegrees);
                Log("Foot:", leftAngles.FeetDegrees);
                Log("Quad:", leftAngles.QuadDegrees);

                // right
                x = XOffset * AnimationDirectionMultiplier
                    + AnimationDirectionMultiplier * Math.Sin(2 * AnimationStepOffset * Math.PI) * StepLength * AbsMax(info.Walk, info.Turn) * (AbsMax(info.Walk, info.Turn) == info.Turn ? 1 : -1);

                y = YOffset
                    - cameraOffsets.Item2
                    + StandingHeight
                    - CrouchHeight * CrouchWaitTime
                    - Math.Max(-Math.Sin(2 * AnimationStepOffset * Math.PI + Math.PI / 2d), 0) * StepHeight * Math.Abs(AbsMax(info.Walk, AbsMax(info.Strafe, info.Turn)));

                z = ZOffset
                    + StandingDistance
                    + (-Math.Sign(info.Strafe) * Math.Sin(2 * AnimationStepOffset * Math.PI)) * StrafeDistance / 2f * Math.Abs(info.Strafe)
                    + StrafeDistance * Math.Abs(info.Strafe);

                if (customTarget != Vector3D.Zero)
                {
                    x = customTarget.X;
                    y = customTarget.Y;
                    z = customTarget.Z;
                }
                LegAngles rightAngles = InverseKinematics.Calculate2Joint3D(CalfLength, AnkleLength, y, z, -x);
                rightAngles.QuadDegrees = -rightAngles.KneeDegrees - rightAngles.FeetDegrees + 90;
                Log("Right Target:");
                Log("X:", x);
                Log("Y:", y);
                Log("Z:", z);
                Log("Right Angles:");
                Log("Hip :", rightAngles.HipDegrees);
                Log("Knee:", rightAngles.KneeDegrees);
                Log("Foot:", rightAngles.FeetDegrees);
                Log("Quad:", rightAngles.QuadDegrees);

                SetAngles(
                    LegAnglesOffset + LocalLegAnglesOffset + leftAngles,
                    LegAnglesOffset * new LegAngles(-1, 1, 1, 1, 1) + LocalLegAnglesOffset + rightAngles
                );
                UpdateHydraulics();

                foreach (var mag in LeftMagnets)
                {
                    mag.AutoLock = false;
                    /*if ((AnimationStep > .5d && AnimationStep < .75d) || (AnimationStep > 0d && AnimationStep < 0.25d))
                    {
                        mag.Unlock();
                    }
                    else
                    {
                        mag.Lock();
                    }*/
                    mag.Unlock();
                }
                foreach (var mag in RightMagnets)
                {
                    mag.AutoLock = false;
                    mag.Unlock();
                    /*if ((new Random()).NextDouble() > 0.5)
                    {
                        mag.Lock();
                    }
                    else mag.Unlock();*/
                    /*if ((AnimationStepOffset > .5d && AnimationStepOffset < .75d) || (AnimationStepOffset > 0d && AnimationStepOffset < 0.25d))
                    {
                        mag.ResetAutoLock();
                        mag.Unlock();
                    }
                    else
                    {
                        mag.Lock();
                    }*/
                }
            }
        }
    }
}
