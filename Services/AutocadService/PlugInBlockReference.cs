using Autocad_Primavera_P6_Plugin.Services.P6ApiService;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using System.Xml.Linq;
using AcadAppServ = Autodesk.AutoCAD.ApplicationServices;

namespace Autocad_Primavera_P6_Plugin.Services.AutocadService
{
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
        private BlockTableRecord AcadBlockTblRec = null;
        private BlockReference AcadBlockRef = null;
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

            if (needsTransaction && tr == null){ throw new ArgumentNullException(nameof(tr)); };

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
            };
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
            };

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
                };

            };

            foreach (var key in SlotsDict.Keys.ToList())
            {
                int slotNumber = ParseSlotNumber(key);
                string nameTag = SlotNameTag(slotNumber);
                string valueTag = SlotValueTag(slotNumber);

                if (!definitions.TryGetValue(nameTag, out AttributeDefinition NameAttDef) ||
                    !definitions.ContainsKey(valueTag))
                {
                    continue;
                };

                SlotsDict[key] = new Slot()
                {
                    SlotPropName = NameAttDef.TextString,
                    NameAttRefTag = nameTag,
                    ValueAttRefTag = valueTag
                };
            };

            Reset_PropSlotDict();

            AcadBlockTblRec = blockTableRecord;
        }

        private void Init_By_BlockRef(Transaction tr)
        {
            var blockReference = (BlockReference)tr.GetObject(AcadBlockRef.ObjectId, OpenMode.ForRead);

            var attDefDict = Get_AttDefDict(blockReference, tr);
            var dyBlockRefPropDict = Get_DyBlockRefPropDict(blockReference);

            BlockAttProps.Attribute_Check_String = TryGetValue_AttDefDict(attDefDict, "Attribute_Check_String");
            BlockAttProps.ElementId = TryGetValue_AttDefDict(attDefDict, "ELEMENT_ID");
            BlockAttProps.BlockType = TryGetValue_AttDefDict(attDefDict, "BLOCK_TYPE");
            BlockAttProps.Update_Status = TryGetValue_DyBlockRefPropDict(dyBlockRefPropDict, "Update Status");
            BlockAttProps.Moveinfo_X = TryGetValue_DyBlockRefPropDict(dyBlockRefPropDict, "MoveInfo X");
            BlockAttProps.Moveinfo_Y = TryGetValue_DyBlockRefPropDict(dyBlockRefPropDict, "MoveInfo Y");

            foreach (var key in SlotsDict.Keys.ToList())
            {
                int slotNumber = ParseSlotNumber(key);
                string nameTag = SlotNameTag(slotNumber);
                string valueTag = SlotValueTag(slotNumber);
                var nameAttribute = TryGetValue_AttDefDict(attDefDict, nameTag);
                var valueAttribute = TryGetValue_AttDefDict(attDefDict, valueTag);

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

            };

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
                    var bdryActCode = await Get_BdryActCode();
                    var elementIdCode = await Get_ElementIdCode();
                    break;

                default:
                    Debug.Print("AsyncInit called with unhandled InitState: " + InitState);
                    return;
            };

        }

        // Rough Below

        ///// <summary>
        ///// Inserts this template into ownerSpace, creates its AttributeReferences, applies
        ///// supplied values, and upgrades this same object to instance-bound state.
        ///// Returns tags that were requested but absent from the block definition.
        ///// </summary>
        //public ObjectId CreateBlockReference(
        //    ObjectId ownerSpaceId,
        //    Point3d insertionPoint,
        //    IDictionary<string, string> attributeValues,
        //    Transaction tr,
        //    out IList<string> missingAttributeTags)
        //{
        //    if (tr == null)
        //    {
        //        throw new ArgumentNullException(nameof(tr));
        //    }
        //    if (InitState != InitStateEnum.BTRInitialized)
        //    {
        //        throw new InvalidOperationException("A block table record must be attached before creating a block reference.");
        //    }

        //    EnsureBound(AcadBlockTblRec, "block table record");
        //    var ownerSpace = (BlockTableRecord)tr.GetObject(ownerSpaceId, OpenMode.ForWrite);
        //    var blockDefinition = (BlockTableRecord)tr.GetObject(AcadBlockTblRec, OpenMode.ForRead);
        //    var blockReference = new BlockReference(insertionPoint, AcadBlockTblRec);
        //    ownerSpace.AppendEntity(blockReference);
        //    tr.AddNewlyCreatedDBObject(blockReference, true);

        //    foreach (ObjectId entityId in blockDefinition)
        //    {
        //        var definition = tr.GetObject(entityId, OpenMode.ForRead) as AttributeDefinition;
        //        if (definition == null || definition.Constant)
        //        {
        //            continue;
        //        }

        //        var attributeReference = new AttributeReference();
        //        attributeReference.SetAttributeFromBlock(definition, blockReference.BlockTransform);
        //        blockReference.AttributeCollection.AppendAttribute(attributeReference);
        //        tr.AddNewlyCreatedDBObject(attributeReference, true);
        //    }

        //    AttachBlockReference(blockReference);
        //    Init(tr);
        //    missingAttributeTags = ApplyAttributeValues(attributeValues, tr);
        //    Init(tr); // Reload the public handles after any attributes were opened for write.
        //    return AcadBlockRef;
        //}

        ///// <summary>Writes only existing AttributeReferences; it never fabricates an orphan attribute.</summary>
        //public IList<string> ApplyAttributeValues(IDictionary<string, string> attributeValues, Transaction tr)
        //{
        //    if (attributeValues == null || attributeValues.Count == 0)
        //    {
        //        return new List<string>();
        //    }
        //    if (tr == null)
        //    {
        //        throw new ArgumentNullException(nameof(tr));
        //    }
        //    if (InitState != InitStateEnum.BlockRefInitialized)
        //    {
        //        throw new InvalidOperationException("A block reference must be attached before writing attributes.");
        //    }

        //    var blockReference = (BlockReference)tr.GetObject(AcadBlockRef, OpenMode.ForRead);
        //    var attributes = ReadAttributeReferences(blockReference, tr, OpenMode.ForWrite);
        //    var missing = new List<string>();
        //    foreach (var value in attributeValues)
        //    {
        //        if (!attributes.TryGetValue(value.Key, out AttributeReference attributeReference))
        //        {
        //            missing.Add(value.Key);
        //            continue;
        //        }

        //        attributeReference.TextString = value.Value ?? string.Empty;
        //    }

        //    return missing;
        //}

        // Helper methods for repeat use
        private static int ParseSlotNumber(string key)
        {
            return int.Parse(key.Substring(key.Length - 2));
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
            };

        }

        private Dictionary<string, AttributeReference> Get_AttDefDict(BlockReference acadBlockRef, Transaction tr)
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

        private Dictionary<string, DynamicBlockReferenceProperty> Get_DyBlockRefPropDict(BlockReference acadBlockRef)
        {
            return acadBlockRef.DynamicBlockReferencePropertyCollection
                .Cast<DynamicBlockReferenceProperty>()
                .ToDictionary(
                    p => p.PropertyName,
                    p => p,
                    StringComparer.OrdinalIgnoreCase
                );
        }

        private static AttributeReference TryGetValue_AttDefDict(Dictionary<string, AttributeReference> attributes, string tag)
        {
            attributes.TryGetValue(tag, out AttributeReference attribute);
            return attribute;
        }

        private static DynamicBlockReferenceProperty TryGetValue_DyBlockRefPropDict(Dictionary<string, DynamicBlockReferenceProperty> properties, string name)
        {
            properties.TryGetValue(name, out DynamicBlockReferenceProperty property);
            return property;
        }

        private void SetBlockTypeFromStatus(Transaction tr)
        {
            if (tr == null || BlockAttProps.BlockType == null || BlockAttProps.Update_Status == null) { return; }
            ;

            string status = Convert.ToString(BlockAttProps.Update_Status.Value);

            if (string.Equals(BlockAttProps.BlockType.TextString, status, StringComparison.Ordinal)) { return; }

            var blockType = (AttributeReference)tr.GetObject(BlockAttProps.BlockType.ObjectId, OpenMode.ForWrite);
            blockType.TextString = status ?? string.Empty;
            BlockAttProps.BlockType = blockType;
        }

        public async Task<ActivityCode> Get_BdryActCode() 
        {
            var boundaryCodeIdString = Get_SlotProperty("BOUNDARY_CODE_ID");
            var isBoundaryCodeId = int.TryParse(boundaryCodeIdString, out int boundaryCodeId);

            if (!isBoundaryCodeId) { return null; };

            if (BdryActCode != null && BdryActCode.ObjectId == boundaryCodeId) { return BdryActCode; };

            var p6ApiService = PluginInstance.MyP6ApiService;
            var actCode = await p6ApiService.GetP6ActivityCodeById(boundaryCodeId);

            if (actCode == null) { return null; };

            BdryActCode = actCode;
            return actCode;
        }

        public bool Set_BdryActCode(ActivityCode actCode, out ActivityCode result, Transaction tr = null) 
        {
            result = null;

            var boundaryCodeId = actCode.ObjectId.ToString();
            var boundaryCodeValue = actCode.Description;
            var boundaryCodePathArray = actCode.CodeConcatName.Split('.');
            var boundaryCodePath = string.Join(" -> ", boundaryCodePathArray);

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

            if(!isElementIdCodeId) { return null; };

            if(ElementIdCode != null  && ElementIdCode.ObjectId == elementIdCodeId) { return ElementIdCode; };

            var p6ApiService = PluginInstance.MyP6ApiService;
            var actCode = await p6ApiService.GetP6ActivityCodeById(elementIdCodeId);

            if(actCode == null) { return null; };

            ElementIdCode = actCode;
            return actCode; 
        }

        public bool Set_ElementIdCode(ActivityCode actCode, out ActivityCode result, Transaction tr = null)
        {
            result = null;

            var elementIdCodeId = actCode.ObjectId.ToString();
            var elementIdCodeValue = actCode.Description;
            var elementIdCodePathArray = actCode.CodeConcatName.Split('.')[^2..];
            var elementIdCodePath = string.Join(" -> ", elementIdCodePathArray);

            var idResult = Set_SlotProperty("ELEMENT_ID_CODE_ID", elementIdCodeId, out _, tr);
            var valueResult = Set_SlotProperty("ELEMENT_ID_CODE_VALUE", elementIdCodeValue, out _, tr);
            var pathResult = Set_SlotProperty("ELEMENT_ID_CODE_PATH", elementIdCodePath, out _, tr);

            if(!idResult || !valueResult || !pathResult) { return false; };

            ElementIdCode = actCode;

            result = actCode; 
            return true; 
        }

        public string Get_SlotProperty(string propName)
        {
            var propSlotDict = BlockAttProps.PropSlotDict;
            var slotFound = propSlotDict.TryGetValue(propName, out string slotKey);

            if(!slotFound || slotKey == null || String.IsNullOrWhiteSpace(slotKey)) { return null; };

            var slot = SlotsDict[slotKey];

            if (slot == null) { return null; };

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

            if (String.IsNullOrWhiteSpace(propName)) { return false; };

            var propSlotDict = BlockAttProps.PropSlotDict;
            var slotFound = propSlotDict.TryGetValue(propName, out string slotKey);

            if (!slotFound || slotKey == null || String.IsNullOrWhiteSpace(slotKey)) { return false; }

            var slot = SlotsDict[slotKey];

            if (slot == null) { return false; }

            switch(InitState)
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
                    if (tr == null) { Debug.Print("Transaction is null"); return false; };

                    var valueAttRef = (AttributeReference)tr.GetObject(slot.ValueAttRef.ObjectId, OpenMode.ForWrite);
                    valueAttRef.TextString = value;
                    slot.ValueAttRef = valueAttRef;
                    slot.SlotPropValue = value;
                    result = value;
                    return true;

                default:
                    return false;
            };

        }

    }
}
