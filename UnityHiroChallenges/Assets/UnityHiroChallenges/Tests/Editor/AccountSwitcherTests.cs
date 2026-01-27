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

using System.Collections.Generic;
using System.Threading.Tasks;
using Nakama;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace HiroChallenges.Tests.Editor
{
    /// <summary>
    /// EditMode tests for AccountSwitcher functionality.
    /// Requires: Nakama server running at 127.0.0.1:7350
    /// </summary>
    [TestFixture]
    public class AccountSwitcherTests
    {
        private const string Scheme = "http";
        private const string Host = "127.0.0.1";
        private const int Port = 7350;
        private const string ServerKey = "defaultkey";
        private const int RequiredAccountCount = 4;

        private const string PlayerPrefsDeviceId = "nakama.DeviceId";
        private const string AccountUsernamesKey = "AccountSwitcher_Usernames";

        private IClient _client;

        [SetUp]
        public void SetUp()
        {
            _client = new Client(Scheme, Host, Port, ServerKey);
        }

        [Test]
        public async Task AccountSwitcher_HasFourAccounts_AllExistOnServer()
        {
            var deviceId = PlayerPrefs.GetString(PlayerPrefsDeviceId, "");

            if (string.IsNullOrEmpty(deviceId))
            {
                deviceId = SystemInfo.deviceUniqueIdentifier;
                if (deviceId == SystemInfo.unsupportedIdentifier)
                {
                    deviceId = System.Guid.NewGuid().ToString();
                }
                PlayerPrefs.SetString(PlayerPrefsDeviceId, deviceId);
            }

            var sessions = new List<ISession>();
            var userIds = new List<string>();

            // Authenticate all 4 accounts (creates them if they don't exist)
            for (var i = 0; i < RequiredAccountCount; i++)
            {
                var session = await _client.AuthenticateDeviceAsync($"{deviceId}_{i}");
                sessions.Add(session);
                userIds.Add(session.UserId);
                Debug.Log($"Account {i}: UserId={session.UserId}, Username={session.Username}");
            }

            // Verify all accounts exist by fetching them
            var result = await _client.GetUsersAsync(sessions[0], userIds);

            var count = 0;
            foreach (var user in result.Users)
            {
                count++;
                Debug.Log($"Verified user: {user.Id}, Username: {user.Username}");
            }

            Assert.AreEqual(RequiredAccountCount, count, $"Expected {RequiredAccountCount} accounts to exist");

            // Save usernames to EditorPrefs for AccountSwitcher
            SaveAccountUsernames(sessions);
        }

        [Test]
        public async Task AccountSwitcher_StoredAccounts_MatchServerAccounts()
        {
            var savedUsernames = EditorPrefs.GetString(AccountUsernamesKey, "");

            if (string.IsNullOrEmpty(savedUsernames))
            {
                Assert.Inconclusive("No stored accounts found. Run AccountSwitcher_HasFourAccounts_AllExistOnServer first.");
                return;
            }

            var usernameData = JsonUtility.FromJson<SerializableStringDictionary>(savedUsernames);

            Assert.IsNotNull(usernameData);
            Assert.IsNotNull(usernameData.items);
            Assert.AreEqual(RequiredAccountCount, usernameData.items.Count,
                $"Expected {RequiredAccountCount} stored accounts");

            // Verify each stored username exists on server
            var usernames = new List<string>();
            foreach (var item in usernameData.items)
            {
                usernames.Add(item.value);
            }

            var deviceId = PlayerPrefs.GetString(PlayerPrefsDeviceId, "");
            var session = await _client.AuthenticateDeviceAsync($"{deviceId}_0");

            var result = await _client.GetUsersAsync(session, usernames: usernames, ids: null);

            var count = 0;
            foreach (var user in result.Users)
            {
                count++;
                Debug.Log($"Verified stored user: {user.Username}");
            }

            Assert.AreEqual(RequiredAccountCount, count,
                $"Expected all {RequiredAccountCount} stored usernames to exist on server");
        }

        [Test]
        public async Task AccountSwitcher_SwitchBetweenAccounts_SessionsAreDistinct()
        {
            var deviceId = PlayerPrefs.GetString(PlayerPrefsDeviceId, "");

            if (string.IsNullOrEmpty(deviceId))
            {
                Assert.Inconclusive("No device ID found. Run AccountSwitcher_HasFourAccounts_AllExistOnServer first.");
                return;
            }

            var userIds = new HashSet<string>();

            for (var i = 0; i < RequiredAccountCount; i++)
            {
                var session = await _client.AuthenticateDeviceAsync($"{deviceId}_{i}");

                Assert.IsFalse(userIds.Contains(session.UserId),
                    $"Account {i} has duplicate UserId: {session.UserId}");

                userIds.Add(session.UserId);
                Debug.Log($"Account {i}: Unique UserId={session.UserId}");
            }

            Assert.AreEqual(RequiredAccountCount, userIds.Count,
                $"Expected {RequiredAccountCount} distinct user IDs");
        }

        private void SaveAccountUsernames(List<ISession> sessions)
        {
            var usernameData = new SerializableStringDictionary();

            for (var i = 0; i < sessions.Count; i++)
            {
                usernameData.items.Add(new SerializableKeyValuePair
                {
                    key = $"ACCOUNT {i + 1}",
                    value = sessions[i].Username
                });
            }

            var json = JsonUtility.ToJson(usernameData);
            EditorPrefs.SetString(AccountUsernamesKey, json);
            Debug.Log($"Saved {sessions.Count} account usernames to EditorPrefs");
        }

        [System.Serializable]
        private class SerializableStringDictionary
        {
            public List<SerializableKeyValuePair> items = new();
        }

        [System.Serializable]
        private class SerializableKeyValuePair
        {
            public string key;
            public string value;
        }
    }
}
