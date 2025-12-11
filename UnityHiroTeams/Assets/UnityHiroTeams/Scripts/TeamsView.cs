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
using Nakama;
using UnityEngine;
using UnityEngine.UIElements;

namespace HiroTeams
{
    public sealed class TeamsView
    {
        private readonly HiroTeamsController _controller;
        private readonly VisualTreeAsset _teamEntryTemplate;
        private readonly VisualTreeAsset _teamUserTemplate;

        // Main tabs
        private Button _allTab;
        private Button _myTeamTab;

        // Action buttons
        private Button _createButton;
        private Button _deleteButton;
        private Button _joinButton;
        private Button _leaveButton;

        // Selected team panel
        private VisualElement _selectedTeamPanel;
        private VisualElement _selectedTeamAvatarIcon;
        private VisualElement _selectedTeamAvatarBackground;
        private Label _selectedTeamNameLabel;
        private Label _selectedTeamDescriptionLabel;

        // Lists
        private ListView _teamUsersList;
        private ListView _teamsList;
        private ScrollView _teamsScrollView;
        private ScrollView _teamUsersScrollView;

        // Create modal
        private VisualElement _createModal;
        private TextField _modalNameField;
        private TextField _modalDescriptionField;
        private IntegerField _modalMaxCountField;
        private Toggle _modalOpenToggle;
        private VisualElement _modalAvatarBackground;
        private VisualElement _modalAvatarIcon;
        private VisualElement _modalPreviousBackgroundButton;
        private VisualElement _modalNextBackgroundButton;
        private VisualElement _modalPreviousIconButton;
        private VisualElement _modalNextIconButton;
        private Button _modalCreateButton;
        private Button _modalCloseButton;

        // Error popup
        private VisualElement _errorPopup;
        private Button _errorCloseButton;
        private Label _errorMessage;

        // Team panel tabs
        private VisualElement _teamTabsContainer;
        private Button _tabAbout;
        private Button _tabMembers;
        private Button _tabGifts;
        private Button _tabChat;
        private Button _tabMailbox;
        private Button[] _teamTabs;

        // Tab content containers
        private VisualElement _contentAbout;
        private VisualElement _contentMembers;
        private VisualElement _contentGifts;
        private VisualElement _contentChat;
        private VisualElement _contentMailbox;
        private VisualElement[] _tabContents;

        // Preview content for non-members
        private VisualElement _contentPreview;
        private VisualElement _previewStats;

        // About tab elements
        private VisualElement _aboutStatsRow;
        private VisualElement _statsPrivateSection;

        // Action buttons (visibility toggled per tab)
        private Button _btnDebug;
        private Button _btnPromote;
        private Button _btnDemote;
        private Button _btnKick;
        private Button _btnBan;
        private Button _btnClaimAll;

        // Debug modal elements
        private VisualElement _debugModal;
        private TextField _debugCurrencyId;
        private IntegerField _debugCurrencyAmount;
        private Button _debugGrantCurrency;
        private TextField _debugStatKey;
        private IntegerField _debugStatValue;
        private Toggle _debugStatPrivate;
        private Button _debugUpdateStat;
        private Button _debugModalClose;

        // View state
        private int _selectedTabIndex;
        private int _selectedTeamTabIndex;
        private int _selectedAvatarBackgroundIndex;
        private int _selectedAvatarIconIndex;

        #region Initialization

        public TeamsView(
            HiroTeamsController controller,
            HiroTeamsCoordinator coordinator,
            VisualTreeAsset teamEntryTemplate,
            VisualTreeAsset teamUserTemplate)
        {
            _controller = controller;
            _teamEntryTemplate = teamEntryTemplate;
            _teamUserTemplate = teamUserTemplate;

            Initialize(controller.GetComponent<UIDocument>().rootVisualElement);
            HideSelectedTeamPanel();

            // Subscribe to events after UI is ready
            controller.OnInitialized += HandleInitialized;
            coordinator.ReceivedStartError += HandleStartError;
        }

