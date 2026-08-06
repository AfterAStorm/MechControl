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
        public enum TimerBlockEvent
        {
            NONE,

            WALK_FORWARDS,
            WALK_BACKWARDS,
            WALK,
            WALK_HALT,

            TURN_LEFT,
            TURN_RIGHT,
            TURN,
            TURN_HALT,

            STRAFE_LEFT,
            STRAFE_RIGHT,
            STRAFE,
            STRAFE_HALT,

            CROUCH,
            STAND,

            POSE_START,
            POSE_LOOP,
            POSE_STOP
        }

        public struct TimerBlock
        {
            public IMyTimerBlock Block;
            public TimerBlockEvent Event;
            public string Subaction;
            public string Subsubaction;
        }

        List<TimerBlock> timerBlocks = new List<TimerBlock>();
        string lastRun = "n/a";

        HashSet<string> posesNeedsStoppedTB = new HashSet<string>();

        void UpdateTimerBlocks()
        {
            Log("-- Timers --");
            Log($"# of timer blocks: {timerBlocks.Count}");
            var current = moveInfo;
            var last = lastMoveInfo;

            if (last.Walk != 0 && current.Walk == 0)
            {
                RunTimerblocksOfType(TimerBlockEvent.WALK_HALT);
            }
            else if (current.Walk != 0 && last.Walk == 0)
            {
                RunTimerblocksOfType(TimerBlockEvent.WALK);
                float direction = current.Walk;
                if (direction > 0)
                    RunTimerblocksOfType(TimerBlockEvent.WALK_FORWARDS);
                else
                    RunTimerblocksOfType(TimerBlockEvent.WALK_BACKWARDS);
            }

            if (last.Turn != 0 && current.Turn == 0)
            {
                RunTimerblocksOfType(TimerBlockEvent.TURN_HALT);
            }
            else if (current.Turn != 0 && last.Turn == 0)
            {
                RunTimerblocksOfType(TimerBlockEvent.TURN);
                float direction = current.Turn;
                if (direction > 0)
                    RunTimerblocksOfType(TimerBlockEvent.TURN_RIGHT);
                else
                    RunTimerblocksOfType(TimerBlockEvent.TURN_LEFT);
            }

            if (last.Strafe != 0 && current.Strafe == 0)
            {
                RunTimerblocksOfType(TimerBlockEvent.STRAFE_HALT);
            }
            else if (current.Strafe != 0 && last.Strafe == 0)
            {
                RunTimerblocksOfType(TimerBlockEvent.STRAFE);
                float direction = current.Strafe;
                if (direction > 0)
                    RunTimerblocksOfType(TimerBlockEvent.STRAFE_RIGHT);
                else
                    RunTimerblocksOfType(TimerBlockEvent.STRAFE_LEFT);
            }

            if (last.Crouched && !current.Crouched)
            {
                RunTimerblocksOfType(TimerBlockEvent.STAND);
            }
            else if (current.Crouched && !last.Crouched)
            {
                RunTimerblocksOfType(TimerBlockEvent.CROUCH);
            }
            Log($"last event: {lastRun}");
        }

        void RunTimerblocksOfType(TimerBlockEvent e)
        {
            lastRun = e.ToString();
            foreach (TimerBlock tb in timerBlocks.Where(tb => tb.Event == e))
                tb.Block.Trigger();
        }

        void RunPoseTimerblocks(TimerBlockEvent e, string anim)
        {
            if (string.IsNullOrEmpty(anim))
                return;
            lastRun = $"pose {anim}/{e}";
            foreach (var tb in timerBlocks.Where(tb => tb.Event == e && tb.Subaction.Equals(anim, StringComparison.InvariantCultureIgnoreCase)))
            {
                tb.Block.Trigger();
            }
        }

        void FetchTimerBlocks()
        {
            List<IMyTimerBlock> tbs = new List<IMyTimerBlock>(); // allocation, i hardly know 'er!

            GridTerminalSystem.GetBlocksOfType(tbs, (tb) => tb.IsSameConstructAs(Me)); // is same construct, rotor+hinge+piston, exclude connectors
            timerBlocks.Clear();

            var regex = new System.Text.RegularExpressions.Regex(@"TB:(\w+)(?::(\w+))?(?::(\w+))?", System.Text.RegularExpressions.RegexOptions.IgnoreCase);

            foreach (var tb in tbs)
            {
                var match = regex.Match(tb.CustomName);
                if (!match.Success)
                    continue;

                string action = match.Groups[1].Value.ToLower();
                string subaction = match.Groups[2].Value.ToLower();
                string subsubaction = match.Groups[3].Value.ToLower();

                TimerBlockEvent evnt = TimerBlockEvent.NONE;
                switch (action)
                {
                    case "walk":
                        switch (subaction)
                        {
                            case "forwards":
                                evnt = TimerBlockEvent.WALK_FORWARDS;
                                break;
                            case "backwards":
                                evnt = TimerBlockEvent.WALK_BACKWARDS;
                                break;
                            case "halt":
                                evnt = TimerBlockEvent.WALK_HALT;
                                break;
                            default:
                                evnt = TimerBlockEvent.WALK;
                                break;
                        }
                        break;
                    case "turn":
                        switch (subaction)
                        {
                            case "left":
                                evnt = TimerBlockEvent.TURN_LEFT;
                                break;
                            case "right":
                                evnt = TimerBlockEvent.TURN_RIGHT;
                                break;
                            case "halt":
                                evnt = TimerBlockEvent.TURN_HALT;
                                break;
                            default:
                                evnt = TimerBlockEvent.TURN;
                                break;
                        }
                        break;
                    case "strafe":
                        switch (subaction)
                        {
                            case "left":
                                evnt = TimerBlockEvent.STRAFE_LEFT;
                                break;
                            case "right":
                                evnt = TimerBlockEvent.STRAFE_RIGHT;
                                break;
                            case "halt":
                                evnt = TimerBlockEvent.STRAFE_HALT;
                                break;
                            default:
                                evnt = TimerBlockEvent.STRAFE;
                                break;
                        }
                        break;
                    case "crouch":
                        evnt = TimerBlockEvent.CROUCH;
                        break;
                    case "stand":
                        evnt = TimerBlockEvent.STAND;
                        break;
                    case "pose":
                        switch (subsubaction)
                        {
                            case "start":
                                evnt = TimerBlockEvent.POSE_START;
                                break;
                            case "loop":
                                evnt = TimerBlockEvent.POSE_LOOP;
                                break;
                            default:
                            case "stop":
                                evnt = TimerBlockEvent.POSE_STOP;
                                break;
                        }
                        break;
                    default:
                        StaticWarn("Invalid timerblock type", $"Unknown type \"{action}\" for timer {tb.CustomName}");
                        break;
                }
                if (evnt != TimerBlockEvent.NONE)
                {
                    timerBlocks.Add(new TimerBlock() { Block = tb, Event = evnt, Subaction = subaction, Subsubaction = subsubaction });
                }
            }
        }
    }
}
