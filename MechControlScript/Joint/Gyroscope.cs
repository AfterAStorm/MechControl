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
        public class Gyroscope
        {

            public IMyGyro Gyro;
            public LegJointConfiguration Configuration;
            public BlockType GyroType;

            public Gyroscope(IMyGyro gyro, LegJointConfiguration? configuration = null)
            {
                Gyro = gyro;
                Configuration = configuration ?? LegJointConfiguration.DEFAULT;
            }

            public Gyroscope(FetchedBlock block)
            {
                GyroType = block.Type;
                Gyro = block.Block as IMyGyro;
                Configuration = new LegJointConfiguration()
                {
                    Inversed = block.Inverted,
                    Offset = 0
                };
            }

            // TODO: replace with MyMath.QuaternionToEuler?
            Vector3D QuaternionToEuler(Quaternion q)
            {
                // Normalize for safety
                q.Normalize();

                // Pitch (X-axis rotation)
                double sinp = 2 * (q.W * q.X + q.Y * q.Z);
                double cosp = 1 - 2 * (q.X * q.X + q.Y * q.Y);
                double pitch = Math.Atan2(sinp, cosp);

                // Yaw (Y-axis rotation)
                double siny = 2 * (q.W * q.Y - q.Z * q.X);
                siny = MathHelper.Clamp(siny, -1, 1); // protect against floating point
                double yaw = Math.Asin(siny);

                // Roll (Z-axis rotation)
                double sinr = 2 * (q.W * q.Z + q.X * q.Y);
                double cosr = 1 - 2 * (q.Y * q.Y + q.Z * q.Z);
                double roll = Math.Atan2(sinr, cosr);

                return new Vector3D(pitch, yaw, roll);
            }

            public void SetOverrides(IMyTerminalBlock reference, float pitch, float yaw, float roll)
            {
                if (Gyro.CubeGrid.Equals(reference.CubeGrid))
                    SetOverrides(reference.Orientation, pitch, yaw, roll);
                else
                    SetOverrides(pitch, yaw, roll);
            }

            public void SetOverrides(MyBlockOrientation reference, float pitch, float yaw, float roll)
            {
                var me = Gyro.Orientation;

                Quaternion meQuat;
                Quaternion refQuat;
                me.GetQuaternion(out meQuat);
                reference.GetQuaternion(out refQuat);

                Quaternion rel = Quaternion.Inverse(meQuat) * refQuat;

                Vector3 relRot = QuaternionToEuler(rel);
                Log("relRot");
                Log(rel.X, rel.Y, rel.Z, rel.W);

                Vector3D controlRates =
                    pitch * Vector3D.Right +
                    yaw * Vector3D.Up +
                    roll * Vector3D.Forward;

                Vector3 rot = Vector3.Transform(controlRates, rel);

                //Vector3 rot = QuaternionToEuler(adjusted);

                Log("SetOverrides");
                Log("yaw", yaw, "-->", rot.Y);
                Log("pitch", pitch, "-->", rot.X);
                Log("roll", roll, "-->", rot.Z);
                SetOverrides(-rot.X, rot.Y, rot.Z);
            }

            public void SetOverrides(float pitch, float yaw, float roll) // PYR, radians to target rotation
            {
                if (!Gyro.GyroOverride)
                    Gyro.GyroOverride = true;

                if (Math.Abs(Gyro.Pitch - pitch) > 2)
                {
                    Gyro.Pitch = pitch;
                }
                else if (pitch == 0 && Gyro.Pitch != 0)
                    Gyro.Pitch = 0;
                if (Math.Abs(Gyro.Yaw - yaw) > 2)
                {
                    Gyro.Yaw = yaw;
                }
                else if (yaw == 0 && Gyro.Yaw != 0)
                    Gyro.Yaw = 0;
                if (Math.Abs(Gyro.Roll - roll) > 2)
                {
                    Gyro.Roll = roll;
                }
                else if (roll == 0 && Gyro.Roll != 0)
                    Gyro.Roll = 0;

                //Gyro.Pitch = pitch;// * (float)(30f / (2f * Math.PI));
                //Gyro.Yaw = yaw;// * (float)(30f / (2f * Math.PI));
                //Gyro.Roll = roll;// * (float)(30f / (2f * Math.PI));
            }
        }
    }
}
