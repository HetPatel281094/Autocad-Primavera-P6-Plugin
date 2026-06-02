using System.Windows;

namespace Autocad_Primavera_P6_Plugin.UserInterface.UI_UserControls.UI_P6ProjectSelector
{
    public partial class P6ProjectSelectorView : Window
    {
        public P6ProjectSelectorView(P6ProjectSelectorViewModel dataContext)
        {
            InitializeComponent();
            DataContext = dataContext;
            dataContext.RequestClose += OnRequestClose;
        }

        private void OnRequestClose(bool? dialogResult)
        {
            DialogResult = dialogResult;
            Close();
        }
    }
}