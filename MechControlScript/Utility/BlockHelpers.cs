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
        internal static string ToInitial(BlockType type)
        {
            switch (type)
            {
                case BlockType.Hip:
                    return "H";
                case BlockType.Knee:
                    return "K";
                case BlockType.Foot:
                    return "F";
                case BlockType.Quad:
                    return "Q";
            }
            throw new Exception("Invalid block type");
        }

        internal static string ToName(BlockType type)
        {
            switch (type)
            {
                case BlockType.Hip:
                    return "Hip";
                case BlockType.Knee:
                    return "Knee";
                case BlockType.Foot:
                    return "Foot";
                case BlockType.Quad:
                    return "Quad";
            }
            throw new Exception("Invalid block type");
        }

        internal static string ToInitial(BlockSide side)
        {
            switch (side)
            {
                case BlockSide.Left:
                    return "L";
                case BlockSide.Right:
                    return "R";
            }
            throw new Exception("Invalid block side");
        }

        internal static string ToName(BlockSide side)
        {
            switch (side)
            {
                case BlockSide.Left:
                    return "Left";
                case BlockSide.Right:
                    return "Right";
            }
            throw new Exception("Invalid block side");
        }
    }
}
