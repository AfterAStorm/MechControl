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
using VRageRender;

namespace IngameScript
{
    partial class Program
    {
        public static Dictionary<int, LegGroup> legs = new Dictionary<int, LegGroup>();

        bool legsEnabled = true;
        bool useLegDefaults = true;
        //bool legsThrustersDisabled = false; // toggled debounce whenever thrusters are enabled

        static MovementInfo moveInfo = new MovementInfo();
        static MovementInfo lastMoveInfo = new MovementInfo();
        Vector3 lastMovementDirection = Vector3.Zero;
        Vector3 movement = Vector3.Zero;

        public static Vector3 flyingOffset = Vector3.Zero;

        public static Vector3D customTarget = Vector3D.Zero;
        public static double customAnimationStep = -1;

        //static bool jumping = false;
        //static double jumpTime = 0;
        static bool crouched = false;
        bool crouchOverride = false;
        public static bool syncStep = false;

        //bool isTurning, isWalking;

        static double animationStepCounter = 0;

        void ToggleLegsEnabled(bool enabled)
        {
            if (legsEnabled && !enabled)
                foreach (var group in legs.Values)
                {
                    var trigroup = group as TriLegGroup;
                    if (trigroup != null)
                    {
                        foreach (var joint in trigroup.AllJoints)
                        {
                            joint.SetRPM(0);
                        }
                    }
                }
            legsEnabled = enabled;
        }

        float MaxComponentOf(Vector3 vector)
        {
            float maxComponent = vector.X;
            maxComponent = vector.Y.Absolute() > maxComponent.Absolute() ? vector.Y : maxComponent;
            maxComponent = vector.Z.Absolute() > maxComponent.Absolute() ? vector.Z : maxComponent;
            return maxComponent;
        }

        public static float AbsMax(float x, float y)
        {
            if (Math.Abs(x) > Math.Abs(y))
                return x;
            return y;
        }

        public void FetchLegs()
        {
            var configs = legs.Select((kv) => new KeyValuePair<int, JointConfiguration>(kv.Key, kv.Value.Configuration)).ToDictionary(pair => pair.Key, pair => pair.Value);
            blockFetcher.FetchGroups(ref legs, configs, BlockFetcher.CreateLegFromType, LegConfiguration.Parse);

            foreach (var leg in legs.Values)
            {
                leg.Initialize();
                if (!useLegDefaults || !configs.ContainsKey(leg.Configuration.Id))
                {
                    leg.ApplyConfiguration();
                    continue;
                }
                var last = (LegConfiguration)configs[leg.Configuration.Id];
                if (leg.Configuration.LegType != last.LegType) // should set defaults?
                {
                    if (leg.DefaultConfiguration == null)
                    {
                        StaticWarn("leg has no default config", $"going from {last.LegType} to {leg.Configuration.LegType}");
                        continue;
                    }
                    leg.DefaultConfiguration.LegType = leg.Configuration.LegType;
                    leg.Configuration = leg.DefaultConfiguration;
                    leg.Initialize();
                    leg.ApplyConfiguration();
                }
                else
                    leg.ApplyConfiguration();
            }

            // fix jump after reload
            if (crouchOverride || crouched)
                foreach (LegGroup leg in legs.Values)
                    leg.CrouchWaitTime = 1;
        }

        /// <summary>
        /// Determine a acceleration/deceleration multiplier based on a basis and direction
        /// </summary>
        float GetDirectionMultiplier(float direction, float from, float accel, float decel)
        {
            if (direction == 0)
                return 0;
            // if slowing down, return decel rate
            if (from < 0 && direction > 0 || from > 0 && direction < 0)
                return decel;
            return accel; // otherwise return accel rate
        }

        float ClampAnyway(float x, float a, float b)
        {
            return MathHelper.Clamp(x, Math.Min(a, b), Math.Max(a, b));
        }

        float Translate(float current, float target, float accel, float decel, float delta) 
        {
            float direction = target - current;
            if (Math.Abs(direction) < .04f)
                return target;
            if (target == 0)
                return ClampAnyway(current + Math.Sign(direction) * DecelerationMultiplier * delta, current, target);
            return ClampAnyway(current + Math.Sign(direction) * AccelerationMultiplier * delta, current, target);
        }

        static bool magnetJumping = false;
        static double magnetJumpProgress = 0d;
        double magnetJumpTime = 0;

