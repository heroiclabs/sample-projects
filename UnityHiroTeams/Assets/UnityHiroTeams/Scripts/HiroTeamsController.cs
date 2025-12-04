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
using Hiro.System;
using Hiro.Unity;
using Nakama;
using UnityEngine;
using UnityEngine.UIElements;

namespace HiroTeams
{
    public enum TeamUserState
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
        public int IconIndex;
        public int BackgroundIndex;
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
        private VisualTreeAsset teamUserTemplate;
        [field: SerializeField]
        public Texture2D[] AvatarIcons { get; private set; }
        [field: SerializeField]
        public Texture2D[] AvatarBackgrounds { get; private set; }

        public event Action<ISession, HiroTeamsController> OnInitialized;

        private Button allTab;
        private Button myTeamTab;
        private Button createButton;
        private Button deleteButton;
        private Button joinButton;
        private Button leaveButton;
        private VisualElement selectedTeamAvatarIcon;
        private VisualElement selectedTeamAvatarBackground;
        private VisualElement selectedTeamPanel;
        private Label selectedTeamNameLabel;
        private Label selectedTeamDescriptionLabel;
        private ListView teamUsersList;
        private ListView teamsList;
        private ScrollView teamsScrollView;
        private ScrollView teamUsersScrollView;

        private VisualElement createModal;
        private TextField modalNameField;
        private TextField modalDescriptionField;
        private IntegerField modalMaxCountField;
        private Toggle modalOpenToggle;
        private VisualElement modalAvatarBackground;
        private VisualElement modalAvatarIcon;
        private VisualElement modalPreviousBackgroundButton;
        private VisualElement modalNextBackgroundButton;
        private VisualElement modalPreviousIconButton;
        private VisualElement modalNextIconButton;
        private Button modalCreateButton;
        private Button modalCloseButton;

        private VisualElement errorPopup;
        private Button errorCloseButton;
        private Label errorMessage;

        private int selectedTabIndex;
        private int selectedAvatarBackgroundIndex;
        private int selectedAvatarIconIndex;
        private string selectedTeamId;
        private ITeam selectedTeam;
        private readonly List<ITeam> teams = new();
        private readonly List<IGroupUserListGroupUser> selectedTeamUsers = new();

        private TeamsSystem teamsSystem;
        private NakamaSystem nakamaSystem;

        #region Initialization

        private void Start()
        {
            InitializeUI();
            var coordinator = HiroCoordinator.Instance as HiroTeamsCoordinator;
            if (coordinator == null)
            {
                Debug.LogError("HiroTeamsCoordinator not found!");
                return;
            }
            coordinator.ReceivedStartError += e =>
            {
                Debug.LogException(e);
                errorPopup.style.display = DisplayStyle.Flex;
                errorMessage.text = e.Message;
            };
            coordinator.ReceivedStartSuccess += session =>
            {
                teamsSystem = HiroCoordinator.Instance.GetSystem<TeamsSystem>();
                nakamaSystem = HiroCoordinator.Instance.GetSystem<NakamaSystem>();
                OnInitialized?.Invoke(session, this);
                _ = InitializeTeamsAndRefresh();
            };
        }

        private async Task InitializeTeamsAndRefresh()
        {
            try
            {
                // Refresh the teams system to load user's current team state
                await teamsSystem.RefreshAsync();
            }
            catch (Exception e)
            {
                Debug.LogException(e);
            }

            _ = UpdateTeams();
        }

        public void SwitchComplete(ISession newSession)
        {
            // For use with the account switcher editor tool.
            (nakamaSystem.Session as Session)?.Update(newSession.AuthToken, newSession.RefreshToken);
            _ = InitializeTeamsAndRefresh();
        }

        #endregion

        #region UI Binding

