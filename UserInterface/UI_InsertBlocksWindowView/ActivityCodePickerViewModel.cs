using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Input;
using Autocad_Primavera_P6_Plugin.Services.P6ApiService;
using PropertyChanged;

namespace Autocad_Primavera_P6_Plugin.UserInterface.UI_InsertBlocksWindowView
{
    [AddINotifyPropertyChangedInterface]
    public sealed class ActivityCodePickerViewModel
    {
        private readonly P6ApiService _p6ApiService;
        private readonly bool _isAutoGenerate;

        public ObservableCollection<ActivityCodePickerNodeViewModel> RootNodes { get; private set; }
        public ActivityCodePickerNodeViewModel SelectedNode { get; set; }
        public string Title { get; private set; }
        public string InstructionText { get; private set; }
        public string SearchString { get; set; }
        public string StatusMessage { get; private set; }
        public bool IsLoading { get; private set; }
        public bool IsSelectionValid => SelectedNode != null && (_isAutoGenerate || SelectedNode.IsCode);

        public event Action<bool?> RequestClose;

        public ICommand SearchButtonCommand { get; private set; }
        public ICommand OkButtonCommand { get; private set; }
        public ICommand CancelButtonCommand { get; private set; }

        public ActivityCodePickerViewModel(MyPlugin pluginInstance, string sectionLabel, bool isAutoGenerate)
        {
            if (pluginInstance == null)
            {
                throw new ArgumentNullException(nameof(pluginInstance));
            }

            _p6ApiService = pluginInstance.MyP6ApiService;
            _isAutoGenerate = isAutoGenerate;
            RootNodes = new ObservableCollection<ActivityCodePickerNodeViewModel>();
            SearchString = string.Empty;
            Title = isAutoGenerate ? "Select " + sectionLabel + " Parent Code" : "Select " + sectionLabel + " Code";
            InstructionText = isAutoGenerate
                ? "Select a code type or existing parent value to generate under."
                : "Select an existing code value to assign.";

            SearchButtonCommand = new RelayCommand(SearchButton);
            OkButtonCommand = new RelayCommand(OkButton, _ => IsSelectionValid);
            CancelButtonCommand = new RelayCommand(_ => RequestClose?.Invoke(false));

            _ = LoadAsync();
        }

        public async Task LoadAsync()
        {
            try
            {
                IsLoading = true;
                StatusMessage = "Loading activity codes...";

                if (_p6ApiService == null || _p6ApiService.Client == null)
                {
                    StatusMessage = "P6 is not connected.";
                    return;
                }

                const string typeFields = "ObjectId,Name,Scope,SequenceNumber,Length";
                const string codeFields = "ObjectId,CodeTypeObjectId,CodeTypeName,CodeTypeScope,CodeValue,CodeConcatName,Description,ParentObjectId,SequenceNumber";

                var client = _p6ApiService.Client;
                var types = await client.GetActivityCodeTypesAsync(null, typeFields, "SequenceNumber", null).ConfigureAwait(true);
                var codes = await client.GetActivityCodesAsync(null, codeFields, "SequenceNumber", null).ConfigureAwait(true);

                BuildTree(types ?? new List<ActivityCodeType>(), codes ?? new List<ActivityCode>());
                StatusMessage = RootNodes.Count == 0 ? "No activity codes found." : string.Empty;
            }
            catch (Exception ex)
            {
                StatusMessage = "Unable to load activity codes.";
                Debug.Print(ex.ToString());
            }
            finally
            {
                IsLoading = false;
            }
        }

        private void BuildTree(IEnumerable<ActivityCodeType> codeTypes, IEnumerable<ActivityCode> codes)
        {
            RootNodes.Clear();
            SelectedNode = null;

            var nodesByCodeId = new Dictionary<int, ActivityCodePickerNodeViewModel>();
            var pendingByType = codes
                .Where(c => c != null)
                .GroupBy(c => c.CodeTypeObjectId)
                .ToDictionary(g => g.Key, g => g.OrderBy(c => c.SequenceNumber ?? int.MaxValue).ThenBy(c => c.CodeValue).ToList());

            foreach (var codeType in codeTypes.Where(t => t != null).OrderBy(t => t.SequenceNumber ?? int.MaxValue).ThenBy(t => t.Name))
            {
                var root = new ActivityCodePickerNodeViewModel(codeType, NodeSelectedHandler);
                RootNodes.Add(root);

                if (!codeType.ObjectId.HasValue || !pendingByType.TryGetValue(codeType.ObjectId.Value, out var typeCodes))
                {
                    continue;
                }

                foreach (var code in typeCodes.Where(c => c.ObjectId.HasValue))
                {
                    var node = new ActivityCodePickerNodeViewModel(code, null, NodeSelectedHandler);
                    nodesByCodeId[code.ObjectId.Value] = node;
                }

                foreach (var code in typeCodes.Where(c => c.ObjectId.HasValue))
                {
                    var node = nodesByCodeId[code.ObjectId.Value];
                    AttachCodeNode(node, root, nodesByCodeId);
                }
            }
        }

        private void AttachCodeNode(
            ActivityCodePickerNodeViewModel node,
            ActivityCodePickerNodeViewModel root,
            Dictionary<int, ActivityCodePickerNodeViewModel> nodesByCodeId)
        {
            if (node.Parent != null)
            {
                return;
            }

            var code = node.Code;
            ActivityCodePickerNodeViewModel parent = null;
            if (code.ParentObjectId.HasValue && nodesByCodeId.TryGetValue(code.ParentObjectId.Value, out parent))
            {
                AttachCodeNode(parent, root, nodesByCodeId);
                node.AttachTo(parent);
                return;
            }

            node.AttachTo(root);
        }

        private void SearchButton(object parameter)
        {
            string searchTerm = SearchString == null ? string.Empty : SearchString.Trim();
            foreach (var root in RootNodes)
            {
                if (string.IsNullOrWhiteSpace(searchTerm))
                {
                    ResetVisibility(root);
                }
                else
                {
                    FilterNode(root, searchTerm);
                }
            }
        }

        private void ResetVisibility(ActivityCodePickerNodeViewModel node)
        {
            node.IsVisible = true;
            node.IsExpanded = true;
            foreach (var child in node.Children)
            {
                ResetVisibility(child);
            }
        }

        private bool FilterNode(ActivityCodePickerNodeViewModel node, string filter)
        {
            bool selfMatch = (node.Name ?? string.Empty).IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0 ||
                             (node.Description ?? string.Empty).IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0;

            bool childMatch = false;
            foreach (var child in node.Children)
            {
                if (FilterNode(child, filter))
                {
                    childMatch = true;
                }
            }

            node.IsVisible = selfMatch || childMatch;
            node.IsExpanded = childMatch || selfMatch;
            return node.IsVisible;
        }

        private void OkButton(object parameter)
        {
            if (!IsSelectionValid)
            {
                return;
            }

            RequestClose?.Invoke(true);
        }

        private void NodeSelectedHandler(ActivityCodePickerNodeViewModel newlySelectedNode)
        {
            SelectedNode = newlySelectedNode;
            CommandManager.InvalidateRequerySuggested();
        }

        public void OnSelectedNodeChanged()
        {
            CommandManager.InvalidateRequerySuggested();
        }
    }
}
