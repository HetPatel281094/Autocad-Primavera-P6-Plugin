using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using Autocad_Primavera_P6_Plugin.Services.AutocadService;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Geometry;
using PropertyChanged;
using acadAppService = Autodesk.AutoCAD.ApplicationServices;

namespace Autocad_Primavera_P6_Plugin.UserInterface.UI_InsertBlocksWindowView
{
    [AddINotifyPropertyChangedInterface]
    public sealed class InsertBlocksWindowViewModel
    {
        private InsertBlocksWindowModel _model;
        private bool _isCancelled;

        public BoundaryActivityCodeSectionViewModel BoundaryCode { get; private set; }
        public ElementIdCodeSectionViewModel ItemIdCode { get; private set; }
        public ACadBlockSectionViewModel ACadBlockVM { get; private set; }

        public bool ContinuousInsert { get; set; }
        public bool EditLegendPosition { get; set; }
        public bool IsInserting { get; set; }
        public string InsertButtonText { get; private set; } = "Insert Block";
        public string StatusMessage { get; set; }

        public ICommand InsertBlockCommand { get; private set; }
        public ICommand CancelCommand { get; private set; }

        public event Action<bool?> RequestClose;

        public InsertBlocksWindowViewModel(MyPlugin pluginInstance)
        {
            _model = new InsertBlocksWindowModel{ PluginInstance = pluginInstance };
            _model.Init();
        }

        public async Task Async_Init()
        {
            BoundaryCode = new BoundaryActivityCodeSectionViewModel(_model.PluginInstance, _model.CurrentProject);
            await BoundaryCode.Async_Init();

            ItemIdCode = new ElementIdCodeSectionViewModel(_model.PluginInstance, _model.CurrentProject);
            await ItemIdCode.Async_Init();

            ACadBlockVM = new ACadBlockSectionViewModel(_model.ActAcadDoc);
            await ACadBlockVM.Async_Init();

            StatusMessage = string.Empty;

            InsertBlockCommand = new RelayCommand(async parameter => await InsertBlockAsync(parameter), _ => !IsInserting);
            CancelCommand = new RelayCommand(Cancel);
        }

        private void OnIsInsertingChanged()
        {
            InsertButtonText = IsInserting ? "Inserting..." : "Insert Block";
            CommandManager.InvalidateRequerySuggested();
        }

