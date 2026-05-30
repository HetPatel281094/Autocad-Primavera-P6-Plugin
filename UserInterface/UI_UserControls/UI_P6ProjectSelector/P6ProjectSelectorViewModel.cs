using Autocad_Primavera_P6_Plugin.Services.LiteDBService;
using Autocad_Primavera_P6_Plugin.Services.P6ApiService;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;

namespace Autocad_Primavera_P6_Plugin.UserInterface.UI_UserControls.UI_P6ProjectSelector
{
    public sealed class P6ProjectSelectorViewModel : INotifyPropertyChanged
    {
        private readonly MyPlugin _pluginInstance;
        private readonly P6ApiService _p6ApiService;
        private readonly LiteDBService _liteDBService;

        private readonly ObservableCollection<EPS> _epsCollection = new ObservableCollection<EPS>();
        private readonly ObservableCollection<Project> _projectCollection = new ObservableCollection<Project>();
        private readonly ObservableCollection<P6NodeViewModel> _rootNodes = new ObservableCollection<P6NodeViewModel>();

        private P6NodeViewModel _selectedNode;
        private bool _isLoading;
        private string _statusMessage;

        public P6ProjectSelectorViewModel(MyPlugin pluginInstance)
        {
            _pluginInstance = pluginInstance ?? throw new ArgumentNullException(nameof(pluginInstance));
            _p6ApiService = _pluginInstance.MyP6ApiService;
            _liteDBService = _pluginInstance.MyLiteDBService;

            _ = LoadAsync();
        }

        public ObservableCollection<P6NodeViewModel> RootNodes
        {
            get { return _rootNodes; }
        }

        public P6NodeViewModel SelectedNode
        {
            get => _selectedNode;
            set
            {
                if (ReferenceEquals(_selectedNode, value))
                {
                    return;
                }

                if (_selectedNode != null)
                {
                    _selectedNode.IsSelected = false;
                }

                _selectedNode = value;

                if (_selectedNode != null)
                {
                    _selectedNode.IsSelected = true;
                }

                OnPropertyChanged();
            }
        }

        public bool IsLoading
        {
            get => _isLoading;
            private set
            {
                if (_isLoading == value)
                {
                    return;
                }

                _isLoading = value;
                OnPropertyChanged();
            }
        }

        public string StatusMessage
        {
            get => _statusMessage;
            private set
            {
                if (_statusMessage == value)
                {
                    return;
                }

                _statusMessage = value;
                OnPropertyChanged();
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;

        public async Task LoadAsync()
        {
            try
            {
                IsLoading = true;
                StatusMessage = "Loading EPS and projects...";

                await LoadEPSCollection().ConfigureAwait(true);
                await LoadProjectCollection().ConfigureAwait(true);

                BuildProjectTree();
                StatusMessage = string.Empty;
            }
            catch (Exception ex)
            {
                StatusMessage = "Unable to load project tree.";
                Debug.Print(ex.ToString());
            }
            finally
            {
                IsLoading = false;
            }
        }

        private async Task LoadEPSCollection()
        {
            try
            {
                var client = _p6ApiService.Client;
                const string fields = "ObjectId, Id, Name, SequenceNumber, ParentObjectId";

                var epsList = await client.GetEPSAsync(null, fields, null, null).ConfigureAwait(true);

                _epsCollection.Clear();
                if (epsList != null)
                {
                    foreach (var eps in epsList)
                    {
                        if (eps != null)
                        {
                            _epsCollection.Add(eps);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.Print(ex.ToString());
                throw;
            }
        }

        private async Task LoadProjectCollection()
        {
            try
            {
                var client = _p6ApiService.Client;
                const string fields = "ObjectId, Id, Name, ParentEPSObjectId";

                var projectList = await client.GetProjectAsync(null, fields, null, null).ConfigureAwait(true);

                _projectCollection.Clear();
                if (projectList != null)
                {
                    foreach (var project in projectList)
                    {
                        if (project != null)
                        {
                            _projectCollection.Add(project);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.Print(ex.ToString());
                throw;
            }
        }

        private void BuildProjectTree()
        {
            SelectedNode = null;
            RootNodes.Clear();

            var roots = new List<P6NodeViewModel>();
            var epsNodesByObjectId = new Dictionary<int, P6NodeViewModel>();

            var epsItems = _epsCollection
                .Where(eps => eps != null)
                .OrderBy(eps => eps.SequenceNumber)
                .ToList();

            foreach (var eps in epsItems)
            {
                var epsNode = new P6NodeViewModel(eps.Id, eps.Name, isEps: true, isExpanded: true);
                epsNodesByObjectId.Add(eps.ObjectId.Value, epsNode);
            }

            foreach (var eps in epsItems)
            {
                var epsNode = epsNodesByObjectId[eps.ObjectId.Value];

                if (eps.ParentObjectId > 0 && epsNodesByObjectId.TryGetValue(eps.ParentObjectId, out var parentEpsNode))
                {
                    parentEpsNode.Children.Add(epsNode);
                }
                else
                {
                    roots.Add(epsNode);
                }
            }

            var projectItems = _projectCollection
                .Where(project => project != null)
                .OrderBy(project => project.Id ?? string.Empty, StringComparer.OrdinalIgnoreCase)
                .ToList();

            foreach (var project in projectItems)
            {
                var projectNode = new P6NodeViewModel(project.Id, project.Name, isEps: false);

                if (project.ParentEPSObjectId > 0 &&
                    epsNodesByObjectId.TryGetValue(project.ParentEPSObjectId, out var parentEpsNode))
                {
                    parentEpsNode.Children.Add(projectNode);
                }
                else
                {
                    roots.Add(projectNode);
                }
            }

            foreach (var root in roots)
            {
                RootNodes.Add(root);
            }
        }

        private void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
