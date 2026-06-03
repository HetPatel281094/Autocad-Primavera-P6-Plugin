using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Windows;
using System.Windows.Input;
using PropertyChanged;
using Autocad_Primavera_P6_Plugin.Services.LiteDBService;
using MessageBox = System.Windows.MessageBox;

namespace Autocad_Primavera_P6_Plugin.UserInterface.UI_P6ConnectionManagerView
{
    /// <summary>
    /// ViewModel for the P6 Connection Manager window.
    ///
    /// PropertyChanged.Fody weaves INotifyPropertyChanged into every auto-property
    /// automatically. The OnXxxChanged() methods are Fody convention: they are called
    /// by the woven setter right after the backing field is updated, keeping
    /// _workingConfig in sync with what the user types in the form.
    /// </summary>
    [AddINotifyPropertyChangedInterface]
    public class P6ConnectionManagerViewModel : INotifyPropertyChanged
    {
        // ── Private state ──────────────────────────────────────────────────────
        private readonly MyPlugin _pluginInstance;

        /// <summary>
        /// In-memory copy of the config currently being edited.
        /// Never bound directly to the UI — the flat properties below are.
        /// OnXxxChanged callbacks keep it in sync as the user types.
        /// </summary>
        private P6ConnectionConfig _workingConfig = new P6ConnectionConfig();

        public event PropertyChangedEventHandler PropertyChanged;

        // ── Form-bound auto-properties (Fody injects OnPropertyChanged) ────────

        public string ConnectionNameText { get; set; } = string.Empty;
        public string ServerUrlText      { get; set; } = string.Empty;
        public string UsernameText       { get; set; } = string.Empty;
        public string PasswordText       { get; set; } = string.Empty;
        public string DatabaseNameText   { get; set; } = string.Empty;

        /// <summary>Status line shown after "Test Connection" is clicked.</summary>
        public string TestStatusMessage { get; private set; } = string.Empty;

        public bool IsFormEnabled { get; private set; } = true;

        // ── List ────────────────────────────────────────────────────────────────

        public ObservableCollection<P6ConnectionConfig> AllConnections { get; private set; }
            = new ObservableCollection<P6ConnectionConfig>();

        /// <summary>
        /// Bound to the ListBox SelectedItem.
        /// OnSelectedConnectionChanged() loads the row into the form for editing.
        /// </summary>
        public P6ConnectionConfig SelectedConnection { get; set; }

        // ── Commands ────────────────────────────────────────────────────────────
        public ICommand SaveConnectionCommand   { get; }
        public ICommand ResetConnectionCommand  { get; }
        public ICommand DeleteConnectionCommand { get; }

        /// <summary>
        /// Renamed from SetActiveCommand → SetDefaultCommand to match
        /// the XAML binding and the IsDefault property on P6ConnectionConfig.
        /// </summary>
        public ICommand SetDefaultCommand       { get; }
        public ICommand TestConnectionCommand   { get; }

        // ── Constructor ─────────────────────────────────────────────────────────
        public P6ConnectionManagerViewModel(MyPlugin pluginInstance)
        {
            _pluginInstance = pluginInstance;

            SaveConnectionCommand   = new RelayCommand(SaveConnection);
            ResetConnectionCommand  = new RelayCommand(_ => InitNewConfig());
            DeleteConnectionCommand = new RelayCommand(DeleteConnection, _ => SelectedConnection != null);
            SetDefaultCommand       = new RelayCommand(SetDefault,       _ => SelectedConnection != null);
            TestConnectionCommand   = new RelayCommand(TestConnection);

            LoadConnections();
            InitNewConfig();
        }

        // ── Fody OnXxxChanged callbacks ──────────────────────────────────────────
        // These fire automatically right after Fody updates the backing field.
        // They keep _workingConfig in sync with whatever the user types.

        private void OnConnectionNameTextChanged() => _workingConfig.ConnectionName = ConnectionNameText;
        private void OnServerUrlTextChanged()      => _workingConfig.ServerUrl       = ServerUrlText;
        private void OnUsernameTextChanged()       => _workingConfig.Username        = UsernameText;
        private void OnPasswordTextChanged()       => _workingConfig.Password        = PasswordText;
        private void OnDatabaseNameTextChanged()   => _workingConfig.DatabaseName    = DatabaseNameText;

        /// <summary>
        /// When the user clicks a row, clone it into _workingConfig and mirror
        /// all fields into the form. A clone is used so edits don't mutate
        /// the list item until Save is clicked — same pattern as LinkedFolders.
        /// </summary>
        private void OnSelectedConnectionChanged()
        {
            if (SelectedConnection == null) return;

            _workingConfig = new P6ConnectionConfig
            {
                Id             = SelectedConnection.Id,
                ConnectionName = SelectedConnection.ConnectionName,
                ServerUrl      = SelectedConnection.ServerUrl,
                Username       = SelectedConnection.Username,
                Password       = SelectedConnection.Password,
                DatabaseName   = SelectedConnection.DatabaseName,
                IsDefault      = SelectedConnection.IsDefault,
            };

            // Each setter triggers OnXxxChanged → _workingConfig stays in sync
            ConnectionNameText = _workingConfig.ConnectionName ?? string.Empty;
            ServerUrlText      = _workingConfig.ServerUrl      ?? string.Empty;
            UsernameText       = _workingConfig.Username       ?? string.Empty;
            PasswordText       = _workingConfig.Password       ?? string.Empty;
            DatabaseNameText   = _workingConfig.DatabaseName   ?? string.Empty;
            TestStatusMessage  = string.Empty;
            IsFormEnabled      = true;
        }

