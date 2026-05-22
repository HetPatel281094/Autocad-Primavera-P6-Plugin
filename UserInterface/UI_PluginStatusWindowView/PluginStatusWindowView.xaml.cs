using System.Windows;

namespace Autocad_Primavera_P6_Plugin.UserInterface.UI_PluginStatusWindowView
{
    public partial class PluginStatusWindowView : Window
    {
        public PluginStatusWindowView()
        {
            InitializeComponent();
            DataContext = new PluginStatusWindowViewModel();
        }
    }
}
