using Autocad_Primavera_P6_Plugin.Services.P6ApiService;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;
using Autodesk.AutoCAD.Runtime;
using ClosedXML.Excel;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using static Autocad_Primavera_P6_Plugin.Services.AutocadService.Temp.PlugInBlockReference_Helpers;
using AcadDocument = Autodesk.AutoCAD.ApplicationServices.Document;

namespace Autocad_Primavera_P6_Plugin.Services.AutocadService.Temp
{
    public static class PlugInBlockReference_Helpers
    {
        public static Dictionary<string, AttributeReference> Get_AttRefDict(BlockReference acadBlockRef, Transaction tr)
        {
            return acadBlockRef.AttributeCollection
                .Cast<ObjectId>()
                .Select(id => (AttributeReference)tr.GetObject(id, OpenMode.ForRead))
                .ToDictionary(
                    ar => ar.Tag,
                    ar => ar,
                    StringComparer.OrdinalIgnoreCase
                );
        }

        public static AttributeReference TryGetValue_AttRefDict(Dictionary<string, AttributeReference> attributes, string tag)
        {
            attributes.TryGetValue(tag, out AttributeReference attribute);
            return attribute;
        }

        public static Dictionary<string, DynamicBlockReferenceProperty> Get_DyBlockRefPropDict(BlockReference acadBlockRef)
        {
            return acadBlockRef.DynamicBlockReferencePropertyCollection
                .Cast<DynamicBlockReferenceProperty>()
                .ToDictionary(
                    p => p.PropertyName,
                    p => p,
                    StringComparer.OrdinalIgnoreCase
                );
        }

        public static DynamicBlockReferenceProperty TryGetValue_DyBlockRefPropDict(Dictionary<string, DynamicBlockReferenceProperty> properties, string name)
        {
            properties.TryGetValue(name, out DynamicBlockReferenceProperty property);
            return property;
        }

        public static void ExecuteWithDocumentLock(AcadDocument document, Action action)
        {
            bool ownsLock = document.LockMode() == DocumentLockMode.NotLocked;

            using (DocumentLock documentLock = ownsLock ? document.LockDocument() : null) { action(); }
        }

        public static T ExecuteWithDocumentLock<T>(AcadDocument document, Func<T> func)
        {
            bool ownsLock = document.LockMode() == DocumentLockMode.NotLocked;

            using (DocumentLock documentLock = ownsLock ? document.LockDocument() : null) { return func(); }
        }

        public enum TransactionAction { Nothing, Commit, Abort }

        public sealed class TransactionResult<T>
        {
            public T Result { get; }
            public TransactionAction Action { get; }

            public TransactionResult(T result, TransactionAction action) { Result = result; Action = action; }
        }

        public static void ExecuteWithTransaction(Database database, Transaction transaction, Func<Transaction, TransactionAction> action)
        {
            bool ownsTransaction = transaction == null;

            using (Transaction currentTransaction = ownsTransaction ? database.TransactionManager.StartTransaction() : null)
            {
                Transaction tr = transaction ?? currentTransaction;

                TransactionAction actionResult = action(tr);

                if (!ownsTransaction) { return; }

                switch (actionResult)
                {
                    case TransactionAction.Commit: tr.Commit(); break;
                    case TransactionAction.Abort: tr.Abort(); break;
                    case TransactionAction.Nothing: break;
                    default: throw new ArgumentOutOfRangeException(nameof(actionResult));
                }
            }
        }

        public static TransactionResult<T> ExecuteWithTransaction<T>(Database database, Transaction transaction, Func<Transaction, TransactionResult<T>> func)
        {
            bool ownsTransaction = transaction == null;

            using (Transaction currentTransaction = ownsTransaction ? database.TransactionManager.StartTransaction() : null)
            {
                Transaction tr = transaction ?? currentTransaction;

                TransactionResult<T> outcome = func(tr);

                if (ownsTransaction)
                {
                    switch (outcome.Action)
                    {
                        case TransactionAction.Commit: tr.Commit(); break;
                        case TransactionAction.Abort: tr.Abort(); break;
                        case TransactionAction.Nothing: break;
                        default: throw new ArgumentOutOfRangeException(nameof(outcome.Action));
                    }
                }

                return outcome;
            }
        }

        public static readonly Dictionary<string, string> DefaultPropSlotDict = new()
        {
            ["Slot_1"] = "BOUNDARY_CODE_ID",
            ["Slot_2"] = "BOUNDARY_CODE_PATH",
            ["Slot_3"] = "BOUNDARY_CODE_VALUE",
            ["Slot_4"] = "ELEMENT_ID_CODE_ID",
            ["Slot_5"] = "ELEMENT_ID_CODE_PATH",
            ["Slot_6"] = "ELEMENT_ID_CODE_VALUE",
            ["Slot_7"] = "LENGTH_L",
            ["Slot_8"] = "BREADTH_B",
            ["Slot_9"] = "HEIGHT_H"
        };
    }

