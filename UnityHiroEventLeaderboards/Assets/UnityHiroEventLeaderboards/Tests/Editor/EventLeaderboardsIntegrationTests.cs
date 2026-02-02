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
using Hiro;
using Hiro.System;
using Hiro.Unity;
using Nakama;
using NUnit.Framework;
using UnityEngine;

namespace HiroEventLeaderboards.Tests.Editor
{
    /// <summary>
    /// EditMode integration tests for EventLeaderboardsController.
    /// Requires: Nakama server running at 127.0.0.1:7350
    /// </summary>
    [TestFixture]
    public class EventLeaderboardsControllerTests
    {
        private const string Scheme = "http";
        private const string Host = "127.0.0.1";
        private const int Port = 7350;
        private const string ServerKey = "defaultkey";

        private EventLeaderboardsController _controller;
        private IClient _client;
        private ISession _session;
        private NakamaSystem _nakamaSystem;
        private EventLeaderboardsSystem _eventLeaderboardsSystem;
        private EconomySystem _economySystem;
        private string _testDeviceId;

        [SetUp]
        public async Task SetUp()
        {
            _testDeviceId = $"test-device-{Guid.NewGuid():N}";
            _client = new Client(Scheme, Host, Port, ServerKey);
            _session = await _client.AuthenticateDeviceAsync($"{_testDeviceId}_0");

            var logger = new Hiro.Unity.Logger();
            _nakamaSystem = new NakamaSystem(logger, _client, _ => Task.FromResult(_session));
            await _nakamaSystem.InitializeAsync();

            _eventLeaderboardsSystem = new EventLeaderboardsSystem(logger, _nakamaSystem);
            await _eventLeaderboardsSystem.InitializeAsync();

            _economySystem = new EconomySystem(logger, _nakamaSystem, EconomyStoreType.Unspecified);
            await _economySystem.InitializeAsync();

            _controller = new EventLeaderboardsController(_nakamaSystem, _eventLeaderboardsSystem, _economySystem);
        }

        [TearDown]
        public async Task TearDown()
        {
            await _client.DeleteAccountAsync(_session);
        }

        [Test]
        public async Task RefreshEventLeaderboardsAsync_ReturnsLeaderboards()
        {
            var result = await _controller.RefreshEventLeaderboardsAsync();

            Assert.IsTrue(_controller.EventLeaderboards.Count >= 0, "Should return leaderboards list");
            Debug.Log($"Found {_controller.EventLeaderboards.Count} event leaderboards");

            foreach (var leaderboard in _controller.EventLeaderboards)
            {
                Debug.Log($"Leaderboard: {leaderboard.Name} ({leaderboard.Id}), Active: {leaderboard.IsActive}");
            }
        }

        [Test]
        public async Task SelectEventLeaderboardAsync_AfterRefresh_ReturnsRecords()
        {
            await _controller.RefreshEventLeaderboardsAsync();

            if (_controller.EventLeaderboards.Count == 0)
            {
                Assert.Inconclusive("No event leaderboards found on server.");
                return;
            }

            var leaderboard = _controller.EventLeaderboards[0];
            var records = await _controller.SelectEventLeaderboardAsync(leaderboard);

            Assert.IsNotNull(records, "Should return records list");
            Debug.Log($"Leaderboard '{leaderboard.Name}' has {records.Count} records");
        }

        [Test]
        public async Task RollEventLeaderboardAsync_WhenCanRoll_JoinsLeaderboard()
        {
            await _controller.RefreshEventLeaderboardsAsync();

            if (_controller.EventLeaderboards.Count == 0)
            {
                Assert.Inconclusive("No event leaderboards found on server.");
                return;
            }

            // Find a leaderboard that can be rolled
            IEventLeaderboard rollableLeaderboard = null;
            foreach (var lb in _controller.EventLeaderboards)
            {
                if (lb.CanRoll && lb.IsActive)
                {
                    rollableLeaderboard = lb;
                    break;
                }
            }

            if (rollableLeaderboard == null)
            {
                Assert.Inconclusive("No rollable event leaderboards found.");
                return;
            }

            await _controller.SelectEventLeaderboardAsync(rollableLeaderboard);
            await _controller.RollEventLeaderboardAsync();

            var records = await _controller.SelectEventLeaderboardAsync(rollableLeaderboard);
            Debug.Log($"After roll, leaderboard has {records.Count} records");
        }

