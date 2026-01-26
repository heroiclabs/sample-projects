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

namespace HiroChallenges
{
    /// <summary>
    /// View for the Challenges system.
    /// Manages UI presentation and user interactions, delegates all business logic to Controller.
    /// </summary>
    public sealed class ChallengesView
    {
        #region Constants

        private const int DefaultTabIndex = 0;

        #endregion

        #region Private Fields

        private readonly ChallengesController _controller;
        private readonly VisualTreeAsset _challengeEntryTemplate;
        private readonly VisualTreeAsset _challengeParticipantTemplate;

        // Main UI elements
        private WalletDisplay _walletDisplay;
        private Button _myChallengesTab;
        private Button _createButton;
        private Button _joinButton;
        private Button _leaveButton;
        private Button _claimRewardsButton;
        private Button _submitScoreButton;
        private Button _inviteButton;
        private VisualElement _selectedChallengePanel;
        private Label _selectedChallengeNameLabel;
        private Label _selectedChallengeDescriptionLabel;
        private Label _selectedChallengeStatusLabel;
        private Label _selectedChallengeEndTimeLabel;
        private ListView _challengeParticipantsList;
        private ListView _challengesList;
        private ScrollView _challengesScrollView;
        private ScrollView _challengeParticipantsScrollView;
        private Button _refreshButton;

        // Create Challenge modal elements
        private VisualElement _createModal;
        private DropdownField _modalTemplateDropdown;
        private TextField _modalNameField;
        private IntegerField _modalMaxParticipantsField;
        private TextField _modalInvitees;
        private SliderInt _modalChallengeDelay;
        private Label _modalChallengeDelayLabel;
        private SliderInt _modalChallengeDuration;
        private Label _modalChallengeDurationLabel;
        private Toggle _modalOpenToggle;
        private Button _modalCreateButton;
        private Button _modalCloseButton;

        // Submit Score modal elements
        private VisualElement _submitScoreModal;
        private IntegerField _scoreField;
        private IntegerField _subScoreField;
        private Button _submitScoreModalButton;
        private Button _submitScoreModalCloseButton;

        // Invite modal elements
        private VisualElement _inviteModal;
        private TextField _inviteModalInvitees;
        private Button _inviteModalButton;
        private Button _inviteModalCloseButton;

        // Error popup elements
        private VisualElement _errorPopup;
        private Button _errorCloseButton;
        private Label _errorMessage;

        // Transient state for current selection (not business state)
        private readonly List<IChallengeScore> _selectedChallengeParticipants = new();
        private IChallenge _currentChallenge;
        private int _selectedTabIndex = DefaultTabIndex;

        #endregion

        #region Initialization

        public ChallengesView(
            ChallengesController controller,
            HiroChallengesCoordinator coordinator,
            VisualTreeAsset challengeEntryTemplate,
            VisualTreeAsset challengeParticipantTemplate)
        {
            _controller = controller;
            _challengeEntryTemplate = challengeEntryTemplate;
            _challengeParticipantTemplate = challengeParticipantTemplate;

            // Subscribe to events (removed unused parameters)
            controller.OnInitialized += HandleInitialized;
            coordinator.ReceivedStartError += HandleStartError;

            Initialize(controller.GetComponent<UIDocument>().rootVisualElement);

            HideSelectedChallengePanel();
        }

        private void Initialize(VisualElement rootElement)
        {
            _walletDisplay = new WalletDisplay(rootElement.Q<VisualElement>("wallet-display"));

            InitializeTabs(rootElement);
            InitializeButtons(rootElement);
            InitializeSelectedChallengePanel(rootElement);
            InitializeLists(rootElement);
            InitializeModals(rootElement);
            InitializeErrorPopup(rootElement);
        }

        private void InitializeTabs(VisualElement rootElement)
        {
            _myChallengesTab = rootElement.Q<Button>("my-challenges-tab");
            
            _myChallengesTab.RegisterCallback<ClickEvent>(_ => OnMyChallengesTabClicked());
        }