    public class Slot
    {
        public int SlotIndex;
        public string SlotName;
        public string NameAttRefTag;
        public string ValueAttRefTag;
        public AttributeReference NameAttRef;
        public AttributeReference ValueAttRef;

        private Slot() { }

        public bool IsNoProperty => string.IsNullOrEmpty(NameAttRef.TextString.Trim());
        public bool IsNoValue => string.IsNullOrEmpty(ValueAttRef.TextString.Trim());

        public static Slot New_Slot(Dictionary<string, AttributeReference> attRefDict, int slotIndex)
        {
            var slotName = "Slot_" + slotIndex;
            var nameAttRefTag = "ATT_" + slotIndex + "_NAME";
            var valueAttRefTag = "ATT_" + slotIndex + "_VALUE";

            var nameAttribute = PlugInBlockReference_Helpers.TryGetValue_AttRefDict(attRefDict, nameAttRefTag);
            var valueAttribute = PlugInBlockReference_Helpers.TryGetValue_AttRefDict(attRefDict, valueAttRefTag);

            if (nameAttribute == null || valueAttribute == null) { return null; }

            var new_slot = new Slot
            {
                SlotIndex = slotIndex,
                SlotName = slotName,
                NameAttRefTag = nameAttRefTag,
                ValueAttRefTag = valueAttRefTag,
                NameAttRef = nameAttribute,
                ValueAttRef = valueAttribute
            };

            return new_slot;
        }
    }

    public class PlugInBlockReference
    {
        public BlockReference AcadBlockRef;
        public PlugInBlockReference(BlockReference blockRef, bool skipInit = false)
        {
            AcadBlockRef = blockRef;
            if (!skipInit) { _ = Async_Init(); }
        }

        private MyPlugin PluginInstance;
        private AcadDocument AcadDoc;
        private AttributeReference Attribute_Check_String_AttRef;
        public AttributeReference ElementId_AttRef;
        public AttributeReference BlockType_AttRef;
        private Dictionary<string, string> PropSlotDict = new(StringComparer.OrdinalIgnoreCase);
        private Dictionary<string, Slot> SlotsDict = new() { ["Slot_1"] = null, ["Slot_2"] = null, ["Slot_3"] = null, ["Slot_4"] = null, ["Slot_5"] = null, ["Slot_6"] = null, ["Slot_7"] = null, ["Slot_8"] = null, ["Slot_9"] = null, ["Slot_10"] = null, ["Slot_11"] = null, ["Slot_12"] = null, ["Slot_13"] = null, ["Slot_14"] = null, ["Slot_15"] = null };
        public Group BlockRefGroup;
        private BlockReference ReportTableHeaderBlockRef;
        public async Task Async_Init(Transaction tr = null)
        {
            PluginInstance = MyPlugin.Instance;
            AcadDoc = Application.DocumentManager.GetDocument(AcadBlockRef.Database);

            PlugInBlockReference_Helpers.ExecuteWithTransaction(
                 AcadDoc.Database,
                 tr,
                 currTr =>
                     {
                         Dictionary<string, AttributeReference> _attRefDict = PlugInBlockReference_Helpers.Get_AttRefDict(AcadBlockRef, currTr);

                         Attribute_Check_String_AttRef = PlugInBlockReference_Helpers.TryGetValue_AttRefDict(_attRefDict, "Attribute_Check_String");

                         if (!IsValidBlock) { throw new System.Exception("Provided Block is not valid. Cannot initialize new plugin block instance"); }

                         ElementId_AttRef = PlugInBlockReference_Helpers.TryGetValue_AttRefDict(_attRefDict, "ELEMENT_ID");

                         BlockType_AttRef = PlugInBlockReference_Helpers.TryGetValue_AttRefDict(_attRefDict, "BLOCK_TYPE");

                         foreach (var key in SlotsDict.Keys.ToList())
                         {
                             int _slotIndex = int.Parse(key.Split("_").Last());
                             Slot _newSlot = Slot.New_Slot(_attRefDict, _slotIndex);
                             SlotsDict[key] = _newSlot;
                             if (_newSlot != null && !_newSlot.IsNoProperty)
                             {
                                 PropSlotDict[_newSlot.NameAttRefTag] = _newSlot.SlotName;
                             }
                         }

                         BlockRefGroup = GetGroupOfBlockRef(currTr);

                         if (IsGrouped) { ReportTableHeaderBlockRef = GetReportTableHeaderBlockRef(currTr); }

                         return TransactionAction.Nothing;
                     }
            );

        }

