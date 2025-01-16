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
        static List<IMyShipController> cockpits = new List<IMyShipController>();

        Vector3 moveInput = Vector3.Zero;     // 
        Vector3 parsedMoveInput = Vector3.Zero;
        Vector2 rotationInput = Vector2.Zero; // MOUSE (gyro) => X is -pitch, Y is yaw
        float rollInput = 0f;                 // Q+E   (roll) => left is -, right is +

        static Vector3 movementOverride = Vector3.Zero;
        static float turnOverride = 0;

        float turnValue = 0f;
        float strafeValue = 0f;

        IMyShipController controller;
        IMyShipController anyController;

        void FetchInputs()
        {
            GridTerminalSystem.GetBlocksOfType(cockpits, c =>
                c.IsSameConstructAs(Me)
                &&
                (c is IMyRemoteControl
                ?
                (RemoteControlName.Equals("auto") ? c.GetProperty("MainRemoteControl").AsBool().GetValue(c) : c.CustomName.Equals(RemoteControlName))
                :
                (CockpitName.Equals("auto") ? c.IsMainCockpit : c.CustomName.Equals(CockpitName)))
            );
        }

        void UpdateInputs()
        {
            Log("-- Inputs --");
            // find ship controllers
            controller = cockpits.Find((pit) => pit.IsUnderControl);
            anyController = controller ?? (cockpits.Count > 0 ? cockpits[0] : null);

            // values
            moveInput = !Vector3.IsZero(movementOverride) ? movementOverride : Vector3.Clamp(controller?.MoveIndicator ?? Vector3.Zero, Vector3.MinusOne, Vector3.One);
            rotationInput = controller?.RotationIndicator ?? Vector2.Zero;
            rollInput = controller?.RollIndicator ?? 0f;

            Log($"moveInput: {moveInput}");
            Log($"rotationInput: {rotationInput}");
            Log($"rollInput: {rollInput}");

            // parse
            turnValue = turnOverride != 0 ? turnOverride : (ReverseTurnControls ? moveInput.X : rollInput);
            strafeValue = (ReverseTurnControls ? rollInput : moveInput.X);

            parsedMoveInput = new Vector3(strafeValue, turnValue, -moveInput.Z);

            Log($"turnValue: {turnValue}");
            Log($"strafeValue: {strafeValue}");
            Log($"parsedMoveInput: {parsedMoveInput}");
        }
    }
}
