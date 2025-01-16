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
            protected virtual LegAngles LegAnglesMultiplier => LegAngles.One;
            protected virtual LegAngles LeftAnglesMultiplier  => new LegAngles(-1, 1, 1, 0, 1);
            protected virtual LegAngles RightAnglesMultiplier => new LegAngles(1, 1, 1, 0, -1);

            public override void Update(MovementInfo info)
            {
                base.Update(info);

                // get lengths
                double thighLength = CalculatedThighLength;
                double calfLength = CalculatedCalfLength;

                // calculate offsets
                double baseHeight = InverseKinematics.FindDistanceWhereKnee(thighLength, calfLength, MathHelper.ToRadians(45)) * Configuration.StandingHeight; //Math.Sqrt(2) * 1.25 * Configuration.StandingHeight;
                double maxHeight = InverseKinematics.FindDistanceWhereKnee(thighLength, calfLength, MathHelper.ToRadians(80));
                double crouchHeight = InverseKinematics.FindDistanceWhereKnee(thighLength, calfLength, MathHelper.ToRadians(90));

                double deltaStepHeight   = -(maxHeight - baseHeight); // find the difference to reach said target angle
                double deltaCrouchHeight = -(crouchHeight - baseHeight) * CrouchWaitTime;

                // calculate values
                double inverseMultiplier = LegAnglesMultiplier.HipDegrees;
                double leftX  = Configuration.Lean * inverseMultiplier;
                double rightX = Configuration.Lean * inverseMultiplier;
                double leftY  = baseHeight;
                double rightY = baseHeight;
                double leftStrafe  = 0;
                double rightStrafe = 0;

                double leftStep = AnimationStep + IdOffset;
                double rightStep = AnimationStepOffset + IdOffset;

                if (info.Idle)
                {

                }

                if (info.Walking)
                {
                    OffsetLegs = true;
                    leftX += -Math.Sin(Math.PI * 2 * leftStep) * .5 * Configuration.StepLength;
                    rightX += -Math.Sin(Math.PI * 2 * rightStep) * .5 * Configuration.StepLength;
                    leftY -= Math.Max(-Math.Sin(Math.PI * 2 * leftStep + Math.PI / 2), 0) * deltaStepHeight * Configuration.StepHeight;
                    rightY -= Math.Max(-Math.Sin(Math.PI * 2 * rightStep + Math.PI / 2), 0) * deltaStepHeight * Configuration.StepHeight;
                }

                if (info.Turning)
                {
                    OffsetLegs = true;
                    leftY -= Math.Max(Math.Sin(Math.PI * 2 * leftStep), 0) * deltaStepHeight;
                    rightY -= Math.Max(Math.Sin(Math.PI * 2 * rightStep), 0) * deltaStepHeight;
                }

                if (info.Strafing)
                {
                    leftStrafe = -Math.Max(Math.Sin(Math.PI * 2 * -AnimationStep * inverseMultiplier * 3 / 4), 0) * 10;
                    rightStrafe = Math.Max(Math.Sin(Math.PI * 2 * -AnimationStep * inverseMultiplier * 3 / 4 - Math.PI / 2), 0) * 10;
                }

                leftY -= deltaCrouchHeight;
                rightY -= deltaCrouchHeight;

                // calculate ik
                Log($"left: {leftX} {leftY}");
                Log($"right: {rightX} {rightY}");
                LegAngles left  = InverseKinematics.Calculate2Joint2D(thighLength, calfLength, leftX , leftY );
                LegAngles right = InverseKinematics.Calculate2Joint2D(thighLength, calfLength, rightX, rightY);

                left.StrafeDegrees = leftStrafe;
                right.StrafeDegrees = rightStrafe;

                SetAngles(
                    left  * LeftAnglesMultiplier  * LegAnglesMultiplier,
                    right * RightAnglesMultiplier * LegAnglesMultiplier
                );
                HandlePistons();
            }
        }
    }
}
