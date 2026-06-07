using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Geometry;
using App = Autodesk.AutoCAD.ApplicationServices.Application;
using WinForms = System.Windows.Forms;

namespace Autocad_Primavera_P6_Plugin.UserInterface.UI_InsertBlocksWindowView
{
    public sealed class InsertBlocksWindowViewModel : System.ComponentModel.INotifyPropertyChanged
    {
        private readonly MyPlugin _pluginInstance;
        private readonly InsertBlocksWindowModel _model;
        private readonly Document _document;
        private bool _isCancelled;
        private BlockTypeMode _blockTypeMode;
        private PredefinedBlockInfo _selectedPredefinedBlock;
        private ObjectId _selectedDrawingBlockId;
        private string _selectedDrawingBlockName;
        private bool _copyBlockDefinition;
        private bool _continuousInsert;
        private bool _editLegendPosition;
        private bool _isInserting;
        private string _statusMessage;

        public ActivityCodeSectionViewModel BoundaryCode { get; private set; }
        public ActivityCodeSectionViewModel ItemIdCode { get; private set; }
        public ObservableCollection<PredefinedBlockInfo> AvailableBlocks => _model.AvailableBlocks;

        public BlockTypeMode BlockTypeMode
        {
            get => _blockTypeMode;
            set
            {
                _blockTypeMode = value;
                OnPropertyChanged(nameof(BlockTypeMode));
                CommandManager.InvalidateRequerySuggested();
            }
        }

        public PredefinedBlockInfo SelectedPredefinedBlock
        {
            get => _selectedPredefinedBlock;
            set
            {
                _selectedPredefinedBlock = value;
                OnPropertyChanged(nameof(SelectedPredefinedBlock));
                CommandManager.InvalidateRequerySuggested();
            }
        }

        public ObjectId SelectedDrawingBlockId
        {
            get => _selectedDrawingBlockId;
            set
            {
                _selectedDrawingBlockId = value;
                OnPropertyChanged(nameof(SelectedDrawingBlockId));
                CommandManager.InvalidateRequerySuggested();
            }
        }

        public string SelectedDrawingBlockName
        {
            get => _selectedDrawingBlockName;
            set
            {
                _selectedDrawingBlockName = value;
                OnPropertyChanged(nameof(SelectedDrawingBlockName));
            }
        }

        public bool ContinuousInsert
        {
            get => _continuousInsert;
            set
            {
                _continuousInsert = value;
                OnPropertyChanged(nameof(ContinuousInsert));
            }
        }

        public bool CopyBlockDefinition
        {
            get => _copyBlockDefinition;
            set
            {
                _copyBlockDefinition = value;
                OnPropertyChanged(nameof(CopyBlockDefinition));
            }
        }

        public bool EditLegendPosition
        {
            get => _editLegendPosition;
            set
            {
                _editLegendPosition = value;
                OnPropertyChanged(nameof(EditLegendPosition));
            }
        }

        public bool IsInserting
        {
            get => _isInserting;
            set
            {
                _isInserting = value;
                OnPropertyChanged(nameof(IsInserting));
                OnPropertyChanged(nameof(InsertButtonText));
                CommandManager.InvalidateRequerySuggested();
            }
        }

        public string InsertButtonText => IsInserting ? "Inserting..." : "Insert Block";

        public string StatusMessage
        {
            get => _statusMessage;
            set
            {
                _statusMessage = value;
                OnPropertyChanged(nameof(StatusMessage));
            }
        }

        public ICommand InsertBlockCommand { get; private set; }
        public ICommand CancelCommand { get; private set; }
        public ICommand BrowseBlocksFolderCommand { get; private set; }
        public ICommand SelectDrawingBlockCommand { get; private set; }

        public event Action<bool?> RequestClose;
        public event System.ComponentModel.PropertyChangedEventHandler PropertyChanged;

        public InsertBlocksWindowViewModel(MyPlugin pluginInstance)
        {
            _pluginInstance = pluginInstance ?? throw new ArgumentNullException(nameof(pluginInstance));
            _model = new InsertBlocksWindowModel();
            _document = App.DocumentManager.MdiActiveDocument;
            _selectedDrawingBlockId = ObjectId.Null;

            BoundaryCode = new ActivityCodeSectionViewModel(_pluginInstance, "Boundary");
            ItemIdCode = new ActivityCodeSectionViewModel(_pluginInstance, "Item ID");
            BlockTypeMode = BlockTypeMode.Predefined;
            SelectedDrawingBlockName = "No drawing block selected.";
            StatusMessage = string.Empty;

            InsertBlockCommand = new RelayCommand(async parameter => await InsertBlockAsync(parameter), _ => !IsInserting);
            CancelCommand = new RelayCommand(Cancel);
            BrowseBlocksFolderCommand = new RelayCommand(BrowseBlocksFolder);
            SelectDrawingBlockCommand = new RelayCommand(parameter => SelectBlockFromDrawing(parameter));

            InitViewModel();
        }