        private void Initialize(VisualElement rootElement)
        {
            InitializeTabs(rootElement);
            InitializeButtons(rootElement);
            InitializeSelectedTeamPanel(rootElement);
            InitializeLists(rootElement);
            InitializeCreateModal(rootElement);
            InitializeErrorPopup(rootElement);
            InitializeTeamTabs(rootElement);
            InitializeTeamTabContents(rootElement);
            InitializeActionButtons(rootElement);
            InitializeDebugModal(rootElement);
        }

        private void InitializeTabs(VisualElement rootElement)
        {
            _allTab = rootElement.Q<Button>("all-tab");
            _allTab.RegisterCallback<ClickEvent>(evt =>
            {
                if (_selectedTabIndex == 0) return;
                _selectedTabIndex = 0;
                _allTab.AddToClassList("selected");
                _myTeamTab.RemoveFromClassList("selected");
                _ = RefreshTeams();
            });

            _myTeamTab = rootElement.Q<Button>("my-team-tab");
            _myTeamTab.RegisterCallback<ClickEvent>(evt =>
            {
                if (_selectedTabIndex == 1) return;
                _selectedTabIndex = 1;
                _myTeamTab.AddToClassList("selected");
                _allTab.RemoveFromClassList("selected");
                _ = RefreshTeams();
            });
        }

        private void InitializeButtons(VisualElement rootElement)
        {
            _createButton = rootElement.Q<Button>("team-create");
            _createButton.RegisterCallback<ClickEvent>(_ => ShowCreateModal());

            _deleteButton = rootElement.Q<Button>("team-delete");
            _deleteButton.RegisterCallback<ClickEvent>(evt => _ = DeleteTeam());

            _joinButton = rootElement.Q<Button>("team-join");
            _joinButton.RegisterCallback<ClickEvent>(evt => _ = JoinTeam());

            _leaveButton = rootElement.Q<Button>("team-leave");
            _leaveButton.RegisterCallback<ClickEvent>(evt => _ = LeaveTeam());
        }

        private void InitializeSelectedTeamPanel(VisualElement rootElement)
        {
            _selectedTeamPanel = rootElement.Q<VisualElement>("selected-team-panel");
            _selectedTeamAvatarIcon = rootElement.Q<VisualElement>("selected-team-avatar-icon");
            _selectedTeamAvatarBackground = rootElement.Q<VisualElement>("selected-team-avatar-background");
            _selectedTeamNameLabel = rootElement.Q<Label>("selected-team-name");
            _selectedTeamDescriptionLabel = rootElement.Q<Label>("selected-team-description");
        }

        private void InitializeLists(VisualElement rootElement)
        {
            // Team users list
            _teamUsersList = rootElement.Q<ListView>("team-users-list");
            _teamUsersList.makeItem = () =>
            {
                var newListEntry = _teamUserTemplate.Instantiate();
                var newListEntryLogic = new TeamUserView();
                newListEntry.userData = newListEntryLogic;
                newListEntryLogic.SetVisualElement(newListEntry, _controller, this);
                return newListEntry;
            };
            _teamUsersList.bindItem = (item, index) =>
            {
                var viewerState = _controller.GetViewerState();
                (item.userData as TeamUserView)?.SetTeamUser(viewerState, _controller.SelectedTeamUsers[index]);
            };
            _teamUsersList.itemsSource = _controller.SelectedTeamUsers;

            _teamUsersScrollView = _teamUsersList.Q<ScrollView>();
            _teamUsersScrollView.verticalScrollerVisibility = ScrollerVisibility.AlwaysVisible;

            // Teams list
            _teamsList = rootElement.Q<ListView>("teams-list");
            _teamsList.makeItem = () =>
            {
                var newListEntry = _teamEntryTemplate.Instantiate();
                var newListEntryLogic = new TeamView();
                newListEntry.userData = newListEntryLogic;
                newListEntryLogic.SetVisualElement(_controller, newListEntry);
                return newListEntry;
            };
            _teamsList.bindItem = (item, index) =>
            {
                (item.userData as TeamView)?.SetTeam(_controller.Teams[index]);
            };
            _teamsList.itemsSource = _controller.Teams;
            _teamsList.selectionChanged += objects => _ = SelectTeam();

            _teamsScrollView = _teamsList.Q<ScrollView>();
            _teamsScrollView.verticalScrollerVisibility = ScrollerVisibility.AlwaysVisible;
        }

