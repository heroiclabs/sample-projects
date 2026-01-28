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
using Nakama;
using NUnit.Framework;
using UnityEngine;

namespace HiroChallenges.Tests.Editor
{
    /// <summary>
    /// Editor tests for Nakama account operations.
    /// Requires: Nakama server running at 127.0.0.1:7350
    /// </summary>
    [TestFixture]
    public class NakamaAccountTests
    {
        private const string Scheme = "http";
        private const string Host = "127.0.0.1";
        private const int Port = 7350;
        private const string ServerKey = "defaultkey";

        private IClient _client;
        private string _testDeviceId;

        [SetUp]
        public void SetUp()
        {
            _client = new Client(Scheme, Host, Port, ServerKey);
            _testDeviceId = $"test-device-{Guid.NewGuid():N}";
        }

        [Test]
        public async Task Authenticate_WithDeviceId_ReturnsSession()
        {
            var session = await _client.AuthenticateDeviceAsync($"{_testDeviceId}_0");

            Assert.IsNotNull(session);
            Assert.IsFalse(string.IsNullOrEmpty(session.UserId));
            Assert.IsFalse(string.IsNullOrEmpty(session.AuthToken));

            Debug.Log($"Authenticated user: {session.UserId}, Username: {session.Username}");
        }

        [Test]
        public async Task Authenticate_WithDifferentIndices_CreatesDifferentUsers()
        {
            var session0 = await _client.AuthenticateDeviceAsync($"{_testDeviceId}_0");
            var session1 = await _client.AuthenticateDeviceAsync($"{_testDeviceId}_1");
            var session2 = await _client.AuthenticateDeviceAsync($"{_testDeviceId}_2");

            Assert.AreNotEqual(session0.UserId, session1.UserId, "Account 0 and 1 should be different users");
            Assert.AreNotEqual(session1.UserId, session2.UserId, "Account 1 and 2 should be different users");
            Assert.AreNotEqual(session0.UserId, session2.UserId, "Account 0 and 2 should be different users");

            Debug.Log($"Account 0: {session0.UserId}");
            Debug.Log($"Account 1: {session1.UserId}");
            Debug.Log($"Account 2: {session2.UserId}");
        }

        [Test]
        public async Task Authenticate_SameDeviceId_ReturnsSameUser()
        {
            var deviceId = $"{_testDeviceId}_0";

            var session1 = await _client.AuthenticateDeviceAsync(deviceId);
            var session2 = await _client.AuthenticateDeviceAsync(deviceId);

            Assert.AreEqual(session1.UserId, session2.UserId, "Same device ID should return same user");
        }

        [Test]
        public async Task GetAccount_ReturnsUserInfo()
        {
            var session = await _client.AuthenticateDeviceAsync($"{_testDeviceId}_0");

            var account = await _client.GetAccountAsync(session);

            Assert.IsNotNull(account);
            Assert.IsNotNull(account.User);
            Assert.AreEqual(session.UserId, account.User.Id);

            Debug.Log($"Account ID: {account.User.Id}");
            Debug.Log($"Username: {account.User.Username}");
            Debug.Log($"Display Name: {account.User.DisplayName}");
            Debug.Log($"Created: {account.User.CreateTime}");
        }

        [Test]
        public async Task GetUsers_WithMultipleAccounts_ReturnsAllUsers()
        {
            var sessions = new List<ISession>();
            var userIds = new List<string>();

            for (var i = 0; i < 3; i++)
            {
                var session = await _client.AuthenticateDeviceAsync($"{_testDeviceId}_{i}");
                sessions.Add(session);
                userIds.Add(session.UserId);
            }

            var result = await _client.GetUsersAsync(sessions[0], userIds);

            Assert.IsNotNull(result);

            var count = 0;
            foreach (var user in result.Users)
            {
                count++;
                Debug.Log($"User: {user.Id}, Username: {user.Username}");
            }

            Assert.AreEqual(3, count, "Should return all 3 users");
        }

        [Test]
        public async Task SwitchAccount_SimulatesAccountSwitcher()
        {
            // Simulate what AccountSwitcher does: authenticate with different indices
            var accountSessions = new Dictionary<int, ISession>();

            // Create accounts 0, 1, 2
            for (var i = 0; i < 3; i++)
            {
                accountSessions[i] = await _client.AuthenticateDeviceAsync($"{_testDeviceId}_{i}");
                Debug.Log($"Account {i}: UserId={accountSessions[i].UserId}, Username={accountSessions[i].Username}");
            }

            // Verify switching back to account 0 returns the same user
            var switchBackSession = await _client.AuthenticateDeviceAsync($"{_testDeviceId}_0");
            Assert.AreEqual(accountSessions[0].UserId, switchBackSession.UserId,
                "Switching back to account 0 should return same user");

            Debug.Log("Account switching simulation successful");
        }

        [Test]
        public async Task SessionRefresh_WithValidSession_ReturnsValidSession()
        {
            var session = await _client.AuthenticateDeviceAsync($"{_testDeviceId}_0");

            var refreshedSession = await _client.SessionRefreshAsync(session);

            Assert.IsNotNull(refreshedSession);
            Assert.AreEqual(session.UserId, refreshedSession.UserId, "UserId should match after refresh");
            Assert.IsFalse(string.IsNullOrEmpty(refreshedSession.AuthToken), "Should have valid auth token");
            Assert.IsFalse(string.IsNullOrEmpty(refreshedSession.RefreshToken), "Should have valid refresh token");

            Debug.Log($"UserId: {refreshedSession.UserId}");
            Debug.Log($"Session valid: AuthToken present={!string.IsNullOrEmpty(refreshedSession.AuthToken)}");
        }
    }
}
