using Autocad_Primavera_P6_Plugin.Services.P6ApiService;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;

namespace Autocad_Primavera_P6_Plugin.UserInterface.UI_InsertBlocksWindowView.ActivityCodePicker
{
    public partial class ActivityCodePickerViewModel : ObservableObject
    {
        private readonly ActivityCodePickerModel _model;

        #region Observable Properties

        [ObservableProperty]
        private ObservableCollection<ActivityCodePickerNodeViewModel> rootNodes;

        [ObservableProperty]
        [NotifyCanExecuteChangedFor(nameof(OkButtonCommand))]
        private ActivityCodePickerNodeViewModel selectedNode;

        [ObservableProperty]
        private string title;

        [ObservableProperty]
        private string instructionText;

        [ObservableProperty]
        private string searchString;

        [ObservableProperty]
        private string statusMessage;

        [ObservableProperty]
        private bool isLoading;

        #endregion


        #region Computed Properties

        public bool IsSelectionValid =>
            SelectedNode != null &&
            (_model.IsAutoGenerate || SelectedNode.IsCode);

        #endregion


        #region Events

        public event Action<bool?> RequestClose;

        #endregion


        #region Constructor

        public ActivityCodePickerViewModel(ActivityCodePickerModel model)
        {
            _model = model ?? throw new ArgumentNullException(nameof(model));

            RootNodes = new ObservableCollection<ActivityCodePickerNodeViewModel>();

            SearchString = string.Empty;

            Title = _model.IsAutoGenerate
                ? "Select Parent Code"
                : "Select Code";

            InstructionText = _model.IsAutoGenerate
                ? "Select a code type or existing parent value to generate under."
                : "Select an existing code value to assign.";

            var types = new List<ActivityCodeType>
            {
                _model.SelectionActCodeType
            };

            var codes = _model.SelectionActCodes;

            BuildTree(
                types ?? new List<ActivityCodeType>(),
                codes ?? new List<ActivityCode>());
        }

        #endregion


        #region Initialization

        public async Task AsyncInit(
            Project project = null,
            List<string> typesObjIds = null,
            List<ActivityCodeType> types = null,
            List<ActivityCode> codes = null)
        {
            var projectObjectId = project?.ObjectId.ToString();

            List<ActivityCodeType> resolvedTypes;
            List<ActivityCode> resolvedCodes;

            try
            {
                IsLoading = true;
                StatusMessage = "Loading activity codes...";

                /*
                 * Resolve Activity Code Types
                 */

                if (types != null && types.Count > 0)
                {
                    resolvedTypes = types;
                }
                else
                {
                    var isTypesObjIds =
                        typesObjIds != null &&
                        typesObjIds.Count > 0;

                    var typesObjectIdsStrings = isTypesObjIds
                        ? typesObjIds.Select(
                            str => $"(ObjectId :eq: {str})")
                        : null;

                    var typesObjectIdsString =
                        typesObjectIdsStrings != null
                            ? string.Join(
                                " :or: ",
                                typesObjectIdsStrings)
                            : string.Empty;

                    var filter = string.Empty;

                    if (projectObjectId != null)
                    {
                        filter +=
                            $"((ProjectObjectId :eq: '{projectObjectId}') :and: (Scope :eq: 'Project'))";
                    }

                    if (filter.Length > 0 &&
                        !string.IsNullOrWhiteSpace(typesObjectIdsString))
                    {
                        filter += " :and: ";
                    }

                    if (!string.IsNullOrWhiteSpace(typesObjectIdsString))
                    {
                        filter += $"({typesObjectIdsString})";
                    }

                    var fields =
                        "ObjectId,Name,Description,Scope,SequenceNumber";

                    var fetchedTypes =
                        await _model
                            .PluginInstance
                            .MyP6ApiService
                            .Client
                            .GetActivityCodeTypesAsync(
                                filter,
                                fields,
                                null,
                                null);

                    resolvedTypes = fetchedTypes.ToList();
                }


                /*
                 * Resolve Activity Codes
                 */

                if (codes != null && codes.Count > 0)
                {
                    resolvedCodes = codes;
                }
                else
                {
                    var isTypesObjIds =
                        typesObjIds != null &&
                        typesObjIds.Count > 0;

                    var typesObjectIdsStrings = isTypesObjIds
                        ? typesObjIds.Select(
                            str => $"(CodeTypeObjectId :eq: {str})")
                        : null;

                    var typesObjectIdsString =
                        typesObjectIdsStrings != null
                            ? string.Join(
                                " :or: ",
                                typesObjectIdsStrings)
                            : string.Empty;

                    var filter = string.Empty;

                    if (projectObjectId != null)
                    {
                        filter +=
                            $"(ProjectObjectId :eq: '{projectObjectId}')";
                    }

                    if (filter.Length > 0 &&
                        !string.IsNullOrWhiteSpace(typesObjectIdsString))
                    {
                        filter += " :and: ";
                    }

                    if (!string.IsNullOrWhiteSpace(typesObjectIdsString))
                    {
                        filter += $"({typesObjectIdsString})";
                    }

                    var fields =
                        "CodeConcatName, CodeTypeName, CodeTypeObjectId, " +
                        "CodeTypeScope, CodeValue, Color, CreateDate, " +
                        "CreateUser, Description, LastUpdateDate, " +
                        "LastUpdateUser, ObjectId, ParentObjectId, " +
                        "ProjectObjectId, SequenceNumber";

                    var fetchedCodes =
                        await _model
                            .PluginInstance
                            .MyP6ApiService
                            .Client
                            .GetActivityCodesAsync(
                                filter,
                                fields,
                                null,
                                null);

                    resolvedCodes = fetchedCodes.ToList();
                }

                BuildTree(
                    resolvedTypes ??
                    new List<ActivityCodeType>(),

                    resolvedCodes ??
                    new List<ActivityCode>());
            }
            catch (Exception e)
            {
                Debug.Print(e.ToString());

                StatusMessage = e.Message;
            }
            finally
            {
                IsLoading = false;
            }
        }