        private void InitializeCreateModal(VisualElement rootElement)
        {
            _createModal = rootElement.Q<VisualElement>("create-modal");
            _modalNameField = rootElement.Q<TextField>("create-modal-name");
            _modalDescriptionField = rootElement.Q<TextField>("create-modal-description");
            _modalMaxCountField = rootElement.Q<IntegerField>("create-modal-max-count");
            _modalOpenToggle = rootElement.Q<Toggle>("create-modal-open");
            _modalAvatarBackground = rootElement.Q<VisualElement>("create-modal-avatar-background");
            _modalAvatarIcon = rootElement.Q<VisualElement>("create-modal-avatar-icon");

            _modalPreviousBackgroundButton = rootElement.Q<VisualElement>("create-modal-previous-background");
            _modalPreviousBackgroundButton.RegisterCallback<ClickEvent>(_ =>
            {
                _selectedAvatarBackgroundIndex--;
                if (_selectedAvatarBackgroundIndex < 0)
                {
                    _selectedAvatarBackgroundIndex = _controller.AvatarBackgrounds.Length - 1;
                }
                UpdateCreateModalAvatar();
            });

            _modalNextBackgroundButton = rootElement.Q<VisualElement>("create-modal-next-background");
            _modalNextBackgroundButton.RegisterCallback<ClickEvent>(_ =>
            {
                _selectedAvatarBackgroundIndex++;
                if (_selectedAvatarBackgroundIndex == _controller.AvatarBackgrounds.Length)
                {
                    _selectedAvatarBackgroundIndex = 0;
                }
                UpdateCreateModalAvatar();
            });

            _modalPreviousIconButton = rootElement.Q<VisualElement>("create-modal-previous-icon");
            _modalPreviousIconButton.RegisterCallback<ClickEvent>(_ =>
            {
                _selectedAvatarIconIndex--;
                if (_selectedAvatarIconIndex < 0)
                {
                    _selectedAvatarIconIndex = _controller.AvatarIcons.Length - 1;
                }
                UpdateCreateModalAvatar();
            });

            _modalNextIconButton = rootElement.Q<VisualElement>("create-modal-next-icon");
            _modalNextIconButton.RegisterCallback<ClickEvent>(_ =>
            {
                _selectedAvatarIconIndex++;
                if (_selectedAvatarIconIndex == _controller.AvatarIcons.Length)
                {
                    _selectedAvatarIconIndex = 0;
                }
                UpdateCreateModalAvatar();
            });

            _modalCreateButton = rootElement.Q<Button>("create-modal-create");
            _modalCreateButton.RegisterCallback<ClickEvent>(evt => _ = CreateTeam());

            _modalCloseButton = rootElement.Q<Button>("create-modal-close");
            _modalCloseButton.RegisterCallback<ClickEvent>(_ => HideCreateModal());
        }

        private void InitializeErrorPopup(VisualElement rootElement)
        {
            _errorPopup = rootElement.Q<VisualElement>("error-popup");
            _errorMessage = rootElement.Q<Label>("error-message");
            _errorCloseButton = rootElement.Q<Button>("error-close");
            _errorCloseButton.RegisterCallback<ClickEvent>(_ => HideErrorPopup());
        }

        private void InitializeTeamTabs(VisualElement rootElement)
        {
            _teamTabsContainer = rootElement.Q<VisualElement>("team-tabs");
            _teamTabsContainer.style.display = DisplayStyle.None;

            _tabMembers = rootElement.Q<Button>("tab-members");
            _tabGifts = rootElement.Q<Button>("tab-gifts");
            _tabChat = rootElement.Q<Button>("tab-chat");
            _tabMailbox = rootElement.Q<Button>("tab-mailbox");
            _tabAbout = rootElement.Q<Button>("tab-about");
            _teamTabs = new[] { _tabMembers, _tabGifts, _tabChat, _tabMailbox, _tabAbout };

            for (int i = 0; i < _teamTabs.Length; i++)
            {
                int tabIndex = i;
                _teamTabs[i].RegisterCallback<ClickEvent>(_ => SelectTeamTab(tabIndex));
            }
        }

