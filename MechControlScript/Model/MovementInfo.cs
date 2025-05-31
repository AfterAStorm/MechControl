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
        public struct MovementInfo
        {
            /// <summary>
            /// X+ is strafe right
            /// X- is strafe left
            /// Y+ is turn right
            /// Y- is turn left
            /// Z+ is forward
            /// Z- is backwards
            /// </summary>
            public Vector3 Direction { get; set; } // the direction, so {0, 0, -1}

            /// <summary>
            /// X+ is strafe right
            /// X- is strafe left
            /// Y+ is turn right
            /// Y- is turn left
            /// Z+ is forward
            /// Z- is backwards
            /// </summary>
            public Vector3 Movement { get; set; } // the direction's values, so {0, 0, -.256116456}
            public double Delta { get; set; } // delta

            /// <summary>
            /// Is the mech "idle"?
            /// </summary>
            public bool Idle { get; set; }

            /// <summary>
            /// Is the mech "crouching"?
            /// </summary>
            public bool Crouched { get; set; }

            /// <summary>
            /// Is the mech "walking" (takes into account SteeringTakesPriority)
            /// <para>*true if strafing</para>
            /// </summary>
            public bool Walking { get; set; }

            public float Walk { get; set; }

            /// <summary>
            /// Is the mech "strafing"
            /// </summary>
            public bool Strafing { get; set; }

            public float Strafe { get; set; }

            /// <summary>
            /// Is the mech turning (takes into account SteeringTakesPriority)
            /// </summary>
            public bool Turning { get; set; }

            public float Turn { get; set; }

            /// <summary>
            /// Is the mech "flying"?
            /// </summary>
            public bool Flying { get; set; }
        }
    }
}
