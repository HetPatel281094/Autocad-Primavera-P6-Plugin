using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;

namespace Autocad_Primavera_P6_Plugin.UserInterface.UI_PluginStatusWindowView
{
    class PluginStatusWindowViewModel : INotifyPropertyChanged
    {
        private PluginStatusWindowModel _model;

        public ObservableCollection<string> PluginStatuses => _model.PluginStatuses;

        public PluginStatusWindowViewModel()
        {
            _model = new PluginStatusWindowModel();
        }

        private ICommand _refreshStatusCommand;
        public ICommand RefreshStatusCommand => _refreshStatusCommand ?? (_refreshStatusCommand = new RelayCommand(RefreshStatus));

        private void RefreshStatus(object parameter)
        {
            // Refresh plugin status logic here
        }

        public event PropertyChangedEventHandler PropertyChanged;

        protected void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }

    public class RelayCommand : ICommand
    {
        private readonly Action<object> _execute;
        private readonly Predicate<object> _canExecute;

        public RelayCommand(Action<object> execute, Predicate<object> canExecute = null)
        {
            _execute = execute;
            _canExecute = canExecute;
        }

        public bool CanExecute(object parameter) => _canExecute == null || _canExecute(parameter);

        public void Execute(object parameter) => _execute(parameter);

        public event EventHandler CanExecuteChanged
        {
            add { CommandManager.RequerySuggested += value; }
            remove { CommandManager.RequerySuggested -= value; }
        }
    }
}
