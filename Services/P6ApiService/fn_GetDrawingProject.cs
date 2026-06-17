using System;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using Autocad_Primavera_P6_Plugin.Services.LiteDBService;
using Autodesk.AutoCAD.ApplicationServices;

namespace Autocad_Primavera_P6_Plugin.Services.P6ApiService
{
    public partial class P6ApiService
    {
        public Project GetP6ProjectFromDWGFile(Autodesk.AutoCAD.ApplicationServices.Document doc) {

            var ProjectConfig = _pluginInstance.MyLiteDBService.Find_byAcadDWG(doc);
            var client = _pluginInstance.MyP6ApiService.Client;

            var filter = $"Id :eq: '{ProjectConfig.ProjectId}'";
            var fields = "ObjectId, Id, Name";

            var projects = client.GetProjectAsync(filter, fields, null, null).Result;
            var project = projects.First();

            return project;
        }
    }
}