using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;

namespace Autocad_Primavera_P6_Plugin.UserInterface.UI_LinkedFoldersManagerView
{
    class LinkedFoldersManagerViewModel : INotifyPropertyChanged
    {
        private static MyPlugin _pluginInstance = null;

        private LinkedFoldersManagerModel _model;

        public ObservableCollection<string> LinkedFolders => _model.LinkedFolders;

        public LinkedFoldersManagerViewModel(MyPlugin pluginInstance)
        {
            _model = new LinkedFoldersManagerModel();
            _pluginInstance = pluginInstance;
        }

        // Example command for adding a folder
        private ICommand _addFolderCommand;
        public ICommand AddFolderCommand => _addFolderCommand ?? (_addFolderCommand = new RelayCommand(AddFolder));

        private void AddFolder(object parameter)
        {
            // Add folder logic here
        }

        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }

    // Simple RelayCommand implementation
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

