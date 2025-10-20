using System;
using System.Threading.Tasks;
using Nakama;
using Hiro;
using UnityEngine;
using Hiro.Unity;
using Hiro.System;

namespace HiroChallenges
{
    public class HiroChallengesCoordinator : HiroCoordinator
    {
        [Header("Nakama Settings")] [SerializeField]
        private string scheme = "http";
        [SerializeField]
        private string host = "127.0.0.1";
        [SerializeField]
        private int port = 7350;
        [SerializeField]
        private string serverKey = "defaultkey";
        [SerializeField]
        private bool local = false;
        

        public event Action<Exception> ReceivedStartError;
        public event Action<ISession> ReceivedStartSuccess;

        protected override Task<Systems> CreateSystemsAsync()
        {
            var logger = new Hiro.Unity.Logger();
            var monitor = NetworkMonitor.Default;

            var client = local ? new Client("http", "127.0.0.1", 7350, "defaultkey") : new Client(scheme, host, port, serverKey);

            var nakamaSystem = new NakamaSystem(logger, client, NakamaAuthorizerFunc());

            var storage = MemoryStorage.Default;
            var systems = new Systems(nameof(HiroChallengesCoordinator), monitor, storage, logger);
            systems.Add(nakamaSystem);
            var challengesSystem = new ChallengesSystem(logger, nakamaSystem);
            systems.Add(challengesSystem);

            return Task.FromResult(systems);
        }

        public NakamaSystem.AuthorizerFunc NakamaAuthorizerFunc(int index = 0)
        {
            const string playerPrefsAuthToken = "nakama.AuthToken";
            const string playerPrefsRefreshToken = "nakama.RefreshToken";
            const string playerPrefsDeviceId = "nakama.DeviceId";

            return async client =>
            {
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