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

        MovementInfo moveInfo = new MovementInfo();
        Vector3 lastMovementDirection = Vector3.Zero;
        Vector3 movement = Vector3.Zero;

        static bool jumping = false;
        static double jumpTime = 0;
        static bool crouched = false;
        static bool crouchOverride = false;

        bool isTurning, isWalking;

        static bool limp = false;

        static double animationStepCounter = 0;

        static Animation activeAnimation, lastAnimation;

        float MaxComponentOf(Vector3 vector)
        {
            float maxComponent = vector.X;
            maxComponent = vector.Y.Absolute() > maxComponent.Absolute() ? vector.Y : maxComponent;
            maxComponent = vector.Z.Absolute() > maxComponent.Absolute() ? vector.Z : maxComponent;
            return maxComponent;
        }

        float AbsMax(float x, float y)
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

        public void UpdateLegs()
        {
            crouched = crouchOverride || moveInput.Y < 0;

            // delta calculations
            Vector3 moveDirection = (parsedMoveInput - movement);

            if (controller != null || AutoHalt)
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

            isWalking = (movement * new Vector3(1, 0, 1)).LengthSquared() > 0; // animationStepCounterDelta.Absolute() > 0;
            Log($"is turning: {isTurning}");
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
            Log($"animation: {activeAnimation}");

            moveInfo.Direction = lastMovementDirection;
            moveInfo.Movement = movement;
            moveInfo.Delta = delta;

            if (legsEnabled)
                foreach (var leg in legs.Values)
                {
                    leg.Animation = activeAnimation;
                    leg.Update(moveInfo);
                }
        }
    }
}
