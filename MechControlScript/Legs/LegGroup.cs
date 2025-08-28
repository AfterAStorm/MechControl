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

            public List<FetchedBlock> LeftPistons = new List<FetchedBlock>();
            public List<FetchedBlock> RightPistons = new List<FetchedBlock>();

            public List<IMyLandingGear> LeftGears = new List<IMyLandingGear>();
            public List<IMyLandingGear> RightGears = new List<IMyLandingGear>();

            //public IMyCameraBlock[] InclineCameras; // TODO: use these, give them a purpose!

            protected double LastDelta = 1;
            public double AnimationStep = 0; // pff, who needes getters and setters?
            public double AnimationStepOffset;// => OffsetLegs ? (AnimationStep + .5).Modulo(1) : AnimationStep;
            public double CrouchWaitTime = 0; // extra property for stuffs that needs it
            public double IdOffset => Configuration.Id % 2 == 1 ? 0 : .5;
            public bool OffsetLegs = true;
            public Animation Animation = Animation.Idle;
            public double AnimationWaitTime = 0;
            public virtual double AnimationDirectionMultiplier => 1;

            #endregion

            #region # - Constructor

            public LegGroup() { }

            #endregion

            #region # - Methods

            public virtual void Initialize()
            {
                //AACalculatedThighLength = Configuration.ThighLength > 0 ? Configuration.ThighLength : FindThighLength();
                //AACalculatedCalfLength = Configuration.CalfLength > 0 ? Configuration.CalfLength : FindCalfLength(); // lower leg, or upper leg for spiders
                //AACalculatedQuadLength = Configuration.ThighLength > 0 ? Configuration.ThighLength : FindQuadLength(); // lower lower leg, or lower leg for spiders
            }

            protected virtual void AddLeftRightBlock<T>(List<T> leftBlocks, List<T> rightBlocks, T block, BlockSide side)
            {
                if (side == BlockSide.Left)
                    leftBlocks.Add(block);
                else
                    rightBlocks.Add(block);
            }

            public override void SetConfiguration(object config)
            {
                Configuration = (LegConfiguration)config;
            }

            public override void ApplyConfiguration()
            {
                string data = Configuration.ToCustomDataString();
                foreach (var block in AllBlocks)
                    block.Block.CustomData = data;
            }

            protected virtual void SetAnglesOf(List<LegJoint> leftStators, List<LegJoint> rightStators, double leftAngle, double rightAngle, double offset=0)
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

            protected virtual void SetAnglesOf(List<LegJoint> stators, double angle, double offset=0, bool rotorInvert = false)
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

            protected virtual void SetAngles(LegAngles leftAngles, LegAngles rightAngles) {}


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
                if (!info.Crouched && !info.Jumping)//!Animation.IsCrouch() && !info.Crouched)
                    CrouchWaitTime = Math.Max(0, CrouchWaitTime - info.Delta * Configuration.CrouchSpeed * CrouchSpeed * (info.Jumped ? 1000f : 1f));//CrouchWaitTime = Math.Max(0, jumping ? 0 : CrouchWaitTime - info.Delta * 2 * Configuration.CrouchSpeed * CrouchSpeed);
                else
                    CrouchWaitTime = Math.Min(1, CrouchWaitTime + info.Delta * Configuration.CrouchSpeed * CrouchSpeed);

                // normal mode
                if (!Configuration.IndependantStep)
                {
                    AnimationStep = (animationStepCounter * Configuration.AnimationSpeed * WalkCycleSpeed + (animationStepCounter > 0 ? IdOffset : 0)).Modulo(1);
                    AnimationStepOffset = OffsetLegs ? (AnimationStep + .5).Modulo(1) : AnimationStep;
                    return;
                }
                
                // handle independant step, where each leg moves 1 cycle over the whole "step"
                double totalGroups = legs.Count;
                double myGroup = Configuration.Id;
                double segmentSize = 1 / (totalGroups * 2);
                double segmentOffset = (myGroup - 1) / (totalGroups * 2); // offset of left legs, it goes in the order of L1 -> L2 -> L3
                double segmentOffsetOffset = segmentSize * totalGroups + segmentOffset; // offset of right legs, after the left legs all go, R1 -> R2 -> R3 
                // final order: L1 -> L2 -> L3 -> R1 -> R2 -> R3

                // get step like normal, but exclude id offset since it will screw everything up... it doesn't matter here since we already account for it
                double currentStep = (animationStepCounter * Configuration.AnimationSpeed * WalkCycleSpeed).Modulo(1);

                // 0.5 is rest, so default to it
                AnimationStep = 0.5f;
                AnimationStepOffset = 0.5f;
                if (currentStep >= segmentOffset && currentStep <= segmentOffset + segmentSize) // left leg's turn
                {
                    AnimationStep = (.5f + MapRange(currentStep, segmentOffset, segmentOffset + segmentSize, 0, 1)).Modulo(1);
                }
                else if (currentStep >= segmentOffsetOffset && currentStep <= segmentOffsetOffset + segmentSize) // right leg's turn
                {
                    AnimationStepOffset = (.5f + MapRange(currentStep, segmentOffsetOffset, segmentOffsetOffset + segmentSize, 0, 1)).Modulo(1);
                }
            }

            #endregion

            #region Experimental

            /*int leftLegCounter = 0;
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
            }*/

            protected void HandlePistons(float multiplier = 1)
            {
                //HandlePistonGroup(LeftPistons, AALeftHipStators, AALeftKneeStators, AALeftFootStators, ref leftLegCounter);
                //HandlePistonGroup(RightPistons, AARightHipStators, AARightKneeStators, AARightFootStators, ref rightLegCounter);
            }

            #endregion
        }
    }
}