        private async Task InsertBlockAsync(object parameter)
        {
            var owner = parameter as Window;
            var doc = _model.ActAcadDoc;

            var validationFailures = ValidateBeforeP6Resolution(doc);

            if (validationFailures.Count > 0)
            {
                MessageBox.Show(string.Join(Environment.NewLine, validationFailures), "Insert Blocks", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            IsInserting = true;
            _isCancelled = false;

            try
            {
                await RunInsertLoopAsync(owner, doc).ConfigureAwait(true);
            }
            catch (System.Exception ex)
            {
                StatusMessage = "Failed before block insertion: " + ex.Message;
                MessageBox.Show(StatusMessage, "Insert Blocks", MessageBoxButton.OK, MessageBoxImage.Error);
                doc.Editor.WriteMessage("\n[Plugin] Failed before block insertion: " + ex.Message);
            }
            finally
            {
                IsInserting = false;
            }
        }

        private List<string> ValidateBeforeP6Resolution(acadAppService.Document doc)
        {
            var failures = new List<string>();

            if (ACadBlockVM == null)
            {
                failures.Add("Block selection is not initialized.");
                return failures;
            }

            if (!BoundaryCode.IsAutoGenerate && IsPlaceholderOrEmpty(BoundaryCode.SelectedCodeValue))
            {
                failures.Add("Select a Boundary activity code or enable auto generate.");
            }

            //if (BoundaryCode.IsAutoGenerate && !BoundaryCode.HasResolvedCode && !BoundaryCode.HasAutoGenerateParent)
            //{
            //    failures.Add("Select a Boundary parent code before auto generation.");
            //}

            if (!ItemIdCode.IsAutoGenerate && IsPlaceholderOrEmpty(ItemIdCode.SelectedCodeValue))
            {
                failures.Add("Select an Item ID activity code or enable auto generate.");
            }

            if (ItemIdCode.IsAutoGenerate && !ItemIdCode.HasResolvedCode && !ItemIdCode.HasAutoGenerateParent)
            {
                failures.Add("Select an Item ID parent code before auto generation.");
            }

            if (ACadBlockVM.BlockTypeMode == BlockTypeMode.Predefined && ACadBlockVM.SelectedPredefinedBlock == null)
            {
                failures.Add("Select a predefined .dwg block file.");
            }

            if (ACadBlockVM.BlockTypeMode == BlockTypeMode.SelectFromDrawing && !ACadBlockVM.HasValidDrawingBlockSelection())
            {
                failures.Add("Select a valid plugin block from the drawing.");
            }

            if (doc == null)
            {
                failures.Add("No active AutoCAD document is available.");
            }

            return failures;
        }

        private List<string> ValidateAfterP6Resolution()
        {
            var failures = new List<string>();

            //if (!BoundaryCode.HasResolvedCode)
            //{
            //    failures.Add("Boundary activity code is not resolved. " + BoundaryCode.Info);
            //}

            if (!ItemIdCode.HasResolvedCode)
            {
                failures.Add("Item ID activity code is not resolved. " + ItemIdCode.Info);
            }

            return failures;
        }

        private bool IsPlaceholderOrEmpty(string value)
        {
            return string.IsNullOrWhiteSpace(value) || value.StartsWith("--", StringComparison.Ordinal);
        }

        private async Task RunInsertLoopAsync(Window owner, Autodesk.AutoCAD.ApplicationServices.Document doc)
        {
            var ed = doc.Editor;

            try
            {
                owner?.Hide();

                do
                {
                    if (_isCancelled)
                    {
                        break;
                    }

                    PromptPointResult pointResult = ed.GetPoint("\nPick insertion point: ");
                    if (pointResult.Status != PromptStatus.OK || _isCancelled)
                    {
                        break;
                    }

                    // Start Here
                    var pluginBlockRef = new PlugInBlockReference(_model.PluginInstance,doc);
                    var SetPositionResult = pluginBlockRef.Set_BlockPosition(pointResult.Value, out _);
                    // Ends Here

                    //bool boundaryReady = await BoundaryCode.EnsureResolvedAsync().ConfigureAwait(true);
                    //if (boundaryReady && BoundaryCode.IsAutoGenerate)
                    //{
                    //    BoundaryCode.CommitAutoGeneratedAsSelected();
                    //}

                    bool itemReady = await ItemIdCode.EnsureResolvedAsync(forceRegenerate: true).ConfigureAwait(true);

                    var resolutionFailures = ValidateAfterP6Resolution();

                    //if (!boundaryReady || !itemReady || resolutionFailures.Count > 0)
                    //{
                    //    MessageBox.Show(string.Join(Environment.NewLine, resolutionFailures), "Insert Blocks", MessageBoxButton.OK, MessageBoxImage.Warning);
                    //    break;
                    //}

                    // Start Here
                    if (!pluginBlockRef.Set_BdryActCode(BoundaryCode.SelectedActivityCode, out _))
                    {
                        throw new InvalidOperationException("Failed to prepare Boundary activity-code values.");
                    }

                    if (!pluginBlockRef.Set_ElementIdCode(ItemIdCode.ResolvedActivityCode, out _))
                    {
                        throw new InvalidOperationException("Failed to prepare Item ID activity-code values.");
                    }
                    // Ends Here

                    ObjectId insertedBlockId;
                    string elementId;
                    string blockName;

                    using (doc.LockDocument())
                    {
                        var source = ACadBlockVM.ResolveBlockSourceForInsert(doc.Database);
                        blockName = source.BlockName;
                        insertedBlockId = InsertBlockReference(doc.Database, source.BlockDefinitionId, blockName, pointResult.Value, out elementId);
                    }

                    if (EditLegendPosition)
                    {
                        EditLegendForBlock(doc, insertedBlockId);
                    }

                    ed.WriteMessage("\n[Plugin] Inserted " + elementId +
                                    " | Boundary: " + BoundaryCode.SelectedCodeValue +
                                    " | ItemId: " + ItemIdCode.ResolvedCodeValue);
                }
                while (ContinuousInsert && !_isCancelled);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"An error occurred during block insertion:\n{ex.Message}", "Insert Blocks Error", MessageBoxButton.OK, MessageBoxImage.Error);
                ed.WriteMessage($"\n[Plugin] Error during insertion: {ex.Message}\n");
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

        private ObjectId InsertBlockReference(Database db, ObjectId blockDefId, string blockName, Point3d insertionPoint, out string elementId)
        {
            elementId = Guid.NewGuid().ToString("N").Substring(0, 8).ToUpperInvariant();

            //using (var tr = db.TransactionManager.StartTransaction())
            //{
            //    var blockDef = (BlockTableRecord)tr.GetObject(blockDefId, OpenMode.ForRead);
            //    var pluginBlock = new PlugInBlockReference(_model.ActAcadDoc);
            //    //pluginBlock.AttachBlockTableRecord(blockDef, tr);
            //    var values = BuildAttributeValues(elementId, blockName);
            //    IList<string> missingTags;
            //    ObjectId insertedId = pluginBlock.CreateBlockReference(
            //        SymbolUtilityServices.GetBlockModelSpaceId(db),
            //        insertionPoint,
            //        values,
            //        tr,
            //        out missingTags);

            //    string[] requiredTags = { "ELEMENT_ID", "BLOCK_TYPE", "Attribute_Check_string" };
            //    var missingRequiredTags = missingTags
            //        .Where(tag => requiredTags.Any(required => string.Equals(required, tag, StringComparison.OrdinalIgnoreCase)))
            //        .ToList();
            //    if (missingRequiredTags.Count > 0)
            //    {
            //        throw new InvalidOperationException(
            //            "The selected block definition is not a plugin block. Missing required attribute definition(s): " +
            //            string.Join(", ", missingRequiredTags));
            //    }

            //    if (missingTags.Count > 0)
            //    {
            //        Debug.Print("[Plugin] Block was inserted without optional attributes: " + string.Join(", ", missingTags));
            //    }

            //    tr.Commit();
            //    return insertedId;
            //}

            return ObjectId.Null;
        }

        private Dictionary<string, string> BuildAttributeValues(string elementId, string blockName)
        {
            return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                { "ELEMENT_ID", elementId },
                { "BLOCK_TYPE", blockName },
                { "ATT_1_NAME", "BOUNDARY_CODE_VALUE" },
                { "ATT_1_VALUE", BoundaryCode.SelectedCodeValue },
                { "ATT_2_NAME", "BOUNDARY_CODE_PATH" },
                { "ATT_2_VALUE", BoundaryCode.SelectedCodePath },
                { "ATT_3_NAME", "BOUNDARY_CODE_ID" },
                { "ATT_3_VALUE", BoundaryCode.SelectedCodeValueId },
                { "ATT_4_NAME", "ITEM_ID_CODE_VALUE" },
                { "ATT_4_VALUE", ItemIdCode.ResolvedCodeValue },
                { "ATT_5_NAME", "ITEM_ID_CODE_PATH" },
                { "ATT_5_VALUE", ItemIdCode.ResolvedCodePath },
                { "ATT_6_NAME", "ITEM_ID_CODE_ID" },
                { "ATT_6_VALUE", ItemIdCode.ResolvedCodeValueId },
                { "ATT_7_NAME", "Attribute_Check_string" },
                { "ATT_7_VALUE", "FoundOK" },
                { "ATT_8_NAME", string.Empty },
                { "ATT_8_VALUE", string.Empty },
                { "ATT_9_NAME", string.Empty },
                { "ATT_9_VALUE", string.Empty },
                { "ATT_10_NAME", string.Empty },
                { "ATT_10_VALUE", string.Empty },
                { "Attribute_Check_string", "FoundOK" }
            };
        }

        private void EditLegendForBlock(Autodesk.AutoCAD.ApplicationServices.Document doc, ObjectId blockRefId)
        {
            var ed = doc.Editor;
            Point3d blockBasePoint;

            using (doc.LockDocument())
            using (var tr = doc.Database.TransactionManager.StartTransaction())
            {
                var blockRef = tr.GetObject(blockRefId, OpenMode.ForRead) as BlockReference;
                if (blockRef == null || !HasWritableMoveInfoProperties(blockRef))
                {
                    tr.Commit();
                    return;
                }

                blockBasePoint = blockRef.Position;
                tr.Commit();
            }

            var pointOptions = new PromptPointOptions("\nPick MoveInfo point or press Esc to keep current value: ")
            {
                UseBasePoint = true,
                BasePoint = blockBasePoint
            };

            PromptPointResult pointResult = ed.GetPoint(pointOptions);
            if (pointResult.Status != PromptStatus.OK)
            {
                return;
            }

            using (doc.LockDocument())
            using (var tr = doc.Database.TransactionManager.StartTransaction())
            {
                var blockRef = tr.GetObject(blockRefId, OpenMode.ForWrite) as BlockReference;
                if (blockRef == null)
                {
                    return;
                }

                if (SetMoveInfoDynamicProperty(blockRef, pointResult.Value - blockBasePoint))
                {
                    tr.Commit();
                }
            }
        }

        private bool HasWritableMoveInfoProperties(BlockReference blockRef)
        {
            DynamicBlockReferenceProperty xProperty;
            DynamicBlockReferenceProperty yProperty;
            return TryGetWritableMoveInfoProperties(blockRef, out xProperty, out yProperty);
        }

        private bool SetMoveInfoDynamicProperty(BlockReference blockRef, Vector3d relativeValue)
        {
            DynamicBlockReferenceProperty xProperty;
            DynamicBlockReferenceProperty yProperty;

            if (!TryGetWritableMoveInfoProperties(blockRef, out xProperty, out yProperty))
            {
                return false;
            }

            xProperty.Value = relativeValue.X;
            yProperty.Value = relativeValue.Y;
            return true;
        }

        private bool TryGetWritableMoveInfoProperties(
            BlockReference blockRef,
            out DynamicBlockReferenceProperty xProperty,
            out DynamicBlockReferenceProperty yProperty)
        {
            xProperty = null;
            yProperty = null;

            if (!blockRef.IsDynamicBlock)
            {
                return false;
            }

            foreach (DynamicBlockReferenceProperty property in blockRef.DynamicBlockReferencePropertyCollection)
            {
                if (property.ReadOnly)
                {
                    continue;
                }

                if (string.Equals(property.PropertyName, _model.MoveInfoXPropertyName, StringComparison.OrdinalIgnoreCase))
                {
                    xProperty = property;
                }
                else if (string.Equals(property.PropertyName, _model.MoveInfoYPropertyName, StringComparison.OrdinalIgnoreCase))
                {
                    yProperty = property;
                }
            }

            return xProperty != null && yProperty != null;
        }

        private void Cancel(object parameter)
        {
            _isCancelled = true;
            RequestClose?.Invoke(false);
        }
        
    }
}
