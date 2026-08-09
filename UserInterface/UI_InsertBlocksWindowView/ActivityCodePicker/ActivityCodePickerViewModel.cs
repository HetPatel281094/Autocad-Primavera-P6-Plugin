using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Input;
using Autocad_Primavera_P6_Plugin.Services.LiteDBService;
using Autocad_Primavera_P6_Plugin.Services.P6ApiService;
using PropertyChanged;

namespace Autocad_Primavera_P6_Plugin.UserInterface.UI_InsertBlocksWindowView.ActivityCodePicker
{
    [AddINotifyPropertyChangedInterface]
    public sealed class ActivityCodePickerViewModel
    {
        private readonly ActivityCodePickerModel _model;

        public ObservableCollection<ActivityCodePickerNodeViewModel> RootNodes { get; private set; }
        public ActivityCodePickerNodeViewModel SelectedNode { get; set; }
        public string Title { get; private set; }
        public string InstructionText { get; private set; }
        public string SearchString { get; set; }
        public string StatusMessage { get; private set; }
        public bool IsLoading { get; private set; }
        public bool IsSelectionValid => SelectedNode != null && (_model.IsAutoGenerate || SelectedNode.IsCode);

        public event Action<bool?> RequestClose;

        public ICommand SearchButtonCommand { get; private set; }
        public ICommand OkButtonCommand { get; private set; }
        public ICommand CancelButtonCommand { get; private set; }

        public ActivityCodePickerViewModel(ActivityCodePickerModel model)
        {
            _model = model ?? throw new ArgumentNullException(nameof(model));

            RootNodes = new ObservableCollection<ActivityCodePickerNodeViewModel>();
            SearchString = string.Empty;

            Title = _model.IsAutoGenerate ? "Select Parent Code" : "Select Code";
            InstructionText = _model.IsAutoGenerate
                ? "Select a code type or existing parent value to generate under."
                : "Select an existing code value to assign.";

            SearchButtonCommand = new RelayCommand(SearchButton);
            OkButtonCommand = new RelayCommand(OkButton, _ => IsSelectionValid);
            CancelButtonCommand = new RelayCommand(_ => RequestClose?.Invoke(false));

            var types = new List<ActivityCodeType>() { _model.SelectionActCodeType };
            var codes = _model.SelectionActCodes;
            BuildTree(types ?? new List<ActivityCodeType>(), codes ?? new List<ActivityCode>());
        }

        public async Task AsyncInit(Project project = null, List<string> typesObjIds = null, List<ActivityCodeType> types = null, List<ActivityCode> codes = null)
        {
            var projectObjectId = project?.ObjectId.ToString();

            List<ActivityCodeType> resolvedTypes;
            List<ActivityCode> resolvedCodes;

            try
            {
                if (types != null && types.Count > 0)
                {
                    resolvedTypes = types;
                }
                else
                {
                    var istypesObjIds = typesObjIds != null && typesObjIds.Count > 0;
                    var typesObjectIdsStrings = istypesObjIds ? typesObjIds.Select(str => $"(ObjectId :eq: {str})") : null;
                    var typesObjectIdsString = typesObjectIdsStrings != null ? string.Join(" :or: ", typesObjectIdsStrings) : "";

                    var filter = "";
                    filter += projectObjectId != null ? $"((ProjectObjectId :eq: '{projectObjectId}') :and: (Scope :eq: 'Project'))" : "";
                    filter += filter.Length > 0 && !string.IsNullOrWhiteSpace(typesObjectIdsString) ? " :and: " : "";
                    filter += !string.IsNullOrWhiteSpace(typesObjectIdsString) ? $"({typesObjectIdsString})" : "";
                    var fields = "ObjectId,Name,Description,Scope,SequenceNumber";
                    var fetchedtypes = await _model.PluginInstance.MyP6ApiService.Client.GetActivityCodeTypesAsync(filter, fields, null, null);
                    resolvedTypes = fetchedtypes.ToList();
                }

                if (codes != null && codes.Count > 0)
                {
                    resolvedCodes = codes;
                }
                else
                {
                    var istypesObjIds = typesObjIds != null && typesObjIds.Count > 0;
                    var typesObjectIdsStrings = istypesObjIds ? typesObjIds.Select(str => $"(CodeTypeObjectId :eq: {str})") : null;
                    var typesObjectIdsString = typesObjectIdsStrings != null ? string.Join(" :or: ", typesObjectIdsStrings) : "";

                    string filter = "";
                    filter += projectObjectId != null ? $"(ProjectObjectId :eq: '{projectObjectId}')" : "";
                    filter += filter.Length > 0 && !string.IsNullOrWhiteSpace(typesObjectIdsString) ? " :and: " : "";
                    filter += !string.IsNullOrWhiteSpace(typesObjectIdsString) ? $"({typesObjectIdsString})" : "";
                    var fields = "CodeConcatName, CodeTypeName, CodeTypeObjectId, CodeTypeScope, CodeValue, Color, CreateDate, CreateUser, Description, LastUpdateDate, LastUpdateUser, ObjectId, ParentObjectId, ProjectObjectId, SequenceNumber";
                    var fetchedCodes = await _model.PluginInstance.MyP6ApiService.Client.GetActivityCodesAsync(filter, fields, null, null);
                    resolvedCodes = fetchedCodes.ToList();
                };

                BuildTree(resolvedTypes ?? new List<ActivityCodeType>(), resolvedCodes ?? new List<ActivityCode>());
            }
            catch (Exception e)
            {
                Debug.Print(e.ToString());
            };
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

            try
            {
                var x = codeTypes.Where(t => t != null).OrderBy(t => t.SequenceNumber ?? int.MaxValue).ThenBy(t => t.Name);
            }
            catch(Exception e)
            {
                Debug.Print("Break Point");
            };



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

        /// <summary>
        /// Searches the hierarchy starting from RootNodes to find a node wrapped around an ActivityCode with the specified ObjectId.
        /// </summary>
        /// <param name="actCode">The ObjectId of the ActivityCode to search for.</param>
        /// <returns>The matching <see cref="ActivityCodePickerNodeViewModel"/>, or <c>null</c> if not found.</returns>
        public ActivityCodePickerNodeViewModel GetNodeByActivityCode(ActivityCode actCode)
        {
            foreach (var rootNode in RootNodes)
            {
                var match = FindNodeByCodeObjectIdRecursive(rootNode, actCode.ObjectId.Value);
                if (match != null)
                {
                    return match;
                }
            }

            return null;
        }

        private ActivityCodePickerNodeViewModel FindNodeByCodeObjectIdRecursive(
            ActivityCodePickerNodeViewModel currentNode,
            int targetObjectId)
        {
            if (currentNode == null) return null;

            // Check if current node is a code and has a matching ObjectId
            if (currentNode.IsCode && currentNode.Code != null && currentNode.Code.ObjectId == targetObjectId)
            {
                return currentNode;
            }

            // Traverse through child nodes recursively
            if (currentNode.Children != null)
            {
                foreach (var childNode in currentNode.Children)
                {
                    var found = FindNodeByCodeObjectIdRecursive(childNode, targetObjectId);
                    if (found != null)
                    {
                        return found;
                    }
                }
            }

            return null;
        }


    }
}