        public bool IsValidBlock => Attribute_Check_String_AttRef != null && Attribute_Check_String_AttRef.TextString.Trim().Equals("FoundOK", StringComparison.OrdinalIgnoreCase);

        public bool IsGrouped => BlockRefGroup != null;

        private Slot Get_PropertySlot(string propertyName)
        {
            var slotFound = PropSlotDict.TryGetValue(propertyName, out string slotName);
            if (!slotFound) { return null; }
            return SlotsDict[slotName];
        }

        public string Get_PropertySlotValue(string propertyName)
        {
            var propertySlot = Get_PropertySlot(propertyName);
            if (propertySlot == null || propertySlot.IsNoValue) { return null; }
            return propertySlot.ValueAttRef.TextString;
        }

        public bool Set_PropertySlotValue(string propertyName, string updateValue, Transaction tr, out string result)
        {
            TransactionResult<(bool Success, string OutValue)> operationResult =
                PlugInBlockReference_Helpers.ExecuteWithDocumentLock(AcadDoc, () =>
                    PlugInBlockReference_Helpers.ExecuteWithTransaction(
                        AcadDoc.Database,
                        tr,
                        currTr =>
                        {
                            var _propertySlot = Get_PropertySlot(propertyName);

                            if (_propertySlot != null)
                            {
                                var _valueAttRef = (AttributeReference)currTr.GetObject(_propertySlot.ValueAttRef.ObjectId, OpenMode.ForWrite);
                                _valueAttRef.TextString = updateValue;

                                return new TransactionResult<(bool, string)>((true, updateValue), TransactionAction.Commit);
                            }
                            else
                            {
                                string _defaultSlotName = PlugInBlockReference_Helpers.DefaultPropSlotDict
                                    .FirstOrDefault(kv => kv.Value.Equals(propertyName, StringComparison.OrdinalIgnoreCase))
                                    .Key;

                                Slot _targetSlot = null;

                                if (_defaultSlotName != null
                                    && SlotsDict.TryGetValue(_defaultSlotName, out Slot _defaultSlot)
                                    && _defaultSlot != null
                                    && _defaultSlot.IsNoProperty)
                                {
                                    _targetSlot = _defaultSlot;
                                }
                                else
                                {
                                    _targetSlot = SlotsDict
                                        .Where(kv => !PlugInBlockReference_Helpers.DefaultPropSlotDict.ContainsKey(kv.Key))
                                        .Select(kv => kv.Value)
                                        .FirstOrDefault(s => s != null && s.IsNoProperty);
                                }

                                if (_targetSlot == null)
                                {
                                    return new TransactionResult<(bool, string)>((false, null), TransactionAction.Nothing);
                                }

                                var _nameAttRef = (AttributeReference)currTr.GetObject(_targetSlot.NameAttRef.ObjectId, OpenMode.ForWrite);
                                _nameAttRef.TextString = propertyName;

                                var _valueAttRef = (AttributeReference)currTr.GetObject(_targetSlot.ValueAttRef.ObjectId, OpenMode.ForWrite);
                                _valueAttRef.TextString = updateValue;

                                PropSlotDict[propertyName] = _targetSlot.SlotName;

                                return new TransactionResult<(bool, string)>((true, updateValue), TransactionAction.Commit);
                            }
                        }
                    )
                );

            result = operationResult.Result.OutValue;
            return operationResult.Result.Success;
        }

        public bool Set_InfoPosition(Point3d targetPosition, out Point3d? resultPosition, Transaction tr = null)
        {
            TransactionResult<(bool Success, Point3d? OutValue)> operationResult =
                PlugInBlockReference_Helpers.ExecuteWithDocumentLock(AcadDoc, () =>
                    PlugInBlockReference_Helpers.ExecuteWithTransaction(
                        AcadDoc.Database,
                        tr,
                        currTr =>
                        {
                            var _blockRef = (BlockReference)currTr.GetObject(AcadBlockRef.ObjectId, OpenMode.ForWrite);

                            if (!_blockRef.IsDynamicBlock) { return new TransactionResult<(bool, Point3d?)>((false, null), TransactionAction.Nothing); }

                            Matrix3d _wcsToBlock = _blockRef.BlockTransform.Inverse();
                            Point3d _localPoint = targetPosition.TransformBy(_wcsToBlock);

                            double _appliedX = _localPoint.X;
                            double _appliedY = _localPoint.Y;

                            string paramName = "MoveInfo";
                            string _dyProperty_X_Name = paramName + " X";
                            string _dyProperty_Y_Name = paramName + " Y";

                            var _dyBlockRefPropDict = PlugInBlockReference_Helpers.Get_DyBlockRefPropDict(_blockRef);
                            var _dyProperty_X = PlugInBlockReference_Helpers.TryGetValue_DyBlockRefPropDict(_dyBlockRefPropDict, _dyProperty_X_Name);
                            var _dyProperty_Y = PlugInBlockReference_Helpers.TryGetValue_DyBlockRefPropDict(_dyBlockRefPropDict, _dyProperty_Y_Name);

                            if (_dyProperty_X == null || _dyProperty_Y == null) { return new TransactionResult<(bool, Point3d?)>((false, null), TransactionAction.Nothing); }

                            _dyProperty_X.Value = _appliedX;
                            _dyProperty_Y.Value = _appliedY;

                            return new TransactionResult<(bool, Point3d?)>((true, targetPosition), TransactionAction.Commit);
                        }
                    )
                );

            resultPosition = operationResult.Result.OutValue;
            return operationResult.Result.Success;
        }

