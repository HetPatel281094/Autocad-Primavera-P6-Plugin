using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using System.Windows.Forms;
using PropertyChanged;
using AcadApp = Autodesk.AutoCAD.ApplicationServices.Application;
using Autocad_Primavera_P6_Plugin.UserInterface.UI_UserControls.UI_P6ProjectSelector;
using Autocad_Primavera_P6_Plugin.Services.LiteDBService;
using Autocad_Primavera_P6_Plugin.Services.P6ApiService;
using MessageBox = System.Windows.MessageBox;

namespace Autocad_Primavera_P6_Plugin.UserInterface.UI_LinkedFoldersManagerView
{
    /// <summary>
    /// ViewModel for the Linked Folders Manager window.
    ///
    /// PropertyChanged.Fody weaves INotifyPropertyChanged into every auto-property
    /// automatically. The OnXxxChanged() methods are Fody convention: they are called
    /// by the woven setter right after the backing field is updated, keeping
    /// _workingConfig in sync with what the user types in the form.
    /// </summary>
    [AddINotifyPropertyChangedInterface]
    public class LinkedFoldersManagerViewModel
    {
        // ── Private state ──────────────────────────────────────────────────────
        private readonly MyPlugin _pluginInstance;

        /// <summary>
        /// In-memory copy of the config currently being edited.
        /// This is never bound directly to the UI; the flat properties below are.
        /// Writing to those flat properties (via bindings or code) syncs back here
        /// through the OnXxxChanged callbacks. Saves write this to the DB.
        /// </summary>
        private ProjectConfig _workingConfig = new ProjectConfig();

        // ── Form-bound auto-properties (Fody injects OnPropertyChanged) ────────

        /// <summary>User-editable name for the link (two-way bound TextBox).</summary>
        public string LinkNameText { get; set; } = string.Empty;

        /// <summary>Read-only display of the selected folder path.</summary>
        public string FolderPathText { get; private set; } = string.Empty;

        /// <summary>Read-only display of the chosen P6 project.</summary>
        public string ProjectSelectorText { get; private set; } = string.Empty;

        /// <summary>
        /// Controls whether the Link Name TextBox is editable.
        /// True whenever a working config exists (new or loaded).
        /// </summary>
        public bool IsLinkNameTextBoxEnabled { get; private set; } = true;

        // ── List ────────────────────────────────────────────────────────────────

        /// <summary>All saved configs, bound to the ListBox ItemsSource.</summary>
        public ObservableCollection<ProjectConfig> AllProjectConfigs { get; private set; }
            = new ObservableCollection<ProjectConfig>();

        /// <summary>
        /// Bound to the ListBox SelectedItem.
        /// When the user clicks a row, OnSelectedListItemChanged() loads that
        /// config's data into the form fields.
        /// </summary>
        public ProjectConfig SelectedListItem { get; set; }

        // ── Commands ────────────────────────────────────────────────────────────
        public ICommand SaveProjectConfigCommand { get; }
        public ICommand ResetProjectConfigCommand { get; }
        public ICommand SelectFolderCommand { get; }
        public ICommand SelectProjectCommand { get; }
        public ICommand DeleteProjectConfigCommand { get; }

        // ── Constructor ─────────────────────────────────────────────────────────
        public LinkedFoldersManagerViewModel(MyPlugin pluginInstance)
        {
            _pluginInstance = pluginInstance;

            SaveProjectConfigCommand = new RelayCommand(SaveProjectConfig);
            ResetProjectConfigCommand = new RelayCommand(ResetProjectConfig);
            SelectFolderCommand = new RelayCommand(SelectFolder);
            SelectProjectCommand = new RelayCommand(SelectProject);
            DeleteProjectConfigCommand = new RelayCommand(DeleteProjectConfig, CanDeleteProjectConfig);

            LoadProjectConfigs();
            InitNewConfigFirstTime();
        }

        // ── Fody OnXxxChanged callbacks ─────────────────────────────────────────
        // Fody calls these automatically right after the matching property's
        // backing field is updated. They keep _workingConfig in sync.

        /// <summary>
        /// Keeps _workingConfig.LinkName in sync whenever the user types
        /// in the Link Name textbox (or the field is set programmatically).
        /// </summary>
        private void OnLinkNameTextChanged()
        {
            if (_workingConfig != null)
                _workingConfig.LinkName = LinkNameText;
        }

        /// <summary>
        /// Keeps _workingConfig.ProjectPlanningDWGFolderPath in sync with
        /// the folder path field.
        /// </summary>
        private void OnFolderPathTextChanged()
        {
            if (_workingConfig != null)
                _workingConfig.ProjectPlanningDWGFolderPath = FolderPathText;
        }

