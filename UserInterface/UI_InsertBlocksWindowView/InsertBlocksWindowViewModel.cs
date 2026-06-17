using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Geometry;
using Autocad_Primavera_P6_Plugin.Services.LiteDBService;
using PropertyChanged;
using App = Autodesk.AutoCAD.ApplicationServices.Application;
using Autocad_Primavera_P6_Plugin.Services.P6ApiService;

namespace Autocad_Primavera_P6_Plugin.UserInterface.UI_InsertBlocksWindowView
{
    [AddINotifyPropertyChangedInterface]
    public sealed class InsertBlocksWindowViewModel
    {
        private readonly MyPlugin _pluginInstance;
        private readonly InsertBlocksWindowModel _model;
        private readonly Autodesk.AutoCAD.ApplicationServices.Document _document;
        private bool _isCancelled;
        private string _blocksFolder;
        private const string MoveInfoXPropertyName = "MoveInfo X";
        private const string MoveInfoYPropertyName = "MoveInfo Y";

        public BoundryActivityCodeSectionViewModel BoundaryCode { get; private set; }
        public ActivityCodeSectionViewModel ItemIdCode { get; private set; }
        public Project CurrentProject { get; private set; }
        public ObservableCollection<PredefinedBlockInfo> AvailableBlocks => _model.AvailableBlocks;

        public BlockTypeMode BlockTypeMode { get; set; }
        public PredefinedBlockInfo SelectedPredefinedBlock { get; set; }
        public ObjectId SelectedDrawingBlockId { get; set; }
        public string SelectedDrawingBlockName { get; set; }
        public bool ContinuousInsert { get; set; }
        public bool CopyBlockDefinition { get; set; }
        public bool EditLegendPosition { get; set; }
        public bool IsInserting { get; set; }
        public string InsertButtonText { get; private set; } = "Insert Block";
        public string StatusMessage { get; set; }

        public ICommand InsertBlockCommand { get; private set; }
        public ICommand CancelCommand { get; private set; }
        public ICommand BrowseBlocksFolderCommand { get; private set; }
        public ICommand SelectDrawingBlockCommand { get; private set; }

        public event Action<bool?> RequestClose;

        public InsertBlocksWindowViewModel(MyPlugin pluginInstance)
        {
            _pluginInstance = pluginInstance ?? throw new ArgumentNullException(nameof(pluginInstance));
            _model = new InsertBlocksWindowModel();
            _document = App.DocumentManager.MdiActiveDocument;

            SelectedDrawingBlockId = ObjectId.Null;

            CurrentProject = _pluginInstance.MyP6ApiService.GetP6ProjectFromDWGFile(_document);

            BoundaryCode = new BoundryActivityCodeSectionViewModel(_pluginInstance, CurrentProject);
            ItemIdCode = new ActivityCodeSectionViewModel(_pluginInstance, "Item ID", CurrentProject);

            BlockTypeMode = BlockTypeMode.Predefined;
            SelectedDrawingBlockName = "No drawing block selected.";
            StatusMessage = string.Empty;

            InsertBlockCommand = new RelayCommand(async parameter => await InsertBlockAsync(parameter), _ => !IsInserting);
            CancelCommand = new RelayCommand(Cancel);
            BrowseBlocksFolderCommand = new RelayCommand(BrowseBlocksFolder);
            SelectDrawingBlockCommand = new RelayCommand(parameter => SelectBlockFromDrawing(parameter));

            _blocksFolder = GetDefaultBlocksFolder();
            LoadPredefinedBlocks(_blocksFolder);
        }

        private void OnBlockTypeModeChanged()
        {
            CommandManager.InvalidateRequerySuggested();
        }

        private void OnSelectedPredefinedBlockChanged()
        {
            CommandManager.InvalidateRequerySuggested();
        }

        private void OnSelectedDrawingBlockIdChanged()
        {
            CommandManager.InvalidateRequerySuggested();
        }

        private void OnIsInsertingChanged()
        {
            InsertButtonText = IsInserting ? "Inserting..." : "Insert Block";
            CommandManager.InvalidateRequerySuggested();
        }

