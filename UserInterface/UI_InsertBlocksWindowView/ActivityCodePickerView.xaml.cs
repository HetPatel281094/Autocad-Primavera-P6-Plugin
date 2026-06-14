using Autocad_Primavera_P6_Plugin.Services.LiteDBService;
using Autocad_Primavera_P6_Plugin.Services.P6ApiService;
using System.Windows;

namespace Autocad_Primavera_P6_Plugin.UserInterface.UI_InsertBlocksWindowView
{
    public partial class ActivityCodePickerView : Window
    {
        public ActivityCodePickerView(MyPlugin pluginInstance, string sectionLabel, bool isAutoGenerate, Project project = null)
        {
            InitializeComponent();

            var viewModel = new ActivityCodePickerViewModel(pluginInstance, sectionLabel, isAutoGenerate, project);
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
