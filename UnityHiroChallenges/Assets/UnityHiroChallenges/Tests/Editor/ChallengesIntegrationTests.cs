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
using System.Reflection;
using System.Threading.Tasks;
using Hiro;
using Hiro.System;
using Hiro.Unity;
using Nakama;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UIElements;

namespace HiroChallenges.Tests.Editor
{
    /// <summary>
    /// EditMode integration tests for challenge creation via ChallengesController.
    /// Requires: Nakama server running at 127.0.0.1:7350
    /// </summary>
    [TestFixture]
    public class ChallengeCreationTests
    {
        private const string Scheme = "http";
        private const string Host = "127.0.0.1";
        private const int Port = 7350;
        private const string ServerKey = "defaultkey";

        private GameObject _controllerGo;
        private ChallengesController _controller;
        private IClient _client;
        private ISession _session;
        private ISession _inviteeSession;
        private NakamaSystem _nakamaSystem;
        private ChallengesSystem _challengesSystem;
        private EconomySystem _economySystem;
        private string _testDeviceId;

        [SetUp]
        public async Task SetUp()
        {
            _testDeviceId = $"test-device-{Guid.NewGuid():N}";
            _client = new Client(Scheme, Host, Port, ServerKey);
            _session = await _client.AuthenticateDeviceAsync($"{_testDeviceId}_0");
            _inviteeSession = await _client.AuthenticateDeviceAsync($"{_testDeviceId}_1");

            var logger = new Hiro.Unity.Logger();
            _nakamaSystem = new NakamaSystem(logger, _client, _ => Task.FromResult(_session));
            await _nakamaSystem.InitializeAsync();

            _challengesSystem = new ChallengesSystem(logger, _nakamaSystem);
            await _challengesSystem.InitializeAsync();

            _economySystem = new EconomySystem(logger, _nakamaSystem, EconomyStoreType.Unspecified);
            await _economySystem.InitializeAsync();

            // Create controller and inject systems
            _controllerGo = new GameObject("TestController");
            _controllerGo.AddComponent<UIDocument>();
            _controller = _controllerGo.AddComponent<ChallengesController>();

            var bindingFlags = BindingFlags.NonPublic | BindingFlags.Instance;
            typeof(ChallengesController).GetField("_nakamaSystem", bindingFlags).SetValue(_controller, _nakamaSystem);
            typeof(ChallengesController).GetField("_challengesSystem", bindingFlags).SetValue(_controller, _challengesSystem);
            typeof(ChallengesController).GetField("_economySystem", bindingFlags).SetValue(_controller, _economySystem);
            typeof(ChallengesController).GetField("_currentUserId", bindingFlags).SetValue(_controller, _session.UserId);
        }

        [TearDown]
        public async Task TearDown()
        {
            if (_controllerGo != null)
                UnityEngine.Object.DestroyImmediate(_controllerGo);

            await _client.DeleteAccountAsync(_session);
            await _client.DeleteAccountAsync(_inviteeSession);
        }

        [Test]
        public async Task CreateChallengeAsync_WithValidTemplate_Succeeds()
        {
            await _controller.LoadChallengeTemplatesAsync();

            var defaults = _controller.GetCreationDefaults();

            await _controller.CreateChallengeAsync(
                0,
                $"Test Challenge {DateTime.UtcNow.Ticks}",
                defaults.MaxParticipants,
                _inviteeSession.Username,
                defaults.DelaySeconds,
                defaults.DurationSeconds,
                true
            );

            await _controller.RefreshChallengesAsync();

            Assert.IsTrue(_controller.Challenges.Count > 0, "Challenge should have been created");
            Debug.Log($"Created challenge, now have {_controller.Challenges.Count} challenges");
        }

        [Test]
        public async Task SelectChallengeAsync_AfterCreate_ReturnsDetails()
        {
            await _controller.LoadChallengeTemplatesAsync();

            var defaults = _controller.GetCreationDefaults();

            await _controller.CreateChallengeAsync(
                0,
                $"Get Test {DateTime.UtcNow.Ticks}",
                defaults.MaxParticipants,
                _inviteeSession.Username,
                defaults.DelaySeconds,
                defaults.DurationSeconds,
                true
            );

            await _controller.RefreshChallengesAsync();

            Assert.IsTrue(_controller.Challenges.Count > 0, "Should have at least one challenge");

            var challenge = _controller.Challenges[0];
            var participants = await _controller.SelectChallengeAsync(challenge);

            Assert.IsNotNull(participants);
            Debug.Log($"Challenge '{challenge.Name}' has {participants.Count} participants");
        }
    }

