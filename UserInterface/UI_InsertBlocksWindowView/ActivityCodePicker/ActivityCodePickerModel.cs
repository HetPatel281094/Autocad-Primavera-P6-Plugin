using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading.Tasks;
using System.Windows.Documents;
using Autocad_Primavera_P6_Plugin.Services.LiteDBService;
using Autocad_Primavera_P6_Plugin.Services.P6ApiService;

namespace Autocad_Primavera_P6_Plugin.UserInterface.UI_InsertBlocksWindowView.ActivityCodePicker
{
    /// <summary>
    /// Plain data container for everything the Activity Code picker needs
    /// up front. Holds only inputs - no UI state, no commands.
    /// </summary>
    public sealed class ActivityCodePickerModel
    {
        public MyPlugin PluginInstance { get; }

        private Client P6Client { get; set; }
        public bool IsAutoGenerate { get; }
        public ActivityCodeType SelectionActCodeType { get; set; }
        public List<ActivityCode> SelectionActCodes { get; set; }

        public Exception ModelException { get; set; }

        public ActivityCodePickerModel(MyPlugin pluginInstance, bool isAutoGenerate, ActivityCodeType selectionActCodeType)
        {
            PluginInstance = pluginInstance ?? throw new ArgumentNullException(nameof(pluginInstance));
            P6Client = PluginInstance.MyP6ApiService.Client;
            IsAutoGenerate = isAutoGenerate;
            SelectionActCodeType = selectionActCodeType;
        }

    }
}
