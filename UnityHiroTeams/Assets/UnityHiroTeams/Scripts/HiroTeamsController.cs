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
using UnityEngine;
using UnityEngine.UIElements;

namespace HiroTeams
{
    public enum TeamMemberState
    {
        None = -1,
        SuperAdmin = 0,
        Admin = 1,
        Member = 2,
        JoinRequest = 3,
        Banned = 4
    }

    [Serializable]
    public struct AvatarData
    {
        public int iconIndex;
        public int backgroundIndex;
    }

    [RequireComponent(typeof(UIDocument))]
    public class HiroTeamsController : MonoBehaviour
    {
        [Header("Team Settings")]
        [SerializeField]
        private int teamEntriesLimit = 100;

        [Header("References")]
        [SerializeField]
        private VisualTreeAsset teamEntryTemplate;
        [SerializeField]
        private VisualTreeAsset teamMemberTemplate;
        [field: SerializeField]
        public Texture2D[] AvatarIcons { get; private set; }
        [field: SerializeField]
        public Texture2D[] AvatarBackgrounds { get; private set; }

        private TeamsSystem _teamsSystem;
        private NakamaSystem _nakamaSystem;
        private TeamsView _view;

        private string _selectedTeamId;

        public ITeam SelectedTeam { get; private set; }
        public List<ITeam> Teams { get; } = new();
        public List<IGroupUserListGroupUser> SelectedTeamMembers { get; } = new();
        public List<IUserChannelMessage> TeamMessages { get; } = new();

        public event Action<ISession, HiroTeamsController> OnInitialized;

        #region Initialization

        private void Start()
        {
            var coordinator = HiroCoordinator.Instance as HiroTeamsCoordinator;
            if (coordinator == null)
            {
                Debug.LogError("HiroTeamsCoordinator not found!");
                return;
            }

            coordinator.ReceivedStartError += HandleStartError;
            coordinator.ReceivedStartSuccess += HandleStartSuccess;

            _view = new TeamsView(this, coordinator, teamEntryTemplate, teamMemberTemplate);
        }

        private static void HandleStartError(Exception e)
        {
            Debug.LogException(e);
        }

        private async void HandleStartSuccess(ISession session)
        {
            _teamsSystem = HiroCoordinator.Instance.GetSystem<TeamsSystem>();
            _nakamaSystem = HiroCoordinator.Instance.GetSystem<NakamaSystem>();

            await _view.RefreshTeams();

            OnInitialized?.Invoke(session, this);
        }

        public void SwitchComplete()
        {
            _ = _teamsSystem.RefreshAsync();
            _ = _view.RefreshTeams();
        }

        #endregion

        #region Team List Operations

        public async Task<int?> RefreshTeams(int tabIndex)
        {
            Teams.Clear();

            await _teamsSystem.RefreshAsync();

            switch (tabIndex)
            {
                case 0:
                    // List all Teams
                    var teamList = await _teamsSystem.ListTeamsAsync(location: "", limit: teamEntriesLimit);
                    foreach (var team in teamList.Teams)
                    {
                        Teams.Add(team);
                    }
                    break;
                case 1:
                    // Show user's current team if they have one
                    if (_teamsSystem.Team != null)
                    {
                        Teams.Add(_teamsSystem.Team);
                    }
                    break;
                default:
                    Debug.LogError("Unhandled Tab Index");
                    return null;
            }

            // If we have a Team selected, try to find it in the new list
            for (var i = 0; i < Teams.Count; i++)
            {
                if (Teams[i].Id != _selectedTeamId) continue;

                await SelectTeam(Teams[i]);
                return i;
            }

            // Team not found, clear selection
            SelectedTeam = null;
            _selectedTeamId = string.Empty;
            return null;
        }

        public async Task SelectTeam(ITeam team)
        {
            if (team == null)
            {
                SelectedTeam = null;
                _selectedTeamId = string.Empty;
                SelectedTeamMembers.Clear();
                return;
            }

            SelectedTeam = team;
            _selectedTeamId = team.Id;

            // Get team members
            var teamMembers = await _teamsSystem.GetTeamMembersAsync(team.Id);
            SelectedTeamMembers.Clear();
            SelectedTeamMembers.AddRange(teamMembers.GroupUsers);
        }

