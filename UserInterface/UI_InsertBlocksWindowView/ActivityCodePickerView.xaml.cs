using Autocad_Primavera_P6_Plugin.Services.LiteDBService;
using Autocad_Primavera_P6_Plugin.Services.P6ApiService;
using System.Windows;

namespace Autocad_Primavera_P6_Plugin.UserInterface.UI_InsertBlocksWindowView
{
    public partial class ActivityCodePickerView : Window
    {
        public ActivityCodePickerView(ActivityCodePickerModel model)
        {
            InitializeComponent();

            var viewModel = new ActivityCodePickerViewModel(model);
            viewModel.RequestClose += result =>
            {
                DialogResult = result;
                Close();
            };

            DataContext = viewModel;
        }

        public ActivityCodePickerViewModel ViewModel => DataContext as ActivityCodePickerViewModel;
    }
}
