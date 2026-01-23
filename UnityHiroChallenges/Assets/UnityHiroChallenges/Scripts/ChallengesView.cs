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

namespace HiroChallenges
{
    // Manages the UI presentation and user interactions for the challenges system.
    // Handles all UI elements including lists, modals, and button states.
    public sealed class ChallengesView
    {
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

        private readonly List<IChallengeScore> _selectedChallengeParticipants = new();
        private IChallenge _currentChallenge;
        private int _selectedTabIndex;

        #region Initialization

        public ChallengesView(ChallengesController controller, HiroChallengesCoordinator coordinator,
            VisualTreeAsset challengeEntryTemplate,
            VisualTreeAsset challengeParticipantTemplate)
        {
            _controller = controller;
            _challengeEntryTemplate = challengeEntryTemplate;
            _challengeParticipantTemplate = challengeParticipantTemplate;

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
            _myChallengesTab.RegisterCallback<ClickEvent>(evt =>
            {
                if (_selectedTabIndex == 0) return;
                _selectedTabIndex = 0;
                _myChallengesTab.AddToClassList("selected");
                _ = RefreshChallenges();
            });
        }

        private void InitializeButtons(VisualElement rootElement)
        {
            _createButton = rootElement.Q<Button>("challenge-create");
            _createButton.RegisterCallback<ClickEvent>(_ => ShowCreateModal());

            _joinButton = rootElement.Q<Button>("challenge-join");
            _joinButton.RegisterCallback<ClickEvent>(JoinChallenge);

            _leaveButton = rootElement.Q<Button>("challenge-leave");
            _leaveButton.RegisterCallback<ClickEvent>(LeaveChallenge);

            _claimRewardsButton = rootElement.Q<Button>("challenge-claim");
            _claimRewardsButton.RegisterCallback<ClickEvent>(ClaimChallenge);

            _submitScoreButton = rootElement.Q<Button>("challenge-submit-score");
            _submitScoreButton.RegisterCallback<ClickEvent>(_ => ShowSubmitScoreModal());

            _inviteButton = rootElement.Q<Button>("challenge-invite");
            _inviteButton.RegisterCallback<ClickEvent>(_ => ShowInviteModal());

            _refreshButton = rootElement.Q<Button>("challenges-refresh");
            _refreshButton.RegisterCallback<ClickEvent>(evt => _ = RefreshChallenges());
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
                (item.userData as ChallengeParticipantView)?.SetChallengeParticipant(_currentChallenge,
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
            _challengesList.selectionChanged += objects => _ = SelectChallenge();

            _challengesScrollView = _challengesList.Q<ScrollView>();
            _challengesScrollView.verticalScrollerVisibility = ScrollerVisibility.AlwaysVisible;
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
            _modalTemplateDropdown = rootElement.Q<DropdownField>("create-modal-template");
            _modalTemplateDropdown.RegisterValueChangedCallback(_ =>
            {
                var template = _controller.GetTemplate(_modalTemplateDropdown.index);
                UpdateCreateModalLimits(template);
            });

            _modalNameField = rootElement.Q<TextField>("create-modal-name");
            _modalMaxParticipantsField = rootElement.Q<IntegerField>("create-modal-max-participants");
            _modalMaxParticipantsField.RegisterCallback<FocusOutEvent>(_ =>
            {
                var template = _controller.GetTemplate(_modalTemplateDropdown.index);
                UpdateCreateModalLimits(template);
            });

            _modalInvitees = rootElement.Q<TextField>("create-modal-invitees");
            _modalOpenToggle = rootElement.Q<Toggle>("create-modal-open");

            // Set up delay slider with live label updates
            _modalChallengeDelay = rootElement.Q<SliderInt>("create-modal-delay");
            _modalChallengeDelayLabel = rootElement.Q<Label>("create-modal-delay-value");
            _modalChallengeDelay.RegisterValueChangedCallback(evt =>
            {
                _modalChallengeDelayLabel.text = $"{evt.newValue}s";
            });
            _modalChallengeDelayLabel.text = $"{_modalChallengeDelay.value}s";

            // Set up duration slider with live label updates
            _modalChallengeDuration = rootElement.Q<SliderInt>("create-modal-duration");
            _modalChallengeDurationLabel = rootElement.Q<Label>("create-modal-duration-value");
            _modalChallengeDuration.RegisterValueChangedCallback(evt =>
            {
                _modalChallengeDurationLabel.text = $"{evt.newValue}s";
            });
            _modalChallengeDurationLabel.text = $"{_modalChallengeDuration.value}s";

            _modalCreateButton = rootElement.Q<Button>("create-modal-create");
            _modalCreateButton.RegisterCallback<ClickEvent>(evt => _ = CreateChallenge());

            _modalCloseButton = rootElement.Q<Button>("create-modal-close");
            _modalCloseButton.RegisterCallback<ClickEvent>(_ => HideCreateModal());
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

        private void InitializeInviteModal(VisualElement rootElement)
        {
            _inviteModal = rootElement.Q<VisualElement>("invite-modal");
            _inviteModalInvitees = rootElement.Q<TextField>("invite-modal-invitees");

            _inviteModalButton = rootElement.Q<Button>("invite-modal-invite");
            _inviteModalButton.RegisterCallback<ClickEvent>(evt => _ = InviteUsers());

            _inviteModalCloseButton = rootElement.Q<Button>("invite-modal-close");
            _inviteModalCloseButton.RegisterCallback<ClickEvent>(_ => HideInviteModal());
        }

        private void InitializeErrorPopup(VisualElement rootElement)
        {
            _errorPopup = rootElement.Q<VisualElement>("error-popup");
            _errorMessage = rootElement.Q<Label>("error-message");
            _errorCloseButton = rootElement.Q<Button>("error-close");
            _errorCloseButton.RegisterCallback<ClickEvent>(_ => HideErrorPopup());
        }

        private async void HandleInitialized(ISession session, ChallengesController controller)
        {
            try
            {
                var choices = await controller.LoadChallengeTemplates();
                _modalTemplateDropdown.choices = choices;
            }
            catch (Exception e)
            {
                ShowError($"Failed to load challenge templates: {e.Message}");
            }

            _walletDisplay.StartObserving();
        }

        private void HandleStartError(Exception e)
        {
            ShowError(e.Message);
        }

        #endregion

        #region Challenge List Management

        public async Task RefreshChallenges()
        {
            HideAllModals();

            var refreshData = await _controller.RefreshChallenges();

            _challengesList.RefreshItems();
            _challengesList.ClearSelection();

            // Restore selection if a challenge was previously selected
            if (refreshData == null)
                HideSelectedChallengePanel();
            else
                _challengesList.SetSelection(refreshData.Item1);
        }

        private async Task SelectChallenge()
        {
            if (_challengesList.selectedItem is not IChallenge challenge) return;

            try
            {
                var participants = await _controller.SelectChallenge(challenge);

                _currentChallenge = challenge;
                _selectedChallengeParticipants.Clear();
                _selectedChallengeParticipants.AddRange(participants);
                _challengeParticipantsList.RefreshItems();

                UpdateChallengeButtons(participants);
                ShowSelectedChallengePanel(challenge);
            }
            catch (Exception e)
            {
                ShowError(e.Message);
            }
        }

        #endregion

        #region Challenge Detail Panel

        private void ShowSelectedChallengePanel(IChallenge challenge)
        {
            _selectedChallengeNameLabel.text = challenge.Name;
            _selectedChallengeDescriptionLabel.text = string.IsNullOrEmpty(challenge.Description)
                ? "No description set."
                : challenge.Description;

            // Calculate and display challenge status (starting soon, active, or ended)
            var now = DateTimeOffset.Now;
            var startTime = DateTimeOffset.FromUnixTimeSeconds(challenge.StartTimeSec);
            var endTime = DateTimeOffset.FromUnixTimeSeconds(challenge.EndTimeSec);
            var difference = startTime - now;

            if (difference.Seconds > 0)
            {
                _selectedChallengeStatusLabel.text =
                    $"Starting in {difference.Days}d, {difference.Hours}h, {difference.Minutes}m";
                _selectedChallengeStatusLabel.style.color = new StyleColor(Color.orange);
            }
            else
            {
                _selectedChallengeStatusLabel.text = challenge.IsActive ? "Active" : "Ended";
                _selectedChallengeStatusLabel.style.color =
                    challenge.IsActive ? new StyleColor(Color.green) : new StyleColor(Color.red);
            }

            _selectedChallengeEndTimeLabel.text = endTime.LocalDateTime.ToString("MMM dd, yyyy HH:mm");
            _selectedChallengePanel.style.display = DisplayStyle.Flex;
        }

        private void HideSelectedChallengePanel()
        {
            _selectedChallengePanel.style.display = DisplayStyle.None;
        }

        // Updates button visibility based on challenge state and user participation status
        private void UpdateChallengeButtons(List<IChallengeScore> participants)
        {
            var isActive = _currentChallenge.IsActive;
            IChallengeScore foundParticipant = null;

            // Find current user in participants list
            foreach (var participant in participants)
            {
                if (participant.Id != _controller.CurrentUserId || participant.State != ChallengeState.Joined) continue;
                foundParticipant = participant;
                break;
            }

            var canClaim = _currentChallenge.CanClaim;

            // Determine which buttons should be visible
            var showJoin = isActive && foundParticipant == null;
            var showLeave = !isActive && foundParticipant != null && !canClaim;
            var showSubmitScore = isActive && foundParticipant != null &&
                                  foundParticipant.NumScores < _currentChallenge.MaxNumScore;
            var submitScoreText = $"Submit Score ({foundParticipant?.NumScores}/{_currentChallenge.MaxNumScore})";
            var showInvite = isActive && foundParticipant != null &&
                             foundParticipant.Id == _currentChallenge.OwnerId &&
                             _currentChallenge.Size < _currentChallenge.MaxSize;
            var showClaimRewards = !isActive && foundParticipant != null && canClaim;

            _joinButton.style.display = showJoin ? DisplayStyle.Flex : DisplayStyle.None;
            _leaveButton.style.display = showLeave ? DisplayStyle.Flex : DisplayStyle.None;
            _submitScoreButton.style.display = showSubmitScore ? DisplayStyle.Flex : DisplayStyle.None;
            _submitScoreButton.text = submitScoreText;
            _inviteButton.style.display = showInvite ? DisplayStyle.Flex : DisplayStyle.None;
            _claimRewardsButton.style.display = showClaimRewards ? DisplayStyle.Flex : DisplayStyle.None;
        }

        #endregion

        #region Challenge Action Handlers

        private async void JoinChallenge(ClickEvent evt)
        {
            try
            {
                await _controller.JoinChallenge();
            }
            catch (Exception e)
            {
                ShowError(e.Message);
            }

            await RefreshChallenges();
        }

        private async void LeaveChallenge(ClickEvent evt)
        {
            try
            {
                await _controller.LeaveChallenge();
                _challengesList.ClearSelection();
            }
            catch (Exception e)
            {
                ShowError(e.Message);
            }

            await RefreshChallenges();
        }

        private async void ClaimChallenge(ClickEvent evt)
        {
            try
            {
                await _controller.ClaimChallenge();
            }
            catch (Exception e)
            {
                ShowError(e.Message);
            }

            await RefreshChallenges();
        }

        #endregion

        #region Create Challenge Modal

        private void ShowCreateModal()
        {
            ResetCreateModalInputs();
            _createModal.style.display = DisplayStyle.Flex;
        }

        private void HideCreateModal()
        {
            _createModal.style.display = DisplayStyle.None;
        }

        private async Task CreateChallenge()
        {
            try
            {
                await _controller.CreateChallenge(
                    _modalTemplateDropdown.index,
                    _modalNameField.value,
                    _modalMaxParticipantsField.value,
                    _modalInvitees.value,
                    _modalChallengeDelay.value,
                    _modalChallengeDuration.value,
                    _modalOpenToggle.value
                );
                HideCreateModal();
                await RefreshChallenges();
            }
            catch (Exception e)
            {
                ShowError(e.Message);
            }
        }

        private void ResetCreateModalInputs()
        {
            _modalNameField.value = string.Empty;
            _modalMaxParticipantsField.value = 100;
            _modalInvitees.value = string.Empty;
            _modalChallengeDelay.value = 0;
            _modalChallengeDuration.value = 2000;
            _modalOpenToggle.value = false;

            if (_modalTemplateDropdown.choices.Count > 0) _modalTemplateDropdown.index = 0;
        }

        // Updates slider and field constraints based on selected challenge template limits
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
            _modalChallengeDuration.value = (int)Mathf.Clamp(_modalChallengeDuration.value, minDuration, maxDuration);
            _modalChallengeDurationLabel.text = $"{_modalChallengeDuration.value}s";

            _modalMaxParticipantsField.value = (int)Mathf.Clamp(_modalMaxParticipantsField.value,
                template.Players.Min, template.Players.Max);
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

            await RefreshChallenges();
        }

        #endregion

        #region Invite Modal

        private void ShowInviteModal()
        {
            _inviteModalInvitees.value = string.Empty;
            _inviteModal.style.display = DisplayStyle.Flex;
        }

        private void HideInviteModal()
        {
            _inviteModal.style.display = DisplayStyle.None;
        }

        private async Task InviteUsers()
        {
            try
            {
                await _controller.InviteToChallenge(_inviteModalInvitees.value);
                HideInviteModal();
                await RefreshChallenges();
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
            HideCreateModal();
            HideSubmitScoreModal();
            HideInviteModal();
        }
    }
}