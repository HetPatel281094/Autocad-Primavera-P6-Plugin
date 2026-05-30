using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net;
using System.Text;
using System.Threading.Tasks;

namespace Autocad_Primavera_P6_Plugin.Services.P6ApiService
{
    public class P6ApiService
    {
        private MyPlugin _pluginInstance = null;

        
        private CookieContainer cookieContainer;                // 1. Create a container to store the cookies
        private HttpClientHandler handler;                      // 2. Setup the handler to use that container
        private HttpClient httpClient;                          // 3. Create the HttpClient using the handler
        public Client Client;                                   // 4. Create the P6 REST API client using the HttpClient

        public P6ApiService(MyPlugin pluginInstance)
        {
            // Initialize P6 API connection and setup here
            _pluginInstance = pluginInstance;
            InIt();
        }

        private void InIt()
        {
            cookieContainer = new CookieContainer();

            handler = new HttpClientHandler()
            {
                CookieContainer = cookieContainer,
                UseCookies = true // This ensures the client automatically sends/receives cookies
            };

            httpClient = new HttpClient(handler);

            Client = new Client("http://localhost:8206/p6ws/restapi/", httpClient);

            Client.LoginAsync("admin", "Uvpce2006", "PMDB");
        }

    }
}
