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
using Nakama;
using Nakama.TinyJson;
using UnityEngine;
using UnityEngine.UIElements;

namespace HiroTeams
{
    public sealed class TeamsView
    {
        private readonly HiroTeamsController _controller;
        private readonly VisualTreeAsset _teamEntryTemplate;
        private readonly VisualTreeAsset _teamMemberTemplate;
        private readonly VisualTreeAsset _mailboxEntryTemplate;

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
        private Label _headerTeamLevel;
        private Label _headerMemberSlots;

        // Lists
        private ListView _teamMembersList;
        private ListView _teamsList;
        private ListView _chatMessages;
        private ListView _mailboxList;
        private ScrollView _teamsScrollView;
        private ScrollView _teamMembersScrollView;
        private VisualElement _mailboxEmptyState;

        // Create modal
        private VisualElement _createModal;
        private TextField _modalNameField;
        private TextField _modalDescriptionField;
        private Toggle _modalOpenToggle;
        private DropdownField _modalLanguageDropdown;
        private VisualElement _modalAvatarBackground;
        private VisualElement _modalAvatarIcon;
        private VisualElement _modalPreviousBackgroundButton;
        private VisualElement _modalNextBackgroundButton;
        private VisualElement _modalPreviousIconButton;
        private VisualElement _modalNextIconButton;
        private Button _modalCreateButton;
        private Button _modalCloseButton;

        // Search modal
        private VisualElement _searchModal;
        private TextField _searchNameField;
        private DropdownField _searchLanguageDropdown;
        private DropdownField _searchOpenFilterDropdown;
        private IntegerField _searchMinActivityField;
        private Button _searchModalSearchButton;
        private Button _searchModalClearButton;
        private Button _searchModalCloseButton;
        private Button _searchButton; // Icon button to open modal

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
        private Label _previewLanguageValue;
        private Label _previewAccessValue;
        private Label _previewWinsValue;
        private Label _previewPointsValue;
        private VisualElement _previewPendingMessage;

        // About tab elements
        private Label _aboutLanguageValue;
        private Label _aboutAccessValue;
        private VisualElement _aboutStatsList;
        private VisualElement _aboutWalletList;

        // Action buttons (visibility toggled per tab)
        private Button _btnDebug;
        private Button _btnPromote;
        private Button _btnDemote;
        private Button _btnKick;
        private Button _btnBan;
        private Button _btnClaimAll;

        // Debug modal elements
        private VisualElement _debugModal;
        private Button _debugModalClose;
        private Label _debugLevelValue;
        private Label _debugWinsValue;
        private Label _debugPointsValue;
        private Button _debugLevelInc;
        private Button _debugLevelDec;
        private Button _debugWinsInc;
        private Button _debugWinsDec;
        private Button _debugPointsInc;
        private Button _debugPointsDec;

