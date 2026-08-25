using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using System;
using System.Collections.Generic;
using System.Linq;

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

        public static Document GetDocumentFromObject(DBObject obj)
        {
            var db = obj.Database;
            if (db == null) return null;

            return Application.DocumentManager
                .Cast<Document>()
                .FirstOrDefault(doc => doc.Database == db);
        }

    }

    public class Slot
    {
        public string NameAttRefTag;
        public string ValueAttRefTag;
        public AttributeReference NameAttRef;
        public AttributeReference ValueAttRef;
    }

    public class PlugInBlockReference
    {
        public MyPlugin PluginInstance = null;

        public BlockReference AcadBlockRef = null;
        public Document AcadDoc => PlugInBlockReference_Helpers.GetDocumentFromObject(this.AcadBlockRef);
        public Database AcadDB => AcadDoc.Database;

        public Dictionary<string, AttributeReference> AttRefDict = null;

        public Dictionary<string, string> PropSlotDict = new(StringComparer.OrdinalIgnoreCase);
        public Dictionary<string, Slot> SlotsDict = new() { ["Slot01"] = null, ["Slot02"] = null, ["Slot03"] = null, ["Slot04"] = null, ["Slot05"] = null, ["Slot06"] = null, ["Slot07"] = null, ["Slot08"] = null, ["Slot09"] = null, ["Slot10"] = null, ["Slot11"] = null, ["Slot12"] = null, ["Slot13"] = null, ["Slot14"] = null, ["Slot15"] = null };

        public AttributeReference Attribute_Check_String;
        public AttributeReference ElementId;
        public AttributeReference BlockType;

        public PlugInBlockReference(MyPlugin pluginInstance, BlockReference blockRef, Transaction tr)
        {
            PluginInstance = pluginInstance ?? throw new ArgumentNullException(nameof(pluginInstance));
            AcadBlockRef = blockRef ?? throw new ArgumentNullException(nameof(blockRef));

            AttRefDict = PlugInBlockReference_Helpers.Get_AttRefDict(AcadBlockRef, tr);
            Init_SlotsDict();

            Attribute_Check_String = PlugInBlockReference_Helpers.TryGetValue_AttRefDict(AttRefDict, "Attribute_Check_String");
            ElementId = PlugInBlockReference_Helpers.TryGetValue_AttRefDict(AttRefDict, "ELEMENT_ID");
            BlockType = PlugInBlockReference_Helpers.TryGetValue_AttRefDict(AttRefDict, "BLOCK_TYPE");

            var x = new Group();
        }

        private void Init_SlotsDict()
        {
            foreach (var key in SlotsDict.Keys.ToList())
            {
                int slotNumber = ParseSlotNumber(key);
                string nameTag = SlotNameTag(slotNumber);
                string valueTag = SlotValueTag(slotNumber);
                var nameAttribute = PluginBlockRefAutocadHelpers.TryGetValue_AttRefDict(AttRefDict, nameTag);
                var valueAttribute = PluginBlockRefAutocadHelpers.TryGetValue_AttRefDict(AttRefDict, valueTag);

                if (nameAttribute == null || valueAttribute == null || String.IsNullOrWhiteSpace(nameAttribute.TextString)) { continue; };

                SlotsDict[key] = new Slot()
                {
                    NameAttRefTag = nameTag,
                    NameAttRef = nameAttribute,
                    ValueAttRefTag = valueTag,
                    ValueAttRef = valueAttribute
                };

            }

            ReInit_PropSlotDict();

        }

        private void ReInit_PropSlotDict()
        {
            PropSlotDict.Clear();

            foreach (var entry in SlotsDict)
            {
                string propertyName = entry.Value?.NameAttRef?.TextString ?? String.Empty;

                if (propertyName.Length > 0 && !PropSlotDict.ContainsKey(propertyName))
                {
                    PropSlotDict.Add(propertyName, entry.Key);
                }

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

    }
}
