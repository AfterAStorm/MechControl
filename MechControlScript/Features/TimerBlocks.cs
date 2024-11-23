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
            WALK_FORWARDS,
            WALK_BACKWARDS,
            WALK,
            WALK_HALT,

            CROUCH,
            STAND,

            TURN_LEFT,
            TURN_RIGHT,
            TURN,
            TURN_HALT
        }

        public struct TimerBlock
        {
            public IMyTimerBlock Block;
            public TimerBlockEvent Event;
        }

        List<TimerBlock> timerBlocks = new List<TimerBlock>();
        string lastRun = "n/a";

        void UpdateTimerBlocks()
        {
            Log($"timerBlocks: {timerBlocks.Count}");
            if (!activeAnimation.IsWalk() && lastAnimation.IsWalk())
            {
                lastRun = "WALK_HALT";
                RunTimerblocksOfType(TimerBlockEvent.WALK_HALT);
            }
            else if (activeAnimation.IsWalk() && !lastAnimation.IsWalk())
            {
                lastRun = "WALK";
                RunTimerblocksOfType(TimerBlockEvent.WALK);
                float direction = movement.Z;
                if (direction > 0)
                    RunTimerblocksOfType(TimerBlockEvent.WALK_FORWARDS);
                else
                    RunTimerblocksOfType(TimerBlockEvent.WALK_BACKWARDS);
            }

            if (!activeAnimation.IsTurn() && lastAnimation.IsTurn())
            {
                lastRun = "TURN_HALT";
                RunTimerblocksOfType(TimerBlockEvent.TURN_HALT);
            }
            else if (activeAnimation.IsTurn() && !lastAnimation.IsTurn())
            {
                lastRun = "TURN";
                RunTimerblocksOfType(TimerBlockEvent.TURN);
                float direction = turnValue;
                if (direction > 0)
                    RunTimerblocksOfType(TimerBlockEvent.TURN_RIGHT);
                else
                    RunTimerblocksOfType(TimerBlockEvent.TURN_LEFT);
            }

            if (!activeAnimation.IsCrouch() && lastAnimation.IsCrouch())
            {
                lastRun = "STAND";
                RunTimerblocksOfType(TimerBlockEvent.STAND);
            }
            else if (activeAnimation.IsCrouch() && !lastAnimation.IsCrouch())
            {
                lastRun = "CROUCH";
                RunTimerblocksOfType(TimerBlockEvent.CROUCH);
            }
            Log($"timerBlocks last run: {lastRun}");
        }

        void RunTimerblocksOfType(TimerBlockEvent e)
        {
            foreach (TimerBlock tb in timerBlocks.Where(tb => tb.Event == e))
                tb.Block.Trigger();
            //timerBlocks.Where(tb => tb.Event == e).ForEach(tb => tb.Block.Trigger());
        }

        void FetchTimerBlocks()
        {
            List<IMyTimerBlock> tbs = new List<IMyTimerBlock>(); // allocation, i hardly know 'er!

            GridTerminalSystem.GetBlocksOfType(tbs, (tb) => tb.IsSameConstructAs(Me)); // is same construct, rotor+hinge+piston, exclude connectors
            timerBlocks.Clear();

            var regex = new System.Text.RegularExpressions.Regex("TB:(\\w+)(?::(\\w+))?");

            foreach (var tb in tbs)
            {
                var match = regex.Match(tb.CustomName);
                if (!match.Success)
                    continue;

                string action = match.Groups[1].Value.ToLower();
                string subaction = match.Groups[2].Value.ToLower();

                switch (action)
                {
                    case "walk":
                        switch (subaction)
                        {
                            case "forwards":
                                timerBlocks.Add(new TimerBlock() { Block = tb, Event = TimerBlockEvent.WALK_FORWARDS });
                                break;
                            case "backwards":
                                timerBlocks.Add(new TimerBlock() { Block = tb, Event = TimerBlockEvent.WALK_BACKWARDS });
                                break;
                            case "halt":
                                timerBlocks.Add(new TimerBlock() { Block = tb, Event = TimerBlockEvent.WALK_HALT });
                                break;
                            default:
                                timerBlocks.Add(new TimerBlock() { Block = tb, Event = TimerBlockEvent.WALK });
                                break;
                        }
                        break;
                    case "turn":
                        switch (subaction)
                        {
                            case "left":
                                timerBlocks.Add(new TimerBlock() { Block = tb, Event = TimerBlockEvent.TURN_LEFT });
                                break;
                            case "right":
                                timerBlocks.Add(new TimerBlock() { Block = tb, Event = TimerBlockEvent.TURN_RIGHT });
                                break;
                            case "halt":
                                timerBlocks.Add(new TimerBlock() { Block = tb, Event = TimerBlockEvent.TURN_HALT });
                                break;
                            default:
                                timerBlocks.Add(new TimerBlock() { Block = tb, Event = TimerBlockEvent.TURN });
                                break;
                        }
                        break;
                    case "crouch":
                        timerBlocks.Add(new TimerBlock() { Block = tb, Event = TimerBlockEvent.CROUCH });
                        break;
                    case "stand":
                        timerBlocks.Add(new TimerBlock() { Block = tb, Event = TimerBlockEvent.STAND });
                        break;
                }
            }
        }
    }
}
