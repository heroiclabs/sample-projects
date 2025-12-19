// Copyright 2025 The Nakama Authors
//
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//
// http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.

using System;
using System.Threading.Tasks;
using Nakama;
using Hiro;
using UnityEngine;
using Hiro.Unity;
using Hiro.System;

namespace HiroEventLeaderboards
{
    public sealed class HiroEventLeaderboardsCoordinator : HiroCoordinator
    {
        [Header("Overrides Nakama Settings")] [SerializeField]
        private bool localHost;

        [Header("Nakama Settings")] [SerializeField]
        private string scheme = "http";
        [SerializeField]
        private string host = "127.0.0.1";
        [SerializeField]
        private int port = 7350;
        [SerializeField]
        private string serverKey = "defaultkey";

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
            var systems = new Systems(nameof(HiroEventLeaderboardsCoordinator), monitor, storage, logger);
            systems.Add(nakamaSystem);
            var eventLeaderboardsSystem = new EventLeaderboardsSystem(logger, nakamaSystem);
            systems.Add(eventLeaderboardsSystem);
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
                // Due to the Account Switcher tool, we might need to log out before re-authenticating.
                var nakamaSystem = Instance.GetSystem<NakamaSystem>();
                if (nakamaSystem.Session != null)
                {
                    await client.SessionLogoutAsync(nakamaSystem.Session);
                }

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
            var nakamaSystem = Instance.GetSystem<NakamaSystem>();
            ReceivedStartSuccess?.Invoke(nakamaSystem.Session);
        }

        protected override void SystemsInitializeFailed(Exception e)
        {
            ReceivedStartError?.Invoke(e);
        }
    }
}
