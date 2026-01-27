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
}
