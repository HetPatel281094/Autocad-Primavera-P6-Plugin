using System.Collections.ObjectModel;

namespace Autocad_Primavera_P6_Plugin.UserInterface.UI_InsertBlocksWindowView
{
    class InsertBlocksWindowModel
    {
        public ObservableCollection<string> AvailableBlocks { get; set; }

        public InsertBlocksWindowModel()
        {
            AvailableBlocks = new ObservableCollection<string>();
        }
    }
}
