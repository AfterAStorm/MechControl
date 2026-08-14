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

            //public new ArmConfiguration Configuration;

            public List<ArmJoint> PitchJoints = new List<ArmJoint>();
            public List<ArmJoint> YawJoints = new List<ArmJoint>();
            //public List<ArmJoint> RollJoints = new List<ArmJoint>();

            //public List<IMyLandingGear> Magnets = new List<IMyLandingGear>();

            public bool IsZeroing = false;
            public double Pitch => _armPitch;
            public double Yaw => _armYaw;
            private double _armPitch, _armYaw;
            //public double Roll => armRoll;

            #endregion

            #region # - Methods

            public override void SetConfiguration(object config)
            {
                //Configuration = (ArmConfiguration)config;
            }

            public override void ApplyConfiguration()
            {
                //string data = Configuration.ToCustomDataString();
                foreach (var joint in AllJoints)
                {
                    MyIni jointIni = Program.Singleton.configManager.GetConfiguration(joint.Stator);
                    //Configuration.Save(jointIni);
                    (joint as ArmJoint)?.Configuration.Save(jointIni);
                    joint.Stator.CustomData = jointIni.ToString();
                    //joint.Stator.CustomData = /*data + "\n" +*/ joint.Configuration.ToCustomDataString();
                }
            }

            public override bool AddBlock(FetchedBlock block)
            {
                switch (block.Type)
                {
                    case BlockType.ArmPitch:
                        var joint = new ArmJoint(block);
                        PitchJoints.Add(joint);
                        AllJoints.Add(joint);
                        AddAllBlock(block);
                        return true;
                    case BlockType.ArmYaw:
                        var yjoint = new ArmJoint(block);
                        YawJoints.Add(yjoint);
                        AllJoints.Add(yjoint);
                        AddAllBlock(block);
                        return true;
                    /*case BlockType.Roll:
                        arm.RollJoints.Add(new ArmJoint(block, jointConfig));
                        return true;*/
                    /*case BlockType.Magnet:
                        Magnets.Add(block.Block as IMyLandingGear);
                        AddAllBlock(block);
                        return true;*/
                }
                return base.AddBlock(block);
            }

            public void ToZero()
            {
                IsZeroing = true;
            }

            void MoveToZero(ArmJoint joint)
            {
                //float lerped = LerpAngleDelta(joint.IsHinge ? joint.Stator.Angle.ToDegrees() : joint.Stator.Angle.ToDegrees().Modulo(360f), (float)joint.Configuration.Offset, /*0.5*/1f, joint.Minimum, joint.Maximum, joint.IsHinge);
                //joint.SetAnglePID(lerped);
                joint.SetAnglePID((float)joint.Configuration.Offset);
            }

            private float LerpAngleDelta(float a, float b, float t, float min, float max, bool hinge)
            {
                if ((min < -360.5d && max > 360.5d) || hinge)
                {
                    float delta = (b - a) % 360;
                    if (delta > 180f)
                        delta -= 360f;
                    if (delta < -180f)
                        delta += 360f;
                    return a + delta * t;
                }

                double delta_cw, delta_ccw;
                double dir = DetermineDirectionLimits(a, b, min, max, out delta_cw, out delta_ccw);
                if (dir == 0d)
                    return a;
                double chosen_delta = dir > 0d ? delta_cw : delta_ccw;
                double final_pos = a + chosen_delta * t;

                if (final_pos > max && (final_pos - 360d) >= min) final_pos -= 360d;
                if (final_pos < min && (final_pos + 360d) <= max) final_pos += 360d;

                if (final_pos < min)
                    return min;
                else if (final_pos > max)
                    return max;
                return (float)final_pos;
            }

            public void Update(double armPitch, double armYaw)
            {
                this._armPitch = armPitch;
                this._armYaw = armYaw;
                Log("is zeroing:", IsZeroing);
                if (Pitch.Absolute() > 0.5 || Yaw.Absolute() > 0.5)
                    IsZeroing = false;
                foreach (var joint in PitchJoints)
                {
                    if (joint.Stator.RotorLock || !Enabled)
                        continue;
                    if (IsZeroing)
                        MoveToZero(joint);
                        //joint.SetAngle(joint.IsHinge ? joint.Configuration.Offset : joint.Configuration.Offset.Modulo(360f), .25f);
                    else
                        joint.Stator.TargetVelocityRPM = (float)(Pitch * joint.Configuration.InversedMultiplier * joint.Configuration.Multiplier);
                    //joint.SetAngle((Pitch + joint.Configuration.Offset) * joint.Configuration.InversedMultiplier * joint.Configuration.Multiplier);
                }
                foreach (var joint in YawJoints)
                {
                    if (joint.Stator.RotorLock || !Enabled)
                        continue;
                    if (IsZeroing)
                        MoveToZero(joint);
                        //joint.SetAngle(joint.IsHinge ? joint.Configuration.Offset : joint.Configuration.Offset.Modulo(360f), .25f);
                    else
                        joint.Stator.TargetVelocityRPM = (float)(Yaw * joint.Configuration.InversedMultiplier * joint.Configuration.Multiplier);
                    //joint.SetAngle((Yaw + joint.Configuration.Offset) * joint.Configuration.InversedMultiplier * joint.Configuration.Multiplier);
                }
                if (IsZeroing)
                {
                    bool done = true;
                    foreach (var joint in PitchJoints.Concat(YawJoints))
                    {
                        if (joint.Stator.RotorLock || !Enabled)
                            continue;
                        if ((joint.Stator.Angle.ToDegrees().Modulo(360f) - joint.Configuration.Offset.Modulo(360f)).Absolute() > .02)
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
