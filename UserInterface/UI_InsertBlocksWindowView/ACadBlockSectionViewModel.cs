using Autocad_Primavera_P6_Plugin.Services.AutocadService;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;

namespace Autocad_Primavera_P6_Plugin.UserInterface.UI_InsertBlocksWindowView
{
    public sealed class PredefinedBlockInfo
    {
        public string Name { get; set; }
        public string FilePath { get; set; }

        public override string ToString()
        {
            return Name;
        }
    }

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
        [NotifyCanExecuteChangedFor(nameof(GenerateNewBTRCommand))]
        public string _newBlockName;


        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(SelectedBTRObjId))]
        [NotifyPropertyChangedFor(nameof(SelectedBTRName))]
        [NotifyPropertyChangedFor(nameof(CanAutoGenerate))]
        [NotifyCanExecuteChangedFor(nameof(GenerateNewBTRCommand))]
        public BlockTableRecord _selectedBlockTableRecord;
        partial void OnSelectedBlockTableRecordChanged(BlockTableRecord oldValue, BlockTableRecord newValue)
        {
            NewBlockName = newValue != null ? $"{newValue.Name}_" : "";
        }

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(CanAutoGenerate))]
        [NotifyCanExecuteChangedFor(nameof(GenerateNewBTRCommand))]
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

                        var attRefDict = PluginBlockRefAutocadHelpers.Get_AttRefDict(blockRef, tr);

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

        [RelayCommand(CanExecute = nameof(GenerateNewBTRCanExecute))]
        public void GenerateNewBTR(object parameter = null)
        {

            var _btr = SelectedBlockTableRecord;
            var _selectedComboBox = SelectedPredefinedBlock;
            var _name = NewBlockName.Trim();

            if ((_btr == null && _selectedComboBox == null) || string.IsNullOrWhiteSpace(_name)) { Info = "Unable to Generate";  return; };

            using (_acadDoc.LockDocument())
            using (var tr = _acadDoc.Database.TransactionManager.StartTransaction())
            {
                var _newBTRName = NormalizeBlockName(_name);

                var _btrTable = tr.GetObject(_acadDoc.Database.BlockTableId, OpenMode.ForWrite) as BlockTable;

                if (_btrTable.Has(_newBTRName))
                {
                    SelectedBlockTableRecord = (BlockTableRecord)tr.GetObject(_btrTable[_name], OpenMode.ForRead);
                    IsCopyBlockDefinition = false;
                    Info = "Name Already Exist";
                    return;
                };

                if (BlockTypeMode.Equals(BlockTypeModeEnum.Predefined))
                {
                    using (var sourceDb = new Database(false, true))
                    {
                        sourceDb.ReadDwgFile(_selectedComboBox.FilePath, FileOpenMode.OpenForReadAndAllShare, true, null);

                        ObjectId _newBTRId = _acadDoc.Database.Insert(_newBTRName, sourceDb, false);

                        SelectedBlockTableRecord = (BlockTableRecord)tr.GetObject(_newBTRId, OpenMode.ForRead);
                        IsCopyBlockDefinition = false;

                        Info = "Generated New BlockTableRecord";
                    }

                    tr.Commit();
                }
                else if (BlockTypeMode.Equals(BlockTypeModeEnum.SelectFromDrawing))
                {
                    using (var _tempDb = new Database(true, true))
                    {
                        var _sourceIds = new ObjectIdCollection { _btr.ObjectId };
                        var _idMapping = new IdMapping();

                        var x = _acadDoc.Database.Wblock(_btr.ObjectId);

                        ObjectId _newBtrId = _acadDoc.Database.Insert(_newBTRName, x, false);

                        SelectedBlockTableRecord = (BlockTableRecord)tr.GetObject(_newBtrId, OpenMode.ForRead);
                        IsCopyBlockDefinition = false;

                        Info = "Generated New BlockTableRecord";
                    };

                    tr.Commit();
                }
                else
                {
                    Info = $"Unable to generate New BlockTableRecord";
                };

            };

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

        private bool GenerateNewBTRCanExecute()
        {
            if (NewBlockName == null || String.IsNullOrWhiteSpace(NewBlockName))
            {
                return false;
            };

            if (BlockTypeMode == BlockTypeModeEnum.Predefined)
            {
                if (SelectedPredefinedBlock != null)
                {
                    return true;
                };
            };

            if (BlockTypeMode == BlockTypeModeEnum.SelectFromDrawing)
            {
                if (SelectedBlockTableRecord != null)
                {
                    return true;
                };
            };

            return false;
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

        private static string GetDefaultBlocksFolder()
        {
            string appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            return Path.Combine(appData, "Autocad_Primavera_P6_Plugin", "Blocks");
        }

        private static string NormalizeBlockName(string blockName)
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
                safeBaseName = safeBaseName[..180];
            }

            return string.IsNullOrWhiteSpace(safeBaseName) ? "PluginBlock" : safeBaseName;
        }
    }
}