    /// <summary>
    /// EditMode integration tests for ChallengesController.
    /// Requires: Nakama server running at 127.0.0.1:7350
    /// </summary>
    [TestFixture]
    public class ChallengesControllerTests
    {
        private const string Scheme = "http";
        private const string Host = "127.0.0.1";
        private const int Port = 7350;
        private const string ServerKey = "defaultkey";

        private GameObject _controllerGo;
        private ChallengesController _controller;
        private IClient _client;
        private ISession _session;
        private NakamaSystem _nakamaSystem;
        private ChallengesSystem _challengesSystem;
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

            _challengesSystem = new ChallengesSystem(logger, _nakamaSystem);
            await _challengesSystem.InitializeAsync();

            _economySystem = new EconomySystem(logger, _nakamaSystem, EconomyStoreType.Unspecified);
            await _economySystem.InitializeAsync();

            // Create controller without UIDocument requirement
            _controllerGo = new GameObject("TestController");
            _controllerGo.AddComponent<UIDocument>();
            _controller = _controllerGo.AddComponent<ChallengesController>();

            // Inject systems via reflection
            var bindingFlags = BindingFlags.NonPublic | BindingFlags.Instance;
            typeof(ChallengesController).GetField("_nakamaSystem", bindingFlags).SetValue(_controller, _nakamaSystem);
            typeof(ChallengesController).GetField("_challengesSystem", bindingFlags).SetValue(_controller, _challengesSystem);
            typeof(ChallengesController).GetField("_economySystem", bindingFlags).SetValue(_controller, _economySystem);
            typeof(ChallengesController).GetField("_currentUserId", bindingFlags).SetValue(_controller, _session.UserId);
        }

        [TearDown]
        public async Task TearDown()
        {
            if (_controllerGo != null)
                UnityEngine.Object.DestroyImmediate(_controllerGo);

            await _client.DeleteAccountAsync(_session);
        }

        [Test]
        public async Task LoadChallengeTemplatesAsync_ReturnsTemplateNames()
        {
            var templates = await _controller.LoadChallengeTemplatesAsync();

            Assert.IsNotNull(templates);
            Assert.IsTrue(templates.Count > 0, "No challenge templates found on server");
            Debug.Log($"Found {templates.Count} templates");

            foreach (var name in templates)
            {
                Debug.Log($"Template: {name}");
            }
        }

        [Test]
        public async Task GetTemplate_AfterLoad_ReturnsTemplateDetails()
        {
            await _controller.LoadChallengeTemplatesAsync();

            var template = _controller.GetTemplate(0);

            Assert.IsNotNull(template, "First template should exist");
            Debug.Log($"Template MaxNumScore: {template.MaxNumScore}");
        }

        [Test]
        public async Task RefreshChallengesAsync_ReturnsChallengesList()
        {
            var result = await _controller.RefreshChallengesAsync();

            Assert.IsNotNull(_controller.Challenges);
            Debug.Log($"Found {_controller.Challenges.Count} challenges");
        }

        [Test]
        public async Task SelectChallengeAsync_WhenChallengesExist_ReturnsParticipants()
        {
            await _controller.RefreshChallengesAsync();

            if (_controller.Challenges.Count == 0)
            {
                Assert.Pass("No challenges available to select");
                return;
            }

            var challenge = _controller.Challenges[0];
            var participants = await _controller.SelectChallengeAsync(challenge);

            Assert.IsNotNull(participants);
            Debug.Log($"Challenge '{challenge.Name}' has {participants.Count} participants");
        }