        private async void OnMyChallengesTabClicked()
        {
            if (_selectedTabIndex == DefaultTabIndex)
                return;

            _selectedTabIndex = DefaultTabIndex;
            _myChallengesTab.AddToClassList("selected");
            await RefreshChallengesAsync();
        }

        private void InitializeButtons(VisualElement rootElement)
        {
            _createButton = rootElement.Q<Button>("challenge-create");
            _createButton.RegisterCallback<ClickEvent>(_ => ShowCreateModal());

            _joinButton = rootElement.Q<Button>("challenge-join");
            _joinButton.RegisterCallback<ClickEvent>(_ => JoinChallengeAsync());

            _leaveButton = rootElement.Q<Button>("challenge-leave");
            _leaveButton.RegisterCallback<ClickEvent>(_ => LeaveChallengeAsync());

            _claimRewardsButton = rootElement.Q<Button>("challenge-claim");
            _claimRewardsButton.RegisterCallback<ClickEvent>(_ => ClaimChallengeAsync());

            _submitScoreButton = rootElement.Q<Button>("challenge-submit-score");
            _submitScoreButton.RegisterCallback<ClickEvent>(_ => ShowSubmitScoreModal());

            _inviteButton = rootElement.Q<Button>("challenge-invite");
            _inviteButton.RegisterCallback<ClickEvent>(_ => ShowInviteModal());

            _refreshButton = rootElement.Q<Button>("challenges-refresh");
            _refreshButton.RegisterCallback<ClickEvent>(_ => OnRefreshClicked());
        }

        private async void OnRefreshClicked()
        {
            await RefreshChallengesAsync();
        }

        private void InitializeSelectedChallengePanel(VisualElement rootElement)
        {
            _selectedChallengePanel = rootElement.Q<VisualElement>("selected-challenge-panel");
            _selectedChallengeNameLabel = rootElement.Q<Label>("selected-challenge-name");
            _selectedChallengeDescriptionLabel = rootElement.Q<Label>("selected-challenge-description");
            _selectedChallengeStatusLabel = rootElement.Q<Label>("selected-challenge-status");
            _selectedChallengeEndTimeLabel = rootElement.Q<Label>("selected-challenge-end-time");
        }

        private void InitializeLists(VisualElement rootElement)
        {
            // Set up and bind participants list
            _challengeParticipantsList = rootElement.Q<ListView>("challenge-participants-list");
            _challengeParticipantsList.makeItem = () =>
            {
                var newListEntry = _challengeParticipantTemplate.Instantiate();
                var newListEntryLogic = new ChallengeParticipantView();
                newListEntry.userData = newListEntryLogic;
                newListEntryLogic.SetVisualElement(newListEntry);
                return newListEntry;
            };
            _challengeParticipantsList.bindItem = (item, index) =>
            {
                (item.userData as ChallengeParticipantView)?.SetChallengeParticipant(
                    _currentChallenge,
                    _selectedChallengeParticipants[index]);
            };
            _challengeParticipantsList.itemsSource = _selectedChallengeParticipants;

            _challengeParticipantsScrollView = _challengeParticipantsList.Q<ScrollView>();
            _challengeParticipantsScrollView.verticalScrollerVisibility = ScrollerVisibility.AlwaysVisible;

            // Set up and bind challenges list
            _challengesList = rootElement.Q<ListView>("challenges-list");
            _challengesList.makeItem = () =>
            {
                var newListEntry = _challengeEntryTemplate.Instantiate();
                var newListEntryLogic = new ChallengeView();
                newListEntry.userData = newListEntryLogic;
                newListEntryLogic.SetVisualElement(newListEntry);
                return newListEntry;
            };
            _challengesList.bindItem = (item, index) =>
            {
                (item.userData as ChallengeView)?.SetChallenge(_controller.Challenges[index]);
            };
            _challengesList.itemsSource = _controller.Challenges;
            
            _challengesList.selectionChanged += _ => OnChallengeSelectionChanged();

            _challengesScrollView = _challengesList.Q<ScrollView>();
            _challengesScrollView.verticalScrollerVisibility = ScrollerVisibility.AlwaysVisible;
        }