        private void InitViewModel()
        {
            LoadPredefinedBlocks(GetDefaultBlocksFolder());
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
            using (var dialog = new WinForms.FolderBrowserDialog())
            {
                dialog.Description = "Select folder containing .dwg block files";
                dialog.SelectedPath = Directory.Exists(GetDefaultBlocksFolder()) ? GetDefaultBlocksFolder() : string.Empty;
                if (dialog.ShowDialog() == WinForms.DialogResult.OK)
                {
                    LoadPredefinedBlocks(dialog.SelectedPath);
                }
            }
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

                        var blockDef = (BlockTableRecord)tr.GetObject(blockRef.BlockTableRecord, OpenMode.ForRead);
                        SelectedDrawingBlockId = blockRef.BlockTableRecord;
                        SelectedDrawingBlockName = blockDef.Name;
                        BlockTypeMode = BlockTypeMode.SelectFromDrawing;
                        tr.Commit();
                        return;
                    }
                }
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
                bool boundaryReady = await BoundaryCode.EnsureResolvedAsync().ConfigureAwait(true);
                bool itemReady = await ItemIdCode.EnsureResolvedAsync().ConfigureAwait(true);
                validationFailures = ValidateAfterP6Resolution();

                if (!boundaryReady || !itemReady || validationFailures.Count > 0)
                {
                    MessageBox.Show(string.Join(Environment.NewLine, validationFailures), "Insert Blocks", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                await RunInsertLoopAsync(owner, doc).ConfigureAwait(true);
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

        private Task RunInsertLoopAsync(Window owner, Document doc)
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
            finally
            {
                if (owner != null)
                {
                    owner.Show();
                    owner.Activate();
                }
            }

            return Task.FromResult(true);
        }

        private BlockSourceResult ResolveBlockSourceForInsert(Database db)
        {
            BlockSourceResult source;

            if (BlockTypeMode == BlockTypeMode.Predefined)
            {
                source = ResolvePredefinedBlockSource(db);
            }
            else
            {
                using (var tr = db.TransactionManager.StartTransaction())
                {
                    var blockDef = (BlockTableRecord)tr.GetObject(SelectedDrawingBlockId, OpenMode.ForRead);
                    source = new BlockSourceResult
                    {
                        BlockDefinitionId = SelectedDrawingBlockId,
                        BlockName = blockDef.Name
                    };
                    tr.Commit();
                }
            }

            return CopyBlockDefinition ? CopyBlockDefinitionToUniqueRecord(db, source.BlockDefinitionId) : source;
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

        private BlockSourceResult CopyBlockDefinitionToUniqueRecord(Database db, ObjectId sourceBlockDefinitionId)
        {
            using (var tr = db.TransactionManager.StartTransaction())
            {
                var sourceBtr = (BlockTableRecord)tr.GetObject(sourceBlockDefinitionId, OpenMode.ForRead);
                var blockTable = (BlockTable)tr.GetObject(db.BlockTableId, OpenMode.ForWrite);
                string newName = MakeUniqueBlockName(blockTable, sourceBtr.Name);

                var newBtr = new BlockTableRecord { Name = newName };
                ObjectId newBtrId = blockTable.Add(newBtr);
                tr.AddNewlyCreatedDBObject(newBtr, true);

                var cloneIds = new ObjectIdCollection();
                foreach (ObjectId entityId in sourceBtr)
                {
                    cloneIds.Add(entityId);
                }

                var idMapping = new IdMapping();
                db.WblockCloneObjects(cloneIds, newBtrId, idMapping, DuplicateRecordCloning.Ignore, false);

                tr.Commit();
                return new BlockSourceResult
                {
                    BlockDefinitionId = newBtrId,
                    BlockName = newName
                };
            }
        }

        private string MakeUniqueBlockName(BlockTable blockTable, string originalName)
        {
            string safeBaseName = string.IsNullOrWhiteSpace(originalName) ? "PluginBlock" : originalName.Trim('*');
            string candidate;
            do
            {
                candidate = safeBaseName + "_" + Guid.NewGuid().ToString("N").Substring(0, 8).ToUpperInvariant();
            }
            while (blockTable.Has(candidate));

            return candidate;
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

        private void EditLegendForBlock(Document doc, ObjectId blockRefId)
        {
            var ed = doc.Editor;
            PromptPointResult crossResult = ed.GetPoint("\nPick legend cross anchor: ");
            if (crossResult.Status != PromptStatus.OK)
            {
                return;
            }

            var textOptions = new PromptPointOptions("\nPick legend text anchor: ")
            {
                UseBasePoint = true,
                BasePoint = crossResult.Value
            };

            PromptPointResult textResult = ed.GetPoint(textOptions);
            if (textResult.Status != PromptStatus.OK)
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

                Vector3d delta = textResult.Value - crossResult.Value;
                foreach (ObjectId attributeId in blockRef.AttributeCollection)
                {
                    var attribute = tr.GetObject(attributeId, OpenMode.ForWrite) as AttributeReference;
                    if (attribute != null && !attribute.Invisible)
                    {
                        attribute.Position = attribute.Position + delta;
                    }
                }

                var modelSpace = (BlockTableRecord)tr.GetObject(SymbolUtilityServices.GetBlockModelSpaceId(doc.Database), OpenMode.ForWrite);
                var leader = new Line(crossResult.Value, textResult.Value);
                modelSpace.AppendEntity(leader);
                tr.AddNewlyCreatedDBObject(leader, true);

                tr.Commit();
            }
        }

        private void Cancel(object parameter)
        {
            _isCancelled = true;
            RequestClose?.Invoke(false);
        }

        private void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new System.ComponentModel.PropertyChangedEventArgs(propertyName));
        }
    }
}
