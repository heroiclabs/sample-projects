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
        private Label usernameLabel;
        private Label roleLabel;
        private Button acceptButton;
        private Button declineButton;
        private Button promoteButton;
        private Button demoteButton;
        private Button kickButton;
        private Button banButton;

        private string userId;
        private HiroTeamsController teamsController;

        public void SetVisualElement(VisualElement visualElement, HiroTeamsController controller)
        {
            teamsController = controller;

            usernameLabel = visualElement.Q<Label>("username");
            roleLabel = visualElement.Q<Label>("role");

            acceptButton = visualElement.Q<Button>("accept");
            acceptButton.RegisterCallback<ClickEvent>(evt => _ = AcceptUser());

            declineButton = visualElement.Q<Button>("decline");
            declineButton.RegisterCallback<ClickEvent>(evt => _ = RejectUser());

            promoteButton = visualElement.Q<Button>("promote");
            promoteButton.RegisterCallback<ClickEvent>(evt => _ = PromoteUser());

            demoteButton = visualElement.Q<Button>("demote");
            demoteButton.RegisterCallback<ClickEvent>(evt => _ = DemoteUser());

            kickButton = visualElement.Q<Button>("kick");
            kickButton.RegisterCallback<ClickEvent>(evt => _ = KickUser());

            banButton = visualElement.Q<Button>("ban");
            banButton.RegisterCallback<ClickEvent>(evt => _ = BanUser());
        }

        public void SetTeamUser(TeamUserState viewerState, IGroupUserListGroupUser teamUser)
        {
            var userState = (TeamUserState)teamUser.State;

            userId = teamUser.User.Id;
            usernameLabel.text = teamUser.User.Username;
            roleLabel.text = userState.ToString();

            // Get current user session to check if this is self
            var nakamaSystem = HiroCoordinator.Instance.GetSystem<NakamaSystem>();
            var session = nakamaSystem.Session;

            // Hide all buttons if user is self
            if (session.UserId == teamUser.User.Id)
            {
                acceptButton.style.display = DisplayStyle.None;
                declineButton.style.display = DisplayStyle.None;
                promoteButton.style.display = DisplayStyle.None;
                demoteButton.style.display = DisplayStyle.None;
                kickButton.style.display = DisplayStyle.None;
                banButton.style.display = DisplayStyle.None;
                return;
            }

            switch (viewerState)
            {
                // We don't have permissions to manage the team
                case TeamUserState.None:
                case TeamUserState.JoinRequest:
                case TeamUserState.Member:
                    acceptButton.style.display = DisplayStyle.None;
                    declineButton.style.display = DisplayStyle.None;
                    promoteButton.style.display = DisplayStyle.None;
                    demoteButton.style.display = DisplayStyle.None;
                    kickButton.style.display = DisplayStyle.None;
                    banButton.style.display = DisplayStyle.None;
                    break;

                // We can manage non-ADMIN or non-SUPERADMIN users, including accepting join requests
                case TeamUserState.Admin:
                    acceptButton.style.display =
                        userState == TeamUserState.JoinRequest ? DisplayStyle.Flex : DisplayStyle.None;
                    declineButton.style.display =
                        userState == TeamUserState.JoinRequest ? DisplayStyle.Flex : DisplayStyle.None;
                    promoteButton.style.display =
                        userState == TeamUserState.Member ? DisplayStyle.Flex : DisplayStyle.None;
                    demoteButton.style.display =
                        userState == TeamUserState.Admin ? DisplayStyle.Flex : DisplayStyle.None;
                    kickButton.style.display =
                        userState == TeamUserState.Member ? DisplayStyle.Flex : DisplayStyle.None;
                    banButton.style.display = userState is TeamUserState.JoinRequest or TeamUserState.Member
                        ? DisplayStyle.Flex
                        : DisplayStyle.None;
                    break;

                // We have all possible privileges
                case TeamUserState.SuperAdmin:
                    acceptButton.style.display =
                        userState == TeamUserState.JoinRequest ? DisplayStyle.Flex : DisplayStyle.None;
                    declineButton.style.display =
                        userState == TeamUserState.JoinRequest ? DisplayStyle.Flex : DisplayStyle.None;
                    promoteButton.style.display = userState is TeamUserState.Member or TeamUserState.Admin
                        ? DisplayStyle.Flex
                        : DisplayStyle.None;
                    demoteButton.style.display = userState is TeamUserState.SuperAdmin or TeamUserState.Admin
                        ? DisplayStyle.Flex
                        : DisplayStyle.None;
                    kickButton.style.display =
                        userState is TeamUserState.Member or TeamUserState.Admin or TeamUserState.SuperAdmin
                            ? DisplayStyle.Flex
                            : DisplayStyle.None;
                    banButton.style.display =
                        userState is TeamUserState.JoinRequest or TeamUserState.Member or TeamUserState.Admin
                            or TeamUserState.SuperAdmin
                            ? DisplayStyle.Flex
                            : DisplayStyle.None;
                    break;
            }
        }

        private Task AcceptUser() => teamsController.TeamAccept(userId);
        private Task RejectUser() => teamsController.TeamReject(userId);
        private Task PromoteUser() => teamsController.TeamPromote(userId);
        private Task DemoteUser() => teamsController.TeamDemote(userId);
        private Task KickUser() => teamsController.TeamKick(userId);
        private Task BanUser() => teamsController.TeamBan(userId);
    }
}
