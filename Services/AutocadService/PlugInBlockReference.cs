using Autocad_Primavera_P6_Plugin.Services.P6ApiService;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using AcadAppServ = Autodesk.AutoCAD.ApplicationServices;

namespace Autocad_Primavera_P6_Plugin.Services.AutocadService
{
    public class PluginBlockRefAutocadHelpers
    {
        public static Dictionary<string, AttributeDefinition> Get_AttDefDict(BlockTableRecord acadBlockTblRec, Transaction tr)
        {
            return acadBlockTblRec
                .Cast<ObjectId>()
                .Select(entityId => tr.GetObject(entityId, OpenMode.ForRead) as AttributeDefinition)
                .Where(attDef => attDef != null)
                .ToDictionary(
                    ad => ad.Tag,
                    ad => ad,
                    StringComparer.OrdinalIgnoreCase
                    );
        }

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

        public static AttributeReference TryGetValue_AttRefDict(Dictionary<string, AttributeReference> attributes, string tag)
        {
            attributes.TryGetValue(tag, out AttributeReference attribute);
            return attribute;
        }

        public static DynamicBlockReferenceProperty TryGetValue_DyBlockRefPropDict(Dictionary<string, DynamicBlockReferenceProperty> properties, string name)
        {
            properties.TryGetValue(name, out DynamicBlockReferenceProperty property);
            return property;
        }

        public static bool IsValidPluginBlock(Dictionary<string, AttributeReference> attdefDict)
        {
            var checkStringAttRef = PluginBlockRefAutocadHelpers.TryGetValue_AttRefDict(attdefDict, "Attribute_Check_String");

            return checkStringAttRef != null && string.Equals(checkStringAttRef.TextString, "FoundOK", StringComparison.OrdinalIgnoreCase);
        }

    };
    public class Slot
    {
        public string SlotPropName;
        public string SlotPropValue;
        public string NameAttRefTag;
        public AttributeReference NameAttRef;
        public string ValueAttRefTag;
        public AttributeReference ValueAttRef;
    }

    public static class DefaultPropSlotName
    {
        public static readonly Dictionary<string, string> DefaultPropSlotDict = new()
        {
            ["Slot01"] = "BOUNDARY_CODE_ID",
            ["Slot02"] = "BOUNDARY_CODE_PATH",
            ["Slot03"] = "BOUNDARY_CODE_VALUE",
            ["Slot04"] = "ELEMENT_ID_CODE_ID",
            ["Slot05"] = "ELEMENT_ID_CODE_PATH",
            ["Slot06"] = "ELEMENT_ID_CODE_VALUE",
            ["Slot07"] = "LENGTH_L",
            ["Slot08"] = "BREADTH_B",
            ["Slot09"] = "HEIGHT_H"
        };
    }

    public enum InitStateEnum
    {
        NotInitialized,
        DefaultInitialized,
        BTRInitialized,
        BlockRefInitialized
    }

