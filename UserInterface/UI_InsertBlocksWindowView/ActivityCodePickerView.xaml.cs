using System.Windows;

namespace Autocad_Primavera_P6_Plugin.UserInterface.UI_InsertBlocksWindowView
{
    public partial class ActivityCodePickerView : Window
    {
        public ActivityCodePickerView(MyPlugin pluginInstance, string sectionLabel, bool isAutoGenerate)
        {
            InitializeComponent();

            var viewModel = new ActivityCodePickerViewModel(pluginInstance, sectionLabel, isAutoGenerate);
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
