using System;
using System.Net;
using System.ServiceModel;
using System.ServiceModel.Channels;

namespace Autocad_Primavera_P6_Plugin.Services.P6ApiService
{
    /// <summary>
    /// App-lifetime singleton holding the single CookieContainer used by every
    /// P6 SOAP client. Call Bind(client) immediately after constructing any
    /// generated PortTypeClient, before making its first request.
    /// </summary>
    public sealed class P6SessionCookieManager
    {
        private static readonly Lazy<P6SessionCookieManager> _lazy =
            new Lazy<P6SessionCookieManager>(() => new P6SessionCookieManager());

        public static P6SessionCookieManager Instance => _lazy.Value;

        /// <summary>
        /// The one CookieContainer shared by every P6 client for the life of
        /// the plugin session. Once AuthenticationService.Login succeeds, the
        /// JSESSIONID the P6 server sets back lives here.
        /// </summary>
        public CookieContainer Cookies { get; } = new CookieContainer();

        // Guards Bind() against two clients racing to wire up on different
        // threads at plugin startup (e.g. concurrent Async_Init calls).
        private readonly object _bindLock = new object();

        private P6SessionCookieManager()
        {
        }

        /// <summary>
        /// Wires a freshly-created P6 SOAP client to use the shared session
        /// CookieContainer instead of its own private one.
        /// </summary>
        /// <typeparam name="TChannel">
        /// The service contract interface, e.g. AuthenticationServicePortType
        /// or ActivityServicePortType — inferred automatically when you pass
        /// the client, you don't need to specify it.
        /// </typeparam>
        /// <param name="client">
        /// A client you just constructed, e.g. new AuthenticationServicePortTypeClient().
        /// Bind it BEFORE making any calls on it.
        /// </param>
        public void Bind<TChannel>(ClientBase<TChannel> client) where TChannel : class
        {
            if (client == null)
            {
                throw new ArgumentNullException(nameof(client));
            }

            var cookieManager = client.InnerChannel.GetProperty<IHttpCookieContainerManager>();

            if (cookieManager == null)
            {
                // Would only happen if a future generated client's binding
                // doesn't have AllowCookies = true set. Both of your current
                // Reference.cs files already set it, so this is a guard rail
                // for services you add later.
                throw new InvalidOperationException(
                    $"{typeof(TChannel).Name}'s binding does not expose a cookie container. " +
                    "Confirm BasicHttpBinding.AllowCookies = true for this endpoint in its Reference.cs.");
            }

            lock (_bindLock)
            {
                cookieManager.CookieContainer = Cookies;
            }
        }

        /// <summary>
        /// Clears the P6 session (e.g. on Logout, or a failed re-login) so the
        /// next Login starts clean instead of presenting a stale JSESSIONID.
        /// </summary>
        public void Reset()
        {
            foreach (Cookie cookie in Cookies.GetAllCookies())
            {
                cookie.Expired = true;
            }
        }

        /// <summary>Quick way to check whether a session cookie is currently held.</summary>
        public bool HasSessionCookie => Cookies.Count > 0;
    }
}
