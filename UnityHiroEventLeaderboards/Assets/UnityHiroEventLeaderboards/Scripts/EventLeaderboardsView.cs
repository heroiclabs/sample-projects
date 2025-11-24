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
using UnityEngine;
using UnityEngine.UIElements;
using HeroicUI;
using Nakama;

namespace HiroEventLeaderboards
{
    public sealed class EventLeaderboardsView
    {
        private readonly EventLeaderboardsController _controller;
        private readonly VisualTreeAsset _eventLeaderboardEntryTemplate;
        private readonly VisualTreeAsset _eventLeaderboardRecordTemplate;
        private readonly VisualTreeAsset _eventLeaderboardZoneTemplate;

        private WalletDisplay _walletDisplay;
        private Button _myEventLeaderboardsTab;
        private Button _submitScoreButton;
        private Button _claimRewardsButton;
        private Button _rollButton;
        private VisualElement _devToolsPanel;
        private Button _debugFillButton;
        private Button _debugRandomScoresButton;
        private VisualElement _selectedEventLeaderboardPanel;
        private Label _selectedEventLeaderboardNameLabel;
        private Label _selectedEventLeaderboardDescriptionLabel;
        private Label _selectedEventLeaderboardTierLabel;
        private Label _selectedEventLeaderboardTimeRemainingLabel;
        private Button _eventInfoButton;
        private ListView _eventLeaderboardRecordsList;
        private VisualElement _emptyStateContainer;
        private Label _emptyStateMessage;
        private ListView _eventLeaderboardsList;
        private ScrollView _eventLeaderboardsScrollView;
        private ScrollView _eventLeaderboardRecordsScrollView;
        private Button _refreshButton;

        private VisualElement _submitScoreModal;
        private IntegerField _scoreField;
        private IntegerField _subScoreField;
        private Button _submitScoreModalButton;
        private Button _submitScoreModalCloseButton;

        private VisualElement _debugFillModal;
        private IntegerField _debugFillTargetCountField;
        private Button _debugFillModalButton;
        private Button _debugFillModalCloseButton;

        private VisualElement _debugRandomScoresModal;
        private IntegerField _debugMinScoreField;
        private IntegerField _debugMaxScoreField;
        private EnumField _debugOperatorField;
        private IntegerField _debugSubscoreMinField;
        private IntegerField _debugSubscoreMaxField;
        private Button _debugRandomScoresModalButton;
        private Button _debugRandomScoresModalCloseButton;

        private VisualElement _errorPopup;
        private Button _errorCloseButton;
        private Label _errorMessage;

        private VisualElement _eventInfoModal;
        private Button _eventInfoModalCloseButton;
        private Label _infoIdLabel;
        private Label _infoOperatorLabel;
        private Label _infoResetScheduleLabel;
        private Label _infoCohortSizeLabel;
        private Label _infoAscendingLabel;
        private Label _infoMaxIdleTierDropLabel;
        private Label _infoChangeZonesLabel;
        private Label _infoRewardTiersLabel;

        private IEventLeaderboard _currentEventLeaderboard;
        private int _selectedTabIndex;

        #region Initialization

        public EventLeaderboardsView(EventLeaderboardsController controller, HiroEventLeaderboardsCoordinator coordinator,
            VisualTreeAsset eventLeaderboardEntryTemplate,
            VisualTreeAsset eventLeaderboardRecordTemplate,
            VisualTreeAsset eventLeaderboardZoneTemplate)
        {
            _controller = controller;
            _eventLeaderboardEntryTemplate = eventLeaderboardEntryTemplate;
            _eventLeaderboardRecordTemplate = eventLeaderboardRecordTemplate;
            _eventLeaderboardZoneTemplate = eventLeaderboardZoneTemplate;

            controller.OnInitialized += HandleInitialized;
            coordinator.ReceivedStartError += HandleStartError;

            Initialize(controller.GetComponent<UIDocument>().rootVisualElement);

            HideSelectedEventLeaderboardPanel();
        }

