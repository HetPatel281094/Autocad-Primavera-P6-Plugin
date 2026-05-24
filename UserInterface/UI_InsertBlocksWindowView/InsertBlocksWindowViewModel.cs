using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Controls;
using System.Windows.Input;

namespace Autocad_Primavera_P6_Plugin.UserInterface.UI_InsertBlocksWindowView
{
    class InsertBlocksWindowViewModel : INotifyPropertyChanged
    {
        private InsertBlocksWindowModel _model;
        private string _selectedBlock;

        public ObservableCollection<string> AvailableBlocks => _model.AvailableBlocks;

        public string SelectedBlock
        {
            get => _selectedBlock;
            set
            {
                _selectedBlock = value;
                OnPropertyChanged();
            }
        }

        public InsertBlocksWindowViewModel()
        {
            _model = new InsertBlocksWindowModel();
        }

        private ICommand _insertBlockCommand;
        public ICommand InsertBlockCommand => _insertBlockCommand ?? (_insertBlockCommand = new RelayCommand(InsertBlock));

        private void InsertBlock(object parameter)
        {
            // Insert block logic here
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