        // Stat limits for debug modal
        private const int MaxLevel = 20;
        private const int MaxWins = 100;
        private const int MaxPoints = 1000;
        private const int MinStat = 0;
        private const int MinLevel = 1;

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
            VisualTreeAsset teamMemberTemplate,
            VisualTreeAsset mailboxEntryTemplate)
        {
            _controller = controller;
            _teamEntryTemplate = teamEntryTemplate;
            _teamMemberTemplate = teamMemberTemplate;
            _mailboxEntryTemplate = mailboxEntryTemplate;

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
            InitializeSearch(rootElement);
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

        private void InitializeSearch(VisualElement rootElement)
        {
            // Search icon button to open modal
            _searchButton = rootElement.Q<Button>("team-search");
            _searchButton.RegisterCallback<ClickEvent>(_ => ShowSearchModal());

            // Search modal elements
            _searchModal = rootElement.Q<VisualElement>("search-modal");
            _searchNameField = rootElement.Q<TextField>("search-name");
            _searchLanguageDropdown = rootElement.Q<DropdownField>("search-language");
            _searchOpenFilterDropdown = rootElement.Q<DropdownField>("search-open-filter");
            _searchMinActivityField = rootElement.Q<IntegerField>("search-min-activity");

            _searchModalSearchButton = rootElement.Q<Button>("search-modal-search");
            _searchModalSearchButton.RegisterCallback<ClickEvent>(evt => _ = SearchTeams());

            _searchModalClearButton = rootElement.Q<Button>("search-modal-clear");
            _searchModalClearButton.RegisterCallback<ClickEvent>(_ => ClearSearch());

            _searchModalCloseButton = rootElement.Q<Button>("search-modal-close");
            _searchModalCloseButton.RegisterCallback<ClickEvent>(_ => HideSearchModal());
        }

        private void InitializeSelectedTeamPanel(VisualElement rootElement)
        {
            _selectedTeamPanel = rootElement.Q<VisualElement>("selected-team-panel");
            _selectedTeamAvatarIcon = rootElement.Q<VisualElement>("selected-team-avatar-icon");
            _selectedTeamAvatarBackground = rootElement.Q<VisualElement>("selected-team-avatar-background");
            _selectedTeamNameLabel = rootElement.Q<Label>("selected-team-name");
            _selectedTeamDescriptionLabel = rootElement.Q<Label>("selected-team-description");
            _headerTeamLevel = rootElement.Q<Label>("header-team-level");
            _headerMemberSlots = rootElement.Q<Label>("header-member-slots");
        }

        private void InitializeLists(VisualElement rootElement)
        {
            // Team users list
            _teamMembersList = rootElement.Q<ListView>("team-members-list");
            _teamMembersList.makeItem = () =>
            {
                var newListEntry = _teamMemberTemplate.Instantiate();
                var newListEntryLogic = new TeamMemberView();
                newListEntry.userData = newListEntryLogic;
                newListEntryLogic.SetVisualElement(newListEntry, _controller, this);
                return newListEntry;
            };
            _teamMembersList.bindItem = (item, index) =>
            {
                var viewerState = _controller.GetPlayerMemberState();
                (item.userData as TeamMemberView)?.SetTeamMember(viewerState, _controller.SelectedTeamMembers[index]);
            };
            _teamMembersList.itemsSource = _controller.SelectedTeamMembers;

            _teamMembersScrollView = _teamMembersList.Q<ScrollView>();
            _teamMembersScrollView.verticalScrollerVisibility = ScrollerVisibility.AlwaysVisible;

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

            // Chat Messages
            _chatMessages = rootElement.Q<ListView>("chat-messages");
            _chatMessages.makeItem = () =>
            {
                var label = new Label();
                return label;
            };
            _chatMessages.bindItem = (item, index) =>
            {
                if (item.userData is Label data) data.text = "";
            };
            _chatMessages.itemsSource = _controller.TeamMessages;

            // Mailbox entries
            _mailboxEmptyState = rootElement.Q<VisualElement>("mailbox-empty-state");
            _mailboxList = rootElement.Q<ListView>("mailbox-list");
            _mailboxList.makeItem = () =>
            {
                var newListEntry = _mailboxEntryTemplate.Instantiate();
                return newListEntry;
            };
            _mailboxList.bindItem = (item, index) =>
            {
                var entry = _controller.MailboxEntries[index];
                BindMailboxEntry(item, entry);
            };
            _mailboxList.itemsSource = _controller.MailboxEntries;
        }

        private void InitializeCreateModal(VisualElement rootElement)
        {
            _createModal = rootElement.Q<VisualElement>("create-modal");
            _modalNameField = rootElement.Q<TextField>("create-modal-name");
            _modalDescriptionField = rootElement.Q<TextField>("create-modal-description");
            _modalOpenToggle = rootElement.Q<Toggle>("create-modal-open");
            _modalLanguageDropdown = rootElement.Q<DropdownField>("create-modal-language");
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
            _previewLanguageValue = rootElement.Q<Label>("preview-language-value");
            _previewAccessValue = rootElement.Q<Label>("preview-access-value");
            _previewWinsValue = rootElement.Q<Label>("preview-wins-value");
            _previewPointsValue = rootElement.Q<Label>("preview-points-value");
            _previewPendingMessage = rootElement.Q<VisualElement>("preview-pending-message");

            // About tab elements
            _aboutLanguageValue = rootElement.Q<Label>("about-language-value");
            _aboutAccessValue = rootElement.Q<Label>("about-access-value");
            _aboutStatsList = rootElement.Q<VisualElement>("about-stats-list");
            _aboutWalletList = rootElement.Q<VisualElement>("about-wallet-list");
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

            // Value labels
            _debugLevelValue = rootElement.Q<Label>("debug-level-value");
            _debugWinsValue = rootElement.Q<Label>("debug-wins-value");
            _debugPointsValue = rootElement.Q<Label>("debug-points-value");

            // Level buttons
            _debugLevelInc = rootElement.Q<Button>("debug-level-inc");
            _debugLevelInc.RegisterCallback<ClickEvent>(evt => { _ = AdjustStat("level", 1, MinLevel, MaxLevel); });
            _debugLevelDec = rootElement.Q<Button>("debug-level-dec");
            _debugLevelDec.RegisterCallback<ClickEvent>(evt => { _ = AdjustStat("level", -1, MinLevel, MaxLevel); });

            // Wins buttons
            _debugWinsInc = rootElement.Q<Button>("debug-wins-inc");
            _debugWinsInc.RegisterCallback<ClickEvent>(evt => { _ = AdjustStat("wins", 1, MinStat, MaxWins); });
            _debugWinsDec = rootElement.Q<Button>("debug-wins-dec");
            _debugWinsDec.RegisterCallback<ClickEvent>(evt => { _ = AdjustStat("wins", -1, MinStat, MaxWins); });

            // Points buttons (increment by 50 since milestones are at 100, 250, 500)
            _debugPointsInc = rootElement.Q<Button>("debug-points-inc");
            _debugPointsInc.RegisterCallback<ClickEvent>(evt => { _ = AdjustStat("points", 50, MinStat, MaxPoints); });
            _debugPointsDec = rootElement.Q<Button>("debug-points-dec");
            _debugPointsDec.RegisterCallback<ClickEvent>(evt => { _ = AdjustStat("points", -50, MinStat, MaxPoints); });

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

            // Update header stats
            UpdateHeaderStats(team);

            _teamMembersList.RefreshItems();

            // Determine membership state
            var viewerState = _controller.GetPlayerMemberState();
            bool isOwnTeam = viewerState != TeamMemberState.None && viewerState != TeamMemberState.JoinRequest;
            bool isAdmin = viewerState == TeamMemberState.Admin || viewerState == TeamMemberState.SuperAdmin;

            // Show Join button if team is not full and user is not a member
            _joinButton.style.display = team.EdgeCount < team.MaxCount && viewerState == TeamMemberState.None
                ? DisplayStyle.Flex
                : DisplayStyle.None;

            // Show/hide team tabs container - only visible for own team
            _teamTabsContainer.style.display = isOwnTeam ? DisplayStyle.Flex : DisplayStyle.None;

            // Show preview for non-members, tab content for members
            if (isOwnTeam)
            {
                _contentPreview.style.display = DisplayStyle.None;
                ResetToDefaultTab();
                UpdateActionButtonsVisibility();
                _ = RefreshAboutTab();
            }
            else
            {
                // Non-member: show preview, hide all tab contents
                _contentPreview.style.display = DisplayStyle.Flex;
                foreach (var content in _tabContents)
                {
                    content.AddToClassList("heroic-tab-content--hidden");
                }
                PopulatePreview();
                HideAllActionButtons();

                // Show pending message if user has a pending join request
                _previewPendingMessage.style.display = viewerState == TeamMemberState.JoinRequest
                    ? DisplayStyle.Flex
                    : DisplayStyle.None;
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
                    _ = RefreshAboutTab();
                    break;
            }
        }

        private async Task RefreshAboutTab()
        {
            PopulateAboutTeamInfo();
            await PopulateStatsList();
            await PopulateWalletList();
        }

        private void PopulateAboutTeamInfo()
        {
            var team = _controller.SelectedTeam;
            if (team == null) return;

            _aboutLanguageValue.text = GetLanguageDisplayName(team.LangTag);
            _aboutAccessValue.text = team.Open ? "Open" : "Invite Only";
        }

        private static string GetLanguageDisplayName(string langTag)
        {
            return langTag switch
            {
                "en" => "English",
                "fr" => "French",
                "pt" => "Portuguese",
                _ => langTag ?? "Unknown"
            };
        }

        private async Task PopulateStatsList()
        {
            _aboutStatsList.Clear();

            var stats = await _controller.GetStatsAsync();
            if (stats == null) return;

            // Public stats
            foreach (var stat in stats.Public)
            {
                AddDataRow(_aboutStatsList, FormatStatName(stat.Key), stat.Value.Value.ToString());
            }

            // Private stats (visible to all team members, not non-members)
            foreach (var stat in stats.Private)
            {
                AddDataRow(_aboutStatsList, FormatStatName(stat.Key), stat.Value.Value.ToString());
            }
        }

        private async Task PopulateWalletList()
        {
            _aboutWalletList.Clear();

            var wallet = await _controller.GetWalletAsync();
            if (wallet == null) return;

            foreach (var currency in wallet)
            {
                AddDataRow(_aboutWalletList, FormatCurrencyName(currency.Key), currency.Value.ToString("N0"));
            }
        }

        private void AddDataRow(VisualElement container, string label, string value)
        {
            var row = new VisualElement();
            row.AddToClassList("heroic-data-row");

            var labelElement = new Label(label);
            labelElement.AddToClassList("heroic-data-row__label");
            row.Add(labelElement);

            var valueElement = new Label(value);
            valueElement.AddToClassList("heroic-data-row__value");
            row.Add(valueElement);

            container.Add(row);
        }

        private string FormatStatName(string key)
        {
            return key switch
            {
                "wins" => "Wins",
                "level" => "Level",
                "points" => "Points",
                "private_rating" => "Private Rating",
                _ => key
            };
        }

        private string FormatCurrencyName(string key)
        {
            return key switch
            {
                "team_coins" => "Team Coins",
                "team_gems" => "Team Gems",
                "raid_tokens" => "Raid Tokens",
                _ => key
            };
        }

        private void RefreshGiftsTab()
        {
            // TODO: Implement gifts display
        }

        private void RefreshChatTab()
        {
            // TODO: Implement chat refresh - load team chat messages
            _chatMessages.RefreshItems();
        }

        private async void RefreshMailboxTab()
        {
            try
            {
                await _controller.GetMailboxEntriesAsync();
                _mailboxList.RefreshItems();

                // Show/hide empty state
                bool hasEntries = _controller.MailboxEntries.Count > 0;
                _mailboxEmptyState.style.display = hasEntries ? DisplayStyle.None : DisplayStyle.Flex;
                _mailboxList.style.display = hasEntries ? DisplayStyle.Flex : DisplayStyle.None;
            }
            catch (Exception e)
            {
                Debug.LogError($"Failed to refresh mailbox: {e.Message}");
            }
        }

        private void BindMailboxEntry(VisualElement item, IRewardMailboxEntry entry)
        {
            var nameLabel = item.Q<Label>("entry-name");
            var rewardText = item.Q<Label>("entry-reward-text");
            var claimButton = item.Q<Button>("entry-claim");

            // Set the name - use the entry ID or a generic name
            nameLabel.text = "Level Reward";

            // Build reward text from the reward object
            var rewardParts = new List<string>();
            if (entry.Reward?.Currencies != null)
            {
                foreach (var currency in entry.Reward.Currencies)
                {
                    rewardParts.Add($"+{currency.Value} {FormatCurrencyName(currency.Key)}");
                }
            }
            rewardText.text = rewardParts.Count > 0 ? string.Join(", ", rewardParts) : "Reward";

            // Set up claim button
            claimButton.SetEnabled(entry.CanClaim);
            claimButton.clickable = new Clickable(() => _ = ClaimMailboxEntry(entry.Id));
        }

        private async Task ClaimMailboxEntry(string entryId)
        {
            try
            {
                await _controller.ClaimMailboxEntry(entryId);
                RefreshMailboxTab();
                _ = RefreshAboutTab(); // Refresh wallet display
            }
            catch (Exception e)
            {
                ShowError(e.Message);
            }
        }

        #endregion

        #region Stats Display

        private void UpdateHeaderStats(ITeam team)
        {
            // Update member slots display (e.g., "5/30" showing open positions)
            int openPositions = team.MaxCount - team.EdgeCount;
            _headerMemberSlots.text = $"{openPositions}/{team.MaxCount}";

            // Update team level from public stats (default to 1 if not set)
            var level = GetStatValueFromMetadata(team.Metadata, "level");
            _headerTeamLevel.text = (level > 0 ? level : 1).ToString();
        }

        private void PopulatePreview()
        {
            var team = _controller.SelectedTeam;
            if (team == null) return;

            // Team info
            _previewLanguageValue.text = GetLanguageDisplayName(team.LangTag);
            _previewAccessValue.text = team.Open ? "Open" : "Invite Only";

            // Team stats (available from metadata)
            var wins = GetStatValueFromMetadata(team.Metadata, "wins");
            var points = GetStatValueFromMetadata(team.Metadata, "points");
            _previewWinsValue.text = wins.ToString("N0");
            _previewPointsValue.text = points.ToString("N0");
        }

        private static long GetStatValueFromMetadata(string metadata, string statKey)
        {
            if (string.IsNullOrEmpty(metadata)) return 0;

            var data = metadata.FromJson<Dictionary<string, object>>();
            if (data?.TryGetValue("stats", out var statsObj) == true &&
                statsObj is Dictionary<string, object> stats &&
                stats.TryGetValue(statKey, out var statObj) &&
                statObj is Dictionary<string, object> stat &&
                stat.TryGetValue("value", out var valueObj))
            {
                return Convert.ToInt64(valueObj);
            }
            return 0;
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

            var viewerState = _controller.GetPlayerMemberState();
            bool isOwnTeam = viewerState != TeamMemberState.None && viewerState != TeamMemberState.JoinRequest;
            bool isAdmin = viewerState == TeamMemberState.Admin || viewerState == TeamMemberState.SuperAdmin;
            bool isSuperAdmin = viewerState == TeamMemberState.SuperAdmin;

            switch (_selectedTeamTabIndex)
            {
                case 0: // Members - per-row buttons in TeamMemberView handle member actions
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

        #region Search

        private void ShowSearchModal()
        {
            _searchModal.style.display = DisplayStyle.Flex;
        }

        private void HideSearchModal()
        {
            _searchModal.style.display = DisplayStyle.None;
        }

        private async Task SearchTeams()
        {
            try
            {
                var name = _searchNameField.value;
                var minActivity = _searchMinActivityField.value;

                // Validate name length if provided
                if (!string.IsNullOrEmpty(name) && name.Length < 3)
                {
                    ShowError("Team name search requires at least 3 characters.");
                    return;
                }

                // Get language code from dropdown value, null for "Any"
                var languageValue = _searchLanguageDropdown.value;
                string language = languageValue == "Any" ? null : GetLanguageCode(languageValue);

                // Get open/closed filter: null = any, true = open only, false = invite only
                bool? openFilter = _searchOpenFilterDropdown.value switch
                {
                    "Open" => true,
                    "Invite Only" => false,
                    _ => null
                };

                await _controller.SearchTeams(name, language, minActivity, openFilter);
                _teamsList.RefreshItems();
                _teamsList.ClearSelection();
                HideSelectedTeamPanel();
                HideSearchModal();
            }
            catch (Exception e)
            {
                ShowError(e.Message);
            }
        }

        private void ClearSearch()
        {
            _searchNameField.value = string.Empty;
            _searchLanguageDropdown.index = 0;
            _searchOpenFilterDropdown.index = 0;
            _searchMinActivityField.value = 0;
            _ = RefreshTeams();
            HideSearchModal();
        }

        #endregion

        #region Team Actions

        private async Task CreateTeam()
        {
            try
            {
                var language = GetLanguageCode(_modalLanguageDropdown.value);
                await _controller.CreateTeam(
                    _modalNameField.value,
                    _modalDescriptionField.value,
                    _modalOpenToggle.value,
                    _selectedAvatarBackgroundIndex,
                    _selectedAvatarIconIndex,
                    language
                );
                HideCreateModal();
                await RefreshTeams();
            }
            catch (Exception e)
            {
                ShowError(e.Message);
            }
        }

        private static readonly Dictionary<string, string> LanguageCodes = new()
        {
            { "English", "en" },
            { "French", "fr" },
            { "Portuguese", "pt" }
        };

        private static string GetLanguageCode(string displayName)
        {
            return LanguageCodes.GetValueOrDefault(displayName, "en");
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
            _modalOpenToggle.value = true;
            _modalLanguageDropdown.index = 0; // Default to English
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

        private async void ShowDebugModal()
        {
            // Load current stats and display them
            try
            {
                var stats = await _controller.GetStatsAsync();
                if (stats != null)
                {
                    // Get current values from public stats
                    long level = 1, wins = 0, points = 0;
                    if (stats.Public.TryGetValue("level", out var levelStat))
                        level = levelStat.Value;
                    if (stats.Public.TryGetValue("wins", out var winsStat))
                        wins = winsStat.Value;
                    if (stats.Public.TryGetValue("points", out var pointsStat))
                        points = pointsStat.Value;

                    _debugLevelValue.text = level.ToString();
                    _debugWinsValue.text = wins.ToString();
                    _debugPointsValue.text = points.ToString();
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"Failed to load stats for debug modal: {e.Message}");
            }

            _debugModal.style.display = DisplayStyle.Flex;
        }

        private void HideDebugModal()
        {
            _debugModal.style.display = DisplayStyle.None;
        }

        private async Task AdjustStat(string statKey, int delta, int minValue, int maxValue)
        {
            try
            {
                // Get current value
                var stats = await _controller.GetStatsAsync();
                if (stats == null) return;

                long currentValue = statKey == "level" ? 1 : 0;
                if (stats.Public.TryGetValue(statKey, out var stat))
                    currentValue = stat.Value;

                // Calculate new value with limits
                long newValue = Math.Clamp(currentValue + delta, minValue, maxValue);

                // Don't update if value hasn't changed
                if (newValue == currentValue) return;

                // Update the stat
                await _controller.DebugUpdateStat(statKey, (int)newValue, false);

                // Update the display
                switch (statKey)
                {
                    case "level":
                        _debugLevelValue.text = newValue.ToString();
                        break;
                    case "wins":
                        _debugWinsValue.text = newValue.ToString();
                        break;
                    case "points":
                        _debugPointsValue.text = newValue.ToString();
                        break;
                }

                // Refresh the about tab to show updated stats
                _ = RefreshAboutTab();
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
                _ = RefreshAboutTab(); // Refresh wallet display
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
            HideSearchModal();
            HideDebugModal();
        }

        #endregion
    }
}
