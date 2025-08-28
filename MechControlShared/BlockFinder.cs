using Sandbox.ModAPI.Ingame;
using Sandbox.ModAPI.Interfaces;
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
        public class BlockFinder
        {

            public IMyGridTerminalSystem Terminal => terminal;
            private IMyGridTerminalSystem terminal;

            public BlockFinder(IMyGridTerminalSystem terminal)
            {
                this.terminal = terminal;
            }

            /// <summary>
            /// Returns a list of blocks instead of changing a list in parameters
            /// </summary>
            /// <typeparam name="T"></typeparam>
            /// <returns></returns>
            public List<T> GetBlocksOfType<T>(Func<T, bool> predicate = null) where T : class
            {
                List<T> blocks = new List<T>();
                terminal.GetBlocksOfType(blocks, block => (block as IMyTerminalBlock).IsSameConstructAs(Singleton.Me) && (predicate == null || predicate(block)));
                return blocks;
            }
        }
    }
}
