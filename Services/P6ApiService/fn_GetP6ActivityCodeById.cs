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
        public async Task<ActivityCode> GetP6ActivityCodeById(int actCodeId)
        {
            if (actCodeId <= 0) return null;

            try
            {
                string filter = $"ObjectId :eq: {actCodeId}";
                string fields = "CodeConcatName, CodeTypeName, CodeTypeObjectId, CodeTypeScope, CodeValue, Color, CreateDate, CreateUser, Description, LastUpdateDate, LastUpdateUser, ObjectId, ParentObjectId, ProjectObjectId, SequenceNumber";
                
                var client = Client;
                var codeColln = await client.GetActivityCodesAsync(filter, fields, null, null).ConfigureAwait(true);
                var code = codeColln?.FirstOrDefault();
                return code;

            }
            catch (Exception ex)
            {
                Debug.Print(ex.ToString());
                return null;
            };
        }

    }
}
