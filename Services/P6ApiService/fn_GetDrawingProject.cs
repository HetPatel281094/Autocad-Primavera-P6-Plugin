using System;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using Autocad_Primavera_P6_Plugin.Services.LiteDBService;
using Autodesk.AutoCAD.ApplicationServices;
using AcadApp = Autodesk.AutoCAD.ApplicationServices.Application;

namespace Autocad_Primavera_P6_Plugin.Services.P6ApiService
{
    public partial class P6ApiService
    {
        public async Task<Project> GetP6ProjectFromDWGFile(Autodesk.AutoCAD.ApplicationServices.Document doc)
        {
            try
            {
                var projectConfig = _pluginInstance.MyLiteDBService.Find_byAcadDWG(doc);

                if (projectConfig == null || string.IsNullOrWhiteSpace(projectConfig.ProjectId))
                {
                    Debug.Print($"No P6 ProjectConfig is linked to DWG: {doc?.Name}");
                    return null;
                }

                if (Client == null)
                {
                    Debug.Print("P6 API client is not initialized.");
                    return null;
                }

                var filter = $"Id :eq: '{projectConfig.ProjectId}'";
                var fields = "ObjectId, Id, Name";

                var projects = await Client.GetProjectAsync(filter, fields, null, null);
                var project = projects?.FirstOrDefault();

                if (project == null)
                {
                    Debug.Print($"P6 project was not found. ProjectId: {projectConfig.ProjectId}, DWG: {doc?.Name}");
                }

                return project;
            }
            catch (Exception ex)
            {
                Debug.Print(ex.ToString());
                return null;
            }
        }
    }
}