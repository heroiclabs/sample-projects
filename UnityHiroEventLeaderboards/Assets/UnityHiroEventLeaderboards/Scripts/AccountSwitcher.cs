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
using System.Collections.Generic;
using System.Threading.Tasks;
using Hiro.Unity;
using Hiro;
using Nakama;
using UnityEngine;

namespace HiroEventLeaderboards
{
    public static class AccountSwitcher
    {
        private const string AccountDataKey = "AccountSwitcher_Data";

        [Serializable]
        public struct AccountInfo
        {
            public string Username;
            public string UserId;
        }

        [Serializable]
        private class AccountDataStorage
        {
            public List<AccountDataEntry> entries = new();
        }

        [Serializable]
        private class AccountDataEntry
        {
            public string key;
            public string username;
            public string userId;
        }

        private static Dictionary<string, AccountInfo> _accountCache;

        private static NakamaSystem _nakamaSystem;
        private static string _currentEnv;
        private static int _currentIndex;

        private const string PlayerPrefsAuthToken = "nakama.AuthToken";
        private const string PlayerPrefsRefreshToken = "nakama.RefreshToken";
        private const string PlayerPrefsDeviceId = "nakama.DeviceId";

        public static event Action AccountSwitched;

        public static void Initialize(NakamaSystem nakamaSystem, string env, int index = 0)
        {
            if (_nakamaSystem != null)
            {
                _nakamaSystem.Client.ReceivedSessionUpdated -= OnSessionUpdated;
            }

            _nakamaSystem = nakamaSystem;
            _currentEnv = env;
            _currentIndex = index;

            _nakamaSystem.Client.ReceivedSessionUpdated += OnSessionUpdated;
        }

        private static void OnSessionUpdated(ISession session)
        {
            var keySuffix = $"{_currentEnv}_{_currentIndex}";
            PlayerPrefs.SetString($"{PlayerPrefsAuthToken}_{keySuffix}", session.AuthToken);
            PlayerPrefs.SetString($"{PlayerPrefsRefreshToken}_{keySuffix}", session.RefreshToken);
            PlayerPrefs.Save();
        }

        public static async Task<ISession> SwitchAccountAsync(
            NakamaSystem nakamaSystem,
            EventLeaderboardsController controller,
            string env,
            int accountIndex)
        {
            var newSession = await AuthenticateAndStoreAccountAsync(nakamaSystem, controller, env, accountIndex);
            _currentEnv = env;
            _currentIndex = accountIndex;
            AccountSwitched?.Invoke();
            return newSession;
        }

        public static async Task SwitchToSessionAsync(
            NakamaSystem nakamaSystem,
            EventLeaderboardsController controller,
            ISession newSession)
        {
            await ApplySessionAsync(nakamaSystem, controller, newSession);
            AccountSwitched?.Invoke();
        }

        private static async Task<ISession> AuthenticateAndStoreAccountAsync(
            NakamaSystem nakamaSystem,
            EventLeaderboardsController controller,
            string env,
            int accountIndex)
        {
            var newSession = await HiroEventLeaderboardsCoordinator.NakamaAuthorizerFunc(env, accountIndex)
                .Invoke(nakamaSystem.Client);
            await ApplySessionAsync(nakamaSystem, controller, newSession);

            var key = $"{env}_{accountIndex}";
            StoreAccountInfo(key, new AccountInfo
            {
                Username = newSession.Username,
                UserId = newSession.UserId
            });

            return newSession;
        }

        private static async Task ApplySessionAsync(
            NakamaSystem nakamaSystem,
            EventLeaderboardsController controller,
            ISession newSession)
        {
            if (nakamaSystem.Session is not Session session)
                throw new InvalidOperationException(
                    $"Cannot switch account: NakamaSystem.Session is {nakamaSystem.Session?.GetType().Name ?? "null"}, expected Session");

            session.Update(newSession.AuthToken, newSession.RefreshToken);
            await nakamaSystem.RefreshAsync();
            await controller.SwitchCompleteAsync();
        }

        public static void StoreAccountInfo(string key, AccountInfo info)
        {
            LoadCache();
            _accountCache[key] = info;
            SaveCache();
        }

        public static IReadOnlyDictionary<string, AccountInfo> GetAllAccounts()
        {
            LoadCache();
            return _accountCache;
        }

        public static string GetUserIdByUsername(string username)
        {
            LoadCache();
            foreach (var kvp in _accountCache)
            {
                if (string.Equals(kvp.Value.Username, username, StringComparison.OrdinalIgnoreCase))
                    return kvp.Value.UserId;
            }
            return null;
        }

