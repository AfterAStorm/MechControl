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

        /// <summary>
        /// A joint with multiple types
        /// </summary>
        class MultiJoint
        {
            public Joint Joint;
            public VtolJointConfiguration Configuration;
            public List<BlockType> Types;
            public List<float> Inverted;

            /// <summary>
            /// Target angle in radians
            /// </summary>
            public float TargetAngle;

            public MultiJoint(Joint joint)
            {
                Joint = joint;
                Configuration = VtolJointConfiguration.Parse(Joint.Source);
                Types = new List<BlockType>() { joint.Source.Type };
                Inverted = new List<float>() { joint.InvertedMultiplier };
                MyIni jointIni = Program.Singleton.configManager.GetConfiguration(Joint.Stator);
                Configuration.Save(jointIni);
                Joint.Stator.CustomData = jointIni.ToString(); //block.Block.CustomData = data;
                //Joint.Stator.CustomData = Configuration.ToCustomDataString();
            }

            /*public void Reset()
            {
                TargetAngle = 0;
            }*/

            public void Apply()
            {
                Joint.SetAngle(TargetAngle.ToDegrees(), Configuration.Multiplier);
            }
        }

        List<MultiJoint> stators = new List<MultiJoint>();
        Dictionary<IMyTerminalBlock, MultiJoint> statorMap = new Dictionary<IMyTerminalBlock, MultiJoint>();

        List<IMyThrust> thrusters = new List<IMyThrust>();
        List<Joint> azimuthVtolStators = new List<Joint>();
        List<Joint> elevationVtolStators = new List<Joint>();
        List<Joint> rollVtolStators = new List<Joint>();
        ThrusterMode thrusterBehavior = ThrusterMode.Override;
        /// <summary>
        /// X: Left/Right
        /// Y: Up/Down
        /// Z: Forward/Back
        /// W: Turn
        /// </summary>
        Vector4 vectorMovement = Vector4.Zero;

        bool thrustersEnabled = false;
        bool thrustersOnMainGrid = false;
        bool thrustersVtol = false;

        double pidIntegral = 0;
        double pidLastError = 0;

        public void FetchThrusters()
        {
            thrusters.Clear();
            thrusters.AddRange(blockFetcher.GetBlocks(BlockType.Thruster).Select(fb => fb.Block as IMyThrust));/*blockFinder.GetBlocksOfType<IMyThrust>()
                .Select(BlockFetcher.ParseBlockOne)
                .Where(f => f.HasValue)
                .Select(f => f.Value)
                .Where(f => f.Type == BlockType.Thruster)
                .Select(f => f.Block as IMyThrust));*/
            /*azimuthVtolStators.Clear();
            azimuthVtolStators.AddRange(blockFetcher.GetBlocks(BlockType.VtolAzimuth).Select(fb => new Joint(fb)));
            elevationVtolStators.Clear();
            elevationVtolStators.AddRange(blockFetcher.GetBlocks(BlockType.VtolElevation).Select(fb => new Joint(fb)));
            rollVtolStators.Clear();
            rollVtolStators.AddRange(blockFetcher.GetBlocks(BlockType.VtolRoll).Select(fb => new Joint(fb)));*/
            statorMap.Clear();
            stators.Clear();

            foreach (var fb in blockFetcher.GetBlocks(BlockType.VtolStrafe, BlockType.VtolVertical, BlockType.VtolForward, BlockType.VtolTurn, BlockType.VtolAzimuth, BlockType.VtolElevation))
            {
                if (statorMap.ContainsKey(fb.Block))
                {
                    statorMap[fb.Block].Types.Add(fb.Type);
                    statorMap[fb.Block].Inverted.Add(fb.Inverted ? -1f : 1f);
                }
                else
                {
                    statorMap.Add(fb.Block, new MultiJoint(new Joint(fb)));
                }
            }
            stators.AddRange(statorMap.Values);
            statorMap.Clear(); // not needed anymore

            thrustersOnMainGrid = thrusters.Any(t => t.CubeGrid == Me.CubeGrid);
        }

        public void ToggleThrustersEnabled(bool enabled)
        {
            if (thrustersEnabled && !enabled)
            {
                foreach (var th in thrusters)
                    th.Enabled = false;
            }
            thrustersEnabled = enabled;
        }

        public void ToggleVtolEnabled(bool enabled)
        {
            if (thrustersVtol && !enabled)
            {
                foreach (var th in stators)
                    th.Joint.SetRPM(0);
            }
            thrustersVtol = enabled;
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

            Vector3 moveDirection = thrustersEnabled ? parsedMoveInput : Vector3.Zero;

            /*
            VT+ / VT- for turning Q/E (based on ReverseTurnControls)
            VF+ / VF- for forward W/S
            VU+ / VU- for up C/SpaceBar
            VR+ / VR- for roll A/D (based on ReverseTurnControls)
            VY+ / VY- for yaw mouse < / >
            VP+ / VP- for pitch mouse ^ / v
            */

            /*
            turn     [A] and [D])
            forward  (Using [W] and [S])
            up       (Using [space] and [C])
            roll     (Using [Q] and [E])
            yaw      (Using [<] and [>] (and mouse))
            pitch    (Using [^] and [v] (and mouse))
            */

            // see "Legs.cs"
            if (moveDirection != Vector3.Zero || AutoHalt)
            {
                const float ACC_MULT = 1f;
                vectorMovement.X = Translate(vectorMovement.X, moveDirection.X, AccelerationMultiplier * ACC_MULT, DecelerationMultiplier * ACC_MULT, (float)moveInfo.Delta);
                vectorMovement.Y = Translate(vectorMovement.Y, parsedVerticalInput, AccelerationMultiplier * ACC_MULT, DecelerationMultiplier * ACC_MULT, (float)moveInfo.Delta);
                vectorMovement.Z = Translate(vectorMovement.Z, moveDirection.Z, AccelerationMultiplier * ACC_MULT, DecelerationMultiplier * ACC_MULT, (float)moveInfo.Delta);
                vectorMovement.W = Translate(vectorMovement.W, moveDirection.Y, AccelerationMultiplier * ACC_MULT, DecelerationMultiplier * ACC_MULT, (float)moveInfo.Delta);
            }

            Log($"thrustersEnabled:", thrustersEnabled);
            Log($"thrustersVtol:", thrustersVtol);
            Log($"azimuthVtolStators:", azimuthVtolStators.Count);
            Log($"elevationVtolStators:", elevationVtolStators.Count);
            Log($"rollVtolStators:", rollVtolStators.Count);
            Log($"vectorMovement:", vectorMovement);
            flyingOffset = thrustersVtol ? new Vector3(vectorMovement.Z, vectorMovement.Y, vectorMovement.X) : Vector3.Lerp(flyingOffset, Vector3D.Zero, 0.75f);
            /*if (thrustersEnabled && thrustersVtol)
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
            }*/

            if (thrustersEnabled && thrustersVtol)
            {
                foreach (var joint in stators)
                {
                    //joint.Reset();
                    float totalMultiplier = 0;
                    for (int i = 0; i < joint.Types.Count; i++)
                    {
                        var type = joint.Types[i];
                        float multiplier = joint.Inverted[i];
                        //float multiplier = joint.InvertedMultiplier;
                        switch (type)
                        {
                            case BlockType.VtolStrafe:
                                multiplier *= vectorMovement.X;
                                break;
                            case BlockType.VtolVertical:
                                multiplier *= vectorMovement.Y;
                                break;
                            case BlockType.VtolForward:
                                multiplier *= vectorMovement.Z;
                                break;
                            case BlockType.VtolTurn:
                                multiplier *= vectorMovement.W;
                                break;
                            case BlockType.VtolAzimuth:
                                multiplier *= MathHelper.Clamp(rotationInput.Y / 10f, -1f, 1f);
                                break;
                            case BlockType.VtolElevation:
                                multiplier *= MathHelper.Clamp(rotationInput.X / 10f, -1f, 1f);
                                break;
                        }
                        totalMultiplier += multiplier;
                        //joint.TargetAngle += multiplier == 0f ? 0f : multiplier * Math.Abs(multiplier > 0f ? joint.Joint.MaximumRad : joint.Joint.MinimumRad);
                    }
                    //joint.TargetAngle += totalMultiplier == 0f ? 0f : MathHelper.Clamp(Math.Abs(totalMultiplier), 0, 1) * (totalMultiplier > 0f ? joint.Joint.MaximumRad : joint.Joint.MinimumRad);
                    /*joint.TargetAngle =
                        totalMultiplier == 0f ? (float)joint.Configuration.Offset :
                        MathHelper.Clamp(Math.Abs(totalMultiplier), 0, 1) *
                            (totalMultiplier > 0f ? joint.Joint.MaximumRad - (float)joint.Configuration.Offset : joint.Joint.MinimumRad - (float)joint.Configuration.Offset);*/
                    float offset = (float)joint.Configuration.Offset.ToRadians();
                    if (totalMultiplier > 0f && joint.Joint.Maximum < 360.5f)
                    {
                        joint.TargetAngle = MathHelper.Lerp(offset, joint.Joint.MaximumRad, totalMultiplier);
                    }
                    else if (totalMultiplier < 0f && joint.Joint.Minimum > -360.5f)
                    {
                        joint.TargetAngle = MathHelper.Lerp(offset, joint.Joint.MinimumRad, -totalMultiplier);
                    }
                    else //if (totalMultiplier == 0f)
                    {
                        joint.TargetAngle = offset;
                    }
                    joint.Apply();
                }
            }

            // if we can use Z, use that (as well as piloted controller); otherwise rely on commands
            /*if (thrustersOnMainGrid && controller != null)
            {
                thrusterBehavior = reference.DampenersOverride ? ThrusterMode.Hover : ThrusterMode.Override;
            }*/
            Log($"thrusters:", thrusters.Count);
            Log($"thruster mode:", thrusterBehavior);
            Log($"moveInput.Y:", moveInput.Y);

            /*foreach (IMyThrust thruster in thrusters)
            {
                thruster.ThrustOverridePercentage = moveInput.Y > 0 ? 1 : 0; //(moveInput.Y > 0 && thrusterBehavior == ThrusterMode.Override) ? 1 : 0;
                thruster.Enabled = thrustersEnabled && (thrusterBehavior == ThrusterMode.Hover ? (moveInput.Y >= 0) : moveInput.Y > 0); // thrustersEnabled && (thrusterBehavior == ThrusterMode.Hover || (moveInput.Y > 0));
            }*/

            if (!thrustersEnabled)
                return;

            var mass = reference.CalculateShipMass();
            var totalMass = mass.TotalMass;
            Vector3D gravityNormal = gravity.Normalized();

            var shipSpeed = reference.GetShipSpeed();
            var linearVelocity = reference.GetShipVelocities().LinearVelocity;

            //if (linearVelocity.LengthSquared() > 0)
            //    linearVelocity.Normalize();

            Vector3D forwardProjection = Vector3D.Reject(reference.WorldMatrix.Forward, gravityNormal);
            Vector3D right = Vector3D.Cross(forwardProjection, -gravityNormal);

            double forwardSpeed = Vector3D.Dot(linearVelocity, forwardProjection);//, reference.WorldMatrix.Forward); //forwardProjection);
            double rightSpeed = Vector3D.Dot(linearVelocity, right);//, reference.WorldMatrix.Right); //right);

            double gravityAcc = gravity.Length();
            double gravityG = gravityAcc / 9.81d;

            double verticalSpeed = Vector3D.Dot(linearVelocity, gravityNormal);
            double verticalSpeedNormal = verticalSpeed / Math.Max(gravityAcc, 0.1);
            Log($"forwardSpeed: {forwardSpeed}");
            Log($"rightSpeed: {rightSpeed}");
            Log($"verticalSpeed: {verticalSpeed}");
            Log($"verticalSpeedNormal: {verticalSpeedNormal}");

            bool isCounteringGravity = thrusterBehavior == ThrusterMode.Hover;
            Vector3D requiredGravityForce;
            if (isCounteringGravity)
            {
                double
                KP = 6d * gravityG,
                KD = 0.5d * gravityG,
                KI = 1d * gravityG,
                MAX_I = 10;

                double error = (vectorMovement.Y * (Math.Abs(verticalSpeedNormal) + 10)) - (-verticalSpeedNormal);
                pidIntegral += error * delta;
                pidIntegral = MathHelper.Clamp(pidIntegral, -MAX_I, MAX_I);

                double derivative = (error - pidLastError) / delta;
                pidLastError = error;

                double pid = error * KP + pidIntegral * KI + derivative * KD;

                Log($"PID:");
                Log($"     error: {error}");
                Log($"derivative: {derivative}");
                Log($"  integral: {pidIntegral}");
                Log($"PID:");

                //Vector3D requiredForce = -gravity * totalMass - gravityNormal * (pid * totalMass) - reference.WorldMatrix.Forward * forwardSpeed * 2 - reference.WorldMatrix.Right * rightSpeed * 2; //-gravity * totalMass - linearVelocity * totalMass * 2;
                requiredGravityForce = gravity * totalMass + gravityNormal * (pid * totalMass); //-gravity * totalMass - linearVelocity * totalMass * 2;
            }
            else
                requiredGravityForce = Vector3D.Zero;

            Vector3D requiredGravityForceNormal = requiredGravityForce.LengthSquared() > 1e-6 ? requiredGravityForce.Normalized() : Vector3D.Zero;
            Vector3D requiredMovementForce = 
                (vectorMovement.X == 0 && Math.Abs(forwardSpeed) > 0.01 ? /*reference.WorldMatrix.Forward*/forwardProjection * forwardSpeed * totalMass : Vector3D.Zero) + 
                (vectorMovement.Z == 0 && Math.Abs(rightSpeed) > 0.01 ? /*reference.WorldMatrix.Right*/right * rightSpeed * totalMass : Vector3D.Zero);
            Vector3D requiredMovementForceNormal = requiredMovementForce.Normalized();
            double requiredMovementForceMagnitude = requiredMovementForce.Length();
            double requiredLift = requiredGravityForce.Length();

            //buildTools.DrawVector(reference.WorldMatrix.Translation, reference.WorldMatrix.Translation + requiredGravityForce, Color.Tomato, 0.03f);
            //buildTools.DrawVector(reference.WorldMatrix.Translation, reference.WorldMatrix.Translation + requiredMovementForce, Color.Tomato, 0.03f);

            double maxLift = 0;
            double weightSum = 0;
            double maxHorizontal = 0;
            double horizontalWeightSum = 0;
            double w;
            foreach (var thruster in thrusters)
            {
                if (isCounteringGravity)
                {
                    w = Vector3D.Dot(thruster.WorldMatrix.Forward, requiredGravityForceNormal);
                    if (w > 0)
                    {
                        maxLift += w * thruster.MaxEffectiveThrust;
                        //buildTools.DrawVector(thruster.WorldMatrix.Translation, thruster.WorldMatrix.Translation + thruster.WorldMatrix.Forward * w, Color.Green, 0.03f);
                        weightSum += w;
                    }
                    w = Vector3D.Dot(thruster.WorldMatrix.Forward, requiredMovementForceNormal);
                    if (w > 0)
                    {
                        maxHorizontal += w * thruster.MaxEffectiveThrust;
                        //buildTools.DrawVector(thruster.WorldMatrix.Translation, thruster.WorldMatrix.Translation + thruster.WorldMatrix.Forward * w, Color.Green, 0.03f);
                        horizontalWeightSum += w;
                    }
                }
            }

            //if (maxLift <= 0)
            //    return;
            if (requiredLift > maxLift)
            {
                Log("NOT ENOUGH LIFT");
                //return;
            }
            
            /*if (isCounteringGravity)
                foreach (var thruster in thrusters)
                {
                    w = Vector3D.Dot(thruster.WorldMatrix.Forward, requiredGravityForceNormal);
                    if (w <= 0)
                    {
                        if (thruster.Enabled)
                            thruster.Enabled = false;
                        continue;
                    }

                    double thrustNewtons = (w / weightSum) * requiredLift;
                    double percent = thrustNewtons / thruster.MaxEffectiveThrust;
                    thruster.ThrustOverridePercentage = (float)MathHelperD.Clamp(percent, 0, 1);
                }*/

            var targetVelocity = -(reference.WorldMatrix.Forward * vectorMovement.Z + reference.WorldMatrix.Right * vectorMovement.X + (!isCounteringGravity ? vectorMovement.Y * reference.WorldMatrix.Up : Vector3D.Zero));// + reference.WorldMatrix.Up * vectorMovement.Y);
            var targetDirection = targetVelocity.Normalized();
            var targetSpeed = MathHelperD.Min(targetVelocity.Length(), 1);

            foreach (var thruster in thrusters)
            {
                /*if (!isCounteringGravity)
                {
                    thruster.Enabled = moveInput.Y > 1e-6f;
                    thruster.ThrustOverridePercentage = MathHelper.Clamp(moveInput.Y, 1e-6f, 1f); // reset for override.. god this is terrible
                }*/

                float thrustOverride = 0;

                bool enabled = false;
                w = Vector3D.Dot(thruster.WorldMatrix.Forward, targetDirection);
                if (w > 0)
                {
                    //buildTools.DrawVector(thruster.WorldMatrix.Translation, thruster.WorldMatrix.Translation + thruster.WorldMatrix.Forward * w, Color.Blue, 0.01f);
                    //buildTools.DrawVector(thruster.WorldMatrix.Translation, thruster.WorldMatrix.Translation + thruster.WorldMatrix.Forward * w * targetSpeed, Color.Teal, 0.02f);
                    enabled = true;
                    thrustOverride = (float)MathHelperD.Clamp(MathHelperD.Max(thrustOverride, w * targetSpeed), 0, 1);
                }
                if (isCounteringGravity)
                {
                    w = Vector3D.Dot(thruster.WorldMatrix.Forward, requiredGravityForceNormal);
                    if (w > 0)
                    {
                        enabled = true;
                        double thrustNewtons = (w / weightSum) * requiredLift;
                        double percent = thrustNewtons / thruster.MaxEffectiveThrust;
                        thrustOverride = (float)MathHelperD.Clamp(MathHelperD.Max(thrustOverride, percent), 0, 1);
                    }
                    w = Vector3D.Dot(thruster.WorldMatrix.Forward, requiredMovementForceNormal);
                    if (w > 0)
                    {
                        //buildTools.DrawVector(thruster.WorldMatrix.Translation, thruster.WorldMatrix.Translation + thruster.WorldMatrix.Forward * w, Color.Blue, 0.01f);
                        //buildTools.DrawVector(thruster.WorldMatrix.Translation, thruster.WorldMatrix.Translation + thruster.WorldMatrix.Forward * w * targetSpeed, Color.Teal, 0.02f);
                        double thrustNewtons = (w / horizontalWeightSum) * requiredMovementForceMagnitude;
                        double percent = thrustNewtons / thruster.MaxEffectiveThrust;
                        enabled = true;
                        thrustOverride = (float)MathHelperD.Clamp(MathHelperD.Max(thrustOverride, percent), 0, 1);
                    }
                    /*bool gravityBigger = requiredGravityForceNormal.Length() > requiredMovementForceNormal.Length();
                    if (gravityBigger)
                    {
                    }
                    else
                    {
                    }*/
                }
                if (thruster.Enabled != enabled)
                    thruster.Enabled = enabled;
                if (thruster.ThrustOverridePercentage != thrustOverride)
                    thruster.ThrustOverridePercentage = thrustOverride;
            }

            /*var upThrusters = new List<IMyThrust>();
            double requiredForce = totalMass * gravity.Length();
            double totalForce = 0;
            foreach (var thruster in thrusters)
            {
                var weight = Vector3D.Dot(thruster.WorldMatrix.Forward, gravityNormal);
                if (weight > (1 / Math.Sqrt(2)) && weight < 1)
                {
                    upThrusters.Add(thruster);
                    totalForce += weight * thruster.MaxEffectiveThrust;

                }
                else
                    thruster.ThrustOverride = 1e-6f;
            }

            requiredForce = Math.Max(requiredForce, 0);

            var proportion = requiredForce / totalForce;
            foreach (var block in upThrusters)
            {
                block.ThrustOverridePercentage = (float)(1f * proportion);
            }*/


            /*var requiredForce = -gravity * totalMass;
            var requiredForceMagnitude = requiredForce.Length();

            Vector3D gravityCounterDir = Vector3D.Normalize(requiredForce);

            double totalWeight = 0;
            double[] weights = new double[thrusters.Count];
            for (int i = 0; i < thrusters.Count; i++)
            {
                double w = Vector3D.Dot(thrusters[i].WorldMatrix.Backward, gravityCounterDir);
                if (w > 0)
                {
                    totalWeight += w;
                    weights[i] = w;
                }
                buildTools.DrawVector(thrusters[i].WorldMatrix.Translation, thrusters[i].WorldMatrix.Translation + thrusters[i].WorldMatrix.Backward, Color.Red, 0.02f);
            }

            if (totalWeight <= 0)
                return;

            float[] thrusterThrusts = new float[thrusters.Count];
            for (int i = 0; i < thrusters.Count; i++)
            {
                if (weights[i] > 0)
                {
                    double thrust = weights[i] / totalWeight * requiredForceMagnitude;
                    thrusterThrusts[i] = (float)thrust; //thrusters[i].ThrustOverride = (float)thrust;
                }
                else
                    thrusters[i].ThrustOverride = 1e-6f;
            }

            Redistribute(requiredForce, thrusterThrusts);*/
        }

        /*void Redistribute(Vector3D requiredForce, float[] thrusts)
        {
            Vector3D remainingForce = requiredForce;
            bool changed;

            bool[] active = new bool[thrusters.Count];
            for (int i = 0; i < active.Length; i++)
                active[i] = true;

            for (int interation = 0; interation < 10; interation++)
            {
                changed = false;

                for (int i = 0; i < thrusters.Count; i++)
                {
                    if (!active[i])
                        continue;

                    if (thrusts[i] > thrusters[i].MaxThrust)
                    {
                        thrusts[i] = thrusters[i].MaxThrust;
                        remainingForce -= thrusters[i].WorldMatrix.Backward * thrusters[i].MaxThrust;
                        active[i] = false;
                        changed = true;
                    }
                }

                if (!changed)
                    break;

                double totalWeight = 0;
                double[] weights = new double[thrusters.Count];

                Vector3D dir = remainingForce.LengthSquared() > 0 ? Vector3D.Normalize(remainingForce) : Vector3D.Zero;

                for (int i = 0; i < thrusters.Count; i++)
                {
                    if (!active[i])
                        continue;

                    double w = Vector3D.Dot(thrusters[i].WorldMatrix.Backward, dir);
                    if (w > 0)
                    {
                        totalWeight += w;
                        weights[i] = w;
                    }
                    //buildTools.DrawVector(thrusters[i].WorldMatrix.Translation, thrusters[i].WorldMatrix.Translation + thrusters[i].WorldMatrix.Forward, Color.Red, 0.02f);
                }

                if (totalWeight <= 0)
                    break;

                for (int i = 0; i < thrusters.Count; i++)
                {
                    if (!active[i])
                        continue;

                    double thrust = weights[i] / totalWeight * remainingForce.Length();
                    thrusts[i] = (float)thrust;
                }
            }

            for (int i = 0; i < thrusters.Count; i++)
                thrusters[i].ThrustOverride = thrusts[i];
        }*/
    }
}
