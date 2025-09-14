using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using Sandbox.Game.EntityComponents;
using Sandbox.ModAPI.Ingame;
using Sandbox.ModAPI.Interfaces;
using SpaceEngineers.Game.ModAPI.Ingame;
using VRage;
using VRage.Collections;
using VRage.Game;
using VRage.Game.Components;
using VRage.Game.GUI.TextPanel;
using VRage.Game.ModAPI.Ingame;
using VRage.Game.ModAPI.Ingame.Utilities;
using VRage.Game.ObjectBuilders.Definitions;
using VRage.Profiler;
using VRageMath;

namespace IngameScript
{
    partial class Program : MyGridProgram
    {
        // Script //

        public static Program Singleton { get; private set; }
        public const string Version = "2.0.0-beta"; // major.minor.patch

        public static readonly double TicksPerSecond = 1d / 60d;

        public BlockFetcher blockFetcher;
        public BlockFinder blockFinder;

        // Diagnostics //

        static IMyTextPanel debugPanel = null;
        static bool debugMode = false;
        static bool debugModeClearOnLoop = true;

        double[] averageRuntimes = new double[AverageRuntimeSampleSize];
        int averageRuntimeIndex = 0;
        double maxRuntime = 0;
        int lastInstructions = 0;
        int maxInstructions = 0;

        //bool inputVisual = false; // TODO: figure out what this is for, i forgot :p

        // Logging //

        public static void Log(params object[] messages)
        {
            if (!debugMode)
                return;
            string message = string.Join(" ", messages);
            if (debugPanel != null)
                debugPanel.WriteText(message + "\n", true);
            else
                Singleton.Echo(message);
        }

        void Warn(string title, string info)
        {
            Echo($"[Color=#dcf71600]Warning: {title}[/Color]");
            Echo($"{info}\n");//Echo($"[Color=#c8e02d00]{info}[/Color]\n");
        }

        struct Warning
        {
            public string Title;
            public string Info;
        }

        static List<Warning> setupWarnings = new List<Warning>();

        public static void StaticWarn(string title, string info)
        {
            if (!setupWarnings.Any(w => w.Title == title))
                setupWarnings.Add(new Warning()
                {
                    Title = title,
                    Info = info
                });
        }

        // Variables //

        ScriptState state;

        int statusTick = 0;
        string[] statuses = new string[]
        {
            ">>>>>>",
            "->>>>>",
            "-->>>>",
            "--->>>",
            "---->>",
            "----->",
            "------",
            "-----<",
            "----<<",
            "---<<<",
            "--<<<<",
            "-<<<<<",
            "<<<<<<",
            "<<<<<-",
            "<<<<--",
            "<<<---",
            "<<----",
            "<-----",
            "------",
            ">-----",
            ">>----",
            ">>>---",
            ">>>>--",
            ">>>>>-",
            ">>>>>>",
            "->>>>>",
            "-->>>>",
            "--->>>",
            "---->>",
            "----->",
            "------",
            "-----<",
            "----<<",
            "---<<<",
            "--<<<<",
            "-<<<<<",
            "<<<<<<",
            "<<<<<-",
            "<<<<--",
            "<<<---",
            "<<----",
            "<-----",
            "------",
            ">-----",
            ">>----",
            ">>>---",
            ">>>>--",
            ">>>>>-",
            ">>>>>>",
            "           |",
            "        < ",
            "      < " ,
            "    < " ,
            "  < " ,
            "| " ,
            "  > " ,
            "    > " ,
            "      > " ,
            "        > ",
            "           |",
            "        < ",
            "      < " ,
            "    < " ,
            "  < " ,
            "| " ,
            "  > " ,
            "    > " ,
            "      > " ,
            "        > ",
            "           |",
            "        < ",
            "      < " ,
            "    < " ,
            "  < " ,
            "| " ,
            "  > " ,
            "    > " ,
            "      > " ,
            "        > ",
            "           |",
            "        < ",
            "      < " ,
            "    < " ,
            "  < " ,
            "| " ,
            "  > " ,
            "    > " ,
            "      > " ,
            "        > ",
            "           |",
            "        < ",
            "      < " ,
            "    < " ,
            "  < " ,
            "| " ,
            "  > " ,
            "    > " ,
            "      > " ,
            "        > ",
            "           |",
            /*"⠾",
            "⠷",
            "⠯",
            "⠟",
            "⠻",
            "⠽",*/
        };

        double delta = 0;
        double deltaOffset = 0;

        // Program //

        /*static IMyCameraBlock baseCamera;
        static Vector3D baseCameraSpot;
        static Vector3D baseGravity;
        static MyDetectedEntityType[] ignoreEntities = new MyDetectedEntityType[]
        {
            MyDetectedEntityType.CharacterHuman,
            MyDetectedEntityType.CharacterOther,
            MyDetectedEntityType.Missile,
            MyDetectedEntityType.Unknown
        };*/

        public Program()
        {
            //baseCamera = GridTerminalSystem.GetBlockWithName("Base Camera") as IMyCameraBlock;
            // define singleton
            Singleton = this;
            blockFinder = new BlockFinder(GridTerminalSystem);
            blockFetcher = new BlockFetcher(blockFinder);

            // load script state
            state = new ScriptState(this);
            Load(); // script state, only matters after initialization anyway
            Reload(); // reload to handle customdata stuffs

            // set runtime update freq.
            Runtime.UpdateFrequency = UpdateFrequency.Update1;
        }

