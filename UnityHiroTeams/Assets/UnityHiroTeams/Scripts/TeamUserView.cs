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

using System.Threading.Tasks;
using Hiro;
using Hiro.System;
using Hiro.Unity;
using Nakama;
using UnityEngine.UIElements;

namespace HiroTeams
{
    public class TeamUserView
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
        private HiroTeamsController _controller;

        public void SetVisualElement(VisualElement visualElement, HiroTeamsController controller)
        {
            _controller = controller;

            _usernameLabel = visualElement.Q<Label>("username");
            _roleLabel = visualElement.Q<Label>("role");

            _acceptButton = visualElement.Q<Button>("accept");
            _acceptButton.RegisterCallback<ClickEvent>(evt => _ = AcceptUser());

            _declineButton = visualElement.Q<Button>("decline");
            _declineButton.RegisterCallback<ClickEvent>(evt => _ = RejectUser());

            _promoteButton = visualElement.Q<Button>("promote");
            _promoteButton.RegisterCallback<ClickEvent>(evt => _ = PromoteUser());

            _demoteButton = visualElement.Q<Button>("demote");
            _demoteButton.RegisterCallback<ClickEvent>(evt => _ = DemoteUser());

            _kickButton = visualElement.Q<Button>("kick");
            _kickButton.RegisterCallback<ClickEvent>(evt => _ = KickUser());

            _banButton = visualElement.Q<Button>("ban");
            _banButton.RegisterCallback<ClickEvent>(evt => _ = BanUser());
        }

        public void SetTeamUser(TeamUserState viewerState, IGroupUserListGroupUser teamUser)
        {
            var userState = (TeamUserState)teamUser.State;

            _userId = teamUser.User.Id;
            _usernameLabel.text = teamUser.User.Username;
            _roleLabel.text = userState.ToString();

            // Get current user session to check if this is self
            var nakamaSystem = HiroCoordinator.Instance.GetSystem<NakamaSystem>();
            var session = nakamaSystem.Session;

            // Hide all buttons if user is self
            if (session.UserId == teamUser.User.Id)
            {
                _acceptButton.style.display = DisplayStyle.None;
                _declineButton.style.display = DisplayStyle.None;
                _promoteButton.style.display = DisplayStyle.None;
                _demoteButton.style.display = DisplayStyle.None;
                _kickButton.style.display = DisplayStyle.None;
                _banButton.style.display = DisplayStyle.None;
                return;
            }

            switch (viewerState)
            {
                // We don't have permissions to manage the team
                case TeamUserState.None:
                case TeamUserState.JoinRequest:
                case TeamUserState.Member:
                    _acceptButton.style.display = DisplayStyle.None;
                    _declineButton.style.display = DisplayStyle.None;
                    _promoteButton.style.display = DisplayStyle.None;
                    _demoteButton.style.display = DisplayStyle.None;
                    _kickButton.style.display = DisplayStyle.None;
                    _banButton.style.display = DisplayStyle.None;
                    break;

                // We can manage non-ADMIN or non-SUPERADMIN users, including accepting join requests
                case TeamUserState.Admin:
                    _acceptButton.style.display =
                        userState == TeamUserState.JoinRequest ? DisplayStyle.Flex : DisplayStyle.None;
                    _declineButton.style.display =
                        userState == TeamUserState.JoinRequest ? DisplayStyle.Flex : DisplayStyle.None;
                    _promoteButton.style.display =
                        userState == TeamUserState.Member ? DisplayStyle.Flex : DisplayStyle.None;
                    _demoteButton.style.display =
                        userState == TeamUserState.Admin ? DisplayStyle.Flex : DisplayStyle.None;
                    _kickButton.style.display =
                        userState == TeamUserState.Member ? DisplayStyle.Flex : DisplayStyle.None;
                    _banButton.style.display = userState is TeamUserState.JoinRequest or TeamUserState.Member
                        ? DisplayStyle.Flex
                        : DisplayStyle.None;
                    break;

                // We have all possible privileges
                case TeamUserState.SuperAdmin:
                    _acceptButton.style.display =
                        userState == TeamUserState.JoinRequest ? DisplayStyle.Flex : DisplayStyle.None;
                    _declineButton.style.display =
                        userState == TeamUserState.JoinRequest ? DisplayStyle.Flex : DisplayStyle.None;
                    _promoteButton.style.display = userState is TeamUserState.Member or TeamUserState.Admin
                        ? DisplayStyle.Flex
                        : DisplayStyle.None;
                    _demoteButton.style.display = userState is TeamUserState.SuperAdmin or TeamUserState.Admin
                        ? DisplayStyle.Flex
                        : DisplayStyle.None;
                    _kickButton.style.display =
                        userState is TeamUserState.Member or TeamUserState.Admin or TeamUserState.SuperAdmin
                            ? DisplayStyle.Flex
                            : DisplayStyle.None;
                    _banButton.style.display =
                        userState is TeamUserState.JoinRequest or TeamUserState.Member or TeamUserState.Admin
                            or TeamUserState.SuperAdmin
                            ? DisplayStyle.Flex
                            : DisplayStyle.None;
                    break;
            }
        }

        private Task AcceptUser() => _controller.AcceptJoinRequest(_userId);
        private Task RejectUser() => _controller.RejectJoinRequest(_userId);
        private Task PromoteUser() => _controller.PromoteUser(_userId);
        private Task DemoteUser() => _controller.DemoteUser(_userId);
        private Task KickUser() => _controller.KickUser(_userId);
        private Task BanUser() => _controller.BanUser(_userId);
    }
}
