using Autocad_Primavera_P6_Plugin.ServiceReference.ActivityService;
using Autocad_Primavera_P6_Plugin.ServiceReference.AuthenticationService;
using Autocad_Primavera_P6_Plugin.Services.LiteDBService;
using System;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.ServiceModel.Channels;
using System.Threading.Tasks;

namespace Autocad_Primavera_P6_Plugin.Services.P6ApiService
{
    public class SOAPClient
    {
        public CookieContainer CookieContainer { get; set; }

        public AuthenticationServicePortTypeClient AuthClient { get; set; }

        public ActivityPortTypeClient ActivityClient { get; set;  }

        public SOAPClient() 
        {
            CookieContainer = new CookieContainer();
            AuthClient = new AuthenticationServicePortTypeClient();
            ActivityClient = new ActivityPortTypeClient();

            var AuthCookieManager = AuthClient.InnerChannel.GetProperty<IHttpCookieContainerManager>();
            var ActivityCookieManager = ActivityClient.InnerChannel.GetProperty<IHttpCookieContainerManager>();

            AuthCookieManager.CookieContainer = CookieContainer;
            ActivityCookieManager.CookieContainer = CookieContainer;
        }

        public async Task LoginAsync(P6ConnectionConfig config)
        {
            var ReadDBInstancesReq = new ReadDatabaseInstancesRequest { ReadDatabaseInstances = "?" };

            var ReadDBInstancesRes = await AuthClient.ReadDatabaseInstancesAsync(ReadDBInstancesReq);

            var DBInstance = ReadDBInstancesRes.ReadDatabaseInstancesResponse1.First(inst => inst.DatabaseName == config.DatabaseName);

            var Login = new Login() { UserName = config.Username, Password = config.Password, DatabaseInstanceId = DBInstance.DatabaseInstanceId, DatabaseInstanceIdSpecified = true };

            var LoginReq = new LoginRequest(Login);

            var LoginRes = await AuthClient.LoginAsync(LoginReq);
        }

    }

    public class LoginStatusChangedEventArgs : EventArgs
    {
        public bool IsLoggedIn { get; }

        public LoginStatusChangedEventArgs(bool isLoggedIn)
        {
            IsLoggedIn = isLoggedIn;
        }
    }

    public partial class P6ApiService
    {
        private readonly MyPlugin _pluginInstance;

        public Client              Client                { get; private set; }
        public bool                IsLoggedIn            { get; private set; }
        public P6ConnectionConfig  LoginConnectionConfig { get; private set; }
        public SOAPClient          SOAPClient            { get; private set; }


        public event EventHandler<LoginStatusChangedEventArgs> LoginStatusChanged;
        private void RaiseLoginStatusChanged() => LoginStatusChanged?.Invoke(this, new LoginStatusChangedEventArgs(IsLoggedIn));


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
                SOAPClient            = null;
                Console.WriteLine("P6ApiService: No default connection configured.");
                RaiseLoginStatusChanged();
                return;
            }

            var result = await TryLoginAsync(defaultConfig).ConfigureAwait(false);

            if (result.IsLoggedIn)
            {
                IsLoggedIn            = true;
                LoginConnectionConfig = result.LoginConnectionConfig;
                Client                = result.Client;
                SOAPClient            = result.SOAPClient;
            }
            else
            {
                IsLoggedIn            = false;
                LoginConnectionConfig = null;
                Client                = null;
                SOAPClient            = null;
                Console.WriteLine("P6ApiService ReInitializeAsync: login failed for default config.");
            }

            RaiseLoginStatusChanged();
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
                //REST Client
                var cookieContainer = new CookieContainer();
                var handler         = new HttpClientHandler { CookieContainer = cookieContainer, UseCookies = true };
                var httpClient      = new HttpClient(handler);
                var client          = new Client(config.ServerUrl, httpClient);

                await client.LoginAsync(config.Username, config.Password, config.DatabaseName).ConfigureAwait(false);

                //SOAP Client
                var soapClient      = new SOAPClient();

                await soapClient.LoginAsync(config);

                return new LoginResult { IsLoggedIn = true, Client = client, LoginConnectionConfig = config, SOAPClient = soapClient };
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
        public SOAPClient         SOAPClient            { get; set; }
    }

}
