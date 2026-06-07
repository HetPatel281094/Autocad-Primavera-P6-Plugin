using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;

namespace Autocad_Primavera_P6_Plugin.Services.P6ApiService
{
    public partial class Client
    {
        // This method is called by the generated code before every request
        partial void PrepareRequest(System.Net.Http.HttpClient client, System.Net.Http.HttpRequestMessage request, string url)
        {
            // Inspect the request here
            var method = request.Method;
            var headers = request.Headers;

            // Example: Add a custom header or log the URL
            Console.WriteLine($"Sending {method} request to: {url}");
        }

        partial void ProcessResponse(System.Net.Http.HttpClient client, HttpResponseMessage response)
        {
            // Inspect the response here
            var statusCode = response.StatusCode;
            var headers = response.Headers;

            var resContent = response.Content.ReadAsStringAsync().Result;

            // Example: Log the status code
            Console.WriteLine($"Received response with status code: {statusCode}");
        }

        // This method is called by the generated constructor
        static partial void UpdateJsonSerializerSettings(Newtonsoft.Json.JsonSerializerSettings settings)
        {
            // 1. Ignore if the JSON has extra fields your C# doesn't have
            settings.MissingMemberHandling = Newtonsoft.Json.MissingMemberHandling.Ignore;

            // 2. Ignore the "Required = Required.Always" constraint 
            // This is the specific fix for your ParentEPSObjectId error
            settings.MetadataPropertyHandling = Newtonsoft.Json.MetadataPropertyHandling.Ignore;

            // Note: If 'Required.Always' still causes issues, 
            // you might need a custom ContractResolver as shown below:
            settings.ContractResolver = new IgnoreRequiredContractResolver();
        }

    }

    // A helper class to force JSON.NET to ignore the "Required" attribute
    public class IgnoreRequiredContractResolver : Newtonsoft.Json.Serialization.DefaultContractResolver
    {
        protected override Newtonsoft.Json.Serialization.JsonProperty CreateProperty(
            System.Reflection.MemberInfo member,
            Newtonsoft.Json.MemberSerialization memberSerialization)
        {
            var property = base.CreateProperty(member, memberSerialization);
            property.Required = Newtonsoft.Json.Required.Default; // Force it to NOT be required
            return property;
        }
    }

}
