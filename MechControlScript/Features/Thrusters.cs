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
        List<IMyThrust> thrusters = new List<IMyThrust>();
        List<Joint> azimuthVtolStators = new List<Joint>();
        List<Joint> elevationVtolStators = new List<Joint>();
        List<Joint> rollVtolStators = new List<Joint>();
        ThrusterMode thrusterBehavior = ThrusterMode.Override;
        Vector3 vectorMovement = Vector3.Zero;

        bool thrustersEnabled = false;
        bool thrustersOnMainGrid = false;
        bool thrustersVtol = false;

        public void FetchThrusters()
        {
            thrusters.Clear();
            thrusters.AddRange(blockFetcher.GetBlocks(BlockType.Thruster).Select(fb => fb.Block as IMyThrust));/*blockFinder.GetBlocksOfType<IMyThrust>()
                .Select(BlockFetcher.ParseBlockOne)
                .Where(f => f.HasValue)
                .Select(f => f.Value)
                .Where(f => f.Type == BlockType.Thruster)
                .Select(f => f.Block as IMyThrust));*/
            azimuthVtolStators.Clear();
            azimuthVtolStators.AddRange(blockFetcher.GetBlocks(BlockType.VtolAzimuth).Select(fb => new Joint(fb)));
            elevationVtolStators.Clear();
            elevationVtolStators.AddRange(blockFetcher.GetBlocks(BlockType.VtolElevation).Select(fb => new Joint(fb)));
            rollVtolStators.Clear();
            rollVtolStators.AddRange(blockFetcher.GetBlocks(BlockType.VtolRoll).Select(fb => new Joint(fb)));
            thrustersOnMainGrid = thrusters.Any(t => t.CubeGrid == Me.CubeGrid);
        }

        public void UpdateThrusters()
        {
            Log("-- Thrusters --");
            IMyShipController reference = cockpits.Count > 0 ? cockpits.First() : null;

            if (reference == null)
            {
                Log("No reference for thrusters");
                return;
            }

            Vector3 moveDirection = thrustersEnabled && thrustersVtol ? parsedMoveInput : Vector3.Zero;

            // see "Legs.cs"
            if (moveDirection != Vector3.Zero || AutoHalt)
            {
                vectorMovement.X = Translate(vectorMovement.X, moveDirection.X, AccelerationMultiplier, DecelerationMultiplier);
                vectorMovement.Y = Translate(vectorMovement.Y, moveDirection.Y, AccelerationMultiplier, DecelerationMultiplier);
                vectorMovement.Z = Translate(vectorMovement.Z, moveDirection.Z, AccelerationMultiplier, DecelerationMultiplier);
            }

            Log($"thrustersEnabled:", thrustersEnabled);
            Log($"thrustersVtol:", thrustersVtol);
            Log($"azimuthVtolStators:", azimuthVtolStators.Count);
            Log($"elevationVtolStators:", elevationVtolStators.Count);
            Log($"rollVtolStators:", rollVtolStators.Count);
            Log($"vectorMovement:", vectorMovement);
            flyingOffset = new Vector3(vectorMovement.Z, vectorMovement.Y, vectorMovement.X);
            if (thrustersEnabled && thrustersVtol)
            {
                // manage vtol mode
                foreach (var joint in azimuthVtolStators)
                    joint.SetAngle(vectorMovement.Y * 90d * (joint.Source.Inverted ? -1d : 1d));
                foreach (var joint in elevationVtolStators)
                {
                    if (azimuthVtolStators.Contains(joint))
                    {
                        if (Math.Abs(vectorMovement.Y) < Math.Abs(vectorMovement.Z))
                            joint.SetAngle(vectorMovement.Z * 90d * (joint.Source.Inverted ? -1d : 1d));
                    }
                    else
                    {
                        joint.SetAngle(vectorMovement.Z * 90d * (joint.Source.Inverted ? -1d : 1d));
                    }
                }
                foreach (var joint in rollVtolStators)
                    joint.SetAngle(vectorMovement.X * 90d * (joint.Source.Inverted ? -1d : 1d));
            }
            else
            {
                foreach (var joint in azimuthVtolStators.Concat(elevationVtolStators).Concat(rollVtolStators))
                    joint.SetAngle(0);
            }

            // if we can use Z, use that (as well as piloted controller); otherwise rely on commands
            if (thrustersOnMainGrid && controller != null)
            {
                thrusterBehavior = reference.DampenersOverride ? ThrusterMode.Hover : ThrusterMode.Override;
            }
            Log($"thrusters:", thrusters.Count);
            Log($"thruster mode:", thrusterBehavior);
            Log($"moveInput.Y:", moveInput.Y);

            foreach (IMyThrust thruster in thrusters)
            {
                thruster.ThrustOverridePercentage = moveInput.Y > 0 ? 1 : 0; //(moveInput.Y > 0 && thrusterBehavior == ThrusterMode.Override) ? 1 : 0;
                thruster.Enabled = thrustersEnabled && (thrusterBehavior == ThrusterMode.Hover ? (moveInput.Y >= 0) : moveInput.Y > 0); // thrustersEnabled && (thrusterBehavior == ThrusterMode.Hover || (moveInput.Y > 0));
            }
        }
    }
}
