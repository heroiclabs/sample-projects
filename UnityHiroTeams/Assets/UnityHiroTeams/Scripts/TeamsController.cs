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
using Nakama;
using UnityEngine;

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

    /// <summary>
    /// Controller for the Teams system.
    /// Plain C# class for testability - no MonoBehaviour inheritance.
    /// Handles business logic and coordinates with Hiro systems.
    /// </summary>
    public class TeamsController
    {
        private readonly NakamaSystem _nakamaSystem;
        private readonly TeamsSystem _teamsSystem;
        private readonly int _teamEntriesLimit;

        private string _selectedTeamId;

        public Texture2D[] AvatarIcons { get; }
        public Texture2D[] AvatarBackgrounds { get; }

        public ITeam SelectedTeam { get; private set; }
        public List<ITeam> Teams { get; } = new();
        public List<IGroupUserListGroupUser> SelectedTeamMembers { get; } = new();
        public List<IRewardMailboxEntry> MailboxEntries { get; } = new();

        public bool IsAdmin
        {
            get
            {
                var state = GetPlayerMemberState();
                return state == TeamMemberState.SuperAdmin || state == TeamMemberState.Admin;
            }
        }

        public TeamsController(
            NakamaSystem nakamaSystem,
            TeamsSystem teamsSystem,
            int teamEntriesLimit,
            Texture2D[] avatarIcons,
            Texture2D[] avatarBackgrounds)
        {
            _nakamaSystem = nakamaSystem ?? throw new ArgumentNullException(nameof(nakamaSystem));
            _teamsSystem = teamsSystem ?? throw new ArgumentNullException(nameof(teamsSystem));
            _teamEntriesLimit = teamEntriesLimit;
            AvatarIcons = avatarIcons ?? Array.Empty<Texture2D>();
            AvatarBackgrounds = avatarBackgrounds ?? Array.Empty<Texture2D>();
        }

        #region Stats and Wallet

        public async Task<IStatList> GetStatsAsync()
        {
            if (_teamsSystem?.Team == null) return null;
            return await _teamsSystem.GetStatsAsync();
        }

        public async Task<Dictionary<string, long>> GetWalletAsync()
        {
            if (_teamsSystem?.Team == null) return null;
            return await _teamsSystem.GetWalletAsync();
        }

        #endregion

        #region Refresh Operations

        public async Task RefreshAsync()
        {
            await _teamsSystem.RefreshAsync();
        }

        public async Task SwitchCompleteAsync()
        {
            await _teamsSystem.RefreshAsync();
        }

        public async Task<int?> RefreshTeamsAsync(int tabIndex)
        {
            Teams.Clear();

            await _teamsSystem.RefreshAsync();

            switch (tabIndex)
            {
                case 0:
                    // List all Teams
                    var teamList = await _teamsSystem.ListTeamsAsync(location: "", limit: _teamEntriesLimit);
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

                await SelectTeamAsync(Teams[i]);
                return i;
            }

            // Team not found, clear selection
            SelectedTeam = null;
            _selectedTeamId = string.Empty;
            return null;
        }

        #endregion

        #region Search and Selection

        public async Task SearchTeamsAsync(string teamName, string language, int minActivity, bool? openFilter = null)
        {
            Teams.Clear();

            ITeamList results;

            // SearchTeamsAsync requires a name; use ListTeamsAsync for empty searches
            if (string.IsNullOrEmpty(teamName))
            {
                results = await _teamsSystem.ListTeamsAsync(location: "", limit: _teamEntriesLimit);
            }
            else
            {
                results = await _teamsSystem.SearchTeamsAsync(
                    name: teamName,
                    langTag: language ?? "",
                    limit: _teamEntriesLimit,
                    minActivity: minActivity
                );
            }

            foreach (var team in results.Teams)
            {
                // Apply client-side filters
                if (openFilter.HasValue && team.Open != openFilter.Value)
                    continue;

                // Apply language filter for ListTeamsAsync (which doesn't filter server-side)
                if (!string.IsNullOrEmpty(language) && team.LangTag != language)
                    continue;

                Teams.Add(team);
            }

            SelectedTeam = null;
            _selectedTeamId = string.Empty;
            SelectedTeamMembers.Clear();
        }

        public async Task SelectTeamAsync(ITeam team)
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

        public async Task CreateTeamAsync(string teamName, string description, bool isOpen, int backgroundIndex, int iconIndex, string language = "en")
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
                language,
                "{}"
            );
        }

        public async Task DeleteTeamAsync()
        {
            if (SelectedTeam == null) return;
            await _teamsSystem.DeleteTeamAsync(SelectedTeam.Id);
        }

        public async Task JoinTeamAsync()
        {
            if (SelectedTeam == null) return;
            await _teamsSystem.JoinTeamAsync(SelectedTeam.Id);
        }

        public async Task LeaveTeamAsync()
        {
            if (SelectedTeam == null) return;
            await _teamsSystem.LeaveTeamAsync(SelectedTeam.Id);
        }

        #endregion

        #region Team Member Operations

        public async Task AcceptJoinRequestAsync(string userId)
        {
            if (SelectedTeam == null) return;
            await _teamsSystem.ApproveJoinRequestAsync(SelectedTeam.Id, userId);
        }

        public async Task RejectJoinRequestAsync(string userId)
        {
            if (SelectedTeam == null) return;
            await _teamsSystem.RejectJoinRequestAsync(SelectedTeam.Id, userId);
        }

        public async Task PromoteUserAsync(string userId)
        {
            if (SelectedTeam == null) return;
            await _teamsSystem.PromoteUsersAsync(SelectedTeam.Id, new[] { userId });
        }

        public async Task DemoteUserAsync(string userId)
        {
            if (SelectedTeam == null) return;
            await _teamsSystem.DemoteUsersAsync(SelectedTeam.Id, new[] { userId });
        }

        public async Task KickUserAsync(string userId)
        {
            if (SelectedTeam == null) return;
            await _teamsSystem.KickUsersAsync(SelectedTeam.Id, new[] { userId });
        }

        public async Task BanUserAsync(string userId)
        {
            if (SelectedTeam == null) return;
            await _nakamaSystem.Client.BanGroupUsersAsync(_nakamaSystem.Session, SelectedTeam.Id, new[] { userId });
        }

        #endregion

        #region Debug Operations

        public async Task DebugUpdateStatAsync(string statKey, int value, bool isPrivate)
        {
            if (SelectedTeam == null) return;

            var statUpdate = new List<UpdateStat>
            {
                new(statKey, value, StatUpdateOperator.Set)
            };

            if (isPrivate)
            {
                await _teamsSystem.UpdateStatsAsync(privateStats: statUpdate, publicStats: null);
            }
            else
            {
                await _teamsSystem.UpdateStatsAsync(privateStats: null, publicStats: statUpdate);
            }
        }

        #endregion

        #region Chat Operations

        public IReadOnlyCollection<IUserChannelMessage> GetChatHistory()
        {
            return _teamsSystem.ChatHistory;
        }

        #endregion

        #region Mailbox Operations

        public async Task<List<IRewardMailboxEntry>> GetMailboxEntriesAsync()
        {
            MailboxEntries.Clear();

            if (_teamsSystem?.Team == null) return MailboxEntries;

            var mailbox = await _teamsSystem.ListMailboxAsync();
            foreach (var entry in mailbox.Entries)
            {
                if (entry.CanClaim)
                {
                    MailboxEntries.Add(entry);
                }
            }

            return MailboxEntries;
        }

        public async Task<IRewardMailboxEntry> ClaimMailboxEntryAsync(string entryId)
        {
            if (SelectedTeam == null) return null;

            var result = await _teamsSystem.ClaimMailboxRewardAsync(entryId, true);
            return result;
        }

        public async Task ClaimAllMailboxAsync()
        {
            if (SelectedTeam == null) return;

            var mailbox = await _teamsSystem.ListMailboxAsync();
            foreach (var entry in mailbox.Entries)
            {
                if (entry.CanClaim)
                {
                    await _teamsSystem.ClaimMailboxRewardAsync(entry.Id, true);
                }
            }
        }

        #endregion
    }
}
