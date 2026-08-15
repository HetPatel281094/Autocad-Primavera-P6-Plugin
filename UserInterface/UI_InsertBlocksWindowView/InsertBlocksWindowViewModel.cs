using Autocad_Primavera_P6_Plugin.Services.AutocadService;
using Autocad_Primavera_P6_Plugin.Services.P6ApiService;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using AcadApp = Autodesk.AutoCAD.ApplicationServices.Application;
using AcadDoc = Autodesk.AutoCAD.ApplicationServices.Document;

namespace Autocad_Primavera_P6_Plugin.UserInterface.UI_InsertBlocksWindowView
{
    public partial class InsertBlocksWindowViewModel : ObservableObject
    {
        private readonly MyPlugin _pluginInstance;
        private P6ApiService _p6ApiService => _pluginInstance.MyP6ApiService;
        public AutocadService _autocadService => _pluginInstance.MyAutocadService;
        public AcadDoc _currentAcadDoc;
        public Project _currentProject;
        public PlugInBlockReference _selectedPluginBlockRef;


        private bool _isInsertCancelled;

        public BoundaryActivityCodeSectionViewModel BoundaryCode { get; private set; }
        public ElementIdCodeSectionViewModel ElementIdCode { get; private set; }
        public ACadBlockSectionViewModel ACadBlockVM { get; private set; }

        public bool ContinuousInsert { get; set; }
        public bool EditLegendPosition { get; set; }
        public bool IsInserting { get; set; }
        public string InsertButtonText { get; private set; } = "Insert Block";
        public string StatusMessage { get; set; }

        public event Action<bool?> RequestClose;

        public InsertBlocksWindowViewModel(MyPlugin pluginInstance)
        {
            _pluginInstance = pluginInstance;
        }

        public async Task Async_Init()
        {
            // Set Current Drawing
            _currentAcadDoc = AcadApp.DocumentManager.MdiActiveDocument;


            // Set Project of the Drawing
            _currentProject = await _p6ApiService.GetP6ProjectFromDWGFile(_currentAcadDoc);


            // Get Selected Blockreferences
            var editor = _currentAcadDoc.Editor;
            var database = _currentAcadDoc.Database;
            var impliedSelected = editor.SelectImplied();
            var impliedSelectedObjects = impliedSelected.Status == PromptStatus.OK ? impliedSelected.Value : null;
            List<BlockReference> impliedSelectedBlockRefs = new();

            if (impliedSelectedObjects != null || impliedSelectedObjects.Count > 0)
            {
                var selectedBlockRefs = impliedSelectedObjects
                    .Cast<SelectedObject>()
                    .Where(obj => obj != null && obj.ObjectId.ObjectClass.DxfName == "INSERT");

                if (selectedBlockRefs != null && selectedBlockRefs.Count() > 0)
                {
                    using (Transaction tr = database.TransactionManager.StartTransaction())
                    {
                        impliedSelectedBlockRefs = selectedBlockRefs
                            .Select(
                                obj => (BlockReference)tr.GetObject(obj.ObjectId, OpenMode.ForRead)
                            )
                            .Where(
                                blockRef => PluginBlockRefAutocadHelpers.IsValidPluginBlock(
                                    PluginBlockRefAutocadHelpers.Get_AttRefDict(blockRef, tr)
                                )
                            )
                            .ToList();
                    }
                }
            }

            if (impliedSelectedBlockRefs.Count > 0)
            {
                using (var tr = database.TransactionManager.StartTransaction())
                {
                    var lastSelectedBlockRef = impliedSelectedBlockRefs.Last();
                    _selectedPluginBlockRef = new PlugInBlockReference(_pluginInstance, _currentAcadDoc, lastSelectedBlockRef, tr);
                }
            }
            else
            {
                editor.WriteMessage("\n No Plugin Block References were Selected. \n");
            }


            var preSelectBoundaryCode = _selectedPluginBlockRef != null ? await _selectedPluginBlockRef?.Get_BdryActCode() : null;
            var preSelectElementIdCode = _selectedPluginBlockRef != null ? await _selectedPluginBlockRef?.Get_ElementIdCode() : null;
            var preselectedAcadBlock = _selectedPluginBlockRef != null ? _selectedPluginBlockRef.AcadBlockRef : null;

            BoundaryCode = new BoundaryActivityCodeSectionViewModel(_pluginInstance, _currentProject, preSelectBoundaryCode);
            await BoundaryCode.Async_Init();

            ElementIdCode = new ElementIdCodeSectionViewModel(_pluginInstance, _currentProject, preSelectElementIdCode);
            await ElementIdCode.Async_Init();

            ACadBlockVM = new ACadBlockSectionViewModel(_currentAcadDoc, preselectedAcadBlock);
            await ACadBlockVM.Async_Init();

            StatusMessage = string.Empty;
        }

