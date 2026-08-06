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
        //public static Dictionary<int, PoseGroup> poses = new Dictionary<int, PoseGroup>();
        List<PoseJoint> poseJoints = new List<PoseJoint>();
        Dictionary<int, HashSet<PoseJoint>> poseJointsByGroup = new Dictionary<int, HashSet<PoseJoint>>();
        Dictionary<IMyTerminalBlock, PoseJoint> poseJointsByBlock = new Dictionary<IMyTerminalBlock, PoseJoint>();

        public int posesPlaying = 0;

        private static readonly Dictionary<byte, string> EASING_NAMES = new Dictionary<byte, string>()
        {
            { 0,  "linear" },
                  
            { 1,  "sine" },
            { 2,  "sine-in" },
            { 3,  "sine-out" },
                  
            { 4,  "cubic" },
            { 5,  "cubic-in" },
            { 6,  "cubic-out" },
                  
            { 7,  "quint" },
            { 8,  "quint-in" },
            { 9,  "quint-out" },

            { 10, "circular" },
            { 11, "circular-in" },
            { 12, "circular-out" },

            { 13, "elastic" },
            { 14, "elastic-in" },
            { 15, "elastic-out" },

            { 16, "quad" },
            { 17, "quad-in" },
            { 18, "quad-out" },

            { 19, "quart" },
            { 20, "quart-in" },
            { 21, "quart-out" },

            { 22, "exponential" },
            { 23, "exponential-in" },
            { 24, "exponential-out" },

            { 25, "back" },
            { 26, "back-in" },
            { 27, "back-out" },

            { 28, "bounce" },
            { 29, "bounce-in" },
            { 30, "bounce-out" },

            { 31, "constant" },
        };

        public struct PoseKeyframe
        {
            public float Time;
            public float Value;
            public byte Easing;
        }

        /*struct PoseAnimation
        {
            public List<PoseKeyframe> Keyframes;
        }*/

        public struct PosePlaybackState
        {
            public string AnimationName;
            public int Index;
            public float LastValue;
            public float LastTime;
            public float Time;
            public bool Looped;
        }

        public class PoseJoint
        {
            public Dictionary<string, List<PoseKeyframe>> Animations;
            public PosePlaybackState? Playback;
            private Joint _joint;
            private IMyPistonBase _piston;
            private IMyTerminalBlock _block;

            public PoseJoint(FetchedBlock block)
            {
                _block = block.Block;
                if (block.Block is IMyMotorStator)
                    _joint = new Joint(block);
                else
                    _piston = block.Block as IMyPistonBase;
                Animations = new Dictionary<string, List<PoseKeyframe>>(StringComparer.OrdinalIgnoreCase);
                LoadAnimations();
            }

            const double c1 = 1.70158d;
            const double c2 = c1 * 1.525f;
            const double c3 = c1 + 1d;
            const double c4 = (2d * Math.PI) / 3d;
            const double c5 = (2d * Math.PI) / 4.5d;
            const double n1 = 7.5625d;
            const double d1 = 2.75d;

            private float HandleEasing(float t, byte e)
            {
                switch (e)
                {
                    default:
                    case 0: // linear
                        return t;
                    case 31: // constant
                        return 0;
                    case 1: // sine-in
                        return 1f - (float)Math.Cos((t * Math.PI) / 2d);
                    case 2: // sine-out
                        return (float)Math.Sin((t * Math.PI) / 2d);
                    case 3: // sine
                        return -(float)(Math.Cos(Math.PI * t) - 1d) / 2f;
                    
                    case 4: // cubic-in
                        return (float)Math.Pow(t, 3);
                    case 5: // cubic-out
                        return 1f - (float)Math.Pow(1 - t, 3);
                    case 6: // cubic
                        return t < 0.5f ? 4f * (float)Math.Pow(t, 3) : 1f - (float)Math.Pow(-2f * t + 2f, 3) / 2f;
                    
                    case 7: // quint-in
                        return (float)Math.Pow(t, 5);
                    case 8: // quint-out
                        return 1f - (float)Math.Pow(1 - t, 5);
                    case 9: // quint
                        return t < 0.5f ? 16f * (float)Math.Pow(t, 5) : 1f - (float)Math.Pow(-2f * t + 2f, 5) / 2f;

                    case 10: // circular-in
                        return 1f - (float)Math.Sqrt(1f - Math.Pow(t, 2));
                    case 11: // circular-out
                        return (float)Math.Sqrt(1d - Math.Pow(t - 1f, 2));
                    case 12: // circular
                        return t < 0.5f
                            ? (1 - (float)Math.Sqrt(1d - Math.Pow(2 * t, 2))) / 2f
                            : ((float)Math.Sqrt(1d - Math.Pow(-2 * t + 2, 2)) + 1) / 2f;

                    case 13: // elastic-in
                        return t <= 0f ? 0f : t >= 1f ? 1f : (float)(-Math.Pow(2, 10f * t - 10f) * Math.Sin((t * 10f - 10.75f) * c4));
                    case 14: // elastic-out
                        return t <= 0f ? 0f : t >= 1f ? 1f : (float)(Math.Pow(2, -10f * t) * Math.Sin((t * 10f - 0.75f) * c4) + 1f);
                    case 15: // elastic
                        return t <= 0f ? 0f : t >= 1f ? 1f : t < 0.5f ?
                            -(float)(Math.Pow(2, 20f * t - 10f)  * Math.Sin((20f * t - 11.125f) * c5)) / 2f :
                             (float)(Math.Pow(2, -20f * t + 10f) * Math.Sin((20f * t - 11.125f) * c5)) / 2f + 1f;

                    case 16: // quad-in
                        return (float)Math.Pow(t, 2);
                    case 17: // quad-out
                        return 1f - (1f - t) * (1f - t);
                    case 18: // quad
                        return t < 0.5f ? 2f * (float)Math.Pow(t, 2) : 1f - (float)Math.Pow(-2f * t + 2f, 2) / 2f;

                    case 19: // quart-in
                        return (float)Math.Pow(t, 4);
                    case 20: // quart-out
                        return 1f - (float)Math.Pow(1 - t, 4);
                    case 21: // quart
                        return t < 0.5f ? 8f * (float)Math.Pow(t, 4) : 1f - (float)Math.Pow(-2f * t + 2f, 4) / 2f;

                    case 22: // exponential-in
                        return t <= 0f ? 0f : (float)Math.Pow(2, 10f * t - 10f);
                    case 23: // exponential-out
                        return t >= 1f ? 1f : 1f - (float)Math.Pow(2, -10f * t);
                    case 24: // exponential
                        return t <= 0f ? 0f : t >= 1f ? 1f : t < 0.5f ?
                            (float)Math.Pow(2, 20f * t - 10f) / 2f :
                            (2f - (float)Math.Pow(2, -20f * t + 10f)) / 2f;

                    case 25: // back-in
                        return (float)(c3 * t * t * t - c1 * t * t);
                    case 26: // back-out
                        return (float)(1d + c3 * Math.Pow(t - 1f, 3) + c1 * Math.Pow(t - 1f, 2));
                    case 27: // back
                        return t < 0.5f
                            ? (float)(Math.Pow(2 * t, 2) * ((c2 + 1) * 2 * t - c2)) / 2f
                            : (float)(Math.Pow(2 * t - 2, 2) * ((c2 + 1) * (t * 2 - 2) + c2) + 2) / 2f;

                    case 28: // bounce-in
                        return 1f - HandleEasing(1f - t, 29);
                    case 29: // bounce-out
                        if (t < 1 / d1)
                            return (float)(n1 * t * t);
                        else if (t < 2 / d1)
                        {
                            t -= (float)(1.5f / d1);
                            return (float)(n1 * t * t + 0.75f);
                        }
                        else if (t < 2.5 / d1)
                        {
                            t -= (float)(2.25f / d1);
                            return (float)(n1 * t * t + 0.9375f);
                        }
                        t -= (float)(2.625f / d1);
                        return (float)(n1 * t * t + 0.984375f);
                    case 30: // bounce
                        return t < 0.5 ? (1f - HandleEasing(1f - 2f * t, 29)) / 2f : (1f + HandleEasing(2f * t - 1f, 29)) / 2f;
                }
            }

            /*float EaseInOutCircular(float x)
            {
                return x < 0.5f
                    ? (1 - (float)Math.Sqrt(1d - Math.Pow(2 * x, 2))) / 2f
                    : ((float)Math.Sqrt(1d - Math.Pow(-2 * x + 2, 2)) + 1) / 2f;
            }

            float EaseInOutBack(float x)
            {
                float c1 = 1.70158f;
                float c2 = c1 * 1.525f;

                return x < 0.5f
                    ? ((float)Math.Pow(2 * x, 2) * ((c2 + 1) * 2 * x - c2)) / 2f
                    : ((float)Math.Pow(2 * x - 2, 2) * ((c2 + 1) * (x * 2 - 2) + c2) + 2) / 2f;
            }*/

            public void SaveAnimations(string removeSection=null)
            {
                MyIni ini = Singleton.configManager.GetConfiguration(_block);
                if (!string.IsNullOrEmpty(removeSection))
                    ini.DeleteSection($"Animation:{removeSection}");
                foreach (var kv in Animations)
                {
                    StringBuilder keyframes = new StringBuilder();
                    foreach (var kf in kv.Value)
                        keyframes.Append(kf.Time).Append(",").Append(kf.Value).Append(",").Append(EASING_NAMES[kf.Easing]).AppendLine();
                    string section = $"Animation:{kv.Key}";
                    ini.DeleteSection(section); // make sure ordering is correct
                    ini.Set(section, "Keyframes", keyframes.ToString());
                    ini.SetComment(section, "Keyframes", "time (s), value (deg/meter), easing");
                }
                _block.CustomData = ini.ToString();
            }

            public void LoadAnimations()
            {
                MyIni ini = Singleton.configManager.GetConfiguration(_block);
                List<string> sections = new List<string>();
                ini.GetSections(sections);
                foreach (var section in sections)
                {
                    if (!section.StartsWith("Animation:"))
                    {
                        continue;
                    }
                    string animationName = section.Substring(section.IndexOf(":") + 1);
                    CreateAnimation(animationName, false);
                    string keyframes = ini.Get(section, "Keyframes").ToString("");
                    foreach (var line in keyframes.Split('\n'))
                    {
                        string[] args = line.Replace(" ", string.Empty).Split(',');
                        if (args.Length != 3)
                            continue;
                        PoseKeyframe keyframe = new PoseKeyframe();
                        float.TryParse(args[0], out keyframe.Time);
                        float.TryParse(args[1], out keyframe.Value);
                        if (EASING_NAMES.ContainsValue(args[2]))
                        {
                            keyframe.Easing = EASING_NAMES.First(kv => kv.Value == args[2]).Key; // always non-null!
                        }

                        Animations[animationName].Add(keyframe);
                    }
                }
            }

            private float GetValue()
            {
                // stators can return a -inf to inf instead of just 0 to 360... amazing design
                return _piston != null ? _piston.CurrentPosition : _joint.IsHinge ? _joint.Stator.Angle.ToDegrees() : _joint.Stator.Angle.ToDegrees().Modulo(360f);
            }

            private void SetValue(float value)
            {
                if (_piston != null)
                {
                    var dif = value - _piston.CurrentPosition;
                    var vel = dif / (float)moveInfo.Delta;
                    _piston.Velocity = vel;
                }
                else
                    _joint.SetAngle(value);
            }

            private void Stop()
            {
                if (_piston != null)
                    _piston.Velocity = 0;
                else
                    _joint.SetRPM(0f);
            }

            private bool HasAnimation(string name)
            {
                return Animations.ContainsKey(name);
            }

            public void CreateAnimation(string name, bool save=true)
            {
                DeleteAnimation(name);
                Animations.Add(name, new List<PoseKeyframe>());
                if (save)
                    SaveAnimations();
            }

            public void DeleteAnimation(string name)
            {
                if (HasAnimation(name))
                {
                    Animations.Remove(name);
                    SaveAnimations(name);
                }
            }

            public void CreateFrame(string name)
            {
                if (!HasAnimation(name))
                    return;
                var keyframes = Animations[name];
                keyframes.Add(new PoseKeyframe()
                {
                    Time = keyframes.Count + 1,
                    Value = (float)Math.Round(GetValue(), 2),
                    Easing = 0
                });
                SaveAnimations();
            }

            public void PlayAnimation(string name, bool looped=false)
            {
                if (!HasAnimation(name))
                    return;
                Playback = new PosePlaybackState()
                {
                    AnimationName = name,
                    Index = 0,
                    Time = 0,
                    LastTime = 0,
                    LastValue = GetValue(),
                    Looped = looped
                };
                Singleton.posesPlaying += 1;
            }

            public void StopAnimation(string name)
            {
                if (Playback.HasValue && Playback.Value.AnimationName.Equals(name))
                {
                    Playback = null;
                    Stop();
                    Singleton.posesPlaying -= 1;
                }
            }

            public void SetFrame(string name, int frameIndex, float time = -1, string easing = null)
            {
                if (!HasAnimation(name))
                    return;
                if (frameIndex < 1)
                    return;
                var anim = Animations[name];
                if (frameIndex > anim.Count)
                    return;
                var frame = anim[frameIndex - 1];
                if (time >= 0)
                    frame.Time = time;
                if (!string.IsNullOrEmpty(easing))
                    easing = easing.Trim();
                if (!string.IsNullOrEmpty(easing) && EASING_NAMES.ContainsValue(easing))
                    frame.Easing = EASING_NAMES.First(kv => kv.Value.Equals(easing)).Key;

                anim[frameIndex - 1] = frame;
                SaveAnimations();
            }

            private float LerpAngleDelta(float a, float b, float t, float min, float max)
            {
                if ((min < -360.5d && max > 360.5d) || _joint.IsHinge)
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

            public void Update(double delta)
            {
                if (!Playback.HasValue)
                    return;
                var state = Playback.Value;
                List<PoseKeyframe> animation;
                if (!Animations.TryGetValue(state.AnimationName, out animation))
                {
                    Playback = null;
                    Singleton.posesPlaying -= 1;
                    return;
                }
                state.Time += (float)delta; // TODO: other delta
                while (animation[state.Index].Time < state.Time)
                {
                    if (state.Index >= animation.Count - 1) // if next will be end
                    {
                        if (state.Looped)
                        {
                            Log("Animation on", _block.CustomName, "at", state.Time);
                            Log(" Looped");
                            PlayAnimation(state.AnimationName, state.Looped);
                            var stopName = Playback?.AnimationName.ToLowerInvariant();
                            Singleton.RunPoseTimerblocks(TimerBlockEvent.POSE_LOOP, stopName);
                        }
                        else
                        {
                            if (Math.Abs(animation[state.Index].Value - GetValue()) > 0.01f)
                            {
                                Log("Animation on", _block.CustomName, "at", state.Time);
                                Log(" Finish", animation[state.Index].Value - GetValue());
                                SetValue(animation[state.Index].Value);
                            }
                            else
                            {
                                Log("Animation on", _block.CustomName, "at", state.Time);
                                Log("   Done");

                                var stopName = Playback?.AnimationName.ToLowerInvariant();
                                if (Singleton.posesNeedsStoppedTB.Contains(stopName))
                                {
                                    Singleton.RunPoseTimerblocks(TimerBlockEvent.POSE_STOP, stopName);
                                    Singleton.posesNeedsStoppedTB.Remove(stopName);
                                }
                                Playback = null;
                                Singleton.posesPlaying -= 1;
                                Stop();
                            }
                        }
                        return;
                    }
                    else
                    {
                        state.LastValue = animation[state.Index].Value;
                        state.LastTime = animation[state.Index].Time;
                        state.Index++;
                    }
                }
                if (state.Index > animation.Count - 1)
                    return;
                var keyframe = animation[state.Index];
                Log("Animation on", _block.CustomName, "at", state.Time);
                Log("  Value", state.LastValue, "-->", keyframe.Value);
                Log("   Time", state.LastTime, "-->", keyframe.Time);
                float t = Math.Min(1f, (state.Time - state.LastTime) / (keyframe.Time - state.LastTime));
                Log("  Delta", t);
                float dt = HandleEasing(t, keyframe.Easing);
                var target = _joint != null ? LerpAngleDelta(state.LastValue, keyframe.Value, dt, _joint.Minimum, _joint.Maximum) : MathHelper.Lerp(state.LastValue, keyframe.Value, dt);
                Log(" Target", target);
                SetValue(target);
                Playback = state;
            }
        }

        /*public class PoseGroup : JointGroup
        {
            public new PoseConfiguration Configuration;

            public readonly List<PoseJoint> PoseJoints;

            public PoseGroup()
            {
                PoseJoints = new List<PoseJoint>();
            }

            public override void ApplyConfiguration()
            {
                return;
            }

            public override void SetConfiguration(object config)
            {
                Configuration = (PoseConfiguration)config;
            }

            public void Update()
            {

            }

            public override bool AddBlock(FetchedBlock block)
            {
                switch (block.Type)
                {
                    case BlockType.Animatable:
                        PoseJoints.Add(new PoseJoint(block));
                        AddAllBlock(block);
                        return true;
                }
                return base.AddBlock(block);
            }
        }*/

        /*

        Pose System:

        A "pose" is an animation of sorts:

        Animation (named) -> Animation Group (assigned) -> Blocks -> Keyframes

        */

        public void FetchPoses()
        {
            //var configs = poses.Select((kv) => new KeyValuePair<int, JointConfiguration>(kv.Key, kv.Value.Configuration)).ToDictionary(pair => pair.Key, pair => pair.Value);
            //blockFetcher.FetchGroups(ref poses, configs, (_) => new PoseGroup(), PoseConfiguration.Parse);
            poseJoints.Clear();
            poseJointsByGroup.Clear();
            foreach (var joint in blockFetcher.GetBlocks(BlockType.Animatable))
            {
                PoseJoint poseJoint;
                if (poseJointsByBlock.ContainsKey(joint.Block))
                    poseJoint = poseJointsByBlock[joint.Block];
                else
                {
                    poseJoint = new PoseJoint(joint);
                    poseJoints.Add(poseJoint);
                }
                if (!poseJointsByGroup.ContainsKey(joint.Group))
                    poseJointsByGroup.Add(joint.Group, new HashSet<PoseJoint>());
                poseJointsByGroup[joint.Group].Add(poseJoint);
            }
        }

        public void CreateAnimation(int groups, string name)
        {
            if (!poseJointsByGroup.ContainsKey(groups))
            {
                commandResponse = $"No animation group with id {groups}";
                return;
            }
            var group = poseJointsByGroup[groups];

            foreach (var joint in group)
                joint.CreateAnimation(name);
        }

        public void CreateFrame(string name)
        {
            foreach (var joint in poseJoints)
                joint.CreateFrame(name);
        }

        public void DeleteAnimation(string name)
        {
            foreach (var joint in poseJoints)
                joint.DeleteAnimation(name);
        }

        public void PlayAnimation(string name, bool looped)
        {
            foreach (var joint in poseJoints)
                joint.PlayAnimation(name, looped);
        }

        public void StopAnimation(string name)
        {
            foreach (var joint in poseJoints)
                joint.StopAnimation(name);
        }

        public void SetFrame(string name, int frame, float time=-1, string easing=null)
        {
            foreach (var joint in poseJoints)
                joint.SetFrame(name, frame, time, easing);
        }

        public void UpdatePoses()
        {
            Log("-- Poses --");

            foreach (var pj in poseJoints)
                pj.Update(delta);
            //if (/*armsEnabled*/true)
            //    foreach (var pose in poses.Values)
            //        if (pose.Enabled)
            //            pose.Update();
        }
    }
}
