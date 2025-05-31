using Sandbox.Game.EntityComponents;
using Sandbox.ModAPI.Ingame;
using Sandbox.ModAPI.Interfaces;
using SpaceEngineers.Game.ModAPI.Ingame;
using System.Collections.Generic;
using System.Collections;
using System.Linq;
using System.Text;
using System;
using VRage.Collections;
using VRage.Game.Components;
using VRage.Game.GUI.TextPanel;
using VRage.Game.ModAPI.Ingame.Utilities;
using VRage.Game.ModAPI.Ingame;
using VRage.Game.ObjectBuilders.Definitions;
using VRage.Game;
using VRage;
using VRageMath;
using System.Collections.Immutable;

namespace IngameScript
{
	partial class Program
	{
        public class QuadLegGroup : TriLegGroup
	    {

			public List<LegJoint> LeftQuadJoints  = new List<LegJoint>();
            public List<LegJoint> RightQuadJoints = new List<LegJoint>();

            public float AnkleLength { protected set; get; }

            public override void Initialize()
            {
                base.Initialize();

                // add joints
                AllJoints.AddRange(LeftQuadJoints);
                AllJoints.AddRange(RightQuadJoints);

                if (AllJoints.Count == 0)
                    return;

                // calculate lengths
                // we assume the left/right legs are both the same length.. at least for easy sake
                AnkleLength = Math.Max(FindJointLength(LeftFootJoints, LeftQuadJoints), FindJointLength(RightFootJoints, RightQuadJoints));

            }

            public override void Update(MovementInfo info)
            {
                base.Update(info);
                Log("# L/R Quads  :", LeftQuadJoints.Count, "/", RightQuadJoints.Count);
                Log("Ankle Length :", AnkleLength, "meters");
            }

            protected override void SetAngles(LegAngles left, LegAngles right)
            {
                base.SetAngles(left, right);
                SetAnglesOf(LeftQuadJoints, left.QuadDegrees, Configuration.HipOffsets);
                SetAnglesOf(RightQuadJoints, right.QuadDegrees, Configuration.HipOffsets);
            }

            public override void AddBlock(FetchedBlock block)
            {
                base.AddBlock(block);
                switch (block.Type)
                {
                    case BlockType.Quad:
                        AddLeftRightBlock(LeftQuadJoints, RightQuadJoints, new LegJoint(block), block.Side);
                        break;
                }
            }

        }
	}
}