        #endregion


        #region Tree Construction

        private void BuildTree(
            IEnumerable<ActivityCodeType> codeTypes,
            IEnumerable<ActivityCode> codes)
        {
            RootNodes.Clear();

            SelectedNode = null;

            var nodesByCodeId =
                new Dictionary<int, ActivityCodePickerNodeViewModel>();

            var pendingByType =
                codes
                    .Where(c => c != null)
                    .GroupBy(c => c.CodeTypeObjectId)
                    .ToDictionary(
                        g => g.Key,
                        g => g
                            .OrderBy(
                                c => c.SequenceNumber ?? int.MaxValue)
                            .ThenBy(c => c.CodeValue)
                            .ToList());

            try
            {
                var x =
                    codeTypes
                        .Where(t => t != null)
                        .OrderBy(
                            t => t.SequenceNumber ?? int.MaxValue)
                        .ThenBy(t => t.Name);
            }
            catch (Exception e)
            {
                Debug.Print("Break Point");
                Debug.Print(e.ToString());
            }


            foreach (
                var codeType in
                codeTypes
                    .Where(t => t != null)
                    .OrderBy(
                        t => t.SequenceNumber ?? int.MaxValue)
                    .ThenBy(t => t.Name))
            {
                var root =
                    new ActivityCodePickerNodeViewModel(
                        codeType,
                        NodeSelectedHandler);

                RootNodes.Add(root);

                if (!codeType.ObjectId.HasValue ||
                    !pendingByType.TryGetValue(
                        codeType.ObjectId.Value,
                        out var typeCodes))
                {
                    continue;
                }


                /*
                 * Create all nodes first.
                 */

                foreach (
                    var code in
                    typeCodes.Where(c => c.ObjectId.HasValue))
                {
                    var node =
                        new ActivityCodePickerNodeViewModel(
                            code,
                            null,
                            NodeSelectedHandler);

                    nodesByCodeId[code.ObjectId.Value] = node;
                }


                /*
                 * Attach nodes to their parents.
                 */

                foreach (
                    var code in
                    typeCodes.Where(c => c.ObjectId.HasValue))
                {
                    var node =
                        nodesByCodeId[code.ObjectId.Value];

                    AttachCodeNode(
                        node,
                        root,
                        nodesByCodeId);
                }
            }
        }


        private void AttachCodeNode(
            ActivityCodePickerNodeViewModel node,
            ActivityCodePickerNodeViewModel root,
            Dictionary<int, ActivityCodePickerNodeViewModel> nodesByCodeId)
        {
            /*
             * Already attached.
             */
            if (node.Parent != null)
            {
                return;
            }

            var code = node.Code;

            ActivityCodePickerNodeViewModel parent = null;

            /*
             * Attach to actual parent.
             */
            if (code.ParentObjectId.HasValue &&
                nodesByCodeId.TryGetValue(
                    code.ParentObjectId.Value,
                    out parent))
            {
                AttachCodeNode(
                    parent,
                    root,
                    nodesByCodeId);

                node.AttachTo(parent);

                return;
            }

            /*
             * No parent means it belongs directly
             * under the Activity Code Type.
             */
            node.AttachTo(root);
        }

