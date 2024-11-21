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
        List<LegJoint> torsoTwistStators = new List<LegJoint>();

        double targetTorsoTwistAngle = -1;

        void FetchTorsoTwisters()
        {
            torsoTwistStators.Clear();
            foreach (FetchedBlock block in BlockFinder.GetBlocksOfType<IMyMotorStator>(motor => BlockFetcher.ParseBlock(motor).HasValue).Select(motor => BlockFetcher.ParseBlock(motor)))
            {
                switch (block.Type)
                {
                    case BlockType.TorsoTwist:
                        torsoTwistStators.Add(new LegJoint(block));
                        break;
                }
            }
        }

        /// <summary>
        /// Torso twist handling
        /// </summary>
        /// <param name="rotationInput">The current mouse value, for rotating the torso (top half)</param>
        void UpdateTorsoTwist()
        {
            float rotationInputTT = rotationInput.Y;
            float torsoTwist = MathHelper.Clamp(rotationInputTT * TorsoTwistSensitivity, -TorsoTwistMaxSpeed, TorsoTwistMaxSpeed);
            // Handle torso twist set angle
            if (torsoTwist == 0 && targetTorsoTwistAngle > -1) // if we aren't trying to move and a set torso twist angle command requested a certain angle, go to it
            {
                bool done = true;
                foreach (var joint in torsoTwistStators)
                {
                    joint.SetAngle(targetTorsoTwistAngle * joint.Configuration.InversedMultiplier);
                    if ((joint.Stator.Angle - targetTorsoTwistAngle * joint.Configuration.InversedMultiplier).Absolute() > 0.05d)
                        done = false;
                }
                if (done)
                    targetTorsoTwistAngle = -1;
            }
            else // otherwise, just handle user input
            {
                targetTorsoTwistAngle = -1;
                foreach (var joint in torsoTwistStators)
                    joint.Stator.TargetVelocityRPM = torsoTwist * (float)joint.Configuration.InversedMultiplier;
            }
        }
    }
}