        public static IEnumerable<string> GetKnownUsernames(string excludeUserId = null)
        {
            LoadCache();
            var usernames = new List<string>();
            foreach (var kvp in _accountCache)
            {
                if (excludeUserId != null && kvp.Value.UserId == excludeUserId)
                    continue;
                if (!string.IsNullOrEmpty(kvp.Value.Username))
                    usernames.Add(kvp.Value.Username);
            }
            return usernames;
        }

        public static string[] ParseUsernamesToIds(string usernamesInput)
        {
            if (string.IsNullOrWhiteSpace(usernamesInput))
                return Array.Empty<string>();

            var usernames = usernamesInput.Split(',');
            var userIds = new List<string>();

            foreach (var username in usernames)
            {
                var trimmed = username.Trim();
                if (string.IsNullOrEmpty(trimmed))
                    continue;

                var userId = GetUserIdByUsername(trimmed);
                if (string.IsNullOrEmpty(userId))
                    throw new ArgumentException($"Unknown user: {trimmed}");

                userIds.Add(userId);
            }

            return userIds.ToArray();
        }

        public static async Task<ISession> AuthenticateDeviceAsync(IClient client, string env, int index)
        {
            var deviceId = GetOrCreateDeviceId(env);
            return await client.AuthenticateDeviceAsync($"{deviceId}_{index}");
        }

        public static string GetOrCreateDeviceId(string env)
        {
            var key = $"{PlayerPrefsDeviceId}_{env}";
            var deviceId = PlayerPrefs.GetString(key, "");

            if (string.IsNullOrEmpty(deviceId))
            {
                deviceId = SystemInfo.deviceUniqueIdentifier;
                if (deviceId == SystemInfo.unsupportedIdentifier)
                    deviceId = Guid.NewGuid().ToString();
                PlayerPrefs.SetString(key, deviceId);
                PlayerPrefs.Save();
            }

            return deviceId;
        }

        public static void ClearAccounts()
        {
            _accountCache = new Dictionary<string, AccountInfo>();
            PlayerPrefs.DeleteKey(AccountDataKey);

            // Also clear auth tokens for all environments and indices
            foreach (var env in new[] { "local", "heroiclabs" })
            {
                PlayerPrefs.DeleteKey($"nakama.DeviceId_{env}");
                for (var i = 0; i < 4; i++)
                {
                    var keySuffix = $"{env}_{i}";
                    PlayerPrefs.DeleteKey($"{PlayerPrefsAuthToken}_{keySuffix}");
                    PlayerPrefs.DeleteKey($"{PlayerPrefsRefreshToken}_{keySuffix}");
                }
            }

            PlayerPrefs.Save();
        }

        public static async Task EnsureAccountsExistAsync(
            NakamaSystem nakamaSystem,
            EventLeaderboardsController controller,
            string env,
            int count = 4)
        {
            // Unsubscribe during bulk account creation - NakamaAuthorizerFunc stores tokens correctly
            if (_nakamaSystem != null)
                _nakamaSystem.Client.ReceivedSessionUpdated -= OnSessionUpdated;

            try
            {
                var accountsCreated = false;
                for (var i = 0; i < count; i++)
                {
                    var key = $"{env}_{i}";
                    LoadCache();
                    if (!_accountCache.ContainsKey(key))
                    {
                        _currentEnv = env;
                        _currentIndex = i;
                        await AuthenticateAndStoreAccountAsync(nakamaSystem, controller, env, i);
                        accountsCreated = true;
                    }
                }

                if (accountsCreated)
                    AccountSwitched?.Invoke();
            }
            finally
            {
                if (_nakamaSystem != null)
                    _nakamaSystem.Client.ReceivedSessionUpdated += OnSessionUpdated;
            }
        }

        private static void LoadCache()
        {
            if (_accountCache != null)
                return;

            _accountCache = new Dictionary<string, AccountInfo>();
            var json = PlayerPrefs.GetString(AccountDataKey, "");
            if (string.IsNullOrEmpty(json))
                return;

            try
            {
                var data = JsonUtility.FromJson<AccountDataStorage>(json);
                foreach (var entry in data.entries)
                {
                    _accountCache[entry.key] = new AccountInfo
                    {
                        Username = entry.username,
                        UserId = entry.userId
                    };
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"Failed to load account data: {ex.Message}");
            }
        }

        private static void SaveCache()
        {
            var data = new AccountDataStorage();
            foreach (var kvp in _accountCache)
            {
                data.entries.Add(new AccountDataEntry
                {
                    key = kvp.Key,
                    username = kvp.Value.Username,
                    userId = kvp.Value.UserId
                });
            }
            var json = JsonUtility.ToJson(data);
            PlayerPrefs.SetString(AccountDataKey, json);
            PlayerPrefs.Save();
        }
    }
}
