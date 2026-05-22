using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;

namespace Autocad_Primavera_P6_Plugin.UserInterface.UI_PropertiesPalateView
{
    class PropertiesPalateViewModel : INotifyPropertyChanged
    {
        private PropertiesPalateModel _model;
        private string _selectedProperty;

        public ObservableCollection<string> Properties => _model.Properties;

        public string SelectedProperty
        {
            get => _selectedProperty;
            set
            {
                _selectedProperty = value;
                OnPropertyChanged();
            }
        }

        public PropertiesPalateViewModel()
        {
            _model = new PropertiesPalateModel();
        }

        private ICommand _loadPropertiesCommand;
        public ICommand LoadPropertiesCommand => _loadPropertiesCommand ?? (_loadPropertiesCommand = new RelayCommand(LoadProperties));

        private void LoadProperties(object parameter)
        {
            // Load selected object properties logic here
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
