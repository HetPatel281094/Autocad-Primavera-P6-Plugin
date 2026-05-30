using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Input;
using System.Windows.Forms;
using Autocad_Primavera_P6_Plugin.UserInterface.UI_UserControls.UI_P6ProjectSelector;
using Autocad_Primavera_P6_Plugin.Services.LiteDBService;

namespace Autocad_Primavera_P6_Plugin.UserInterface.UI_LinkedFoldersManagerView
{
    public class LinkedFoldersManagerViewModel : INotifyPropertyChanged
    {
        private static MyPlugin _pluginInstance = null;

        // --- SelectedProjectConfig ---
        private ProjectConfig _selectedProjectConfig = null;

        public bool IsLinkNameTextBoxEnabled
        {
            get
            {
                return (SelectedProjectConfig != null && _selectedProjectConfig?.Id != null);
            }
        }

        public string LinkNameText
        {
            get => _selectedProjectConfig.LinkName;
            set
            {
                if (_selectedProjectConfig != null)
                {
                    _selectedProjectConfig.LinkName = value;
                    OnPropertyChanged();
                }
            }
        }

        public string FolderPathText
        {
            get => _selectedProjectConfig.ProjectPlanningDWGFolderPath;
            set
            {
                if (_selectedProjectConfig != null)
                {
                    _selectedProjectConfig.ProjectPlanningDWGFolderPath = value;
                    OnPropertyChanged();
                }
            }
        }

        public string ProjectSelectorText
        {
            get
            {
                if (_selectedProjectConfig.ProjectId != null && _selectedProjectConfig.ProjectName != null)
                {
                    return $"({_selectedProjectConfig.ProjectId}) {_selectedProjectConfig.ProjectName}";
                }
                else
                {
                    return "";
                }
            }
        }

        public ProjectConfig SelectedProjectConfig
        {
            get => _selectedProjectConfig;
            set 
            { 
                _selectedProjectConfig = value; 
                OnPropertyChanged();
                OnPropertyChanged(nameof(IsLinkNameTextBoxEnabled));
                OnPropertyChanged(nameof(LinkNameText));
                OnPropertyChanged(nameof(FolderPathText));
                OnPropertyChanged(nameof(ProjectSelectorText));
            }
        }

        // --- Commands ---
        public ICommand SaveProjectConfigCommand { get; }
        public ICommand ResetProjectConfigCommand { get; }
        public ICommand SelectFolderCommand { get; }
        public ICommand SelectProjectCommand { get; }

        public LinkedFoldersManagerViewModel(MyPlugin pluginInstance)
        {
            _pluginInstance = pluginInstance;

            SelectedProjectConfig = new ProjectConfig();

            // Initialize Commands
            SaveProjectConfigCommand = new RelayCommand(SaveProjectConfig);
            ResetProjectConfigCommand = new RelayCommand(ResetProjectConfig);
            SelectFolderCommand = new RelayCommand(SelectFolder);
            SelectProjectCommand = new RelayCommand(SelectProject);
        }

        private void SaveProjectConfig(object parameter)
        {
            // Placeholder for save logic (e.g., saving to a config file or DB)
        }

        private void ResetProjectConfig(object parameter)
        {
            // Placeholder for reset logic (e.g., reverting to last saved state)
            SelectedProjectConfig = new ProjectConfig();
        }

        private void SelectFolder(object parameter)
        {
            // Open the standard Windows Folder Browser Dialog
            using (var dialog = new FolderBrowserDialog())
            {
                dialog.Description = "Select a folder to link";
                dialog.ShowNewFolderButton = true;

                if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
                {
                    FolderPathText = dialog.SelectedPath;
                }
            }
        }

        private void SelectProject(object parameter)
        {
            // Initialize the View Component and bind its decoupled context
            var controlView = new P6ProjectSelectorView();
            var contextViewModel = new P6ProjectSelectorViewModel(_pluginInstance);
            controlView.DataContext = contextViewModel;

            // Wrap the control in a modal presentation window context
            System.Windows.Window modalWrapper = new System.Windows.Window
            {
                Title = "Select Primavera P6 Project Target",
                Content = controlView,
                Width = 750,
                Height = 900,
                WindowStartupLocation = System.Windows.WindowStartupLocation.CenterOwner,
                ResizeMode = System.Windows.ResizeMode.CanResize
            };

            // Show Dialog block process loop execution
            bool? interactionResult = modalWrapper.ShowDialog();

            if (interactionResult == true && contextViewModel.SelectedNode != null)
            {
                var targetElement = contextViewModel.SelectedNode;
                if (!targetElement.IsEPS)
                {
                    // Process the designated project reference node object downstream
                    string selectedId = targetElement.ProjectId;
                }
            }
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
