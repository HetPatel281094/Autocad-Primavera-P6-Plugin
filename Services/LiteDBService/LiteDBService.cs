using Autodesk.AutoCAD.ApplicationServices;
using System.IO;
using LiteDB;
using System;

namespace Autocad_Primavera_P6_Plugin.Services.LiteDBService
{
    public partial class LiteDBService
    {
        private MyPlugin _pluginInstance = null;

        private LiteDatabase pluginDb = null;
            private string pluginAppDataFolderName = "Autocad_Primavera_P6_Plugin";
            private string liteDbFileName = "MyPluginTemplateDatabase.litedb";

        public LiteDBService(MyPlugin pluginInstance)
        {
            // Initialize LiteDB connection and setup here
            _pluginInstance = pluginInstance;
            CheckCreatePluginFolder();
            CreateNewConnection();
        }

        private void CheckCreatePluginFolder()
        {
            var appDataFolder = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            var pluginAppDataFolderPath = Path.Combine(appDataFolder, pluginAppDataFolderName);
            if (!Directory.Exists(pluginAppDataFolderPath))
            {
                Directory.CreateDirectory(pluginAppDataFolderPath);
            }
        }

        public void CreateNewConnection()
        {
            var appDataFolder = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            var pluginAppDataFolderPath = Path.Combine(appDataFolder, pluginAppDataFolderName);
            var liteDbFilePath = Path.Combine(pluginAppDataFolderPath, liteDbFileName);
            try
            {
                this.pluginDb = new LiteDatabase(liteDbFilePath);
                init_ProjectConfigsColn();
                init_P6ConnectionConfigsColn();
            }
            catch (System.Exception ex)
            {
                var ed = Autodesk.AutoCAD.ApplicationServices.Application.DocumentManager.MdiActiveDocument.Editor;
                ed.WriteMessage($"\n[MyPlugin] Error initializing LiteDB connection: {ex.Message}");
            }
        }

    }
}
