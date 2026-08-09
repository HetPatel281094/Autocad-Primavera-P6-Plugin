using System;
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
        public async Task<Project> GetP6ProjectFromDWGFile(Autodesk.AutoCAD.ApplicationServices.Document doc) {
            try
            {
                var ProjectConfig = _pluginInstance.MyLiteDBService.Find_byAcadDWG(doc);

                var filter = $"Id :eq: '{ProjectConfig.ProjectId}'";
                var fields = "ObjectId, Id, Name";

                var projects = await Client.GetProjectAsync(filter, fields, null, null);
                var project = projects.First();

                return project;
            }
            catch (Exception ex)
            {
                return null;
            }
        }
    }
}