using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Controls;
using System.Windows.Input;
using Autocad_Primavera_P6_Plugin.Services;
using Autocad_Primavera_P6_Plugin.Services.LiteDBService;
using Autocad_Primavera_P6_Plugin.Services.P6ApiService;
using Autodesk.AutoCAD.ApplicationServices;
using App = Autodesk.AutoCAD.ApplicationServices.Application;
using LiteDB;

namespace Autocad_Primavera_P6_Plugin.UserInterface.UI_InsertBlocksWindowView
{
    class InsertBlocksWindowViewModel : INotifyPropertyChanged
    {
        private static MyPlugin _pluginInstance = null;
        private static LiteDBService _liteDBService = null;
        private static P6ApiService _p6ApiService = null;
        private static Autodesk.AutoCAD.ApplicationServices.Document 
                             _document = null;

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

        public InsertBlocksWindowViewModel(MyPlugin pluginInstance)
        {
            _model = new InsertBlocksWindowModel();
            _document = App.DocumentManager.MdiActiveDocument;
            _pluginInstance = pluginInstance;
            _liteDBService = _pluginInstance.MyLiteDBService;
            _p6ApiService = _pluginInstance.MyP6ApiService;
            initViewModel();
        }

        private void initViewModel()
        {
            var _projectConfig = _liteDBService.Find_byAcadDWG(_document);
            var _breakPoint = ""; // For debugging purposes
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