        // ── Command handlers ────────────────────────────────────────────────────

        private void SaveConnection(object _)
        {
            try
            {
                // Final explicit sync (safety net — callbacks handle the normal case)
                _workingConfig.ConnectionName = ConnectionNameText;
                _workingConfig.ServerUrl      = ServerUrlText;
                _workingConfig.Username       = UsernameText;
                _workingConfig.Password       = PasswordText;
                _workingConfig.DatabaseName   = DatabaseNameText;

                var saved = _pluginInstance.MyLiteDBService.Upsert_P6ConnectionConfig(_workingConfig);
                _workingConfig = saved;

                LoadConnections();
                SelectedConnection = AllConnections.FirstOrDefault(c => c.Id == saved.Id);
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    $"Failed to save connection:\n\n{ex.Message}",
                    "Save Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void DeleteConnection(object _)
        {
            if (SelectedConnection == null) return;

            var confirm = MessageBox.Show(
                $"Delete connection \"{SelectedConnection.ConnectionName}\"?\n\nThis cannot be undone.",
                "Confirm Delete", MessageBoxButton.YesNo, MessageBoxImage.Warning);

            if (confirm != MessageBoxResult.Yes) return;

            try
            {
                _pluginInstance.MyLiteDBService.Delete_P6ConnectionConfig(SelectedConnection.Id);
                AllConnections.Remove(SelectedConnection);
                InitNewConfig();
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    $"Failed to delete connection:\n\n{ex.Message}",
                    "Delete Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        /// <summary>
        /// Marks the selected connection as the default in the DB, reloads the
        /// list so the DEFAULT badge renders on the correct row only, then
        /// re-initialises the live P6 API client with the new credentials.
        ///
        /// BUG FIXES:
        ///   1. Capture targetId BEFORE LoadConnections() replaces the collection —
        ///      otherwise SelectedConnection.Id is read from an object that may no
        ///      longer be the same reference after the ObservableCollection is rebuilt.
        ///   2. Re-select by targetId (not _workingConfig.Id) so the highlighted row
        ///      always matches the connection that was just made default.
        ///   3. Call ReInitializeAsync() so the live P6 client actually switches to
        ///      the new default (previously this was commented out / unimplemented).
        /// </summary>
        private async void SetDefault(object _)
        {
            if (SelectedConnection == null) return;

            try
            {
                // Capture before LoadConnections() rebuilds the collection
                int targetId = SelectedConnection.Id;

                _pluginInstance.MyLiteDBService.SetDefault_P6ConnectionConfig(targetId);

                // Reload so IsDefault is correct on every item in the list
                LoadConnections();

                // Re-select the row we just promoted
                SelectedConnection = AllConnections.FirstOrDefault(c => c.Id == targetId);

                // Re-initialise the live P6 API client with the new default credentials
                await _pluginInstance.MyP6ApiService.ReInitializeAsync();

                TestStatusMessage = _pluginInstance.MyP6ApiService.IsLoggedIn
                    ? "✓ Switched to new default connection"
                    : "⚠ Default updated but login with new credentials failed";
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    $"Failed to set default connection:\n\n{ex.Message}",
                    "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async void TestConnection(object _)
        {
            TestStatusMessage = "Testing...";
            IsFormEnabled = false;
            try
            {
                var result = await _pluginInstance.MyP6ApiService.TryLoginAsync(
                    new P6ConnectionConfig
                    {
                        ServerUrl    = ServerUrlText,
                        Username     = UsernameText,
                        Password     = PasswordText,
                        DatabaseName = DatabaseNameText,
                    });

                TestStatusMessage = result.IsLoggedIn
                    ? "✓ Connection successful"
                    : "✗ Login failed — check credentials";
            }
            catch (Exception ex)
            {
                TestStatusMessage = $"✗ Error: {ex.Message}";
            }
            finally
            {
                IsFormEnabled = true;
            }
        }

        // ── Private helpers ─────────────────────────────────────────────────────

        private void LoadConnections()
        {
            AllConnections.Clear();
            foreach (var c in _pluginInstance.MyLiteDBService.GetAll_P6ConnectionConfigs())
                AllConnections.Add(c);
        }

        private void InitNewConfig()
        {
            SelectedConnection = null;
            _workingConfig     = new P6ConnectionConfig();

            ConnectionNameText = GenerateNewConnectionName();
            ServerUrlText      = "http://localhost:8206/p6ws/restapi/";
            UsernameText       = string.Empty;
            PasswordText       = string.Empty;
            DatabaseNameText   = "PMDB";
            TestStatusMessage  = string.Empty;
            IsFormEnabled      = true;
        }

        private string GenerateNewConnectionName()
        {
            int maxN = AllConnections
                .Select(c => c.ConnectionName)
                .Where(n => n != null
                         && n.StartsWith("Connection_", StringComparison.OrdinalIgnoreCase)
                         && int.TryParse(n.Substring("Connection_".Length), out _))
                .Select(n => int.Parse(n.Substring("Connection_".Length)))
                .DefaultIfEmpty(0)
                .Max();

            return $"Connection_{maxN + 1}";
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
        public void Execute(object parameter)    => _execute(parameter);

        public event EventHandler CanExecuteChanged
        {
            add    { CommandManager.RequerySuggested += value; }
            remove { CommandManager.RequerySuggested -= value; }
        }
    }
}
