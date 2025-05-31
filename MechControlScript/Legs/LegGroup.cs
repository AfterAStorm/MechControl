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

            public List<LegJoint> AALeftHipStators = new List<LegJoint>();
            public List<LegJoint> AARightHipStators = new List<LegJoint>();

            public List<LegJoint> AALeftKneeStators = new List<LegJoint>();
            public List<LegJoint> AARightKneeStators = new List<LegJoint>();

            public List<LegJoint> AALeftFootStators = new List<LegJoint>();
            public List<LegJoint> AARightFootStators = new List<LegJoint>();

            public List<LegJoint> AALeftQuadStators = new List<LegJoint>();
            public List<LegJoint> AARightQuadStators = new List<LegJoint>();

            public List<LegJoint> AALeftStrafeStators = new List<LegJoint>();
            public List<LegJoint> AARightStrafeStators = new List<LegJoint>();

            public List<FetchedBlock> LeftPistons = new List<FetchedBlock>();
            public List<FetchedBlock> RightPistons = new List<FetchedBlock>();

            public List<IMyLandingGear> LeftGears = new List<IMyLandingGear>();
            public List<IMyLandingGear> RightGears = new List<IMyLandingGear>();

            public List<IMyTerminalBlock> AllBlocks =>
                AALeftHipStators.Concat(AARightHipStators).Concat(AALeftKneeStators).Concat(AARightKneeStators).Concat(AALeftFootStators).Concat(AARightFootStators).Concat(AALeftQuadStators).Concat(AARightQuadStators).Select(j => j.Stator as IMyTerminalBlock).Concat(LeftPistons.Concat(RightPistons).Select(p => p.Block)).Concat(LeftGears.Concat(RightGears)).ToList();

            //public IMyCameraBlock[] InclineCameras; // TODO: use these, give them a purpose!

            protected double LastDelta = 1;
            public double AnimationStep = 0; // pff, who needes getters and setters?
            public double AnimationStepOffset => OffsetLegs ? (AnimationStep + .5).Modulo(1) : AnimationStep;
            public double CrouchWaitTime = 0; // extra property for stuffs that needs it
            public double IdOffset => Configuration.Id % 2 == 1 ? 0 : .5;
            public bool OffsetLegs = true;
            public Animation Animation = Animation.Idle;
            public double AnimationWaitTime = 0;
            public virtual double AnimationDirectionMultiplier => 1;

            public double AACalculatedThighLength = 0;
            public double AACalculatedCalfLength = 0;
            public double AACalculatedQuadLength = 0;

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

                AACalculatedThighLength = Configuration.ThighLength > 0 ? Configuration.ThighLength : FindThighLength();
                AACalculatedCalfLength = Configuration.CalfLength > 0 ? Configuration.CalfLength : FindCalfLength(); // lower leg, or upper leg for spiders
                AACalculatedQuadLength = Configuration.ThighLength > 0 ? Configuration.ThighLength : FindQuadLength(); // lower lower leg, or lower leg for spiders
            }

            public virtual void AddLeftRightBlock<T>(List<T> leftBlocks, List<T> rightBlocks, T block, BlockSide side)
            {
                if (side == BlockSide.Left)
                    leftBlocks.Add(block);
                else
                    rightBlocks.Add(block);
            }

            public virtual void AddBlock(FetchedBlock block)
            {

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

            protected virtual void SetAnglesOf(List<LegJoint> stators, double angle, double offset, bool rotorInvert = false)
            {
                foreach (var motor in stators)
                    motor.SetAngle((angle + /*(motor.IsRotor ?*/ offset * (rotorInvert && motor.IsRotor ? -1 : 1)/*: -offset)*/ + motor.Configuration.Offset) * motor.Configuration.InversedMultiplier);
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
                SetAnglesOf(AALeftHipStators, leftAngles.HipDegrees, Configuration.HipOffsets); // +hip goes the right way
                SetAnglesOf(AARightHipStators, rightAngles.HipDegrees, -Configuration.HipOffsets);

                SetAnglesOf(AALeftKneeStators, leftAngles.KneeDegrees, -Configuration.KneeOffsets); //Configuration.HipOffsets + Configuration.KneeOffsets); // why is this different from hip
                SetAnglesOf(AARightKneeStators, rightAngles.KneeDegrees, -Configuration.KneeOffsets);//-Configuration.HipOffsets + Configuration.KneeOffsets);

                SetAnglesOf(AALeftFootStators, leftAngles.FeetDegrees, -Configuration.FootOffsets);// + Configuration.KneeOffsets); // what?
                SetAnglesOf(AARightFootStators, rightAngles.FeetDegrees, -Configuration.FootOffsets);// + Configuration.KneeOffsets);

                SetAnglesOf(AALeftQuadStators, leftAngles.QuadDegrees, -Configuration.QuadOffsets);
                SetAnglesOf(AARightQuadStators, rightAngles.QuadDegrees, -Configuration.QuadOffsets);

                SetAnglesOf(AALeftStrafeStators, leftAngles.StrafeDegrees, -Configuration.StrafeOffsets);
                SetAnglesOf(AARightStrafeStators, rightAngles.StrafeDegrees, -Configuration.StrafeOffsets);
            }

            protected double FindThighLength()
            {
                if (AALeftHipStators.Count == 0 || AALeftKneeStators.Count == 0)
                {
                    if (AARightHipStators.Count == 0 || AARightKneeStators.Count == 0)
                        return 2.5d;
                    return (AARightHipStators.First().Stator.GetPosition() - AARightKneeStators.First().Stator.GetPosition()).Length();
                }
                return (AALeftHipStators.First().Stator.GetPosition() - AALeftKneeStators.First().Stator.GetPosition()).Length();
            }

            protected double FindCalfLength()
            {
                if (AALeftFootStators.Count == 0 || AALeftKneeStators.Count == 0)
                {
                    if (AARightFootStators.Count == 0 || AARightKneeStators.Count == 0)
                        return 2.5d;
                    return (AARightFootStators.First().Stator.GetPosition() - AARightKneeStators.First().Stator.GetPosition()).Length();
                }
                return (AALeftFootStators.First().Stator.GetPosition() - AALeftKneeStators.First().Stator.GetPosition()).Length();
            }

            protected double FindQuadLength()
            {
                if (AALeftFootStators.Count == 0 || AALeftQuadStators.Count == 0)
                {
                    if (AARightFootStators.Count == 0 || AARightQuadStators.Count == 0)
                        return 2.5d;
                    return (AARightFootStators.First().Stator.GetPosition() - AARightQuadStators.First().Stator.GetPosition()).Length();
                }
                return (AALeftFootStators.First().Stator.GetPosition() - AALeftQuadStators.First().Stator.GetPosition()).Length();
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
                if (!info.Crouched)//!Animation.IsCrouch() && !info.Crouched)
                    CrouchWaitTime = Math.Max(0, CrouchWaitTime - info.Delta * Configuration.CrouchSpeed);//CrouchWaitTime = Math.Max(0, jumping ? 0 : CrouchWaitTime - info.Delta * 2 * Configuration.CrouchSpeed * CrouchSpeed);
                else
                    CrouchWaitTime = Math.Min(1, CrouchWaitTime + info.Delta * Configuration.CrouchSpeed);

                AnimationStep = (animationStepCounter * Configuration.AnimationSpeed + (animationStepCounter > 0 ? IdOffset : 0)).Modulo(1);
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
                HandlePistonGroup(LeftPistons, AALeftHipStators, AALeftKneeStators, AALeftFootStators, ref leftLegCounter);
                HandlePistonGroup(RightPistons, AARightHipStators, AARightKneeStators, AARightFootStators, ref rightLegCounter);
            }

            #endregion
        }
    }
}