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
        public class TriLegGroup : LegGroup
	    {

			public List<LegJoint> LeftHipJoints  = new List<LegJoint>();
            public List<LegJoint> RightHipJoints = new List<LegJoint>();

            public List<LegJoint> LeftKneeJoints  = new List<LegJoint>();
            public List<LegJoint> RightKneeJoints = new List<LegJoint>();

            public List<LegJoint> LeftFootJoints  = new List<LegJoint>();
            public List<LegJoint> RightFootJoints = new List<LegJoint>();

            public List<LegJoint> LeftStrafeJoints = new List<LegJoint>();
            public List<LegJoint> RightStrafeJoints = new List<LegJoint>();

            public List<Hydraulic> Hydraulics = new List<Hydraulic>();

            public List<IMyCameraBlock> LeftCameras = new List<IMyCameraBlock>();
            public List<IMyCameraBlock> RightCameras = new List<IMyCameraBlock>();

            public List<IMyLandingGear> LeftMagnets = new List<IMyLandingGear>();
            public List<IMyLandingGear> RightMagnets = new List<IMyLandingGear>();

            public List<LegJoint> AllJoints = new List<LegJoint>();

            protected LegAngles LegAnglesOffset;

            public float GridSize { protected set; get; }

            public float ThighLength { protected set; get; }
			public float CalfLength { protected set; get; }

            protected float FindJointLength(List<LegJoint> jointsA, List<LegJoint> jointsB)
            {
                if (jointsA.Count == 0 || jointsB.Count == 0)
                    return float.NegativeInfinity;
                float length = float.PositiveInfinity;
                Vector3I ai, bi;
                foreach (var a in jointsA)
                {
                    foreach (var b in jointsB)
                    {
                        // TODO: test all options :)
                        // account for all rotor arrangements, since it doesn't technically have to all be one way
                        if (a.Stator.Top.CubeGrid.Equals(b.Stator.CubeGrid))
                        {
                            // top --> stator
                            ai = a.Stator.Top.Position + Base6Directions.GetIntVector(a.Stator.Top.Orientation.Up);
                            bi = b.Stator.Position;
                        }
                        else if (a.Stator.CubeGrid.Equals(b.Stator.CubeGrid))
                        {
                            // stator --> stator
                            ai = a.Stator.Position;// + Base6Directions.GetIntVector(a.Stator.Orientation.Up);
                            bi = b.Stator.Position;
                        }
                        else if (a.Stator.CubeGrid.Equals(b.Stator.Top.CubeGrid))
                        {
                            // stator --> top
                            ai = a.Stator.Position;// + Base6Directions.GetIntVector(a.Stator.Top.Orientation.Up);
                            bi = b.Stator.Top.Position;
                        }
                        else if (a.Stator.Top.CubeGrid.Equals(b.Stator.Top.CubeGrid))
                        {
                            // top --> top
                            ai = a.Stator.Top.Position + Base6Directions.GetIntVector(a.Stator.Top.Orientation.Up);
                            bi = b.Stator.Top.Position;
                        }
                        else
                        {
                            ai = Vector3I.Zero;
                            bi = Vector3I.Zero;
                        }
                        length = Math.Min(length, (ai - bi).Length() * GridSize);
                    }
                }
                return length;
            }

            protected float FindJointLength(List<LegJoint> jointsA, List<IMyPistonBase> jointsB)
            {
                if (jointsA.Count == 0 || jointsB.Count == 0)
                    return float.NegativeInfinity;
                float length = float.PositiveInfinity;
                //Vector3I ai, bi;
                IMyCubeGrid grid;
                foreach (var a in jointsA)
                {
                    foreach (var b in jointsB)
                    {
                        float dist = (float)Hydraulic.CountDistance(a.Stator, b, out grid, 1);
                        length = Math.Min(length, dist * grid.GridSize);
                        //ai = a.Stator.Top.Position + Base6Directions.GetIntVector(a.Stator.Top.Orientation.Up);
                        //bi = b.Position;
                        //length = Math.Min(length, ((ai - bi).Length()) * GridSize);
                    }
                }
                return length;
            }

            public override void Initialize()
            {
                base.Initialize();

                LegAnglesOffset = new LegAngles(Configuration.HipOffsets, Configuration.KneeOffsets, Configuration.FootOffsets, Configuration.QuadOffsets, Configuration.StrafeOffsets);

                // add joints
                AllJoints.AddRange(LeftHipJoints);
                AllJoints.AddRange(LeftKneeJoints);
                AllJoints.AddRange(LeftFootJoints);
                AllJoints.AddRange(RightHipJoints);
                AllJoints.AddRange(RightKneeJoints);
                AllJoints.AddRange(RightFootJoints);

                if (AllJoints.Count == 0)
                    return;
                GridSize = AllJoints.First().Stator.CubeGrid.GridSize;

                // calculate lengths
                // we assume the left/right legs are both the same length.. at least for easy sake
                ThighLength = Configuration.ThighLength ?? Math.Max(FindJointLength(LeftHipJoints, LeftKneeJoints), FindJointLength(RightHipJoints, RightKneeJoints)).AlwaysANumber(1);
                CalfLength  = Configuration.CalfLength ?? Math.Max(FindJointLength(LeftKneeJoints, LeftFootJoints), FindJointLength(RightKneeJoints, RightFootJoints)).AlwaysANumber(1);

            }

            public override void Update(MovementInfo info)
            {
                base.Update(info);
                Log("# L/R Hips   :", LeftHipJoints.Count, "/", RightHipJoints.Count);
                Log("# L/R Knees  :", LeftKneeJoints.Count, "/", RightKneeJoints.Count);
                Log("# L/R Feet   :", LeftFootJoints.Count, "/", RightFootJoints.Count);
                Log("# L/R Strafe :", LeftStrafeJoints.Count, "/", RightStrafeJoints.Count);
                Log("Thigh Length :", ThighLength, "meters", Configuration.ThighLength);
                Log("Calf  Length :", CalfLength, "meters", Configuration.CalfLength);
            }

            protected override void SetAngles(LegAngles left, LegAngles right)
            {
                SetAnglesOf(LeftHipJoints, left.HipDegrees);
                SetAnglesOf(RightHipJoints, right.HipDegrees);
                SetAnglesOf(LeftKneeJoints, left.KneeDegrees);
                SetAnglesOf(RightKneeJoints, right.KneeDegrees);
                SetAnglesOf(LeftFootJoints, left.FeetDegrees);
                SetAnglesOf(RightFootJoints, right.FeetDegrees);
                SetAnglesOf(LeftStrafeJoints, left.StrafeDegrees);
                SetAnglesOf(RightStrafeJoints, right.StrafeDegrees);
            }

            /// <summary>
            /// Update any hydraulics
            /// Not a part of the base Update method for optionality
            /// </summary>
            protected void UpdateHydraulics()
            {
                if (Hydraulics.Count == 0)
                    return; // is checking this faster than the Where enumerator having no elements?
                foreach (var hy in Hydraulics.Where(h => h.Valid))
                {
                    hy.TopPosition = hy.TopStator.WorldMatrix;
                    hy.BottomPosition = hy.BottomStator.WorldMatrix;
                    hy.Update();
                }
            }

            protected MyTuple<double, double> cameraOffsetTween = new MyTuple<double, double>(0, 0);
            protected double CameraOffsetTweenMultiplier = 10d;

            /// <summary>
            /// Update the cameras
            /// Not a part of the base Update method for optionality
            /// </summary>
            protected MyTuple<double, double> UpdateCameras()
            {
                Log("Cameras:", LeftCameras.Count, RightCameras.Count);
                if (LeftCameras.Count > 0 && RightCameras.Count > 0)
                {
                    double left = 999999999d;
                    double right = 999999999d;
                    MyDetectedEntityInfo hit;
                    foreach (var camera in LeftCameras)
                    {
                        camera.EnableRaycast = true;
                        hit = camera.Raycast(20);
                        if (!hit.IsEmpty())
                        {
                            left = Math.Min(left, Vector3D.Dot(gravity.Normalized(), hit.HitPosition.Value));
                        }
                    }
                    foreach (var camera in RightCameras)
                    {
                        camera.EnableRaycast = true;
                        hit = camera.Raycast(20);
                        if (!hit.IsEmpty())
                        {
                            right = Math.Min(right, Vector3D.Dot(gravity.Normalized(), hit.HitPosition.Value));
                        }
                    }

                    if (left != 999999999d && right != 999999999d)
                        Cameras.SetGroup(Configuration.Id, left, right);
                    //var x = Cameras.GetGroup(Configuration.Id);
                    //Log("Camera Values:", x.Item1, x.Item2);
                }

                var cameraOffsets = Cameras.CalculateGroup(Configuration.Id);
                cameraOffsetTween.Item1 += (cameraOffsets.Item1 - cameraOffsetTween.Item1) / CameraOffsetTweenMultiplier;
                cameraOffsetTween.Item2 += (cameraOffsets.Item2 - cameraOffsetTween.Item2) / CameraOffsetTweenMultiplier;
                return cameraOffsetTween;
            }

            public override bool AddBlock(FetchedBlock block)
            {
                switch (block.Type)
                {
                    case BlockType.Hip:
                        AddLeftRightBlock(LeftHipJoints, RightHipJoints, new LegJoint(block), block.Side);
                        AddAllBlock(block);
                        return true;
                    case BlockType.Knee:
                        if (!(block.Block is IMyMotorStator))
                            return false;
                        AddLeftRightBlock(LeftKneeJoints, RightKneeJoints, new LegJoint(block), block.Side);
                        AddAllBlock(block);
                        return true;
                    case BlockType.Foot:
                        AddLeftRightBlock(LeftFootJoints, RightFootJoints, new LegJoint(block), block.Side);
                        AddAllBlock(block);
                        return true;
                    case BlockType.Strafe:
                        AddLeftRightBlock(LeftStrafeJoints, RightStrafeJoints, new LegJoint(block), block.Side);
                        AddAllBlock(block);
                        return true;
                    case BlockType.Camera:
                        AddLeftRightBlock(LeftCameras, RightCameras, block.Block as IMyCameraBlock, block.Side);
                        AddAllBlock(block);
                        return true;
                    case BlockType.Magnet:
                        AddLeftRightBlock(LeftMagnets, RightMagnets, block.Block as IMyLandingGear, block.Side);
                        AddAllBlock(block);
                        return true;
                    case BlockType.Hydraulic:
                        Hydraulics.Add(new Hydraulic(block));
                        AddAllBlock(block);
                        return true;
                }
                return base.AddBlock(block); // no blocks were added
            }

        }
	}
}
