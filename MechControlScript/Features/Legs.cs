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

        static MovementInfo moveInfo = new MovementInfo();
        static MovementInfo lastMoveInfo = new MovementInfo();
        Vector3 lastMovementDirection = Vector3.Zero;
        Vector3 movement = Vector3.Zero;

        public static Vector3 flyingOffset = Vector3.Zero;

        public static Vector3D customTarget = Vector3D.Zero;

        //static bool jumping = false;
        //static double jumpTime = 0;
        static bool crouched = false;
        bool crouchOverride = false;

        //bool isTurning, isWalking;

        static double animationStepCounter = 0;

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
            blockFetcher.FetchGroups(ref legs, configs, BlockFetcher.IsForLeg, BlockFetcher.CreateLegFromType, LegConfiguration.Parse, BlockFetcher.AddToLeg);

            foreach (var leg in legs.Values)
            {
                leg.Initialize();
                /*if (!configs.ContainsKey(leg.Configuration.Id))
                    continue;
                var last = (LegConfiguration)configs[leg.Configuration.Id];
                if (leg.Configuration.LegType != last.LegType) // should set defaults?
                {
                    if (leg.DefaultConfiguration == null)
                    {
                        StaticWarn("leg has no default config", ":(");
                        continue;
                    }
                    leg.DefaultConfiguration.LegType = leg.Configuration.LegType;
                    leg.Configuration = leg.DefaultConfiguration;
                }*/
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

        float Translate(float current, float target, float accel, float decel) 
        {
            float direction = target - current;
            if (Math.Abs(direction) < .04f)
                return target;
            if (target == 0)
                return current + direction * DecelerationMultiplier * (float)TicksPerSecond;
            return current + direction * AccelerationMultiplier * (float)TicksPerSecond;
        }

        public void UpdateLegs()
        {
            Log("-- Legs --");
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
                movement.X = Translate(movement.X, moveDirection.X, AccelerationMultiplier, DecelerationMultiplier);
                movement.Y = Translate(movement.Y, moveDirection.Y, AccelerationMultiplier, DecelerationMultiplier);
                movement.Z = Translate(movement.Z, moveDirection.Z, AccelerationMultiplier, DecelerationMultiplier);
                /*movement.Z = MathHelper.Clamp(
                    movement.Z + GetDirectionMultiplier(moveDirection.Z, movement.Z, AccelerationMultiplier, DecelerationMultiplier) * .5f * (float)delta, -1f, 1f);*/
            }
            Log($"movement: {movement}");

            lastMoveInfo.Walk     = moveInfo.Walk;
            lastMoveInfo.Turn     = moveInfo.Turn;
            lastMoveInfo.Strafe   = moveInfo.Strafe;
            lastMoveInfo.Crouched = moveInfo.Crouched;
            lastMoveInfo.Jumping  = moveInfo.Jumping;
            lastMoveInfo.Jumped   = moveInfo.Jumped;
            lastMoveInfo.Flying   = moveInfo.Flying;
            lastMoveInfo.Delta    = moveInfo.Delta;

            float flyingMultiplier = thrustersEnabled ? 0f : 1f;
            moveInfo.Walk         = flyingMultiplier * movement.Z; // since -1 is forward, negate it so 1 is forward -- already inverted in parsedMoveInput
            moveInfo.Turn         = flyingMultiplier * movement.Y;
            moveInfo.Strafe       = flyingMultiplier * movement.X;
            //flyingOffset = new Vector3(movement.Z, movement.Y, movement.X);
            moveInfo.Crouched     = /*parsedVerticalInput < 0*/crouched && (!thrustersEnabled);
            moveInfo.Jumping      = parsedVerticalInput > 0 && (!thrustersEnabled);
            moveInfo.Jumped       = (moveInfo.Jumped || parsedVerticalInput > 0) && !(parsedVerticalInput < 0); // if jumping or jumped, keep state--if crouched, reset state
            moveInfo.Flying       = thrustersEnabled; // parsedVerticalInput > 0 && !moveInfo.Jumping;
            moveInfo.Delta        = 1 / 60f;
            Log($"move info: WALK:{moveInfo.Walk}; TURN:{moveInfo.Turn}; STRAFE:{moveInfo.Strafe}; CROUCHED:{moveInfo.Crouched}");
            Log($"move cont: JUMP:{moveInfo.Jumping},{moveInfo.Jumped}; FLY:{moveInfo.Flying}");

            /// X: Strafe
            /// Y: Turn
            /// Z: Forward
            // updating deltas
            /*float maxComponent = MaxComponentOf(movement);

            animationStepCounter += maxComponent * delta;*/
            if (movement.LengthSquared() != 0)
                animationStepCounter = (animationStepCounter + moveInfo.Delta * WalkCycleSpeed * .5f);
            else
            {
                if (animationStepCounter > 1)
                    animationStepCounter -= (animationStepCounter - 1); // return to terms of 0 to 1
                if (animationStepCounter > .25 && animationStepCounter < .75)
                    animationStepCounter = .5;
                else
                    animationStepCounter = 0;
            }
            Log($"animationStepCounter: {animationStepCounter}");

            if (legsEnabled)
                foreach (var leg in legs.Values)
                {
                    leg.Update(moveInfo);
                }
        }
    }
}
