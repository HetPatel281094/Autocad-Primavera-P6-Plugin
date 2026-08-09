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
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PropertyChanged;

namespace Autocad_Primavera_P6_Plugin.UserInterface.UI_InsertBlocksWindowView
{
    public enum BlockTypeModeEnum
    {
        Predefined,
        SelectFromDrawing
    }

    public sealed class BlockSourceResult
    {
        public ObjectId BlockDefinitionId { get; set; }

        public string BlockName { get; set; }
    }

    public partial class ACadBlockSectionViewModel: ObservableObject
    {
        private readonly Document _acadDoc;

        private BlockReference _preSelectedBlockRef;
        private string _defaultBlocksFolder => GetDefaultBlocksFolder();


        [ObservableProperty]
        public BlockTypeModeEnum _blockTypeMode;
        partial void OnBlockTypeModeChanged(BlockTypeModeEnum oldValue, BlockTypeModeEnum newValue)
        {
            SelectedBlockTableRecord = null;

            if (newValue == BlockTypeModeEnum.Predefined) { SelectedPredefinedBlock = null; };

            NewBlockName = "";

        }

        public string SelectedBTRObjId => SelectedBlockTableRecord?.ObjectId.ToString() ?? String.Empty;

        public string SelectedBTRName => SelectedBlockTableRecord?.Name ?? String.Empty;

        [ObservableProperty]
        public bool _isCopyBlockDefinition;

        [ObservableProperty]
        public string _newBlockName;


        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(SelectedBTRObjId))]
        [NotifyPropertyChangedFor(nameof(SelectedBTRName))]
        [NotifyPropertyChangedFor(nameof(CanAutoGenerate))]
        public BlockTableRecord _selectedBlockTableRecord;
        partial void OnSelectedBlockTableRecordChanged(BlockTableRecord oldValue, BlockTableRecord newValue)
        {
            NewBlockName = newValue != null ? $"{newValue.Name}_" : "";
        }

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(CanAutoGenerate))]
        public PredefinedBlockInfo _selectedPredefinedBlock;
        partial void OnSelectedPredefinedBlockChanged(PredefinedBlockInfo oldValue, PredefinedBlockInfo newValue)
        {
            NewBlockName = newValue != null ? $"{newValue.Name}_" : "";
        }

        [ObservableProperty]
        public string _info;

        [ObservableProperty]
        public ObservableCollection<PredefinedBlockInfo> _availableBlocks = new();

        [ObservableProperty]
        public bool _isRefreshAvailableBlocks = false;

        public bool CanAutoGenerate => SelectedPredefinedBlock != null || (SelectedBlockTableRecord != null &&
            SelectedBlockTableRecord.ObjectId.IsWellBehaved);

        public ACadBlockSectionViewModel(Document acadDoc, BlockReference preSelectedBlockRef = null)
        {
            _acadDoc = acadDoc;
            _preSelectedBlockRef = preSelectedBlockRef;
            Info = string.Empty;
        }

        public Task Async_Init()
        {
            if (_preSelectedBlockRef != null)
            {
                BlockTypeMode = BlockTypeModeEnum.SelectFromDrawing;

                using(var tr = _acadDoc.TransactionManager.StartTransaction())
                {
                    var reFetchedreSelectedBlockRef = (BlockReference)tr.GetObject(_preSelectedBlockRef.ObjectId, OpenMode.ForRead);

                    var BTR = reFetchedreSelectedBlockRef.IsDynamicBlock ?
                        (BlockTableRecord)tr.GetObject(reFetchedreSelectedBlockRef.DynamicBlockTableRecord, OpenMode.ForRead)
                        : (BlockTableRecord)tr.GetObject(reFetchedreSelectedBlockRef.BlockTableRecord, OpenMode.ForRead);

                    SelectedBlockTableRecord = BTR;
                };

            };

            LoadPredefinedBlocks();

            return Task.CompletedTask;
        }


        [RelayCommand]
        private void BrowseBlocksFolder(object parameter)
        {
            string folder = _defaultBlocksFolder;

            try
            {
                Directory.CreateDirectory(folder);

                Process.Start(new ProcessStartInfo("explorer.exe", folder)
                {
                    UseShellExecute = true
                });

                IsRefreshAvailableBlocks = true;
            }
            catch (Exception ex)
            {
                Info = "Failed to open blocks folder: " + ex.Message;
                MessageBox.Show(Info, "Insert Blocks", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        [RelayCommand]
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

                        var attRefDict = PluginBlockRefAutocadHelpers.Get_AttDefDict(blockRef, tr);

                        if (blockRef == null || attRefDict == null || !PluginBlockRefAutocadHelpers.IsValidPluginBlock(attRefDict))
                        {
                            ed.WriteMessage("\nNot a valid plugin block.");
                            continue;
                        }

                        var BTR = blockRef.IsDynamicBlock ?
                            (BlockTableRecord)tr.GetObject(blockRef.DynamicBlockTableRecord, OpenMode.ForRead)
                            : (BlockTableRecord)tr.GetObject(blockRef.BlockTableRecord, OpenMode.ForRead);

                        SelectedBlockTableRecord = BTR;

                        return;
                    };
                };
            }
            catch (Exception ex)
            {
                Info = "Failed to select drawing block: " + ex.Message;
                ed.WriteMessage("\n[Plugin] Failed to select drawing block: " + ex.Message);
                MessageBox.Show(Info, "Insert Blocks", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                if (owner != null)
                {
                    owner.Show();
                    owner.Activate();
                }
            };

        }

        [RelayCommand]
        private void GenerateNewBTR(object parameter)
        {

        }

        [RelayCommand]
        private void RefreshAvailableBlocks()
        {
            if (IsRefreshAvailableBlocks)
            {
                LoadPredefinedBlocks();
                IsRefreshAvailableBlocks = false;
            };
        }

        private void LoadPredefinedBlocks()
        {
            try
            {
                string targetFolder = _defaultBlocksFolder;

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

                Info = AvailableBlocks.Count == 0
                    ? "No predefined .dwg block files found in " + targetFolder
                    : "Loaded " + AvailableBlocks.Count + " predefined block file(s).";
            }
            catch (Exception ex)
            {
                Info = "Failed to load predefined blocks: " + ex.Message;
                Debug.Print(ex.ToString());
            }
            finally
            {
                CommandManager.InvalidateRequerySuggested();
            };
        }




        public ObjectId SelectedDrawingBlockId;

        public bool HasValidDrawingBlockSelection()
        {
            return !SelectedDrawingBlockId.IsNull && SelectedDrawingBlockId.IsValid;
        }

        public BlockSourceResult ResolveBlockSourceForInsert(Database db)
        {
            if (BlockTypeMode == BlockTypeModeEnum.Predefined)
            {
                return IsCopyBlockDefinition
                    ? ResolveCopiedPredefinedBlockSource(db)
                    : ResolvePredefinedBlockSource(db);
            }

            var source = ResolveDrawingBlockSource(db);
            return IsCopyBlockDefinition ? CopyBlockDefinitionToUniqueRecord(db, source.BlockDefinitionId) : source;
        }

        private string GetDefaultBlocksFolder()
        {
            string appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            return Path.Combine(appData, "Autocad_Primavera_P6_Plugin", "Blocks");
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
