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
        public class ArmController
        {
            public int ArmCount => _arms.Count;
            public Dictionary<int, ArmGroup> Arms => _arms;
            private Dictionary<int, ArmGroup> _arms = new Dictionary<int, ArmGroup>();

            private Program _program;

            public bool Enabled => _enabled;
            private bool _enabled = true;

            private double _armPitch, _armYaw;

            internal ArmController(Program program)
            {
                _program = program;
            }

            public void Update(double delta)
            {
                Log("-- Arms --");
                //_enabled = true;
                _armPitch = _enabled ? -_program.rotationInput.X : 0;
                _armYaw = _enabled ? _program.rotationInput.Y : 0;

                if (_enabled)
                    foreach (var arm in _arms.Values)
                        if (arm.Enabled)
                            arm.Update(_armPitch, _armYaw);
            }
            public void ResetToZero()
            {
                foreach (var arm in _arms.Values)
                    arm.ToZero(); // TODO: select legs with 1 / 2 / all
            }

            public void ToggleEnabled(bool enabled)
            {
                if (_enabled && !enabled)
                    foreach (var group in _arms.Values)
                    {
                        foreach (var joint in group.PitchJoints.Concat(group.YawJoints))
                        {
                            joint.SetRPM(0);
                        }
                    }
                _enabled = enabled;
            }

            public void Fetch()
            {
                var configs = _arms.Select((kv) => new KeyValuePair<int, JointConfiguration>(kv.Key, kv.Value.Configuration)).ToDictionary(pair => pair.Key, pair => pair.Value);
                _program.blockFetcher.FetchGroups(ref _arms, configs, BlockFetcher.CreateArmFromType, ArmConfiguration.Parse);

                foreach (var arm in Arms.Values)
                    arm.ApplyConfiguration();
            }

        }

    }
}
