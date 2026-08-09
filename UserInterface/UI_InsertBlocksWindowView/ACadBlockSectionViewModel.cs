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
using PropertyChanged;

namespace Autocad_Primavera_P6_Plugin.UserInterface.UI_InsertBlocksWindowView
{
    public sealed class ACadBlockSectionModel
    {
        public ObservableCollection<PredefinedBlockInfo> AvailableBlocks { get; } = new ObservableCollection<PredefinedBlockInfo>();

        public string DefaultBlocksFolder { get; set; }

        public BlockTypeMode BlockTypeMode { get; set; } = BlockTypeMode.Predefined;

        public PredefinedBlockInfo SelectedPredefinedBlock { get; set; }

        public ObjectId SelectedDrawingBlockId { get; set; } = ObjectId.Null;

        public string SelectedDrawingBlockName { get; set; } = "No drawing block selected.";

        public bool CopyBlockDefinition { get; set; }
    }

    public sealed class BlockSourceResult
    {
        public ObjectId BlockDefinitionId { get; set; }

        public string BlockName { get; set; }
    }

    [AddINotifyPropertyChangedInterface]
    public sealed class ACadBlockSectionViewModel
    {
        private readonly Document _acadDoc;
        private readonly ACadBlockSectionModel _model;

        public string StatusMessage { get; set; }

        public ICommand BrowseBlocksFolderCommand { get; private set; }

        public ICommand SelectDrawingBlockCommand { get; private set; }

        public BlockTypeMode BlockTypeMode
        {
            get => _model.BlockTypeMode;
            set => _model.BlockTypeMode = value;
        }

        public ObservableCollection<PredefinedBlockInfo> AvailableBlocks => _model.AvailableBlocks;

        public PredefinedBlockInfo SelectedPredefinedBlock
        {
            get => _model.SelectedPredefinedBlock;
            set => _model.SelectedPredefinedBlock = value;
        }

        public ObjectId SelectedDrawingBlockId
        {
            get => _model.SelectedDrawingBlockId;
            set => _model.SelectedDrawingBlockId = value;
        }

        public string SelectedDrawingBlockName
        {
            get => _model.SelectedDrawingBlockName;
            set => _model.SelectedDrawingBlockName = value;
        }

        public bool CopyBlockDefinition
        {
            get => _model.CopyBlockDefinition;
            set => _model.CopyBlockDefinition = value;
        }

        public ACadBlockSectionViewModel(Document acadDoc, BlockReference preSelectedBlockRef)
        {
            _acadDoc = acadDoc;
            _model = new ACadBlockSectionModel
            {
                DefaultBlocksFolder = GetDefaultBlocksFolder()
            };

            StatusMessage = string.Empty;
            BrowseBlocksFolderCommand = new RelayCommand(BrowseBlocksFolder);
            SelectDrawingBlockCommand = new RelayCommand(SelectBlockFromDrawing);
        }

        public Task Async_Init()
        {
            LoadPredefinedBlocks(_model.DefaultBlocksFolder);
            return Task.CompletedTask;
        }

        public bool HasValidDrawingBlockSelection()
        {
            return !SelectedDrawingBlockId.IsNull && SelectedDrawingBlockId.IsValid;
        }

        public BlockSourceResult ResolveBlockSourceForInsert(Database db)
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

        private void LoadPredefinedBlocks(string folder)
        {
            try
            {
                string targetFolder = string.IsNullOrWhiteSpace(folder) ? GetDefaultBlocksFolder() : folder;

                AvailableBlocks.Clear();
                Directory.CreateDirectory(targetFolder);

                foreach (var file in Directory.GetFiles(targetFolder, "*.dwg", SearchOption.TopDirectoryOnly)
                             .OrderBy(Path.GetFileNameWithoutExtension))
                {
                    AvailableBlocks.Add(new PredefinedBlockInfo
                    {
                        Name = Path.GetFileNameWithoutExtension(file),
                        FilePath = file
                    });
                }

                SelectedPredefinedBlock = AvailableBlocks.FirstOrDefault();
                StatusMessage = AvailableBlocks.Count == 0
                    ? "No predefined .dwg block files found in " + targetFolder
                    : "Loaded " + AvailableBlocks.Count + " predefined block file(s).";
            }
            catch (Exception ex)
            {
                StatusMessage = "Failed to load predefined blocks: " + ex.Message;
                Debug.Print(ex.ToString());
            }
            finally
            {
                CommandManager.InvalidateRequerySuggested();
            }
        }

        private string GetDefaultBlocksFolder()
        {
            string appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            return Path.Combine(appData, "Autocad_Primavera_P6_Plugin", "Blocks");
        }

        private void BrowseBlocksFolder(object parameter)
        {
            string folder = _model.DefaultBlocksFolder;
            if (SelectedPredefinedBlock != null && !string.IsNullOrWhiteSpace(SelectedPredefinedBlock.FilePath))
            {
                folder = Path.GetDirectoryName(SelectedPredefinedBlock.FilePath);
            }

            if (string.IsNullOrWhiteSpace(folder))
            {
                folder = GetDefaultBlocksFolder();
            }

            try
            {
                Directory.CreateDirectory(folder);
                Process.Start(new ProcessStartInfo("explorer.exe", folder)
                {
                    UseShellExecute = true
                });
                LoadPredefinedBlocks(folder);
            }
            catch (Exception ex)
            {
                StatusMessage = "Failed to open blocks folder: " + ex.Message;
                MessageBox.Show(StatusMessage, "Insert Blocks", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void SelectBlockFromDrawing(object parameter)
        {
            var owner = parameter as Window;
            var doc = _acadDoc;
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
            catch (Exception ex)
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

        private ObjectId GetSourceDefinitionId(BlockReference blockRef)
        {
            if (blockRef.IsDynamicBlock && !blockRef.DynamicBlockTableRecord.IsNull)
            {
                return blockRef.DynamicBlockTableRecord;
            }

            return blockRef.BlockTableRecord;
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
    }
}
