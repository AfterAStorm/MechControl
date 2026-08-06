using Sandbox.ModAPI.Ingame;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using VRage.Game.ModAPI.Ingame.Utilities;

namespace IngameScript
{
    public class ConfigManager
    {

        private Dictionary<IMyTerminalBlock, MyIni> configs = new Dictionary<IMyTerminalBlock, MyIni>();

        public ConfigManager()
        {

        }

        public void Clear()
        {
            configs.Clear();
        }

        public void Save()
        {

        }

        public void Reload()
        {

        }

        public MyIni GetConfiguration(IMyTerminalBlock block)
        {
            if (configs.ContainsKey(block))
                return configs[block];
            MyIni ini = new MyIni();
            ini.TryParse(block.CustomData);
            configs[block] = ini;
            return ini;
        }

    }
}