        private async void OnChallengeSelectionChanged()
        {
            await SelectChallengeAsync();
        }

        private void InitializeModals(VisualElement rootElement)
        {
            InitializeCreateModal(rootElement);
            InitializeSubmitScoreModal(rootElement);
            InitializeInviteModal(rootElement);
        }

        private void InitializeCreateModal(VisualElement rootElement)
        {
            _createModal = rootElement.Q<VisualElement>("create-modal");
            if (_createModal == null)
            {
                Debug.LogError("Missing UI element: 'create-modal'");
                return;
            }

            _modalTemplateDropdown = _createModal.Q<DropdownField>("create-modal-template");
            if (_modalTemplateDropdown != null)
            {
                _modalTemplateDropdown.RegisterValueChangedCallback(evt =>
                {
                    var template = _controller.GetTemplate(evt.newValue != null ? _modalTemplateDropdown.index : 0);
                    if (template != null)
                        UpdateCreateModalLimits(template);
                });
            }

            _modalNameField = _createModal.Q<TextField>("create-modal-name");
            _modalMaxParticipantsField = _createModal.Q<IntegerField>("create-modal-max-participants");
            _modalInvitees = _createModal.Q<TextField>("create-modal-invitees");

            _modalChallengeDelay = _createModal.Q<SliderInt>("create-modal-delay");
            _modalChallengeDelayLabel = _createModal.Q<Label>("create-modal-delay-value");
            if (_modalChallengeDelay != null && _modalChallengeDelayLabel != null)
            {
                _modalChallengeDelay.RegisterValueChangedCallback(evt =>
                {
                    _modalChallengeDelayLabel.text = $"{evt.newValue}s";
                });
            }

            _modalChallengeDuration = _createModal.Q<SliderInt>("create-modal-duration");
            _modalChallengeDurationLabel = _createModal.Q<Label>("create-modal-duration-value");
            if (_modalChallengeDuration != null && _modalChallengeDurationLabel != null)
            {
                _modalChallengeDuration.RegisterValueChangedCallback(evt =>
                {
                    _modalChallengeDurationLabel.text = $"{evt.newValue}s";
                });
            }

            _modalOpenToggle = _createModal.Q<Toggle>("create-modal-open");

            _modalCreateButton = _createModal.Q<Button>("create-modal-create");
            if (_modalCreateButton != null)
            {
                _modalCreateButton.RegisterCallback<ClickEvent>(_ => OnCreateConfirmClicked());
            }

            _modalCloseButton = _createModal.Q<Button>("create-modal-close");
            if (_modalCloseButton != null)
            {
                _modalCloseButton.RegisterCallback<ClickEvent>(_ => HideCreateModal());
            }
        }

        private async void OnCreateConfirmClicked()
        {
            await CreateChallengeAsync();
        }

        private void InitializeSubmitScoreModal(VisualElement rootElement)
        {
            _submitScoreModal = rootElement.Q<VisualElement>("submit-score-modal");
            if (_submitScoreModal == null)
            {
                Debug.LogError("Missing UI element: 'submit-score-modal'");
                return;
            }

            _scoreField = _submitScoreModal.Q<IntegerField>("submit-score-score");
            _subScoreField = _submitScoreModal.Q<IntegerField>("submit-score-subscore");

            _submitScoreModalButton = _submitScoreModal.Q<Button>("submit-score-modal-submit");
            if (_submitScoreModalButton != null)
            {
                _submitScoreModalButton.RegisterCallback<ClickEvent>(_ => SubmitScoreAsync());
            }

            _submitScoreModalCloseButton = _submitScoreModal.Q<Button>("submit-score-modal-close");
            if (_submitScoreModalCloseButton != null)
            {
                _submitScoreModalCloseButton.RegisterCallback<ClickEvent>(_ => HideSubmitScoreModal());
            }
        }

