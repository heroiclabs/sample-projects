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
using Nakama;
using UnityEngine.UIElements;

namespace HiroTeams
{
    public class TeamMemberView
    {
        private Label _usernameLabel;
        private Label _roleLabel;
        private Button _acceptButton;
        private Button _declineButton;
        private Button _promoteButton;
        private Button _demoteButton;
        private Button _kickButton;
        private Button _banButton;

        private string _userId;
        private TeamsController _controller;
        private NakamaSystem _nakamaSystem;
        private TeamsView _teamsView;

        public void SetVisualElement(VisualElement visualElement, TeamsController controller, NakamaSystem nakamaSystem, TeamsView teamsView)
        {
            _controller = controller;
            _nakamaSystem = nakamaSystem;
            _teamsView = teamsView;

            _usernameLabel = visualElement.Q<Label>("username");
            _roleLabel = visualElement.Q<Label>("role");

            _acceptButton = visualElement.Q<Button>("accept");
            _acceptButton.RegisterCallback<ClickEvent>(evt => _ = AcceptUserAsync());

            _declineButton = visualElement.Q<Button>("decline");
            _declineButton.RegisterCallback<ClickEvent>(evt => _ = RejectUserAsync());

            _promoteButton = visualElement.Q<Button>("promote");
            _promoteButton.RegisterCallback<ClickEvent>(evt => _ = PromoteUserAsync());

            _demoteButton = visualElement.Q<Button>("demote");
            _demoteButton.RegisterCallback<ClickEvent>(evt => _ = DemoteUserAsync());

            _kickButton = visualElement.Q<Button>("kick");
            _kickButton.RegisterCallback<ClickEvent>(evt => _ = KickUserAsync());

            _banButton = visualElement.Q<Button>("ban");
            _banButton.RegisterCallback<ClickEvent>(evt => _ = BanUserAsync());
        }

        public void SetTeamMember(TeamMemberState playerMemberState, IGroupUserListGroupUser teamMember)
        {
            var userState = (TeamMemberState)teamMember.State;

            _userId = teamMember.User.Id;
            _usernameLabel.text = teamMember.User.Username;
            _roleLabel.text = userState.ToString();

            var session = _nakamaSystem.Session;

            // Hide all buttons if member is self
            if (session.UserId == teamMember.User.Id)
            {
                _acceptButton.style.display = DisplayStyle.None;
                _declineButton.style.display = DisplayStyle.None;
                _promoteButton.style.display = DisplayStyle.None;
                _demoteButton.style.display = DisplayStyle.None;
                _kickButton.style.display = DisplayStyle.None;
                _banButton.style.display = DisplayStyle.None;
                return;
            }

            switch (playerMemberState)
            {
                case TeamMemberState.None:
                case TeamMemberState.JoinRequest:
                case TeamMemberState.Member:
                    _acceptButton.style.display = DisplayStyle.None;
                    _declineButton.style.display = DisplayStyle.None;
                    _promoteButton.style.display = DisplayStyle.None;
                    _demoteButton.style.display = DisplayStyle.None;
                    _kickButton.style.display = DisplayStyle.None;
                    _banButton.style.display = DisplayStyle.None;
                    break;

                case TeamMemberState.Admin:
                    _acceptButton.style.display =
                        userState == TeamMemberState.JoinRequest ? DisplayStyle.Flex : DisplayStyle.None;
                    _declineButton.style.display =
                        userState == TeamMemberState.JoinRequest ? DisplayStyle.Flex : DisplayStyle.None;
                    _promoteButton.style.display =
                        userState == TeamMemberState.Member ? DisplayStyle.Flex : DisplayStyle.None;
                    _demoteButton.style.display =
                        userState == TeamMemberState.Admin ? DisplayStyle.Flex : DisplayStyle.None;
                    _kickButton.style.display =
                        userState == TeamMemberState.Member ? DisplayStyle.Flex : DisplayStyle.None;
                    _banButton.style.display = userState is TeamMemberState.JoinRequest or TeamMemberState.Member
                        ? DisplayStyle.Flex
                        : DisplayStyle.None;
                    break;

                case TeamMemberState.SuperAdmin:
                    _acceptButton.style.display =
                        userState == TeamMemberState.JoinRequest ? DisplayStyle.Flex : DisplayStyle.None;
                    _declineButton.style.display =
                        userState == TeamMemberState.JoinRequest ? DisplayStyle.Flex : DisplayStyle.None;
                    _promoteButton.style.display = userState is TeamMemberState.Member or TeamMemberState.Admin
                        ? DisplayStyle.Flex
                        : DisplayStyle.None;
                    _demoteButton.style.display = userState is TeamMemberState.SuperAdmin or TeamMemberState.Admin
                        ? DisplayStyle.Flex
                        : DisplayStyle.None;
                    _kickButton.style.display =
                        userState is TeamMemberState.Member or TeamMemberState.Admin or TeamMemberState.SuperAdmin
                            ? DisplayStyle.Flex
                            : DisplayStyle.None;
                    _banButton.style.display =
                        userState is TeamMemberState.JoinRequest or TeamMemberState.Member or TeamMemberState.Admin
                            or TeamMemberState.SuperAdmin
                            ? DisplayStyle.Flex
                            : DisplayStyle.None;
                    break;
                case TeamMemberState.Banned:
                    break;
            }
        }

        private async Task AcceptUserAsync()
        {
            try
            {
                await _controller.AcceptJoinRequestAsync(_userId);
                await _teamsView.RefreshTeamsAsync();
            }
            catch (Exception e)
            {
                _teamsView.ShowError(e.Message);
            }
        }

        private async Task RejectUserAsync()
        {
            try
            {
                await _controller.RejectJoinRequestAsync(_userId);
                await _teamsView.RefreshTeamsAsync();
            }
            catch (Exception e)
            {
                _teamsView.ShowError(e.Message);
            }
        }

        private async Task PromoteUserAsync()
        {
            try
            {
                await _controller.PromoteUserAsync(_userId);
                await _teamsView.RefreshTeamsAsync();
            }
            catch (Exception e)
            {
                _teamsView.ShowError(e.Message);
            }
        }

        private async Task DemoteUserAsync()
        {
            try
            {
                await _controller.DemoteUserAsync(_userId);
                await _teamsView.RefreshTeamsAsync();
            }
            catch (Exception e)
            {
                _teamsView.ShowError(e.Message);
            }
        }

        private async Task KickUserAsync()
        {
            try
            {
                await _controller.KickUserAsync(_userId);
                await _teamsView.RefreshTeamsAsync();
            }
            catch (Exception e)
            {
                _teamsView.ShowError(e.Message);
            }
        }

        private async Task BanUserAsync()
        {
            try
            {
                await _controller.BanUserAsync(_userId);
                await _teamsView.RefreshTeamsAsync();
            }
            catch (Exception e)
            {
                _teamsView.ShowError(e.Message);
            }
        }
    }
}