        [Test]
        public async Task GetPermissions_WhenChallengeSelected_ReturnsValidPermissions()
        {
            await _controller.RefreshChallengesAsync();

            if (_controller.Challenges.Count == 0)
            {
                Assert.Pass("No challenges available");
                return;
            }

            var challenge = _controller.Challenges[0];
            var participants = await _controller.SelectChallengeAsync(challenge);
            var permissions = _controller.GetPermissions(challenge, participants);

            Assert.IsNotNull(permissions);
            Debug.Log($"Permissions: CanJoin={permissions.CanJoin}, CanSubmitScore={permissions.CanSubmitScore}, CanClaim={permissions.CanClaim}");
        }

        [Test]
        public void GetCreationDefaults_ReturnsDefaults()
        {
            var defaults = _controller.GetCreationDefaults();

            Assert.IsNotNull(defaults);
            Assert.AreEqual(100, defaults.MaxParticipants);
            Assert.AreEqual(0, defaults.DelaySeconds);
            Assert.AreEqual(2000, defaults.DurationSeconds);
        }
    }

    /// <summary>
    /// EditMode integration tests for the full challenge flow with multiple players.
    /// Uses the Account Switcher pattern to switch between accounts on a single controller.
    /// Requires: Nakama server running at 127.0.0.1:7350
    /// </summary>
    [TestFixture]
    public class ChallengeFlowTests
    {
        private const string Scheme = "http";
        private const string Host = "127.0.0.1";
        private const int Port = 7350;
        private const string ServerKey = "defaultkey";

        private IClient _client;
        private string _testDeviceId;

        private GameObject _controllerGo;
        private ChallengesController _controller;
        private NakamaSystem _nakamaSystem;
        private ChallengesSystem _challengesSystem;
        private EconomySystem _economySystem;

        // Store sessions for cleanup
        private ISession[] _sessions;
        private string[] _usernames;

        [SetUp]
        public async Task SetUp()
        {
            _testDeviceId = $"test-device-{Guid.NewGuid():N}";
            _client = new Client(Scheme, Host, Port, ServerKey);

            // Authenticate all 4 accounts and store their sessions/usernames
            _sessions = new ISession[4];
            _usernames = new string[4];

            for (var i = 0; i < 4; i++)
            {
                _sessions[i] = await _client.AuthenticateDeviceAsync($"{_testDeviceId}_{i}");
                _usernames[i] = _sessions[i].Username;
            }

            var logger = new Hiro.Unity.Logger();

            // Initialize with account 0
            _nakamaSystem = new NakamaSystem(logger, _client, _ => Task.FromResult(_sessions[0]));
            await _nakamaSystem.InitializeAsync();

            _challengesSystem = new ChallengesSystem(logger, _nakamaSystem);
            await _challengesSystem.InitializeAsync();

            _economySystem = new EconomySystem(logger, _nakamaSystem, EconomyStoreType.Unspecified);
            await _economySystem.InitializeAsync();

            // Create controller
            _controllerGo = new GameObject("TestController");
            _controllerGo.AddComponent<UIDocument>();
            _controller = _controllerGo.AddComponent<ChallengesController>();

            var bindingFlags = BindingFlags.NonPublic | BindingFlags.Instance;
            typeof(ChallengesController).GetField("_nakamaSystem", bindingFlags).SetValue(_controller, _nakamaSystem);
            typeof(ChallengesController).GetField("_challengesSystem", bindingFlags).SetValue(_controller, _challengesSystem);
            typeof(ChallengesController).GetField("_economySystem", bindingFlags).SetValue(_controller, _economySystem);
            typeof(ChallengesController).GetField("_currentUserId", bindingFlags).SetValue(_controller, _sessions[0].UserId);
        }

        [TearDown]
        public async Task TearDown()
        {
            if (_controllerGo != null)
                UnityEngine.Object.DestroyImmediate(_controllerGo);

            // Re-authenticate to get fresh sessions (originals were modified during switching)
            for (var i = 0; i < _sessions.Length; i++)
            {
                var freshSession = await _client.AuthenticateDeviceAsync($"{_testDeviceId}_{i}");
                await _client.DeleteAccountAsync(freshSession);
            }
        }

        private async Task SwitchToAccountAsync(int accountIndex)
        {
            var newSession = _sessions[accountIndex];
            await AccountSwitcher.SwitchToSessionAsync(_nakamaSystem, _controller, newSession);

            var bindingFlags = BindingFlags.NonPublic | BindingFlags.Instance;
            typeof(ChallengesController).GetField("_currentUserId", bindingFlags).SetValue(_controller, newSession.UserId);

            Debug.Log($"Switched to account {accountIndex}: {newSession.Username}");
        }

        [Test]
        public async Task FullChallengeFlow_CreateInviteJoinSubmitScores_ScoresAreSortedByRank()
        {
            // Account 0: Load templates and create challenge with 3 invitees
            await _controller.LoadChallengeTemplatesAsync();
            var defaults = _controller.GetCreationDefaults();

            var invitees = $"{_usernames[1]},{_usernames[2]},{_usernames[3]}";

            await _controller.CreateChallengeAsync(
                0,
                $"Flow Test {DateTime.UtcNow.Ticks}",
                defaults.MaxParticipants,
                invitees,
                0,
                defaults.DurationSeconds,
                true
            );

            await _controller.RefreshChallengesAsync();
            Assert.IsTrue(_controller.Challenges.Count > 0, "Challenge should have been created");

            var challenge = _controller.Challenges[0];
            await _controller.SelectChallengeAsync(challenge);
            Debug.Log($"Created challenge: {challenge.Name}");

            // Account 0 submits score 10
            await _controller.SubmitScoreAsync(10, 0);
            Debug.Log($"Account 0 ({_usernames[0]}) submitted score: 10");

            // Switch to account 1, join and submit score 25
            await SwitchToAccountAsync(1);
            await _controller.RefreshChallengesAsync();
            await _controller.SelectChallengeAsync(_controller.Challenges[0]);
            await _controller.JoinChallengeAsync();
            await _controller.SubmitScoreAsync(25, 0);
            Debug.Log($"Account 1 ({_usernames[1]}) joined and submitted score: 25");

            // Switch to account 2, join and submit score 50
            await SwitchToAccountAsync(2);
            await _controller.RefreshChallengesAsync();
            await _controller.SelectChallengeAsync(_controller.Challenges[0]);
            await _controller.JoinChallengeAsync();
            await _controller.SubmitScoreAsync(50, 0);
            Debug.Log($"Account 2 ({_usernames[2]}) joined and submitted score: 50");

            // Switch to account 3, join and submit score 75
            await SwitchToAccountAsync(3);
            await _controller.RefreshChallengesAsync();
            await _controller.SelectChallengeAsync(_controller.Challenges[0]);
            await _controller.JoinChallengeAsync();
            await _controller.SubmitScoreAsync(75, 0);
            Debug.Log($"Account 3 ({_usernames[3]}) joined and submitted score: 75");

            // Switch back to account 0 to verify final state
            await SwitchToAccountAsync(0);
            await _controller.RefreshChallengesAsync();
            var participants = await _controller.SelectChallengeAsync(_controller.Challenges[0]);

            Assert.IsNotNull(participants);
            Assert.AreEqual(4, participants.Count, "Should have 4 participants");

            Debug.Log("=== Participants in returned order ===");
            for (var i = 0; i < participants.Count; i++)
            {
                var p = participants[i];
                Debug.Log($"Position {i}: {p.Username}, Score: {p.Score}, Rank: {p.Rank}");
            }

            // Verify scores are sorted by rank (highest score = rank 1)
            Assert.AreEqual(1, participants[0].Rank, $"First should be rank 1, got {participants[0].Rank}");
            Assert.AreEqual(75, participants[0].Score, $"Rank 1 should have score 75, got {participants[0].Score}");

            Assert.AreEqual(2, participants[1].Rank, $"Second should be rank 2, got {participants[1].Rank}");
            Assert.AreEqual(50, participants[1].Score, $"Rank 2 should have score 50, got {participants[1].Score}");

            Assert.AreEqual(3, participants[2].Rank, $"Third should be rank 3, got {participants[2].Rank}");
            Assert.AreEqual(25, participants[2].Score, $"Rank 3 should have score 25, got {participants[2].Score}");

            Assert.AreEqual(4, participants[3].Rank, $"Fourth should be rank 4, got {participants[3].Rank}");
            Assert.AreEqual(10, participants[3].Score, $"Rank 4 should have score 10, got {participants[3].Score}");

            Debug.Log("=== All assertions passed - scores are sorted correctly by rank ===");
        }
    }
}
