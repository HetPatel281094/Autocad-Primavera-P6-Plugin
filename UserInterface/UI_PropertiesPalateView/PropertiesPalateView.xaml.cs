using System.Windows.Controls;

namespace Autocad_Primavera_P6_Plugin.UserInterface.UI_PropertiesPalateView
{
    public partial class PropertiesPalateView : UserControl
    {
        public PropertiesPalateView()
        {
            InitializeComponent();
            DataContext = new PropertiesPalateViewModel();
        }
    }
}