        private void InitializeTeamTabContents(VisualElement rootElement)
        {
            _contentMembers = rootElement.Q<VisualElement>("content-members");
            _contentGifts = rootElement.Q<VisualElement>("content-gifts");
            _contentChat = rootElement.Q<VisualElement>("content-chat");
            _contentMailbox = rootElement.Q<VisualElement>("content-mailbox");
            _contentAbout = rootElement.Q<VisualElement>("content-about");
            _tabContents = new[] { _contentMembers, _contentGifts, _contentChat, _contentMailbox, _contentAbout };

            // Preview content for non-members
            _contentPreview = rootElement.Q<VisualElement>("content-preview");
            _previewStats = rootElement.Q<VisualElement>("preview-stats");

            // About tab elements
            _aboutStatsRow = rootElement.Q<VisualElement>("about-stats-row");
            _statsPrivateSection = rootElement.Q<VisualElement>("stats-private-section");
        }

        private void InitializeActionButtons(VisualElement rootElement)
        {
            _btnDebug = rootElement.Q<Button>("btn-debug");
            _btnDebug.RegisterCallback<ClickEvent>(_ => ShowDebugModal());

            _btnPromote = rootElement.Q<Button>("btn-promote");
            _btnDemote = rootElement.Q<Button>("btn-demote");
            _btnKick = rootElement.Q<Button>("btn-kick");
            _btnBan = rootElement.Q<Button>("btn-ban");

            _btnClaimAll = rootElement.Q<Button>("btn-claim-all");
            _btnClaimAll.RegisterCallback<ClickEvent>(evt => _ = ClaimAllMailbox());
        }

        private void InitializeDebugModal(VisualElement rootElement)
        {
            _debugModal = rootElement.Q<VisualElement>("debug-modal");
            _debugCurrencyId = rootElement.Q<TextField>("debug-currency-id");
            _debugCurrencyAmount = rootElement.Q<IntegerField>("debug-currency-amount");
            _debugGrantCurrency = rootElement.Q<Button>("debug-grant-currency");
            _debugGrantCurrency.RegisterCallback<ClickEvent>(evt => _ = DebugGrantCurrency());

            _debugStatKey = rootElement.Q<TextField>("debug-stat-key");
            _debugStatValue = rootElement.Q<IntegerField>("debug-stat-value");
            _debugStatPrivate = rootElement.Q<Toggle>("debug-stat-private");
            _debugUpdateStat = rootElement.Q<Button>("debug-update-stat");
            _debugUpdateStat.RegisterCallback<ClickEvent>(evt => _ = DebugUpdateStat());

            _debugModalClose = rootElement.Q<Button>("debug-modal-close");
            _debugModalClose.RegisterCallback<ClickEvent>(_ => HideDebugModal());
        }

        private void HandleInitialized(ISession session, HiroTeamsController controller)
        {
            _ = RefreshTeams();
        }

        private void HandleStartError(Exception e)
        {
            ShowError(e.Message);
        }

        #endregion

        #region Teams List Management

        public async Task RefreshTeams()
        {
            HideAllModals();

            try
            {
                var reselectedTeamIndex = await _controller.RefreshTeams(_selectedTabIndex);

                _teamsList.RefreshItems();
                _teamsList.ClearSelection();

                if (reselectedTeamIndex.HasValue)
                {
                    _teamsList.SetSelection(reselectedTeamIndex.Value);
                }
                else
                {
                    HideSelectedTeamPanel();
                }
            }
            catch (Exception e)
            {
                ShowError(e.Message);
            }
        }

        private async Task SelectTeam()
        {
            if (_teamsList.selectedItem is not ITeam team) return;

            try
            {
                await _controller.SelectTeam(team);
                UpdateSelectedTeamPanel();
            }
            catch (Exception e)
            {
                ShowError(e.Message);
            }
        }

        #endregion

