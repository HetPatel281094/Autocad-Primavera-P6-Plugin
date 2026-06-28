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
            var _activeDoc = acadApp.DocumentManager.MdiActiveDocument;
            var _selectedBlocks = MyAutocadService.GetPluginBlockImpliedSelected(_activeDoc);
            var _selectedBlock = _selectedBlocks.Last() ?? null;

            try
            {
                PlugInBlockReference _blockRef = new PlugInBlockReference(_activeDoc, _selectedBlock);
            }
            catch (System.Exception e) { };

            var _viewModel = new InsertBlocksWindowViewModel(this, _selectedBlock);
            await _viewModel.Async_Init();

            var _view = new InsertBlocksWindowView(_viewModel);
            Application.ShowModalWindow(_view);
        }

        public void OpenLinkedFoldersManager()
        {
            var view = new LinkedFoldersManagerView(this);
            Application.ShowModalWindow(view);
        }

        public void OpenP6ConnectionManager()
        {
            var view = new UserInterface.UI_P6ConnectionManagerView.P6ConnectionManagerView(this);
            Application.ShowModalWindow(view);
        }
    }

}