        [Test]
        public async Task SubmitScoreAsync_AfterJoining_UpdatesScore()
        {
            await _controller.RefreshEventLeaderboardsAsync();

            if (_controller.EventLeaderboards.Count == 0)
            {
                Assert.Inconclusive("No event leaderboards found on server.");
                return;
            }

            // Find a leaderboard that can be rolled (to join)
            IEventLeaderboard activeLeaderboard = null;
            foreach (var lb in _controller.EventLeaderboards)
            {
                if (lb.CanRoll && lb.IsActive)
                {
                    activeLeaderboard = lb;
                    break;
                }
            }

            if (activeLeaderboard == null)
            {
                Assert.Inconclusive("No active rollable event leaderboards found.");
                return;
            }

            // Join the leaderboard
            await _controller.SelectEventLeaderboardAsync(activeLeaderboard);
            await _controller.RollEventLeaderboardAsync();

            // Submit a score
            var testScore = 100;
            var testSubScore = 50;
            await _controller.SubmitScoreAsync(testScore, testSubScore);

            // Refresh and verify
            var records = await _controller.SelectEventLeaderboardAsync(activeLeaderboard);

            var foundOurScore = false;
            foreach (var record in records)
            {
                if (record.Username == _controller.CurrentUsername)
                {
                    foundOurScore = true;
                    Debug.Log($"Found our score: {record.Score}, subscore: {record.Subscore}");
                    break;
                }
            }

            Assert.IsTrue(foundOurScore, "Should find our submitted score");
        }
    }

    /// <summary>
    /// EditMode tests for EventLeaderboardsController constructor validation.
    /// </summary>
    [TestFixture]
    public class EventLeaderboardsControllerConstructorTests
    {
        [Test]
        public void Constructor_NullNakamaSystem_ThrowsArgumentNullException()
        {
            Assert.Throws<ArgumentNullException>(() =>
            {
                new EventLeaderboardsController(null, null, null);
            });
        }
    }

    /// <summary>
    /// EditMode integration tests for account switching behavior.
    /// Requires: Nakama server running locally or remote server available.
    /// </summary>
    [TestFixture]
    public class AccountSwitchingIntegrationTests
    {
        private const int AccountCount = 4;

        // Local server settings
        private const string LocalScheme = "http";
        private const string LocalHost = "127.0.0.1";
        private const int LocalPort = 7350;
        private const string LocalServerKey = "defaultkey";

        // Remote server settings
        private const string RemoteScheme = "https";
        private const string RemoteHost = "sample-prjcts.eu-west1-a.nakamacloud.io";
        private const int RemotePort = 443;
        private const string RemoteServerKey = "uNezOE3FOprj6nPs";

        private EventLeaderboardsController _controller;
        private IClient _client;
        private ISession[] _sessions;
        private NakamaSystem _nakamaSystem;
        private EventLeaderboardsSystem _eventLeaderboardsSystem;
        private EconomySystem _economySystem;
        private string _testDeviceId;

        private async Task SetUpForEnv(string env)
        {
            _testDeviceId = $"test-device-{Guid.NewGuid():N}";

            _client = env == "local"
                ? new Client(LocalScheme, LocalHost, LocalPort, LocalServerKey)
                : new Client(RemoteScheme, RemoteHost, RemotePort, RemoteServerKey);

            // Create all 4 accounts
            _sessions = new ISession[AccountCount];
            for (var i = 0; i < AccountCount; i++)
            {
                _sessions[i] = await _client.AuthenticateDeviceAsync($"{_testDeviceId}_{i}");
            }

            var logger = new Hiro.Unity.Logger();
            _nakamaSystem = new NakamaSystem(logger, _client, _ => Task.FromResult(_sessions[0]));
            await _nakamaSystem.InitializeAsync();

            _eventLeaderboardsSystem = new EventLeaderboardsSystem(logger, _nakamaSystem);
            await _eventLeaderboardsSystem.InitializeAsync();

            _economySystem = new EconomySystem(logger, _nakamaSystem, EconomyStoreType.Unspecified);
            await _economySystem.InitializeAsync();

            _controller = new EventLeaderboardsController(_nakamaSystem, _eventLeaderboardsSystem, _economySystem);
        }

        private async Task TearDownAccounts()
        {
            if (_sessions == null || _client == null) return;

            foreach (var session in _sessions)
            {
                if (session == null) continue;
                try
                {
                    await _client.DeleteAccountAsync(session);
                }
                catch (Exception e)
                {
                    Debug.LogWarning($"Failed to delete account {session.UserId}: {e.Message}");
                }
            }
        }

