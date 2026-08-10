using Autocad_Primavera_P6_Plugin.Services.AutocadService;
using Autodesk.AutoCAD.EditorInput;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System;
using System.Diagnostics;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;

namespace Autocad_Primavera_P6_Plugin.UserInterface.UI_InsertBlocksWindowView
{
    public partial class InsertBlocksWindowViewModel : ObservableObject
    {
        private InsertBlocksWindowModel _model;
        private bool _isCancelled;

        public BoundaryActivityCodeSectionViewModel BoundaryCode { get; private set; }
        public ElementIdCodeSectionViewModel ElementIdCode { get; private set; }
        public ACadBlockSectionViewModel ACadBlockVM { get; private set; }

        public bool ContinuousInsert { get; set; }
        public bool EditLegendPosition { get; set; }
        public bool IsInserting { get; set; }
        public string InsertButtonText { get; private set; } = "Insert Block";
        public string StatusMessage { get; set; }
        public ICommand CancelCommand { get; private set; }

        public event Action<bool?> RequestClose;

        public InsertBlocksWindowViewModel(MyPlugin pluginInstance)
        {
            _model = new InsertBlocksWindowModel { PluginInstance = pluginInstance };
        }

        public async Task Async_Init()
        {
            await _model.Async_Init();

            var preSelectBoundaryCode = _model.PreselectedBlock != null ? await _model.PreselectedBlock?.Get_BdryActCode() : null;
            var preSelectElementIdCode = _model.PreselectedBlock != null ? await _model.PreselectedBlock?.Get_ElementIdCode() : null;
            var preselectedAcadBlock = _model.PreselectedBlock != null ? _model.PreselectedBlock.AcadBlockRef : null;

            BoundaryCode = new BoundaryActivityCodeSectionViewModel(_model.PluginInstance, _model.CurrentProject, preSelectBoundaryCode);
            await BoundaryCode.Async_Init();

            ElementIdCode = new ElementIdCodeSectionViewModel(_model.PluginInstance, _model.CurrentProject, preSelectElementIdCode);
            await ElementIdCode.Async_Init();

            ACadBlockVM = new ACadBlockSectionViewModel(_model.ActAcadDoc, preselectedAcadBlock);
            await ACadBlockVM.Async_Init();

            StatusMessage = string.Empty;

            CancelCommand = new RelayCommand(Cancel);
        }

        [RelayCommand]
        private async Task InsertBlockAsync(object parameter)
        {
            var owner = parameter as Window;
            var doc = _model.ActAcadDoc;

            IsInserting = true;
            _isCancelled = false;

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
                    if (_isCancelled) { break; }
                    ;

                    PromptPointResult pointResult = ed.GetPoint("\nPick insertion point: ");
                    if (pointResult.Status != PromptStatus.OK || _isCancelled) { break; }
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
                        var pluginBlockRef = new PlugInBlockReference(_model.PluginInstance, doc, resolvedBTRRecord, tr);

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
                while (ContinuousInsert && !_isCancelled);
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

        private void Cancel(object parameter)
        {
            _isCancelled = true;
            RequestClose?.Invoke(false);
        }

    }
}