        private void Initialize(VisualElement rootElement)
        {
            _walletDisplay = new WalletDisplay(rootElement.Q<VisualElement>("wallet-display"));

            InitializeTabs(rootElement);
            InitializeButtons(rootElement);
            InitializeDevTools(rootElement);
            InitializeSelectedEventLeaderboardPanel(rootElement);
            InitializeLists(rootElement);
            InitializeModals(rootElement);
            InitializeErrorPopup(rootElement);
        }

        private void InitializeTabs(VisualElement rootElement)
        {
            _myEventLeaderboardsTab = rootElement.Q<Button>("my-event-leaderboards-tab");
            _myEventLeaderboardsTab.RegisterCallback<ClickEvent>(evt =>
            {
                if (_selectedTabIndex == 0) return;
                _selectedTabIndex = 0;
                _myEventLeaderboardsTab.AddToClassList("selected");
                _ = RefreshEventLeaderboards();
            });
        }

        private void InitializeButtons(VisualElement rootElement)
        {
            _submitScoreButton = rootElement.Q<Button>("event-leaderboard-submit-score");
            _submitScoreButton.RegisterCallback<ClickEvent>(_ => ShowSubmitScoreModal());

            _claimRewardsButton = rootElement.Q<Button>("event-leaderboard-claim");
            _claimRewardsButton.RegisterCallback<ClickEvent>(ClaimRewards);

            _rollButton = rootElement.Q<Button>("event-leaderboard-roll");
            _rollButton.RegisterCallback<ClickEvent>(RollEventLeaderboard);

            _refreshButton = rootElement.Q<Button>("event-leaderboards-refresh");
            _refreshButton.RegisterCallback<ClickEvent>(evt => _ = RefreshEventLeaderboards());
        }

        private void InitializeDevTools(VisualElement rootElement)
        {
            _devToolsPanel = rootElement.Q<VisualElement>("dev-tools");

            _debugFillButton = rootElement.Q<Button>("event-leaderboard-debug-fill");
            _debugFillButton?.RegisterCallback<ClickEvent>(_ => ShowDebugFillModal());

            _debugRandomScoresButton = rootElement.Q<Button>("event-leaderboard-debug-random-scores");
            _debugRandomScoresButton?.RegisterCallback<ClickEvent>(_ => ShowDebugRandomScoresModal());
        }

        private void InitializeSelectedEventLeaderboardPanel(VisualElement rootElement)
        {
            _selectedEventLeaderboardPanel = rootElement.Q<VisualElement>("selected-event-leaderboard-panel");
            _selectedEventLeaderboardNameLabel = rootElement.Q<Label>("selected-event-leaderboard-name");
            _selectedEventLeaderboardDescriptionLabel = rootElement.Q<Label>("selected-event-leaderboard-description");
            _selectedEventLeaderboardTierLabel = rootElement.Q<Label>("selected-event-leaderboard-tier");
            _selectedEventLeaderboardTimeRemainingLabel = rootElement.Q<Label>("selected-event-leaderboard-time-remaining");

            _eventInfoButton = rootElement.Q<Button>("event-info-button");
            _eventInfoButton.RegisterCallback<ClickEvent>(_ => ShowEventInfoModal());
        }

