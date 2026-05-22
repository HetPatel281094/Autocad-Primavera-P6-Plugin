using System.Windows;

namespace Autocad_Primavera_P6_Plugin.UserInterface.UI_P6ConnectionManagerView
{
    public partial class P6ConnectionManagerView : Window
    {
        public P6ConnectionManagerView()
        {
            InitializeComponent();
            DataContext = new P6ConnectionManagerViewModel();
        }
    }
}
