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
        #region # - Settings

        // - Controls

        /*
         * Mech Controls
         * W/S   >> Forward/Backward
         * A/D   >> Strafe Left/Right
         * Q/E   >> Turn Left/Right
         * C     >> Crouch
         * Space >> Jetpack
         * Mouse >> Torso Twist/Arm Control
         *
         * Reversed Mech Turn Controls
         * W/S   >> (see above)
         * A/D   >> Turn Left/Right
         * Q/E   >> Strafe Left/Right
         * C     >> (see above)
         * Space >> (see above)
         * Mouse >> (see above)
         *
         */

        bool ReverseTurnControls = false; // see above

        // - Mech

        static float StandingHeight = 1f; // a multiplier applied to some leg types
        //static ThrusterMode ThrusterBehavior = ThrusterMode.Override; // set the default thruster behavior, changable with commands. valid: Override, Hover

        static double StandingLean = 0d; // the offset of where the foot sits when standing (idling)
        static double AccelerationLean = 0d; // the offset of where the foot sits when walking

        // - Walking

        static float WalkCycleSpeed = 1f; // a global speed multiplier
        static float CrouchSpeed = 1f; // a global crouch speed multiplier
        static bool AutoHalt = true; // if it should stop walking when there is no one in the cockpit holding a direction

        // - Joints

        static float AccelerationMultiplier = 1f;   // how fast the mech accelerates, 1f is normal, .5f is half speed, 2f is double speed
        static float DecelerationMultiplier = 1f; // how fast the mech decelerates, same as above
        static bool IndependentStepEnabled = false;

        /*static float MaxRPM = float.MaxValue; // 60f is the max speed for rotors
                                              // *Configure motor limits in the blocks themselves!* // */

        static float TorsoTwistSensitivity = 1f; // how sensitive the torso twist is, can also change based on the rotor's torque
        static float TorsoTwistMaxSpeed = 60f; // maximum RPM of the torso twist rotor;

        // - Stablization / Steering

        static double SteeringSensitivity = 5; // x / 60th speed, specifies rotor/gyro RPM divided by 60, so 30 is half max power/rpm
        static bool SteeringTakesPriority = true; // should turning take priority over walking (animation wise)
        static double YawThreshold = 0;
        static double PitchThreshold = 0;
        static double RollThreshold = 0;

        // - Blocks

        string CockpitName = "auto"; // auto will find the main cockpit; optional (for manually controlling)
        string RemoteControlName = "auto"; // auto will find a main remote control; optional (for remote controlling)

        // -- Diagnostics

        bool ShowStats = true;
        string DebugLCD = "MCS_Debug";
        const int AverageRuntimeSampleSize = 15;

        #endregion

        #region # - Properties

        MyIni configIni = new MyIni();
        string configSection;

        #endregion

        #region # - Methods

        MyIniValue GetConfig(string key)
        {
            return configIni.Get(configSection, key);
        }

        void SetSection(string section) => configSection = section;
        void SetConfig(string key, double value) => configIni.Set(configSection, key, value);
        void SetConfig(string key, float value) => configIni.Set(configSection, key, value);
        void SetConfig(string key, string value) => configIni.Set(configSection, key, value);
        void SetConfig(string key, bool value) => configIni.Set(configSection, key, value);
        void SetConfigComment(string key, string value) => configIni.SetComment(configSection, key, value);

        void LoadConfig()
        {
            MyIniParseResult result;
            bool parsed = configIni.TryParse(Me.CustomData, out result);

            if (!result.Success)
            {
                StaticWarn("Invalid configuration", $"Failed to load main configuration:\n{result.ToString()}");
                return;
            }

            // parse -- the painful part
            SetSection("Controls");
            ReverseTurnControls = GetConfig("ReverseTurnControls").ToBoolean();
            AutoHalt = GetConfig("AutoHalt").ToBoolean(true);

            SetSection("Mech");
            StandingHeight = GetConfig("StandingHeight").ToSingle(.95f);
            //ThrusterBehavior = (ThrusterMode)Enum.Parse(typeof(ThrusterMode), GetConfig("ThrusterBehavior").ToString("Override"), true);

            StandingLean = GetConfig("StandingLean").ToDouble(0);
            AccelerationLean = GetConfig("AccelerationLean").ToDouble(0);

            // - Walking

            SetSection("Walking");
            WalkCycleSpeed = GetConfig("WalkSpeed").ToSingle(1f);
            CrouchSpeed = GetConfig("CrouchSpeed").ToSingle(1f);
            //AutoHalt = GetConfig("AutoHalt").ToBoolean(true);

            // - Joints

            SetSection("Joints");
            AccelerationMultiplier = GetConfig("AccelerationMultiplier").ToSingle(1f);
            DecelerationMultiplier = GetConfig("DecelerationMultiplier").ToSingle(1.5f);
            IndependentStepEnabled = GetConfig("IndependentStep").ToBoolean();

            //MaxRPM = GetConfig("MaxRPM").ToSingle(3600f);

            TorsoTwistSensitivity = GetConfig("TorsoTwistSensitivity").ToSingle(1f);
            TorsoTwistMaxSpeed = GetConfig("TorsoTwistMaxSpeed").ToSingle(3600f);

            // - Stablization / Steering

            SetSection("Stabilization");
            SteeringSensitivity = GetConfig("TurnSpeed"/*"SteeringSensitivity"*/).ToDouble(5);
            SteeringTakesPriority = GetConfig("SteeringTakesPriority").ToBoolean(false);
            YawThreshold = GetConfig("YawThreshold").ToDouble();
            PitchThreshold = GetConfig("PitchThreshold").ToDouble(5);
            RollThreshold = GetConfig("RollThreshold").ToDouble(5);

            // - Blocks

            SetSection("Blocks");
            CockpitName = GetConfig("CockpitName").ToString("auto");
            RemoteControlName = GetConfig("RemoteControlName").ToString("auto");

            // - Debug
            SetSection("Diagnostics");
            ShowStats = GetConfig("ShowStats").ToBoolean();
        }

        void SaveConfig()
        {
            configIni.Clear();

            SetSection("Controls");
            SetConfig("ReverseTurnControls", ReverseTurnControls);
            SetConfig("AutoHalt", AutoHalt);

            //SetSection("Mech");
            //SetConfig("StandingHeight", StandingHeight);
            //SetConfig("ThrusterBehavior", Enum.GetName(typeof(ThrusterMode), ThrusterBehavior));

            //SetConfig("StandingLean", StandingLean);
            //SetConfig("AccelerationLean", AccelerationLean);

            // - Walking

            //SetSection("Walking");
            //SetConfig("WalkSpeed", WalkCycleSpeed);
            //SetConfig("CrouchSpeed", CrouchSpeed);
            // SetConfig("AutoHalt", AutoHalt);

            // - Joints

            SetSection("Joints");
            SetConfig("AccelerationMultiplier", AccelerationMultiplier);
            SetConfig("DecelerationMultiplier", DecelerationMultiplier);
            SetConfig("IndependentStep", IndependentStepEnabled);

            //SetConfig("MaxRPM", MaxRPM); // OBSOLETE

            SetConfig("TorsoTwistSensitivity", TorsoTwistSensitivity);
            //SetConfig("TorsoTwistMaxSpeed", TorsoTwistMaxSpeed); // OBSOLETE

            // - Stablization / Steering

            SetSection("Stabilization");
            SetConfig("TurnSpeed"/*"SteeringSensitivity"*/, SteeringSensitivity);
            SetConfig("SteeringTakesPriority", SteeringTakesPriority);

            // - Blocks

            SetSection("Blocks");
            SetConfig("CockpitName", CockpitName);
            SetConfig("RemoteControlName", RemoteControlName);

            // - Diagnostics
            SetSection("Diagnostics");
            SetConfig("ShowStats", ShowStats);

            Me.CustomData = configIni.ToString();
        }

        #endregion
    }
}