        public void Fetch()
        {
            // diag
            //if (debugMode)
                debugPanel = Singleton.GridTerminalSystem.GetBlockWithName(DebugLCD) as IMyTextPanel;

            blockFetcher.Invalidate(); // "reset cache"

            // blocks
            FetchInputs(); // cockpits

            FetchStabilizers();
            FetchTorsoTwisters();
            FetchThrusters();

            FetchArms();
            FetchLegs();

            FetchTimerBlocks();
        }

        // State Management //

        public void Save()
        {
            Storage = state.Serialize();
            SaveConfig();
        }

        public void Load()
        {
            state.Parse(Storage ?? "");
            LoadConfig();
        }

        public void Reload()
        {
            setupWarnings.Clear();
            LoadConfig(); // allow recompile to be used (for pb, not legs)
            Save();
            Cameras.Reset();
            Load();
            Fetch();
            IterateHydraulics();
        }

        // Main Loop //

        public void Main(string argument, UpdateType updateSource)
        {
            Log($"Main({updateSource.ToString()})");
            // calculate delta
            double fakeDelta = /*Runtime.TimeSinceLastRun.TotalMilliseconds / 1000d*/ (1/60d) + deltaOffset;

            // run diagnostics
            double lastRuntime = Runtime.LastRunTimeMs;

            averageRuntimes[averageRuntimeIndex] = lastRuntime;
            averageRuntimeIndex = (averageRuntimeIndex + 1) % averageRuntimes.Length;
            maxRuntime = Math.Max(maxRuntime, lastRuntime);
            maxInstructions = Math.Max(maxInstructions, lastInstructions);

            // echo
            statusTick = (statusTick + 1) % statuses.Length;
            Echo($"[Color=#13ebca00]Mech Control Script[/Color] [Color=#00aaaaaa]>[/Color] [Color=#0034eb95]{Version}[/Color]");
            Echo($"{legs.Count} leg group{(legs.Count != 1 ? "s" : "")} | {arms.Count} arm{(arms.Count != 1 ? "s" : "")}{(!ShowStats ? $" | {statuses[statusTick]}" : "")}");
            Echo($"");
            if (ShowStats)
            {
                Echo($"Last       Tick: {lastRuntime:f3}ms");
                Echo($"Average Tick: {averageRuntimes.Sum() / averageRuntimes.Length:f3}ms over {averageRuntimes.Length} samples");
                Echo($"Max        Tick: {maxRuntime:f3}ms");
                Echo($"Last Instructions: {lastInstructions}");
                Echo($"Last Complexity: {lastInstructions / Runtime.MaxInstructionCount * 100:f1}%");
                Echo($"Max Instructions: {maxInstructions}");
                Echo($"Updates/s: {1 / fakeDelta:f1} up/s");
                Echo("");
            }

            // warnings
            if (setupMode)
                Warn("Setup Mode Active", "Any changes will be detected, beware that the script uses a lot more resources");

            if (debugMode)
            {
                Warn("Debug Mode Active", "Debug mode is active");
                if (debugPanel == null)
                {
                    Warn("No Debug Panel", $"No debug panel was found named \"{DebugLCD}\", using Echo instead");
                }
            }

            if (cockpits.Count <= 0)
            {
                List<IMyShipController> controllers = new List<IMyShipController>();
                GridTerminalSystem.GetBlocksOfType(controllers, c => c.IsSameConstructAs(Me));
                if (controllers.Count > 0) // if there is any actual controllers, add it to the warning message
                    Warn("No Cockpits Found!", "Failed to find any MAIN cockpits or remote controls; " +
                        $"try changing [Color=#0000ff77]{(controllers.Count > 1 ? $"one of the {controllers.Count} ship controllers[/Color] to the main cockpit?" : $"{controllers[0].CustomName}[/Color] to the main cockpit?")}");
                else
                    Warn("No Cockpits Found!", "Failed to find any MAIN cockpits or remote controls");
            }
            if (legs.Count <= 0) // how bruh gonna *walk* without legza?
                Warn("No Legs Found!", "Failed to find any leg groups!\nNeed help setting up? Check the documentation at github.com/AfterAStorm/MechControl/wiki");

            foreach (var warn in setupWarnings)
                Warn(warn.Title, warn.Info);

            // handle arguments / commands
            if (!string.IsNullOrEmpty(argument))
                foreach (var cmd in argument.Split('|'))
                    HandleCommand(cmd.Trim());

            // delta management
            if (!updateSource.HasFlag(UpdateType.Update1))
            {
                deltaOffset = fakeDelta; // add "fake" offset so it's accurate for real steps--just set since deltaOffset is already added
                lastInstructions = Runtime.CurrentInstructionCount;
                maxInstructions = Math.Max(lastInstructions, maxInstructions);
                return;
            }

            /*baseCamera.EnableRaycast = true;
            var a = baseCamera.Raycast(20);
            if (!a.IsEmpty() && !ignoreEntities.Contains(a.Type))
            {
                baseCameraSpot = a.HitPosition.Value;
                baseGravity = cockpits[0].GetTotalGravity();
            }*/
            
            // get delta
            delta = fakeDelta;
            deltaOffset = 0;

            // perform routines
            Log("---- MAIN LOOP ----");
            if (debugModeClearOnLoop && debugPanel != null)
                debugPanel.WriteText("", false);

            HandleSetup(); // setup mode
            UpdateInputs(); // cockpits

            UpdateStabilization();
            UpdateTorsoTwist();
            UpdateThrusters();

            UpdateArms();
            UpdateLegs();

            UpdateTimerBlocks();

            // profiling
            lastInstructions = Runtime.CurrentInstructionCount;
            maxInstructions = Math.Max(lastInstructions, maxInstructions);
        }
    }
}