        public void UpdateLegs()
        {
            Log("-- Legs --");
            // must be done BEFORE for timerblocks
            lastMoveInfo.Walk = moveInfo.Walk;
            lastMoveInfo.Turn = moveInfo.Turn;
            lastMoveInfo.Strafe = moveInfo.Strafe;
            lastMoveInfo.Crouched = moveInfo.Crouched;
            lastMoveInfo.Jumping = moveInfo.Jumping;
            lastMoveInfo.Jumped = moveInfo.Jumped;
            lastMoveInfo.Flying = moveInfo.Flying;
            lastMoveInfo.Delta = moveInfo.Delta;
            lastMoveInfo.Stopping = moveInfo.Stopping;

            crouched = crouchOverride || parsedVerticalInput < 0;

            // delta calculations
            Vector3 moveDirection = legsEnabled && !thrustersEnabled ? parsedMoveInput : Vector3.Zero;//(parsedMoveInput - movement);

            // if key is released, go to 0 by default
            //moveDirection.X = moveDirection.X == 0 ? -movement.X : moveDirection.X;
            //moveDirection.Y = moveDirection.Y == 0 ? -movement.Y : moveDirection.Y;
            //moveDirection.Z = moveDirection.Z == 0 ? -movement.Z : moveDirection.Z;

            // move "movement"--current movement vector--if controller in use/auto halt
            if (moveDirection != Vector3.Zero || AutoHalt)
            {
                /*movement.X = MathHelper.Clamp(
                    movement.X + GetDirectionMultiplier(moveDirection.X, movement.X, AccelerationMultiplier, DecelerationMultiplier) * .5f * (float)delta, -1f, 1f);
                movement.Y = MathHelper.Clamp(
                    movement.Y + GetDirectionMultiplier(moveDirection.Y, movement.Y, AccelerationMultiplier, DecelerationMultiplier) * .5f * (float)delta, -1f, 1f);*/
                movement.X = Translate(movement.X, moveDirection.X, AccelerationMultiplier, DecelerationMultiplier, (float)delta);
                movement.Y = Translate(movement.Y, moveDirection.Y, AccelerationMultiplier, DecelerationMultiplier, (float)delta);
                movement.Z = Translate(movement.Z, moveDirection.Z, AccelerationMultiplier, DecelerationMultiplier, (float)delta);
                /*movement.Z = MathHelper.Clamp(
                    movement.Z + GetDirectionMultiplier(moveDirection.Z, movement.Z, AccelerationMultiplier, DecelerationMultiplier) * .5f * (float)delta, -1f, 1f);*/
            }
            Log($"movement:", movement);

            float flyingMultiplier = thrustersEnabled ? 0f : 1f;
            moveInfo.Walk         = flyingMultiplier * movement.Z; // since -1 is forward, negate it so 1 is forward -- already inverted in parsedMoveInput
            moveInfo.Turn         = flyingMultiplier * movement.Y;
            moveInfo.Strafe       = flyingMultiplier * movement.X;
            //flyingOffset = new Vector3(movement.Z, movement.Y, movement.X);
            moveInfo.Crouched     = /*parsedVerticalInput < 0*/crouched && (!thrustersEnabled);
            moveInfo.Jumping      = parsedVerticalInput > 0 && (!thrustersEnabled);
            moveInfo.Jumped       = (moveInfo.Jumped || parsedVerticalInput > 0) && !(parsedVerticalInput < 0); // if jumping or jumped, keep state--if crouched, reset state
            moveInfo.Flying       = thrustersEnabled; // parsedVerticalInput > 0 && !moveInfo.Jumping;
            moveInfo.Delta        = delta;
            Log($"move info : WALK:{moveInfo.Walk}; TURN:{moveInfo.Turn}; STRAFE:{moveInfo.Strafe}; CROUCHED:{moveInfo.Crouched}");
            Log($"move cont : JUMP:{moveInfo.Jumping},{moveInfo.Jumped}; FLY:{moveInfo.Flying}");
            Log($"move delta:", moveInfo.Delta);

            if (magnetJumping)
            {
                magnetJumpTime += moveInfo.Delta;
                magnetJumpProgress = magnetJumpTime / 0.5d;
                if (magnetJumpTime > 0.5d)
                {
                    magnetJumping = false;
                    ToggleMagnetsEnabled(true);
                }
            }
            if (!moveInfo.Jumping && lastMoveInfo.Jumping && magnetsEnabled)
            {
                ToggleMagnetsEnabled(false);
                magnetJumping = true;
                magnetJumpTime = 0;
            }

            if (customAnimationStep != -1d)
            {
                moveInfo.Walk = 1f;
            }

            /// X: Strafe
            /// Y: Turn
            /// Z: Forward
            // updating deltas
            /*float maxComponent = MaxComponentOf(movement);

            animationStepCounter += maxComponent * delta;*/
            moveInfo.Stopping = false;
            if (movement.LengthSquared() != 0)
                animationStepCounter = (animationStepCounter + moveInfo.Delta * WalkCycleSpeed * .5f);
            else
            {
                if (anyController != null)
                    Singleton.buildTools.DrawPoint(anyController.GetPosition() + Vector3.One, Color.Blue, 0.25f);
                if (animationStepCounter > 1)
                    animationStepCounter -= (animationStepCounter - 1); // return to terms of 0 to 1
                if (animationStepCounter > .25 && animationStepCounter < .75)
                    animationStepCounter = MathHelper.Lerp(animationStepCounter, 0.5d, 0.5d);
                else if (animationStepCounter >= 0.75)
                    animationStepCounter = MathHelper.Lerp(animationStepCounter, 1d, 0.5d);
                else
                    animationStepCounter = MathHelper.Lerp(animationStepCounter, 0d, 0.5d);
                if (!(Math.Abs(animationStepCounter) < 0.02d || Math.Abs(animationStepCounter - 1d) < 0.02d || Math.Abs(animationStepCounter - 0.5d) < 0.02d))
                    moveInfo.Stopping = true; // prevent magnets from being angy
            }
            Log($"animationStepCounter: {animationStepCounter}");
            Log($"stopping?: {moveInfo.Stopping}");
            // TODO: REMOVE
            if (moveInfo.Stopping && anyController != null)
                Singleton.buildTools.DrawPoint(anyController.GetPosition(), Color.Red, 0.3f);

            // crazy logic for neat boolean tricks!
            if (legsEnabled)// && !legsThrustersDisabled)
                foreach (var leg in legs.Values)
                {
                    if (!leg.Enabled)
                        continue;
                    //if (thrustersEnabled)
                    //    leg.ToggleEnabled(false, false);
                    leg.Update(moveInfo);
                }
            //legsThrustersDisabled = thrustersEnabled;
        }
    }
}
