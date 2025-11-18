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

        bool stabilizationEnabled = true;

        List<Gyroscope> stabilizationGyros = new List<Gyroscope>();
        List<RotorGyroscope> azimuthStators = new List<RotorGyroscope>();
        List<RotorGyroscope> elevationStators = new List<RotorGyroscope>();
        List<RotorGyroscope> rollStators = new List<RotorGyroscope>();

        void FetchStabilizers()
        {
            azimuthStators.Clear();
            azimuthStators.AddRange(blockFetcher.GetBlocks(BlockType.GyroscopeAzimuth).Where(fb => fb.Block is IMyMotorStator).Select(fb => new RotorGyroscope(fb, blockFinder)));
            elevationStators.Clear();
            elevationStators.AddRange(blockFetcher.GetBlocks(BlockType.GyroscopeElevation).Where(fb => fb.Block is IMyMotorStator).Select(fb => new RotorGyroscope(fb, blockFinder)));
            rollStators.Clear();
            rollStators.AddRange(blockFetcher.GetBlocks(BlockType.GyroscopeRoll).Where(fb => fb.Block is IMyMotorStator).Select(fb => new RotorGyroscope(fb, blockFinder)));
            stabilizationGyros.Clear();
            stabilizationGyros.AddRange(blockFetcher.CachedBlocks.Where(fb => fb.Block is IMyGyro).Select(fb => new Gyroscope(fb)));
            stabilizationEnabled = azimuthStators.Count > 0 || elevationStators.Count > 0 || rollStators.Count > 0 || stabilizationGyros.Count > 0;
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

        public static Vector3D gravity = Vector3D.Zero;

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
            gravity = reference.GetTotalGravity();
            Log("gravity:", gravity);
            if (!stabilizationEnabled)
                return;
            if (double.IsNaN(gravity.X) || gravity.LengthSquared() == 0) // why can it be 0,0,0? AAAAAAAAAAAAAAAAAAAA
            {
                Log("Not in gravity well or invalid world reference");
                return;
            }

            // new stuff

            Vector3D gravityNormal = gravity.Normalized();
            Quaternion currentRot = Quaternion.CreateFromRotationMatrix(reference.WorldMatrix);

            // make the forward vector flat with the gravity
            Vector3D forwardProjection = Vector3D.Reject(reference.WorldMatrix.Forward, gravityNormal);

            // normalize it for reasons
            forwardProjection.Normalize();

            // get the difference between the target rotation and the current rotation, yaw isn't accounted for since it's only forward and up, not right
            Quaternion idealRotation = Quaternion.Inverse(currentRot) * Quaternion.CreateFromForwardUp(forwardProjection, -gravityNormal); // target - current?

            Vector3D eulerOffsets = MyMath.QuaternionToEuler(idealRotation); // pitch, roll, yaw? nope, PYR

            Log("offsets:");
            Log("x (pitch):", $"{eulerOffsets.X:f2}", $"{MathHelper.ToDegrees(eulerOffsets.X):f2}");
            // always |x| < 1e-8 (ish) since yaw is cut out at the Reject-- there really is no definitive "yaw" unless you get the planet's matrix?
            Log("y (yaw  ):", $"{eulerOffsets.Y:f2}", $"{MathHelper.ToDegrees(eulerOffsets.Y):f2}");
            Log("z (roll ):", $"{eulerOffsets.Z:f2}", $"{MathHelper.ToDegrees(eulerOffsets.Z):f2}");

            var velocities = reference.GetShipVelocities();
            var angularVelocities = Vector3D.TransformNormal(velocities.AngularVelocity, MatrixD.Transpose(reference.WorldMatrix));
            Log("grid velocity (relative to reference):");
            Log("x (pitch):", $"{angularVelocities.X:f2}", $"{MathHelper.ToDegrees(angularVelocities.X):f2}");
            Log("y (yaw  ):", $"{angularVelocities.Y:f2}", $"{MathHelper.ToDegrees(angularVelocities.Y):f2}");
            Log("z (roll ):", $"{angularVelocities.Z:f2}", $"{MathHelper.ToDegrees(angularVelocities.Z):f2}");

            /*while (eulerOffsets.X > Math.PI) // sure.. why not
                eulerOffsets.X -= 2 * Math.PI;
            while (eulerOffsets.X < -Math.PI)
                eulerOffsets.X += 2 * Math.PI;
            while (eulerOffsets.Z > Math.PI) // oh boy, i love math
                eulerOffsets.Z -= 2 * Math.PI;
            while (eulerOffsets.Z < -Math.PI)
                eulerOffsets.Z += 2 * Math.PI;*/

            double pitchSpeed = -eulerOffsets.X * (60 / (Math.PI * 2)) * 30; // convert RAD/S to RPM, then apply speed multiplier
            double rollSpeed  = eulerOffsets.Z * (60 / (Math.PI * 2)) * 30;

            Log($"roll  dir: {rollSpeed} for {rollStators.Count} rotors");
            Log($"pitch dir: {pitchSpeed} for {elevationStators.Count} rotors");

            float azimuthValue = -movement.Y * ((float)SteeringSensitivity);
            bool isTurning = azimuthValue.Absolute() > 0;
            foreach (var stator in azimuthStators)
            {
                if (!stator.Stator.IsSharingInertiaTensor())
                    Warn($"Share Inertia Tensor", $"Share intertia tensor is disabled for azimuth/yaw stabilization rotor {stator.Stator.CustomName}, enable it for better results");
                //foreach (var gyro in stator.SubGyros)
                //    gyro.Enabled = isTurning;

                stator.SetRPM(azimuthValue * (float)stator.Configuration.InversedMultiplier);
            }

            float elevationValue = (float)pitchSpeed;
            foreach (var stator in elevationStators)
            {
                if (!stator.Stator.IsSharingInertiaTensor())
                    Warn($"Share Inertia Tensor", $"Share intertia tensor is disabled for elevation/pitch stabilization rotor {stator.Stator.CustomName}, enable it for better results");
                foreach (var gyro in stator.SubGyros)
                    gyro.Enabled = /*isTurning ?*/ elevationValue.Absolute() > 0.5f;// : true;

                stator.SetRPM(elevationValue * (float)stator.Configuration.InversedMultiplier);
                // TODO
            }

            float rollValue = (float)rollSpeed;
            foreach (var stator in rollStators)
            {
                if (!stator.Stator.IsSharingInertiaTensor())
                    Warn($"Share Inertia Tensor", $"Share intertia tensor is disabled for roll stabilization rotor {stator.Stator.CustomName}, enable it for better results");
                foreach (var gyro in stator.SubGyros)
                    gyro.Enabled = /*isTurning ?*/ rollValue.Absolute() > 0.1f;// : true;

                stator.SetRPM(rollValue * (float)stator.Configuration.InversedMultiplier);
            }

            // clamp so when "stable", it can abuse the overriden gyro's frozen state
            float epsilon = 0.1f;
            if (Math.Abs(azimuthValue) < epsilon)
                azimuthValue = 0f;
            if (Math.Abs(elevationValue) < epsilon)
                elevationValue = 0f;
            if (Math.Abs(rollValue) < epsilon)
                rollValue = 0f;

            float totalRotation = Math.Abs(azimuthValue) + Math.Abs(elevationValue) + Math.Abs(rollValue);
            bool isStabilizing = totalRotation > 3f;
            /*elevationValue /= Math.Abs(angularVelocities.X) <= 1 ? 1f : Math.Abs((float)angularVelocities.X);
            azimuthValue /= Math.Abs(angularVelocities.Y) <= 1 ? 1f : Math.Abs((float)angularVelocities.Y); // add some "dampening" (not dampening)
            rollValue /= Math.Abs(angularVelocities.Z) <= 1 ? 1f : Math.Abs((float)angularVelocities.Z);*/
            foreach (var gyro in stabilizationGyros)
            {
                /*Log($"pitch:", -elevationValue * (float)gyro.Configuration.InversedMultiplier);
                Log($"yaw:", -azimuthValue * (float)gyro.Configuration.InversedMultiplier);
                Log($"roll:", rollValue * (float)gyro.Configuration.InversedMultiplier);*/
                if (gyro.GyroType == BlockType.GyroscopeRoll)
                    gyro.SetOverrides(reference, 0, 0, rollValue * (float)gyro.Configuration.InversedMultiplier); //SetAngles(gyro, 0, 0, rollValue * (float)gyro.Configuration.InversedMultiplier);
                //gyro.Gyro.Roll = rollValue * (float)gyro.Configuration.InversedMultiplier;
                else if (gyro.GyroType == BlockType.GyroscopeAzimuth)
                    gyro.SetOverrides(reference, 0, -azimuthValue * (float)gyro.Configuration.InversedMultiplier, 0); //SetAngles(gyro, -azimuthValue * (float)gyro.Configuration.InversedMultiplier, 0, 0);
                //gyro.Gyro.Yaw = -azimuthValue * (float)gyro.Configuration.InversedMultiplier;
                else if (gyro.GyroType == BlockType.GyroscopeElevation)
                    gyro.SetOverrides(reference, -elevationValue * (float)gyro.Configuration.InversedMultiplier, 0, 0); //SetAngles(gyro, 0, elevationValue * (float)gyro.Configuration.InversedMultiplier, 0);
                //gyro.Gyro.Pitch = elevationValue * (float)gyro.Configuration.InversedMultiplier;


                else if (gyro.GyroType == BlockType.GyroscopeStabilization)
                    gyro.SetOverrides(reference, -elevationValue * (float)gyro.Configuration.InversedMultiplier, -azimuthValue * (float)gyro.Configuration.InversedMultiplier, rollValue * (float)gyro.Configuration.InversedMultiplier);
                    /*SetAngles(gyro,
                        -azimuthValue * (float)gyro.Configuration.InversedMultiplier,
                        elevationValue * (float)gyro.Configuration.InversedMultiplier,
                        rollValue * (float)gyro.Configuration.InversedMultiplier);*/

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