        #region Selected Team Panel

        private void UpdateSelectedTeamPanel()
        {
            var team = _controller.SelectedTeam;
            if (team == null)
            {
                HideSelectedTeamPanel();
                return;
            }

            // Update avatar
            try
            {
                var avatarData = JsonUtility.FromJson<AvatarData>(team.AvatarUrl);
                if (avatarData.iconIndex >= 0 && avatarData.iconIndex < _controller.AvatarIcons.Length)
                {
                    _selectedTeamAvatarIcon.style.backgroundImage = _controller.AvatarIcons[avatarData.iconIndex];
                }
                if (avatarData.backgroundIndex >= 0 && avatarData.backgroundIndex < _controller.AvatarBackgrounds.Length)
                {
                    _selectedTeamAvatarBackground.style.backgroundImage = _controller.AvatarBackgrounds[avatarData.backgroundIndex];
                }
            }
            catch
            {
                // Avatar URL might not be valid JSON, use defaults
            }

            _selectedTeamNameLabel.text = team.Name;
            _selectedTeamDescriptionLabel.text = team.Description ?? "No description set.";

            _teamUsersList.RefreshItems();

            // Determine membership state
            var viewerState = _controller.GetViewerState();
            bool isOwnTeam = viewerState != TeamUserState.None && viewerState != TeamUserState.JoinRequest;
            bool isAdmin = viewerState == TeamUserState.Admin || viewerState == TeamUserState.SuperAdmin;

            // Show Join button if team is not full and user is not a member
            _joinButton.style.display = team.EdgeCount < team.MaxCount && viewerState == TeamUserState.None
                ? DisplayStyle.Flex
                : DisplayStyle.None;

            // Show/hide team tabs container - only visible for own team
            _teamTabsContainer.style.display = isOwnTeam ? DisplayStyle.Flex : DisplayStyle.None;

            // Show/hide admin-only elements
            _statsPrivateSection.style.display = isAdmin ? DisplayStyle.Flex : DisplayStyle.None;

            // Show preview for non-members, tab content for members
            if (isOwnTeam)
            {
                _contentPreview.style.display = DisplayStyle.None;
                ResetToDefaultTab();
                UpdateActionButtonsVisibility();
                RefreshAboutTab();
            }
            else
            {
                // Non-member: show preview, hide all tab contents
                _contentPreview.style.display = DisplayStyle.Flex;
                foreach (var content in _tabContents)
                {
                    content.AddToClassList("heroic-tab-content--hidden");
                }
                PopulateStatsRow(_previewStats);
                HideAllActionButtons();
            }

            _selectedTeamPanel.style.display = DisplayStyle.Flex;
        }

        private void HideSelectedTeamPanel()
        {
            _selectedTeamPanel.style.display = DisplayStyle.None;
            _teamTabsContainer.style.display = DisplayStyle.None;
        }

        #endregion

        #region Team Tabs

        private void SelectTeamTab(int tabIndex)
        {
            if (_selectedTeamTabIndex == tabIndex) return;

            // Update tab styling
            _teamTabs[_selectedTeamTabIndex].RemoveFromClassList("selected");
            _teamTabs[tabIndex].AddToClassList("selected");

            // Show/hide content
            _tabContents[_selectedTeamTabIndex].AddToClassList("heroic-tab-content--hidden");
            _tabContents[tabIndex].RemoveFromClassList("heroic-tab-content--hidden");

            _selectedTeamTabIndex = tabIndex;

            UpdateActionButtonsVisibility();
            RefreshTeamTabContent();
        }

        private void ResetToDefaultTab()
        {
            int defaultTab = 0; // Members tab
            if (_selectedTeamTabIndex != defaultTab)
            {
                _teamTabs[_selectedTeamTabIndex].RemoveFromClassList("selected");
                _tabContents[_selectedTeamTabIndex].AddToClassList("heroic-tab-content--hidden");
                _selectedTeamTabIndex = defaultTab;
                _teamTabs[defaultTab].AddToClassList("selected");
                _tabContents[defaultTab].RemoveFromClassList("heroic-tab-content--hidden");
            }
            else
            {
                _tabContents[defaultTab].RemoveFromClassList("heroic-tab-content--hidden");
            }
        }

