using System.Collections.ObjectModel;

namespace Autocad_Primavera_P6_Plugin.UserInterface.UI_PropertiesPalateView
{
    class PropertiesPalateModel
    {
        public ObservableCollection<string> Properties { get; set; }

        public PropertiesPalateModel()
        {
            Properties = new ObservableCollection<string>();
        }
    }
}
