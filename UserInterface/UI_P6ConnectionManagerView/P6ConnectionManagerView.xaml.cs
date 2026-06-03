using System.Windows;

namespace Autocad_Primavera_P6_Plugin.UserInterface.UI_P6ConnectionManagerView
{
    public partial class P6ConnectionManagerView : Window
    {
        public P6ConnectionManagerView(MyPlugin pluginInstance)
        {
            InitializeComponent();
            var vm = new P6ConnectionManagerViewModel(pluginInstance);
            DataContext = vm;

            // PasswordBox can't data-bind securely in MVVM.
            // Wire it manually: push to VM when user types, pull from VM when
            // the selection changes (which resets the form fields).
            PasswordBoxField.PasswordChanged += (_, __) =>
                vm.PasswordText = PasswordBoxField.Password;

            vm.PropertyChanged += (_, e) =>
            {
                if (e.PropertyName == nameof(vm.PasswordText)
                    && PasswordBoxField.Password != vm.PasswordText)
                {
                    PasswordBoxField.Password = vm.PasswordText;
                }
            };
        }
    }
}