        /// <summary>
        /// When the user selects a row in the ListBox, this clones that config
        /// into _workingConfig and mirrors all fields into the form properties.
        /// A clone is used so edits don't mutate the list item until Save is clicked.
        /// </summary>
        private void OnSelectedListItemChanged()
        {
            if (SelectedListItem == null) return;

            // Clone so that editing doesn't affect the displayed list until saved
            _workingConfig = new ProjectConfig
            {
                Id = SelectedListItem.Id,
                LinkName = SelectedListItem.LinkName,
                ProjectObjectId = SelectedListItem.ProjectObjectId,
                ProjectId = SelectedListItem.ProjectId,
                ProjectName = SelectedListItem.ProjectName,
                ProjectPlanningDWGFolderPath = SelectedListItem.ProjectPlanningDWGFolderPath,
            };

            // Fody picks up each setter call below and raises PropertyChanged
            // for that property, so the UI updates automatically.
            LinkNameText = _workingConfig.LinkName ?? string.Empty;
            FolderPathText = _workingConfig.ProjectPlanningDWGFolderPath ?? string.Empty;
            ProjectSelectorText = BuildProjectSelectorText();
            IsLinkNameTextBoxEnabled = true;
        }

        // ── Command handlers ────────────────────────────────────────────────────

        private void SaveProjectConfig(object _)
        {
            try
            {
                // The OnXxxChanged callbacks keep _workingConfig current as the
                // user types, but we do a final explicit sync as a safety net.
                _workingConfig.LinkName = LinkNameText;
                _workingConfig.ProjectPlanningDWGFolderPath = FolderPathText;

                var saved = _pluginInstance.MyLiteDBService.CreateNew_ProjectConfig(_workingConfig);
                _workingConfig = saved;

                // Refresh the list and re-select the item that was just saved
                LoadProjectConfigs();
                SelectedListItem = AllProjectConfigs.FirstOrDefault(c => c.Id == saved.Id);

                IsLinkNameTextBoxEnabled = true;
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    $"Failed to save the link configuration:\n\n{ex.Message}",
                    "Save Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }

        private void ResetProjectConfig(object _) => InitNewConfig();

        private void SelectFolder(object _)
        {
            using (var dialog = new FolderBrowserDialog
            {
                Description = "Select the AutoCAD DWG folder to link",
                ShowNewFolderButton = true,
            })
            {
                if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
                    FolderPathText = dialog.SelectedPath;
                // OnFolderPathTextChanged() fires automatically and updates _workingConfig
            }
        }

        private void SelectProject(object _)
        {
            var contextViewModel = new P6ProjectSelectorViewModel(_pluginInstance);
            var controlView = new P6ProjectSelectorView(contextViewModel) { DataContext = contextViewModel };

            bool? result = AcadApp.ShowModalWindow(controlView);

            if (result == true && contextViewModel.IsProjectSelected && contextViewModel.SelectedProject != null)
            {
                Project p = contextViewModel.SelectedProject;
                _workingConfig.ProjectObjectId = p.ObjectId.ToString();
                _workingConfig.ProjectId = p.Id;
                _workingConfig.ProjectName = p.Name;

                // ProjectSelectorText has no user-editable textbox so we update it here
                ProjectSelectorText = BuildProjectSelectorText();
            }
        }

        private bool CanDeleteProjectConfig(object _) => SelectedListItem != null;

        private void DeleteProjectConfig(object _)
        {
        }

        private void ResetProjectConfig(object _) => InitNewConfig();

        private void SelectFolder(object _)
        {
            using (var dialog = new FolderBrowserDialog
            {
                Description = "Select the AutoCAD DWG folder to link",
                ShowNewFolderButton = true,
            })
            {
                if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
                    FolderPathText = dialog.SelectedPath;
                // OnFolderPathTextChanged() fires automatically and updates _workingConfig
            }
        }
            if (SelectedListItem == null) return;

            var confirm = MessageBox.Show(
                $"Delete the link \"{SelectedListItem.LinkName}\"?\n\nThis cannot be undone.",
                "Confirm Delete",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);

            if (confirm != MessageBoxResult.Yes) return;

            try
            {
                _pluginInstance.MyLiteDBService.Delete_ProjectConfig(SelectedListItem.Id);
                AllProjectConfigs.Remove(SelectedListItem);
                InitNewConfig(); // Clear form and start fresh
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    $"Failed to delete the link configuration:\n\n{ex.Message}",
                    "Delete Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }

        // ── Private helpers ─────────────────────────────────────────────────────

        /// <summary>Fetches all configs from the DB and refreshes AllProjectConfigs.</summary>
        private void LoadProjectConfigs()
        {
            AllProjectConfigs.Clear();
            foreach (var config in _pluginInstance.MyLiteDBService.GetAll_ProjectConfigs())
                AllProjectConfigs.Add(config);
        }

        /// <summary>
        /// Resets the form to a blank new-config state.
        /// Called by the "+" button and after a successful delete.
        /// </summary>
        private void InitNewConfig()
        {
            // Clear list selection first; OnSelectedListItemChanged guards against null
            SelectedListItem = null;

            _workingConfig = new ProjectConfig();

            // Setting these auto-properties triggers their OnXxxChanged callbacks,
            // which sync _workingConfig — intentional and harmless for initial values.
            LinkNameText = GenerateNewLinkName();
            FolderPathText = string.Empty;
            ProjectSelectorText = string.Empty;
            IsLinkNameTextBoxEnabled = true;
        }

        /// <summary>
        /// Check if current autocad file belong to any linked folder config.
        /// if exist load that config else load new config 
        /// </summary>
        private void InitNewConfigFirstTime()
        {
            // Clear list selection first; OnSelectedListItemChanged guards against null
            SelectedListItem = null;

            var drawingFile = AcadApp.DocumentManager.MdiActiveDocument;
            var projectConfig = _pluginInstance.MyLiteDBService.Find_byAcadDWG(drawingFile);

            if (projectConfig != null)
            {
                _workingConfig = new ProjectConfig
                {
                    Id = projectConfig.Id,
                    LinkName = projectConfig.LinkName,
                    ProjectObjectId = projectConfig.ProjectObjectId,
                    ProjectId = projectConfig.ProjectId,
                    ProjectName = projectConfig.ProjectName,
                    ProjectPlanningDWGFolderPath = projectConfig.ProjectPlanningDWGFolderPath,
                };

                // Fody picks up each setter call below and raises PropertyChanged
                // for that property, so the UI updates automatically.
                LinkNameText = _workingConfig.LinkName ?? string.Empty;
                FolderPathText = _workingConfig.ProjectPlanningDWGFolderPath ?? string.Empty;
                ProjectSelectorText = BuildProjectSelectorText();
                IsLinkNameTextBoxEnabled = true;
            }
            else
            {
                InitNewConfig();
            }
        }

        /// <summary>
        /// Generates a unique "Link_N" name that doesn't collide with any
        /// existing config in the list.
        /// </summary>
        private string GenerateNewLinkName()
        {
            int maxN = AllProjectConfigs
                .Select(c => c.LinkName)
                .Where(name => name != null
                            && name.StartsWith("Link_", StringComparison.OrdinalIgnoreCase)
                            && int.TryParse(name.Substring("Link_".Length), out _))
                .Select(name => int.Parse(name.Substring("Link_".Length)))
                .DefaultIfEmpty(0)
                .Max();

            return $"Link_{maxN + 1}";
        }

        /// <summary>Formats the P6 project display string from _workingConfig.</summary>
        private string BuildProjectSelectorText()
        {
            if (!string.IsNullOrWhiteSpace(_workingConfig?.ProjectId) &&
                !string.IsNullOrWhiteSpace(_workingConfig?.ProjectName))
            {
                return $"({_workingConfig.ProjectId}) {_workingConfig.ProjectName}";
            }
            return string.Empty;
        }

        /// <summary>
        /// Check if current autocad file belong to any linked folder config.
        /// if exist load that config else load new config 
        /// </summary>
        private void InitNewConfigFirstTime()
        {
            // Clear list selection first; OnSelectedListItemChanged guards against null
            SelectedListItem = null;

            var drawingFile = AcadApp.DocumentManager.MdiActiveDocument;
            var projectConfig = _pluginInstance.MyLiteDBService.Find_byAcadDWG(drawingFile);

            if (projectConfig != null)
            {
                _workingConfig = new ProjectConfig
                {
                    Id = projectConfig.Id,
                    LinkName = projectConfig.LinkName,
                    ProjectObjectId = projectConfig.ProjectObjectId,
                    ProjectId = projectConfig.ProjectId,
                    ProjectName = projectConfig.ProjectName,
                    ProjectPlanningDWGFolderPath = projectConfig.ProjectPlanningDWGFolderPath,
                };

                // Fody picks up each setter call below and raises PropertyChanged
                // for that property, so the UI updates automatically.
                LinkNameText = _workingConfig.LinkName ?? string.Empty;
                FolderPathText = _workingConfig.ProjectPlanningDWGFolderPath ?? string.Empty;
                ProjectSelectorText = BuildProjectSelectorText();
                IsLinkNameTextBoxEnabled = true;
            }
            else
            {
                InitNewConfig();
            }
        }

        /// <summary>
        /// Generates a unique "Link_N" name that doesn't collide with any
        /// existing config in the list.
        /// </summary>
        private string GenerateNewLinkName()
        {
            int maxN = AllProjectConfigs
                .Select(c => c.LinkName)
                .Where(name => name != null
                            && name.StartsWith("Link_", StringComparison.OrdinalIgnoreCase)
                            && int.TryParse(name.Substring("Link_".Length), out _))
                .Select(name => int.Parse(name.Substring("Link_".Length)))
                .DefaultIfEmpty(0)
                .Max();

            return $"Link_{maxN + 1}";
        }

        /// <summary>Formats the P6 project display string from _workingConfig.</summary>
        private string BuildProjectSelectorText()
        {
            if (!string.IsNullOrWhiteSpace(_workingConfig?.ProjectId) &&
                !string.IsNullOrWhiteSpace(_workingConfig?.ProjectName))
            {
                return $"({_workingConfig.ProjectId}) {_workingConfig.ProjectName}";
            }
            return string.Empty;
        }
    }

    // ── RelayCommand ─────────────────────────────────────────────────────────────
    public class RelayCommand : ICommand
    {
        private readonly Action<object> _execute;
        private readonly Predicate<object> _canExecute;

        public RelayCommand(Action<object> execute, Predicate<object> canExecute = null)
        {
            _execute = execute ?? throw new ArgumentNullException(nameof(execute));
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
