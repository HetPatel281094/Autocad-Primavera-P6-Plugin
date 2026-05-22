using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Autocad_Primavera_P6_Plugin.Services.P6ApiService
{
    public class P6ApiService
    {
        private MyPlugin _pluginInstance = null;

        public P6ApiService(MyPlugin pluginInstance)
        {
            // Initialize P6 API connection and setup here
            _pluginInstance = pluginInstance;
        }
    }
}