        private void InitializeInviteModal(VisualElement rootElement)
        {
            _inviteModal = rootElement.Q<VisualElement>("invite-modal");
            if (_inviteModal == null)
            {
                Debug.LogError("Missing UI element: 'invite-modal'");
                return;
            }

            _inviteModalInvitees = _inviteModal.Q<TextField>("invite-modal-invitees");

            _inviteModalButton = _inviteModal.Q<Button>("invite-modal-invite");
            if (_inviteModalButton != null)
            {
                _inviteModalButton.RegisterCallback<ClickEvent>(_ => OnInviteConfirmClicked());
            }

            _inviteModalCloseButton = _inviteModal.Q<Button>("invite-modal-close");
            if (_inviteModalCloseButton != null)
            {
                _inviteModalCloseButton.RegisterCallback<ClickEvent>(_ => HideInviteModal());
            }
        }

        private async void OnInviteConfirmClicked()
        {
            await InviteUsersAsync();
        }

        private void InitializeErrorPopup(VisualElement rootElement)
        {
            _errorPopup = rootElement.Q<VisualElement>("error-popup");
            if (_errorPopup == null)
            {
                Debug.LogError("Missing UI element: 'error-popup'");
                return;
            }

            _errorMessage = _errorPopup.Q<Label>("error-message");
            _errorCloseButton = _errorPopup.Q<Button>("error-close");
            if (_errorCloseButton != null)
            {
                _errorCloseButton.RegisterCallback<ClickEvent>(_ => HideErrorPopup());
            }
        }

        private async void HandleInitialized(Nakama.ISession session, ChallengesController controller)
        {
            _walletDisplay.StartObserving();
            await LoadTemplatesAsync();
        }

        private void HandleStartError(Exception e)
        {
            ShowError(e.Message);
        }

        #endregion

        #region Challenge List Management

        public async Task RefreshChallengesAsync()
        {
            try
            {
                var refreshResult = await _controller.RefreshChallengesAsync();
                _challengesList.RefreshItems();

                // If a previously selected challenge still exists, reselect it
                if (refreshResult != null)
                {
                    _challengesList.selectedIndex = refreshResult.SelectedChallengeIndex;
                    UpdateSelectedChallengePanel(
                        _controller.Challenges[refreshResult.SelectedChallengeIndex],
                        refreshResult.Participants);
                }
            }
            catch (Exception e)
            {
                ShowError(e.Message);
                Debug.Log(e);
            }
        }

        private async Task SelectChallengeAsync()
        {
            try
            {
                if (_challengesList.selectedIndex == -1)
                {
                    HideSelectedChallengePanel();
                    return;
                }

                var selectedChallenge = _controller.Challenges[_challengesList.selectedIndex];
                var participants = await _controller.SelectChallengeAsync(selectedChallenge);

                UpdateSelectedChallengePanel(selectedChallenge, participants);
            }
            catch (Exception e)
            {
                ShowError(e.Message);
                Debug.Log(e);
            }
        }

        /// <summary>
        /// Updates the selected challenge panel with challenge details and participants.
        /// Reads view state from controller to determine button visibility.
        /// </summary>
        private void UpdateSelectedChallengePanel(IChallenge challenge, List<IChallengeScore> participants)
        {
            _currentChallenge = challenge;
            _selectedChallengeParticipants.Clear();
            _selectedChallengeParticipants.AddRange(participants);

            _selectedChallengeNameLabel.text = challenge.Name;
            _selectedChallengeDescriptionLabel.text = challenge.Description;

            // Convert status to readable string
            var now = DateTimeOffset.Now;
            var startTime = DateTimeOffset.FromUnixTimeSeconds(challenge.StartTimeSec);
            var difference = startTime - now;

            if (difference.Seconds > 0)
            {
                _selectedChallengeStatusLabel.text =
                    $"Starting in {difference.Days}d, {difference.Hours}h, {difference.Minutes}m";
                _selectedChallengeStatusLabel.style.color = new StyleColor(Color.yellow);
            }
            else
            {
                _selectedChallengeStatusLabel.text = challenge.IsActive ? "Active" : "Ended";
                _selectedChallengeStatusLabel.style.color = challenge.IsActive
                    ? new StyleColor(Color.green)
                    : new StyleColor(Color.red);
            }

            var endTime = DateTimeOffset.FromUnixTimeSeconds(challenge.EndTimeSec).LocalDateTime;
            _selectedChallengeEndTimeLabel.text = endTime.ToString("MMM dd, HH:mm");

            _challengeParticipantsList.RefreshItems();
            ShowSelectedChallengePanel();

            // Update button states based on controller's view state
            UpdateButtonStates();
        }

