// (C) Copyright 2026 by  
//
using Autocad_Primavera_P6_Plugin.ServiceReference.ActivityService;
using Autocad_Primavera_P6_Plugin.Services;
using Autocad_Primavera_P6_Plugin.Services.AutocadService;
using Autocad_Primavera_P6_Plugin.Services.LiteDBService;
using Autocad_Primavera_P6_Plugin.Services.P6ApiService;
using Autocad_Primavera_P6_Plugin.UserInterface.UI_InsertBlocksWindowView;
using Autocad_Primavera_P6_Plugin.UserInterface.UI_LinkedFoldersManagerView;
using Autodesk.AutoCAD.Runtime;
using System.Diagnostics;
using System.Threading.Tasks;
using AcadApp = Autodesk.AutoCAD.ApplicationServices.Application;

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

                var soapClient = MyP6ApiService.SOAPClient;

                var sourceActivityObjId = 113976;

                var destinationWBSObjId = 27821;

                var copyActObj = new CopyActivity
                {
                    ObjectId = sourceActivityObjId,
                    TargetWBSObjectId = destinationWBSObjId,
                    TargetWBSObjectIdSpecified = true,
                    CopyResourceAndRoleAssignmentsSpecified = true
                };

                var CopyActivityRes = await soapClient.ActivityClient.CopyActivityAsync(
                    new CopyActivityRequest(copyActObj)
                );

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
