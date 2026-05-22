using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;

namespace Autocad_Primavera_P6_Plugin.UserInterface.UI_P6ConnectionManagerView
{
    class P6ConnectionManagerViewModel : INotifyPropertyChanged
    {
        private P6ConnectionManagerModel _model;
        private string _selectedConnection;

        public ObservableCollection<string> P6Connections => _model.P6Connections;

        public string SelectedConnection
        {
            get => _selectedConnection;
            set
            {
                _selectedConnection = value;
                OnPropertyChanged();
            }
        }

        public P6ConnectionManagerViewModel()
        {
            _model = new P6ConnectionManagerModel();
        }

        private ICommand _addConnectionCommand;
        public ICommand AddConnectionCommand => _addConnectionCommand ?? (_addConnectionCommand = new RelayCommand(AddConnection));

        private void AddConnection(object parameter)
        {
            // Add P6 connection logic here
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