        private void InitializeUI()
        {
            var rootElement = GetComponent<UIDocument>().rootVisualElement;

            allTab = rootElement.Q<Button>("all-tab");
            allTab.RegisterCallback<ClickEvent>(evt =>
            {
                if (selectedTabIndex == 0) return;
                selectedTabIndex = 0;
                allTab.AddToClassList("selected");
                myTeamTab.RemoveFromClassList("selected");
                _ = UpdateTeams();
            });

            myTeamTab = rootElement.Q<Button>("my-team-tab");
            myTeamTab.RegisterCallback<ClickEvent>(evt =>
            {
                if (selectedTabIndex == 1) return;
                selectedTabIndex = 1;
                myTeamTab.AddToClassList("selected");
                allTab.RemoveFromClassList("selected");
                _ = UpdateTeams();
            });

            createButton = rootElement.Q<Button>("team-create");
            createButton.RegisterCallback<ClickEvent>(_ =>
            {
                selectedAvatarBackgroundIndex = 0;
                selectedAvatarIconIndex = 0;
                modalNameField.value = string.Empty;
                modalDescriptionField.value = string.Empty;
                modalMaxCountField.value = 30;
                modalOpenToggle.value = true;
                UpdateCreateModal();
                createModal.style.display = DisplayStyle.Flex;
            });

            deleteButton = rootElement.Q<Button>("team-delete");
            deleteButton.RegisterCallback<ClickEvent>(evt => _ = TeamDelete());

            joinButton = rootElement.Q<Button>("team-join");
            joinButton.RegisterCallback<ClickEvent>(evt => _ = TeamJoin());

            leaveButton = rootElement.Q<Button>("team-leave");
            leaveButton.RegisterCallback<ClickEvent>(evt => _ = TeamLeave());

            selectedTeamPanel = rootElement.Q<VisualElement>("selected-team-panel");
            selectedTeamAvatarIcon = rootElement.Q<VisualElement>("selected-team-avatar-icon");
            selectedTeamAvatarBackground = rootElement.Q<VisualElement>("selected-team-avatar-background");
            selectedTeamNameLabel = rootElement.Q<Label>("selected-team-name");
            selectedTeamDescriptionLabel = rootElement.Q<Label>("selected-team-description");

            teamUsersList = rootElement.Q<ListView>("team-users-list");
            teamUsersList.makeItem = () =>
            {
                var newListEntry = teamUserTemplate.Instantiate();
                var newListEntryLogic = new TeamUserView();
                newListEntry.userData = newListEntryLogic;
                newListEntryLogic.SetVisualElement(newListEntry, this);
                return newListEntry;
            };
            teamUsersList.bindItem = (item, index) =>
            {
                var viewerUser = GetViewerUser();
                var viewerState = viewerUser != null ? (TeamUserState)viewerUser.State : TeamUserState.None;
                (item.userData as TeamUserView)?.SetTeamUser(viewerState, selectedTeamUsers[index]);
            };
            teamUsersList.itemsSource = selectedTeamUsers;

            teamUsersScrollView = teamUsersList.Q<ScrollView>();
            teamUsersScrollView.verticalScrollerVisibility = ScrollerVisibility.AlwaysVisible;

            teamsList = rootElement.Q<ListView>("teams-list");
            teamsList.makeItem = () =>
            {
                var newListEntry = teamEntryTemplate.Instantiate();
                var newListEntryLogic = new TeamView();
                newListEntry.userData = newListEntryLogic;
                newListEntryLogic.SetVisualElement(this, newListEntry);
                return newListEntry;
            };
            teamsList.bindItem = (item, index) => { (item.userData as TeamView)?.SetTeam(teams[index]); };
            teamsList.itemsSource = teams;
            teamsList.selectionChanged += objects =>
            {
                if (teamsList.selectedItem is ITeam)
                {
                    _ = OnTeamSelected(teamsList.selectedItem as ITeam);
                }
            };

            teamsScrollView = teamsList.Q<ScrollView>();
            teamsScrollView.verticalScrollerVisibility = ScrollerVisibility.AlwaysVisible;

            createModal = rootElement.Q<VisualElement>("create-modal");
            modalNameField = rootElement.Q<TextField>("create-modal-name");
            modalDescriptionField = rootElement.Q<TextField>("create-modal-description");
            modalMaxCountField = rootElement.Q<IntegerField>("create-modal-max-count");
            modalOpenToggle = rootElement.Q<Toggle>("create-modal-open");
            modalAvatarBackground = rootElement.Q<VisualElement>("create-modal-avatar-background");
            modalAvatarIcon = rootElement.Q<VisualElement>("create-modal-avatar-icon");
            modalPreviousBackgroundButton = rootElement.Q<VisualElement>("create-modal-previous-background");
            modalPreviousBackgroundButton.RegisterCallback<ClickEvent>(_ =>
            {
                selectedAvatarBackgroundIndex--;
                if (selectedAvatarBackgroundIndex < 0)
                {
                    selectedAvatarBackgroundIndex = AvatarBackgrounds.Length - 1;
                }
                UpdateCreateModal();
            });
            modalNextBackgroundButton = rootElement.Q<VisualElement>("create-modal-next-background");
            modalNextBackgroundButton.RegisterCallback<ClickEvent>(_ =>
            {
                selectedAvatarBackgroundIndex++;
                if (selectedAvatarBackgroundIndex == AvatarBackgrounds.Length)
                {
                    selectedAvatarBackgroundIndex = 0;
                }
                UpdateCreateModal();
            });
            modalPreviousIconButton = rootElement.Q<VisualElement>("create-modal-previous-icon");
            modalPreviousIconButton.RegisterCallback<ClickEvent>(_ =>
            {
                selectedAvatarIconIndex--;
                if (selectedAvatarIconIndex < 0)
                {
                    selectedAvatarIconIndex = AvatarIcons.Length - 1;
                }
                UpdateCreateModal();
            });
            modalNextIconButton = rootElement.Q<VisualElement>("create-modal-next-icon");
            modalNextIconButton.RegisterCallback<ClickEvent>(_ =>
            {
                selectedAvatarIconIndex++;
                if (selectedAvatarIconIndex == AvatarIcons.Length)
                {
                    selectedAvatarIconIndex = 0;
                }
                UpdateCreateModal();
            });
            modalCreateButton = rootElement.Q<Button>("create-modal-create");
            modalCreateButton.RegisterCallback<ClickEvent>(evt => _ = TeamCreate());
            modalCloseButton = rootElement.Q<Button>("create-modal-close");
            modalCloseButton.RegisterCallback<ClickEvent>(_ => createModal.style.display = DisplayStyle.None);

            errorPopup = rootElement.Q<VisualElement>("error-popup");
            errorMessage = rootElement.Q<Label>("error-message");
            errorCloseButton = rootElement.Q<Button>("error-close");
            errorCloseButton.RegisterCallback<ClickEvent>(_ => errorPopup.style.display = DisplayStyle.None);

            _ = OnTeamSelected(null);
        }