        private void InitializeLists(VisualElement rootElement)
        {
            // Event leaderboard records list
            _eventLeaderboardRecordsList = rootElement.Q<ListView>("event-leaderboard-records-list");
            _eventLeaderboardRecordsList.makeItem = () =>
            {
                // Create a container that can hold either a record or a zone indicator
                var container = new VisualElement();
                container.style.flexGrow = 1;
                return container;
            };
            _eventLeaderboardRecordsList.bindItem = (item, index) =>
            {
                item.Clear();
                var displayItem = _controller.DisplayItems[index];

                if (displayItem.Type == LeaderboardDisplayItem.ItemType.PlayerRecord)
                {
                    // Create and bind a player record
                    var recordElement = _eventLeaderboardRecordTemplate.Instantiate();
                    var recordView = new EventLeaderboardRecordView();
                    recordView.SetVisualElement(recordElement);
                    recordView.SetEventLeaderboardRecord(displayItem.PlayerRecord, _currentEventLeaderboard, _controller.CurrentUsername);
                    item.Add(recordElement);
                }
                else if (displayItem.Type == LeaderboardDisplayItem.ItemType.ZoneIndicator)
                {
                    // Create and bind a zone indicator
                    var zoneElement = _eventLeaderboardZoneTemplate.Instantiate();
                    var zoneView = new EventLeaderboardZoneView();
                    zoneView.SetVisualElement(zoneElement);
                    zoneView.SetZone(displayItem.ZoneType);
                    item.Add(zoneElement);
                }
            };
            _eventLeaderboardRecordsList.itemsSource = _controller.DisplayItems;

            _eventLeaderboardRecordsScrollView = _eventLeaderboardRecordsList.Q<ScrollView>();
            _eventLeaderboardRecordsScrollView.verticalScrollerVisibility = ScrollerVisibility.AlwaysVisible;

            // Empty state elements
            _emptyStateContainer = rootElement.Q<VisualElement>("empty-state-container");
            _emptyStateMessage = rootElement.Q<Label>("empty-state-message");

            // Event leaderboards list
            _eventLeaderboardsList = rootElement.Q<ListView>("event-leaderboards-list");
            _eventLeaderboardsList.makeItem = () =>
            {
                var newListEntry = _eventLeaderboardEntryTemplate.Instantiate();
                var newListEntryLogic = new EventLeaderboardView();
                newListEntry.userData = newListEntryLogic;
                newListEntryLogic.SetVisualElement(newListEntry);
                return newListEntry;
            };
            _eventLeaderboardsList.bindItem = (item, index) =>
            {
                (item.userData as EventLeaderboardView)?.SetEventLeaderboard(_controller.EventLeaderboards[index]);
            };
            _eventLeaderboardsList.itemsSource = _controller.EventLeaderboards;
            _eventLeaderboardsList.selectionChanged += objects => _ = SelectEventLeaderboard();

            _eventLeaderboardsScrollView = _eventLeaderboardsList.Q<ScrollView>();
            _eventLeaderboardsScrollView.verticalScrollerVisibility = ScrollerVisibility.Auto;
        }

        private void InitializeModals(VisualElement rootElement)
        {
            InitializeSubmitScoreModal(rootElement);
            InitializeDebugFillModal(rootElement);
            InitializeDebugRandomScoresModal(rootElement);
        }

        private void InitializeSubmitScoreModal(VisualElement rootElement)
        {
            _submitScoreModal = rootElement.Q<VisualElement>("submit-score-modal");
            _scoreField = rootElement.Q<IntegerField>("submit-score-score");
            _subScoreField = rootElement.Q<IntegerField>("submit-score-subscore");

            _submitScoreModalButton = rootElement.Q<Button>("submit-score-modal-submit");
            _submitScoreModalButton.RegisterCallback<ClickEvent>(SubmitScore);

            _submitScoreModalCloseButton = rootElement.Q<Button>("submit-score-modal-close");
            _submitScoreModalCloseButton.RegisterCallback<ClickEvent>(_ => HideSubmitScoreModal());
        }

        private void InitializeDebugFillModal(VisualElement rootElement)
        {
            _debugFillModal = rootElement.Q<VisualElement>("debug-fill-modal");
            if (_debugFillModal == null) return;

            _debugFillTargetCountField = rootElement.Q<IntegerField>("debug-fill-target-count");

            _debugFillModalButton = rootElement.Q<Button>("debug-fill-modal-fill");
            _debugFillModalButton.RegisterCallback<ClickEvent>(evt => _ = DebugFill());

            _debugFillModalCloseButton = rootElement.Q<Button>("debug-fill-modal-close");
            _debugFillModalCloseButton.RegisterCallback<ClickEvent>(_ => HideDebugFillModal());
        }

