using System.Windows;

namespace Autocad_Primavera_P6_Plugin.UserInterface.UI_InsertBlocksWindowView
{
    public partial class InsertBlocksWindowView : Window
    {
        public InsertBlocksWindowView(InsertBlocksWindowViewModel viewModel)
        {
            InitializeComponent();

            viewModel.RequestClose += result =>
            {
                Close();
            };

            DataContext = viewModel;
        }
    }
}
