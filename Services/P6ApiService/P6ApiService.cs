using System;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using Autocad_Primavera_P6_Plugin.Services.LiteDBService;

namespace Autocad_Primavera_P6_Plugin.Services.P6ApiService
{
    public partial class P6ApiService
    {
        private readonly MyPlugin _pluginInstance;

        public Client              Client               { get; private set; }
        public bool                IsLoggedIn           { get; private set; }
        public P6ConnectionConfig  LoginConnectionConfig { get; private set; }

        public P6ApiService(MyPlugin pluginInstance)
        {
            _pluginInstance = pluginInstance;
            _ = ReInitializeAsync();   // fire-and-forget on startup
        }

        // ── Public surface ──────────────────────────────────────────────────────

        /// <summary>
        /// Reads the current default connection from the DB, logs in, and updates
        /// the live Client / IsLoggedIn / LoginConnectionConfig properties.
        ///
        /// Called:
        ///   • Once automatically in the constructor (startup).
        ///   • By P6ConnectionManagerViewModel.SetDefault() whenever the user
        ///     promotes a different connection to default, so the plugin immediately
        ///     switches to the new credentials without requiring a restart.
        /// </summary>
        public async Task ReInitializeAsync()
        {
            var defaultConfig = _pluginInstance.MyLiteDBService.GetDefault_P6ConnectionConfig();

            if (defaultConfig == null)
            {
                IsLoggedIn            = false;
                LoginConnectionConfig = null;
                Client                = null;
                Console.WriteLine("P6ApiService: No default connection configured.");
                return;
            }

            var result = await TryLoginAsync(defaultConfig).ConfigureAwait(false);

            if (result.IsLoggedIn)
            {
                IsLoggedIn            = true;
                LoginConnectionConfig = result.LoginConnectionConfig;
                Client                = result.Client;
            }
            else
            {
                IsLoggedIn            = false;
                LoginConnectionConfig = null;
                Client                = null;
                Console.WriteLine("P6ApiService ReInitializeAsync: login failed for default config.");
            }
        }

        /// <summary>
        /// Attempts a login with the supplied config and returns the result.
        /// Does NOT mutate the service's own state — use ReInitializeAsync() for that.
        /// Called by the Connection Manager "Test Connection" button as well as
        /// ReInitializeAsync() internally.
        /// </summary>
        public async Task<LoginResult> TryLoginAsync(P6ConnectionConfig config)
        {
            try
            {
                var cookieContainer = new CookieContainer();
                var handler         = new HttpClientHandler { CookieContainer = cookieContainer, UseCookies = true };
                var httpClient      = new HttpClient(handler);
                var client          = new Client(config.ServerUrl, httpClient);

                await client.LoginAsync(config.Username, config.Password, config.DatabaseName)
                            .ConfigureAwait(false);

                return new LoginResult { IsLoggedIn = true, Client = client, LoginConnectionConfig = config };
            }
            catch (Exception ex)
            {
                Console.WriteLine($"P6ApiService TryLoginAsync failed: {ex.Message}");
                return new LoginResult { IsLoggedIn = false };
            }
        }
    }

    public class LoginResult
    {
        public bool               IsLoggedIn            { get; set; }
        public P6ConnectionConfig LoginConnectionConfig { get; set; }
        public Client             Client                { get; set; }
    }
}
