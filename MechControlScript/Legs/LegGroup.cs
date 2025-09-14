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

            public LegConfiguration DefaultConfiguration;
            public new LegConfiguration Configuration;

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
                Log($"- {/*GetType().Name*/"Leg Group"} (group {Configuration.Id}) -");
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

            // n/a

            #endregion
        }
    }
}