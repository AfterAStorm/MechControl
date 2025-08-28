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
            protected virtual LegAngles LegAnglesMultiplier => LegAngles.One;
            protected virtual LegAngles LeftAnglesMultiplier => new LegAngles(-1, 1, 1, 1, 1);
            protected virtual LegAngles RightAnglesMultiplier => new LegAngles(1, 1, 1, 1, 1);

            float Radius, RadiusAnkle;

            float XOffset;
            float YOffset;
            float ZOffset;

            float StandingHeight, StandingDistance;
            float StepLength;
            float StepHeight;
            float CrouchHeight;

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
                
                if (StandingHeight > radiusAnkle)
                {
                    StaticWarn("Out of Bounds: Standing Height", $"The standing height of leg group {Configuration.Id} is out of bounds, current/maximum: {StandingHeight:f3}m/{radius:f3}m");
                }

                // x^2 + y^2 = r^2
                float maxStepLength = (float)Math.Sqrt(Math.Pow(radiusAnkle, 2) - Math.Pow(StandingHeight, 2)); // sqrt(r^2 - y^2) = x

                StepLength = Configuration.VariableStepLength.GetMetersOf(GridSize, 0, maxStepLength);
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

            private double x, y;

            public override void Update(MovementInfo info)
            {
                base.Update(info);
                Log("Step:", AnimationStep, AnimationStepOffset);

                Log("StandingHeight:", StandingHeight);
                Log("StandingDistance:", StandingDistance);
                //Log("StrafeDistance:", StrafeDistance);
                Log("StepLength:", StepLength);
                Log("StepHeight:", StepHeight);
                Log("CrouchHeight:", CrouchHeight);
                Log("Radius:", Radius);
                Log("XOffset:", XOffset);
                Log("YOffset:", YOffset);
                Log("ZOffset:", ZOffset);

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
                    + StandingHeight
                    - CrouchHeight * CrouchWaitTime
                    - Math.Max(Math.Sin(2 * AnimationStep * Math.PI + Math.PI / 2d), 0) * StepHeight * Math.Abs(AbsMax(info.Walk, AbsMax(info.Strafe, info.Turn)));

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
                Log("Left  Target:");
                Log("X:", x);
                Log("Y:", y);
                Log("Left  Angles:");
                Log("Hip :", leftAngles.HipDegrees);
                Log("Knee:", leftAngles.KneeDegrees);
                Log("Foot:", leftAngles.FeetDegrees);
                Log("Quad:", leftAngles.QuadDegrees);

                // right

                // a^2 + b^2 = c^2
                // a^2 = c^2 - b^2

                double distanceFromCenter = Math.Sqrt(Math.Pow(AnkleLength, 2) - Math.Pow(StandingDistance, 2));
                Log("distanceFromCenter:", distanceFromCenter);

                x =
                    AnimationDirectionMultiplier * Math.Sin(2 * AnimationStepOffset * Math.PI) * StepLength * -info.Walk
                    - XOffset;
                y = YOffset
                    + StandingHeight
                    - CrouchHeight * CrouchWaitTime
                    - Math.Max(Math.Sin(2 * AnimationStepOffset * Math.PI + Math.PI / 2d), 0) * StepHeight * Math.Abs(AbsMax(info.Walk, AbsMax(info.Strafe, info.Turn)));

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
                Log("Right Angles:");
                Log("Hip :", rightAngles.HipDegrees);
                Log("Knee:", rightAngles.KneeDegrees);
                Log("Foot:", rightAngles.FeetDegrees);
                Log("Quad:", rightAngles.QuadDegrees);

                SetAngles(
                    flyingAngles * LeftAnglesMultiplier  + LegAnglesMultiplier * LeftAnglesMultiplier  * leftAngles ,
                    flyingAngles * RightAnglesMultiplier + LegAnglesMultiplier * RightAnglesMultiplier * rightAngles
                );
                UpdateHydraulics();
            }
        }
    }
}
