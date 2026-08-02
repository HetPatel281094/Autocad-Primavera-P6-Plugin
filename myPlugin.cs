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
using Autodesk.AutoCAD.Geometry;

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

        public async void OpenTestButtonFunction()
        {
            try
            {
                Debug.Print("Test Function is invoked.");

                var doc = acadApp.DocumentManager.MdiActiveDocument;

                var blockRefList = MyAutocadService.GetPluginBlockImpliedSelected(doc);
                var blockRef = blockRefList?.Count > 0 ? blockRefList.Last() : null;
                if (blockRef == null) { return; };

                BlockReference freshRef;

                using (doc.LockDocument())
                using (var tr = doc.Database.TransactionManager.StartTransaction())
                {
                    freshRef = (BlockReference)tr.GetObject(blockRef.ObjectId, OpenMode.ForRead);

                    var pluginBlockRef = new PlugInBlockReference(this, doc, freshRef, tr);
                    await pluginBlockRef.AsyncInit();

                    // Props
                    var v1 = pluginBlockRef.InitState;
                    var v2 = pluginBlockRef.LoadWarnings;

                    var v3 = pluginBlockRef.Get_BlockPosition();
                    if (v3.HasValue) { var v4 = pluginBlockRef.Set_BlockPosition(v3.Value, out _); };

                    var v5 = pluginBlockRef.Get_InfoPosition();
                    if (v5.HasValue) { var v6 = pluginBlockRef.Set_InfoPosition(v5.Value, out _, tr); };

                    var bdryActCode = await pluginBlockRef.Get_BdryActCode();
                    if (bdryActCode != null) { pluginBlockRef.Set_BdryActCode(bdryActCode, out _, tr); };

                    var elementIdCode = await pluginBlockRef.Get_ElementIdCode();
                    if (elementIdCode != null) { 
                        pluginBlockRef.Set_ElementIdCode(elementIdCode, out _, tr); 
                        pluginBlockRef.Set_ElementId(out _, tr: tr);
                    };

                    var v7 = pluginBlockRef.Set_BlockType(out _, tr: tr);

                    tr.Commit(); // or Abort() since you only read
                };

                var _ed = doc.Editor;
                _ed.SetImpliedSelection(new ObjectId[0]);
                _ed.SetImpliedSelection(new ObjectId[] { blockRef.ObjectId });

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