    /// <summary>Live attribute and dynamic-property handles for an instance-bound block.</summary>
    public class AttProps
    {
        public AttributeReference Attribute_Check_String;
        public AttributeReference ElementId;
        public AttributeReference BlockType;
        public DynamicBlockReferenceProperty Update_Status;
        public DynamicBlockReferenceProperty Moveinfo_X;
        public DynamicBlockReferenceProperty Moveinfo_Y;
        public Dictionary<string, string> PropSlotDict = new(StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>
    /// A plugin block through its lifecycle: blank data container, definition-bound
    /// template, then a live instance-bound reference. Callers own the document lock
    /// and transaction; AutoCAD DBObjects exposed by BlockAttProps are valid only for
    /// the transaction used to call Init, AttachBlockReference, or CreateBlockReference.
    /// </summary>
    public class PlugInBlockReference
    {
        private MyPlugin PluginInstance = null;
        public InitStateEnum InitState { get; private set; } = InitStateEnum.NotInitialized;
        private AcadAppServ.Document AcadDoc = null;
        public BlockTableRecord AcadBlockTblRec = null;
        public BlockReference AcadBlockRef = null;
        private Point3d? PluginBlockPosition = null;
        private Point3d? InfoPosition = null;
        private Dictionary<string, Slot> SlotsDict = new() { ["Slot01"] = null, ["Slot02"] = null, ["Slot03"] = null, ["Slot04"] = null, ["Slot05"] = null, ["Slot06"] = null, ["Slot07"] = null, ["Slot08"] = null, ["Slot09"] = null, ["Slot10"] = null, ["Slot11"] = null, ["Slot12"] = null, ["Slot13"] = null, ["Slot14"] = null, ["Slot15"] = null };
        private AttProps BlockAttProps = null;

        private ActivityCode BdryActCode = null;
        private ActivityCode ElementIdCode = null;

        public IList<string> LoadWarnings = new List<string>();

        /// <summary>Creates a blank object. Attach a BTR or block reference later.</summary>
        public PlugInBlockReference(MyPlugin pluginInstance, AcadAppServ.Document acadDoc)
        {
            PluginInstance = pluginInstance ?? throw new ArgumentNullException(nameof(pluginInstance));
            AcadDoc = acadDoc ?? throw new ArgumentNullException(nameof(acadDoc));
            Init_PlugInBlockReference(InitStateEnum.DefaultInitialized);
        }

        /// <summary>Creates a definition-bound object. Call Init with the caller transaction.</summary>
        public PlugInBlockReference(MyPlugin pluginInstance, AcadAppServ.Document acadDoc, BlockTableRecord blockTableRecord, Transaction tr)
        {
            PluginInstance = pluginInstance ?? throw new ArgumentNullException(nameof(pluginInstance));
            AcadDoc = acadDoc ?? throw new ArgumentNullException(nameof(acadDoc));
            AcadBlockTblRec = blockTableRecord ?? throw new ArgumentNullException(nameof(blockTableRecord));
            Init_PlugInBlockReference(InitStateEnum.BTRInitialized, tr);
        }

        /// <summary>Creates a reference-bound object. Call Init with the caller transaction.</summary>
        public PlugInBlockReference(MyPlugin pluginInstance, AcadAppServ.Document acadDoc, BlockReference blockRef, Transaction tr)
        {
            PluginInstance = pluginInstance ?? throw new ArgumentNullException(nameof(pluginInstance));
            AcadDoc = acadDoc ?? throw new ArgumentNullException(nameof(acadDoc));
            AcadBlockRef = blockRef ?? throw new ArgumentNullException(nameof(blockRef));
            Init_PlugInBlockReference(InitStateEnum.BlockRefInitialized, tr);
        }

        private void Init_PlugInBlockReference(InitStateEnum refInitState, Transaction tr = null)
        {
            bool needsTransaction = refInitState == InitStateEnum.BTRInitialized || refInitState == InitStateEnum.BlockRefInitialized;

            if (needsTransaction && tr == null) { throw new ArgumentNullException(nameof(tr)); }
            ;

            BlockAttProps = new AttProps();

            switch (refInitState)
            {
                case InitStateEnum.DefaultInitialized:
                    LoadDefaultSlots();
                    InitState = refInitState;
                    break;

                case InitStateEnum.BTRInitialized:
                    Init_By_BTR(tr);
                    InitState = refInitState;
                    break;

                case InitStateEnum.BlockRefInitialized:
                    Init_By_BlockRef(tr);
                    InitState = refInitState;
                    break;

                default:
                    Debug.Print("Init_PlugInBlockReference called with unhandled InitState: " + refInitState);
                    return;
            }
            ;
        }

        private void LoadDefaultSlots()
        {
            var defaultDict = DefaultPropSlotName.DefaultPropSlotDict;

            foreach (var key in SlotsDict.Keys.ToList())
            {
                int slotNumber = ParseSlotNumber(key);
                string nameTag = SlotNameTag(slotNumber);
                string valueTag = SlotValueTag(slotNumber);

                string defaultPropName = defaultDict.TryGetValue(key, out var propName) ? propName : string.Empty;

                if (!string.IsNullOrEmpty(defaultPropName))
                {
                    SlotsDict[key] = new Slot()
                    {
                        SlotPropName = defaultPropName,
                        NameAttRefTag = nameTag,
                        ValueAttRefTag = valueTag
                    };
                }
            }
            ;

            Reset_PropSlotDict();

        }

        private void Init_By_BTR(Transaction tr)
        {
            var blockTableRecord = (BlockTableRecord)tr.GetObject(AcadBlockTblRec.ObjectId, OpenMode.ForRead);

            var definitions = new Dictionary<string, AttributeDefinition>(StringComparer.OrdinalIgnoreCase);

            foreach (ObjectId entityId in blockTableRecord)
            {
                var attDef = tr.GetObject(entityId, OpenMode.ForRead) as AttributeDefinition;
                var attDefTag = attDef?.Tag?.Trim() ?? string.Empty;

                if (attDef != null && !String.IsNullOrWhiteSpace(attDefTag) && !definitions.ContainsKey(attDefTag))
                {
                    definitions.Add(attDef.Tag, attDef);
                }
                ;

            }
            ;

            foreach (var key in SlotsDict.Keys.ToList())
            {
                int slotNumber = ParseSlotNumber(key);
                string nameTag = SlotNameTag(slotNumber);
                string valueTag = SlotValueTag(slotNumber);

                if (!definitions.TryGetValue(nameTag, out AttributeDefinition NameAttDef) || !definitions.ContainsKey(valueTag)) { continue; }
                ;

                SlotsDict[key] = new Slot()
                {
                    SlotPropName = NameAttDef.TextString,
                    NameAttRefTag = nameTag,
                    ValueAttRefTag = valueTag
                };
            }
            ;

            Reset_PropSlotDict();

            AcadBlockTblRec = blockTableRecord;
        }

        private void Init_By_BlockRef(Transaction tr)
        {
            var blockReference = (BlockReference)tr.GetObject(AcadBlockRef.ObjectId, OpenMode.ForRead);

            var attDefDict = PluginBlockRefAutocadHelpers.Get_AttRefDict(blockReference, tr);
            var dyBlockRefPropDict = PluginBlockRefAutocadHelpers.Get_DyBlockRefPropDict(blockReference);

            BlockAttProps.Attribute_Check_String = PluginBlockRefAutocadHelpers.TryGetValue_AttRefDict(attDefDict, "Attribute_Check_String");
            BlockAttProps.ElementId = PluginBlockRefAutocadHelpers.TryGetValue_AttRefDict(attDefDict, "ELEMENT_ID");
            BlockAttProps.BlockType = PluginBlockRefAutocadHelpers.TryGetValue_AttRefDict(attDefDict, "BLOCK_TYPE");
            BlockAttProps.Update_Status = PluginBlockRefAutocadHelpers.TryGetValue_DyBlockRefPropDict(dyBlockRefPropDict, "Update Status");
            BlockAttProps.Moveinfo_X = PluginBlockRefAutocadHelpers.TryGetValue_DyBlockRefPropDict(dyBlockRefPropDict, "MoveInfo X");
            BlockAttProps.Moveinfo_Y = PluginBlockRefAutocadHelpers.TryGetValue_DyBlockRefPropDict(dyBlockRefPropDict, "MoveInfo Y");

            foreach (var key in SlotsDict.Keys.ToList())
            {
                int slotNumber = ParseSlotNumber(key);
                string nameTag = SlotNameTag(slotNumber);
                string valueTag = SlotValueTag(slotNumber);
                var nameAttribute = PluginBlockRefAutocadHelpers.TryGetValue_AttRefDict(attDefDict, nameTag);
                var valueAttribute = PluginBlockRefAutocadHelpers.TryGetValue_AttRefDict(attDefDict, valueTag);

                if (nameAttribute == null || valueAttribute == null || String.IsNullOrWhiteSpace(nameAttribute.TextString)) { continue; };

                SlotsDict[key] = new Slot()
                {
                    SlotPropName = nameAttribute.TextString,
                    SlotPropValue = valueAttribute.TextString,
                    NameAttRefTag = nameTag,
                    NameAttRef = nameAttribute,
                    ValueAttRefTag = valueTag,
                    ValueAttRef = valueAttribute
                };

            }
            ;

            Reset_PropSlotDict();

            AcadBlockRef = blockReference;
            AcadBlockTblRec = (BlockTableRecord)tr.GetObject(blockReference.BlockTableRecord, OpenMode.ForRead);

        }

        public async Task AsyncInit()
        {
            switch (InitState)
            {
                //case InitStateEnum.DefaultInitialized:
                //    break;

                //case InitStateEnum.BTRInitialized:
                //    break;

                case InitStateEnum.BlockRefInitialized:
                    _ = await Get_BdryActCode();
                    _ = await Get_ElementIdCode();
                    break;

                default:
                    Debug.Print("AsyncInit called with unhandled InitState: " + InitState);
                    return;
            }
            ;

        }

        public Point3d? Get_BlockPosition()
        {
            if (InitState == InitStateEnum.BlockRefInitialized)
            {
                return AcadBlockRef.Position;
            }
            else
            {
                return PluginBlockPosition;
            }
            ;
        }

        public bool Set_BlockPosition(Point3d newPosition, out Point3d? resultPosition, Transaction tr = null)
        {
            resultPosition = null;

            switch (InitState)
            {
                case InitStateEnum.NotInitialized:
                    return false;

                case InitStateEnum.DefaultInitialized:
                    PluginBlockPosition = newPosition;
                    resultPosition = PluginBlockPosition;
                    return true;

                case InitStateEnum.BTRInitialized:
                    PluginBlockPosition = newPosition;
                    resultPosition = PluginBlockPosition;
                    return true;

                case InitStateEnum.BlockRefInitialized:
                    if (AcadBlockRef == null || tr == null) { return false; }

                    var blockRef = (BlockReference)tr.GetObject(AcadBlockRef.ObjectId, OpenMode.ForWrite);

                    blockRef.Position = newPosition;

                    AcadBlockRef = blockRef;
                    PluginBlockPosition = newPosition;
                    resultPosition = PluginBlockPosition;

                    return true;

                default:
                    return false;
            }
            ;

        }

        public Point3d? Get_InfoPosition()
        {
            if (InitState != InitStateEnum.BlockRefInitialized)
            {
                return InfoPosition;
            }
            ;

            if (AcadBlockRef == null ||
                BlockAttProps?.Moveinfo_X == null ||
                BlockAttProps?.Moveinfo_Y == null ||
                BlockAttProps.Moveinfo_X.Value == null ||
                BlockAttProps.Moveinfo_Y.Value == null)
            {
                return null;
            }
            ;

            try
            {
                double moveInfoX = Convert.ToDouble(BlockAttProps.Moveinfo_X.Value);
                double moveInfoY = Convert.ToDouble(BlockAttProps.Moveinfo_Y.Value);
                Point3d blockPosition = AcadBlockRef.Position;

                return new Point3d(
                    blockPosition.X + moveInfoX,
                    blockPosition.Y + moveInfoY,
                    blockPosition.Z);
            }
            catch (Exception)
            {
                return null;
            }
            ;
        }

        public bool Set_InfoPosition(Point3d newPosition, out Point3d? resultPosition, Transaction tr = null)
        {
            resultPosition = null;

            switch (InitState)
            {
                case InitStateEnum.NotInitialized:
                    return false;

                case InitStateEnum.DefaultInitialized:
                case InitStateEnum.BTRInitialized:
                    InfoPosition = newPosition;
                    resultPosition = InfoPosition;
                    return true;

                case InitStateEnum.BlockRefInitialized:
                    if (AcadBlockRef == null || tr == null) { return false; }
                    ;

                    var blockRef = (BlockReference)tr.GetObject(AcadBlockRef.ObjectId, OpenMode.ForWrite);
                    var dyBlockRefPropDict = PluginBlockRefAutocadHelpers.Get_DyBlockRefPropDict(blockRef);

                    var moveInfoX = PluginBlockRefAutocadHelpers.TryGetValue_DyBlockRefPropDict(dyBlockRefPropDict, "MoveInfo X");
                    var moveInfoY = PluginBlockRefAutocadHelpers.TryGetValue_DyBlockRefPropDict(dyBlockRefPropDict, "MoveInfo Y");

                    if (moveInfoX == null || moveInfoY == null || moveInfoX.ReadOnly || moveInfoY.ReadOnly) { return false; }
                    ;

                    Point3d blockPosition = blockRef.Position;

                    var delta = newPosition - blockPosition;

                    moveInfoX.Value = delta.X;
                    moveInfoY.Value = delta.Y;

                    BlockAttProps.Moveinfo_X = moveInfoX;
                    BlockAttProps.Moveinfo_Y = moveInfoY;

                    InfoPosition = new Point3d(newPosition.X, newPosition.Y, blockPosition.Z);

                    resultPosition = InfoPosition;
                    return true;

                default:
                    return false;
            }
        }

        private static int ParseSlotNumber(string key)
        {
            return int.Parse(key[^2..]);
        }

        private static string SlotNameTag(int slotNumber)
        {
            return "ATT_" + slotNumber + "_NAME";
        }

        private static string SlotValueTag(int slotNumber)
        {
            return "ATT_" + slotNumber + "_VALUE";
        }

        private void Reset_PropSlotDict()
        {
            BlockAttProps.PropSlotDict.Clear();

            foreach (var entry in SlotsDict)
            {
                string propertyName = entry.Value?.SlotPropName?.Trim() ?? String.Empty;
                if (propertyName.Length > 0 && !BlockAttProps.PropSlotDict.ContainsKey(propertyName))
                {
                    BlockAttProps.PropSlotDict.Add(propertyName, entry.Key);
                }
            }
            ;

        }

        private void SetBlockTypeFromStatus(Transaction tr)
        {
            if (tr == null || BlockAttProps.BlockType == null || BlockAttProps.Update_Status == null) { return; }
            ;

            string status = Convert.ToString(BlockAttProps.Update_Status.Value);

            if (string.Equals(BlockAttProps.BlockType.TextString, status, StringComparison.Ordinal)) { return; }
            ;

            var blockType = (AttributeReference)tr.GetObject(BlockAttProps.BlockType.ObjectId, OpenMode.ForWrite);
            blockType.TextString = status ?? string.Empty;
            BlockAttProps.BlockType = blockType;
        }

        public async Task<ActivityCode> Get_BdryActCode()
        {
            var boundaryCodeIdString = Get_SlotProperty("BOUNDARY_CODE_ID");
            var isBoundaryCodeId = int.TryParse(boundaryCodeIdString, out int boundaryCodeId);

            if (!isBoundaryCodeId) { return null; }
            ;

            if (BdryActCode != null && BdryActCode.ObjectId == boundaryCodeId) { return BdryActCode; }
            ;

            var p6ApiService = PluginInstance.MyP6ApiService;
            var actCode = await p6ApiService.GetP6ActivityCodeById(boundaryCodeId);

            if (actCode == null) { return null; }
            ;

            BdryActCode = actCode;
            return actCode;
        }

        public bool Set_BdryActCode(ActivityCode actCode, out ActivityCode result, Transaction tr = null)
        {
            result = null;

            var boundaryCodeId = actCode.ObjectId.ToString();
            var boundaryCodeValue = actCode.Description;
            var boundaryCodePath = actCode.CodeConcatName;

            var idResult = Set_SlotProperty("BOUNDARY_CODE_ID", boundaryCodeId, out _, tr);
            var valueResult = Set_SlotProperty("BOUNDARY_CODE_VALUE", boundaryCodeValue, out _, tr);
            var pathResult = Set_SlotProperty("BOUNDARY_CODE_PATH", boundaryCodePath, out _, tr);

            if (!idResult || !valueResult || !pathResult) { return false; };

            BdryActCode = actCode;

            result = actCode;
            return true;
        }

        public async Task<ActivityCode> Get_ElementIdCode()
        {
            var elementIdCodeIdString = Get_SlotProperty("ELEMENT_ID_CODE_ID");
            var isElementIdCodeId = int.TryParse(elementIdCodeIdString, out int elementIdCodeId);

            if (!isElementIdCodeId) { return null; }
            ;

            if (ElementIdCode != null && ElementIdCode.ObjectId == elementIdCodeId) { return ElementIdCode; }
            ;

            var p6ApiService = PluginInstance.MyP6ApiService;
            var actCode = await p6ApiService.GetP6ActivityCodeById(elementIdCodeId);

            if (actCode == null) { return null; }
            ;

            ElementIdCode = actCode;
            return actCode;
        }

        public bool Set_ElementIdCode(ActivityCode actCode, out ActivityCode result, Transaction tr = null)
        {
            result = null;

            var elementIdCodeId = actCode.ObjectId.ToString();
            var elementIdCodeValue = actCode.Description;
            var elementIdCodePath = actCode.CodeConcatName;

            var idResult = Set_SlotProperty("ELEMENT_ID_CODE_ID", elementIdCodeId, out _, tr);
            var valueResult = Set_SlotProperty("ELEMENT_ID_CODE_VALUE", elementIdCodeValue, out _, tr);
            var pathResult = Set_SlotProperty("ELEMENT_ID_CODE_PATH", elementIdCodePath, out _, tr);

            if (!idResult || !valueResult || !pathResult) { return false; };

            ElementIdCode = actCode;

            var setElementIdResult = Set_ElementId(out _, null, tr);
            
            result = actCode;
            return true;
        }

        public string Get_SlotProperty(string propName)
        {
            var propSlotDict = BlockAttProps.PropSlotDict;
            var slotFound = propSlotDict.TryGetValue(propName, out string slotKey);

            if (!slotFound || slotKey == null || String.IsNullOrWhiteSpace(slotKey)) { return null; }
            ;

            var slot = SlotsDict[slotKey];

            if (slot == null) { return null; }
            ;

            var SlotPropertyValueString = InitState switch
            {
                InitStateEnum.NotInitialized => null,
                InitStateEnum.DefaultInitialized => slot.SlotPropValue,
                InitStateEnum.BTRInitialized => slot.SlotPropValue,
                InitStateEnum.BlockRefInitialized => slot.ValueAttRef?.TextString,
                _ => null
            };

            return SlotPropertyValueString;
        }

        public bool Set_SlotProperty(string propName, string value, out string result, Transaction tr = null)
        {
            result = null;

            if (String.IsNullOrWhiteSpace(propName)) { return false; }
            ;

            var propSlotDict = BlockAttProps.PropSlotDict;
            var slotFound = propSlotDict.TryGetValue(propName, out string slotKey);

            if (!slotFound || slotKey == null || String.IsNullOrWhiteSpace(slotKey)) { return false; }

            var slot = SlotsDict[slotKey];

            if (slot == null) { return false; }

            switch (InitState)
            {
                case InitStateEnum.NotInitialized:
                    return false;

                case InitStateEnum.DefaultInitialized:
                    slot.SlotPropValue = value;
                    result = value;
                    return true;

                case InitStateEnum.BTRInitialized:
                    slot.SlotPropValue = value;
                    result = value;
                    return true;

                case InitStateEnum.BlockRefInitialized:
                    if (tr == null) { Debug.Print("Transaction is null"); return false; }
                    ;

                    var valueAttRef = (AttributeReference)tr.GetObject(slot.ValueAttRef.ObjectId, OpenMode.ForWrite);
                    valueAttRef.TextString = value;
                    slot.ValueAttRef = valueAttRef;
                    slot.SlotPropValue = value;
                    result = value;
                    return true;

                default:
                    return false;
            }
            ;

        }

        public object Get_Update_Status()
        {
            if (InitState != InitStateEnum.BlockRefInitialized || BlockAttProps.Update_Status == null)
            {
                return null;
            }

            return BlockAttProps.Update_Status.Value;
        }

        public bool Set_Update_Status(object value, out object result, Transaction tr = null)
        {
            result = null;

            if (InitState != InitStateEnum.BlockRefInitialized || BlockAttProps.Update_Status == null) { return false; };

            if (tr == null) { Debug.Print("Transaction is null"); return false; };

            if (!TryGetWritableDynamicProperty(tr, "Update Status", out var writableProp)) { return false; };

            writableProp.Value = value;
            BlockAttProps.Update_Status = writableProp;

            result = value;
            return true;
        }

        // ── Shared helper ────────────────────────────────────────────────────────────
        // DynamicBlockReferenceProperty isn't a DBObject, so unlike AttributeReference
        // it can't be reopened by ObjectId. The owning BlockReference must be reopened
        // ForWrite, then the matching property re-fetched from its live collection.
        private bool TryGetWritableDynamicProperty(Transaction tr, string propertyName, out DynamicBlockReferenceProperty property)
        {
            property = default;

            if (InitState != InitStateEnum.BlockRefInitialized || AcadBlockRef == null)
            {
                return false;
            }

            var blockRef = (BlockReference)tr.GetObject(AcadBlockRef.ObjectId, OpenMode.ForWrite);

            foreach (DynamicBlockReferenceProperty candidate in blockRef.DynamicBlockReferencePropertyCollection)
            {
                if (string.Equals(candidate.PropertyName, propertyName, StringComparison.OrdinalIgnoreCase))
                {
                    property = candidate;
                    return true;
                }
            }

            return false;
        }

        public string Get_ElementId()
        {
            return InitState switch
            {
                InitStateEnum.DefaultInitialized => null,
                InitStateEnum.BTRInitialized => null,
                InitStateEnum.BlockRefInitialized => BlockAttProps?.ElementId?.TextString,
                _ => null
            };
        }

        public bool Set_ElementId(out string result, string value = null, Transaction tr = null)
        {
            result = null;

            switch (InitState)
            {
                case InitStateEnum.BlockRefInitialized:
                    var elementIdCodePathString = Get_SlotProperty("ELEMENT_ID_CODE_PATH");
                    var elementIdPathString = Get_ElementIdString(elementIdCodePathString);

                    var writableAttribute = (AttributeReference)tr.GetObject(BlockAttProps.ElementId.ObjectId, OpenMode.ForWrite);

                    var finalValueString = value ?? elementIdPathString ?? string.Empty;

                    writableAttribute.TextString = finalValueString;
                    BlockAttProps.ElementId = writableAttribute;
                    result = finalValueString;

                    return true;

                default:
                    return false;
            }

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

        }

        public string Get_BlockType()
        {
            return InitState switch
            {
                InitStateEnum.DefaultInitialized => null,
                InitStateEnum.BTRInitialized => null,
                InitStateEnum.BlockRefInitialized => BlockAttProps?.BlockType?.TextString,
                _ => null
            };
        }

        public bool Set_BlockType(out string result, string value = null, Transaction tr = null)
        {
            result = null;

            switch (InitState)
            {
                case InitStateEnum.BlockRefInitialized:
                    var updateStatus = BlockAttProps.Update_Status;

                    if (value != null || !string.IsNullOrWhiteSpace(value))
                    {
                        var writableAttribute = (AttributeReference)tr.GetObject(BlockAttProps.BlockType.ObjectId, OpenMode.ForWrite);
                        writableAttribute.TextString = updateStatus.Value?.ToString();
                        result = updateStatus.Value?.ToString();

                        return true;
                    }
                    else
                    {
                        var allowedStatuses = updateStatus.GetAllowedValues();

                        var isAllowedValue = allowedStatuses.Any(v => string.Equals(v.ToString(), value, StringComparison.OrdinalIgnoreCase));
                        if (!isAllowedValue) { return false; }
                        ;

                        var isSet_Update_Status = Set_Update_Status(value, out object Set_Update_Status_Result, tr);

                        var writableAttribute = (AttributeReference)tr.GetObject(BlockAttProps.BlockType.ObjectId, OpenMode.ForWrite);
                        writableAttribute.TextString = Set_Update_Status_Result?.ToString();
                        result = Set_Update_Status_Result?.ToString();

                        return true;
                    }
                    ;

                default:
                    return false;
            }
            ;

        }

        public PlugInBlockReference InsertAsNewBlockRef(Transaction tr)
        {
            switch (InitState)
            {
                case InitStateEnum.NotInitialized:
                case InitStateEnum.DefaultInitialized:
                    return null;

                case InitStateEnum.BTRInitialized:
                    var modelSpaceBTR = (BlockTableRecord)tr.GetObject(SymbolUtilityServices.GetBlockModelSpaceId(AcadDoc.Database), OpenMode.ForWrite);

                    var BTRattDefDict = PluginBlockRefAutocadHelpers.Get_AttDefDict(AcadBlockTblRec, tr);

                    var newBlockRef = new BlockReference(PluginBlockPosition.Value, AcadBlockTblRec.ObjectId);

                    modelSpaceBTR.AppendEntity(newBlockRef);
                    tr.AddNewlyCreatedDBObject(newBlockRef, true);

                    var replaceAttRefDict = new Dictionary<string, AttributeReference>();

                    if (BlockAttProps.Attribute_Check_String != null) { replaceAttRefDict.Add(BlockAttProps.Attribute_Check_String.Tag, BlockAttProps.Attribute_Check_String); }
                    ;
                    if (BlockAttProps.ElementId != null) { replaceAttRefDict.Add(BlockAttProps.ElementId.Tag, BlockAttProps.ElementId); }
                    ;
                    if (BlockAttProps.BlockType != null) { replaceAttRefDict.Add(BlockAttProps.BlockType.Tag, BlockAttProps.BlockType); }
                    ;

                    foreach (var slotKeyVal in SlotsDict)
                    {
                        var slot = slotKeyVal.Value;

                        if (slot == null) { continue; }
                        ;
                        if ((slot.NameAttRef == null || String.IsNullOrWhiteSpace(slot.NameAttRef.TextString)) && String.IsNullOrWhiteSpace(slot.SlotPropName)) { continue; }
                        ;
                        if ((slot.ValueAttRef == null || String.IsNullOrWhiteSpace(slot.ValueAttRef.TextString)) && String.IsNullOrWhiteSpace(slot.SlotPropValue)) { continue; }

                        if (slot.NameAttRef != null)
                        {
                            replaceAttRefDict.Add(slot.NameAttRef.TextString, slot.NameAttRef);
                        }
                        else
                        {
                            replaceAttRefDict.Add(slot.NameAttRefTag, new AttributeReference() { Tag = slot.NameAttRefTag, TextString = slot.SlotPropName });
                        }
                        ;

                        if (slot.ValueAttRef != null)
                        {
                            replaceAttRefDict.Add(slot.ValueAttRef.TextString, slot.ValueAttRef);
                        }
                        else
                        {
                            replaceAttRefDict.Add(slot.ValueAttRefTag, new AttributeReference() { Tag = slot.ValueAttRefTag, TextString = slot.SlotPropValue });
                        }
                        ;

                    }
                    ;


                    foreach (var BTRattDefKeyVal in BTRattDefDict)
                    {
                        var newAttRef = new AttributeReference();
                        newAttRef.SetAttributeFromBlock(BTRattDefKeyVal.Value, newBlockRef.BlockTransform);

                        var isValueUpdate = replaceAttRefDict.TryGetValue(BTRattDefKeyVal.Key, out var replaceAttDef);

                        if (isValueUpdate) { newAttRef.TextString = replaceAttDef.TextString; }
                        ;

                        newBlockRef.AttributeCollection.AppendAttribute(newAttRef);
                    }
                    ;

                    var newPluginBlockRef = new PlugInBlockReference(PluginInstance, AcadDoc, newBlockRef, tr);

                    newPluginBlockRef.Set_BdryActCode(BdryActCode, out _, tr);
                    newPluginBlockRef.Set_ElementIdCode(ElementIdCode, out _, tr);

                    return newPluginBlockRef;

                case InitStateEnum.BlockRefInitialized:
                    return null;

                default:
                    return null;

            }
            ;

        }

    }
}
