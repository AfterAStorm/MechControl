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
        public class ArmGroup : JointGroup
        {

            #region # - Properties

            public new ArmConfiguration Configuration;

            public List<ArmJoint> PitchJoints = new List<ArmJoint>();
            public List<ArmJoint> YawJoints = new List<ArmJoint>();
            //public List<ArmJoint> RollJoints = new List<ArmJoint>();

            public List<IMyLandingGear> Magnets = new List<IMyLandingGear>();

            public bool IsZeroing = false;
            public double Pitch => armPitch;
            public double Yaw => armYaw;
            //public double Roll => armRoll;

            #endregion

            #region # - Methods

            public override void SetConfiguration(object config)
            {
                Configuration = (ArmConfiguration)config;
            }

            public override void ApplyConfiguration()
            {
                // n/a
            }

            public override bool AddBlock(FetchedBlock block)
            {
                base.AddBlock(block);
                switch (block.Type)
                {
                    case BlockType.ArmPitch:
                        PitchJoints.Add(new ArmJoint(block, ArmJointConfiguration.Parse(block)));
                        return true;
                    case BlockType.ArmYaw:
                        YawJoints.Add(new ArmJoint(block, ArmJointConfiguration.Parse(block)));
                        return true;
                    /*case BlockType.Roll:
                        arm.RollJoints.Add(new ArmJoint(block, jointConfig));
                        return true;*/
                    case BlockType.Magnet:
                        Magnets.Add(block.Block as IMyLandingGear);
                        return true;
                }
                return false;
            }

            public void ToZero()
            {
                IsZeroing = true;
            }

            public void Update()
            {
                Log("is zeroing:", IsZeroing);
                if (Pitch.Absolute() > 0.5 || Yaw.Absolute() > 0.5)
                    IsZeroing = false;
                foreach (var joint in PitchJoints)
                {
                    if (joint.Stator.RotorLock)
                        continue;
                    if (IsZeroing)
                        joint.SetAngle(joint.Configuration.Offset);
                    else
                        joint.Stator.TargetVelocityRPM = (float)(Pitch * joint.Configuration.InversedMultiplier * joint.Configuration.Multiplier);
                    //joint.SetAngle((Pitch + joint.Configuration.Offset) * joint.Configuration.InversedMultiplier * joint.Configuration.Multiplier);
                }
                foreach (var joint in YawJoints)
                {
                    if (joint.Stator.RotorLock)
                        continue;
                    if (IsZeroing)
                        joint.SetAngle(joint.Configuration.Offset);
                    else
                        joint.Stator.TargetVelocityRPM = (float)(Yaw * joint.Configuration.InversedMultiplier * joint.Configuration.Multiplier);
                    //joint.SetAngle((Yaw + joint.Configuration.Offset) * joint.Configuration.InversedMultiplier * joint.Configuration.Multiplier);
                }
                if (IsZeroing)
                {
                    bool done = true;
                    foreach (var joint in PitchJoints.Concat(YawJoints))
                    {
                        if (joint.Stator.RotorLock)
                            continue;
                        if ((joint.Stator.Angle - joint.Configuration.Offset).Absolute() > .1)
                        {
                            done = false;
                            break;
                        }
                    }
                    if (done)
                        IsZeroing = false;
                }
                /*foreach (var joint in RollJoints)
                {
                    joint.SetAngle((Roll + joint.Configuration.Offset) * joint.Configuration.InversedMultiplier * joint.Configuration.Multiplier);
                }*/
            }

            #endregion

        }
    }
}
