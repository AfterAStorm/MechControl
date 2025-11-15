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
        public enum BlockType
        {
            // Leg
            Hip,
            Knee,
            Foot,
            Quad,
            Strafe,
            Turn,

            Hydraulic,

            // Arm
            ArmPitch,
            ArmYaw,
            ArmRoll,
            Magnet, // arm landing gear

            // Misc
            LandingGear,
            TorsoTwist,
            GyroscopeAzimuth, // rotor or gyroscope, yaw
            GyroscopeElevation, // rotor or gyroscope, pitch
            GyroscopeRoll, // rotor or gyroscope, roll
            GyroscopeStabilization,
            GyroscopeStop, // turn off when gyros in use

            // Thruster
            Thruster,
            VtolAzimuth,
            VtolElevation,
            VtolRoll,

            Camera
        }
    }
}
