using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Nakama;
using Hiro;
using UnityEngine;
using Hiro.Unity;
using Hiro.System;

namespace HiroStore
{
    public class HiroStoreCoordinator : HiroCoordinator
    {
        [Header("Overrides Nakama Settings")]
        [SerializeField] private bool localHost;

        public bool IsLocalHost => localHost;

        [Header("Nakama Settings")]
        [SerializeField] private string scheme = "https";
        [SerializeField] private string host = "sample-prjcts.eu-west1-a.nakamacloud.io";
        [SerializeField] private int port = 443;
        [SerializeField] private string serverKey = "uNezOE3FOprj6nPs";

        [Header("Satori Settings")]
        [SerializeField] private string satoriScheme = "https";
        [SerializeField] private string satoriHost = "demo.us-east1-b.satoricloud.io";
        [SerializeField] private int satoriPort = 443;
        [SerializeField] private string satoriApiKey = "297c3e54-1ce4-40db-bd86-87f81aeac640";

        public event Action<Exception> ReceivedStartError;
        public event Action<ISession> ReceivedStartSuccess;

        protected override Task<Systems> CreateSystemsAsync()
        {
            var logger = new Hiro.Unity.Logger();
            var monitor = NetworkMonitor.Default;

            var client = localHost
                ? new Client("http", "127.0.0.1", 7350, "defaultkey")
                : new Client(scheme, host, port, serverKey);

            var env = localHost ? "local" : "heroiclabs";
            var nakamaSystem = new NakamaSystem(logger, client, NakamaAuthorizerFunc(env));

            var storage = MemoryStorage.Default;

            // Register necessary Hiro Systems
            var systems = new Systems(nameof(HiroStoreCoordinator), monitor, storage, logger);
            systems.Add(nakamaSystem);
            var economySystem = new EconomySystem(logger, nakamaSystem, EconomyStoreType.Unspecified);
            systems.Add(economySystem);

            var satoriClient = new Satori.Client(satoriScheme, satoriHost, satoriPort, satoriApiKey,
                Satori.UnityWebRequestAdapter.Instance);
            var satoriSystem = new SatoriSystem(logger, satoriClient, SatoriAuthorizerFunc(nakamaSystem, env));
            systems.Add(satoriSystem);

            _env = env;

            return Task.FromResult(systems);
        }

        private const string PlayerPrefsSatoriAuthToken = "satori.AuthToken";
        private const string PlayerPrefsSatoriRefreshToken = "satori.RefreshToken";

        private string _env = "default";

        /// <summary>
        /// Authorizes the Satori session with the Nakama user ID as the Satori identity.
        /// The server-side SatoriPersonalizer looks players up in Satori by their Nakama user ID,
        /// so the client must authenticate as the same identity for audience targeting to apply.
        /// Systems initialize in registration order, so the Nakama session exists by the time this runs.
        /// </summary>
        public static SatoriSystem.AuthorizerFunc SatoriAuthorizerFunc(NakamaSystem nakamaSystem, string env = "default")
        {
            return async client =>
            {
                var userId = nakamaSystem.Session.UserId;

                // Attempt to load a previous session if it belongs to the same player and is still valid.
                var authToken = PlayerPrefs.GetString($"{PlayerPrefsSatoriAuthToken}_{env}");
                var refreshToken = PlayerPrefs.GetString($"{PlayerPrefsSatoriRefreshToken}_{env}");
                var session = Satori.Session.Restore(authToken, refreshToken);

                // Add an hour, so we check whether the token is within an hour of expiration to refresh it.
                var expiredDate = DateTime.UtcNow.AddHours(1);
                if (session != null && session.IdentityId == userId && !session.HasRefreshExpired(expiredDate))
                {
                    try
                    {
                        // Validate the session by refreshing it
                        session = await client.SessionRefreshAsync(session);
                        PlayerPrefs.SetString($"{PlayerPrefsSatoriAuthToken}_{env}", session.AuthToken);
                        PlayerPrefs.SetString($"{PlayerPrefsSatoriRefreshToken}_{env}", session.RefreshToken);
                        return session;
                    }
                    catch (Satori.ApiResponseException e)
                    {
                        Debug.LogWarning($"Stored Satori session invalid ({e.Message}), re-authenticating...");
                    }
                }

                return await AuthenticateSatoriAsync(client, userId, env);
            };
        }

