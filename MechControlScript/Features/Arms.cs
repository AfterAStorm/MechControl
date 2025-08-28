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
        public static Dictionary<int, ArmGroup> arms = new Dictionary<int, ArmGroup>();

        static bool armsEnabled = true;
        static double armPitch = 0;
        static double armYaw = 0;

        public void FetchArms()
        {
            var configs = arms.Select((kv) => new KeyValuePair<int, JointConfiguration>(kv.Key, kv.Value.Configuration)).ToDictionary(pair => pair.Key, pair => pair.Value);
            blockFetcher.FetchGroups(ref arms, configs, BlockFetcher.IsForArm, BlockFetcher.CreateArmFromType, ArmConfiguration.Parse, BlockFetcher.AddToArm);
        }

        public void UpdateArms()
        {
            Log("-- Arms --");
            armPitch = armsEnabled ? - rotationInput.X : 0;
            armYaw = armsEnabled ? rotationInput.Y : 0;

            if (armsEnabled)
                foreach (var arm in arms.Values)
                    arm.Update();
        }
    }
}
