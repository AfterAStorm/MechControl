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

        static double testLegX = 0;
        static double testLegY = 0;
        static double testLegZ = 45;

        public class TestLegGroup : LegGroup
        {
            protected virtual LegAngles LegAnglesMultiplier => LegAngles.One;
            protected virtual LegAngles LeftAnglesMultiplier => new LegAngles(-1, 1, 1, 1, 1);
            protected virtual LegAngles RightAnglesMultiplier => new LegAngles(1, 1, 1, 1, -1);

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
                    leftX += Math.Max(-Math.Sin(Math.PI * 2 * leftStep + Math.PI / 2), 0) * .25 * Configuration.StepHeight;
                    rightX += Math.Max(-Math.Sin(Math.PI * 2 * rightStep + Math.PI / 2), 0) * .25 * Configuration.StepHeight;
                    leftZ += -Math.Sin(Math.PI * 2 * leftStep) * .5 * Configuration.StepLength;
                    rightZ += -Math.Sin(Math.PI * 2 * rightStep) * .5 * Configuration.StepLength;
                    /*leftY -= Math.Max(-Math.Sin(Math.PI * 2 * leftStep + Math.PI / 2), 0) * deltaStepHeight;
                    rightY -= Math.Max(-Math.Sin(Math.PI * 2 * rightStep + Math.PI / 2), 0) * deltaStepHeight;*/
                }

                if (info.Turning)
                {
                    OffsetLegs = true;
                    leftX += Math.Max(-Math.Sin(Math.PI * 2 * leftStep + Math.PI / 2), 0) * .25 * Configuration.StepHeight;
                    rightX += Math.Max(-Math.Sin(Math.PI * 2 * rightStep + Math.PI / 2), 0) * .25 * Configuration.StepHeight;
                    leftZ += -Math.Sin(Math.PI * 2 * leftStep) * .5 * Configuration.StepLength;
                    rightZ += Math.Sin(Math.PI * 2 * rightStep) * .5 * Configuration.StepLength;
                }

                /*if (info.Strafing)
                {
                    leftStrafe = -Math.Max(Math.Sin(Math.PI * 2 * -AnimationStep * inverseMultiplier * 3 / 4), 0) * 10;
                    rightStrafe = Math.Max(Math.Sin(Math.PI * 2 * -AnimationStep * inverseMultiplier * 3 / 4 - Math.PI / 2), 0) * 10;
                }*/

                //leftY -= deltaCrouchHeight;

                rightY -= deltaCrouchHeight;

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
                    left * LeftAnglesMultiplier * LegAnglesMultiplier,
                    right * RightAnglesMultiplier * LegAnglesMultiplier
                );
                HandlePistons();
            }
        }
    }
}
