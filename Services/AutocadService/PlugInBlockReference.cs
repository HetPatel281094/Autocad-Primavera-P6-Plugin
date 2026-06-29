using Autodesk.AutoCAD.DatabaseServices;
using System.Collections.Generic;
using AcadAppServ = Autodesk.AutoCAD.ApplicationServices;
using System.Diagnostics;

namespace Autocad_Primavera_P6_Plugin.Services.AutocadService
{

    public class Slot
    {
        public string SlotPropName;
        public string NameAttRefTag;
        public AttributeReference NameAttRef;
        public string ValueAttRefTag;
        public AttributeReference ValueAttRef;
    };

    public enum UpdateStatus
    {
        Unplanned,
        Planned,
        Started,
        Onhold,
        Completed
    };

    public class AttProps
    {
        public AttributeReference Attribute_Check_String; // Value must be "FoundOK"
        public AttributeReference ElementId; //BlockName Currently ElementId Code Parent > Current Code e.g. Precast_Columns > 1
        public AttributeReference BlockType; // Default is empty
        public DynamicBlockReferenceProperty Update_Status;
        public DynamicBlockReferenceProperty Moveinfo_X;
        public DynamicBlockReferenceProperty Moveinfo_Y;
        public Dictionary<string, string> PropSlotDict = new Dictionary<string, string>();
    }

    public static class DefaultPropSlotName
    {
        public const string BOUNDARY_CODE_ID = "BOUNDARY_CODE_ID";
        public const string BOUNDARY_CODE_PATH = "BOUNDARY_CODE_PATH";
        public const string BOUNDARY_CODE_VALUE = "BOUNDARY_CODE_VALUE";
        public const string ITEM_ID_CODE_ID = "ITEM_ID_CODE_ID";
        public const string ITEM_ID_CODE_PATH = "ITEM_ID_CODE_PATH";
        public const string ITEM_ID_CODE_VALUE = "ITEM_ID_CODE_VALUE";
        public const string LENGTH_L = "LENGTH_L";
        public const string BREADTH_B = "BREADTH_B";
        public const string HEIGHT_H = "HEIGHT_H";
    }

    public enum RefState
    {
        Linked,
        NoRef
    };

    public class PlugInBlockReference
    {
        public RefState refState = RefState.NoRef;

        public AcadAppServ.Document AcadDoc = null;

        private BlockReference AcadBlock = null;

        private BlockTableRecord AcadBlockTR = null;

        private Dictionary<string, Slot> SlotsDict = new Dictionary<string, Slot>
        {
            ["Slot01"] = new Slot(),
            ["Slot02"] = new Slot(),
            ["Slot03"] = new Slot(),
            ["Slot04"] = new Slot(),
            ["Slot05"] = new Slot(),
            ["Slot06"] = new Slot(),
            ["Slot07"] = new Slot(),
            ["Slot08"] = new Slot(),
            ["Slot09"] = new Slot(),
            ["Slot10"] = new Slot(),
            ["Slot11"] = new Slot(),
            ["Slot12"] = new Slot(),
            ["Slot13"] = new Slot(),
            ["Slot14"] = new Slot(),
            ["Slot15"] = new Slot(),
        };

        private AttProps BlockAttProps = null;

        //private ActivityCode BdryActCode => GetBdryActCode();
        //private ActivityCode ItemIdActCode => AcadBlock.AttributeCollection;

        public PlugInBlockReference(AcadAppServ.Document acadDoc, BlockReference blockRef = null)
        {
            AcadDoc = acadDoc;

            if (blockRef != null)
            {
                AcadBlock = blockRef;

                var _ed = AcadDoc.Editor;
                var _db = AcadDoc.Database;
                using (var tr = _db.TransactionManager.StartTransaction())
                {
                    AcadBlockTR = (BlockTableRecord)tr.GetObject(AcadBlock.BlockTableRecord, OpenMode.ForRead);
                    LoadBlockAttProps(tr);
                    tr.Commit();
                }
            }
            ;
        }

        private void LoadBlockAttProps(Transaction tr)
        {
            try
            {
                var attColln = AcadBlock.AttributeCollection;
                var attDict = new Dictionary<string, AttributeReference>();

                foreach (ObjectId item in attColln)
                {
                    AttributeReference attRef = (AttributeReference)item.GetObject(OpenMode.ForRead);
                    attDict.Add(attRef.Tag, attRef);
                };

                var _Attribute_Check_String = attDict.ContainsKey("Attribute_Check_String") ? attDict["Attribute_Check_String"] : null;
                var _ElementId = attDict.ContainsKey("ELEMENT_ID") ? attDict["ELEMENT_ID"] : null;
                var _BlockType = attDict.ContainsKey("BLOCK_TYPE") ? attDict["BLOCK_TYPE"] : null;

                if (_Attribute_Check_String != null && _ElementId != null && _BlockType != null)
                {
                    BlockAttProps = new AttProps();
                    BlockAttProps.Attribute_Check_String = _Attribute_Check_String;
                    BlockAttProps.ElementId = _ElementId;
                    BlockAttProps.BlockType = _BlockType;

                    foreach (var item in SlotsDict)
                    {
                        var dictKey = item.Key.Trim();
                        var slotIndexStr = dictKey.Substring(dictKey.Length - 2);
                        var slotIndex = int.Parse(slotIndexStr);

                        var nameAttTag = $"ATT_{slotIndex}_NAME";
                        var valAttTag = $"ATT_{slotIndex}_VALUE";

                        var nameAtt = attDict.ContainsKey(nameAttTag) ? attDict[nameAttTag] : null;
                        var valAtt = attDict.ContainsKey(valAttTag) ? attDict[valAttTag] : null;

                        if (nameAtt != null && valAtt != null)
                        {
                            var dictVal = item.Value;

                            dictVal.SlotPropName = nameAtt?.TextString ?? string.Empty;
                            dictVal.NameAttRefTag = nameAttTag;
                            dictVal.NameAttRef = nameAtt;
                            dictVal.ValueAttRefTag = valAttTag;
                            dictVal.ValueAttRef = valAtt;
                        };

                    };

                    foreach (var item in SlotsDict)
                    {
                        var slotItem = item.Value;
                        if (slotItem.SlotPropName != null && slotItem.SlotPropName != string.Empty && slotItem.SlotPropName.Trim().Length > 0)
                        {
                            BlockAttProps.PropSlotDict.Add(slotItem.SlotPropName, item.Key);
                        };
                    };

                };

            }
            catch (System.Exception ex)
            {
                Debug.Print($"Error in LoadBlockAttProps: {ex.Message}");
            }

        }

    }
}