        public bool Set_BlockPosition(Point3d newPosition, out Point3d resultPosition, Transaction tr = null)
        {
            TransactionResult<(bool Success, Point3d OutValue)> operationResult =
                PlugInBlockReference_Helpers.ExecuteWithDocumentLock(AcadDoc, () =>
                    PlugInBlockReference_Helpers.ExecuteWithTransaction(
                        AcadDoc.Database,
                        tr,
                        currTr =>
                        {
                            var _blockRef = (BlockReference)currTr.GetObject(AcadBlockRef.ObjectId, OpenMode.ForWrite);
                            _blockRef.Position = newPosition;

                            return new TransactionResult<(bool, Point3d)>((true, _blockRef.Position), TransactionAction.Commit);
                        }
                    )
                );

            resultPosition = operationResult.Result.OutValue;
            return operationResult.Result.Success;
        }

        public async Task<ActivityCode> Get_BdryActCode()
        {
            var boundaryCodeIdString = Get_PropertySlotValue("BOUNDARY_CODE_ID");
            var isBoundaryCodeId = int.TryParse(boundaryCodeIdString, out int boundaryCodeId);

            if (!isBoundaryCodeId) { return null; }

            var p6ApiService = PluginInstance.MyP6ApiService;
            var actCode = await p6ApiService.GetP6ActivityCodeById(boundaryCodeId);

            if (actCode == null) { return null; }

            return actCode;
        }

        public bool Set_BdryActCode(ActivityCode actCode, out ActivityCode result, Transaction tr = null)
        {
            TransactionResult<(bool Success, ActivityCode OutValue)> operationResult =
                PlugInBlockReference_Helpers.ExecuteWithDocumentLock(AcadDoc, () =>
                    PlugInBlockReference_Helpers.ExecuteWithTransaction(
                        AcadDoc.Database,
                        tr,
                        currTr =>
                        {
                            var boundaryCodeId = actCode.ObjectId.ToString();
                            var boundaryCodeValue = actCode.Description;
                            var boundaryCodePath = actCode.CodeConcatName;

                            var idResult = Set_PropertySlotValue("BOUNDARY_CODE_ID", boundaryCodeId, currTr, out _);
                            var valueResult = Set_PropertySlotValue("BOUNDARY_CODE_VALUE", boundaryCodeValue, currTr, out _);
                            var pathResult = Set_PropertySlotValue("BOUNDARY_CODE_PATH", boundaryCodePath, currTr, out _);

                            if (!idResult || !valueResult || !pathResult) { return new TransactionResult<(bool, ActivityCode)>((false, null), TransactionAction.Abort); }

                            return new TransactionResult<(bool, ActivityCode)>((true, actCode), TransactionAction.Commit);
                        }
                    )
                );

            result = operationResult.Result.OutValue;
            return operationResult.Result.Success;
        }

        public async Task<ActivityCode> Get_ElementIdCode()
        {
            var elementIdCodeIdString = Get_PropertySlotValue("ELEMENT_ID_CODE_ID");
            var isElementIdCodeId = int.TryParse(elementIdCodeIdString, out int elementIdCodeId);

            if (!isElementIdCodeId) { return null; }

            var p6ApiService = PluginInstance.MyP6ApiService;
            var actCode = await p6ApiService.GetP6ActivityCodeById(elementIdCodeId);

            if (actCode == null) { return null; }

            return actCode;
        }

