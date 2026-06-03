using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using Autocad_Primavera_P6_Plugin.Services.LiteDBService;

namespace Autocad_Primavera_P6_Plugin.Services.P6ApiService
{
    public class P6ApiService
    {
        private MyPlugin _pluginInstance = null;

        public Client Client;                                   // 1. Create the P6 REST API client using the HttpClient
        public bool IsLoggedIn;                                 // 2. Track login state (optional but useful)
        public P6ConnectionConfig LoginConnectionConfig;        // 3. Store the current login connection config (optional but useful)

        public P6ApiService(MyPlugin pluginInstance)
        {
            // Initialize P6 API connection and setup here
            _pluginInstance = pluginInstance;
            _ = InIt();
        }

        private async Task InIt()
        {
            {
                var _defaultConfig = _pluginInstance.MyLiteDBService.GetDefault_P6ConnectionConfig();

                if (_defaultConfig == null) return;

                var _loginResult = await TryLoginAsync(_defaultConfig);

                if(_loginResult.IsLoggedIn)
                {
                    IsLoggedIn = _loginResult.IsLoggedIn;
                    LoginConnectionConfig = _loginResult.LoginConnectionConfig;
                    Client = _loginResult.Client;
                }
                else
                {
                    IsLoggedIn = false;
                    LoginConnectionConfig = null;
                    Client = null;
                    Console.WriteLine("P6ApiService InIt: Default P6 connection config found but login failed.");
                }
            }

        }

        public async Task<LoginResult> TryLoginAsync(P6ConnectionConfig _p6ConnectionConfig)
        {
            try
            {
                var _cookieContainer = new CookieContainer();
                var _handler = new HttpClientHandler() { CookieContainer = _cookieContainer, UseCookies = true };
                var _httpClient = new HttpClient(_handler);
                var _client = new Client(_p6ConnectionConfig.ServerUrl, _httpClient);

                await _client.LoginAsync(
                    _p6ConnectionConfig.Username, 
                    _p6ConnectionConfig.Password, 
                    _p6ConnectionConfig.DatabaseName
                );

                return new LoginResult
                {
                    IsLoggedIn = true,
                    Client = _client,
                    LoginConnectionConfig = _p6ConnectionConfig
                };
            }
            catch (Exception ex)
            {
                Console.WriteLine($"P6ApiService Login failed: {ex.Message}");
                return new LoginResult
                {
                    IsLoggedIn = false
                };
            }
        }

    }

    public class LoginResult
    {
        public bool IsLoggedIn { get; set; }
        public P6ConnectionConfig LoginConnectionConfig { get; set; }
        public Client Client { get; set; }
    }
}
