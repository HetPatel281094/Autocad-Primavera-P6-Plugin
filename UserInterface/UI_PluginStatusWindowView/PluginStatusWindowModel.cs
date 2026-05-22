using System.Collections.ObjectModel;

namespace Autocad_Primavera_P6_Plugin.UserInterface.UI_PluginStatusWindowView
{
    class PluginStatusWindowModel
    {
        public ObservableCollection<string> PluginStatuses { get; set; }

        public PluginStatusWindowModel()
        {
            PluginStatuses = new ObservableCollection<string>();
        }
    }
}
