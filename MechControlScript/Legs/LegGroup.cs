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
        public class LegGroup : JointGroup
        {

            #region # - Properties

            public new LegConfiguration Configuration;

            public List<LegJoint> LeftHipStators = new List<LegJoint>();
            public List<LegJoint> RightHipStators = new List<LegJoint>();

            public List<LegJoint> LeftKneeStators = new List<LegJoint>();
            public List<LegJoint> RightKneeStators = new List<LegJoint>();

            public List<LegJoint> LeftFootStators = new List<LegJoint>();
            public List<LegJoint> RightFootStators = new List<LegJoint>();

            public List<LegJoint> LeftQuadStators = new List<LegJoint>();
            public List<LegJoint> RightQuadStators = new List<LegJoint>();

            public List<LegJoint> LeftStrafeStators = new List<LegJoint>();
            public List<LegJoint> RightStrafeStators = new List<LegJoint>();

            public List<FetchedBlock> LeftPistons = new List<FetchedBlock>();
            public List<FetchedBlock> RightPistons = new List<FetchedBlock>();

            public List<IMyLandingGear> LeftGears = new List<IMyLandingGear>();
            public List<IMyLandingGear> RightGears = new List<IMyLandingGear>();

            public List<IMyTerminalBlock> AllBlocks =>
                LeftHipStators.Concat(RightHipStators).Concat(LeftKneeStators).Concat(RightKneeStators).Concat(LeftFootStators).Concat(RightFootStators).Concat(LeftQuadStators).Concat(RightQuadStators).Select(j => j.Stator as IMyTerminalBlock).Concat(LeftPistons.Concat(RightPistons).Select(p => p.Block)).Concat(LeftGears.Concat(RightGears)).ToList();

            //public IMyCameraBlock[] InclineCameras; // TODO: use these, give them a purpose!

            protected double LastDelta = 1;
            public double AnimationStep = 0; // pff, who needes getters and setters?
            public double AnimationStepOffset => OffsetLegs ? (AnimationStep + .5).Modulo(1) : AnimationStep;
            public double CrouchWaitTime = 0; // extra property for stuffs that needs it
            public double IdOffset => Configuration.Id % 2 == 1 ? 0 : .5;
            public bool OffsetLegs = true;
            public Animation Animation = Animation.Idle;
            public double AnimationWaitTime = 0;
            public virtual double AnimationSpeedMultiplier => 1;

            public double CalculatedThighLength = 0;
            public double CalculatedCalfLength = 0;
            public double CalculatedQuadLength = 0;

            protected double HipInversedMultiplier = 1;
            protected double KneeInversedMultiplier = 1;
            protected double FeetInversedMultiplier = 1;
            protected double QuadInversedMultiplier = 1;

            #endregion

            #region # - Constructor

            public LegGroup() { }

            #endregion

            #region # - Methods

            public virtual void Initialize()
            {
                // Update multipliers
                HipInversedMultiplier = Configuration.HipsInverted ? -1 : 1;
                KneeInversedMultiplier = Configuration.KneesInverted ? -1 : 1;
                FeetInversedMultiplier = Configuration.FeetInverted ? -1 : 1;
                QuadInversedMultiplier = Configuration.QuadInverted ? -1 : 1;

                CalculatedThighLength = Configuration.ThighLength > 0 ? Configuration.ThighLength : FindThighLength();
                CalculatedCalfLength = Configuration.CalfLength > 0 ? Configuration.CalfLength : FindCalfLength(); // lower leg, or upper leg for spiders
                CalculatedQuadLength = Configuration.ThighLength > 0 ? Configuration.ThighLength : FindQuadLength(); // lower lower leg, or lower leg for spiders
            }

            public override void SetConfiguration(object config)
            {
                Configuration = (LegConfiguration)config;
            }

            protected virtual void SetAnglesOf(List<LegJoint> leftStators, List<LegJoint> rightStators, double leftAngle, double rightAngle, double offset)
            {
                // We could split this into ANOTHER method, but i don't believe it's worth it
                foreach (var motor in leftStators)
                    motor.SetAngle(leftAngle * motor.Configuration.InversedMultiplier + (-(motor.IsRotor ? offset : -offset) + motor.Configuration.Offset) * (motor.IsHinge ? 1 : -1));
                //SetJointAngle(motor, leftAngle * motor.Configuration.InversedMultiplier, offset + motor.Configuration.Offset);
                //motor.Stator.TargetVelocityRPM = (float)MathHelper.Clamp((leftAngle * motor.Configuration.InversedMultiplier).AbsoluteDegrees(motor.Stator.BlockDefinition.SubtypeName.Contains("Hinge")) - motor.Stator.Angle.ToDegrees() - offset - motor.Configuration.Offset, -MaxRPM, MaxRPM);
                foreach (var motor in rightStators)
                    motor.SetAngle(rightAngle * motor.Configuration.InversedMultiplier - ((motor.IsRotor ? offset : -offset) + motor.Configuration.Offset) * (motor.IsHinge ? 1 : -1));
                //SetJointAngle(motor, -rightAngle * motor.Configuration.InversedMultiplier, offset + motor.Configuration.Offset);
                //motor.Stator.TargetVelocityRPM = (float)MathHelper.Clamp((-rightAngle * motor.Configuration.InversedMultiplier).AbsoluteDegrees(motor.Stator.BlockDefinition.SubtypeName.Contains("Hinge")) - motor.Stator.Angle.ToDegrees() - offset - motor.Configuration.Offset, -MaxRPM, MaxRPM);
            }

            protected virtual void SetAnglesOf(List<LegJoint> stators, double angle, double offset)
            {
                foreach (var motor in stators)
                    motor.SetAngle((angle + /*(motor.IsRotor ?*/ offset /*: -offset)*/ + motor.Configuration.Offset) * motor.Configuration.InversedMultiplier);
            }

            /*protected virtual void SetAngles(double leftHipDegrees, double leftKneeDegrees, double leftFeetDegrees, double leftQuadDegrees, double rightHipDegrees, double rightKneeDegrees, double rightFeetDegrees, double rightQuadDegrees)
            {
                // The code documents itself!
                SetAnglesOf(LeftHipStators,     RightHipStators,    (leftHipDegrees  * HipInversedMultiplier),      (rightHipDegrees * HipInversedMultiplier),     Configuration.HipOffsets);
                SetAnglesOf(LeftKneeStators,    RightKneeStators,   (leftKneeDegrees * KneeInversedMultiplier),     (rightKneeDegrees * KneeInversedMultiplier),   Configuration.KneeOffsets + Configuration.HipOffsets);
                SetAnglesOf(LeftFootStators,    RightFootStators,   (leftFeetDegrees * FeetInversedMultiplier),     (rightFeetDegrees * FeetInversedMultiplier),   Configuration.FootOffsets + Configuration.KneeOffsets);
                SetAnglesOf(LeftQuadStators,    RightQuadStators,   (leftQuadDegrees * QuadInversedMultiplier),     (rightQuadDegrees * QuadInversedMultiplier),   Configuration.QuadOffsets + Configuration.FootOffsets);
            }*/

            protected virtual void SetAngles(LegAngles leftAngles, LegAngles rightAngles)
            {
                SetAnglesOf(LeftHipStators, leftAngles.HipDegrees, Configuration.HipOffsets); // +hip goes the right way
                SetAnglesOf(RightHipStators, rightAngles.HipDegrees, -Configuration.HipOffsets);

                SetAnglesOf(LeftKneeStators, leftAngles.KneeDegrees, -Configuration.KneeOffsets); //Configuration.HipOffsets + Configuration.KneeOffsets); // why is this different from hip
                SetAnglesOf(RightKneeStators, rightAngles.KneeDegrees, -Configuration.KneeOffsets);//-Configuration.HipOffsets + Configuration.KneeOffsets);

                SetAnglesOf(LeftFootStators, leftAngles.FeetDegrees, -Configuration.FootOffsets);// + Configuration.KneeOffsets); // what?
                SetAnglesOf(RightFootStators, rightAngles.FeetDegrees, -Configuration.FootOffsets);// + Configuration.KneeOffsets);

                SetAnglesOf(LeftQuadStators, leftAngles.QuadDegrees, -Configuration.QuadOffsets);
                SetAnglesOf(RightQuadStators, rightAngles.QuadDegrees, -Configuration.QuadOffsets);

                SetAnglesOf(LeftStrafeStators, leftAngles.StrafeDegrees, -Configuration.StrafeOffsets);
                SetAnglesOf(RightStrafeStators, rightAngles.StrafeDegrees, -Configuration.StrafeOffsets);
            }

            protected double FindThighLength()
            {
                if (LeftHipStators.Count == 0 || LeftKneeStators.Count == 0)
                {
                    if (RightHipStators.Count == 0 || RightKneeStators.Count == 0)
                        return 2.5d;
                    return (RightHipStators.First().Stator.GetPosition() - RightKneeStators.First().Stator.GetPosition()).Length();
                }
                return (LeftHipStators.First().Stator.GetPosition() - LeftKneeStators.First().Stator.GetPosition()).Length();
            }

            protected double FindCalfLength()
            {
                if (LeftFootStators.Count == 0 || LeftKneeStators.Count == 0)
                {
                    if (RightFootStators.Count == 0 || RightKneeStators.Count == 0)
                        return 2.5d;
                    return (RightFootStators.First().Stator.GetPosition() - RightKneeStators.First().Stator.GetPosition()).Length();
                }
                return (LeftFootStators.First().Stator.GetPosition() - LeftKneeStators.First().Stator.GetPosition()).Length();
            }

            protected double FindQuadLength()
            {
                if (LeftFootStators.Count == 0 || LeftQuadStators.Count == 0)
                {
                    if (RightFootStators.Count == 0 || RightQuadStators.Count == 0)
                        return 2.5d;
                    return (RightFootStators.First().Stator.GetPosition() - RightQuadStators.First().Stator.GetPosition()).Length();
                }
                return (LeftFootStators.First().Stator.GetPosition() - LeftQuadStators.First().Stator.GetPosition()).Length();
            }

            /// <summary>
            /// Used internally in each leg implementation, calculates each leg angle
            /// </summary>
            /// <param name="step"></param>
            /// <returns></returns>
            /// <exception cref="Exception"></exception>
            protected virtual LegAngles CalculateAngles(double step)
            {
                throw new Exception("CalculateAngles Not Implemented");
            }

            public virtual void Update(MovementInfo info)
            {
                Log($"- {GetType().Name} (group {Configuration.Id}) -");
                // Animate crouch
                if (!Animation.IsCrouch() && !info.Crouched)
                    CrouchWaitTime = Math.Max(0, jumping ? 0 : CrouchWaitTime - info.Delta * 2 * Configuration.CrouchSpeed * CrouchSpeed);
                else
                    CrouchWaitTime = Math.Min(1, CrouchWaitTime + info.Delta * 2 * Configuration.CrouchSpeed * CrouchSpeed);

                AnimationStep = (animationStepCounter * Configuration.AnimationSpeed * AnimationSpeedMultiplier).Modulo(1);
            }

            public virtual void Update(Vector3 forwardsDeltaVec, Vector3 movementVector, double delta)
            {
                double forwardsDelta = forwardsDeltaVec.Z;
                // Update multipliers, we should probably isolate this in a "Initialize" method or something
                HipInversedMultiplier = Configuration.HipsInverted ? -1 : 1;
                KneeInversedMultiplier = Configuration.KneesInverted ? -1 : 1;
                FeetInversedMultiplier = Configuration.FeetInverted ? -1 : 1;
                QuadInversedMultiplier = Configuration.QuadInverted ? -1 : 1;

                // If the legs should be offset or not, used for animation stuffs
                OffsetLegs = forwardsDelta != 0;

                if (OffsetLegs)
                    LastDelta = forwardsDelta;

                // Animate crouch
                if (!Animation.IsCrouch())
                    CrouchWaitTime = Math.Max(0, jumping ? 0 : CrouchWaitTime - delta * 2 * Configuration.CrouchSpeed);
                else
                    CrouchWaitTime = Math.Min(1, CrouchWaitTime + delta * 2 * Configuration.CrouchSpeed);

                // Update animation step
                double multiplier = forwardsDelta / Math.Abs(forwardsDelta);
                Log($"mul: {multiplier}");
                //if (!double.IsNaN(multiplier))
                AnimationStep = (animationStepCounter * Configuration.AnimationSpeed) % 4;
                //else
                //    AnimationStep += (!double.IsNaN(multiplier) ? forwardsDelta : delta * (LastDelta / Math.Abs(LastDelta)) / 2) * Configuration.AnimationSpeed;//delta * (!double.IsNaN(multiplier) ? multiplier : 1) * Configuration.AnimationSpeed;
                AnimationStep %= 4; // 0 to 3
            }

            #endregion

            #region Experimental

            int leftLegCounter = 0;
            int rightLegCounter = 0;

            private void HandlePistonGroup(List<FetchedBlock> pistons, List<LegJoint> hips, List<LegJoint> knees, List<LegJoint> feet, ref int counter)
            {
                foreach (var piston in pistons)
                {
                    var block = piston.Block as IMyPistonBase;
                    var speed = Math.Abs(block.Velocity) * (piston.Inverted ? -1 : 1);

                    double rpm = 0;
                    if (piston.Name.ToLower().Contains("h") && hips.Count > 0)
                        rpm += hips[0].Stator.TargetVelocityRPM;
                    else if (piston.Name.ToLower().Contains("k") && knees.Count > 0)
                        rpm += knees[0].Stator.TargetVelocityRPM;
                    else if (piston.Name.ToLower().Contains("f") && feet.Count > 0)
                        rpm += feet[0].Stator.TargetVelocityRPM;
                    if (rpm > 0)
                    {
                        counter = MathHelper.Clamp(counter + 1, -15, 15);
                        if (counter > 0)
                            block.Velocity = speed;
                    }
                    else
                    {
                        counter = MathHelper.Clamp(counter - 1, -15, 15);
                        if (counter < 0)
                            block.Velocity = speed;
                        block.Velocity = -speed;
                    }
                    block.Enabled = true;//Math.Abs(rpm) > .05;
                }
            }

            protected void HandlePistons(float multiplier = 1)
            {
                HandlePistonGroup(LeftPistons, LeftHipStators, LeftKneeStators, LeftFootStators, ref leftLegCounter);
                HandlePistonGroup(RightPistons, RightHipStators, RightKneeStators, RightFootStators, ref rightLegCounter);
            }

            #endregion
        }
    }
}