        private void InitializeDebugRandomScoresModal(VisualElement rootElement)
        {
            _debugRandomScoresModal = rootElement.Q<VisualElement>("debug-random-scores-modal");
            if (_debugRandomScoresModal == null) return;

            _debugMinScoreField = rootElement.Q<IntegerField>("debug-min-score");
            _debugMaxScoreField = rootElement.Q<IntegerField>("debug-max-score");
            _debugOperatorField = rootElement.Q<EnumField>("debug-operator");
            _debugSubscoreMinField = rootElement.Q<IntegerField>("debug-subscore-min");
            _debugSubscoreMaxField = rootElement.Q<IntegerField>("debug-subscore-max");

            _debugRandomScoresModalButton = rootElement.Q<Button>("debug-random-scores-modal-submit");
            _debugRandomScoresModalButton.RegisterCallback<ClickEvent>(evt => _ = DebugRandomScores());

            _debugRandomScoresModalCloseButton = rootElement.Q<Button>("debug-random-scores-modal-close");
            _debugRandomScoresModalCloseButton.RegisterCallback<ClickEvent>(_ => HideDebugRandomScoresModal());
        }

        private void InitializeErrorPopup(VisualElement rootElement)
        {
            _errorPopup = rootElement.Q<VisualElement>("error-popup");
            _errorMessage = rootElement.Q<Label>("error-message");
            _errorCloseButton = rootElement.Q<Button>("error-close");
            _errorCloseButton.RegisterCallback<ClickEvent>(_ => HideErrorPopup());

            // Initialize event info modal
            _eventInfoModal = rootElement.Q<VisualElement>("event-info-modal");
            _eventInfoModalCloseButton = rootElement.Q<Button>("event-info-modal-close");
            _eventInfoModalCloseButton?.RegisterCallback<ClickEvent>(_ => HideEventInfoModal());

            _infoIdLabel = rootElement.Q<Label>("info-id");
            _infoOperatorLabel = rootElement.Q<Label>("info-operator");
            _infoResetScheduleLabel = rootElement.Q<Label>("info-reset-schedule");
            _infoCohortSizeLabel = rootElement.Q<Label>("info-cohort-size");
            _infoAscendingLabel = rootElement.Q<Label>("info-ascending");
            _infoMaxIdleTierDropLabel = rootElement.Q<Label>("info-max-idle-tier-drop");
            _infoChangeZonesLabel = rootElement.Q<Label>("info-change-zones");
            _infoRewardTiersLabel = rootElement.Q<Label>("info-reward-tiers");
        }

        private void HandleInitialized(ISession session, EventLeaderboardsController controller)
        {
            _walletDisplay.StartObserving();
        }

        private void HandleStartError(Exception e)
        {
            ShowError(e.Message);
        }

        #endregion

        #region Event Leaderboard List Management

        public async Task RefreshEventLeaderboards()
        {
            HideAllModals();

            var refreshData = await _controller.RefreshEventLeaderboards();

            _eventLeaderboardsList.RefreshItems();
            _eventLeaderboardsList.ClearSelection();

            if (refreshData == null)
                HideSelectedEventLeaderboardPanel();
            else
                _eventLeaderboardsList.SetSelection(refreshData.Item1);
        }

        private async Task SelectEventLeaderboard()
        {
            if (_eventLeaderboardsList.selectedItem is not IEventLeaderboard eventLeaderboard) return;

            try
            {
                var records = await _controller.SelectEventLeaderboard(eventLeaderboard);

                _currentEventLeaderboard = eventLeaderboard;
                _controller.SelectedEventLeaderboardRecords.Clear();
                _controller.SelectedEventLeaderboardRecords.AddRange(records);
                _eventLeaderboardRecordsList.RefreshItems();

                UpdateEventLeaderboardButtons(records);
                ShowSelectedEventLeaderboardPanel(eventLeaderboard);
                UpdateEmptyState(eventLeaderboard, records);
            }
            catch (Exception e)
            {
                ShowError(e.Message);
            }
        }

        private void UpdateEmptyState(IEventLeaderboard eventLeaderboard, List<IEventLeaderboardScore> records)
        {
            if (_emptyStateContainer == null || _emptyStateMessage == null)
                return;

            var isActive = eventLeaderboard.IsActive && EventLeaderboardTimeUtility.HasStarted(eventLeaderboard);

            // Check if the user has joined the event
            var userHasJoined = false;
            foreach (var record in records)
            {
                if (record.Username == _controller.CurrentUsername)
                {
                    userHasJoined = true;
                    break;
                }
            }

            // Show empty state if there are no records
            if (records.Count == 0)
            {
                if (isActive && !userHasJoined)
                {
                    _emptyStateMessage.text = "Join this event to see player scores";
                }
                else if (!isActive && !userHasJoined)
                {
                    _emptyStateMessage.text = "You did not participate in this event";
                }
                else
                {
                    _emptyStateMessage.text = "No records to display";
                }
                _emptyStateContainer.style.display = DisplayStyle.Flex;
                _eventLeaderboardRecordsList.style.display = DisplayStyle.None;
            }
            else
            {
                // Show the list
                _emptyStateContainer.style.display = DisplayStyle.None;
                _eventLeaderboardRecordsList.style.display = DisplayStyle.Flex;
            }
        }