        public bool Set_ElementIdCode(ActivityCode actCode, out ActivityCode result, Transaction tr = null)
        {
            TransactionResult<(bool Success, ActivityCode OutValue)> operationResult =
                PlugInBlockReference_Helpers.ExecuteWithDocumentLock(AcadDoc, () =>
                    PlugInBlockReference_Helpers.ExecuteWithTransaction(
                        AcadDoc.Database,
                        tr,
                        currTr =>
                        {
                            var elementIdCodeId = actCode.ObjectId.ToString();
                            var elementIdCodeValue = actCode.Description;
                            var elementIdCodePath = actCode.CodeConcatName;

                            var idResult = Set_PropertySlotValue("ELEMENT_ID_CODE_ID", elementIdCodeId, currTr, out _);
                            var valueResult = Set_PropertySlotValue("ELEMENT_ID_CODE_VALUE", elementIdCodeValue, currTr, out _);
                            var pathResult = Set_PropertySlotValue("ELEMENT_ID_CODE_PATH", elementIdCodePath, currTr, out _);

                            if (!idResult || !valueResult || !pathResult) { return new TransactionResult<(bool, ActivityCode)>((false, null), TransactionAction.Abort); }

                            var setElementIdResult = Reset_ElementId(out _, currTr);

                            return new TransactionResult<(bool, ActivityCode)>((true, actCode), TransactionAction.Commit);
                        }
                    )
                );

            result = operationResult.Result.OutValue;
            return operationResult.Result.Success;
        }

        public bool Reset_ElementId(out string result, Transaction tr = null)
        {
            static string Get_ElementIdString(string pathString)
            {
                try
                {
                    var actCodePathArray = pathString.Split('.')[^2..];
                    var elementIdCodePath = string.Join(" -> ", actCodePathArray);
                    return elementIdCodePath;
                }
                catch
                {
                    return String.Empty;
                }
            }

            TransactionResult<(bool Success, string OutValue)> operationResult =
                PlugInBlockReference_Helpers.ExecuteWithDocumentLock(AcadDoc, () =>
                    PlugInBlockReference_Helpers.ExecuteWithTransaction(
                        AcadDoc.Database,
                        tr,
                        currTr =>
                        {
                            var _elementIdCodePath = Get_PropertySlotValue("ELEMENT_ID_CODE_PATH");
                            var _elementIdString = Get_ElementIdString(_elementIdCodePath);

                            var _elementId_AttRef = (AttributeReference)currTr.GetObject(ElementId_AttRef.ObjectId, OpenMode.ForWrite);

                            _elementId_AttRef.TextString = _elementIdString;

                            return new TransactionResult<(bool, string)>((true, _elementIdString), TransactionAction.Commit);
                        }
                    )
                );

            result = operationResult.Result.OutValue;
            return operationResult.Result.Success;
        }

        private Group GetGroupOfBlockRef(Transaction tr = null)
        {
            TransactionResult<(bool Success, Group OutValue)> operationResult =
                PlugInBlockReference_Helpers.ExecuteWithTransaction(
                    AcadDoc.Database,
                    tr,
                    currTr =>
                    {
                        try
                        {
                            DBDictionary groupDict = (DBDictionary)currTr.GetObject(AcadDoc.Database.GroupDictionaryId, OpenMode.ForRead);

                            foreach (DBDictionaryEntry entry in groupDict)
                            {
                                Group grp = (Group)currTr.GetObject(entry.Value, OpenMode.ForRead);
                                if (grp != null && grp.Has(AcadBlockRef))
                                {
                                    return new TransactionResult<(bool, Group)>((true, grp), TransactionAction.Nothing);
                                }
                            }

                            return new TransactionResult<(bool, Group)>((false, null), TransactionAction.Nothing);
                        }
                        catch (System.Exception e)
                        {
                            Debug.Print(e.ToString());
                            return new TransactionResult<(bool, Group)>((false, null), TransactionAction.Nothing);
                        }
                    }
                );

            return operationResult.Result.OutValue;
        }

        private BlockReference GetReportTableHeaderBlockRef(Transaction tr = null)
        {
            TransactionResult<(bool Success, BlockReference OutValue)> operationResult =
                PlugInBlockReference_Helpers.ExecuteWithTransaction(
                    AcadDoc.Database,
                    tr,
                    currTr =>
                    {
                        try
                        {
                            RXClass blockReferenceClass = RXObject.GetClass(typeof(BlockReference));

                            var allEntityIds = BlockRefGroup.GetAllEntityIds();

                            var reportTableHeaderBlockRef = allEntityIds
                                .Cast<ObjectId>()
                                .Where(id => id.ObjectClass.IsDerivedFrom(blockReferenceClass))
                                .Select(id => (BlockReference)tr.GetObject(id, OpenMode.ForRead))
                                .FirstOrDefault(br =>
                                    br.AttributeCollection
                                    .Cast<ObjectId>()
                                    .Select(id => (AttributeReference)tr.GetObject(id, OpenMode.ForRead))
                                    .Any(att =>
                                        att.Tag.Equals(
                                            "table_header_block_check_string",
                                            StringComparison.OrdinalIgnoreCase) &&
                                        att.TextString.Equals(
                                            "FoundOK",
                                            StringComparison.OrdinalIgnoreCase)
                                    ),
                                    null
                                );

                            return new TransactionResult<(bool, BlockReference)>((true, reportTableHeaderBlockRef), TransactionAction.Nothing);
                        }
                        catch (System.Exception e)
                        {
                            Debug.Print(e.ToString());
                            return new TransactionResult<(bool, BlockReference)>((false, null), TransactionAction.Nothing);
                        }
                    }
                );

            return operationResult.Result.OutValue;
        }

