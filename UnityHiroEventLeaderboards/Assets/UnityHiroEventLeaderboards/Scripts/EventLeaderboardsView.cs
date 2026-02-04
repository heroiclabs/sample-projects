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
using System.Threading;
using System.Threading.Tasks;
using Hiro;
using HeroicUI;
using HeroicUtils;
using Nakama;
using UnityEngine;
using UnityEngine.UIElements;

namespace HiroEventLeaderboards
{
    /// <summary>
    /// View for the Event Leaderboards UI.
    /// Handles UI presentation and user interactions.
    /// Implements IDisposable for proper resource cleanup.
    /// </summary>
    public sealed class EventLeaderboardsView : IDisposable
    {
        private readonly EventLeaderboardsController _controller;
        private readonly VisualElement _rootElement;
        private readonly VisualTreeAsset _eventLeaderboardEntryTemplate;
        private readonly VisualTreeAsset _eventLeaderboardRecordTemplate;
        private readonly VisualTreeAsset _eventLeaderboardZoneTemplate;

        private CancellationTokenSource _cts = new();
        private readonly object _disposeLock = new();
        private volatile bool _disposed;

        private WalletDisplay _walletDisplay;
        private LoadingSpinner _listSpinner;
        private LoadingSpinner _recordsSpinner;
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
        private Button _refreshButton;

        private VisualElement _submitScoreModal;
        private TextField _scoreField;
        private TextField _subScoreField;
        private Button _submitScoreModalButton;
        private Button _submitScoreModalCloseButton;

        private VisualElement _debugFillModal;
        private TextField _debugFillTargetCountField;
        private Button _debugFillModalButton;
        private Button _debugFillModalCloseButton;

        private VisualElement _debugRandomScoresModal;
        private TextField _debugMinScoreField;
        private TextField _debugMaxScoreField;
        private EnumField _debugOperatorField;
        private TextField _debugSubscoreMinField;
        private TextField _debugSubscoreMaxField;
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

        public EventLeaderboardsView(
            EventLeaderboardsController controller,
            VisualElement rootElement,
            VisualTreeAsset eventLeaderboardEntryTemplate,
            VisualTreeAsset eventLeaderboardRecordTemplate,
            VisualTreeAsset eventLeaderboardZoneTemplate)
        {
            _controller = controller ?? throw new ArgumentNullException(nameof(controller));
            _rootElement = rootElement ?? throw new ArgumentNullException(nameof(rootElement));
            _eventLeaderboardEntryTemplate = eventLeaderboardEntryTemplate ?? throw new ArgumentNullException(nameof(eventLeaderboardEntryTemplate));
            _eventLeaderboardRecordTemplate = eventLeaderboardRecordTemplate ?? throw new ArgumentNullException(nameof(eventLeaderboardRecordTemplate));
            _eventLeaderboardZoneTemplate = eventLeaderboardZoneTemplate ?? throw new ArgumentNullException(nameof(eventLeaderboardZoneTemplate));
        }

        /// <summary>
        /// Initializes the view and starts observing systems.
        /// Call this after construction when the coordinator is ready.
        /// </summary>
        public void Initialize()
        {
            InitializeElements();
            SubscribeToEvents();
            _selectedEventLeaderboardPanel.Hide();

            _walletDisplay.StartObserving();

            // Initial refresh
            OnRefreshRequested();
        }

        public void Dispose()
        {
            lock (_disposeLock)
            {
                if (_disposed)
                    return;
                _disposed = true;
            }

            _cts.Cancel();
            _cts.Dispose();

            _listSpinner?.Dispose();
            _recordsSpinner?.Dispose();
            _walletDisplay?.Dispose();

            UnsubscribeFromEvents();
        }

