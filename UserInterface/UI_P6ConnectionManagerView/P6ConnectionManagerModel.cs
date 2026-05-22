using System.Collections.ObjectModel;

namespace Autocad_Primavera_P6_Plugin.UserInterface.UI_P6ConnectionManagerView
{
    class P6ConnectionManagerModel
    {
        public ObservableCollection<string> P6Connections { get; set; }

        public P6ConnectionManagerModel()
        {
            P6Connections = new ObservableCollection<string>();
        }
    }
}