        private IGroupUserListGroupUser GetViewerUser()
        {
            var session = nakamaSystem.Session;
            foreach (var user in selectedTeamUsers)
            {
                if (user.User.Id == session.UserId)
                {
                    return user;
                }
            }
            return null;
        }

        private async Task OnTeamSelected(ITeam team)
        {
            if (team == null)
            {
                selectedTeamId = string.Empty;
                selectedTeamPanel.style.display = DisplayStyle.None;
                return;
            }

            selectedTeam = team;
            selectedTeamId = selectedTeam.Id;

            // Parse avatar data
            try
            {
                var avatarData = JsonUtility.FromJson<AvatarData>(selectedTeam.AvatarUrl);
                if (avatarData.IconIndex >= 0 && avatarData.IconIndex < AvatarIcons.Length)
                {
                    selectedTeamAvatarIcon.style.backgroundImage = AvatarIcons[avatarData.IconIndex];
                }
                if (avatarData.BackgroundIndex >= 0 && avatarData.BackgroundIndex < AvatarBackgrounds.Length)
                {
                    selectedTeamAvatarBackground.style.backgroundImage = AvatarBackgrounds[avatarData.BackgroundIndex];
                }
            }
            catch
            {
                // Avatar URL might not be valid JSON, use defaults
            }

            selectedTeamNameLabel.text = selectedTeam.Name;
            selectedTeamDescriptionLabel.text = selectedTeam.Description ?? "No description set.";

            // Get team members using TeamsSystem
            try
            {
                var teamMembers = await teamsSystem.GetTeamMembersAsync(selectedTeam.Id);
                selectedTeamUsers.Clear();
                selectedTeamUsers.AddRange(teamMembers.GroupUsers);
                teamUsersList.RefreshItems();
            }
            catch (Exception e)
            {
                Debug.LogException(e);
                errorPopup.style.display = DisplayStyle.Flex;
                errorMessage.text = e.Message;
                return;
            }

            var viewerUser = GetViewerUser();

            // Show Join button if team is not full and user is not a member
            if (selectedTeam.EdgeCount < selectedTeam.MaxCount && viewerUser == null)
            {
                joinButton.style.display = DisplayStyle.Flex;
            }
            else
            {
                joinButton.style.display = DisplayStyle.None;
            }

            // Show Leave and Delete buttons based on membership
            if (viewerUser != null)
            {
                leaveButton.style.display = DisplayStyle.Flex;
                deleteButton.style.display = viewerUser.State == (int)TeamUserState.SuperAdmin
                    ? DisplayStyle.Flex
                    : DisplayStyle.None;
            }
            else
            {
                leaveButton.style.display = DisplayStyle.None;
                deleteButton.style.display = DisplayStyle.None;
            }

            selectedTeamPanel.style.display = DisplayStyle.Flex;
        }

