using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;

namespace Autocad_Primavera_P6_Plugin.UserInterface.UI_InsertBlocksWindowView.CopyActivityOptionsPicker
{
    /// <summary>
    /// Interaction logic for CopyActivityOptionsPickerView.xaml
    /// </summary>
    public partial class CopyActivityOptionsPickerView : Window
    {
        public CopyActivityOptionsPickerView(CopyActivityOptionsPickerViewModel viewModel)
        {
            InitializeComponent();

            DataContext = viewModel;
        }
    }
}
