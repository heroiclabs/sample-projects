using System;
using System.Threading.Tasks;
using Nakama;
using Hiro;
using UnityEngine;
using Hiro.Unity;
using Hiro.System;

namespace XPGuide
{
    public class XPGuideCoordinator : HiroCoordinator
    {
        [Header("Overrides Nakama Settings")]
        [SerializeField] private bool localHost;

        [Header("Nakama Settings")]
        [SerializeField] private string scheme = "https";
        [SerializeField] private string host = "sample-prjcts.eu-west1-a.nakamacloud.io";
        [SerializeField] private int port = 443;
        [SerializeField] private string serverKey = "uNezOE3FOprj6nPs";

        public bool IsLocalHost => localHost;
        public IClient NakamaClient { get; private set; }

        public event Action<Exception> ReceivedStartError;
        public event Action<ISession> ReceivedStartSuccess;

        protected override Task<Systems> CreateSystemsAsync()
        {
            var logger = new Hiro.Unity.Logger();
            var monitor = NetworkMonitor.Default;

            NakamaClient = localHost
                ? new Client("http", "127.0.0.1", 7350, "defaultkey")
                : new Client(scheme, host, port, serverKey);

            var nakamaSystem = new NakamaSystem(logger, NakamaClient, NakamaAuthorizerFunc());

            var storage = MemoryStorage.Default;

            var systems = new Systems(nameof(XPGuideCoordinator), monitor, storage, logger);
            systems.Add(nakamaSystem);
            var economySystem = new EconomySystem(logger, nakamaSystem, EconomyStoreType.Unspecified);
            systems.Add(economySystem);
            var achievementsSystem = new AchievementsSystem(logger, nakamaSystem);
            systems.Add(achievementsSystem);

            return Task.FromResult(systems);
        }

        public static NakamaSystem.AuthorizerFunc NakamaAuthorizerFunc(int index = 0)
        {
            const string playerPrefsAuthToken = "nakama.AuthToken";
            const string playerPrefsRefreshToken = "nakama.RefreshToken";
            const string playerPrefsDeviceId = "nakama.DeviceId";

            return async client =>
            {
                var authToken = PlayerPrefs.GetString($"{playerPrefsAuthToken}_{index}");
                var refreshToken = PlayerPrefs.GetString($"{playerPrefsRefreshToken}_{index}");
                var session = Session.Restore(authToken, refreshToken);

                var expiredDate = DateTime.UtcNow.AddHours(1);
                if (session != null && !session.HasRefreshExpired(expiredDate))
                {
                    try
                    {
                        await client.GetAccountAsync(session);
                        return session;
                    }
                    catch (ApiResponseException ex) when (ex.Message.Contains("User account not found"))
                    {
                        session = null;
                    }
                    catch (ApiResponseException ex) when (ex.Message.Contains("Session has expired"))
                    {
                        session = null;
                    }
                    catch (Exception ex)
                    {
                        Debug.LogWarning($"Session validation failed: {ex.Message}. Creating new session.");
                        session = null;
                    }
                }

                if (session == null)
                {
                    var deviceId = PlayerPrefs.GetString(playerPrefsDeviceId, SystemInfo.deviceUniqueIdentifier);
                    if (deviceId == SystemInfo.unsupportedIdentifier)
                    {
                        deviceId = Guid.NewGuid().ToString();
                    }

                    if (session is { Created: true })
                    {
                        await client.SessionLogoutAsync(session);
                    }

                    session = await client.AuthenticateDeviceAsync($"{deviceId}_{index}");

                    PlayerPrefs.SetString(playerPrefsDeviceId, deviceId);
                    PlayerPrefs.SetString($"{playerPrefsAuthToken}_{index}", session.AuthToken);
                    PlayerPrefs.SetString($"{playerPrefsRefreshToken}_{index}", session.RefreshToken);

                    if (session.Created)
                    {
                        Debug.LogFormat("New user account '{0}' created.", session.UserId);
                    }
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
