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

            public List<LegJoint> LeftTurnJoints = new List<LegJoint>();
            public List<LegJoint> RightTurnJoints = new List<LegJoint>();

            public List<Hydraulic> Hydraulics = new List<Hydraulic>();

            public List<IMyCameraBlock> LeftCameras = new List<IMyCameraBlock>();
            public List<IMyCameraBlock> RightCameras = new List<IMyCameraBlock>();

            public List<IMyLandingGear> LeftMagnets = new List<IMyLandingGear>();
            public List<IMyLandingGear> RightMagnets = new List<IMyLandingGear>();

            protected LegAngles LegAnglesOffset;
            public bool AssuredMagnets => Configuration.PrecisionLocking;

            public float GridSize { protected set; get; }

            public float ThighLength { protected set; get; }
			public float CalfLength { protected set; get; }

            protected float FindJointLength(List<LegJoint> jointsA, List<LegJoint> jointsB)
            {
                if (jointsA.Count == 0 || jointsB.Count == 0)
                    return float.NegativeInfinity;
                float length = float.PositiveInfinity;
                Vector3I ai, bi;
                Vector3I diri = Vector3I.Zero;
                foreach (var a in jointsA) // thanks @beal for finding the pesking random crash when .Top is null :D
                {
                    if (a.Stator == null) // not sure if these are technically needed, but... it's probably fine
                        continue;
                    foreach (var b in jointsB)
                    {
                        if (b.Stator == null)
                            continue;
                        // TODO: test all options :)
                        // account for all rotor arrangements, since it doesn't technically have to all be one way
                        if (a.Stator.Top != null && a.Stator.Top.CubeGrid.Equals(b.Stator.CubeGrid))
                        {
                            // top --> stator
                            ai = a.Stator.Top.Position + Base6Directions.GetIntVector(a.Stator.Top.Orientation.Up);
                            bi = b.Stator.Position;
                            //(b.Stator.CubeGrid.WorldMatrix.GetOrientation() * b.Stator.WorldMatrix).Up;
                            diri = a.IsRotor ? Base6Directions.GetIntVector(a.Stator.Top.Orientation.Forward) : Base6Directions.GetIntVector(a.Stator.Top.Orientation.Left);
                            //#if DEBUG
                            /*Singleton.buildTools.Log("woah diff!");
                            Singleton.buildTools.Log($"{ai} to {bi} dot {diri} {Vector3I.Dot(ai - bi, diri)}");
                            Singleton.buildTools.DrawPoint(a.Stator.Top.CubeGrid.GridIntegerToWorld(ai), Color.Green);
                            Singleton.buildTools.DrawPoint(b.Stator.CubeGrid.GridIntegerToWorld(bi), Color.Red);
                            Singleton.buildTools.DrawLine(a.Stator.Top.WorldMatrix.Translation, a.Stator.Top.WorldMatrix.Translation + Vector3D.TransformNormal((Vector3D)diri, a.Stator.Top.WorldMatrix), Color.Blue);*/
                            //#endif
                            float dot = Math.Abs(Vector3I.Dot(ai - bi, diri));
                            if (dot > 0)
                            {
                                length = Math.Min(length, dot * GridSize);
                                continue;
                            }
                        }
                        else if (a.Stator.CubeGrid.Equals(b.Stator.CubeGrid))
                        {
                            // stator --> stator
                            ai = a.Stator.Position;// + Base6Directions.GetIntVector(a.Stator.Orientation.Up);
                            bi = b.Stator.Position;
                        }
                        else if (b.Stator.Top != null && a.Stator.CubeGrid.Equals(b.Stator.Top.CubeGrid))
                        {
                            // stator --> top
                            ai = a.Stator.Position;// + Base6Directions.GetIntVector(a.Stator.Top.Orientation.Up);
                            bi = b.Stator.Top.Position;
                        }
                        else if (a.Stator.Top != null && b.Stator.Top != null && a.Stator.Top.CubeGrid.Equals(b.Stator.Top.CubeGrid) == true)
                        {
                            // top --> top
                            ai = a.Stator.Top.Position + Base6Directions.GetIntVector(a.Stator.Top.Orientation.Up);
                            bi = b.Stator.Top.Position;
                        }
                        else
                        {
                            //ai = Vector3I.Zero;
                            //bi = Vector3I.Zero;
                            continue; // not on same grid, hmmmmm
                        }
                        //length = Math.Min(length, Math.Abs(Vector3I.Dot(ai - bi, diri)) * GridSize);
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

            protected bool AreAnyJointsHinges(IEnumerable<LegJoint> joints)
            {
                return joints.Any(j => j.IsHinge);
            }

            public override void Initialize()
            {
                base.Initialize();

                LegAnglesOffset = new LegAngles(Configuration.HipOffsets, Configuration.KneeOffsets, Configuration.FootOffsets, Configuration.QuadOffsets, Configuration.StrafeOffsets, Configuration.TurnOffsets);

                // add joints
                AllJoints.AddRange(LeftHipJoints);
                AllJoints.AddRange(LeftKneeJoints);
                AllJoints.AddRange(LeftFootJoints);
                AllJoints.AddRange(LeftTurnJoints);
                AllJoints.AddRange(LeftStrafeJoints);
                AllJoints.AddRange(RightHipJoints);
                AllJoints.AddRange(RightKneeJoints);
                AllJoints.AddRange(RightFootJoints);
                AllJoints.AddRange(RightTurnJoints);
                AllJoints.AddRange(RightStrafeJoints);

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
                Log("# L/R Turn   :", LeftTurnJoints.Count, "/", RightTurnJoints.Count);
                Log("Thigh Length :", ThighLength, "meters (set", Configuration.ThighLength?.ToString() ?? "auto", ")");
                Log("Calf  Length :", CalfLength, "meters (set", Configuration.CalfLength?.ToString() ?? "auto", ")");
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
                SetAnglesOf(LeftTurnJoints, left.TurnDegrees);
                SetAnglesOf(RightTurnJoints, right.TurnDegrees);
            }
            
            protected void UpdateMagnets(MovementInfo info, bool inversed=false)
            {
                // left leg starts lifting at 0.25
                // left leg lands at 0.75

                // (we use Offset, since then it just follows previous)
                // right leg starts lifting at 0.75
                // right leg lands at 0.25
                if (!magnetsEnabled)
                    return;
                bool leftDownStep = inversed ? !(AnimationStepOffset > .25d && AnimationStepOffset < .75d) : (AnimationStep > .25d && AnimationStep < .75d);
                bool rightDownStep = inversed ? !(AnimationStep > .25d && AnimationStep < .75d) : (AnimationStepOffset > .25d && AnimationStepOffset < .75d);

                bool leftDown = /*!info.Jumped &&*/ !moveInfo.Stopping ? ((info.Walk == 0 && info.Turn == 0 && info.Strafe == 0) || leftDownStep) : rightDownStep;
                bool isLeftDown = LeftMagnets.Any(m => m.IsLocked);
                
                bool rightDown = /*!info.Jumped &&*/ !moveInfo.Stopping ? ((info.Walk == 0 && info.Turn == 0 && info.Strafe == 0) || rightDownStep) : leftDownStep;
                bool isRightDown = RightMagnets.Any(m => m.IsLocked);

                foreach (var mag in LeftMagnets)
                {
                    if (!mag.IsWorking || mag.Closed)
                        continue;
                    if (mag.AutoLock != false)
                        mag.AutoLock = false;
                    if (leftDown && mag.LockMode == LandingGearMode.ReadyToLock)
                        mag.Lock();
                    if (!leftDown && mag.IsLocked && (!AssuredMagnets || ((!leftDown && !rightDown) || isRightDown))) // && isXDown should option be
                        mag.Unlock();
                }
                foreach (var mag in RightMagnets)
                {
                    if (!mag.IsWorking || mag.Closed)
                        continue;
                    if (mag.AutoLock != false)
                        mag.AutoLock = false;
                    if (rightDown && mag.LockMode == LandingGearMode.ReadyToLock)
                        mag.Lock();
                    if (!rightDown && mag.IsLocked && (!AssuredMagnets || ((!leftDown && !rightDown) || isLeftDown)))
                        mag.Unlock();
                }
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

            protected Vector3D[] cameraLeftRollingPositions = new Vector3D[3];
            protected int cameraLeftRollIndex = 1;
            protected Vector3D cameraLeftNormal;

            protected Vector3D[] cameraRightRollingPositions = new Vector3D[3];
            protected int cameraRightRollIndex = 1;
            protected Vector3D cameraRightNormal;

            protected double GetLeftTiltA()
            {
                if (!HipTiltCorrection)
                    return 0d;
                bool isLeftDown = LeftMagnets.Any(m => m.IsLocked);
                bool isRightDown = RightMagnets.Any(m => m.IsLocked);

                //double leftTilt = GetLeftTilt(); // because it calculates the normal :D
                if (isLeftDown && isRightDown)
                    return 0d;

                if (isLeftDown)
                {
                    return 0d;
                }
                else if (isRightDown)
                {
                    return GetLeftTilt() - GetRightTilt();
                }
                return 0d; // GetRightTilt() - GetLeftTilt();
            }

            protected double GetRightTiltA()
            {
                if (!HipTiltCorrection)
                    return 0d;
                bool isLeftDown = LeftMagnets.Any(m => m.IsLocked);
                bool isRightDown = RightMagnets.Any(m => m.IsLocked);

                if (isLeftDown && isRightDown)
                    return 0d;

                if (isRightDown)
                {
                    return 0d;
                }
                else if (isLeftDown)
                {
                    return GetRightTilt() - GetLeftTilt();
                }
                return 0d; // GetLeftTilt() - GetRightTilt();
            }

            // TODO: de-duplicate left/right tilt methods
            protected double GetLeftTilt()
            {
                Vector3D forward = referenceForwards;
                Vector3D up = anyController?.WorldMatrix.Up.Normalized() ?? Vector3D.Up; // (-gravity).Normalized();

                if (cameraLeftNormal.IsZero())
                    return 0d;

                /*Vector3D a = cameraLeftRollingPositions[cameraLeftRollIndex];
                Vector3D b = cameraLeftRollingPositions[(cameraLeftRollIndex + 1) % 3];
                Vector3D c = cameraLeftRollingPositions[(cameraLeftRollIndex + 2) % 3];
                if (a == Vector3D.Zero || b == Vector3D.Zero || c == Vector3D.Zero)
                    return 0d;

                Vector3D normalUp = anyController?.WorldMatrix.Up ?? up;*/
                Vector3D normal = cameraLeftNormal;//ApproximateNormal3(a, b, c, normalUp);
                //currentUpNormal = normal;

                double mag = Math.Atan2(
                    Vector3D.Dot(forward, normal),
                    Vector3D.Dot(up, normal)
                );

                return mag;
            }

            protected double GetRightTilt()
            {
                Vector3D forward = referenceForwards;
                Vector3D up = anyController?.WorldMatrix.Up.Normalized() ?? Vector3D.Up; //(-gravity).Normalized();

                if (cameraRightNormal.IsZero())
                    return 0d;

                /*Vector3D a = cameraRightRollingPositions[cameraRightRollIndex];
                Vector3D b = cameraRightRollingPositions[(cameraRightRollIndex + 1) % 3];
                Vector3D c = cameraRightRollingPositions[(cameraRightRollIndex + 2) % 3];
                if (a == Vector3D.Zero || b == Vector3D.Zero || c == Vector3D.Zero)
                    return 0d;

                Vector3D normalUp = anyController?.WorldMatrix.Up ?? up;*/
                Vector3D normal = cameraRightNormal; //ApproximateNormal3(a, b, c, normalUp);

                // then this is how it determines the "tilt" from the angles (below) it uses the cockpit forwards which obviously isn't ideal
                double mag = Math.Atan2(
                    Vector3D.Dot(forward, normal),
                    Vector3D.Dot(up, normal)
                );

                return mag;
            }

            /// <summary>
            /// Update the cameras
            /// Not a part of the base Update method for optionality
            /// </summary>
            protected MyTuple<double, double> UpdateCameras()
            {
                var grav = double.IsPositiveInfinity(currentUpNormal.X) ? gravity.Normalized() : -currentUpNormal; // anyController?.WorldMatrix.Down ?? gravity.Normalized(); //gravity.Normalized();
                Log("Camera gravity:", grav);
                Log("Cameras:", LeftCameras.Count, RightCameras.Count);
                if (LeftCameras.Count > 0 && RightCameras.Count > 0)
                {
                    double left = double.NegativeInfinity;
                    double right = double.NegativeInfinity;
                    foreach (var cam in LeftCameras.Concat(RightCameras))
                        cam.EnableRaycast = true;
                    // this is not ideal, it should Cameras.SetGroup by side, not by left/right
                    // but... this works and isn't *that* bad... right?
                    bool canScan = LeftCameras.Any(c => c.CanScan(20)) && RightCameras.Any(c => c.CanScan(20));
                    if (canScan)
                    {
                        //MyDetectedEntityInfo hit;
                        foreach (var camera in LeftCameras)
                        {
                            camera.EnableRaycast = true;
                            if (!camera.CanScan(20 * 3))
                                continue;
                            /*hit = camera.Raycast(20, 2 * cameraLeftRollIndex, 2 * cameraLeftRollIndex);
                            if (!hit.IsEmpty() && hit.EntityId != camera.CubeGrid.EntityId)
                            {
                                // #if DEBUG
                                Singleton.buildTools.DrawVector(camera.GetPosition(), hit.HitPosition.Value, Color.Wheat, 0.05f);
                                // #endif
                                var dot = Vector3D.Dot(grav, hit.HitPosition.Value);
                                if (!double.IsNaN(dot))
                                    left = Math.Max(left, dot);
                                if (Vector3D.Distance(cameraLeftRollingPositions[cameraLeftRollIndex], hit.HitPosition.Value) > 0.5f)
                                {
                                    cameraLeftRollIndex = (cameraLeftRollIndex + 1) % cameraLeftRollingPositions.Length;
                                    cameraLeftRollingPositions[cameraLeftRollIndex] = hit.HitPosition.Value;
                                }
                            }*/
                            var a = camera.Raycast(20d, 0f, 0f);
                            var b = camera.Raycast(20d, 2f, 0f);
                            var c = camera.Raycast(20d, 0f, 2f);
                            if (a.HitPosition.HasValue && b.HitPosition.HasValue && c.HitPosition.HasValue)
                            {
                                var dot = Vector3D.Dot(grav, a.HitPosition.Value);
                                if (!double.IsNaN(dot))
                                    left = Math.Max(left, dot);
                                Singleton.buildTools.DrawVector(camera.GetPosition(), a.HitPosition.Value, Color.Wheat, 0.05f);
                                Singleton.buildTools.DrawVector(camera.GetPosition(), b.HitPosition.Value, Color.Wheat, 0.05f);
                                Singleton.buildTools.DrawVector(camera.GetPosition(), c.HitPosition.Value, Color.Wheat, 0.05f);
                                var norm = ApproximateNormal3(a.HitPosition.Value, b.HitPosition.Value, c.HitPosition.Value, anyController?.WorldMatrix.Up ?? Vector3D.Up);
                                if (Vector3D.IsZero(cameraLeftNormal))
                                    cameraLeftNormal = norm;
                                else
                                    cameraLeftNormal = Vector3D.Lerp(cameraLeftNormal, norm, 0.5f);
                                cameraLeftNormal.Normalize();
                                if (CamerasDetermineGravity)
                                {
                                    if (double.IsNaN(currentUpNormal.X))
                                        currentUpNormal = norm;
                                    currentUpNormal = Vector3D.Lerp(currentUpNormal, norm, 0.5f);
                                    currentUpNormal.Normalize();
                                }
                            }
                        }
                        foreach (var camera in RightCameras)
                        {
                            camera.EnableRaycast = true;
                            if (!camera.CanScan(20 * 3))
                                continue;
                            /*hit = camera.Raycast(20, 2 * cameraRightRollIndex, 2 * cameraRightRollIndex);
                            if (!hit.IsEmpty() && hit.EntityId != camera.CubeGrid.EntityId)
                            {
                                // #if DEBUG
                                Singleton.buildTools.DrawVector(camera.GetPosition(), hit.HitPosition.Value, Color.Wheat, 0.05f);
                                // #endif
                                double dot = Vector3D.Dot(grav, hit.HitPosition.Value);
                                if (!double.IsNaN(dot))
                                    right = Math.Max(right, dot);
                                if (Vector3D.Distance(cameraRightRollingPositions[cameraRightRollIndex], hit.HitPosition.Value) > 0.5f)
                                {
                                    cameraRightRollIndex = (cameraRightRollIndex + 1) % cameraRightRollingPositions.Length;
                                    cameraRightRollingPositions[cameraRightRollIndex] = hit.HitPosition.Value;
                                }
                            }*/
                            var a = camera.Raycast(20d, 0f, 0f);
                            var b = camera.Raycast(20d, 2f, 0f);
                            var c = camera.Raycast(20d, 0f, 2f);
                            if (a.HitPosition.HasValue && b.HitPosition.HasValue && c.HitPosition.HasValue)
                            {
                                Singleton.buildTools.DrawVector(camera.GetPosition(), a.HitPosition.Value, Color.Wheat, 0.05f);
                                Singleton.buildTools.DrawVector(camera.GetPosition(), b.HitPosition.Value, Color.Wheat, 0.05f);
                                Singleton.buildTools.DrawVector(camera.GetPosition(), c.HitPosition.Value, Color.Wheat, 0.05f);
                                double dot = Vector3D.Dot(grav, a.HitPosition.Value);
                                if (!double.IsNaN(dot))
                                    right = Math.Max(right, dot);
                                var norm = ApproximateNormal3(a.HitPosition.Value, b.HitPosition.Value, c.HitPosition.Value, anyController?.WorldMatrix.Up ?? Vector3D.Up);
                                if (Vector3D.IsZero(cameraRightNormal))
                                    cameraRightNormal = norm;
                                else
                                    cameraRightNormal = Vector3D.Lerp(cameraRightNormal, norm, 0.5f);
                                cameraRightNormal.Normalize();
                                if (CamerasDetermineGravity)
                                {
                                    if (double.IsNaN(currentUpNormal.X))
                                        currentUpNormal = norm;
                                    currentUpNormal = Vector3D.Lerp(currentUpNormal, norm, 0.5f);
                                    currentUpNormal.Normalize();
                                }
                            }
                        }
                    }

                    // level
                    /*bool valid = true;
                    hit = RightCameras[0].Raycast(20);
                    valid = !hit.IsEmpty();

                    if (valid)
                    {
                        Vector3D a = hit.HitPosition.Value;
                        hit = RightCameras[1].Raycast(20);
                        if (!hit.IsEmpty())
                        {
                            Vector3D b = hit.HitPosition.Value;

                            double dotA = Vector3D.Dot(grav, a);
                            double dotB = Vector3D.Dot(grav, b);

                            double c = Vector3D.Distance(RightCameras[0].GetPosition(), RightCameras[1].GetPosition());
                            normalangle = Math.Asin(Math.Abs(dotA - dotB) / c);
                        }
                    }*/
                    /*Log("a:", a);
                    Log("b:", b);
                    Log("da:", dotA);
                    Log("db:", dotB);
                    Log("da-db:", Math.Abs(dotA - dotB));
                    Log("c:", c);*/

                    if (LegHeightCorrection)
                    {
                        var x = Cameras.GetGroup(Configuration.Id);
                        if (!double.IsNegativeInfinity(left) && !double.IsNegativeInfinity(right))
                            Cameras.SetGroup(Configuration.Id, left, right);
                        else if (!double.IsNegativeInfinity(left) && double.IsNegativeInfinity(right))
                            Cameras.SetGroup(Configuration.Id, left, x.Item2);
                        else if (double.IsNegativeInfinity(left) && !double.IsNegativeInfinity(right))
                            Cameras.SetGroup(Configuration.Id, x.Item1, right);
                        Log("Camera Values:", x.Item1, x.Item2);
                    }
                }

                var cameraOffsets = Cameras.CalculateGroup(Configuration.Id);
                cameraOffsetTween.Item1 += (cameraOffsets.Item1 - cameraOffsetTween.Item1) / CameraOffsetTweenMultiplier;
                cameraOffsetTween.Item2 += (cameraOffsets.Item2 - cameraOffsetTween.Item2) / CameraOffsetTweenMultiplier;
                // TODO: temp
                //cameraOffsetTween.Item1 = 0;
                //cameraOffsetTween.Item2 = 0;
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
                        if (!(block.Block is IMyMotorStator)) // block fetcher supports pistons for prismatic leg type, ignore it here
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
                    case BlockType.Turn:
                        AddLeftRightBlock(LeftTurnJoints, RightTurnJoints, new LegJoint(block), block.Side);
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
