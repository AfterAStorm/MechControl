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
        Vector3 ProjectOnPlane(Vector3 a, Vector3 b)
        {
            Vector3 projection = (Vector3.Dot(a, b) / b.Length()) * (b / b.Length());
            return a - projection;
        }

        double AngleBetween(Vector3 a, Vector3 b)
        {
            return Math.Atan2(Vector3.Cross(a, b).Length(), Vector3.Dot(a, b));
        }


        List<Gyroscope> stabilizationGyros = new List<Gyroscope>();
        List<RotorGyroscope> azimuthStators = new List<RotorGyroscope>();
        List<RotorGyroscope> elevationStators = new List<RotorGyroscope>();
        List<RotorGyroscope> rollStators = new List<RotorGyroscope>();

        void FetchStabilizers()
        {
            azimuthStators.Clear();
            elevationStators.Clear();
            rollStators.Clear();
            foreach (FetchedBlock block in BlockFinder.GetBlocksOfType<IMyMotorStator>(motor => BlockFetcher.ParseBlock(motor).HasValue).Select(motor => BlockFetcher.ParseBlock(motor)))
            {
                switch (block.Type)
                {
                    case BlockType.GyroscopeAzimuth:
                        azimuthStators.Add(new RotorGyroscope(block));
                        break;
                    case BlockType.GyroscopeElevation:
                        elevationStators.Add(new RotorGyroscope(block));
                        break;
                    case BlockType.GyroscopeRoll:
                        if (block.Side != BlockSide.Right)
                            return; // since r is keyword, we have to look for "g" then block side "r" :/
                        rollStators.Add(new RotorGyroscope(block));
                        break;
                }
            }

            stabilizationGyros.Clear();
            foreach (FetchedBlock block in BlockFinder.GetBlocksOfType<IMyGyro>(gyro => BlockFetcher.ParseBlock(gyro).HasValue).Select(gyro => BlockFetcher.ParseBlock(gyro)))
                switch (block.Type)
                {
                    case BlockType.GyroscopeAzimuth:
                    case BlockType.GyroscopeElevation:
                    case BlockType.GyroscopeRoll:
                    case BlockType.GyroscopeStabilization:
                    case BlockType.GyroscopeStop:
                        stabilizationGyros.Add(new Gyroscope(block));
                        break;
                }
        }

        void SetAngles(Gyroscope gyroBlock, float yaw, float pitch, float roll)
        {
            IMyGyro gyro = gyroBlock.Gyro;
            float max = gyro.CubeGrid.GridSizeEnum == MyCubeSize.Small ? 2f * (float)Math.PI : (float)Math.PI;
            float conversion = max / (gyro.CubeGrid.GridSizeEnum == MyCubeSize.Small ? 60f : 30f);
            // the conversion is the same either way... but the max isn't so it still counts :p
            yaw = MathHelper.Clamp(yaw * conversion, -max, max);
            pitch = MathHelper.Clamp(pitch * conversion, -max, max);
            roll = MathHelper.Clamp(roll * conversion, -max, max);
            if ((gyro.Yaw - yaw).Absolute() > .1f || (gyro.Yaw != 0 && yaw == 0))
            {
                Log($"SET {gyro.CustomName} YAW {gyro.Yaw} --> {yaw}");
                gyro.Yaw = yaw;
            }
            if ((gyro.Pitch - pitch).Absolute() > .1f || (gyro.Pitch != 0 && pitch == 0))
            {
                Log($"SET {gyro.CustomName} PITCH {gyro.Pitch} --> {pitch}");
                gyro.Pitch = pitch;
            }
            if ((gyro.Roll - roll).Absolute() > .1f || (gyro.Roll != 0 && roll == 0))
            {
                Log($"SET {gyro.CustomName} ROLL TO {gyro.Roll} --> {roll}");
                gyro.Roll = roll;
            }
        }

        void UpdateStabilization()
        {
            Log("-- Stabilization --");
            //bool overrideEnabled = !GyroscopesDisableOverride || turnValue != 0;
            IMyShipController reference = cockpits.Count > 0 ? cockpits.First() : null;
            if (reference == null)
            {
                Log("No reference for stabilization");
                return;
            }
            Vector3D gravity = reference.GetTotalGravity();
            if (gravity == null || reference.WorldMatrix == null)
                return;
            Vector3D up = reference.WorldMatrix.Up;
            Vector3D forward = reference.WorldMatrix.Forward.Normalized();
            Vector3D back = reference.WorldMatrix.Backward;
            Vector3D right = reference.WorldMatrix.Right;

            /*Vector3D gravityAlignedRight = Vector3D.Cross(gravity.Normalized(), -forward).Normalized();
            Vector3D gravityAlignedForward = Vector3D.Cross(gravity.Normalized(), gravityAlignedRight).Normalized();
            Vector3D gravityAlignedDown = Vector3D.Cross(gravityAlignedRight, forward).Normalized();

            double pitchDot = -Vector3D.Dot(gravityAlignedDown, gravityAlignedForward);
            double rollDot = -Vector3D.Dot(up, gravityAlignedRight);

            double pitch = Vector3D.Angle(forward, gravityAlignedForward) * Math.Sign(pitchDot);
            double roll = Vector3D.Angle(right, gravityAlignedRight) * Math.Sign(rollDot);*/

            //Vector3D plane = forward - (Vector3D.Dot(forward, gravity) / gravity.Length()) * (gravity / gravity.Length());
            double pitch = AngleBetween(forward, ProjectOnPlane(forward, gravity.Normalized())); //Math.Atan2(Vector3D.Cross(forward, plane).Normalize(), Vector3.Dot(forward, plane));
            Vector3D plane = right - (Vector3D.Dot(right, gravity) / gravity.Length()) * (gravity / gravity.Length());
            double roll = Math.Atan2(Vector3D.Cross(right, plane).Normalize(), Vector3.Dot(right, plane)) * Math.Sign(Vector3.Dot(right, gravity));

            pitch *= Math.Sign(forward.Dot(gravity.Normalized()));

            Log($"pitch?: {pitch} >< {forward.Dot(gravity.Normalized())}");
            Log($"roll? : {roll}");


            /*Vector3D crossed = gravity.Normalized().Cross(forward);
            Vector3D rollCrossed = gravity.Normalized().Cross(up);
            double rollDirection = (rollCrossed.Y) * 6;
            Log($"crossed:");
            Log($"{rollCrossed.X}");
            Log($"{rollCrossed.Y}");
            Log($"{rollCrossed.Z}");*/

            double pitchDirection = pitch * 2;
            double rollDirection = roll * 2;

            Log($"roll  dir: {rollDirection} for {rollStators.Count} rotors");
            Log($"pitch dir: {pitchDirection} for {elevationStators.Count} rotors");

            float azimuthValue = -movement.Y * ((float)SteeringSensitivity / 60f) * 60f;
            bool isTurning = azimuthValue.Absolute() > 0;
            foreach (var stator in azimuthStators)
            {
                if (!stator.Stator.IsSharingInertiaTensor())
                    Warn($"Share Inertia Tensor", $"Share intertia tensor is disabled for azimuth/yaw stabilization rotor {stator.Stator.CustomName}, enable it for better results");
                //foreach (var gyro in stator.SubGyros)
                //    gyro.Enabled = isTurning;

                stator.SetRPM(azimuthValue * (float)stator.Configuration.InversedMultiplier);
            }

            float elevationValue = (float)pitchDirection * 60;
            foreach (var stator in elevationStators)
            {
                if (!stator.Stator.IsSharingInertiaTensor())
                    Warn($"Share Inertia Tensor", $"Share intertia tensor is disabled for elevation/pitch stabilization rotor {stator.Stator.CustomName}, enable it for better results");
                foreach (var gyro in stator.SubGyros)
                    gyro.Enabled = /*isTurning ?*/ elevationValue.Absolute() > 0.5f;// : true;

                stator.SetRPM(elevationValue * (float)stator.Configuration.InversedMultiplier);
                // TODO
            }

            float rollValue = (float)rollDirection * 60f;
            foreach (var stator in rollStators)
            {
                if (!stator.Stator.IsSharingInertiaTensor())
                    Warn($"Share Inertia Tensor", $"Share intertia tensor is disabled for roll stabilization rotor {stator.Stator.CustomName}, enable it for better results");
                foreach (var gyro in stator.SubGyros)
                    gyro.Enabled = /*isTurning ?*/ rollValue.Absolute() > 0.1f;// : true;

                stator.SetRPM(rollValue * (float)stator.Configuration.InversedMultiplier);
            }

            float totalRotation = Math.Abs(azimuthValue) + Math.Abs(elevationValue) + Math.Abs(rollValue);
            bool isStabilizing = totalRotation > 3f;
            foreach (var gyro in stabilizationGyros)
            {
                if (gyro.GyroType == BlockType.GyroscopeRoll)
                    SetAngles(gyro, 0, 0, rollValue * (float)gyro.Configuration.InversedMultiplier);
                    //gyro.Gyro.Roll = rollValue * (float)gyro.Configuration.InversedMultiplier;
                else if (gyro.GyroType == BlockType.GyroscopeAzimuth)
                    SetAngles(gyro, -azimuthValue * (float)gyro.Configuration.InversedMultiplier, 0, 0);
                //gyro.Gyro.Yaw = -azimuthValue * (float)gyro.Configuration.InversedMultiplier;
                else if (gyro.GyroType == BlockType.GyroscopeElevation)
                    SetAngles(gyro, 0, elevationValue * (float)gyro.Configuration.InversedMultiplier, 0);
                //gyro.Gyro.Pitch = elevationValue * (float)gyro.Configuration.InversedMultiplier;

                else if (gyro.GyroType == BlockType.GyroscopeStabilization)
                    SetAngles(gyro,
                        -azimuthValue * (float)gyro.Configuration.InversedMultiplier,
                        elevationValue * (float)gyro.Configuration.InversedMultiplier,
                        rollValue * (float)gyro.Configuration.InversedMultiplier);

                if (gyro.GyroType == BlockType.GyroscopeAzimuth && !isTurning)
                    gyro.Gyro.Enabled = true;
                else if (gyro.GyroType == BlockType.GyroscopeStop)
                    gyro.Gyro.GyroOverride = gyro.Configuration.Inversed ? isStabilizing : !isStabilizing;
                else
                    gyro.Gyro.Enabled = true;/*isTurning ?*//*
                        gyro.Gyro.Roll.Absolute() > 0.1f ||
                        gyro.Gyro.Yaw.Absolute() > 0.1f ||
                        gyro.Gyro.Pitch.Absolute() > 0.1f;// : true;*/
            }
        }
    }
}