        private void RefreshTeamTabContent()
        {
            switch (_selectedTeamTabIndex)
            {
                case 1: // Gifts
                    RefreshGiftsTab();
                    break;
                case 2: // Chat
                    RefreshChatTab();
                    break;
                case 3: // Mailbox
                    RefreshMailboxTab();
                    break;
                case 4: // About
                    RefreshAboutTab();
                    break;
            }
        }

        private void RefreshAboutTab()
        {
            PopulateStatsRow(_aboutStatsRow);
            // TODO: Populate private stats for admins
            // TODO: Populate wallet currencies
        }

        private void RefreshGiftsTab()
        {
            // TODO: Implement gifts display
        }

        private void RefreshChatTab()
        {
            // TODO: Implement chat display
        }

        private void RefreshMailboxTab()
        {
            // TODO: Implement mailbox display
        }

        #endregion

        #region Stats Display

        private void PopulateStatsRow(VisualElement container)
        {
            var team = _controller.SelectedTeam;
            if (team == null) return;

            container.Clear();

            AddStatItem(container, "Members", team.EdgeCount.ToString());
            AddStatItem(container, "Wins", "0");
            AddStatItem(container, "Level", "1");
            AddStatItem(container, "Total Score", "0");
        }

        private void AddStatItem(VisualElement container, string label, string value)
        {
            var statItem = new VisualElement();
            statItem.AddToClassList("heroic-stat-item");

            var content = new VisualElement();
            content.AddToClassList("heroic-stat-item__content");

            var labelElement = new Label(label);
            labelElement.AddToClassList("heroic-stat-item__label");
            content.Add(labelElement);

            var valueElement = new Label(value);
            valueElement.AddToClassList("heroic-stat-item__value");
            content.Add(valueElement);

            statItem.Add(content);
            container.Add(statItem);
        }

        #endregion

        #region Action Buttons

        private void HideAllActionButtons()
        {
            _leaveButton.style.display = DisplayStyle.None;
            _deleteButton.style.display = DisplayStyle.None;
            _btnDebug.style.display = DisplayStyle.None;
            _btnPromote.style.display = DisplayStyle.None;
            _btnDemote.style.display = DisplayStyle.None;
            _btnKick.style.display = DisplayStyle.None;
            _btnBan.style.display = DisplayStyle.None;
            _btnClaimAll.style.display = DisplayStyle.None;
        }

        private void UpdateActionButtonsVisibility()
        {
            HideAllActionButtons();

            var viewerState = _controller.GetViewerState();
            bool isOwnTeam = viewerState != TeamUserState.None && viewerState != TeamUserState.JoinRequest;
            bool isAdmin = viewerState == TeamUserState.Admin || viewerState == TeamUserState.SuperAdmin;
            bool isSuperAdmin = viewerState == TeamUserState.SuperAdmin;

            switch (_selectedTeamTabIndex)
            {
                case 0: // Members - per-row buttons in TeamUserView handle member actions
                    break;
                case 3: // Mailbox - show Claim All
                    _btnClaimAll.style.display = DisplayStyle.Flex;
                    break;
                case 4: // About - show Leave, Delete (super admin), Debug (admin)
                    if (isOwnTeam)
                    {
                        _leaveButton.style.display = DisplayStyle.Flex;
                        if (isSuperAdmin)
                        {
                            _deleteButton.style.display = DisplayStyle.Flex;
                        }
                        if (isAdmin)
                        {
                            _btnDebug.style.display = DisplayStyle.Flex;
                        }
                    }
                    break;
            }
        }

        #endregion

        #region Team Actions

        private async Task CreateTeam()
        {
            try
            {
                await _controller.CreateTeam(
                    _modalNameField.value,
                    _modalDescriptionField.value,
                    _modalOpenToggle.value,
                    _selectedAvatarBackgroundIndex,
                    _selectedAvatarIconIndex
                );
                HideCreateModal();
                await RefreshTeams();
            }
            catch (Exception e)
            {
                ShowError(e.Message);
            }
        }

