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
using System.Linq;
using System.Threading.Tasks;
using Hiro;
using HiroChallenges.Tools;
using Nakama;
using NUnit.Framework;
using UnityEngine;

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

            _controller = new ChallengesController(_nakamaSystem, _challengesSystem, _economySystem);
        }

        [TearDown]
        public async Task TearDown()
        {
            await _client.DeleteAccountAsync(_session);
            await _client.DeleteAccountAsync(_inviteeSession);
        }

        [Test]
        public async Task CreateChallengeAsync_WithValidTemplate_Succeeds()
        {
            var templates = await _controller.LoadChallengeTemplatesAsync();
            var templateId = templates[0].Id;

            var defaults = _controller.GetCreationDefaults();

            await _controller.CreateChallengeAsync(
                templateId,
                $"Test Challenge {DateTime.UtcNow.Ticks}",
                defaults.MaxParticipants,
                new[] { _inviteeSession.UserId },
                defaults.DelaySeconds,
                defaults.DurationSeconds,
                true
            );

            await _controller.RefreshChallengesAsync();

            Assert.IsTrue(_controller.Challenges.Count > 0, "Challenge should have been created");
            Debug.Log($"Created challenge, now have {_controller.Challenges.Count} challenges");
        }

        /// <summary>
        /// Tests that stale accounts are automatically cleaned up from the cache
        /// when AccountSwitcher is initialized, preventing invalid user suggestions.
        /// </summary>
        [Test]
        public async Task AccountSwitcher_OnInitialize_RemovesStaleAccountsFromCache()
        {
            // Setup: Store a fake/stale user ID in AccountSwitcher cache
            var staleUsername = "StaleTestUser";
            var staleUserId = Guid.NewGuid().ToString(); // Non-existent user ID

            AccountSwitcher.StoreAccountInfo("stale_test", new AccountSwitcher.AccountInfo
            {
                Username = staleUsername,
                UserId = staleUserId
            });

            try
            {
                // Verify the stale username is in the cache before re-initialization
                var knownUsernamesBefore = new List<string>(AccountSwitcher.GetKnownUsernames());
                Assert.Contains(staleUsername, knownUsernamesBefore,
                    "Stale username should be in cache before re-initialization");

                // Re-initialize AccountSwitcher - this triggers cleanup of stale accounts
                AccountSwitcher.Initialize(_nakamaSystem, "test");

                // Give time for async cleanup to complete
                await Task.Delay(500);

                // Verify the stale username has been removed from cache
                var knownUsernamesAfter = new List<string>(AccountSwitcher.GetKnownUsernames());
                Assert.IsFalse(knownUsernamesAfter.Contains(staleUsername),
                    "Stale username should be removed from cache after re-initialization");

                Debug.Log("Stale account was correctly cleaned up during initialization");
            }
            finally
            {
                // Clean up
                AccountSwitcher.ClearAccounts();
            }
        }

        [Test]
        public async Task SelectChallengeAsync_AfterCreate_ReturnsDetails()
        {
            var templates = await _controller.LoadChallengeTemplatesAsync();
            var templateId = templates[0].Id;

            var defaults = _controller.GetCreationDefaults();

            await _controller.CreateChallengeAsync(
                templateId,
                $"Get Test {DateTime.UtcNow.Ticks}",
                defaults.MaxParticipants,
                new[] { _inviteeSession.UserId },
                defaults.DelaySeconds,
                defaults.DurationSeconds,
                true
            );

            await _controller.RefreshChallengesAsync();

            Assert.IsTrue(_controller.Challenges.Count > 0, "Should have at least one challenge");

            var challenge = _controller.Challenges[0];
            var participants = await _controller.SelectChallengeAsync(challenge.Id);

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

            _controller = new ChallengesController(_nakamaSystem, _challengesSystem, _economySystem);
        }

        [TearDown]
        public async Task TearDown()
        {
            await _client.DeleteAccountAsync(_session);
        }

        [Test]
        public async Task LoadChallengeTemplatesAsync_ReturnsTemplateOptions()
        {
            var templates = await _controller.LoadChallengeTemplatesAsync();

            Assert.IsNotNull(templates);
            Assert.IsTrue(templates.Count > 0, "No challenge templates found on server");
            Debug.Log($"Found {templates.Count} templates");

            foreach (var template in templates) Debug.Log($"Template: {template.DisplayName} ({template.Id})");
        }

        [Test]
        public async Task GetTemplate_AfterLoad_ReturnsTemplateDetails()
        {
            var templates = await _controller.LoadChallengeTemplatesAsync();

            var template = _controller.GetTemplate(templates[0].Id);

            Assert.IsNotNull(template, "First template should exist");
            Debug.Log($"Template MaxNumScore: {template.MaxNumScore}");
        }

        [Test]
        public async Task LoadChallengeTemplatesAsync_ReturnsOptionsSortedByDisplayName()
        {
            var templates = await _controller.LoadChallengeTemplatesAsync();

            if (templates.Count < 2)
                Assert.Inconclusive("Need at least two templates to validate ordering.");

            var ordered = templates
                .OrderBy(template => template.DisplayName, StringComparer.OrdinalIgnoreCase)
                .ToList();

            Assert.IsTrue(
                templates.SequenceEqual(ordered),
                "Template options should be sorted by display name.");
        }

        [Test]
        public async Task RefreshChallengesAsync_ReturnsChallengesList()
        {
            await _controller.RefreshChallengesAsync();

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
            var participants = await _controller.SelectChallengeAsync(challenge.Id);

            Assert.IsNotNull(participants);
            Debug.Log($"Challenge '{challenge.Name}' has {participants.Count} participants");
        }

        [Test]
        public async Task ChallengeExtensions_WhenChallengeSelected_ReturnsValidPermissions()
        {
            await _controller.RefreshChallengesAsync();

            if (_controller.Challenges.Count == 0)
            {
                Assert.Pass("No challenges available");
                return;
            }

            var challenge = _controller.Challenges[0];
            var participants = await _controller.SelectChallengeAsync(challenge.Id);
            var participant = _controller.GetCurrentParticipant(participants);

            Debug.Log(
                $"Permissions: CanJoin={challenge.CanJoin(participant)}, CanSubmitScore={challenge.CanSubmitScore(participant)}, CanClaim={challenge.CanClaimReward(participant)}");
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

        private ChallengesController _controller;
        private NakamaSystem _nakamaSystem;
        private ChallengesSystem _challengesSystem;
        private EconomySystem _economySystem;

        [SetUp]
        public async Task SetUp()
        {
            // Clear any cached account data from previous test runs
            AccountSwitcher.ClearAccounts();
            ClearTestTokens();

            _client = new Client(Scheme, Host, Port, ServerKey);

            var logger = new Hiro.Unity.Logger();

            // Initialize with account 0
            _nakamaSystem =
                new NakamaSystem(logger, _client, HiroChallengesCoordinator.NakamaAuthorizerFunc("test"));
            await _nakamaSystem.InitializeAsync();

            _challengesSystem = new ChallengesSystem(logger, _nakamaSystem);
            await _challengesSystem.InitializeAsync();

            _economySystem = new EconomySystem(logger, _nakamaSystem, EconomyStoreType.Unspecified);
            await _economySystem.InitializeAsync();

            _controller = new ChallengesController(_nakamaSystem, _challengesSystem, _economySystem);
        }

        [TearDown]
        public async Task TearDown()
        {
            // Delete all test accounts
            for (var i = 0; i < 4; i++)
            {
                var session = await HiroChallengesCoordinator.NakamaAuthorizerFunc("test", i).Invoke(_client);
                await _client.DeleteAccountAsync(session);
            }

            // Clear cached data
            AccountSwitcher.ClearAccounts();
            ClearTestTokens();
        }

        private static void ClearTestTokens()
        {
            PlayerPrefs.DeleteKey("nakama.DeviceId_test");
            for (var i = 0; i < 4; i++)
            {
                PlayerPrefs.DeleteKey($"nakama.AuthToken_test_{i}");
                PlayerPrefs.DeleteKey($"nakama.RefreshToken_test_{i}");
            }
        }

        [Test]
        public async Task FullChallengeFlow_CreateInviteJoinSubmitScores_ScoresAreSortedByRank()
        {
            // Get user IDs for all accounts
            var userIds = new string[4];
            for (var i = 0; i < 4; i++)
            {
                var session = await AccountSwitcher.SwitchAccountAsync(_nakamaSystem, "test", i);
                userIds[i] = session.UserId;
            }

            // Switch to Account 0: Load templates and create challenge with 1 invitee
            await AccountSwitcher.SwitchAccountAsync(_nakamaSystem, "test", 0);
            var templates = await _controller.LoadChallengeTemplatesAsync();
            var templateId = templates[0].Id;
            var defaults = _controller.GetCreationDefaults();

            var createdChallenge = await _controller.CreateChallengeAsync(
                templateId,
                $"Flow Test {DateTime.UtcNow.Ticks}",
                defaults.MaxParticipants,
                new[] { userIds[1] },
                0,
                defaults.DurationSeconds,
                true
            );

            var challengeId = createdChallenge.Id;
            var participants0 = await _controller.SelectChallengeAsync(challengeId);
            Assert.AreEqual(createdChallenge.Id, _controller.SelectedChallenge.Id);
            Assert.AreEqual(2, participants0.Count, "Challenge should have 2 participants (owner + 1 invitee)");

            // Account 0 submits score 10
            await _controller.SubmitScoreAsync(10, 0);

            // Switch to Account 1, join and submit score 25
            await AccountSwitcher.SwitchAccountAsync(_nakamaSystem, "test", 1);
            var participants1 = await _controller.SelectChallengeAsync(challengeId);
            Assert.AreEqual(createdChallenge.Id, _controller.SelectedChallenge.Id);
            var challenge1 = _controller.SelectedChallenge;
            var participant1 = _controller.GetCurrentParticipant(participants1);

            Assert.IsNotNull(participant1, "Account 1 should be a participant (was invited)");
            Assert.AreEqual(ChallengeState.Invited, participant1.State,
                "Account 1 should be in Invited state before joining");
            Assert.IsTrue(challenge1.IsActive, "Challenge should be active");
            Assert.IsTrue(challenge1.Size < challenge1.MaxSize, "Challenge should have room for more participants");
            Assert.AreEqual(2, participants1.Count, "Challenge should have 2 participants (owner + 1 invitee)");
            Assert.IsTrue(challenge1.CanJoin(participant1), "Account 1 should be able to join");
            Assert.IsFalse(challenge1.CanSubmitScore(participant1),
                "Account 1 should not be able to submit score before joining");

            await _controller.JoinChallengeAsync();
            participants1 = await _controller.SelectChallengeAsync(challengeId);
            Assert.AreEqual(createdChallenge.Id, _controller.SelectedChallenge.Id);
            challenge1 = _controller.SelectedChallenge;
            participant1 = _controller.GetCurrentParticipant(participants1);
            Assert.AreEqual(ChallengeState.Joined, participant1.State,
                "Account 1 should be in Joined state after joining");
            Assert.IsFalse(challenge1.CanJoin(participant1), "Account 1 should not be able to join after joining");
            Assert.IsTrue(challenge1.CanSubmitScore(participant1),
                "Account 1 should be able to submit score after joining");

            await _controller.SubmitScoreAsync(25, 0);

            // Switch back to Account 0 to invite Accounts 2 and 3
            await AccountSwitcher.SwitchAccountAsync(_nakamaSystem, "test", 0);
            await _controller.SelectChallengeAsync(challengeId);
            Assert.AreEqual(createdChallenge.Id, _controller.SelectedChallenge.Id);
            await _controller.InviteToChallengeAsync(new[] { userIds[2], userIds[3] });

            // Switch to Account 2, join and submit score 50
            await AccountSwitcher.SwitchAccountAsync(_nakamaSystem, "test", 2);
            var participants2 = await _controller.SelectChallengeAsync(challengeId);
            Assert.AreEqual(createdChallenge.Id, _controller.SelectedChallenge.Id);
            var challenge2 = _controller.SelectedChallenge;
            var participant2 = _controller.GetCurrentParticipant(participants2);
            Assert.IsNotNull(participant2, "Account 2 should be a participant (was invited)");
            Assert.AreEqual(4, participants2.Count, "Challenge should have 4 participants (owner + 3 invitees)");
            Assert.AreEqual(ChallengeState.Invited, participant2.State,
                "Account 2 should be in Invited state before joining");
            Assert.IsTrue(challenge2.CanJoin(participant2), "Account 2 should be able to join");
            Assert.IsFalse(challenge2.CanSubmitScore(participant2),
                "Account 2 should not be able to submit score before joining");

            await _controller.JoinChallengeAsync();
            participants2 = await _controller.SelectChallengeAsync(challengeId);
            Assert.AreEqual(createdChallenge.Id, _controller.SelectedChallenge.Id);
            challenge2 = _controller.SelectedChallenge;
            participant2 = _controller.GetCurrentParticipant(participants2);
            Assert.AreEqual(ChallengeState.Joined, participant2.State,
                "Account 2 should be in Joined state after joining");
            Assert.IsTrue(challenge2.CanSubmitScore(participant2),
                "Account 2 should be able to submit score after joining");

            await _controller.SubmitScoreAsync(50, 0);

            // Switch to Account 3, join and submit score 75
            await AccountSwitcher.SwitchAccountAsync(_nakamaSystem, "test", 3);
            var participants3 = await _controller.SelectChallengeAsync(challengeId);
            Assert.AreEqual(createdChallenge.Id, _controller.SelectedChallenge.Id);
            var challenge3 = _controller.SelectedChallenge;
            var participant3 = _controller.GetCurrentParticipant(participants3);
            Assert.IsNotNull(participant3, "Account 3 should be a participant (was invited)");
            Assert.AreEqual(4, participants3.Count, "Challenge should have 4 participants (owner + 3 invitees)");
            Assert.AreEqual(ChallengeState.Invited, participant3.State,
                "Account 3 should be in Invited state before joining");
            Assert.IsTrue(challenge3.CanJoin(participant3), "Account 3 should be able to join");
            Assert.IsFalse(challenge3.CanSubmitScore(participant3),
                "Account 3 should not be able to submit score before joining");

            await _controller.JoinChallengeAsync();
            participants3 = await _controller.SelectChallengeAsync(challengeId);
            challenge3 = _controller.SelectedChallenge;
            participant3 = _controller.GetCurrentParticipant(participants3);
            Assert.AreEqual(ChallengeState.Joined, participant3.State,
                "Account 3 should be in Joined state after joining");
            Assert.IsTrue(challenge3.CanSubmitScore(participant3),
                "Account 3 should be able to submit score after joining");

            await _controller.SubmitScoreAsync(75, 0);

            // Switch back to Account 0 to verify final state
            await AccountSwitcher.SwitchAccountAsync(_nakamaSystem, "test", 0);
            var participants = await _controller.SelectChallengeAsync(challengeId);
            Assert.AreEqual(createdChallenge.Id, _controller.SelectedChallenge.Id);
            Assert.IsNotNull(participants);
            Assert.AreEqual(4, participants.Count, "Should have 4 participants");

            // Verify all participants are in Joined state
            foreach (var p in participants)
                Assert.AreEqual(ChallengeState.Joined, p.State, $"Participant {p.Username} should be in Joined state");

            // Verify scores are sorted by rank (highest score = rank 1)
            Assert.AreEqual(1, participants[0].Rank, "First should be rank 1");
            Assert.AreEqual(75, participants[0].Score, "Rank 1 should have score 75");
            Assert.AreEqual(userIds[3], participants[0].Id, "Rank 1 should be Account 3");

            Assert.AreEqual(2, participants[1].Rank, "Second should be rank 2");
            Assert.AreEqual(50, participants[1].Score, "Rank 2 should have score 50");
            Assert.AreEqual(userIds[2], participants[1].Id, "Rank 2 should be Account 2");

            Assert.AreEqual(3, participants[2].Rank, "Third should be rank 3");
            Assert.AreEqual(25, participants[2].Score, "Rank 3 should have score 25");
            Assert.AreEqual(userIds[1], participants[2].Id, "Rank 3 should be Account 1");

            Assert.AreEqual(4, participants[3].Rank, "Fourth should be rank 4");
            Assert.AreEqual(10, participants[3].Score, "Rank 4 should have score 10");
            Assert.AreEqual(userIds[0], participants[3].Id, "Rank 4 should be Account 0");
        }

        /// <summary>
        /// Tests that an invited participant can join even when challenge size equals max_size.
        /// Reproduces bug where CanJoin returns false for invited user when challenge appears full.
        /// </summary>
        [Test]
        public async Task CanJoin_WhenInvitedAndChallengeAtMaxSize_ReturnsTrue()
        {
            // Setup: Create challenge with max_size = 3
            // Uses accounts 0, 1, 2 from existing setup
            var templates = await _controller.LoadChallengeTemplatesAsync();
            Assert.IsNotEmpty(templates, "Should have at least one template");

            // Get user IDs for accounts 1 and 2
            var session1 = await HiroChallengesCoordinator.NakamaAuthorizerFunc("test", 1).Invoke(_client);
            var session2 = await HiroChallengesCoordinator.NakamaAuthorizerFunc("test", 2).Invoke(_client);
            var userIds = new[] { session1.UserId, session2.UserId };

            // Account 0 creates challenge with max 3 participants, invites accounts 1 and 2
            var createdChallenge = await _controller.CreateChallengeAsync(
                templates[0].Id,
                "MaxSizeTest",
                3, // max 3 participants
                userIds,
                0,
                2000,
                false
            );

            var challengeId = createdChallenge.Id;
            Assert.IsNotNull(challengeId);

            // Account 0 (owner) joins and submits score
            await _controller.JoinChallengeAsync();
            await _controller.SubmitScoreAsync(10, 0);

            // Account 1 joins and submits score
            await AccountSwitcher.SwitchAccountAsync(_nakamaSystem, "test", 1);
            await _controller.SelectChallengeAsync(challengeId);
            await _controller.JoinChallengeAsync();
            await _controller.SubmitScoreAsync(20, 0);

            // Now challenge has: size=3, max_size=3
            // Account 0: Joined, Account 1: Joined, Account 2: Invited (not yet joined)

            // Switch to Account 2 (invited but not joined)
            await AccountSwitcher.SwitchAccountAsync(_nakamaSystem, "test", 2);
            var participants = await _controller.SelectChallengeAsync(challengeId);
            var challenge = _controller.SelectedChallenge;
            var participant = _controller.GetCurrentParticipant(participants);

            // Verify state
            Assert.IsNotNull(participant, "Account 2 should be a participant (was invited)");
            Assert.AreEqual(ChallengeState.Invited, participant.State, "Account 2 should be in Invited state");
            Assert.AreEqual(challenge.Size, challenge.MaxSize, "Challenge should be at max size");

            // The invited participant is already counted in size, so they should be able to join
            Assert.IsTrue(challenge.CanJoin(participant),
                $"Invited participant should be able to join even when size({challenge.Size}) == max_size({challenge.MaxSize}). " +
                $"Participant state: {participant.State}");
        }
    }
}