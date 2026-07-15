// (C) Copyright 2026 by  
//
using System;
using System.Threading.Tasks;
using Autodesk.AutoCAD.Runtime;
using Autodesk.AutoCAD.ApplicationServices;
using Autocad_Primavera_P6_Plugin.Services;
using Autocad_Primavera_P6_Plugin.Services.LiteDBService;
using Autocad_Primavera_P6_Plugin.Services.P6ApiService;
using Autocad_Primavera_P6_Plugin.UserInterface.UI_InsertBlocksWindowView;
using Autocad_Primavera_P6_Plugin.UserInterface.UI_LinkedFoldersManagerView;
using System.Reflection;
using System.IO;

using acadApp = Autodesk.AutoCAD.ApplicationServices.Application;
using Autodesk.AutoCAD.EditorInput;
using Autocad_Primavera_P6_Plugin.Services.AutocadService;
using Autodesk.AutoCAD.DatabaseServices;
using System.Collections.Generic;
using System.Linq;
using System.Diagnostics;

[assembly: ExtensionApplication(typeof(Autocad_Primavera_P6_Plugin.MyPlugin))]

namespace Autocad_Primavera_P6_Plugin
{
    public class MyPlugin : IExtensionApplication
    {
        public static MyPlugin Instance { get; private set; } = null;
        public LiteDBService MyLiteDBService { get; private set; } = null;
        public P6ApiService MyP6ApiService { get; private set; } = null;
        public RibbonService MyRibbonService { get; private set; } = null;
        public AutocadService MyAutocadService { get; private set; } = null;


        void IExtensionApplication.Initialize()
        {
            // Plugin Code
            Instance = this;  //Singleton Class pattern
            MyLiteDBService = new LiteDBService(pluginInstance: this);  //LiteDBService instance initialization
            MyP6ApiService = new P6ApiService(pluginInstance: this);  //P6ApiService instance initialization
            MyRibbonService = new RibbonService(pluginInstance: this); //RibbonService instance initialization
            MyAutocadService = new AutocadService(); //AutocadService instance initialization
        }

        void IExtensionApplication.Terminate()
        {
            // Plugin Code
            Instance = null;  //Singleton Class pattern
            MyLiteDBService = null;  //LiteDBService instance cleanup
            MyP6ApiService = null;  //P6ApiService instance cleanup
            MyRibbonService = null; //RibbonService instance cleanup
            MyAutocadService = null; //MyAutocadService instance cleanup
        }

        public async Task P6InsertBlocks()
        {
            var _vm = new InsertBlocksWindowViewModel(this);
            await _vm.Async_Init();

            var _view = new InsertBlocksWindowView(_vm);
            acadApp.ShowModalWindow(_view);
        }

        public void OpenLinkedFoldersManager()
        {
            var view = new LinkedFoldersManagerView(this);
            acadApp.ShowModalWindow(view);
        }

        public void OpenP6ConnectionManager()
        {
            var view = new UserInterface.UI_P6ConnectionManagerView.P6ConnectionManagerView(this);
            acadApp.ShowModalWindow(view);
        }

        public void OpenTestButtonFunction()
        {
            try
            {
                Debug.Print("Test Function is invoked.");

                var doc = acadApp.DocumentManager.MdiActiveDocument;
                var blockRefList = MyAutocadService.GetPluginBlockImpliedSelected(doc);
                var blockRef = blockRefList?.Count > 0 ? blockRefList.Last() : null;
                if (blockRef == null) { return; };
                var x = blockRef.DynamicBlockReferencePropertyCollection;

                BlockReference freshRef;
                using (var tr = doc.Database.TransactionManager.StartTransaction())
                {
                    freshRef = (BlockReference)tr.GetObject(blockRef.ObjectId, OpenMode.ForRead);

                    var pluginBlockRef = new PlugInBlockReference(doc, freshRef);
                    pluginBlockRef.Init(tr);

                    var actCode = pluginBlockRef.BdryActCode;
                    var actCode2 = pluginBlockRef.BlockAttProps;

                    bool isDyn = freshRef.IsDynamicBlock;
                    var props = freshRef.DynamicBlockReferencePropertyCollection;

                    var dynPropDict = new Dictionary<string, DynamicBlockReferenceProperty>();

                    dynPropDict = props
                                    .Cast<DynamicBlockReferenceProperty>()
                                    .ToDictionary(val => val.PropertyName, val => val);

                    Debug.Print($"IsDynamicBlock: {isDyn}, PropCount: {props.Count}");

                    tr.Commit(); // or Abort() since you only read
                }
                ;

                Debug.Print("Test Function is Completed without error.");
            }
            catch (System.Exception e)
            {
                Debug.Print(e.ToString());
                return;
            }
            finally
            {
                Debug.Print("Test Function is Completed.");
            }
        }
    }

}