        #endregion

        #region Teams

        private async Task UpdateTeams()
        {
            teams.Clear();

            try
            {
                switch (selectedTabIndex)
                {
                    case 0:
                        // List all Teams
                        var teamList = await teamsSystem.ListTeamsAsync(location: "", limit: teamEntriesLimit);
                        foreach (var team in teamList.Teams)
                        {
                            teams.Add(team);
                        }
                        break;
                    case 1:
                        // Show user's current team if they have one
                        await teamsSystem.RefreshAsync();
                        if (teamsSystem.Team != null)
                        {
                            teams.Add(teamsSystem.Team);
                        }
                        break;
                    default:
                        Debug.LogError("Unhandled Tab Index");
                        return;
                }
            }
            catch (Exception e)
            {
                errorPopup.style.display = DisplayStyle.Flex;
                errorMessage.text = e.Message;
                return;
            }

            teamsList.RefreshItems();
            teamsList.ClearSelection();

            // If we have a Team selected, try to select it again if it still exists
            foreach (var team in teams)
            {
                if (team.Id != selectedTeamId) continue;
                _ = OnTeamSelected(team);
                teamsList.SetSelection(teams.IndexOf(team));
                return;
            }

            // If we don't find the previously selected Team, hide the selected Team panel
            selectedTeamPanel.style.display = DisplayStyle.None;
        }

        private void UpdateCreateModal()
        {
            // Update the preview for the Team's avatar logo
            if (selectedAvatarBackgroundIndex >= 0 && selectedAvatarBackgroundIndex < AvatarBackgrounds.Length)
            {
                modalAvatarBackground.style.backgroundImage = AvatarBackgrounds[selectedAvatarBackgroundIndex];
            }
            if (selectedAvatarIconIndex >= 0 && selectedAvatarIconIndex < AvatarIcons.Length)
            {
                modalAvatarIcon.style.backgroundImage = AvatarIcons[selectedAvatarIconIndex];
            }
        }

        private async Task TeamCreate()
        {
            try
            {
                // Take the selected avatar icon and background, and convert it into a JSON object
                var avatarDataJson = JsonUtility.ToJson(new AvatarData
                {
                    BackgroundIndex = selectedAvatarBackgroundIndex,
                    IconIndex = selectedAvatarIconIndex
                });

                // Use TeamsSystem to create a new team
                // Parameters: name, desc, open, avatar, langTag, setupMetadata
                await teamsSystem.CreateTeamAsync(
                    modalNameField.value,
                    modalDescriptionField.value,
                    modalOpenToggle.value,
                    avatarDataJson,
                    "en",
                    "{}"
                );
            }
            catch (Exception e)
            {
                errorPopup.style.display = DisplayStyle.Flex;
                errorMessage.text = e.Message;
                return;
            }

            // After successfully creating the Team, hide the create modal and update the Teams list
            createModal.style.display = DisplayStyle.None;
            _ = UpdateTeams();
        }

        private async Task TeamDelete()
        {
            if (selectedTeam == null) return;

            try
            {
                // Attempt to delete the selected Team
                await teamsSystem.DeleteTeamAsync(selectedTeam.Id);
                teamsList.ClearSelection();
            }
            catch (Exception e)
            {
                errorPopup.style.display = DisplayStyle.Flex;
                errorMessage.text = e.Message;
                return;
            }

            // After successfully deleting the Team, update the Teams list
            _ = UpdateTeams();
        }

