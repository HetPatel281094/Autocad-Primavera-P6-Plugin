// (C) Copyright 2026 by  
//
using Autocad_Primavera_P6_Plugin.Services;
using Autocad_Primavera_P6_Plugin.Services.AutocadService;
using Autocad_Primavera_P6_Plugin.Services.LiteDBService;
using Autocad_Primavera_P6_Plugin.Services.P6ApiService;
using Autocad_Primavera_P6_Plugin.Services.P6UiAutomationService;
using Autocad_Primavera_P6_Plugin.UserInterface.UI_InsertBlocksWindowView;
using Autocad_Primavera_P6_Plugin.UserInterface.UI_LinkedFoldersManagerView;
using Autodesk.AutoCAD.Runtime;
using Autodesk.AutoCAD.Windows;
using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.Versioning;
using System.Threading.Tasks;
using AcadApp = Autodesk.AutoCAD.ApplicationServices.Application;
using AcadTest = Autocad_Primavera_P6_Plugin.Services.AutocadService.Temp;

[assembly: ExtensionApplication(typeof(Autocad_Primavera_P6_Plugin.MyPlugin))]
[assembly: SupportedOSPlatform("windows")]
namespace Autocad_Primavera_P6_Plugin
{
    public class MyPlugin : IExtensionApplication
    {
        private static ResolveEventHandler _assemblyResolveHandler;

        public static MyPlugin Instance { get; private set; } = null;
        public LiteDBService MyLiteDBService { get; private set; } = null;
        public P6ApiService MyP6ApiService { get; private set; } = null;
        public RibbonService MyRibbonService { get; private set; } = null;
        public AutocadService MyAutocadService { get; private set; } = null;
        public P6UiAutomationService MyP6UiAutomationService { get; private set; } = null;


        private static string PropertiesPaletteSet_GUID = "99999999-4444-4444-4444-1234567890AC";
        public static PaletteSet PropertiesPaletteSet = null;


        void IExtensionApplication.Initialize()
        {
            // Register before any plugin code starts using assemblies
            _assemblyResolveHandler = ResolvePluginAssembly;
            AppDomain.CurrentDomain.AssemblyResolve += _assemblyResolveHandler;

            // Plugin Code
            Instance = this;  //Singleton Class pattern
            MyLiteDBService = new LiteDBService(pluginInstance: this);  //LiteDBService instance initialization
            MyP6ApiService = new P6ApiService(pluginInstance: this);  //P6ApiService instance initialization
            MyRibbonService = new RibbonService(pluginInstance: this); //RibbonService instance initialization
            MyAutocadService = new AutocadService(); //AutocadService instance initialization
            MyP6UiAutomationService = new P6UiAutomationService();  //P6UiAutomationService instance initialization
        }

        void IExtensionApplication.Terminate()
        {
            if (_assemblyResolveHandler != null)
            {
                AppDomain.CurrentDomain.AssemblyResolve -= _assemblyResolveHandler;
                _assemblyResolveHandler = null;
            }

            // Plugin Code
            Instance = null;  //Singleton Class pattern
            MyLiteDBService = null;  //LiteDBService instance cleanup
            MyP6ApiService = null;  //P6ApiService instance cleanup
            MyRibbonService = null; //RibbonService instance cleanup
            MyAutocadService = null; //MyAutocadService instance cleanup
            MyP6UiAutomationService = null; //MyP6UiAutomationService instance cleanup
        }

        public async Task P6ShowPropertirs()
        {
            if (PropertiesPaletteSet == null)
            {
                PropertiesPaletteSet = new PaletteSet("Properties Palate UI", new Guid(PropertiesPaletteSet_GUID))
                {
                    Dock = DockSides.Right,
                };

                var view = new Autocad_Primavera_P6_Plugin.UserInterface.UI_PropertiesPalateView.PropertiesPalateView();

                PropertiesPaletteSet.AddVisual("Main", view);
            }

            PropertiesPaletteSet.Visible = true;
        }

        public async Task P6InsertBlocks()
        {
            var _vm = new InsertBlocksWindowViewModel(this);
            await _vm.Async_Init();

            var _view = new InsertBlocksWindowView(_vm);
            AcadApp.ShowModalWindow(_view);
        }

        public void OpenLinkedFoldersManager()
        {
            var view = new LinkedFoldersManagerView(this);
            AcadApp.ShowModalWindow(view);
        }

        public void OpenP6ConnectionManager()
        {
            var view = new UserInterface.UI_P6ConnectionManagerView.P6ConnectionManagerView(this);
            AcadApp.ShowModalWindow(view);
        }

        public async void OpenTestButtonFunction()
        {
            try
            {
                Debug.Print("Test Function is invoked.");

                var _doc = AcadApp.DocumentManager.MdiActiveDocument;
                var _db = _doc.Database;

                var _selectedObjs = MyAutocadService.GetPluginBlockImpliedSelected(_doc);
                var _selectedObj = _selectedObjs.First();

                if (_selectedObj == null) { return; }

                var _pluginBlockRef = new AcadTest.PlugInBlockReference(_selectedObj, true);
                await _pluginBlockRef.Async_Init();

                _pluginBlockRef.Refresh_ReportTable();

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

        private static Assembly ResolvePluginAssembly(object sender, ResolveEventArgs args)
        {
            try
            {
                var requestedAssemblyName = new AssemblyName(args.Name);

                string pluginDirectory = Path.GetDirectoryName(
                    typeof(MyPlugin).Assembly.Location
                );

                if (string.IsNullOrEmpty(pluginDirectory))
                {
                    return null;
                }

                string assemblyPath = Path.Combine(
                    pluginDirectory,
                    requestedAssemblyName.Name + ".dll"
                );

                if (!File.Exists(assemblyPath))
                {
                    return null;
                }

                return Assembly.LoadFrom(assemblyPath);
            }
            catch
            {
                return null;
            }
        }

    }

}
