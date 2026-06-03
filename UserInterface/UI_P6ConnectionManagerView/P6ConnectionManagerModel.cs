using System.Collections.ObjectModel;

namespace Autocad_Primavera_P6_Plugin.UserInterface.UI_P6ConnectionManagerView
{
    class P6ConnectionManagerModel
    {
        // The flat list displayed in the ListBox
        public ObservableCollection<Services.LiteDBService.P6ConnectionConfig> P6Connections { get; set; }

        public P6ConnectionManagerModel()
        {
            P6Connections = new ObservableCollection<Services.LiteDBService.P6ConnectionConfig>();
        }
    }
}