        /// <summary>
        /// Updates button visibility based on the view state from the controller.
        /// </summary>
        private void UpdateButtonStates()
        {
            var viewState = _controller.ViewState;

            _joinButton.SetDisplay(viewState.ShowJoinButton);
            _leaveButton.SetDisplay(viewState.ShowLeaveButton);
            _submitScoreButton.SetDisplay(viewState.ShowSubmitScoreButton);
            _submitScoreButton.text = viewState.SubmitScoreText;
            _inviteButton.SetDisplay(viewState.ShowInviteButton);
            _claimRewardsButton.SetDisplay(viewState.ShowClaimRewardsButton);
        }

        private void ShowSelectedChallengePanel()
        {
            _selectedChallengePanel.Show();
        }

        private void HideSelectedChallengePanel()
        {
            _selectedChallengePanel.Hide();
        }

        #endregion

        #region Challenge Action Handlers

        private async void JoinChallengeAsync()
        {
            try
            {
                await _controller.JoinChallengeAsync();
                await RefreshChallengesAsync();
            }
            catch (Exception e)
            {
                ShowError(e.Message);
                Debug.Log(e);
            }
        }

        private async void LeaveChallengeAsync()
        {
            try
            {
                await _controller.LeaveChallengeAsync();
                _challengesList.ClearSelection();
                await RefreshChallengesAsync();
            }
            catch (Exception e)
            {
                ShowError(e.Message);
                Debug.Log(e);
            }
        }

        private async void ClaimChallengeAsync()
        {
            try
            {
                await _controller.ClaimChallengeAsync();
                await RefreshChallengesAsync();
            }
            catch (Exception e)
            {
                ShowError(e.Message);
                Debug.Log(e);
            }
        }

        #endregion

        #region Create Challenge Modal

        private async Task LoadTemplatesAsync()
        {
            try
            {
                var templateNames = await _controller.LoadChallengeTemplatesAsync();
                _modalTemplateDropdown.choices = templateNames;

                if (templateNames.Count > 0)
                {
                    _modalTemplateDropdown.index = 0;
                    var firstTemplate = _controller.GetTemplate(0);
                    if (firstTemplate != null)
                        UpdateCreateModalLimits(firstTemplate);
                }
            }
            catch (Exception e)
            {
                ShowError(e.Message);
                Debug.Log(e);
            }
        }

        private void ShowCreateModal()
        {
            ResetCreateModalInputs();
            _createModal?.Show();
        }

        private void HideCreateModal()
        {
            _createModal?.Hide();
        }

        private async Task CreateChallengeAsync()
        {
            try
            {
                await _controller.CreateChallengeAsync(
                    _modalTemplateDropdown.index,
                    _modalNameField.value,
                    _modalMaxParticipantsField.value,
                    _modalInvitees.value,
                    _modalChallengeDelay.value,
                    _modalChallengeDuration.value,
                    _modalOpenToggle.value
                );
                
                HideCreateModal();
                await RefreshChallengesAsync();
            }
            catch (Exception e)
            {
                ShowError(e.Message);
                Debug.Log(e);
            }
        }

