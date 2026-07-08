using System;
using System.Threading.Tasks;
using Nakama;
using Hiro;
using UnityEngine;
using Hiro.Unity;
using Hiro.System;

namespace UnityGacha
{
    public class UnityGachaCoordinator : HiroCoordinator
    {
        [Header("Nakama Settings")]
        [SerializeField] private string scheme = "http";
        [SerializeField] private string host = "127.0.0.1";
        [SerializeField] private int port = 7350;
        [SerializeField] private string serverKey = "defaultkey";

        public event Action<Exception> ReceivedStartError;
        public event Action<ISession> ReceivedStartSuccess;

        protected override Task<Systems> CreateSystemsAsync()
        {
            var logger = new Hiro.Unity.Logger();
            var monitor = NetworkMonitor.Default;

            var client = new Client(scheme, host, port, serverKey);

            var nakamaSystem = new NakamaSystem(logger, client, NakamaAuthorizerFunc());

            var storage = MemoryStorage.Default;

            // Register necessary Hiro Systems
            var systems = new Systems(nameof(UnityGachaCoordinator), monitor, storage, logger);
            systems.Add(nakamaSystem);
            var inventorySystem = new InventorySystem(logger, nakamaSystem);
            systems.Add(inventorySystem);
            var economySystem = new EconomySystem(logger, nakamaSystem, EconomyStoreType.Unspecified);
            systems.Add(economySystem);
            var statsSystem = new StatsSystem(logger, nakamaSystem);
            systems.Add(statsSystem);

            return Task.FromResult(systems);
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