        #endregion

        #region Event Leaderboard Detail Panel

        private void ShowSelectedEventLeaderboardPanel(IEventLeaderboard eventLeaderboard)
        {
            _selectedEventLeaderboardNameLabel.text = eventLeaderboard.Name;
            _selectedEventLeaderboardDescriptionLabel.text = string.IsNullOrEmpty(eventLeaderboard.Description)
                ? "No description set."
                : eventLeaderboard.Description;

            // Map tier to Bronze/Silver/Gold with tier number
            var (tierName, tierColor) = GetTierNameAndColor(eventLeaderboard.Tier);
            _selectedEventLeaderboardTierLabel.text = $"Cohort: {tierName} (Tier {eventLeaderboard.Tier})";
            _selectedEventLeaderboardTierLabel.style.color = new StyleColor(Color.white);

            // Display time remaining
            if (eventLeaderboard.IsActive)
            {
                var timeRemaining = EventLeaderboardTimeUtility.GetTimeRemaining(eventLeaderboard);
                _selectedEventLeaderboardTimeRemainingLabel.text = EventLeaderboardTimeUtility.FormatTimeDuration(timeRemaining);
            }
            else
            {
                _selectedEventLeaderboardTimeRemainingLabel.text = "Ended";
            }

            _selectedEventLeaderboardPanel.style.display = DisplayStyle.Flex;
        }

        /// <summary>
        /// Maps tier numbers to tier names and colors.
        /// Tier 0 = Bronze, Tier 1 = Silver, Tier 2 = Gold
        /// </summary>
        private static (string name, Color color) GetTierNameAndColor(int tier)
        {
            return tier switch
            {
                0 => ("Bronze", new Color(0.8f, 0.5f, 0.2f)), // Bronze color
                1 => ("Silver", new Color(0.75f, 0.75f, 0.75f)), // Silver color
                2 => ("Gold", new Color(1f, 0.84f, 0f)), // Gold color
                _ => ($"Tier {tier}", Color.white)
            };
        }

        private void HideSelectedEventLeaderboardPanel()
        {
            _selectedEventLeaderboardPanel.style.display = DisplayStyle.None;
        }

        private void UpdateEventLeaderboardButtons(List<IEventLeaderboardScore> records)
        {
            var isActive = _currentEventLeaderboard.IsActive && EventLeaderboardTimeUtility.HasStarted(_currentEventLeaderboard);
            var canClaim = _currentEventLeaderboard.CanClaim;
            var canRoll = _currentEventLeaderboard.CanRoll;

            // Check if the user has joined the event (has a record in the leaderboard)
            var userHasJoined = false;
            foreach (var record in records)
            {
                if (record.Username == _controller.CurrentUsername)
                {
                    userHasJoined = true;
                    break;
                }
            }

            // Submit score is only available if the event is active and user has joined
            _submitScoreButton.style.display = (isActive && userHasJoined) ? DisplayStyle.Flex : DisplayStyle.None;

            // Claim rewards is available if the event has ended and rewards can be claimed
            _claimRewardsButton.style.display = canClaim ? DisplayStyle.Flex : DisplayStyle.None;

            // Roll/Join button shows when:
            // 1. Event is active and user hasn't joined (to join initially)
            // 2. Event can be rolled after claiming rewards (to re-join)
            _rollButton.style.display = ((isActive && !userHasJoined) || canRoll) ? DisplayStyle.Flex : DisplayStyle.None;

            // Debug buttons are only shown when event is active AND user has joined (developer tools)
            if (_debugFillButton != null)
                _debugFillButton.style.display = (isActive && userHasJoined) ? DisplayStyle.Flex : DisplayStyle.None;
            if (_debugRandomScoresButton != null)
                _debugRandomScoresButton.style.display = (isActive && userHasJoined) ? DisplayStyle.Flex : DisplayStyle.None;

            // Show dev tools section when an event is selected
            if (_devToolsPanel != null)
                _devToolsPanel.style.display = DisplayStyle.Flex;
        }