        public bool Refresh_ReportTable(Transaction tr = null)
        {
            if (!IsGrouped || ReportTableHeaderBlockRef == null) { return false; }

            using(var currTr = AcadDoc.Database.TransactionManager.StartTransaction())
            {
                try
                {
                    BlockTableRecord blockTableRecord = (BlockTableRecord)currTr.GetObject(ReportTableHeaderBlockRef.BlockTableRecord, OpenMode.ForRead);

                    RXClass tableClass = RXObject.GetClass(typeof(Table));

                    var reportHeaderTableObjectId = blockTableRecord
                        .Cast<ObjectId>()
                        .FirstOrDefault(id => id.ObjectClass.IsDerivedFrom(tableClass));

                    var reportHeaderTable = (Table)currTr.GetObject(reportHeaderTableObjectId, OpenMode.ForRead);

                    var attRefDict_HeaderBlock = PlugInBlockReference_Helpers.Get_AttRefDict(ReportTableHeaderBlockRef, currTr);

                    var excelFilePathType = PlugInBlockReference_Helpers
                        .TryGetValue_AttRefDict(attRefDict_HeaderBlock, "EXCEL_FILEPATH_TYPE")
                        ?.TextString
                        ?.Trim();

                    var excelFilePath = PlugInBlockReference_Helpers
                        .TryGetValue_AttRefDict(attRefDict_HeaderBlock, "EXCEL_FILEPATH")
                        ?.TextString
                        ?.Trim();

                    var excelSheetName = PlugInBlockReference_Helpers
                        .TryGetValue_AttRefDict(attRefDict_HeaderBlock, "EXCEL_SHEETNAME")
                        ?.TextString
                        ?.Trim();

                    var reportTableHandle = PlugInBlockReference_Helpers
                        .TryGetValue_AttRefDict(attRefDict_HeaderBlock, "REPORT_TABLE_HANDLE")
                        ?.TextString
                        ?.Trim();

                    if (reportHeaderTable == null || string.IsNullOrEmpty(excelFilePath)) { return false; }

                    var excelFilePathResolved = excelFilePathType switch
                    {
                        "ABSOLUTE" => excelFilePath,
                        "RELATIVE" => System.IO.Path.Combine(System.IO.Path.GetDirectoryName(AcadDoc.Name), excelFilePath),
                        _ => System.IO.Path.Combine(System.IO.Path.GetDirectoryName(AcadDoc.Name), excelFilePath)
                    };

                    var excelWorkBook = new XLWorkbook(excelFilePathResolved);

                    var excelWorkSheet = string.IsNullOrEmpty(excelSheetName) ? excelWorkBook.Worksheet(1) : excelWorkBook.Worksheet(excelSheetName);

                    var excelTable = excelWorkSheet.RangeUsed().AsTable();

                    var headerRow_HeaderBlock = reportHeaderTable.Rows[0];

                    var newTable = new Table() { TableStyle = reportHeaderTable.TableStyle };
                    newTable.SetSize(0, reportHeaderTable.Columns.Count);
                    newTable.SetRowHeight(headerRow_HeaderBlock.Height);
                    for (int i = 0; i < newTable.Columns.Count; i++)
                    {
                        newTable.Columns[i].Width = reportHeaderTable.Columns[i].Width;
                        newTable.Columns[i].TextHeight = reportHeaderTable.Columns[i].TextHeight;
                    }
                    newTable.Position = ReportTableHeaderBlockRef.Position;
                    newTable.ScaleFactors = ReportTableHeaderBlockRef.ScaleFactors;
                    newTable.Rotation = ReportTableHeaderBlockRef.Rotation;

                    foreach (var tableRow in excelTable.DataRange.Rows())
                    {
                        newTable.InsertRows(newTable.Rows.Count, headerRow_HeaderBlock.Height, 1);

                        foreach (var headerCell_HeaderBlock in headerRow_HeaderBlock)
                        {
                            var columnHeaderCell = reportHeaderTable.Cells[headerCell_HeaderBlock.Row, headerCell_HeaderBlock.Column];
                            var columnHeaderName = columnHeaderCell.TextString.Trim();

                            var excelCellValue = tableRow.Field(columnHeaderName).GetFormattedString();
                            var lastRow = newTable.Rows.Last();

                            newTable.Cells[newTable.Rows.Count, headerCell_HeaderBlock.Column].TextString = excelCellValue;
                        }

                        newTable.GenerateLayout();
                    }

                    Table reportTable = null;

                    if (!string.IsNullOrEmpty(reportTableHandle))
                    {
                        try
                        {
                            ObjectId reportTableId = AcadDoc.Database.GetObjectId(false, new Handle(Convert.ToInt64(reportTableHandle, 16)), 0);

                            if (!reportTableId.IsNull && !reportTableId.IsErased)
                            {
                                reportTable = currTr.GetObject(reportTableId, OpenMode.ForWrite) as Table;
                            }
                        }
                        catch (System.Exception e)
                        {
                            Debug.Print(e.ToString());
                            reportTable = null;
                        }
                    }

                    if (reportTable != null)
                    {
                        reportTable.HandOverTo(newTable, false, false);
                    }
                    else
                    {
                        var blockTable = (BlockTable)currTr.GetObject(AcadDoc.Database.BlockTableId, OpenMode.ForRead);
                        var modelSpaceId = blockTable[BlockTableRecord.ModelSpace];
                        var modelSpace = (BlockTableRecord)currTr.GetObject(modelSpaceId, OpenMode.ForWrite);
                        modelSpace.AppendEntity(newTable);
                        currTr.AddNewlyCreatedDBObject(newTable, true);

                        var handleAttRef = PlugInBlockReference_Helpers.TryGetValue_AttRefDict(attRefDict_HeaderBlock, "REPORT_TABLE_HANDLE");
                        handleAttRef.UpgradeOpen();
                        handleAttRef.TextString = newTable.Handle.ToString();

                    }

                    if (BlockRefGroup != null)
                    {
                        var group = (Group)currTr.GetObject(BlockRefGroup.ObjectId, OpenMode.ForWrite);

                        if (!group.Has(newTable))
                        {
                            group.Append(new ObjectIdCollection(new[] { newTable.ObjectId }));
                        }
                    }

                    currTr.Commit();

                    return true;
                }
                catch (System.Exception e)
                {
                    Debug.Print(e.ToString());
                    return false;
                }
            }

            //TransactionResult<bool> operationResult =
            //    PlugInBlockReference_Helpers.ExecuteWithTransaction(
            //        AcadDoc.Database,
            //        tr,
            //        currTr =>
            //        {
            //            try
            //            {
            //                BlockTableRecord blockTableRecord = (BlockTableRecord)currTr.GetObject(ReportTableHeaderBlockRef.BlockTableRecord, OpenMode.ForRead);

            //                RXClass tableClass = RXObject.GetClass(typeof(Table));

            //                var reportHeaderTableObjectId = blockTableRecord
            //                    .Cast<ObjectId>()
            //                    .FirstOrDefault(id => id.ObjectClass.IsDerivedFrom(tableClass));

            //                var reportHeaderTable = (Table)currTr.GetObject(reportHeaderTableObjectId, OpenMode.ForRead);

            //                var attRefDict_HeaderBlock = PlugInBlockReference_Helpers.Get_AttRefDict(ReportTableHeaderBlockRef, currTr);

            //                var excelFilePathType = PlugInBlockReference_Helpers
            //                    .TryGetValue_AttRefDict(attRefDict_HeaderBlock, "EXCEL_FILEPATH_TYPE")
            //                    ?.TextString
            //                    ?.Trim();

            //                var excelFilePath = PlugInBlockReference_Helpers
            //                    .TryGetValue_AttRefDict(attRefDict_HeaderBlock, "EXCEL_FILEPATH")
            //                    ?.TextString
            //                    ?.Trim();

            //                var excelSheetName = PlugInBlockReference_Helpers
            //                    .TryGetValue_AttRefDict(attRefDict_HeaderBlock, "EXCEL_SHEETNAME")
            //                    ?.TextString
            //                    ?.Trim();

            //                var reportTableHandle = PlugInBlockReference_Helpers
            //                    .TryGetValue_AttRefDict(attRefDict_HeaderBlock, "REPORT_TABLE_HANDLE")
            //                    ?.TextString
            //                    ?.Trim();

            //                if (reportHeaderTable == null || string.IsNullOrEmpty(excelFilePath)) { return new TransactionResult<bool>(false, TransactionAction.Nothing); }

            //                var excelFilePathResolved = excelFilePathType switch
            //                {
            //                    "ABSOLUTE" => excelFilePath,
            //                    "RELATIVE" => System.IO.Path.Combine(System.IO.Path.GetDirectoryName(AcadDoc.Name), excelFilePath),
            //                    _ => System.IO.Path.Combine(System.IO.Path.GetDirectoryName(AcadDoc.Name), excelFilePath)
            //                };

            //                var excelWorkBook = new XLWorkbook(excelFilePathResolved);

            //                var excelWorkSheet = string.IsNullOrEmpty(excelSheetName) ? excelWorkBook.Worksheet(1) : excelWorkBook.Worksheet(excelSheetName);

            //                var excelTable = excelWorkSheet.RangeUsed().AsTable();

            //                var headerRow_HeaderBlock = reportHeaderTable.Rows[0];

            //                var newTable = new Table(){ TableStyle = reportHeaderTable.TableStyle };
            //                newTable.SetSize(0, reportHeaderTable.Columns.Count);
            //                newTable.SetRowHeight(headerRow_HeaderBlock.Height);
            //                for (int i = 0; i < newTable.Columns.Count; i++) 
            //                { 
            //                    newTable.Columns[i].Width = reportHeaderTable.Columns[i].Width;
            //                    newTable.Columns[i].TextHeight = reportHeaderTable.Columns[i].TextHeight;
            //                }
            //                newTable.Position = ReportTableHeaderBlockRef.Position;
            //                newTable.ScaleFactors = ReportTableHeaderBlockRef.ScaleFactors;
            //                newTable.Rotation = ReportTableHeaderBlockRef.Rotation;

            //                foreach (var tableRow in excelTable.DataRange.Rows())
            //                {
            //                    newTable.InsertRows(newTable.Rows.Count, headerRow_HeaderBlock.Height, 1);

            //                    foreach (var headerCell_HeaderBlock in headerRow_HeaderBlock)
            //                    {
            //                        var columnHeaderCell = reportHeaderTable.Cells[headerCell_HeaderBlock.Row, headerCell_HeaderBlock.Column];
            //                        var columnHeaderName = columnHeaderCell.TextString.Trim();

            //                        var excelCellValue = tableRow.Field(columnHeaderName).GetFormattedString();
            //                        var lastRow = newTable.Rows.Last();

            //                        newTable.Cells[newTable.Rows.Count, headerCell_HeaderBlock.Column].TextString = excelCellValue;
            //                    }

            //                    newTable.GenerateLayout();
            //                }

            //                Table reportTable = null;

            //                if (!string.IsNullOrEmpty(reportTableHandle))
            //                {
            //                    try
            //                    {
            //                        ObjectId reportTableId = AcadDoc.Database.GetObjectId(false, new Handle(Convert.ToInt64(reportTableHandle, 16)), 0);

            //                        if (!reportTableId.IsNull && !reportTableId.IsErased)
            //                        {
            //                            reportTable = currTr.GetObject(reportTableId, OpenMode.ForWrite) as Table;
            //                        }
            //                    }
            //                    catch (System.Exception e)
            //                    {
            //                        Debug.Print(e.ToString());
            //                        reportTable = null;
            //                    }
            //                }

            //                if (reportTable != null)
            //                {
            //                    reportTable.HandOverTo(newTable, false, false);
            //                }
            //                else
            //                {
            //                    var blockTable = (BlockTable)currTr.GetObject(AcadDoc.Database.BlockTableId, OpenMode.ForRead);
            //                    var modelSpaceId = blockTable[BlockTableRecord.ModelSpace];
            //                    var modelSpace = (BlockTableRecord)currTr.GetObject(modelSpaceId, OpenMode.ForWrite);
            //                    modelSpace.AppendEntity(newTable);
            //                    currTr.AddNewlyCreatedDBObject(newTable, true);

            //                    var handleAttRef = PlugInBlockReference_Helpers.TryGetValue_AttRefDict(attRefDict_HeaderBlock, "REPORT_TABLE_HANDLE");
            //                    handleAttRef.UpgradeOpen();
            //                    handleAttRef.TextString = newTable.Handle.ToString();

            //                }

            //                if (BlockRefGroup != null)
            //                {
            //                    var group = (Group)currTr.GetObject(BlockRefGroup.ObjectId, OpenMode.ForWrite);

            //                    if (!group.Has(newTable))
            //                    {
            //                        group.Append(new ObjectIdCollection(new[] { newTable.ObjectId }));
            //                    }
            //                }

            //                return new TransactionResult<bool>(true, TransactionAction.Commit);
            //            }
            //            catch (System.Exception e)
            //            {
            //                Debug.Print(e.ToString());
            //                return new TransactionResult<bool>(false, TransactionAction.Nothing);
            //            }
            //        }
            //    );

            //return operationResult.Result;

        }

    }

}