        private async Task TeamJoin()
        {
            if (selectedTeam == null) return;

            try
            {
                // Attempt to join the selected Team
                await teamsSystem.JoinTeamAsync(selectedTeam.Id);
            }
            catch (Exception e)
            {
                errorPopup.style.display = DisplayStyle.Flex;
                errorMessage.text = e.Message;
                return;
            }

            // After successfully joining the Team, update the Teams list
            _ = UpdateTeams();
        }

        private async Task TeamLeave()
        {
            if (selectedTeam == null) return;

            try
            {
                // Attempt to leave the selected Team
                await teamsSystem.LeaveTeamAsync(selectedTeam.Id);
                teamsList.ClearSelection();
            }
            catch (Exception e)
            {
                errorPopup.style.display = DisplayStyle.Flex;
                errorMessage.text = e.Message;
                return;
            }

            // After successfully leaving, update the Teams list
            _ = UpdateTeams();
        }

        public async Task TeamAccept(string userId)
        {
            if (selectedTeam == null) return;

            try
            {
                // Attempt to accept the selected user's join request
                await teamsSystem.ApproveJoinRequestAsync(selectedTeam.Id, userId);
            }
            catch (Exception e)
            {
                errorPopup.style.display = DisplayStyle.Flex;
                errorMessage.text = e.Message;
                return;
            }

            // After successfully accepting the user, update the Teams list
            _ = UpdateTeams();
        }

        public async Task TeamReject(string userId)
        {
            if (selectedTeam == null) return;

            try
            {
                // Attempt to reject the selected user's join request
                await teamsSystem.RejectJoinRequestAsync(selectedTeam.Id, userId);
            }
            catch (Exception e)
            {
                errorPopup.style.display = DisplayStyle.Flex;
                errorMessage.text = e.Message;
                return;
            }

            // After successfully rejecting the user, update the Teams list
            _ = UpdateTeams();
        }

        public async Task TeamPromote(string userId)
        {
            if (selectedTeam == null) return;

            try
            {
                // Attempt to promote the selected user
                await teamsSystem.PromoteUsersAsync(selectedTeam.Id, new[] { userId });
            }
            catch (Exception e)
            {
                errorPopup.style.display = DisplayStyle.Flex;
                errorMessage.text = e.Message;
                return;
            }

            // After successfully promoting the user, update the Teams list
            _ = UpdateTeams();
        }

        public async Task TeamDemote(string userId)
        {
            if (selectedTeam == null) return;

            try
            {
                // Demote is done via Nakama client API since TeamsSystem doesn't expose it directly
                await nakamaSystem.Client.DemoteGroupUsersAsync(nakamaSystem.Session, selectedTeam.Id, new[] { userId });
            }
            catch (Exception e)
            {
                errorPopup.style.display = DisplayStyle.Flex;
                errorMessage.text = e.Message;
                return;
            }

            // After successfully demoting the user, update the Teams list
            _ = UpdateTeams();
        }

        public async Task TeamKick(string userId)
        {
            if (selectedTeam == null) return;

            try
            {
                // Kick is done via Nakama client API since TeamsSystem doesn't expose it directly
                await nakamaSystem.Client.KickGroupUsersAsync(nakamaSystem.Session, selectedTeam.Id, new[] { userId });
            }
            catch (Exception e)
            {
                errorPopup.style.display = DisplayStyle.Flex;
                errorMessage.text = e.Message;
                return;
            }

            // After successfully kicking the user, update the Teams list
            _ = UpdateTeams();
        }

        public async Task TeamBan(string userId)
        {
            if (selectedTeam == null) return;

            try
            {
                // Ban is done via Nakama client API since TeamsSystem doesn't expose it directly
                await nakamaSystem.Client.BanGroupUsersAsync(nakamaSystem.Session, selectedTeam.Id, new[] { userId });
            }
            catch (Exception e)
            {
                errorPopup.style.display = DisplayStyle.Flex;
                errorMessage.text = e.Message;
                return;
            }

            // After successfully banning the user, update the Teams list
            _ = UpdateTeams();
        }

        #endregion
    }
}