        /// <summary>
        /// Authenticates a Satori session for the given user ID and stores its tokens.
        /// The default properties sent here are available for audience filters in the Satori Console.
        /// </summary>
        public static async Task<Satori.ISession> AuthenticateSatoriAsync(Satori.IClient client, string userId, string env)
        {
            var defaultProperties = new Dictionary<string, string>
            {
                { "platform", Application.platform.ToString() },
                { "language", Application.systemLanguage.ToString() }
            };

            var session = await client.AuthenticateAsync(userId, defaultProperties);

            PlayerPrefs.SetString($"{PlayerPrefsSatoriAuthToken}_{env}", session.AuthToken);
            PlayerPrefs.SetString($"{PlayerPrefsSatoriRefreshToken}_{env}", session.RefreshToken);

            return session;
        }

        /// <summary>
        /// Re-authenticates the Satori session when the active Nakama account changes,
        /// so audience targeting follows the account currently in use.
        /// </summary>
        public async Task AlignSatoriIdentityAsync()
        {
            var nakamaSystem = this.GetSystem<NakamaSystem>();
            var satoriSystem = this.GetSystem<SatoriSystem>();
            if (nakamaSystem == null || satoriSystem?.Session == null) return;

            var userId = nakamaSystem.Session.UserId;
            if (satoriSystem.Session.IdentityId == userId) return;

            var newSession = await AuthenticateSatoriAsync(satoriSystem.Client, userId, _env);
            if (satoriSystem.Session is Satori.Session current)
            {
                current.Update(newSession.AuthToken, newSession.RefreshToken);
            }
        }



        public static NakamaSystem.AuthorizerFunc NakamaAuthorizerFunc(string env = "default", int index = 0)
        {
            const string playerPrefsAuthToken = "nakama.AuthToken";
            const string playerPrefsRefreshToken = "nakama.RefreshToken";
            const string playerPrefsDeviceId = "nakama.DeviceId";

            var keySuffix = $"{env}_{index}";

            return async client =>
            {
                // Attempt to load a previous session if it is still valid.
                var authToken = PlayerPrefs.GetString($"{playerPrefsAuthToken}_{keySuffix}");
                var refreshToken = PlayerPrefs.GetString($"{playerPrefsRefreshToken}_{keySuffix}");
                var session = Session.Restore(authToken, refreshToken);

                // Add an hour, so we check whether the token is within an hour of expiration to refresh it.
                var expiredDate = DateTime.UtcNow.AddHours(1);
                if (session != null && !session.HasRefreshExpired(expiredDate))
                {
                    try
                    {
                        // Validate the session by refreshing it
                        session = await client.SessionRefreshAsync(session);
                        PlayerPrefs.SetString($"{playerPrefsAuthToken}_{keySuffix}", session.AuthToken);
                        PlayerPrefs.SetString($"{playerPrefsRefreshToken}_{keySuffix}", session.RefreshToken);
                        return session;
                    }
                    catch (ApiResponseException e) when (
                        e.Message.Contains("Refresh token invalid or expired") ||
                        e.Message.Contains("User account not found") ||
                        e.Message.Contains("Auth token invalid"))
                    {
                        Debug.LogWarning($"Stored session invalid ({e.Message}), clearing tokens and re-authenticating...");
                        PlayerPrefs.DeleteKey($"{playerPrefsAuthToken}_{keySuffix}");
                        PlayerPrefs.DeleteKey($"{playerPrefsRefreshToken}_{keySuffix}");
                    }
                }

                // Fall through: Authenticate with device ID
                var deviceId = PlayerPrefs.GetString($"{playerPrefsDeviceId}_{env}", SystemInfo.deviceUniqueIdentifier);
                if (deviceId == SystemInfo.unsupportedIdentifier) deviceId = Guid.NewGuid().ToString();

                session = await client.AuthenticateDeviceAsync($"{deviceId}_{index}");

                // Store tokens to avoid needing to re-authenticate next time.
                PlayerPrefs.SetString($"{playerPrefsDeviceId}_{env}", deviceId);
                PlayerPrefs.SetString($"{playerPrefsAuthToken}_{keySuffix}", session.AuthToken);
                PlayerPrefs.SetString($"{playerPrefsRefreshToken}_{keySuffix}", session.RefreshToken);

                if (session.Created) Debug.LogFormat("New user account '{0}' created.", session.UserId);

                return session;
            };
        }

        protected override void SystemsInitializeCompleted()
        {
            ReceivedStartSuccess?.Invoke(this.GetSystem<NakamaSystem>().Session);
        }

        protected override void SystemsInitializeFailed(Exception e)
        {
            ReceivedStartError?.Invoke(e);
        }
    }
}