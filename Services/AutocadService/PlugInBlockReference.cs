using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;
using AcadAppServ = Autodesk.AutoCAD.ApplicationServices;
using Autocad_Primavera_P6_Plugin.Services.P6ApiService;

namespace Autocad_Primavera_P6_Plugin.Services.AutocadService
{
    public class Slot
    {
        public string DefaultPropSlotName;
        public string SlotPropName;
        public string NameAttRefTag;
        public AttributeReference NameAttRef;
        public string ValueAttRefTag;
        public AttributeReference ValueAttRef;
    }

    public enum UpdateStatus
    {
        Unplanned,
        Planned,
        Started,
        Onhold,
        Completed
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
        public Dictionary<string, string> PropSlotDict = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        public void UpdateBlockType(UpdateStatus status)
        {
            if (BlockType != null)
            {
                BlockType.TextString = status.ToString();
            }
        }

        public void SetBlockTypeFromStatus(Transaction tr)
        {
            if (tr == null || BlockType == null || Update_Status == null)
            {
                return;
            }

            string status = Convert.ToString(Update_Status.Value);
            if (string.Equals(BlockType.TextString, status, StringComparison.Ordinal))
            {
                return;
            }

            var blockType = (AttributeReference)tr.GetObject(BlockType.ObjectId, OpenMode.ForWrite);
            blockType.TextString = status ?? string.Empty;
            BlockType = blockType;
        }

        public async Task<ActivityCode> GetBdryActCodeAsync(Autocad_Primavera_P6_Plugin.Services.P6ApiService.P6ApiService p6ApiService, Dictionary<string, Slot> slotsDict)
        {
            if (p6ApiService == null || p6ApiService.Client == null || slotsDict == null ||
                !PropSlotDict.TryGetValue(DefaultPropSlotName.BOUNDARY_CODE_ID, out string slotKey) ||
                !slotsDict.TryGetValue(slotKey, out Slot slot) || slot.ValueAttRef == null)
            {
                return null;
            }

            string boundaryCodeId = slot.ValueAttRef.TextString;
            if (string.IsNullOrWhiteSpace(boundaryCodeId))
            {
                return null;
            }

            var activityCodes = await p6ApiService.Client.GetActivityCodesAsync(
                null,
                "ObjectId,CodeValue,CodeConcatName,CodeTypeObjectId,ParentObjectId",
                null,
                null);

            return activityCodes.FirstOrDefault(code =>
                code.ObjectId.HasValue &&
                string.Equals(code.ObjectId.Value.ToString(), boundaryCodeId, StringComparison.OrdinalIgnoreCase));
        }
    }

    public static class DefaultPropSlotName
    {
        public const string Slot01 = "BOUNDARY_CODE_ID";
        public const string Slot02 = "BOUNDARY_CODE_PATH";
        public const string Slot03 = "BOUNDARY_CODE_VALUE";
        public const string Slot04 = "ITEM_ID_CODE_ID";
        public const string Slot05 = "ITEM_ID_CODE_PATH";
        public const string Slot06 = "ITEM_ID_CODE_VALUE";
        public const string Slot07 = "LENGTH_L";
        public const string Slot08 = "BREADTH_B";
        public const string Slot09 = "HEIGHT_H";
        public const string Slot10 = "";
        public const string Slot11 = "";
        public const string Slot12 = "";
        public const string Slot13 = "";
        public const string Slot14 = "";
        public const string Slot15 = "";
    }

    public enum RefState
    {
        Linked,
        NoRef
    }

    public enum InitState
    {
        NotInitialized,
        BTRInitialized,
        BlockRefInitialized
    }

    /// <summary>
    /// A plugin block through its lifecycle: blank data container, definition-bound
    /// template, then a live instance-bound reference. Callers own the document lock
    /// and transaction; AutoCAD DBObjects exposed by BlockAttProps are valid only for
    /// the transaction used to call Init, AttachBlockReference, or CreateBlockReference.
    /// </summary>
    public class PlugInBlockReference
    {
        public const int SlotCount = 15;

        public InitState initState { get; private set; } = InitState.NotInitialized;
        public RefState refState { get; private set; } = RefState.NoRef;
        public AcadAppServ.Document AcadDoc { get; }
        public ObjectId BlockReferenceId { get; private set; } = ObjectId.Null;
        public ObjectId BlockTableRecordId { get; private set; } = ObjectId.Null;

        public Dictionary<string, Slot> SlotsDict { get; } = CreateSlots();
        public AttProps BlockAttProps { get; private set; }
        public ActivityCode BdryActCode { get; set; }
        public IList<string> LoadWarnings { get; } = new List<string>();

        /// <summary>Creates a blank object. Attach a BTR or block reference later.</summary>
        public PlugInBlockReference(AcadAppServ.Document acadDoc)
        {
            AcadDoc = acadDoc ?? throw new ArgumentNullException(nameof(acadDoc));
        }

        /// <summary>Creates a reference-bound object. Call Init with the caller transaction.</summary>
        public PlugInBlockReference(AcadAppServ.Document acadDoc, BlockReference blockRef)
            : this(acadDoc)
        {
            AttachBlockReference(blockRef);
        }

        /// <summary>Creates a definition-bound object. Call Init with the caller transaction.</summary>
        public PlugInBlockReference(AcadAppServ.Document acadDoc, BlockTableRecord blockTableRecord)
            : this(acadDoc)
        {
            AttachBlockTableRecord(blockTableRecord);
        }

        public void AttachBlockReference(BlockReference blockRef)
        {
            if (blockRef == null)
            {
                throw new ArgumentNullException(nameof(blockRef));
            }

            BlockReferenceId = blockRef.ObjectId;
            BlockTableRecordId = blockRef.BlockTableRecord;
            initState = InitState.BlockRefInitialized;
            refState = RefState.Linked;
            BlockAttProps = null;
            ClearSlots();
        }

        public void AttachBlockReference(BlockReference blockRef, Transaction tr)
        {
            AttachBlockReference(blockRef);
            Init(tr);
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
            refState = RefState.NoRef;
            BlockAttProps = null;
            ClearSlots();
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
                slot.DefaultPropSlotName = string.Empty;
                slot.SlotPropName = string.Empty;
                slot.NameAttRefTag = null;
                slot.NameAttRef = null;
                slot.ValueAttRefTag = null;
                slot.ValueAttRef = null;
            }
        }

        private static Dictionary<string, Slot> CreateSlots()
        {
            var slots = new Dictionary<string, Slot>();
            for (int slot = 1; slot <= SlotCount; slot++)
            {
                slots.Add("Slot" + slot.ToString("D2"), new Slot());
            }
            return slots;
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
    }
}
