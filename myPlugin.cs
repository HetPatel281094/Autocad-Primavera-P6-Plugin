// (C) Copyright 2026 by  
//
using Autocad_Primavera_P6_Plugin.ServiceReference.ActivityService;
using Autocad_Primavera_P6_Plugin.ServiceReference.AuthenticationService;
using Autocad_Primavera_P6_Plugin.Services;
using Autocad_Primavera_P6_Plugin.Services.AutocadService;
using Autocad_Primavera_P6_Plugin.Services.LiteDBService;
using Autocad_Primavera_P6_Plugin.Services.P6ApiService;
using Autocad_Primavera_P6_Plugin.UserInterface.UI_InsertBlocksWindowView;
using Autocad_Primavera_P6_Plugin.UserInterface.UI_LinkedFoldersManagerView;
using Autodesk.AutoCAD.Runtime;
using System.Diagnostics;
using System.Linq;
using System.ServiceModel.Channels;
using System.Threading.Tasks;
using acadApp = Autodesk.AutoCAD.ApplicationServices.Application;

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

                var DBInstanceName = "PMDB";


                var AuthClient = new AuthenticationServicePortTypeClient();
                P6SessionCookieManager.Instance.Bind(AuthClient);

                var ReadDBInstancesReq = new ReadDatabaseInstancesRequest();
                ReadDBInstancesReq.ReadDatabaseInstances = "?";

                var ReadDBInstancesRes = await AuthClient.ReadDatabaseInstancesAsync(ReadDBInstancesReq);

                var DBInstance = ReadDBInstancesRes.ReadDatabaseInstancesResponse1.First(inst => inst.DatabaseName == DBInstanceName);

                var Login = new Login() { UserName = "admin", Password = "Uvpce2006", DatabaseInstanceId = DBInstance.DatabaseInstanceId, DatabaseInstanceIdSpecified = true };

                var LoginReq = new LoginRequest(Login);

                var LoginRes = await AuthClient.LoginAsync(LoginReq);

                var LoginResString = LoginRes.LoginResponse.Return;

                Debug.Print(LoginResString.ToString());


                var ActivityServiceClient = new ActivityPortTypeClient();
                var cookieManager = ActivityServiceClient.InnerChannel.GetProperty<IHttpCookieContainerManager>();
                P6SessionCookieManager.Instance.Bind(ActivityServiceClient);

                var ReadActivities = new ReadActivities();
                ReadActivities.Filter = "ProjectId = 'MASTER'";
                ReadActivities.Field = [ ActivityFieldType.ObjectId, ActivityFieldType.Name, ActivityFieldType.ProjectId, ActivityFieldType.ProjectName, ActivityFieldType.WBSPath];

                var ReadActivitiesReq = new ReadActivitiesRequest(ReadActivities);

                var ReadActivitiesRes = await ActivityServiceClient.ReadActivitiesAsync(ReadActivitiesReq);

                var activities = ReadActivitiesRes.ReadActivitiesResponse1;

                foreach (var item in activities)
                {
                    Debug.Print($"WBS Path : {item.WBSPath} - Act Name : {item.Name}");
                };

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