        private void ResetCreateModalInputs()
        {
            var defaults = _controller.GetCreationDefaults();
            
            _modalNameField.value = string.Empty;
            _modalMaxParticipantsField.value = defaults.MaxParticipants;
            _modalInvitees.value = string.Empty;
            _modalChallengeDelay.value = defaults.DelaySeconds;
            _modalChallengeDuration.value = defaults.DurationSeconds;
            _modalOpenToggle.value = false;

            if (_modalTemplateDropdown.choices.Count > 0)
                _modalTemplateDropdown.index = 0;
        }

        /// <summary>
        /// Updates slider and field constraints based on selected challenge template limits.
        /// </summary>
        private void UpdateCreateModalLimits(IChallengeTemplate template)
        {
            var maxDelay = template.StartDelayMax;
            _modalChallengeDelay.highValue = (int)maxDelay;
            _modalChallengeDelay.value = (int)Mathf.Clamp(_modalChallengeDelay.value, 0, maxDelay);
            _modalChallengeDelayLabel.text = $"{_modalChallengeDelay.value}s";

            var minDuration = template.Duration.MinSec;
            var maxDuration = template.Duration.MaxSec;
            _modalChallengeDuration.lowValue = (int)minDuration;
            _modalChallengeDuration.highValue = (int)maxDuration;
            _modalChallengeDuration.value = (int)Mathf.Clamp(
                _modalChallengeDuration.value,
                minDuration,
                maxDuration);
            _modalChallengeDurationLabel.text = $"{_modalChallengeDuration.value}s";

            _modalMaxParticipantsField.value = (int)Mathf.Clamp(
                _modalMaxParticipantsField.value,
                template.Players.Min,
                template.Players.Max);
        }

        #endregion

        #region Submit Score Modal

        private void ShowSubmitScoreModal()
        {
            if (_scoreField != null) _scoreField.value = 0;
            if (_subScoreField != null) _subScoreField.value = 0;
            _submitScoreModal?.Show();
        }

        private void HideSubmitScoreModal()
        {
            _submitScoreModal?.Hide();
        }

        private async void SubmitScoreAsync()
        {
            try
            {
                await _controller.SubmitScoreAsync(_scoreField.value, _subScoreField.value);
                HideSubmitScoreModal();
                await RefreshChallengesAsync();
            }
            catch (Exception e)
            {
                ShowError(e.Message);
                Debug.Log(e);
            }
        }

        #endregion

        #region Invite Modal

        private void ShowInviteModal()
        {
            if (_inviteModalInvitees != null)
            {
                _inviteModalInvitees.value = string.Empty;
            }
            _inviteModal?.Show();
        }

        private void HideInviteModal()
        {
            _inviteModal?.Hide();
        }

        private async Task InviteUsersAsync()
        {
            try
            {
                await _controller.InviteToChallengeAsync(_inviteModalInvitees.value);
                HideInviteModal();
                await RefreshChallengesAsync();
            }
            catch (Exception e)
            {
                ShowError(e.Message);
                Debug.Log(e);
            }
        }

        #endregion

        #region Error Handling

        private void ShowError(string message)
        {
            if (_errorMessage != null)
            {
                _errorMessage.text = message;
            }
            
            if (_errorPopup != null)
            {
                _errorPopup.Show();
            }
            else
            {
                Debug.LogError($"Challenge Error: {message}");
            }
        }

        private void HideErrorPopup()
        {
            _errorPopup?.Hide();
        }

        #endregion

        #region Helper Methods

        private void HideAllModals()
        {
            HideCreateModal();
            HideSubmitScoreModal();
            HideInviteModal();
        }

        #endregion
    }

    /// <summary>
    /// Extension methods for UI element visibility.
    /// </summary>
    public static class UIElementExtensions
    {
        public static void Show(this VisualElement element)
        {
            element.style.display = DisplayStyle.Flex;
        }

        public static void Hide(this VisualElement element)
        {
            element.style.display = DisplayStyle.None;
        }

        public static void SetDisplay(this VisualElement element, bool visible)
        {
            element.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;
        }
    }
}