using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Autocad_Primavera_P6_Plugin.Services
{
    public class LiteDBService
    {
        private MyPlugin _pluginInstance = null;

        public LiteDBService(MyPlugin pluginInstance)
        {
            // Initialize LiteDB connection and setup here
            _pluginInstance = pluginInstance;
        }
    }
}
