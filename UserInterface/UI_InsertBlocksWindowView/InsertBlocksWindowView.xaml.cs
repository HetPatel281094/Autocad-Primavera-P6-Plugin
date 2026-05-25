using System.Windows;
using System.Windows.Controls;

namespace Autocad_Primavera_P6_Plugin.UserInterface.UI_InsertBlocksWindowView
{
    public partial class InsertBlocksWindowView : Window
    {
        public InsertBlocksWindowView(MyPlugin pluginInstance)
        {
            InitializeComponent();
            DataContext = new InsertBlocksWindowViewModel(pluginInstance);
        }
    }
}
