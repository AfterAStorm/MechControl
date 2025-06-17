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
        public class TriLegGroup : LegGroup
	    {

			public List<LegJoint> LeftHipJoints  = new List<LegJoint>();
            public List<LegJoint> RightHipJoints = new List<LegJoint>();

            public List<LegJoint> LeftKneeJoints  = new List<LegJoint>();
            public List<LegJoint> RightKneeJoints = new List<LegJoint>();

            public List<LegJoint> LeftFootJoints  = new List<LegJoint>();
            public List<LegJoint> RightFootJoints = new List<LegJoint>();

            public List<LegJoint> LeftStrafeJoints = new List<LegJoint>();
            public List<LegJoint> RightStrafeJoints = new List<LegJoint>();

            public List<LegJoint> AllJoints = new List<LegJoint>();

            protected LegAngles LegAnglesOffset;

            public float GridSize { protected set; get; }

            public float ThighLength { protected set; get; }
			public float CalfLength { protected set; get; }

            protected float FindJointLength(List<LegJoint> jointsA, List<LegJoint> jointsB)
            {
                if (jointsA.Count == 0 || jointsB.Count == 0)
                    return 1;
                float length = float.PositiveInfinity;
                Vector3I ai, bi;
                foreach (var a in jointsA)
                {
                    foreach (var b in jointsB)
                    {
                        ai = a.Stator.Top.Position + Base6Directions.GetIntVector(a.Stator.Top.Orientation.Up);
                        bi = b.Stator.Position;
                        length = Math.Min(length, (ai - bi).Length() * GridSize);
                    }
                }
                return length;
            }

            public override void Initialize()
            {
                base.Initialize();

                LegAnglesOffset = new LegAngles(Configuration.HipOffsets, Configuration.KneeOffsets, Configuration.FootOffsets, Configuration.QuadOffsets, Configuration.StrafeOffsets);

                // add joints
                AllJoints.AddRange(LeftHipJoints);
                AllJoints.AddRange(LeftKneeJoints);
                AllJoints.AddRange(LeftFootJoints);
                AllJoints.AddRange(RightHipJoints);
                AllJoints.AddRange(RightKneeJoints);
                AllJoints.AddRange(RightFootJoints);

                if (AllJoints.Count == 0)
                    return;
                GridSize = AllJoints.First().Stator.CubeGrid.GridSize;

                // calculate lengths
                // we assume the left/right legs are both the same length.. at least for easy sake
                ThighLength = Configuration.ThighLength ?? Math.Max(FindJointLength(LeftHipJoints, LeftKneeJoints), FindJointLength(RightHipJoints, RightKneeJoints));
                CalfLength  = Configuration.CalfLength ?? Math.Max(FindJointLength(LeftKneeJoints, LeftFootJoints), FindJointLength(RightKneeJoints, RightFootJoints));

            }

            public override void Update(MovementInfo info)
            {
                base.Update(info);
                Log("# L/R Hips   :", LeftHipJoints.Count, "/", RightHipJoints.Count);
                Log("# L/R Knees  :", LeftKneeJoints.Count, "/", RightKneeJoints.Count);
                Log("# L/R Feet   :", LeftFootJoints.Count, "/", RightFootJoints.Count);
                Log("# L/R Strafe :", LeftStrafeJoints.Count, "/", RightStrafeJoints.Count);
                Log("Thigh Length :", ThighLength, "meters", Configuration.ThighLength);
                Log("Calf  Length :", CalfLength, "meters", Configuration.CalfLength);
            }

            protected override void SetAngles(LegAngles left, LegAngles right)
            {
                SetAnglesOf(LeftHipJoints, left.HipDegrees);
                SetAnglesOf(RightHipJoints, right.HipDegrees);
                SetAnglesOf(LeftKneeJoints, left.KneeDegrees);
                SetAnglesOf(RightKneeJoints, right.KneeDegrees);
                SetAnglesOf(LeftFootJoints, left.FeetDegrees);
                SetAnglesOf(RightFootJoints, right.FeetDegrees);
                SetAnglesOf(LeftStrafeJoints, left.StrafeDegrees);
                SetAnglesOf(RightStrafeJoints, right.StrafeDegrees);
            }

            public override void AddBlock(FetchedBlock block)
            {
                base.AddBlock(block);
                switch (block.Type)
                {
                    case BlockType.Hip:
                        AddLeftRightBlock(LeftHipJoints, RightHipJoints, new LegJoint(block), block.Side);
                        break;
                    case BlockType.Knee:
                        AddLeftRightBlock(LeftKneeJoints, RightKneeJoints, new LegJoint(block), block.Side);
                        break;
                    case BlockType.Foot:
                        AddLeftRightBlock(LeftFootJoints, RightFootJoints, new LegJoint(block), block.Side);
                        break;
                    case BlockType.Strafe:
                        AddLeftRightBlock(LeftStrafeJoints, RightStrafeJoints, new LegJoint(block), block.Side);
                        break;
                }
            }

        }
	}
}