        private void LoadPredefinedBlocks(string folder)
        {
            AvailableBlocks.Clear();
            Directory.CreateDirectory(folder);

            foreach (var file in Directory.GetFiles(folder, "*.dwg", SearchOption.TopDirectoryOnly).OrderBy(Path.GetFileNameWithoutExtension))
            {
                AvailableBlocks.Add(new PredefinedBlockInfo
                {
                    Name = Path.GetFileNameWithoutExtension(file),
                    FilePath = file
                });
            }

            SelectedPredefinedBlock = AvailableBlocks.FirstOrDefault();
            StatusMessage = AvailableBlocks.Count == 0
                ? "No predefined .dwg block files found in " + folder
                : "Loaded " + AvailableBlocks.Count + " predefined block file(s).";
        }

        private string GetDefaultBlocksFolder()
        {
            string appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            return Path.Combine(appData, "Autocad_Primavera_P6_Plugin", "Blocks");
        }

        private void BrowseBlocksFolder(object parameter)
        {
            string folder = _blocksFolder;
            if (SelectedPredefinedBlock != null && !string.IsNullOrWhiteSpace(SelectedPredefinedBlock.FilePath))
            {
                folder = Path.GetDirectoryName(SelectedPredefinedBlock.FilePath);
            }

            if (string.IsNullOrWhiteSpace(folder))
            {
                folder = GetDefaultBlocksFolder();
            }

            Directory.CreateDirectory(folder);
            Process.Start("explorer.exe", folder);
            LoadPredefinedBlocks(folder);
        }