        private void ThrowIfDisposedOrCancelled()
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(EventLeaderboardsView));
            _cts.Token.ThrowIfCancellationRequested();
        }

        #region Initialization

        private void InitializeElements()
        {
            _walletDisplay = new WalletDisplay(_rootElement.Q<VisualElement>("wallet-display"));

            var listSpinnerElement = _rootElement.Q<VisualElement>("event-leaderboards-list-spinner");
            _listSpinner = new LoadingSpinner(listSpinnerElement);

            var recordsSpinnerElement = _rootElement.Q<VisualElement>("event-leaderboard-records-spinner");
            _recordsSpinner = new LoadingSpinner(recordsSpinnerElement);

            InitializeTabs();
            InitializeButtons();
            InitializeDevTools();
            InitializeSelectedEventLeaderboardPanel();
            InitializeLists();
            InitializeModals();
            InitializeErrorPopup();
        }

        private void InitializeTabs()
        {
            _myEventLeaderboardsTab = _rootElement.RequireElement<Button>("my-event-leaderboards-tab");
            _myEventLeaderboardsTab.RegisterCallback<ClickEvent>(OnTabClicked);
        }

        private void InitializeButtons()
        {
            _submitScoreButton = _rootElement.RequireElement<Button>("event-leaderboard-submit-score");
            _submitScoreButton.RegisterCallback<ClickEvent>(OnSubmitScoreClicked);

            _claimRewardsButton = _rootElement.RequireElement<Button>("event-leaderboard-claim");
            _claimRewardsButton.RegisterCallback<ClickEvent>(OnClaimRewardsClicked);

            _rollButton = _rootElement.RequireElement<Button>("event-leaderboard-roll");
            _rollButton.RegisterCallback<ClickEvent>(OnRollClicked);

            _refreshButton = _rootElement.RequireElement<Button>("event-leaderboards-refresh");
            _refreshButton.RegisterCallback<ClickEvent>(OnRefreshClicked);
        }

        private void InitializeDevTools()
        {
            _devToolsPanel = _rootElement.Q<VisualElement>("dev-tools");

            _debugFillButton = _rootElement.Q<Button>("event-leaderboard-debug-fill");
            _debugFillButton?.RegisterCallback<ClickEvent>(OnDebugFillClicked);

            _debugRandomScoresButton = _rootElement.Q<Button>("event-leaderboard-debug-random-scores");
            _debugRandomScoresButton?.RegisterCallback<ClickEvent>(OnDebugRandomScoresClicked);
        }

        private void InitializeSelectedEventLeaderboardPanel()
        {
            _selectedEventLeaderboardPanel = _rootElement.RequireElement<VisualElement>("selected-event-leaderboard-panel");
            _selectedEventLeaderboardNameLabel = _rootElement.RequireElement<Label>("selected-event-leaderboard-name");
            _selectedEventLeaderboardDescriptionLabel = _rootElement.RequireElement<Label>("selected-event-leaderboard-description");
            _selectedEventLeaderboardTierLabel = _rootElement.RequireElement<Label>("selected-event-leaderboard-tier");
            _selectedEventLeaderboardTimeRemainingLabel = _rootElement.RequireElement<Label>("selected-event-leaderboard-time-remaining");

            _eventInfoButton = _rootElement.RequireElement<Button>("event-info-button");
            _eventInfoButton.RegisterCallback<ClickEvent>(OnEventInfoClicked);
        }

        private void InitializeLists()
        {
            // Event leaderboard records list
            _eventLeaderboardRecordsList = _rootElement.RequireElement<ListView>("event-leaderboard-records-list");
            _eventLeaderboardRecordsList.makeItem = MakeRecordItem;
            _eventLeaderboardRecordsList.bindItem = BindRecordItem;
            _eventLeaderboardRecordsList.itemsSource = (System.Collections.IList)_controller.DisplayItems;

            var recordsScrollView = _eventLeaderboardRecordsList.Q<ScrollView>();
            recordsScrollView.verticalScrollerVisibility = ScrollerVisibility.AlwaysVisible;

            // Empty state elements
            _emptyStateContainer = _rootElement.Q<VisualElement>("empty-state-container");
            _emptyStateMessage = _rootElement.Q<Label>("empty-state-message");

            // Event leaderboards list
            _eventLeaderboardsList = _rootElement.RequireElement<ListView>("event-leaderboards-list");
            _eventLeaderboardsList.makeItem = MakeLeaderboardItem;
            _eventLeaderboardsList.bindItem = BindLeaderboardItem;
            _eventLeaderboardsList.itemsSource = (System.Collections.IList)_controller.EventLeaderboards;
            _eventLeaderboardsList.selectionChanged += OnLeaderboardSelectionChanged;

            var leaderboardsScrollView = _eventLeaderboardsList.Q<ScrollView>();
            leaderboardsScrollView.verticalScrollerVisibility = ScrollerVisibility.Auto;
        }

        private VisualElement MakeRecordItem()
        {
            var container = new VisualElement();
            container.style.flexGrow = 1;
            return container;
        }

        private void BindRecordItem(VisualElement item, int index)
        {
            item.Clear();
            var displayItem = _controller.DisplayItems[index];

            if (displayItem.Type == LeaderboardDisplayItem.ItemType.PlayerRecord)
            {
                var recordElement = _eventLeaderboardRecordTemplate.Instantiate();
                var recordView = new EventLeaderboardRecordView();
                recordView.SetVisualElement(recordElement);
                recordView.SetEventLeaderboardRecord(displayItem.PlayerRecord, _currentEventLeaderboard, _controller.CurrentUsername);
                item.Add(recordElement);
            }
            else if (displayItem.Type == LeaderboardDisplayItem.ItemType.ZoneIndicator)
            {
                var zoneElement = _eventLeaderboardZoneTemplate.Instantiate();
                var zoneView = new EventLeaderboardZoneView();
                zoneView.SetVisualElement(zoneElement);
                zoneView.SetZone(displayItem.ZoneType);
                item.Add(zoneElement);
            }
        }

        private VisualElement MakeLeaderboardItem()
        {
            var newListEntry = _eventLeaderboardEntryTemplate.Instantiate();
            var newListEntryLogic = new EventLeaderboardView();
            newListEntry.userData = newListEntryLogic;
            newListEntryLogic.SetVisualElement(newListEntry);
            return newListEntry;
        }

        private void BindLeaderboardItem(VisualElement item, int index)
        {
            (item.userData as EventLeaderboardView)?.SetEventLeaderboard(_controller.EventLeaderboards[index]);
        }

        private void InitializeModals()
        {
            InitializeSubmitScoreModal();
            InitializeDebugFillModal();
            InitializeDebugRandomScoresModal();
        }

        private void InitializeSubmitScoreModal()
        {
            _submitScoreModal = _rootElement.RequireElement<VisualElement>("submit-score-modal");
            _scoreField = _rootElement.RequireElement<TextField>("submit-score-score");
            _subScoreField = _rootElement.RequireElement<TextField>("submit-score-subscore");

            _submitScoreModalButton = _rootElement.RequireElement<Button>("submit-score-modal-submit");
            _submitScoreModalButton.RegisterCallback<ClickEvent>(OnSubmitScoreModalSubmitClicked);

            _submitScoreModalCloseButton = _rootElement.RequireElement<Button>("submit-score-modal-close");
            _submitScoreModalCloseButton.RegisterCallback<ClickEvent>(OnSubmitScoreModalCloseClicked);
        }

        private void InitializeDebugFillModal()
        {
            _debugFillModal = _rootElement.Q<VisualElement>("debug-fill-modal");
            if (_debugFillModal == null)
                return;

            _debugFillTargetCountField = _rootElement.Q<TextField>("debug-fill-target-count");

            _debugFillModalButton = _rootElement.Q<Button>("debug-fill-modal-fill");
            _debugFillModalButton?.RegisterCallback<ClickEvent>(OnDebugFillModalSubmitClicked);

            _debugFillModalCloseButton = _rootElement.Q<Button>("debug-fill-modal-close");
            _debugFillModalCloseButton?.RegisterCallback<ClickEvent>(OnDebugFillModalCloseClicked);
        }

        private void InitializeDebugRandomScoresModal()
        {
            _debugRandomScoresModal = _rootElement.Q<VisualElement>("debug-random-scores-modal");
            if (_debugRandomScoresModal == null)
                return;

            _debugMinScoreField = _rootElement.Q<TextField>("debug-min-score");
            _debugMaxScoreField = _rootElement.Q<TextField>("debug-max-score");
            _debugOperatorField = _rootElement.Q<EnumField>("debug-operator");
            _debugSubscoreMinField = _rootElement.Q<TextField>("debug-subscore-min");
            _debugSubscoreMaxField = _rootElement.Q<TextField>("debug-subscore-max");

            _debugRandomScoresModalButton = _rootElement.Q<Button>("debug-random-scores-modal-submit");
            _debugRandomScoresModalButton?.RegisterCallback<ClickEvent>(OnDebugRandomScoresModalSubmitClicked);

            _debugRandomScoresModalCloseButton = _rootElement.Q<Button>("debug-random-scores-modal-close");
            _debugRandomScoresModalCloseButton?.RegisterCallback<ClickEvent>(OnDebugRandomScoresModalCloseClicked);
        }

        private void InitializeErrorPopup()
        {
            _errorPopup = _rootElement.RequireElement<VisualElement>("error-popup");
            _errorMessage = _rootElement.RequireElement<Label>("error-message");
            _errorCloseButton = _rootElement.RequireElement<Button>("error-close");
            _errorCloseButton.RegisterCallback<ClickEvent>(OnErrorCloseClicked);

            // Initialize event info modal
            _eventInfoModal = _rootElement.Q<VisualElement>("event-info-modal");
            _eventInfoModalCloseButton = _rootElement.Q<Button>("event-info-modal-close");
            _eventInfoModalCloseButton?.RegisterCallback<ClickEvent>(OnEventInfoModalCloseClicked);

            _infoIdLabel = _rootElement.Q<Label>("info-id");
            _infoOperatorLabel = _rootElement.Q<Label>("info-operator");
            _infoResetScheduleLabel = _rootElement.Q<Label>("info-reset-schedule");
            _infoCohortSizeLabel = _rootElement.Q<Label>("info-cohort-size");
            _infoAscendingLabel = _rootElement.Q<Label>("info-ascending");
            _infoMaxIdleTierDropLabel = _rootElement.Q<Label>("info-max-idle-tier-drop");
            _infoChangeZonesLabel = _rootElement.Q<Label>("info-change-zones");
            _infoRewardTiersLabel = _rootElement.Q<Label>("info-reward-tiers");
        }

        private void SubscribeToEvents()
        {
            AccountSwitcher.AccountSwitched += OnAccountSwitched;
        }

        private void UnsubscribeFromEvents()
        {
            AccountSwitcher.AccountSwitched -= OnAccountSwitched;
            _eventLeaderboardsList.selectionChanged -= OnLeaderboardSelectionChanged;
        }

        #endregion

        #region Event Handlers

        private void OnTabClicked(ClickEvent evt)
        {
            if (_selectedTabIndex == 0)
                return;
            _selectedTabIndex = 0;
            _myEventLeaderboardsTab.AddToClassList("selected");
            OnRefreshRequested();
        }

        private void OnSubmitScoreClicked(ClickEvent evt)
        {
            ShowSubmitScoreModal();
        }

        private async void OnClaimRewardsClicked(ClickEvent evt)
        {
            try
            {
                ThrowIfDisposedOrCancelled();
                await _controller.ClaimRewardsAsync();
                await RefreshEventLeaderboardsAsync();
            }
            catch (OperationCanceledException)
            {
                // Expected on dispose
            }
            catch (Exception e)
            {
                ShowError(e.Message);
                Debug.LogException(e);
            }
        }

        private async void OnRollClicked(ClickEvent evt)
        {
            try
            {
                ThrowIfDisposedOrCancelled();
                await _controller.RollEventLeaderboardAsync();
                await RefreshEventLeaderboardsAsync();
            }
            catch (OperationCanceledException)
            {
                // Expected on dispose
            }
            catch (Exception e)
            {
                ShowError(e.Message);
                Debug.LogException(e);
            }
        }

        private void OnRefreshClicked(ClickEvent evt)
        {
            OnRefreshRequested();
        }

        private async void OnRefreshRequested()
        {
            try
            {
                ThrowIfDisposedOrCancelled();
                await RefreshEventLeaderboardsAsync();
            }
            catch (OperationCanceledException)
            {
                // Expected on dispose
            }
            catch (Exception e)
            {
                ShowError(e.Message);
                Debug.LogException(e);
            }
        }

        private async void OnAccountSwitched()
        {
            try
            {
                ThrowIfDisposedOrCancelled();
                HideSelectedLeaderboardPanel();
                await _controller.SwitchCompleteAsync();
                await RefreshEventLeaderboardsAsync();
            }
            catch (OperationCanceledException)
            {
                // Expected on dispose
            }
            catch (Exception e)
            {
                ShowError(e.Message);
                Debug.LogException(e);
            }
        }

        private void OnDebugFillClicked(ClickEvent evt)
        {
            ShowDebugFillModal();
        }

        private void OnDebugRandomScoresClicked(ClickEvent evt)
        {
            ShowDebugRandomScoresModal();
        }

        private void OnEventInfoClicked(ClickEvent evt)
        {
            ShowEventInfoModal();
        }

        private async void OnLeaderboardSelectionChanged(IEnumerable<object> selection)
        {
            try
            {
                ThrowIfDisposedOrCancelled();
                await SelectEventLeaderboardAsync();
            }
            catch (OperationCanceledException)
            {
                // Expected on dispose
            }
            catch (Exception e)
            {
                ShowError(e.Message);
                Debug.LogException(e);
            }
        }

        private void OnSubmitScoreModalCloseClicked(ClickEvent evt)
        {
            HideSubmitScoreModal();
        }

        private async void OnSubmitScoreModalSubmitClicked(ClickEvent evt)
        {
            try
            {
                ThrowIfDisposedOrCancelled();
                await _controller.SubmitScoreAsync(int.Parse(_scoreField.value), int.Parse(_subScoreField.value));
                HideSubmitScoreModal();
                await RefreshEventLeaderboardsAsync();
            }
            catch (OperationCanceledException)
            {
                // Expected on dispose
            }
            catch (Exception e)
            {
                ShowError(e.Message);
                Debug.LogException(e);
            }
        }

        private void OnDebugFillModalCloseClicked(ClickEvent evt)
        {
            HideDebugFillModal();
        }

        private async void OnDebugFillModalSubmitClicked(ClickEvent evt)
        {
            try
            {
                ThrowIfDisposedOrCancelled();
                await _controller.DebugFillEventLeaderboardAsync(int.Parse(_debugFillTargetCountField.value));
                HideDebugFillModal();
                await RefreshEventLeaderboardsAsync();
            }
            catch (OperationCanceledException)
            {
                // Expected on dispose
            }
            catch (Exception e)
            {
                ShowError(e.Message);
                Debug.LogException(e);
            }
        }

        private void OnDebugRandomScoresModalCloseClicked(ClickEvent evt)
        {
            HideDebugRandomScoresModal();
        }

        private async void OnDebugRandomScoresModalSubmitClicked(ClickEvent evt)
        {
            try
            {
                ThrowIfDisposedOrCancelled();
                await _controller.DebugRandomScoresAsync(
                    int.Parse(_debugMinScoreField.value),
                    int.Parse(_debugMaxScoreField.value),
                    (ApiOperator)_debugOperatorField.value,
                    int.Parse(_debugSubscoreMinField.value),
                    int.Parse(_debugSubscoreMaxField.value));
                HideDebugRandomScoresModal();
                await RefreshEventLeaderboardsAsync();
            }
            catch (OperationCanceledException)
            {
                // Expected on dispose
            }
            catch (Exception e)
            {
                ShowError(e.Message);
                Debug.LogException(e);
            }
        }

        private void OnErrorCloseClicked(ClickEvent evt)
        {
            HideErrorPopup();
        }

        private void OnEventInfoModalCloseClicked(ClickEvent evt)
        {
            HideEventInfoModal();
        }

        #endregion

        #region Event Leaderboard List Management

        private async Task RefreshEventLeaderboardsAsync()
        {
            HideAllModals();

            // Clear current selection state before refresh
            _currentEventLeaderboard = null;
            _selectedEventLeaderboardPanel.Hide();
            _eventLeaderboardsList.ClearSelection();

            _listSpinner?.Show();
            try
            {
                var refreshData = await _controller.RefreshEventLeaderboardsAsync();

                _eventLeaderboardsList.RefreshItems();

                if (refreshData != null)
                {
                    _eventLeaderboardsList.SetSelection(refreshData.Value.index);
                }
            }
            finally
            {
                _listSpinner?.Hide();
            }
        }

        private async Task SelectEventLeaderboardAsync()
        {
            if (_eventLeaderboardsList.selectedItem is not IEventLeaderboard eventLeaderboard)
                return;

            _recordsSpinner?.Show();
            try
            {
                var records = await _controller.SelectEventLeaderboardAsync(eventLeaderboard);

                _currentEventLeaderboard = eventLeaderboard;
                _eventLeaderboardRecordsList.RefreshItems();

                UpdateEventLeaderboardButtons(records);
                ShowSelectedEventLeaderboardPanel(eventLeaderboard);
                UpdateEmptyState(eventLeaderboard, records);
            }
            finally
            {
                _recordsSpinner?.Hide();
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
                _emptyStateContainer.Show();
                _eventLeaderboardRecordsList.Hide();
            }
            else
            {
                _emptyStateContainer.Hide();
                _eventLeaderboardRecordsList.Show();
            }
        }

        #endregion

        #region Event Leaderboard Detail Panel

        private void HideSelectedLeaderboardPanel()
        {
            _selectedEventLeaderboardPanel.Hide();
        }

        private void ShowSelectedEventLeaderboardPanel(IEventLeaderboard eventLeaderboard)
        {
            _selectedEventLeaderboardNameLabel.text = eventLeaderboard.Name;
            _selectedEventLeaderboardDescriptionLabel.text = string.IsNullOrEmpty(eventLeaderboard.Description)
                ? "No description set."
                : eventLeaderboard.Description;

            // Map tier to Bronze/Silver/Gold with tier number
            var (tierName, _) = GetTierNameAndColor(eventLeaderboard.Tier);
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

            _selectedEventLeaderboardPanel.Show();
        }

        private static (string name, Color color) GetTierNameAndColor(int tier)
        {
            return tier switch
            {
                0 => ("Bronze", new Color(0.8f, 0.5f, 0.2f)),
                1 => ("Silver", new Color(0.75f, 0.75f, 0.75f)),
                2 => ("Gold", new Color(1f, 0.84f, 0f)),
                _ => ($"Tier {tier}", Color.white)
            };
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
            _submitScoreButton.SetDisplay(isActive && userHasJoined);

            // Claim rewards is available if the event has ended and rewards can be claimed
            _claimRewardsButton.SetDisplay(canClaim);

            // Roll/Join button shows when:
            // 1. Event is active and user hasn't joined (to join initially)
            // 2. Event can be rolled after claiming rewards (to re-join)
            _rollButton.SetDisplay((isActive && !userHasJoined) || canRoll);

            // Debug buttons are only shown when event is active AND user has joined (developer tools)
            _debugFillButton?.SetDisplay(isActive && userHasJoined);
            _debugRandomScoresButton?.SetDisplay(isActive && userHasJoined);

            // Show dev tools section when an event is selected
            _devToolsPanel?.Show();
        }

        #endregion

        #region Modal Management

        private void ShowSubmitScoreModal()
        {
            _scoreField.value = "0";
            _subScoreField.value = "0";
            _submitScoreModal.Show();
        }

        private void HideSubmitScoreModal()
        {
            _submitScoreModal.Hide();
        }

        private void ShowDebugFillModal()
        {
            _debugFillTargetCountField.value = "50";
            _debugFillModal?.Show();
        }

        private void HideDebugFillModal()
        {
            _debugFillModal?.Hide();
        }

        private void ShowDebugRandomScoresModal()
        {
            _debugMinScoreField.value = "1";
            _debugMaxScoreField.value = "100";
            _debugOperatorField.value = ApiOperator.SET;
            _debugSubscoreMinField.value = "1";
            _debugSubscoreMaxField.value = "100";
            _debugRandomScoresModal?.Show();
        }

        private void HideDebugRandomScoresModal()
        {
            _debugRandomScoresModal?.Hide();
        }

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
                ? "<b>Reset schedule:</b> N/A"
                : $"<b>Reset schedule:</b> {resetScheduleDesc}";

            // Calculate duration in human-readable format
            var durationSeconds = leaderboard.EndTimeSec - leaderboard.StartTimeSec;
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
                _infoRewardTiersLabel.text = "";
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
                _infoChangeZonesLabel.text = "";
            }

            _eventInfoModal.Show();
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
            _eventInfoModal?.Hide();
        }

        private void HideAllModals()
        {
            HideSubmitScoreModal();
            HideDebugFillModal();
            HideDebugRandomScoresModal();
            HideEventInfoModal();
        }

        #endregion

        #region Error Handling

        private void ShowError(string message)
        {
            _errorMessage.text = message;
            _errorPopup.Show();
        }

        private void HideErrorPopup()
        {
            _errorPopup.Hide();
        }

        #endregion
    }
}
