using System;
using System.Threading.Tasks;
using Nakama;
using Hiro;
using UnityEngine;
using Hiro.Unity;
using Hiro.System;

namespace HiroAchievements
{
    public class HiroAchievementsCoordinator : HiroCoordinator
    {
        [Header("Overrides Nakama Settings")] 
        [SerializeField] private bool localHost;

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

            var client = localHost
                ? new Client("http", "127.0.0.1", 7350, "defaultkey")
                : new Client(scheme, host, port, serverKey);

            var nakamaSystem = new NakamaSystem(logger, client, NakamaAuthorizerFunc());

            var storage = MemoryStorage.Default;

            // Register necessary Hiro Systems
            var systems = new Systems(nameof(HiroAchievementsCoordinator), monitor, storage, logger);
            systems.Add(nakamaSystem);
            var inventorySystem = new InventorySystem(logger, nakamaSystem);
            systems.Add(inventorySystem);
            var economySystem = new EconomySystem(logger, nakamaSystem, EconomyStoreType.Unspecified);
            systems.Add(economySystem);

            return Task.FromResult(systems);
        }

        public static NakamaSystem.AuthorizerFunc NakamaAuthorizerFunc(int index = 0)
        {
            const string playerPrefsAuthToken = "nakama.AuthToken";
            const string playerPrefsRefreshToken = "nakama.RefreshToken";
            const string playerPrefsDeviceId = "nakama.DeviceId";

            return async client =>
            {
                // Attempt to load a previous session if it is still valid.
                var authToken = PlayerPrefs.GetString($"{playerPrefsAuthToken}_{index}");
                var refreshToken = PlayerPrefs.GetString($"{playerPrefsRefreshToken}_{index}");
                var session = Session.Restore(authToken, refreshToken);
                Debug.Log("Session:" + session);

                // Add an hour, so we check whether the token is within an hour of expiration to refresh it.
                var expiredDate = DateTime.UtcNow.AddHours(1);
                if (session != null && !session.HasRefreshExpired(expiredDate))
                {
                    return session;
                }

                // Attempt to read the device ID to use for Authentication.
                var deviceId = PlayerPrefs.GetString(playerPrefsDeviceId, SystemInfo.deviceUniqueIdentifier);
                if (deviceId == SystemInfo.unsupportedIdentifier)
                {
                    deviceId = Guid.NewGuid().ToString();
                }

                // Due to the Account Switcher tool, we might need to logout before re-authenticating.
                if (session is { Created: true })
                {
                    await client.SessionLogoutAsync(session);
                }

                session = await client.AuthenticateDeviceAsync($"{deviceId}_{index}");

                // Store tokens to avoid needing to re-authenticate next time.
                PlayerPrefs.SetString(playerPrefsDeviceId, deviceId);
                PlayerPrefs.SetString($"{playerPrefsAuthToken}_{index}", session.AuthToken);
                PlayerPrefs.SetString($"{playerPrefsRefreshToken}_{index}", session.RefreshToken);

                if (session.Created)
                {
                    Debug.LogFormat("New user account '{0}' created.", session.UserId);
                }

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