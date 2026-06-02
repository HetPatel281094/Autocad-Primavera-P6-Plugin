using Autocad_Primavera_P6_Plugin.Services.P6ApiService;
using PropertyChanged;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;

namespace Autocad_Primavera_P6_Plugin.UserInterface.UI_UserControls.UI_P6ProjectSelector
{
    [AddINotifyPropertyChangedInterface]
    public sealed class P6ProjectSelectorViewModel
    {
        private readonly MyPlugin _pluginInstance;
        private readonly P6ApiService _p6ApiService;

        private readonly ObservableCollection<EPS> _epsCollection = new ObservableCollection<EPS>();
        private readonly ObservableCollection<Project> _projectCollection = new ObservableCollection<Project>();

        public ObservableCollection<P6NodeViewModel> RootNodes { get; } = new ObservableCollection<P6NodeViewModel>();
        public P6NodeViewModel SelectedNode { get; set; }
        public bool IsProjectSelected => SelectedNode != null && SelectedNode.IsProject;
        public Project SelectedProject => IsProjectSelected ? SelectedNode?.ProjectInstance : null;
        public bool IsLoading { get; private set; }
        public string StatusMessage { get; private set; }
        public string SearchString { get; set; } = string.Empty;

        public bool IsCancelled { get; private set; }
        public event Action<bool?> RequestClose;

        public ICommand SearchButtonCommand { get; }
        public ICommand OkButtonCommand { get; }
        public ICommand CancelButtonCommand { get; }

        public P6ProjectSelectorViewModel(MyPlugin pluginInstance)
        {
            _pluginInstance = pluginInstance ?? throw new ArgumentNullException(nameof(pluginInstance));
            _p6ApiService = _pluginInstance.MyP6ApiService;

            // Initialize Commands
            SearchButtonCommand = new RelayCommand(SearchButton);
            OkButtonCommand = new RelayCommand(OkButton);
            CancelButtonCommand = new RelayCommand(CancelButton);

            _ = LoadAsync();
        }

        private void OkButton(object parameter)
        {
            if (SelectedProject == null) return;
            RequestClose?.Invoke(true);
        }

        private void CancelButton(object parameter)
        {
            SelectedNode = null;
            IsCancelled = true;
            RequestClose?.Invoke(false);
        }
        private void SearchButton(object parameter)
        {
            string searchTerm = SearchString?.Trim() ?? string.Empty;

            if (string.IsNullOrWhiteSpace(searchTerm))
            {
                foreach (var root in RootNodes)
                {
                    ResetVisibility(root);
                }

                return;
            }
            else
            {
                foreach (var root in RootNodes)
                {
                    FilterNode(root, searchTerm);
                }
            }
        }

        private void ResetVisibility(P6NodeViewModel node)
        {
            node.IsVisible = true;
            node.IsExpanded = true;

            foreach (var child in node.Children)
            {
                ResetVisibility(child);
            }
        }

        private bool FilterNode(P6NodeViewModel node, string filter)
        {
            bool selfMatch = node.Name.IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0;

            bool childMatch = false;

            foreach (var child in node.Children)
            {
                if (FilterNode(child, filter))
                    childMatch = true;
            }

            node.IsVisible = selfMatch || childMatch;

            node.IsExpanded = childMatch;

            return node.IsVisible;
        }

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
                var epsNode = new P6NodeViewModel(P6Instance: eps, onSelectedCallback: NodeSelectedHandler);
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
                var projectNode = new P6NodeViewModel(P6Instance: project, onSelectedCallback: NodeSelectedHandler);

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

        private void NodeSelectedHandler(P6NodeViewModel newlySelectedNode)
        {
            // Update the globally tracked selected node
            SelectedNode = newlySelectedNode;
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