        [RelayCommand]
        private async Task InsertBlockAsync(object parameter)
        {
            var owner = parameter as Window;
            var doc = _currentAcadDoc;

            IsInserting = true;
            _isInsertCancelled = false;

            ElementIdCode.IsAutoGenerateLoop = true;

            try
            {
                await RunInsertLoopAsync(owner, doc).ConfigureAwait(true);
            }
            catch (System.Exception ex)
            {
                ElementIdCode.IsAutoGenerateLoop = false;

                StatusMessage = "Failed before block insertion: " + ex.Message;
                MessageBox.Show(StatusMessage, "Insert Blocks", MessageBoxButton.OK, MessageBoxImage.Error);
                doc.Editor.WriteMessage("\n[Plugin] Failed before block insertion: " + ex.Message);
            }
            finally
            {
                ElementIdCode.IsAutoGenerateLoop = false;

                IsInserting = false;
            }
        }

        private async Task RunInsertLoopAsync(Window owner, Autodesk.AutoCAD.ApplicationServices.Document doc)
        {
            var ed = doc.Editor;

            try
            {
                owner?.Hide();

                do
                {
                    if (_isInsertCancelled) { break; }
                    ;

                    PromptPointResult pointResult = ed.GetPoint("\nPick insertion point: ");
                    if (pointResult.Status != PromptStatus.OK || _isInsertCancelled) { break; }
                    ;

                    // Must insert block or fail with error prompt.
                    if (BoundaryCode.IsAutoGenerate) { await BoundaryCode.AutoGenerateActivityCodeAsync(); }
                    ;
                    if (ElementIdCode.IsAutoGenerate) { await ElementIdCode.AutoGenerateActivityCodeAsync(); }
                    ;
                    if (ACadBlockVM.IsCopyBlockDefinition) { ACadBlockVM.GenerateNewBTR(); }
                    ;

                    if (BoundaryCode.IsAutoGenerate || ACadBlockVM.IsCopyBlockDefinition)
                    {
                        throw new InvalidOperationException("Failed to turn autogenerate off for required activity codes or blockTableRecord.");
                    }
                    ;

                    var resolvedBoundaryCode = BoundaryCode.SelectedActivityCode;
                    var resolvedElementIdCode = ElementIdCode.SelectedActivityCode;
                    var resolvedBTRRecord = ACadBlockVM.SelectedBlockTableRecord;

                    if (resolvedBoundaryCode == null || resolvedElementIdCode == null || resolvedBTRRecord == null)
                    {
                        throw new InvalidOperationException("Failed to resolve required activity codes or blockTableRecord.");
                    }
                    ;

                    PlugInBlockReference newPluginBlockRef;

                    using (doc.LockDocument())
                    using (var tr = doc.TransactionManager.StartTransaction())
                    {
                        var pluginBlockRef = new PlugInBlockReference(_pluginInstance, doc, resolvedBTRRecord, tr);

                        var SetPositionResult = pluginBlockRef.Set_BlockPosition(pointResult.Value, out _);

                        if (!SetPositionResult) { throw new InvalidOperationException("Failed to set activity-code values to plugin block reference."); }
                        ;

                        var isBdrySet = pluginBlockRef.Set_BdryActCode(resolvedBoundaryCode, out _);
                        var isElementIdSet = pluginBlockRef.Set_ElementIdCode(resolvedElementIdCode, out _);

                        // Checks if the activity codes were successfully set on the plugin block reference.
                        if (!isBdrySet || !isElementIdSet) { throw new InvalidOperationException("Failed to set activity-code values to plugin block reference."); }
                        ;

                        var insertedPluginRef = pluginBlockRef.InsertAsNewBlockRef(tr);

                        tr.Commit();

                        newPluginBlockRef = insertedPluginRef;

                    }
                    ;

                    if (EditLegendPosition)
                    {
                        var pointOptions = new PromptPointOptions("\nPick MoveInfo point or press Esc to keep current value: ")
                        {
                            UseBasePoint = true,
                            BasePoint = newPluginBlockRef.Get_BlockPosition().Value
                        };

                        PromptPointResult pointResultMoveInfo = ed.GetPoint(pointOptions);

                        if (pointResultMoveInfo.Status == PromptStatus.OK)
                        {
                            using (doc.LockDocument())
                            using (var tr = doc.TransactionManager.StartTransaction())
                            {
                                newPluginBlockRef.Set_InfoPosition(pointResultMoveInfo.Value, out _, tr);
                                tr.Commit();
                            }
                            ;

                        }
                        ;

                    }
                    ;

                    ed.WriteMessage("\n[Plugin] Inserted " + newPluginBlockRef.Get_ElementId() +
                        " | Boundary: " + BoundaryCode.SelectedCodeValue +
                        " | ItemId: " + ElementIdCode.SelectedCodeValue + "\n");

                }
                while (ContinuousInsert && !_isInsertCancelled);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"An error occurred during block insertion:\n{ex.Message}", "Insert Blocks Error", MessageBoxButton.OK, MessageBoxImage.Error);
                ed.WriteMessage($"\n[Plugin] Error during insertion: {ex.Message}\n");
                Debug.Print("[Plugin] Error during insertion: " + ex.ToString());
            }
            finally
            {
                if (owner != null)
                {
                    owner.Show();
                    owner.Activate();
                }
            }
        }

        [RelayCommand]
        private void Cancel(object parameter)
        {
            _isInsertCancelled = true;
            RequestClose?.Invoke(false);
        }

    }
}
