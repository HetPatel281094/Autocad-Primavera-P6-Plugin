using System;
using System.Collections.Generic;
using System.Diagnostics;
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
        public async Task<ActivityCodeType> GetP6ActivityCodeTypeOfProject(Project project,  string ActivtyCodeTypeName)
        {
            if (project == null || ActivtyCodeTypeName == null || ActivtyCodeTypeName == string.Empty) return null;

            try
            {
                string filter = project != null ? $":and: ( Scope :eq: 'Project' , ProjectObjectId :eq: '{project.ObjectId}' , Name :eq: '{ActivtyCodeTypeName}' )" : null;
                string fields = "CreateDate, CreateUser, EPSCodeTypeHierarchy, EPSObjectId, IsBaseline, IsSecureCode, IsTemplate, LastUpdateDate, LastUpdateUser, Length, Name, ObjectId, ProjectObjectId, RefProjectObjectIds, Scope, SequenceNumber";

                var client = Client;
                var typeColln = await client.GetActivityCodeTypesAsync(filter, fields, null, null).ConfigureAwait(true);
                var type = typeColln?.FirstOrDefault();

                return type;
            }
            catch (Exception ex)
            {
                Debug.Print(ex.ToString());
                return null;
            }
        }

    }
}