        private async Task DeleteTeam()
        {
            try
            {
                await _controller.DeleteTeam();
                _teamsList.ClearSelection();
                await RefreshTeams();
            }
            catch (Exception e)
            {
                ShowError(e.Message);
            }
        }

        private async Task JoinTeam()
        {
            try
            {
                await _controller.JoinTeam();
                await RefreshTeams();
            }
            catch (Exception e)
            {
                ShowError(e.Message);
            }
        }

        private async Task LeaveTeam()
        {
            try
            {
                await _controller.LeaveTeam();
                _teamsList.ClearSelection();
                await RefreshTeams();
            }
            catch (Exception e)
            {
                ShowError(e.Message);
            }
        }

        #endregion

        #region Create Modal

        private void ShowCreateModal()
        {
            _selectedAvatarBackgroundIndex = 0;
            _selectedAvatarIconIndex = 0;
            _modalNameField.value = string.Empty;
            _modalDescriptionField.value = string.Empty;
            _modalMaxCountField.value = 30;
            _modalOpenToggle.value = true;
            UpdateCreateModalAvatar();
            _createModal.style.display = DisplayStyle.Flex;
        }

        private void HideCreateModal()
        {
            _createModal.style.display = DisplayStyle.None;
        }

        private void UpdateCreateModalAvatar()
        {
            if (_selectedAvatarBackgroundIndex >= 0 && _selectedAvatarBackgroundIndex < _controller.AvatarBackgrounds.Length)
            {
                _modalAvatarBackground.style.backgroundImage = _controller.AvatarBackgrounds[_selectedAvatarBackgroundIndex];
            }
            if (_selectedAvatarIconIndex >= 0 && _selectedAvatarIconIndex < _controller.AvatarIcons.Length)
            {
                _modalAvatarIcon.style.backgroundImage = _controller.AvatarIcons[_selectedAvatarIconIndex];
            }
        }

        #endregion

        #region Debug Modal

        private void ShowDebugModal()
        {
            _debugModal.style.display = DisplayStyle.Flex;
        }

        private void HideDebugModal()
        {
            _debugModal.style.display = DisplayStyle.None;
        }

        private async Task DebugGrantCurrency()
        {
            string currencyId = _debugCurrencyId.value;
            int amount = _debugCurrencyAmount.value;

            if (string.IsNullOrEmpty(currencyId))
            {
                ShowError("Currency ID is required");
                return;
            }

            try
            {
                await _controller.DebugGrantCurrency(currencyId, amount);
                HideDebugModal();
                RefreshAboutTab();
            }
            catch (Exception e)
            {
                ShowError(e.Message);
            }
        }

        private async Task DebugUpdateStat()
        {
            string statKey = _debugStatKey.value;
            int value = _debugStatValue.value;
            bool isPrivate = _debugStatPrivate.value;

            if (string.IsNullOrEmpty(statKey))
            {
                ShowError("Stat key is required");
                return;
            }

            try
            {
                await _controller.DebugUpdateStat(statKey, value, isPrivate);
                HideDebugModal();
                RefreshAboutTab();
            }
            catch (Exception e)
            {
                ShowError(e.Message);
            }
        }

        #endregion

        #region Mailbox

        private async Task ClaimAllMailbox()
        {
            try
            {
                await _controller.ClaimAllMailbox();
                RefreshMailboxTab();
            }
            catch (Exception e)
            {
                ShowError(e.Message);
            }
        }

        #endregion

        #region Error Handling

        public void ShowError(string message)
        {
            if (_errorPopup == null || _errorMessage == null)
            {
                Debug.LogError($"[TeamsView] Error (UI not ready): {message}");
                return;
            }
            _errorMessage.text = message;
            _errorPopup.style.display = DisplayStyle.Flex;
        }

        private void HideErrorPopup()
        {
            _errorPopup.style.display = DisplayStyle.None;
        }

        #endregion

        #region Modal Utilities

        private void HideAllModals()
        {
            HideCreateModal();
            HideDebugModal();
        }

        #endregion
    }
}
