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

        public static Vector3D customTarget = Vector3D.Zero;

        static bool jumping = false;
        static double jumpTime = 0;
        static bool crouched = false;
        static bool crouchOverride = false;

        bool isTurning, isWalking;

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
            BlockFetcher.FetchGroups(ref legs, configs, BlockFetcher.IsForLeg, BlockFetcher.CreateLegFromType, LegConfiguration.Parse, BlockFetcher.AddToLeg);

            foreach (var leg in legs.Values)
                leg.Initialize();

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
            if (Math.Abs(direction) < .08f)
                return target;
            if (target == 0)
                return current + direction * DecelerationMultiplier * (float)TicksPerSecond;
            return current + direction * AccelerationMultiplier * (float)TicksPerSecond;
        }

        public void UpdateLegs()
        {
            Log("-- Legs --");
            crouched = crouchOverride || moveInput.Y < 0;

            // delta calculations
            Vector3 moveDirection = legsEnabled ? parsedMoveInput : Vector3.Zero;//(parsedMoveInput - movement);

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
            lastMoveInfo.Delta    = moveInfo.Delta;

            moveInfo.Walk         = movement.Z; // since -1 is forward, negative it so 1 is forward -- already inverted in parsedMoveInput
            moveInfo.Turn         = movement.Y;
            moveInfo.Strafe       = movement.X;
            moveInfo.Crouched     = parsedVerticalInput < 0;
            moveInfo.Jumping      = parsedVerticalInput > 0;
            moveInfo.Jumped       = (moveInfo.Jumped || parsedVerticalInput > 0) && !(parsedVerticalInput < 0);
            moveInfo.Delta        = 1 / 60f;
            Log($"move info: WALK:{moveInfo.Walk}; TURN:{moveInfo.Turn}; STRAFE:{moveInfo.Strafe}; CROUCHED:{moveInfo.Crouched}");

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

            foreach (var leg in legs.Values)
            {
                leg.Update(moveInfo);
            }

            /*if (controller != null || AutoHalt)
            {
                // TODO: fix multipliers to work, currently walking backwards uses decel/acc in reverse
                movement.X += moveDirection.X * (moveDirection.X > 0 ? AccelerationMultiplier : DecelerationMultiplier) * .3f * (float)delta;
                movement.Y += moveDirection.Y * (moveDirection.Y > 0 ? AccelerationMultiplier : DecelerationMultiplier) * 1f * (float)delta;
                movement.Z += moveDirection.Z * (moveDirection.Z > 0 ? AccelerationMultiplier : DecelerationMultiplier) * .3f * (float)delta;
            }

            jumping = jumpTime > 0;
            if (moveInput.Y > 0 && !thrustersEnabled)
            {
                jumping = false;
                jumpTime = .5d;
                crouched = true;
            }
            else if (jumpTime > 0)
            {
                jumpTime -= delta;
            }

            Log($"movement: {movement}");
            Log($"animation step counter (before): {animationStepCounter}");

            float maxComponent = MaxComponentOf(movement);
            Log($"animation step maxComponent: {maxComponent}");

            // detect when we should start trying to stop between 0 and .5
            bool isStopping = movement.Length() < 0.4 && (moveDirection.Length() < .4 || parsedMoveInput.Length() == 0) && movement.LengthSquared() > 0;
            Log($"is stopping?: {isStopping}");

            // calculate delta
            double animationStepCounterDelta =
                (!isStopping ?
                    (movement.LengthSquared() > 0 ? maxComponent : 0) :
                    AbsMax(MaxComponentOf(lastMovementDirection) * .3f, maxComponent)
                ) * WalkCycleSpeed * .01; // .01 is constant

            animationStepCounter += animationStepCounterDelta;

            Log($"animation step counter delta: {animationStepCounterDelta}");
            Log($"animation step counter (after): {animationStepCounter}");

            double animationStepModulo = animationStepCounter.Modulo(1);
            Log($"animation step (modulo): {animationStepModulo}");

            if (parsedMoveInput != Vector3.Zero)
                lastMovementDirection = parsedMoveInput;

            if (isStopping)
            {
                if ((animationStepModulo).Absolute() < .02 || (animationStepModulo - 1).Absolute() < .02 || (animationStepModulo - .5d).Absolute() < .02) // close to point
                {
                    if ((animationStepModulo).Absolute() < .25 || (animationStepModulo - 1).Absolute() < .25) // close to 0/1
                    {
                        animationStepCounter = 0;
                    }
                    else if ((animationStepModulo - .5d).Absolute() < .25) // cose to .5
                    {
                        animationStepCounter = .5;
                    }
                    movement *= 0;
                    animationStepCounterDelta = 0;
                }
            }

            turnValue = lastMovementDirection.Y;
            isTurning = turnValue != 0 && animationStepCounterDelta != 0;

            isWalking = (movement * new Vector3(0, 0, 1)).LengthSquared() > 0; // animationStepCounterDelta.Absolute() > 0;
            Log($"is turning: {isTurning}");
            Log($"is crouching: {crouched}");
            Log($"is walking: {isWalking}");
            Log($"is flying : {isInFlight}");
            Log($"is jumping: {jumping}");
            Animation chosenAnimation;
            if (isInFlight)
                chosenAnimation = Animation.Flight;
            else if (isWalking && (!isTurning || !SteeringTakesPriority))
                chosenAnimation = crouched ? Animation.CrouchWalk : Animation.Walk;
            else if (isTurning)
                chosenAnimation = crouched ? Animation.CrouchTurn : Animation.Turn;
            else
                chosenAnimation = crouched ? Animation.Crouch : Animation.Idle;

            lastAnimation = activeAnimation;
            activeAnimation = chosenAnimation;/*turning ? (crouched ? Animation.CrouchTurn : Animation.Turn) :
                animationStepCounterDelta.Absolute() > 0 ? (crouched ? Animation.CrouchWalk : Animation.Walk) :
                (crouched ? Animation.Crouch : Animation.Idle);*/
            /*Log($"animation: {activeAnimation}");

            // motion
            moveInfo.Walking = (isWalking && (!isTurning || !SteeringTakesPriority));
            moveInfo.Turning = !moveInfo.Walking && isTurning;
            moveInfo.Strafing = (movement.X != 0);
            moveInfo.Idle = !moveInfo.Walking && !moveInfo.Turning && !moveInfo.Strafing;

            // states
            moveInfo.Flying = isInFlight;
            moveInfo.Crouched = crouched;

            // values
            moveInfo.Direction = lastMovementDirection;
            moveInfo.Movement = movement;
            moveInfo.Delta = delta;
            Log($"moveInfo: Direction={moveInfo.Direction}, Movement={moveInfo.Movement}, Delta={moveInfo.Delta}");
            Log($"moveInfo: Walking={moveInfo.Walking}, Turning={moveInfo.Turning}, Strafing={moveInfo.Strafing}");
            Log($"moveInfo: Idle={moveInfo.Idle}; Flying={moveInfo.Flying}, Crouched={moveInfo.Crouched}");

            if (legsEnabled)
                foreach (var leg in legs.Values)
                {
                    leg.Animation = activeAnimation;
                    leg.Update(moveInfo);
                }

            if (inputVisual && debugPanel != null)
            {
                debugPanel.ContentType = ContentType.SCRIPT;
                var frame = debugPanel.DrawFrame();



                frame.Dispose();
            }*/
        }
    }
}