        #endregion

        #region Event Leaderboard Action Handlers

        private async void ClaimRewards(ClickEvent evt)
        {
            try
            {
                await _controller.ClaimRewards();
            }
            catch (Exception e)
            {
                ShowError(e.Message);
            }

            await RefreshEventLeaderboards();
        }

        private async void RollEventLeaderboard(ClickEvent evt)
        {
            try
            {
                await _controller.RollEventLeaderboard();
            }
            catch (Exception e)
            {
                ShowError(e.Message);
            }

            await RefreshEventLeaderboards();
        }

        #endregion

        #region Submit Score Modal

        private void ShowSubmitScoreModal()
        {
            _scoreField.value = 0;
            _subScoreField.value = 0;
            _submitScoreModal.style.display = DisplayStyle.Flex;
        }

        private void HideSubmitScoreModal()
        {
            _submitScoreModal.style.display = DisplayStyle.None;
        }

        private async void SubmitScore(ClickEvent evt)
        {
            try
            {
                await _controller.SubmitScore(_scoreField.value, _subScoreField.value);
                HideSubmitScoreModal();
            }
            catch (Exception e)
            {
                ShowError(e.Message);
            }

            await RefreshEventLeaderboards();
        }

        #endregion

        #region Debug Modals

        private void ShowDebugFillModal()
        {
            _debugFillTargetCountField.value = 50;
            _debugFillModal.style.display = DisplayStyle.Flex;
        }

        private void HideDebugFillModal()
        {
            _debugFillModal.style.display = DisplayStyle.None;
        }

        private async Task DebugFill()
        {
            try
            {
                await _controller.DebugFillEventLeaderboard(_debugFillTargetCountField.value);
                HideDebugFillModal();
                await RefreshEventLeaderboards();
            }
            catch (Exception e)
            {
                ShowError(e.Message);
            }
        }

        private void ShowDebugRandomScoresModal()
        {
            _debugMinScoreField.value = 1;
            _debugMaxScoreField.value = 100;
            _debugOperatorField.value = ApiOperator.SET;
            _debugSubscoreMinField.value = 1;
            _debugSubscoreMaxField.value = 100;
            _debugRandomScoresModal.style.display = DisplayStyle.Flex;
        }

        private void HideDebugRandomScoresModal()
        {
            _debugRandomScoresModal.style.display = DisplayStyle.None;
        }

        private async Task DebugRandomScores()
        {
            try
            {
                await _controller.DebugRandomScores(
                    _debugMinScoreField.value,
                    _debugMaxScoreField.value,
                    (ApiOperator)_debugOperatorField.value,
                    _debugSubscoreMinField.value,
                    _debugSubscoreMaxField.value
                );
                HideDebugRandomScoresModal();
                await RefreshEventLeaderboards();
            }
            catch (Exception e)
            {
                ShowError(e.Message);
            }
        }

        #endregion

        #region Error Handling

        private void ShowError(string message)
        {
            _errorMessage.text = message;
            _errorPopup.style.display = DisplayStyle.Flex;
        }

        private void HideErrorPopup()
        {
            _errorPopup.style.display = DisplayStyle.None;
        }

        #endregion

        #region Event Info Modal

