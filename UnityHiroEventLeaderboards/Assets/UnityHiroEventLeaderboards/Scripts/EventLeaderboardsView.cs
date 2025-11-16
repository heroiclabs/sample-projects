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
        private Label _selectedEventLeaderboardStatusLabel;
        private Label _selectedEventLeaderboardTierLabel;
        private Label _selectedEventLeaderboardEndTimeLabel;
        private ListView _eventLeaderboardRecordsList;
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

        private IEventLeaderboard _currentEventLeaderboard;
        private int _selectedTabIndex;

        #region Initialization

        public EventLeaderboardsView(EventLeaderboardsController controller, HiroEventLeaderboardsCoordinator coordinator,
            VisualTreeAsset eventLeaderboardEntryTemplate,
            VisualTreeAsset eventLeaderboardRecordTemplate)
        {
            _controller = controller;
            _eventLeaderboardEntryTemplate = eventLeaderboardEntryTemplate;
            _eventLeaderboardRecordTemplate = eventLeaderboardRecordTemplate;

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
            _selectedEventLeaderboardStatusLabel = rootElement.Q<Label>("selected-event-leaderboard-status");
            _selectedEventLeaderboardTierLabel = rootElement.Q<Label>("selected-event-leaderboard-tier");
            _selectedEventLeaderboardEndTimeLabel = rootElement.Q<Label>("selected-event-leaderboard-end-time");
        }

        private void InitializeLists(VisualElement rootElement)
        {
            // Event leaderboard records list
            _eventLeaderboardRecordsList = rootElement.Q<ListView>("event-leaderboard-records-list");
            _eventLeaderboardRecordsList.makeItem = () =>
            {
                var newListEntry = _eventLeaderboardRecordTemplate.Instantiate();
                var newListEntryLogic = new EventLeaderboardRecordView();
                newListEntry.userData = newListEntryLogic;
                newListEntryLogic.SetVisualElement(newListEntry);
                return newListEntry;
            };
            _eventLeaderboardRecordsList.bindItem = (item, index) =>
            {
                (item.userData as EventLeaderboardRecordView)?.SetEventLeaderboardRecord(_controller.SelectedEventLeaderboardRecords[index]);
            };
            _eventLeaderboardRecordsList.itemsSource = _controller.SelectedEventLeaderboardRecords;

            _eventLeaderboardRecordsScrollView = _eventLeaderboardRecordsList.Q<ScrollView>();
            _eventLeaderboardRecordsScrollView.verticalScrollerVisibility = ScrollerVisibility.AlwaysVisible;

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
            _eventLeaderboardsScrollView.verticalScrollerVisibility = ScrollerVisibility.AlwaysVisible;
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
            }
            catch (Exception e)
            {
                ShowError(e.Message);
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

            _selectedEventLeaderboardTierLabel.text = $"Tier {eventLeaderboard.Tier}";

            var currentTime = DateTimeOffset.FromUnixTimeSeconds(eventLeaderboard.CurrentTimeSec);
            var startTime = DateTimeOffset.FromUnixTimeSeconds(eventLeaderboard.StartTimeSec);
            var endTime = DateTimeOffset.FromUnixTimeSeconds(eventLeaderboard.EndTimeSec);

            if (eventLeaderboard.IsActive)
            {
                if (currentTime >= startTime)
                {
                    _selectedEventLeaderboardStatusLabel.text = "Active";
                    _selectedEventLeaderboardStatusLabel.style.color = new StyleColor(Color.green);
                }
                else
                {
                    var difference = startTime - currentTime;
                    _selectedEventLeaderboardStatusLabel.text = $"Starts in {difference.Days}d, {difference.Hours}h, {difference.Minutes}m";
                    _selectedEventLeaderboardStatusLabel.style.color = new StyleColor(Color.yellow);
                }
            }
            else
            {
                _selectedEventLeaderboardStatusLabel.text = "Ended";
                _selectedEventLeaderboardStatusLabel.style.color = new StyleColor(Color.red);
            }

            _selectedEventLeaderboardEndTimeLabel.text = endTime.LocalDateTime.ToString("MMM dd, yyyy HH:mm");
            _selectedEventLeaderboardPanel.style.display = DisplayStyle.Flex;
        }

        private void HideSelectedEventLeaderboardPanel()
        {
            _selectedEventLeaderboardPanel.style.display = DisplayStyle.None;
        }

        private void UpdateEventLeaderboardButtons(List<IEventLeaderboardScore> records)
        {
            var currentTime = DateTimeOffset.FromUnixTimeSeconds(_currentEventLeaderboard.CurrentTimeSec);
            var startTime = DateTimeOffset.FromUnixTimeSeconds(_currentEventLeaderboard.StartTimeSec);
            var isActive = _currentEventLeaderboard.IsActive && currentTime >= startTime;
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

            // Debug buttons are always shown when event is active (developer tools)
            if (_debugFillButton != null)
                _debugFillButton.style.display = isActive ? DisplayStyle.Flex : DisplayStyle.None;
            if (_debugRandomScoresButton != null)
                _debugRandomScoresButton.style.display = isActive ? DisplayStyle.Flex : DisplayStyle.None;

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

        private void HideAllModals()
        {
            HideSubmitScoreModal();
            if (_debugFillModal != null) HideDebugFillModal();
            if (_debugRandomScoresModal != null) HideDebugRandomScoresModal();
        }
    }
}
