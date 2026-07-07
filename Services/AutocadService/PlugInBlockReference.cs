using Autodesk.AutoCAD.DatabaseServices;
using System.Collections.Generic;
using AcadAppServ = Autodesk.AutoCAD.ApplicationServices;
using System.Diagnostics;
using System.Linq;
using System;
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
    };

    public enum UpdateStatus
    {
        Unplanned,
        Planned,
        Started,
        Onhold,
        Completed
    };

    /// <summary>
    /// Main Model
    /// Represents the properties of the block attributes
    /// </summary>
    public class AttProps
    {
        public AttributeReference Attribute_Check_String; // Value must be "FoundOK"
        public AttributeReference ElementId; // BlockName Currently ElementId Code Parent > Current Code e.g. Precast_Columns > 1
        public AttributeReference BlockType; // Mirrors the current visibility-state name
        public DynamicBlockReferenceProperty Update_Status; // Dynamic visibility-state property
        public DynamicBlockReferenceProperty Moveinfo_X;
        public DynamicBlockReferenceProperty Moveinfo_Y;
        public Dictionary<string, string> PropSlotDict = new Dictionary<string, string>();

        public void updateBlockType(UpdateStatus status)
        {
            if (BlockType != null && Update_Status != null)
            {
                BlockType.TextString = Update_Status.Value?.ToString()?.Trim();
            }
        }

        public void setBlockTypeFromStatus(Transaction tr)
        {
            if (tr != null && BlockType?.ObjectId != null && BlockType.TextString != (string)Update_Status.Value)
            {
                var _blockTypeObj = (AttributeReference)tr.GetObject(BlockType.ObjectId, OpenMode.ForWrite);
                _blockTypeObj.TextString = (string)Update_Status.Value;
                BlockType = _blockTypeObj;
            }
        }

        public ActivityCode GetBdryActCode(P6ApiService.P6ApiService myP6ApiService, Dictionary<string, Slot> slotsDict)
        {
            var bdryIdSlotKey = PropSlotDict["BOUNDARY_CODE_ID"];
            var bdryIdSlot = slotsDict[bdryIdSlotKey];
            var bdryId = bdryIdSlot.ValueAttRef.TextString;

            var activityCode = myP6ApiService.Client.GetActivityCodesAsync("","",null,null).Result;

            return activityCode.First();
        }

    }

    public static class DefaultPropSlotName
    {
            public const string Slot01 = "BOUNDARY_CODE_ID";
            public const string Slot02  = "";
            public const string Slot03  = "";
            public const string Slot04  = "";
            public const string Slot05  = "";
            public const string Slot06  = "";
            public const string Slot07  = "";
            public const string Slot08  = "";
            public const string Slot09  = "";
            public const string Slot10  = "";
            public const string Slot11  = "";
            public const string Slot12  = "";
            public const string Slot13  = "";
            public const string Slot14  = "";
            public const string Slot15  = "";

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

    public enum InitState
    {
        NotInitialized,
        BTRInitialized,
        BlockRefInitialized
    };

    // I am a blockRef
    // I have parent BTR (BlockTableRecord object)
    // I have a dictionary of slots
    // I have a dictionary of attProps
    // I can be initialized with either a blockRef or a BTR
    // If I am initialized with a blockRef, I will load my attProps and slots from the blockRef's attributes
    // If I am initialized with a BTR, I will not load my attProps and slots until I am given a blockRef
    public class PlugInBlockReference
    {
        public InitState initState = InitState.NotInitialized;

        public RefState refState = RefState.NoRef;

        public AcadAppServ.Document AcadDoc = null;

        private BlockReference AcadBlock = null;

        private BlockTableRecord AcadBlockTR = null;

        /// <summary>
        /// Dictionary to store the slots associated with the block reference
        /// </summary>
        public Dictionary<string, Slot> SlotsDict = new Dictionary<string, Slot>
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
            ["Slot15"] = new Slot()
        };

        public AttProps BlockAttProps = null;

        private ActivityCode _BdryActCode;
        public ActivityCode BdryActCode { 
            get { return _BdryActCode; } 
            set { _BdryActCode = value; } 
        }
        //private ActivityCode ItemIdActCode => AcadBlock.AttributeCollection;

        public PlugInBlockReference(AcadAppServ.Document acadDoc, BlockReference blockRef = null)
        {
            AcadDoc = acadDoc;
            initState = InitState.BlockRefInitialized;
            if (blockRef != null) { AcadBlock = blockRef; };
        }

        public PlugInBlockReference(AcadAppServ.Document acadDoc, BlockTableRecord blockRec = null)
        {
            AcadDoc = acadDoc;
            initState = InitState.BTRInitialized;
            if (blockRec != null) { AcadBlockTR = blockRec; };
        }

        public void Init(Transaction tr)
        {
            if (initState == InitState.BlockRefInitialized)
            {
                AcadBlockTR = (BlockTableRecord)tr.GetObject(AcadBlock.BlockTableRecord, OpenMode.ForRead);
                LoadBlockAttProps(tr);
            }
            else if (initState == InitState.BTRInitialized)
            {

            }

        }

        private void LoadBlockAttProps(Transaction tr)
        {
            try
            {
                var attColln = AcadBlock.AttributeCollection;
                var dynPropColln = AcadBlock.DynamicBlockReferencePropertyCollection;

                var attDict = new Dictionary<string, AttributeReference>();
                var dynPropDict = new Dictionary<string, DynamicBlockReferenceProperty>();


                attDict = attColln
                            .Cast<ObjectId>()
                            .Select(objId => new { Id = objId, attRef = (AttributeReference)tr.GetObject(objId, OpenMode.ForRead) })
                            .ToDictionary(val => val.attRef.Tag, val => val.attRef);

                dynPropDict = dynPropColln
                                .Cast<DynamicBlockReferenceProperty>()
                                .ToDictionary(val => val.PropertyName, val => val);

                var _Attribute_Check_String = attDict.ContainsKey("Attribute_Check_String") ? attDict["Attribute_Check_String"] : null;
                var _ElementId = attDict.ContainsKey("ELEMENT_ID") ? attDict["ELEMENT_ID"] : null;
                var _BlockType = attDict.ContainsKey("BLOCK_TYPE") ? attDict["BLOCK_TYPE"] : null;

                var _Update_Status = dynPropDict.ContainsKey("Update Status") ? dynPropDict["Update Status"] : null;
                var _Moveinfo_X =  dynPropDict.ContainsKey("MoveInfo X") ? dynPropDict["MoveInfo X"] : null;
                var _Moveinfo_Y = dynPropDict.ContainsKey("MoveInfo Y") ? dynPropDict["MoveInfo Y"] : null;

                if (_Attribute_Check_String != null && _ElementId != null && _BlockType != null)
                {
                    BlockAttProps = new AttProps();
                    BlockAttProps.Attribute_Check_String = _Attribute_Check_String;
                    BlockAttProps.ElementId = _ElementId;
                    BlockAttProps.BlockType = _BlockType;
                    BlockAttProps.Moveinfo_X = _Moveinfo_X;
                    BlockAttProps.Moveinfo_Y = _Moveinfo_Y;

                    BlockAttProps.Update_Status = _Update_Status;
                    BlockAttProps.setBlockTypeFromStatus(tr);

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
