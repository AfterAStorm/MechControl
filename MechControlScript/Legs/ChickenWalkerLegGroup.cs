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
        public class ChickenWalkerLegGroup : HumanoidLegGroup
        {

            public new LegConfiguration DefaultConfiguration = new LegConfiguration()
            {
                VariableStandingHeight = new JointVariable(JointVariableType.Percentage, 90f),
                VariableXOffset = new JointVariable(JointVariableType.Percentage, 0),
                VariableZOffset = new JointVariable(JointVariableType.Percentage, 0),
                VariableStepLength = new JointVariable(JointVariableType.Percentage, 30f),
                VariableStepHeight = new JointVariable(JointVariableType.Percentage, 10f),
                VariableStrafeDistance = new JointVariable(JointVariableType.Percentage, 25f),
                VariableCrouchHeight = new JointVariable(JointVariableType.Percentage, 10f),
                AnimationSpeed = 1f,
                CrouchSpeed = 1f,

                HipOffsets = 0,
                KneeOffsets = 0,
                FootOffsets = 0,
                QuadOffsets = 0,
                StrafeOffsets = 0,
                VtolActive = true
            };

            public override double AnimationDirectionMultiplier => -1;

            protected override LegAngles LegAnglesMultiplier => LegAngles.MinusOne;
        }
    }
}