        private void ShowEventInfoModal()
        {
            if (_currentEventLeaderboard == null || _eventInfoModal == null)
                return;

            var leaderboard = _currentEventLeaderboard;

            // Get description from additional properties
            var description = "";
            if (leaderboard.AdditionalProperties.TryGetValue("description", out var additionalProperty))
            {
                description = additionalProperty;
            }

            _infoIdLabel.text = string.IsNullOrEmpty(description)
                ? $"<b>Event ID:</b> {leaderboard.Id}"
                : $"<b>Event ID:</b> {leaderboard.Id}\n\n{description}";
            _infoOperatorLabel.text = $"<b>Operator:</b> {leaderboard.Operator}";
            _infoCohortSizeLabel.text = $"<b>Cohort size:</b> {leaderboard.MaxCount}";
            _infoAscendingLabel.text = $"<b>Ascending:</b> {leaderboard.Ascending} (lower scores are better)";

            var resetScheduleDesc = "";
       
            if (leaderboard.AdditionalProperties.TryGetValue("reset_schedule_desc", out var property))
            {
                resetScheduleDesc = property;
            }
            
            _infoResetScheduleLabel.text = string.IsNullOrEmpty(resetScheduleDesc)
                ? $"<b>Reset schedule:</b> N/A"
                : $"<b>Reset schedule:</b> {resetScheduleDesc}";

            // Calculate duration in human-readable format
            var durationSeconds = (leaderboard.EndTimeSec - leaderboard.StartTimeSec);
            var durationText = FormatDurationInSeconds(durationSeconds);
            _infoMaxIdleTierDropLabel.text = $"<b>Duration:</b> {durationText}";

            // Determine which promotion mechanic is being used
            var tierKey = leaderboard.Tier.ToString();
            var usingChangeZones = leaderboard.ChangeZones.TryGetValue(tierKey, out var changeZone) &&
                                   (changeZone.Promotion > 0 || changeZone.Demotion > 0);

            if (usingChangeZones)
            {
                // Show change zones
                var changeZonesText = "<b>Change zones:</b>\n";
                foreach (var kvp in leaderboard.ChangeZones)
                {
                    var tier = kvp.Key;
                    var zone = kvp.Value;
                    if (zone.Promotion > 0 || zone.Demotion > 0)
                    {
                        changeZonesText += $"Tier {tier}: Promotion {zone.Promotion * 100:F0}%, Demotion {zone.Demotion * 100:F0}%, Demote idle: {zone.DemoteIdle}\n";
                    }
                }
                _infoChangeZonesLabel.text = changeZonesText.TrimEnd('\n');
                _infoRewardTiersLabel.text = ""; // Hide reward tiers
            }
            else
            {
                // Show reward tiers grouped by tier
                if (leaderboard.RewardTiers.Count > 0)
                {
                    var rewardTiersText = "<b>Reward tiers:</b>\n";
                    foreach (var kvp in leaderboard.RewardTiers)
                    {
                        var tier = kvp.Key;
                        var tierRewards = kvp.Value;
                        rewardTiersText += $"<b>Tier {tier}</b>\n";
                        foreach (var rewardTier in tierRewards.RewardTiers)
                        {
                            var tierChangeName = rewardTier.TierChange switch
                            {
                                > 0 => $"+{rewardTier.TierChange} tier",
                                < 0 => $"{rewardTier.TierChange} tier",
                                _ => "No change"
                            };
                            rewardTiersText += $"Rank {rewardTier.RankMin}-{rewardTier.RankMax}: {tierChangeName}\n";
                        }
                    }
                    _infoRewardTiersLabel.text = rewardTiersText.TrimEnd('\n');
                }
                else
                {
                    _infoRewardTiersLabel.text = "";
                }
                _infoChangeZonesLabel.text = ""; // Hide change zones
            }

            _eventInfoModal.style.display = DisplayStyle.Flex;
        }

        private static string FormatDurationInSeconds(long seconds)
        {
            if (seconds < 60)
                return $"{seconds} seconds";

            var minutes = seconds / 60;
            if (minutes < 60)
                return $"{minutes} minutes";

            var hours = minutes / 60;
            var remainingMinutes = minutes % 60;
            if (remainingMinutes > 0)
                return $"{hours} hours {remainingMinutes} minutes";

            return $"{hours} hours";
        }

        private void HideEventInfoModal()
        {
            if (_eventInfoModal != null)
            {
                _eventInfoModal.style.display = DisplayStyle.None;
            }
        }

        #endregion

        private void HideAllModals()
        {
            HideSubmitScoreModal();
            if (_debugFillModal != null) HideDebugFillModal();
            if (_debugRandomScoresModal != null) HideDebugRandomScoresModal();
            HideEventInfoModal();
        }
    }
}