        #endregion


        #region Search

        [RelayCommand]
        private void SearchButton(object parameter)
        {
            var searchTerm =
                SearchString == null
                    ? string.Empty
                    : SearchString.Trim();

            foreach (var root in RootNodes)
            {
                if (string.IsNullOrWhiteSpace(searchTerm))
                {
                    ResetVisibility(root);
                }
                else
                {
                    FilterNode(
                        root,
                        searchTerm);
                }
            }
        }


        private void ResetVisibility(
            ActivityCodePickerNodeViewModel node)
        {
            node.IsVisible = true;
            node.IsExpanded = true;

            foreach (var child in node.Children)
            {
                ResetVisibility(child);
            }
        }


        private bool FilterNode(
            ActivityCodePickerNodeViewModel node,
            string filter)
        {
            var selfMatch =
                (node.Name ?? string.Empty)
                    .IndexOf(
                        filter,
                        StringComparison.OrdinalIgnoreCase) >= 0

                ||

                (node.Description ?? string.Empty)
                    .IndexOf(
                        filter,
                        StringComparison.OrdinalIgnoreCase) >= 0;

            var childMatch = false;

            foreach (var child in node.Children)
            {
                if (FilterNode(child, filter))
                {
                    childMatch = true;
                }
            }

            node.IsVisible =
                selfMatch ||
                childMatch;

            node.IsExpanded =
                childMatch ||
                selfMatch;

            return node.IsVisible;
        }

        #endregion


        #region Commands

        private bool CanExecuteOkButton()
        {
            return IsSelectionValid;
        }


        [RelayCommand(CanExecute = nameof(CanExecuteOkButton))]
        private void OkButton(object parameter)
        {
            if (!IsSelectionValid)
            {
                return;
            }

            RequestClose?.Invoke(true);
        }


        [RelayCommand]
        private void CancelButton(object parameter)
        {
            RequestClose?.Invoke(false);
        }

        #endregion


        #region Selection

        private void NodeSelectedHandler(
            ActivityCodePickerNodeViewModel newlySelectedNode)
        {
            SelectedNode = newlySelectedNode;

            /*
             * Normally this is not required with MVVM Toolkit because
             * [NotifyCanExecuteChangedFor] automatically notifies
             * OkButtonCommand when SelectedNode changes.
             */
        }

        #endregion


        #region Activity Code Lookup

        /// <summary>
        /// Searches the hierarchy starting from RootNodes to find
        /// a node wrapped around an ActivityCode with the specified
        /// ObjectId.
        /// </summary>
        /// <param name="actCode">
        /// The ObjectId of the ActivityCode to search for.
        /// </param>
        /// <returns>
        /// The matching ActivityCodePickerNodeViewModel,
        /// or null if not found.
        /// </returns>
        public ActivityCodePickerNodeViewModel GetNodeByActivityCode(
            ActivityCode actCode)
        {
            if (actCode == null ||
                !actCode.ObjectId.HasValue)
            {
                return null;
            }

            foreach (var rootNode in RootNodes)
            {
                var match =
                    FindNodeByCodeObjectIdRecursive(
                        rootNode,
                        actCode.ObjectId.Value);

                if (match != null)
                {
                    return match;
                }
            }

            return null;
        }


        private ActivityCodePickerNodeViewModel
            FindNodeByCodeObjectIdRecursive(
                ActivityCodePickerNodeViewModel currentNode,
                int targetObjectId)
        {
            if (currentNode == null)
            {
                return null;
            }

            /*
             * Check if current node is a code and has
             * a matching ObjectId.
             */
            if (currentNode.IsCode &&
                currentNode.Code != null &&
                currentNode.Code.ObjectId == targetObjectId)
            {
                return currentNode;
            }

            /*
             * Traverse child nodes recursively.
             */
            if (currentNode.Children != null)
            {
                foreach (var childNode in currentNode.Children)
                {
                    var found =
                        FindNodeByCodeObjectIdRecursive(
                            childNode,
                            targetObjectId);

                    if (found != null)
                    {
                        return found;
                    }
                }
            }

            return null;
        }

        #endregion
    }
}