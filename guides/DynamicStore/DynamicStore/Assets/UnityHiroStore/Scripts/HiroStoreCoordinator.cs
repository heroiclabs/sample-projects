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
        [SerializeField] private string satoriScheme = "";
        [SerializeField] private string satoriHost = "";
        [SerializeField] private int satoriPort = 443;
        [SerializeField] private string satoriApiKey = "";

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
            var satoriSystem = new SatoriSystem(logger, satoriClient, SatoriAuthorizerFunc(nakamaSystem));
            systems.Add(satoriSystem);

            return Task.FromResult(systems);
        }

        /// <summary>
        /// Authorizes the Satori session with the Nakama user ID as the Satori identity.
        /// The server-side SatoriPersonalizer looks players up in Satori by their Nakama user ID,
        /// so the client must authenticate as the same identity for audience targeting to apply.
        /// </summary>
        public static SatoriSystem.AuthorizerFunc SatoriAuthorizerFunc(NakamaSystem nakamaSystem)
        {
            return async client =>
            {
                var userId = nakamaSystem.Session.UserId;
                return await AuthenticateSatoriAsync(client, userId);
            };
        }

        /// <summary>
        /// Authenticates a fresh Satori session for the given user ID.
        /// </summary>
        public static async Task<Satori.ISession> AuthenticateSatoriAsync(Satori.IClient client, string userId)
        {
            var defaultProperties = new Dictionary<string, string>
            {
                { "platform", Application.platform.ToString() },
                { "language", Application.systemLanguage.ToString() }
            };

            return await client.AuthenticateAsync(userId, defaultProperties);
        }

        /// <summary>
        /// Re-authenticates the Satori session when the active Nakama account changes
        /// </summary>
        public async Task AlignSatoriIdentityAsync()
        {
            var nakamaSystem = this.GetSystem<NakamaSystem>();
            var satoriSystem = this.GetSystem<SatoriSystem>();
            if (nakamaSystem == null || satoriSystem?.Session == null) return;

            var userId = nakamaSystem.Session.UserId;
            if (satoriSystem.Session.IdentityId == userId) return;

            var newSession = await AuthenticateSatoriAsync(satoriSystem.Client, userId);
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