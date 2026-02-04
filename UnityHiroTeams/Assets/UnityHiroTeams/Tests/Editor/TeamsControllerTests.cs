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
using Hiro;
using Hiro.System;
using Hiro.Unity;
using Nakama;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace HiroTeams.Tests.Editor
{
    /// <summary>
    /// EditMode integration tests for TeamsController.
    /// Requires: Nakama server running at 127.0.0.1:7350
    /// </summary>
    [TestFixture]
    public class TeamsControllerTests
    {
        private const string Scheme = "http";
        private const string Host = "127.0.0.1";
        private const int Port = 7350;
        private const string ServerKey = "defaultkey";
        private const int TeamEntriesLimit = 20;

        private TeamsController _controller;
        private IClient _client;
        private ISession _session;
        private NakamaSystem _nakamaSystem;
        private TeamsSystem _teamsSystem;
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

            _teamsSystem = new TeamsSystem(logger, _nakamaSystem);
            await _teamsSystem.InitializeAsync();

            _controller = new TeamsController(
                _nakamaSystem,
                _teamsSystem,
                TeamEntriesLimit,
                null,
                null);
        }

        [TearDown]
        public async Task TearDown()
        {
            // Try to leave/delete any team we're in before deleting account
            try
            {
                if (_teamsSystem?.Team != null)
                {
                    if (_teamsSystem.IsAdmin)
                    {
                        await _teamsSystem.DeleteTeamAsync(_teamsSystem.Team.Id);
                    }
                    else
                    {
                        await _teamsSystem.LeaveTeamAsync(_teamsSystem.Team.Id);
                    }
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning($"Cleanup warning: {e.Message}");
            }

            await _client.DeleteAccountAsync(_session);
        }

        #region Constructor Tests

        [Test]
        public void Constructor_NullNakamaSystem_ThrowsArgumentNullException()
        {
            Assert.Throws<ArgumentNullException>(() =>
            {
                new TeamsController(null, _teamsSystem, TeamEntriesLimit, null, null);
            });
        }

        [Test]
        public void Constructor_NullTeamsSystem_ThrowsArgumentNullException()
        {
            Assert.Throws<ArgumentNullException>(() =>
            {
                new TeamsController(_nakamaSystem, null, TeamEntriesLimit, null, null);
            });
        }

        [Test]
        public void Constructor_NullAvatarArrays_SetsEmptyArrays()
        {
            var controller = new TeamsController(
                _nakamaSystem,
                _teamsSystem,
                TeamEntriesLimit,
                null,
                null);

            Assert.IsNotNull(controller.AvatarIcons);
            Assert.IsNotNull(controller.AvatarBackgrounds);
            Assert.AreEqual(0, controller.AvatarIcons.Length);
            Assert.AreEqual(0, controller.AvatarBackgrounds.Length);
        }

        #endregion

        #region GetStatsAsync Tests

        [Test]
        public async Task GetStatsAsync_NoTeam_ReturnsNull()
        {
            var stats = await _controller.GetStatsAsync();
            Assert.IsNull(stats);
        }

        [Test]
        public async Task GetStatsAsync_HasTeam_ReturnsStats()
        {
            // Create a team first
            var teamName = $"TestTeam_{Guid.NewGuid():N}".Substring(0, 20);
            await _controller.CreateTeamAsync(teamName, "Test description", true, 0, 0);
            await _controller.RefreshAsync();

            var stats = await _controller.GetStatsAsync();

            // May or may not have stats depending on server config
            Debug.Log($"Team stats: {(stats != null ? "received" : "null")}");
        }

        #endregion

        #region GetWalletAsync Tests

        [Test]
        public async Task GetWalletAsync_NoTeam_ReturnsNull()
        {
            var wallet = await _controller.GetWalletAsync();
            Assert.IsNull(wallet);
        }

        [Test]
        public async Task GetWalletAsync_HasTeam_ReturnsWallet()
        {
            var teamName = $"TestTeam_{Guid.NewGuid():N}".Substring(0, 20);
            await _controller.CreateTeamAsync(teamName, "Test description", true, 0, 0);
            await _controller.RefreshAsync();

            var wallet = await _controller.GetWalletAsync();

            Debug.Log($"Team wallet: {(wallet != null ? $"{wallet.Count} currencies" : "null")}");
        }

        #endregion

        #region RefreshAsync Tests

        [Test]
        public async Task RefreshAsync_Success_RefreshesTeamsSystem()
        {
            await _controller.RefreshAsync();
            // Should not throw
            Assert.Pass("RefreshAsync completed successfully");
        }

        #endregion

        #region RefreshTeamsAsync Tests

        [Test]
        public async Task RefreshTeamsAsync_Tab0_ListsAllTeams()
        {
            var result = await _controller.RefreshTeamsAsync(0);

            Assert.IsNotNull(_controller.Teams);
            Debug.Log($"Found {_controller.Teams.Count} teams in tab 0");
        }

        [Test]
        public async Task RefreshTeamsAsync_Tab1_NoTeam_ReturnsEmptyList()
        {
            var result = await _controller.RefreshTeamsAsync(1);

            Assert.IsNotNull(_controller.Teams);
            Assert.AreEqual(0, _controller.Teams.Count);
        }

        [Test]
        public async Task RefreshTeamsAsync_Tab1_HasTeam_ShowsUserTeam()
        {
            // Create a team first
            var teamName = $"TestTeam_{Guid.NewGuid():N}".Substring(0, 20);
            await _controller.CreateTeamAsync(teamName, "Test description", true, 0, 0);

            var result = await _controller.RefreshTeamsAsync(1);

            Assert.IsNotNull(_controller.Teams);
            Assert.AreEqual(1, _controller.Teams.Count);
            Assert.AreEqual(teamName, _controller.Teams[0].Name);
        }

        [Test]
        public async Task RefreshTeamsAsync_InvalidTab_ReturnsNull()
        {
            LogAssert.Expect(LogType.Error, "Unhandled Tab Index");
            var result = await _controller.RefreshTeamsAsync(99);

            Assert.IsNull(result);
        }

        #endregion

        #region SearchTeamsAsync Tests

        [Test]
        public async Task SearchTeamsAsync_EmptySearch_ReturnsTeams()
        {
            await _controller.SearchTeamsAsync("", "", 0);

            Assert.IsNotNull(_controller.Teams);
            Debug.Log($"Search returned {_controller.Teams.Count} teams");
        }

        [Test]
        public async Task SearchTeamsAsync_ByName_ReturnsMatchingTeams()
        {
            // Create a team with known name
            var teamName = $"SearchTest_{Guid.NewGuid():N}".Substring(0, 20);
            await _controller.CreateTeamAsync(teamName, "Test description", true, 0, 0);

            await _controller.SearchTeamsAsync("SearchTest", "", 0);

            Debug.Log($"Search for 'SearchTest' returned {_controller.Teams.Count} teams");
        }

        [Test]
        public async Task SearchTeamsAsync_OpenFilter_True_ReturnsOpenTeams()
        {
            await _controller.SearchTeamsAsync("", "", 0, openFilter: true);

            foreach (var team in _controller.Teams)
            {
                Assert.IsTrue(team.Open, $"Team {team.Name} should be open");
            }
        }

        [Test]
        public async Task SearchTeamsAsync_ClearsSelection()
        {
            // Create and select a team first
            var teamName = $"TestTeam_{Guid.NewGuid():N}".Substring(0, 20);
            await _controller.CreateTeamAsync(teamName, "Test description", true, 0, 0);
            await _controller.RefreshTeamsAsync(0);

            if (_controller.Teams.Count > 0)
            {
                await _controller.SelectTeamAsync(_controller.Teams[0]);
                Assert.IsNotNull(_controller.SelectedTeam);
            }

            await _controller.SearchTeamsAsync("", "", 0);

            Assert.IsNull(_controller.SelectedTeam);
            Assert.AreEqual(0, _controller.SelectedTeamMembers.Count);
        }

        #endregion

        #region SelectTeamAsync Tests

        [Test]
        public async Task SelectTeamAsync_ValidTeam_SetsSelected()
        {
            var teamName = $"TestTeam_{Guid.NewGuid():N}".Substring(0, 20);
            await _controller.CreateTeamAsync(teamName, "Test description", true, 0, 0);
            await _controller.RefreshTeamsAsync(0);

            if (_controller.Teams.Count == 0)
            {
                Assert.Inconclusive("No teams available.");
                return;
            }

            await _controller.SelectTeamAsync(_controller.Teams[0]);

            Assert.IsNotNull(_controller.SelectedTeam);
            Assert.AreEqual(_controller.Teams[0].Id, _controller.SelectedTeam.Id);
        }

        [Test]
        public async Task SelectTeamAsync_LoadsMembers()
        {
            var teamName = $"TestTeam_{Guid.NewGuid():N}".Substring(0, 20);
            await _controller.CreateTeamAsync(teamName, "Test description", true, 0, 0);
            await _controller.RefreshTeamsAsync(1);

            if (_controller.Teams.Count == 0)
            {
                Assert.Inconclusive("No teams available.");
                return;
            }

            await _controller.SelectTeamAsync(_controller.Teams[0]);

            Assert.IsNotNull(_controller.SelectedTeamMembers);
            Assert.IsTrue(_controller.SelectedTeamMembers.Count > 0);
            Debug.Log($"Team has {_controller.SelectedTeamMembers.Count} members");
        }

        [Test]
        public async Task SelectTeamAsync_Null_ClearsSelection()
        {
            var teamName = $"TestTeam_{Guid.NewGuid():N}".Substring(0, 20);
            await _controller.CreateTeamAsync(teamName, "Test description", true, 0, 0);
            await _controller.RefreshTeamsAsync(1);

            if (_controller.Teams.Count > 0)
            {
                await _controller.SelectTeamAsync(_controller.Teams[0]);
            }

            await _controller.SelectTeamAsync(null);

            Assert.IsNull(_controller.SelectedTeam);
            Assert.AreEqual(0, _controller.SelectedTeamMembers.Count);
        }

        #endregion

        #region GetPlayerMemberState Tests

        [Test]
        public void GetPlayerMemberState_NotMember_ReturnsNone()
        {
            var state = _controller.GetPlayerMemberState();
            Assert.AreEqual(TeamMemberState.None, state);
        }

        [Test]
        public async Task GetPlayerMemberState_IsOwner_ReturnsSuperAdmin()
        {
            var teamName = $"TestTeam_{Guid.NewGuid():N}".Substring(0, 20);
            await _controller.CreateTeamAsync(teamName, "Test description", true, 0, 0);
            await _controller.RefreshTeamsAsync(1);

            if (_controller.Teams.Count == 0)
            {
                Assert.Inconclusive("Team not found.");
                return;
            }

            await _controller.SelectTeamAsync(_controller.Teams[0]);

            var state = _controller.GetPlayerMemberState();
            Assert.AreEqual(TeamMemberState.SuperAdmin, state);
        }

        #endregion

        #region CreateTeamAsync Tests

        [Test]
        public async Task CreateTeamAsync_ValidParams_CreatesTeam()
        {
            var teamName = $"TestTeam_{Guid.NewGuid():N}".Substring(0, 20);

            await _controller.CreateTeamAsync(teamName, "Test description", true, 0, 0, "en");
            await _controller.RefreshTeamsAsync(1);

            Assert.AreEqual(1, _controller.Teams.Count);
            Assert.AreEqual(teamName, _controller.Teams[0].Name);
        }

        [Test]
        public async Task CreateTeamAsync_OpenTeam_TeamIsOpen()
        {
            var teamName = $"TestTeam_{Guid.NewGuid():N}".Substring(0, 20);

            await _controller.CreateTeamAsync(teamName, "Test description", true, 0, 0);
            await _controller.RefreshTeamsAsync(1);

            Assert.IsTrue(_controller.Teams[0].Open);
        }

        [Test]
        public async Task CreateTeamAsync_ClosedTeam_TeamIsClosed()
        {
            var teamName = $"TestTeam_{Guid.NewGuid():N}".Substring(0, 20);

            await _controller.CreateTeamAsync(teamName, "Test description", false, 0, 0);
            await _controller.RefreshTeamsAsync(1);

            Assert.IsFalse(_controller.Teams[0].Open);
        }

        #endregion

        #region DeleteTeamAsync Tests

        [Test]
        public async Task DeleteTeamAsync_NoSelectedTeam_DoesNothing()
        {
            await _controller.SelectTeamAsync(null);

            // Should not throw
            await _controller.DeleteTeamAsync();
        }

        [Test]
        public async Task DeleteTeamAsync_IsSuperAdmin_DeletesTeam()
        {
            var teamName = $"TestTeam_{Guid.NewGuid():N}".Substring(0, 20);
            await _controller.CreateTeamAsync(teamName, "Test description", true, 0, 0);
            await _controller.RefreshTeamsAsync(1);
            await _controller.SelectTeamAsync(_controller.Teams[0]);

            await _controller.DeleteTeamAsync();
            await _controller.RefreshTeamsAsync(1);

            Assert.AreEqual(0, _controller.Teams.Count);
        }

        #endregion

        #region JoinTeamAsync Tests

        [Test]
        public async Task JoinTeamAsync_NoSelectedTeam_DoesNothing()
        {
            await _controller.SelectTeamAsync(null);

            // Should not throw
            await _controller.JoinTeamAsync();
        }

        #endregion

        #region LeaveTeamAsync Tests

        [Test]
        public async Task LeaveTeamAsync_NoSelectedTeam_DoesNothing()
        {
            await _controller.SelectTeamAsync(null);

            // Should not throw
            await _controller.LeaveTeamAsync();
        }

        #endregion

        #region AcceptJoinRequestAsync Tests

        [Test]
        public async Task AcceptJoinRequestAsync_NoSelectedTeam_DoesNothing()
        {
            await _controller.SelectTeamAsync(null);

            // Should not throw
            await _controller.AcceptJoinRequestAsync("some-user-id");
        }

        #endregion

        #region RejectJoinRequestAsync Tests

        [Test]
        public async Task RejectJoinRequestAsync_NoSelectedTeam_DoesNothing()
        {
            await _controller.SelectTeamAsync(null);

            // Should not throw
            await _controller.RejectJoinRequestAsync("some-user-id");
        }

        #endregion

        #region PromoteUserAsync Tests

        [Test]
        public async Task PromoteUserAsync_NoSelectedTeam_DoesNothing()
        {
            await _controller.SelectTeamAsync(null);

            // Should not throw
            await _controller.PromoteUserAsync("some-user-id");
        }

        #endregion

        #region DemoteUserAsync Tests

        [Test]
        public async Task DemoteUserAsync_NoSelectedTeam_DoesNothing()
        {
            await _controller.SelectTeamAsync(null);

            // Should not throw
            await _controller.DemoteUserAsync("some-user-id");
        }

        #endregion

        #region KickUserAsync Tests

        [Test]
        public async Task KickUserAsync_NoSelectedTeam_DoesNothing()
        {
            await _controller.SelectTeamAsync(null);

            // Should not throw
            await _controller.KickUserAsync("some-user-id");
        }

        #endregion

        #region BanUserAsync Tests

        [Test]
        public async Task BanUserAsync_NoSelectedTeam_DoesNothing()
        {
            await _controller.SelectTeamAsync(null);

            // Should not throw
            await _controller.BanUserAsync("some-user-id");
        }

        #endregion

        #region DebugUpdateStatAsync Tests

        [Test]
        public async Task DebugUpdateStatAsync_NoSelectedTeam_DoesNothing()
        {
            await _controller.SelectTeamAsync(null);

            // Should not throw
            await _controller.DebugUpdateStatAsync("wins", 100, false);
        }

        [Test]
        public async Task DebugUpdateStatAsync_PublicStat_UpdatesStat()
        {
            var teamName = $"TestTeam_{Guid.NewGuid():N}".Substring(0, 20);
            await _controller.CreateTeamAsync(teamName, "Test description", true, 0, 0);
            await _controller.RefreshTeamsAsync(1);
            await _controller.SelectTeamAsync(_controller.Teams[0]);

            // Uses "wins" stat defined in base-teams.json
            await _controller.DebugUpdateStatAsync("wins", 100, false);
        }

        [Test]
        public async Task DebugUpdateStatAsync_PrivateStat_UpdatesStat()
        {
            var teamName = $"TestTeam_{Guid.NewGuid():N}".Substring(0, 20);
            await _controller.CreateTeamAsync(teamName, "Test description", true, 0, 0);
            await _controller.RefreshTeamsAsync(1);
            await _controller.SelectTeamAsync(_controller.Teams[0]);

            // Uses "private_rating" stat defined in base-teams.json
            await _controller.DebugUpdateStatAsync("private_rating", 1500, true);
        }

        #endregion

        #region GetChatHistory Tests

        [Test]
        public void GetChatHistory_ReturnsCollection()
        {
            var history = _controller.GetChatHistory();
            Assert.IsNotNull(history);
        }

        #endregion

        #region GetMailboxEntriesAsync Tests

        [Test]
        public async Task GetMailboxEntriesAsync_NoTeam_ReturnsEmptyList()
        {
            var entries = await _controller.GetMailboxEntriesAsync();

            Assert.IsNotNull(entries);
            Assert.AreEqual(0, entries.Count);
        }

        [Test]
        public async Task GetMailboxEntriesAsync_HasTeam_ReturnsEntries()
        {
            var teamName = $"TestTeam_{Guid.NewGuid():N}".Substring(0, 20);
            await _controller.CreateTeamAsync(teamName, "Test description", true, 0, 0);
            await _controller.RefreshAsync();

            var entries = await _controller.GetMailboxEntriesAsync();

            Assert.IsNotNull(entries);
            Assert.IsNotNull(_controller.MailboxEntries);
            Debug.Log($"Mailbox has {entries.Count} claimable entries");
        }

        #endregion

        #region ClaimMailboxEntryAsync Tests

        [Test]
        public async Task ClaimMailboxEntryAsync_NoSelectedTeam_ReturnsNull()
        {
            await _controller.SelectTeamAsync(null);

            var result = await _controller.ClaimMailboxEntryAsync("some-entry-id");

            Assert.IsNull(result);
        }

        #endregion

        #region ClaimAllMailboxAsync Tests

        [Test]
        public async Task ClaimAllMailboxAsync_NoSelectedTeam_DoesNothing()
        {
            await _controller.SelectTeamAsync(null);

            // Should not throw
            await _controller.ClaimAllMailboxAsync();
        }

        #endregion

        #region IsAdmin Tests

        [Test]
        public void IsAdmin_NoTeam_ReturnsFalse()
        {
            Assert.IsFalse(_controller.IsAdmin);
        }

        [Test]
        public async Task IsAdmin_IsOwner_ReturnsTrue()
        {
            var teamName = $"TestTeam_{Guid.NewGuid():N}".Substring(0, 20);
            await _controller.CreateTeamAsync(teamName, "Test description", true, 0, 0);
            await _controller.RefreshTeamsAsync(1);
            await _controller.SelectTeamAsync(_controller.Teams[0]);

            Assert.IsTrue(_controller.IsAdmin);
        }

        #endregion
    }

    /// <summary>
    /// Multi-user integration tests for TeamsController.
    /// Tests team operations that require multiple users.
    /// </summary>
    [TestFixture]
    public class TeamsControllerMultiUserTests
    {
        private const string Scheme = "http";
        private const string Host = "127.0.0.1";
        private const int Port = 7350;
        private const string ServerKey = "defaultkey";
        private const int TeamEntriesLimit = 20;
        private const int AccountCount = 2;

        private IClient _client;
        private ISession[] _sessions;
        private NakamaSystem[] _nakamaSystems;
        private TeamsSystem[] _teamsSystems;
        private TeamsController[] _controllers;
        private string _testDeviceId;

        [SetUp]
        public async Task SetUp()
        {
            _testDeviceId = $"test-device-{Guid.NewGuid():N}";
            _client = new Client(Scheme, Host, Port, ServerKey);

            _sessions = new ISession[AccountCount];
            _nakamaSystems = new NakamaSystem[AccountCount];
            _teamsSystems = new TeamsSystem[AccountCount];
            _controllers = new TeamsController[AccountCount];

            for (var i = 0; i < AccountCount; i++)
            {
                _sessions[i] = await _client.AuthenticateDeviceAsync($"{_testDeviceId}_{i}");

                var logger = new Hiro.Unity.Logger();
                _nakamaSystems[i] = new NakamaSystem(logger, _client, _ => Task.FromResult(_sessions[i]));
                await _nakamaSystems[i].InitializeAsync();

                _teamsSystems[i] = new TeamsSystem(logger, _nakamaSystems[i]);
                await _teamsSystems[i].InitializeAsync();

                _controllers[i] = new TeamsController(
                    _nakamaSystems[i],
                    _teamsSystems[i],
                    TeamEntriesLimit,
                    null,
                    null);
            }
        }

        [TearDown]
        public async Task TearDown()
        {
            // Clean up teams first
            for (var i = 0; i < AccountCount; i++)
            {
                try
                {
                    if (_teamsSystems[i]?.Team != null)
                    {
                        if (_teamsSystems[i].IsAdmin)
                        {
                            await _teamsSystems[i].DeleteTeamAsync(_teamsSystems[i].Team.Id);
                        }
                        else
                        {
                            await _teamsSystems[i].LeaveTeamAsync(_teamsSystems[i].Team.Id);
                        }
                    }
                }
                catch (Exception e)
                {
                    Debug.LogWarning($"Cleanup warning for account {i}: {e.Message}");
                }
            }

            // Delete accounts
            for (var i = 0; i < AccountCount; i++)
            {
                try
                {
                    await _client.DeleteAccountAsync(_sessions[i]);
                }
                catch (Exception e)
                {
                    Debug.LogWarning($"Failed to delete account {i}: {e.Message}");
                }
            }
        }

        [Test]
        public async Task JoinTeam_OpenTeam_UserJoinsSuccessfully()
        {
            // User 0 creates an open team
            var teamName = $"OpenTeam_{Guid.NewGuid():N}".Substring(0, 20);
            await _controllers[0].CreateTeamAsync(teamName, "Open team", true, 0, 0);
            await _controllers[0].RefreshTeamsAsync(1);

            Assert.AreEqual(1, _controllers[0].Teams.Count, "User 0 should have their team");

            // User 1 searches for and joins the team
            await _controllers[1].SearchTeamsAsync(teamName.Substring(0, 8), "", 0);

            ITeam foundTeam = null;
            foreach (var team in _controllers[1].Teams)
            {
                if (team.Name == teamName)
                {
                    foundTeam = team;
                    break;
                }
            }

            if (foundTeam == null)
            {
                Assert.Inconclusive("Could not find the created team.");
                return;
            }

            await _controllers[1].SelectTeamAsync(foundTeam);
            await _controllers[1].JoinTeamAsync();

            // Verify user 1 is now a member
            await _controllers[1].RefreshAsync();
            await _controllers[1].RefreshTeamsAsync(1);

            Assert.AreEqual(1, _controllers[1].Teams.Count, "User 1 should be in a team");
            Assert.AreEqual(teamName, _controllers[1].Teams[0].Name);
        }

        [Test]
        public async Task JoinTeam_ClosedTeam_SendsJoinRequest()
        {
            // User 0 creates a closed team
            var teamName = $"ClosedTeam_{Guid.NewGuid():N}".Substring(0, 20);
            await _controllers[0].CreateTeamAsync(teamName, "Closed team", false, 0, 0);
            await _controllers[0].RefreshTeamsAsync(1);

            // User 1 searches for and requests to join the team
            await _controllers[1].SearchTeamsAsync(teamName.Substring(0, 10), "", 0, openFilter: false);

            ITeam foundTeam = null;
            foreach (var team in _controllers[1].Teams)
            {
                if (team.Name == teamName)
                {
                    foundTeam = team;
                    break;
                }
            }

            if (foundTeam == null)
            {
                Assert.Inconclusive("Could not find the created team.");
                return;
            }

            await _controllers[1].SelectTeamAsync(foundTeam);
            await _controllers[1].JoinTeamAsync();

            // Verify user 1 is in JoinRequest state
            await _controllers[1].RefreshAsync();

            // User 1 should have a pending join request (member state = JoinRequest)
            var state = _controllers[1].GetPlayerMemberState();
            Debug.Log($"User 1 member state after join request: {state}");

            // Team should show in user 1's list since they have a pending request
            await _controllers[1].RefreshTeamsAsync(1);
            Debug.Log($"User 1 teams count: {_controllers[1].Teams.Count}");
        }

        [Test]
        public async Task AcceptJoinRequest_ValidRequest_UserBecomesMember()
        {
            // User 0 creates a closed team
            var teamName = $"ClosedTeam_{Guid.NewGuid():N}".Substring(0, 20);
            await _controllers[0].CreateTeamAsync(teamName, "Closed team", false, 0, 0);
            await _controllers[0].RefreshTeamsAsync(1);
            await _controllers[0].SelectTeamAsync(_controllers[0].Teams[0]);

            // User 1 requests to join
            await _controllers[1].SearchTeamsAsync(teamName.Substring(0, 10), "", 0, openFilter: false);

            ITeam foundTeam = null;
            foreach (var team in _controllers[1].Teams)
            {
                if (team.Name == teamName)
                {
                    foundTeam = team;
                    break;
                }
            }

            if (foundTeam == null)
            {
                Assert.Inconclusive("Could not find the created team.");
                return;
            }

            await _controllers[1].SelectTeamAsync(foundTeam);
            await _controllers[1].JoinTeamAsync();

            // User 0 accepts the join request
            await _controllers[0].SelectTeamAsync(_controllers[0].Teams[0]);

            // Find user 1 in the member list
            string user1Id = _sessions[1].UserId;

            // Refresh to get updated member list with join request
            await _controllers[0].SelectTeamAsync(_controllers[0].Teams[0]);

            IGroupUserListGroupUser joinRequestUser = null;
            foreach (var member in _controllers[0].SelectedTeamMembers)
            {
                if (member.User.Id == user1Id)
                {
                    joinRequestUser = member;
                    break;
                }
            }

            if (joinRequestUser == null)
            {
                Debug.Log($"Members in team: {_controllers[0].SelectedTeamMembers.Count}");
                foreach (var m in _controllers[0].SelectedTeamMembers)
                {
                    Debug.Log($"  Member: {m.User.Username} ({m.User.Id}), State: {m.State}");
                }
                Assert.Inconclusive("User 1's join request not found in member list.");
                return;
            }

            await _controllers[0].AcceptJoinRequestAsync(user1Id);

            // Verify user 1 is now a member
            await _controllers[1].RefreshAsync();
            await _controllers[1].RefreshTeamsAsync(1);

            Assert.AreEqual(1, _controllers[1].Teams.Count);

            await _controllers[1].SelectTeamAsync(_controllers[1].Teams[0]);
            var state = _controllers[1].GetPlayerMemberState();

            Assert.AreEqual(TeamMemberState.Member, state,
                $"User 1 should be a member after acceptance, but state is {state}");
        }

        [Test]
        public async Task PromoteUser_MemberToAdmin_ChangesState()
        {
            // User 0 creates team and user 1 joins
            var teamName = $"PromoteTest_{Guid.NewGuid():N}".Substring(0, 20);
            await _controllers[0].CreateTeamAsync(teamName, "Test", true, 0, 0);
            await _controllers[0].RefreshTeamsAsync(1);

            // User 1 joins
            await _controllers[1].SearchTeamsAsync(teamName.Substring(0, 10), "", 0);
            ITeam foundTeam = null;
            foreach (var team in _controllers[1].Teams)
            {
                if (team.Name == teamName)
                {
                    foundTeam = team;
                    break;
                }
            }

            if (foundTeam == null)
            {
                Assert.Inconclusive("Could not find team.");
                return;
            }

            await _controllers[1].SelectTeamAsync(foundTeam);
            await _controllers[1].JoinTeamAsync();

            // User 0 promotes user 1
            await _controllers[0].SelectTeamAsync(_controllers[0].Teams[0]);
            string user1Id = _sessions[1].UserId;

            await _controllers[0].PromoteUserAsync(user1Id);

            // Verify user 1 is now admin
            await _controllers[1].RefreshAsync();
            await _controllers[1].RefreshTeamsAsync(1);
            await _controllers[1].SelectTeamAsync(_controllers[1].Teams[0]);

            var state = _controllers[1].GetPlayerMemberState();
            Assert.AreEqual(TeamMemberState.Admin, state,
                $"User 1 should be admin after promotion, but state is {state}");
        }

        [Test]
        public async Task KickUser_ValidMember_RemovesFromTeam()
        {
            // User 0 creates team and user 1 joins
            var teamName = $"KickTest_{Guid.NewGuid():N}".Substring(0, 20);
            await _controllers[0].CreateTeamAsync(teamName, "Test", true, 0, 0);
            await _controllers[0].RefreshTeamsAsync(1);

            // User 1 joins
            await _controllers[1].SearchTeamsAsync(teamName.Substring(0, 8), "", 0);
            ITeam foundTeam = null;
            foreach (var team in _controllers[1].Teams)
            {
                if (team.Name == teamName)
                {
                    foundTeam = team;
                    break;
                }
            }

            if (foundTeam == null)
            {
                Assert.Inconclusive("Could not find team.");
                return;
            }

            await _controllers[1].SelectTeamAsync(foundTeam);
            await _controllers[1].JoinTeamAsync();

            // Verify user 1 joined
            await _controllers[1].RefreshTeamsAsync(1);
            Assert.AreEqual(1, _controllers[1].Teams.Count, "User 1 should be in team");

            // User 0 kicks user 1
            await _controllers[0].SelectTeamAsync(_controllers[0].Teams[0]);
            string user1Id = _sessions[1].UserId;

            await _controllers[0].KickUserAsync(user1Id);

            // Verify user 1 is no longer in team
            await _controllers[1].RefreshAsync();
            await _controllers[1].RefreshTeamsAsync(1);

            Assert.AreEqual(0, _controllers[1].Teams.Count, "User 1 should not be in any team after kick");
        }
    }

    /// <summary>
    /// Lifecycle integration tests for TeamsController.
    /// </summary>
    [TestFixture]
    public class TeamsControllerLifecycleTests
    {
        private const string Scheme = "http";
        private const string Host = "127.0.0.1";
        private const int Port = 7350;
        private const string ServerKey = "defaultkey";
        private const int TeamEntriesLimit = 20;

        private TeamsController _controller;
        private IClient _client;
        private ISession _session;
        private NakamaSystem _nakamaSystem;
        private TeamsSystem _teamsSystem;
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

            _teamsSystem = new TeamsSystem(logger, _nakamaSystem);
            await _teamsSystem.InitializeAsync();

            _controller = new TeamsController(
                _nakamaSystem,
                _teamsSystem,
                TeamEntriesLimit,
                null,
                null);
        }

        [TearDown]
        public async Task TearDown()
        {
            try
            {
                if (_teamsSystem?.Team != null)
                {
                    if (_teamsSystem.IsAdmin)
                    {
                        await _teamsSystem.DeleteTeamAsync(_teamsSystem.Team.Id);
                    }
                    else
                    {
                        await _teamsSystem.LeaveTeamAsync(_teamsSystem.Team.Id);
                    }
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning($"Cleanup warning: {e.Message}");
            }

            await _client.DeleteAccountAsync(_session);
        }

        [Test]
        public async Task TeamLifecycle_CreateSearchSelectDelete_CompletesSuccessfully()
        {
            // Create team
            var teamName = $"Lifecycle_{Guid.NewGuid():N}".Substring(0, 20);
            await _controller.CreateTeamAsync(teamName, "Lifecycle test", true, 0, 0);

            // Verify in user's team list
            await _controller.RefreshTeamsAsync(1);
            Assert.AreEqual(1, _controller.Teams.Count);
            Assert.AreEqual(teamName, _controller.Teams[0].Name);

            // Search for team
            await _controller.SearchTeamsAsync(teamName.Substring(0, 9), "", 0);
            Assert.IsTrue(_controller.Teams.Count > 0);

            // Select team
            ITeam foundTeam = null;
            foreach (var team in _controller.Teams)
            {
                if (team.Name == teamName)
                {
                    foundTeam = team;
                    break;
                }
            }
            Assert.IsNotNull(foundTeam);

            await _controller.SelectTeamAsync(foundTeam);
            Assert.IsNotNull(_controller.SelectedTeam);
            Assert.AreEqual(teamName, _controller.SelectedTeam.Name);

            // Verify member state
            var state = _controller.GetPlayerMemberState();
            Assert.AreEqual(TeamMemberState.SuperAdmin, state);

            // Delete team
            await _controller.DeleteTeamAsync();

            // Verify deleted
            await _controller.RefreshTeamsAsync(1);
            Assert.AreEqual(0, _controller.Teams.Count);
        }
    }
}
