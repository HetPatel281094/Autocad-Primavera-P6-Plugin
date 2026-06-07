using System.Windows;

namespace Autocad_Primavera_P6_Plugin.UserInterface.UI_InsertBlocksWindowView
{
    public partial class InsertBlocksWindowView : Window
    {
        public InsertBlocksWindowView(MyPlugin pluginInstance)
        {
            InitializeComponent();

            var viewModel = new InsertBlocksWindowViewModel(pluginInstance);
            viewModel.RequestClose += result =>
            {
                DialogResult = result;
                Close();
            };

            DataContext = viewModel;
        }
    }
}
