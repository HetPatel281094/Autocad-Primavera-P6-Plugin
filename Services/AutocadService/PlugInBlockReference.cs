using Autocad_Primavera_P6_Plugin.Services.P6ApiService;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;
using System;
using System.Collections.Generic;
using System.Linq;
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
        public static readonly Dictionary<string, string> PropSlotDict = new()
        {
            ["Slot01"] = "BOUNDARY_CODE",
            ["Slot02"] = "BOUNDARY_CODE",
            ["Slot03"] = "BOUNDARY_CODE",
            ["Slot04"] = "ITEM_ID_CODE",
            ["Slot05"] = "ITEM_ID_CODE",
            ["Slot06"] = "ITEM_ID_CODE",
            ["Slot07"] = "LENGTH_L",
            ["Slot08"] = "BREADTH_B",
            ["Slot09"] = "HEIGHT_H"
        };
    }

    public enum InitState
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
        public InitState initState { get; private set; } = InitState.NotInitialized;
        private AcadAppServ.Document AcadDoc = null;
        private ObjectId BlockReferenceId = ObjectId.Null;
        private ObjectId BlockTableRecordId = ObjectId.Null;
        private Dictionary<string, Slot> SlotsDict = new() { ["Slot01"] = null, ["Slot02"] = null, ["Slot03"] = null, ["Slot04"] = null, ["Slot05"] = null, ["Slot06"] = null, ["Slot07"] = null, ["Slot08"] = null, ["Slot09"] = null, ["Slot10"] = null, ["Slot11"] = null, ["Slot12"] = null, ["Slot13"] = null, ["Slot14"] = null, ["Slot15"] = null };
        private AttProps BlockAttProps = null;

        private ActivityCode BdryActCode = null;
        public ActivityCode Get_BdryActCode() { return BdryActCode; }
        public bool Set_BdryActCode(ActivityCode actCode, out ActivityCode result) { BdryActCode = actCode; result = actCode; return true; }

        private ActivityCode ElementIdCode = null;
        public ActivityCode Get_ElementIdCode() { return ElementIdCode; }
        public bool Set_ElementIdCode(ActivityCode actCode, out ActivityCode result) { ElementIdCode = actCode; result = actCode; return true; }

        public IList<string> LoadWarnings = new List<string>();

        /// <summary>Creates a blank object. Attach a BTR or block reference later.</summary>
        public PlugInBlockReference(AcadAppServ.Document acadDoc)
        {
            AcadDoc = acadDoc ?? throw new ArgumentNullException(nameof(acadDoc));
            Init_Slots(InitState.DefaultInitialized);
            SetDefaultSlots();
        }

        private void SetDefaultSlots()
        {
            var defaultDict = DefaultPropSlotName.PropSlotDict;

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

            initState = InitState.DefaultInitialized;
        }

        /// <summary>Creates a reference-bound object. Call Init with the caller transaction.</summary>
        public PlugInBlockReference(AcadAppServ.Document acadDoc, BlockReference blockRef)
        {
            AcadDoc = acadDoc ?? throw new ArgumentNullException(nameof(acadDoc));
            Init_Slots(InitState.BlockRefInitialized);
            AttachBlockReference(blockRef);
        }

        public void AttachBlockReference(BlockReference blockRef)
        {
            if (blockRef == null){ throw new ArgumentNullException(nameof(blockRef)); };

            BlockReferenceId = blockRef.ObjectId;
            BlockTableRecordId = blockRef.BlockTableRecord;
            initState = InitState.BlockRefInitialized;
            BlockAttProps = null;
            ClearSlots();
        }

        /// <summary>Creates a definition-bound object. Call Init with the caller transaction.</summary>
        public PlugInBlockReference(AcadAppServ.Document acadDoc, BlockTableRecord blockTableRecord)
        {
            AcadDoc = acadDoc ?? throw new ArgumentNullException(nameof(acadDoc));
            Init_Slots(InitState.BTRInitialized);
            AttachBlockTableRecord(blockTableRecord);
        }

        private void Init_Slots(InitState bTRInitialized)
        {
            throw new NotImplementedException();
        }

        public void AttachBlockTableRecord(BlockTableRecord blockTableRecord)
        {
            if (blockTableRecord == null)
            {
                throw new ArgumentNullException(nameof(blockTableRecord));
            }

            BlockReferenceId = ObjectId.Null;
            BlockTableRecordId = blockTableRecord.ObjectId;
            initState = InitState.BTRInitialized;
            BlockAttProps = null;
            ClearSlots();
        }

        public void AttachBlockReference(BlockReference blockRef, Transaction tr)
        {
            AttachBlockReference(blockRef);
            Init(tr);
        }

        public void AttachBlockTableRecord(BlockTableRecord blockTableRecord, Transaction tr)
        {
            AttachBlockTableRecord(blockTableRecord);
            Init(tr);
        }

        /// <summary>Loads the template schema or the live instance attributes for the current state.</summary>
        public void Init(Transaction tr)
        {
            if (tr == null)
            {
                throw new ArgumentNullException(nameof(tr));
            }

            LoadWarnings.Clear();
            BlockAttProps = null;
            ClearSlots();

            if (initState == InitState.BTRInitialized)
            {
                EnsureBound(BlockTableRecordId, "block table record");
                var blockTableRecord = (BlockTableRecord)tr.GetObject(BlockTableRecordId, OpenMode.ForRead);
                LoadTemplateSlots(blockTableRecord, tr);
                return;
            }

            if (initState == InitState.BlockRefInitialized)
            {
                EnsureBound(BlockReferenceId, "block reference");
                var blockReference = (BlockReference)tr.GetObject(BlockReferenceId, OpenMode.ForRead);
                BlockTableRecordId = blockReference.BlockTableRecord;
                LoadInstanceProperties(blockReference, tr);
                return;
            }

            // Blank state deliberately has no AutoCAD data to load.
        }

        /// <summary>
        /// Inserts this template into ownerSpace, creates its AttributeReferences, applies
        /// supplied values, and upgrades this same object to instance-bound state.
        /// Returns tags that were requested but absent from the block definition.
        /// </summary>
        public ObjectId CreateBlockReference(
            ObjectId ownerSpaceId,
            Point3d insertionPoint,
            IDictionary<string, string> attributeValues,
            Transaction tr,
            out IList<string> missingAttributeTags)
        {
            if (tr == null)
            {
                throw new ArgumentNullException(nameof(tr));
            }
            if (initState != InitState.BTRInitialized)
            {
                throw new InvalidOperationException("A block table record must be attached before creating a block reference.");
            }

            EnsureBound(BlockTableRecordId, "block table record");
            var ownerSpace = (BlockTableRecord)tr.GetObject(ownerSpaceId, OpenMode.ForWrite);
            var blockDefinition = (BlockTableRecord)tr.GetObject(BlockTableRecordId, OpenMode.ForRead);
            var blockReference = new BlockReference(insertionPoint, BlockTableRecordId);
            ownerSpace.AppendEntity(blockReference);
            tr.AddNewlyCreatedDBObject(blockReference, true);

            foreach (ObjectId entityId in blockDefinition)
            {
                var definition = tr.GetObject(entityId, OpenMode.ForRead) as AttributeDefinition;
                if (definition == null || definition.Constant)
                {
                    continue;
                }

                var attributeReference = new AttributeReference();
                attributeReference.SetAttributeFromBlock(definition, blockReference.BlockTransform);
                blockReference.AttributeCollection.AppendAttribute(attributeReference);
                tr.AddNewlyCreatedDBObject(attributeReference, true);
            }

            AttachBlockReference(blockReference);
            Init(tr);
            missingAttributeTags = ApplyAttributeValues(attributeValues, tr);
            Init(tr); // Reload the public handles after any attributes were opened for write.
            return BlockReferenceId;
        }

        /// <summary>Writes only existing AttributeReferences; it never fabricates an orphan attribute.</summary>
        public IList<string> ApplyAttributeValues(IDictionary<string, string> attributeValues, Transaction tr)
        {
            if (attributeValues == null || attributeValues.Count == 0)
            {
                return new List<string>();
            }
            if (tr == null)
            {
                throw new ArgumentNullException(nameof(tr));
            }
            if (initState != InitState.BlockRefInitialized)
            {
                throw new InvalidOperationException("A block reference must be attached before writing attributes.");
            }

            var blockReference = (BlockReference)tr.GetObject(BlockReferenceId, OpenMode.ForRead);
            var attributes = ReadAttributeReferences(blockReference, tr, OpenMode.ForWrite);
            var missing = new List<string>();
            foreach (var value in attributeValues)
            {
                if (!attributes.TryGetValue(value.Key, out AttributeReference attributeReference))
                {
                    missing.Add(value.Key);
                    continue;
                }

                attributeReference.TextString = value.Value ?? string.Empty;
            }

            return missing;
        }

        private void LoadTemplateSlots(BlockTableRecord blockTableRecord, Transaction tr)
        {
            var definitions = new Dictionary<string, AttributeDefinition>(StringComparer.OrdinalIgnoreCase);
            foreach (ObjectId entityId in blockTableRecord)
            {
                var definition = tr.GetObject(entityId, OpenMode.ForRead) as AttributeDefinition;
                if (definition != null && !definitions.ContainsKey(definition.Tag))
                {
                    definitions.Add(definition.Tag, definition);
                }
            }

            foreach (var entry in SlotsDict)
            {
                int slotNumber = ParseSlotNumber(entry.Key);
                string nameTag = SlotNameTag(slotNumber);
                string valueTag = SlotValueTag(slotNumber);
                if (!definitions.TryGetValue(nameTag, out AttributeDefinition nameDefinition) ||
                    !definitions.ContainsKey(valueTag))
                {
                    continue;
                }

                entry.Value.NameAttRefTag = nameTag;
                entry.Value.ValueAttRefTag = valueTag;
                entry.Value.SlotPropName = nameDefinition.TextString ?? string.Empty;
            }
        }

        private void LoadInstanceProperties(BlockReference blockReference, Transaction tr)
        {
            var attributes = ReadAttributeReferences(blockReference, tr, OpenMode.ForRead);
            var dynamicProperties = new Dictionary<string, DynamicBlockReferenceProperty>(StringComparer.OrdinalIgnoreCase);
            foreach (DynamicBlockReferenceProperty property in blockReference.DynamicBlockReferencePropertyCollection)
            {
                if (!dynamicProperties.ContainsKey(property.PropertyName))
                {
                    dynamicProperties.Add(property.PropertyName, property);
                }
            }

            var properties = new AttProps
            {
                Attribute_Check_String = GetAttribute(attributes, "Attribute_Check_String"),
                ElementId = GetAttribute(attributes, "ELEMENT_ID"),
                BlockType = GetAttribute(attributes, "BLOCK_TYPE"),
                Update_Status = GetDynamicProperty(dynamicProperties, "Update Status"),
                Moveinfo_X = GetDynamicProperty(dynamicProperties, "MoveInfo X"),
                Moveinfo_Y = GetDynamicProperty(dynamicProperties, "MoveInfo Y")
            };

            foreach (var entry in SlotsDict)
            {
                int slotNumber = ParseSlotNumber(entry.Key);
                string nameTag = SlotNameTag(slotNumber);
                string valueTag = SlotValueTag(slotNumber);
                var nameAttribute = GetAttribute(attributes, nameTag);
                var valueAttribute = GetAttribute(attributes, valueTag);
                if (nameAttribute == null || valueAttribute == null)
                {
                    continue;
                }

                entry.Value.NameAttRefTag = nameTag;
                entry.Value.NameAttRef = nameAttribute;
                entry.Value.ValueAttRefTag = valueTag;
                entry.Value.ValueAttRef = valueAttribute;
                entry.Value.SlotPropName = nameAttribute.TextString ?? string.Empty;

                string propertyName = entry.Value.SlotPropName.Trim();
                if (propertyName.Length > 0 && !properties.PropSlotDict.ContainsKey(propertyName))
                {
                    properties.PropSlotDict.Add(propertyName, entry.Key);
                }
                else if (propertyName.Length > 0)
                {
                    LoadWarnings.Add("Duplicate slot property name '" + propertyName + "' in " + entry.Key + ". The first slot is used.");
                }
            }

            BlockAttProps = properties;
            if (properties.Attribute_Check_String == null || properties.ElementId == null || properties.BlockType == null)
            {
                LoadWarnings.Add("The block is missing one or more required plugin attributes: Attribute_Check_String, ELEMENT_ID, BLOCK_TYPE.");
            }
        }

        private static Dictionary<string, AttributeReference> ReadAttributeReferences(BlockReference blockReference, Transaction tr, OpenMode mode)
        {
            var attributes = new Dictionary<string, AttributeReference>(StringComparer.OrdinalIgnoreCase);
            foreach (ObjectId attributeId in blockReference.AttributeCollection)
            {
                var attribute = tr.GetObject(attributeId, mode) as AttributeReference;
                if (attribute != null && !attributes.ContainsKey(attribute.Tag))
                {
                    attributes.Add(attribute.Tag, attribute);
                }
            }
            return attributes;
        }

        private static AttributeReference GetAttribute(Dictionary<string, AttributeReference> attributes, string tag)
        {
            attributes.TryGetValue(tag, out AttributeReference attribute);
            return attribute;
        }

        private static DynamicBlockReferenceProperty GetDynamicProperty(Dictionary<string, DynamicBlockReferenceProperty> properties, string name)
        {
            properties.TryGetValue(name, out DynamicBlockReferenceProperty property);
            return property;
        }

        private void ClearSlots()
        {
            foreach (Slot slot in SlotsDict.Values)
            {
                slot.SlotPropName = string.Empty;
                slot.NameAttRefTag = null;
                slot.NameAttRef = null;
                slot.ValueAttRefTag = null;
                slot.ValueAttRef = null;
            }
        }

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

        private static void EnsureBound(ObjectId objectId, string objectName)
        {
            if (objectId.IsNull || !objectId.IsValid)
            {
                throw new InvalidOperationException("No valid " + objectName + " is attached.");
            }
        }

        public void SetBlockTypeFromStatus(Transaction tr)
        {
            if (tr == null || BlockAttProps.BlockType == null || BlockAttProps.Update_Status == null) { return; }
            ;

            string status = Convert.ToString(BlockAttProps.Update_Status.Value);

            if (string.Equals(BlockAttProps.BlockType.TextString, status, StringComparison.Ordinal)) { return; }

            var blockType = (AttributeReference)tr.GetObject(BlockAttProps.BlockType.ObjectId, OpenMode.ForWrite);
            blockType.TextString = status ?? string.Empty;
            BlockAttProps.BlockType = blockType;
        }
    }
}
