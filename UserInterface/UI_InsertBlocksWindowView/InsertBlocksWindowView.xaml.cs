using System.Windows;

namespace Autocad_Primavera_P6_Plugin.UserInterface.UI_InsertBlocksWindowView
{
    public partial class InsertBlocksWindowView : Window
    {
        public InsertBlocksWindowView()
        {
            InitializeComponent();
            DataContext = new InsertBlocksWindowViewModel();
        }
    }
}
