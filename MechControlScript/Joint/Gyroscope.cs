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

            public void SetOverrides(float pitch, float yaw, float roll) // PYR
            {
                if (!Gyro.GyroOverride)
                    Gyro.GyroOverride = true;
                Gyro.Pitch = pitch;
                Gyro.Yaw = yaw;
                Gyro.Roll = roll;
            }
        }
    }
}