        private void SelectBlockFromDrawing(object parameter)
        {
            var owner = parameter as Window;
            var doc = App.DocumentManager.MdiActiveDocument;
            if (doc == null)
            {
                MessageBox.Show("No active AutoCAD document is available.", "Insert Blocks", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var ed = doc.Editor;
            try
            {
                owner?.Hide();

                while (true)
                {
                    var options = new PromptEntityOptions("\nSelect plugin block: ");
                    options.SetRejectMessage("\nSelect a block reference.");
                    options.AddAllowedClass(typeof(BlockReference), true);

                    PromptEntityResult result = ed.GetEntity(options);
                    if (result.Status != PromptStatus.OK)
                    {
                        return;
                    }

                    using (var tr = doc.Database.TransactionManager.StartTransaction())
                    {
                        var blockRef = tr.GetObject(result.ObjectId, OpenMode.ForRead) as BlockReference;
                        if (blockRef == null || !IsValidPluginBlock(blockRef, tr))
                        {
                            ed.WriteMessage("\nNot a valid plugin block.");
                            continue;
                        }

                        ObjectId blockDefinitionId = GetSourceDefinitionId(blockRef);
                        var blockDef = (BlockTableRecord)tr.GetObject(blockDefinitionId, OpenMode.ForRead);
                        SelectedDrawingBlockId = blockDefinitionId;
                        SelectedDrawingBlockName = blockDef.Name;
                        BlockTypeMode = BlockTypeMode.SelectFromDrawing;
                        tr.Commit();
                        return;
                    }
                }
            }
            catch (System.Exception ex)
            {
                StatusMessage = "Failed to select drawing block: " + ex.Message;
                ed.WriteMessage("\n[Plugin] Failed to select drawing block: " + ex.Message);
                MessageBox.Show(StatusMessage, "Insert Blocks", MessageBoxButton.OK, MessageBoxImage.Error);
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

        private async Task InsertBlockAsync(object parameter)
        {
            var owner = parameter as Window;
            var doc = App.DocumentManager.MdiActiveDocument;
            if (doc == null)
            {
                MessageBox.Show("No active AutoCAD document is available.", "Insert Blocks", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var validationFailures = ValidateBeforeP6Resolution();
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

        private List<string> ValidateBeforeP6Resolution()
        {
            var failures = new List<string>();

            if (!BoundaryCode.IsAutoGenerate && IsPlaceholderOrEmpty(BoundaryCode.SelectedCodeValue))
            {
                failures.Add("Select a Boundary activity code or enable auto generate.");
            }

            if (BoundaryCode.IsAutoGenerate && !BoundaryCode.HasResolvedCode && !BoundaryCode.HasAutoGenerateParent)
            {
                failures.Add("Select a Boundary parent code before auto generation.");
            }

            if (!ItemIdCode.IsAutoGenerate && IsPlaceholderOrEmpty(ItemIdCode.SelectedCodeValue))
            {
                failures.Add("Select an Item ID activity code or enable auto generate.");
            }

            if (ItemIdCode.IsAutoGenerate && !ItemIdCode.HasResolvedCode && !ItemIdCode.HasAutoGenerateParent)
            {
                failures.Add("Select an Item ID parent code before auto generation.");
            }

            if (BlockTypeMode == BlockTypeMode.Predefined && SelectedPredefinedBlock == null)
            {
                failures.Add("Select a predefined .dwg block file.");
            }

            if (BlockTypeMode == BlockTypeMode.SelectFromDrawing && !HasValidDrawingBlockSelection())
            {
                failures.Add("Select a valid plugin block from the drawing.");
            }

            return failures;
        }

        private List<string> ValidateAfterP6Resolution()
        {
            var failures = new List<string>();

            if (!BoundaryCode.HasResolvedCode)
            {
                failures.Add("Boundary activity code is not resolved. " + BoundaryCode.Info);
            }

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

        private bool HasValidDrawingBlockSelection()
        {
            return !SelectedDrawingBlockId.IsNull && SelectedDrawingBlockId.IsValid;
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

                    bool boundaryReady = await BoundaryCode.EnsureResolvedAsync().ConfigureAwait(true);
                    if (boundaryReady && BoundaryCode.IsAutoGenerate)
                    {
                        BoundaryCode.CommitAutoGeneratedAsSelected();
                    }

                    bool itemReady = await ItemIdCode.EnsureResolvedAsync(forceRegenerate: true).ConfigureAwait(true);
                    var resolutionFailures = ValidateAfterP6Resolution();

                    if (!boundaryReady || !itemReady || resolutionFailures.Count > 0)
                    {
                        MessageBox.Show(string.Join(Environment.NewLine, resolutionFailures), "Insert Blocks", MessageBoxButton.OK, MessageBoxImage.Warning);
                        break;
                    }

                    ObjectId insertedBlockId;
                    string elementId;
                    string blockName;

                    using (doc.LockDocument())
                    {
                        var source = ResolveBlockSourceForInsert(doc.Database);
                        blockName = source.BlockName;
                        insertedBlockId = InsertBlockReference(doc.Database, source.BlockDefinitionId, blockName, pointResult.Value, out elementId);
                    }

                    if (EditLegendPosition)
                    {
                        EditLegendForBlock(doc, insertedBlockId);
                    }

                    ed.WriteMessage("\n[Plugin] Inserted " + elementId +
                                    " | Boundary: " + BoundaryCode.ResolvedCodeValue +
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

        private BlockSourceResult ResolveBlockSourceForInsert(Database db)
        {
            if (BlockTypeMode == BlockTypeMode.Predefined)
            {
                return CopyBlockDefinition
                    ? ResolveCopiedPredefinedBlockSource(db)
                    : ResolvePredefinedBlockSource(db);
            }

            var source = ResolveDrawingBlockSource(db);
            return CopyBlockDefinition ? CopyBlockDefinitionToUniqueRecord(db, source.BlockDefinitionId) : source;
        }

        private BlockSourceResult ResolveDrawingBlockSource(Database db)
        {
            using (var tr = db.TransactionManager.StartTransaction())
            {
                var blockDef = (BlockTableRecord)tr.GetObject(SelectedDrawingBlockId, OpenMode.ForRead);
                var source = new BlockSourceResult
                {
                    BlockDefinitionId = SelectedDrawingBlockId,
                    BlockName = blockDef.Name
                };
                tr.Commit();
                return source;
            }
        }

        private BlockSourceResult ResolvePredefinedBlockSource(Database db)
        {
            string blockName = SelectedPredefinedBlock.Name;

            using (var tr = db.TransactionManager.StartTransaction())
            {
                var blockTable = (BlockTable)tr.GetObject(db.BlockTableId, OpenMode.ForRead);
                if (blockTable.Has(blockName))
                {
                    var result = new BlockSourceResult
                    {
                        BlockDefinitionId = blockTable[blockName],
                        BlockName = blockName
                    };
                    tr.Commit();
                    return result;
                }
            }

            using (var sourceDb = new Database(false, true))
            {
                sourceDb.ReadDwgFile(SelectedPredefinedBlock.FilePath, FileOpenMode.OpenForReadAndAllShare, true, null);
                ObjectId importedId = db.Insert(blockName, sourceDb, false);
                return new BlockSourceResult
                {
                    BlockDefinitionId = importedId,
                    BlockName = blockName
                };
            }
        }

        private BlockSourceResult ResolveCopiedPredefinedBlockSource(Database db)
        {
            string blockName = MakeUniqueBlockName(db, SelectedPredefinedBlock.Name);

            using (var sourceDb = new Database(false, true))
            {
                sourceDb.ReadDwgFile(SelectedPredefinedBlock.FilePath, FileOpenMode.OpenForReadAndAllShare, true, null);
                ObjectId importedId = db.Insert(blockName, sourceDb, false);
                return new BlockSourceResult
                {
                    BlockDefinitionId = importedId,
                    BlockName = blockName
                };
            }
        }

        private BlockSourceResult CopyBlockDefinitionToUniqueRecord(Database db, ObjectId sourceBlockDefinitionId)
        {
            using (var tr = db.TransactionManager.StartTransaction())
            {
                var sourceBtr = (BlockTableRecord)tr.GetObject(sourceBlockDefinitionId, OpenMode.ForRead);
                var blockTable = (BlockTable)tr.GetObject(db.BlockTableId, OpenMode.ForWrite);
                string newName = MakeUniqueBlockName(blockTable, sourceBtr.Name);

                var newBtr = new BlockTableRecord { Name = newName };
                CopyBlockDefinitionProperties(sourceBtr, newBtr);

                ObjectId newBtrId = blockTable.Add(newBtr);
                tr.AddNewlyCreatedDBObject(newBtr, true);

                var cloneIds = new ObjectIdCollection();
                foreach (ObjectId entityId in sourceBtr)
                {
                    cloneIds.Add(entityId);
                }

                var idMapping = new IdMapping();
                db.DeepCloneObjects(cloneIds, newBtrId, idMapping, false);

                tr.Commit();
                return new BlockSourceResult
                {
                    BlockDefinitionId = newBtrId,
                    BlockName = newName
                };
            }
        }

        private ObjectId GetSourceDefinitionId(BlockReference blockRef)
        {
            if (blockRef.IsDynamicBlock && !blockRef.DynamicBlockTableRecord.IsNull)
            {
                return blockRef.DynamicBlockTableRecord;
            }

            return blockRef.BlockTableRecord;
        }

        private void CopyBlockDefinitionProperties(BlockTableRecord sourceBtr, BlockTableRecord newBtr)
        {
            newBtr.Origin = sourceBtr.Origin;
            newBtr.Units = sourceBtr.Units;
            newBtr.BlockScaling = sourceBtr.BlockScaling;
            newBtr.Explodable = sourceBtr.Explodable;
            newBtr.Comments = sourceBtr.Comments;
        }

        private string MakeUniqueBlockName(Database db, string originalName)
        {
            using (var tr = db.TransactionManager.StartTransaction())
            {
                var blockTable = (BlockTable)tr.GetObject(db.BlockTableId, OpenMode.ForRead);
                string uniqueName = MakeUniqueBlockName(blockTable, originalName);
                tr.Commit();
                return uniqueName;
            }
        }

        private string MakeUniqueBlockName(BlockTable blockTable, string originalName)
        {
            string safeBaseName = NormalizeBlockName(originalName);
            string candidate;
            do
            {
                candidate = "P6_" + safeBaseName + "_" + Guid.NewGuid().ToString("N").Substring(0, 8).ToUpperInvariant();
            }
            while (blockTable.Has(candidate));

            return candidate;
        }

        private string NormalizeBlockName(string blockName)
        {
            string safeBaseName = string.IsNullOrWhiteSpace(blockName) ? "PluginBlock" : blockName.Trim('*').Trim();
            var invalidCharacters = Path.GetInvalidFileNameChars()
                .Concat(new[] { ';', '=', ',', '`' })
                .Distinct();

            foreach (char invalidCharacter in invalidCharacters)
            {
                safeBaseName = safeBaseName.Replace(invalidCharacter, '_');
            }

            if (safeBaseName.Length > 180)
            {
                safeBaseName = safeBaseName.Substring(0, 180);
            }

            return string.IsNullOrWhiteSpace(safeBaseName) ? "PluginBlock" : safeBaseName;
        }

        private ObjectId InsertBlockReference(Database db, ObjectId blockDefId, string blockName, Point3d insertionPoint, out string elementId)
        {
            elementId = Guid.NewGuid().ToString("N").Substring(0, 8).ToUpperInvariant();

            using (var tr = db.TransactionManager.StartTransaction())
            {
                var modelSpace = (BlockTableRecord)tr.GetObject(SymbolUtilityServices.GetBlockModelSpaceId(db), OpenMode.ForWrite);
                var blockRef = new BlockReference(insertionPoint, blockDefId);
                modelSpace.AppendEntity(blockRef);
                tr.AddNewlyCreatedDBObject(blockRef, true);

                var blockDef = (BlockTableRecord)tr.GetObject(blockDefId, OpenMode.ForRead);
                foreach (ObjectId id in blockDef)
                {
                    var attDef = tr.GetObject(id, OpenMode.ForRead) as AttributeDefinition;
                    if (attDef == null || attDef.Constant)
                    {
                        continue;
                    }

                    var attRef = new AttributeReference();
                    attRef.SetAttributeFromBlock(attDef, blockRef.BlockTransform);
                    blockRef.AttributeCollection.AppendAttribute(attRef);
                    tr.AddNewlyCreatedDBObject(attRef, true);
                }

                var values = BuildAttributeValues(elementId, blockName);
                foreach (var pair in values)
                {
                    SetAttributeValue(blockRef, pair.Key, pair.Value, tr);
                }

                ObjectId insertedId = blockRef.ObjectId;
                tr.Commit();
                return insertedId;
            }
        }

        private Dictionary<string, string> BuildAttributeValues(string elementId, string blockName)
        {
            return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                { "ELEMENT_ID", elementId },
                { "BLOCK_TYPE", blockName },
                { "ATT_1_NAME", "BOUNDARY_CODE_VALUE" },
                { "ATT_1_VALUE", BoundaryCode.ResolvedCodeValue },
                { "ATT_2_NAME", "BOUNDARY_CODE_PATH" },
                { "ATT_2_VALUE", BoundaryCode.ResolvedCodePath },
                { "ATT_3_NAME", "BOUNDARY_CODE_ID" },
                { "ATT_3_VALUE", BoundaryCode.ResolvedCodeValueId },
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

        private void SetAttributeValue(BlockReference blockRef, string tag, string value, Transaction tr)
        {
            foreach (ObjectId attributeId in blockRef.AttributeCollection)
            {
                var attribute = tr.GetObject(attributeId, OpenMode.ForWrite) as AttributeReference;
                if (attribute != null && string.Equals(attribute.Tag, tag, StringComparison.OrdinalIgnoreCase))
                {
                    attribute.TextString = value ?? string.Empty;
                    return;
                }
            }

            var created = new AttributeReference
            {
                Tag = tag,
                TextString = value ?? string.Empty,
                Position = blockRef.Position,
                Height = 1.0,
                Invisible = true
            };
            blockRef.AttributeCollection.AppendAttribute(created);
            tr.AddNewlyCreatedDBObject(created, true);
        }

        private bool IsValidPluginBlock(BlockReference blockRef, Transaction tr)
        {
            foreach (ObjectId attributeId in blockRef.AttributeCollection)
            {
                var attribute = tr.GetObject(attributeId, OpenMode.ForRead) as AttributeReference;
                if (attribute != null &&
                    string.Equals(attribute.Tag, "Attribute_Check_string", StringComparison.OrdinalIgnoreCase) &&
                    string.Equals(attribute.TextString, "FoundOK", StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
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

                if (string.Equals(property.PropertyName, MoveInfoXPropertyName, StringComparison.OrdinalIgnoreCase))
                {
                    xProperty = property;
                }
                else if (string.Equals(property.PropertyName, MoveInfoYPropertyName, StringComparison.OrdinalIgnoreCase))
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