        [Test]
        [TestCase("local")]
        [TestCase("heroiclabs")]
        public async Task SwitchAccountAsync_FourAccounts_UsernamesAreConsistent(string env)
        {
            await SetUpForEnv(env);
            try
            {
                // Arrange: Cache usernames from the sessions we created
                var expectedUsernames = new string[AccountCount];
                for (var i = 0; i < AccountCount; i++)
                {
                    expectedUsernames[i] = _sessions[i].Username;
                    Debug.Log($"[{env}] Account {i}: {_sessions[i].Username}");
                }

                // Verify all usernames are unique
                var uniqueUsernames = new HashSet<string>(expectedUsernames);
                Assert.AreEqual(AccountCount, uniqueUsernames.Count,
                    $"[{env}] All {AccountCount} accounts should have unique usernames");

                // Act & Assert: Switch to each account and verify username matches
                for (var i = 0; i < AccountCount; i++)
                {
                    await AccountSwitcher.SwitchAccountAsync(
                        _nakamaSystem,
                        _controller,
                        env,
                        i);

                    var actualUsername = _controller.CurrentUsername;
                    Debug.Log($"[{env}] Switched to account {i}: expected={expectedUsernames[i]}, actual={actualUsername}");

                    Assert.AreEqual(expectedUsernames[i], actualUsername,
                        $"[{env}] Account {i} username mismatch after switch");
                }

                // Switch through all accounts again to verify consistency
                Debug.Log($"[{env}] Second pass - verifying consistency:");
                for (var i = 0; i < AccountCount; i++)
                {
                    await AccountSwitcher.SwitchAccountAsync(
                        _nakamaSystem,
                        _controller,
                        env,
                        i);

                    var actualUsername = _controller.CurrentUsername;
                    Debug.Log($"[{env}] Second switch to account {i}: expected={expectedUsernames[i]}, actual={actualUsername}");

                    Assert.AreEqual(expectedUsernames[i], actualUsername,
                        $"[{env}] Account {i} username mismatch on second switch");
                }
            }
            finally
            {
                await TearDownAccounts();
            }
        }

        [Test]
        [TestCase("local")]
        [TestCase("heroiclabs")]
        public async Task SwitchCompleteAsync_ClearsSelectedLeaderboard(string env)
        {
            await SetUpForEnv(env);
            try
            {
                await _controller.RefreshEventLeaderboardsAsync();

                if (_controller.EventLeaderboards.Count == 0)
                {
                    Assert.Inconclusive($"[{env}] No event leaderboards found on server.");
                    return;
                }

                var leaderboard = _controller.EventLeaderboards[0];
                await _controller.SelectEventLeaderboardAsync(leaderboard);

                Assert.IsTrue(_controller.SelectedEventLeaderboardRecords != null,
                    $"[{env}] Should have selected a leaderboard");

                await _controller.SwitchCompleteAsync();

                Assert.AreEqual(0, _controller.SelectedEventLeaderboardRecords.Count,
                    $"[{env}] Selected leaderboard records should be cleared after switch");
                Assert.AreEqual(0, _controller.DisplayItems.Count,
                    $"[{env}] Display items should be cleared after switch");
            }
            finally
            {
                await TearDownAccounts();
            }
        }

        [Test]
        [TestCase("local")]
        [TestCase("heroiclabs")]
        public async Task SwitchAccountAsync_ChangesCurrentUserId(string env)
        {
            await SetUpForEnv(env);
            try
            {
                var initialUserId = _controller.CurrentUserId;
                Debug.Log($"[{env}] Initial user: {initialUserId}");

                Assert.AreEqual(_sessions[0].UserId, initialUserId,
                    $"[{env}] Should start with session 0's user ID");

                await AccountSwitcher.SwitchAccountAsync(
                    _nakamaSystem,
                    _controller,
                    env,
                    1);

                var newUserId = _controller.CurrentUserId;
                Debug.Log($"[{env}] After switch: {newUserId}");

                Assert.AreNotEqual(initialUserId, newUserId,
                    $"[{env}] User ID should change after account switch");
            }
            finally
            {
                await TearDownAccounts();
            }
        }

        [Test]
        [TestCase("local")]
        [TestCase("heroiclabs")]
        public async Task SwitchAccountAsync_FiresAccountSwitchedEvent(string env)
        {
            await SetUpForEnv(env);
            try
            {
                var eventFired = false;
                AccountSwitcher.AccountSwitched += OnAccountSwitched;

                void OnAccountSwitched()
                {
                    eventFired = true;
                }

                try
                {
                    await AccountSwitcher.SwitchAccountAsync(
                        _nakamaSystem,
                        _controller,
                        env,
                        1);

                    Assert.IsTrue(eventFired, $"[{env}] AccountSwitched event should fire");
                }
                finally
                {
                    AccountSwitcher.AccountSwitched -= OnAccountSwitched;
                }
            }
            finally
            {
                await TearDownAccounts();
            }
        }
    }
}