        public TeamMemberState GetPlayerMemberState()
        {
            if (_nakamaSystem?.Session == null) return TeamMemberState.None;

            foreach (var member in SelectedTeamMembers)
            {
                if (member.User.Id == _nakamaSystem.Session.UserId)
                {
                    return (TeamMemberState)member.State;
                }
            }
            return TeamMemberState.None;
        }

        #endregion

        #region Team Lifecycle Operations

        public async Task CreateTeam(string teamName, string description, bool isOpen, int backgroundIndex, int iconIndex)
        {
            var avatarDataJson = JsonUtility.ToJson(new AvatarData
            {
                backgroundIndex = backgroundIndex,
                iconIndex = iconIndex
            });

            await _teamsSystem.CreateTeamAsync(
                teamName,
                description,
                isOpen,
                avatarDataJson,
                "en",
                "{}"
            );
        }

        public async Task DeleteTeam()
        {
            if (SelectedTeam == null) return;
            await _teamsSystem.DeleteTeamAsync(SelectedTeam.Id);
        }

        public async Task JoinTeam()
        {
            if (SelectedTeam == null) return;
            await _teamsSystem.JoinTeamAsync(SelectedTeam.Id);
        }

        public async Task LeaveTeam()
        {
            if (SelectedTeam == null) return;
            await _teamsSystem.LeaveTeamAsync(SelectedTeam.Id);
        }

        #endregion

        #region Team Member Operations

        public async Task AcceptJoinRequest(string userId)
        {
            if (SelectedTeam == null) return;
            await _teamsSystem.ApproveJoinRequestAsync(SelectedTeam.Id, userId);
        }

        public async Task RejectJoinRequest(string userId)
        {
            if (SelectedTeam == null) return;
            await _teamsSystem.RejectJoinRequestAsync(SelectedTeam.Id, userId);
        }

        public async Task PromoteUser(string userId)
        {
            if (SelectedTeam == null) return;
            await _teamsSystem.PromoteUsersAsync(SelectedTeam.Id, new[] { userId });
        }

        public async Task DemoteUser(string userId)
        {
            if (SelectedTeam == null) return;
            await _teamsSystem.DemoteUsersAsync(SelectedTeam.Id, new[] { userId });
        }

        public async Task KickUser(string userId)
        {
            if (SelectedTeam == null) return;
            await _teamsSystem.KickUsersAsync(SelectedTeam.Id, new[] { userId });
        }

        public async Task BanUser(string userId)
        {
            if (SelectedTeam == null) return;
            await _nakamaSystem.Client.BanGroupUsersAsync(_nakamaSystem.Session, SelectedTeam.Id, new[] { userId });
        }

        #endregion

        #region Debug Operations

        public async Task DebugGrantCurrency(string currencyId, int amount)
        {
            if (SelectedTeam == null) return;

            var currencies = new Dictionary<string, long>
            {
                { currencyId, amount }
            };
            await _teamsSystem.GrantAsync(currencies);
        }

        public Task DebugUpdateStat(string statKey, int value, bool isPrivate)
        {
            if (SelectedTeam == null) return Task.CompletedTask;

            // Fix UpdateStat API call - commenting out for now
            Debug.Log($"[DEBUG] Update Stat - Key: {statKey}, Value: {value}, Private: {isPrivate}");
            return Task.CompletedTask;
        }

        #endregion

        #region Chat Operations

        public IReadOnlyCollection<IUserChannelMessage> GetChatHistory()
        {
            return _teamsSystem.ChatHistory;
        }

        #endregion
        
        #region Mailbox Operations

        public async Task ClaimAllMailbox()
        {
            if (SelectedTeam == null) return;

            var mailbox = await _teamsSystem.ListMailboxAsync();
            foreach (var entry in mailbox.Entries)
            {
                await _teamsSystem.ClaimMailboxRewardAsync(entry.Id, true);
            }
        }

        #endregion
    }
}
