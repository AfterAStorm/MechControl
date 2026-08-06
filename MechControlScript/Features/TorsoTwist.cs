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
        List<ArmJoint> torsoTwistStators = new List<ArmJoint>();

        double targetTorsoTwistAngle = -1;

        void FetchTorsoTwisters()
        {
            torsoTwistStators.Clear();
            torsoTwistStators.AddRange(blockFetcher.GetBlocks(BlockType.TorsoTwist).Select(fb =>
            {
                ArmJoint lj = new ArmJoint(fb)
                {
                    Configuration = ArmJointConfiguration.Parse(fb)
                };
                lj.Configuration.Save(Singleton.configManager.GetConfiguration(fb.Block));
                fb.Block.CustomData = fb.Ini.ToString();
                return lj;
            }));
            /*foreach (FetchedBlock block in blockFinder.GetBlocksOfType<IMyMotorStator>(motor => BlockFetcher.ParseBlockOne(motor).HasValue).Select(motor => BlockFetcher.ParseBlockOne(motor)))
            {
                switch (block.Type)
                {
                    case BlockType.TorsoTwist:
                        torsoTwistStators.Add(new LegJoint(block));
                        break;
                }
            }*/
        }

        float CalculateTorsoTwistVelocity(ArmJoint lj, float rotationInputTT)
        {
            return rotationInputTT * (float)lj.Configuration.Multiplier;
        }

        /// <summary>
        /// Torso twist handling
        /// </summary>
        /// <param name="rotationInput">The current mouse value, for rotating the torso (top half)</param>
        void UpdateTorsoTwist()
        {
            float rotationInputTT = rotationInput.Y;
            //float torsoTwist = MathHelper.Clamp(rotationInputTT * TorsoTwistSensitivity, -TorsoTwistMaxSpeed, TorsoTwistMaxSpeed);
            // Handle torso twist set angle
            if (rotationInputTT == 0 && targetTorsoTwistAngle > -1) // if we aren't trying to move and a set torso twist angle command requested a certain angle, go to it
            {
                bool done = true;
                foreach (var joint in torsoTwistStators)
                {
                    var target = targetTorsoTwistAngle + joint.Configuration.Offset;
                    joint.SetAngle(target * joint.Configuration.InversedMultiplier);
                    if ((joint.Stator.Angle.ToDegrees() - target * joint.Configuration.InversedMultiplier).Absolute() > 0.02d)
                        done = false;
                }
                if (done)
                    targetTorsoTwistAngle = -1;
            }
            else // otherwise, just handle user input
            {
                targetTorsoTwistAngle = -1;
                foreach (var joint in torsoTwistStators)
                    joint.Stator.TargetVelocityRPM = CalculateTorsoTwistVelocity(joint, rotationInputTT) * (float)joint.Configuration.InversedMultiplier;
            }
        }
    }
}
