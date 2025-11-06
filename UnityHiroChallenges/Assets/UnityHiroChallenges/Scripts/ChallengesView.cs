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
using Hiro;
using UnityEngine;
using UnityEngine.UIElements;
using HeroicUI;

namespace HiroChallenges
{
    public sealed class ChallengesView
    {
        private readonly VisualTreeAsset _challengeEntryTemplate;
        private readonly VisualTreeAsset _challengeParticipantTemplate;
        private ChallengesController _controller;

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

        private VisualElement _submitScoreModal;
        private IntegerField _scoreField;
        private IntegerField _subScoreField;
        private TextField _scoreMetadataField;
        private Button _submitScoreModalButton;
        private Button _submitScoreModalCloseButton;

        private VisualElement _inviteModal;
        private TextField _inviteModalInvitees;
        private Button _inviteModalButton;
        private Button _inviteModalCloseButton;

        private VisualElement _errorPopup;
        private Button _errorCloseButton;
        private Label _errorMessage;

        private readonly List<IChallenge> _challenges = new();
        private readonly List<IChallengeScore> _selectedChallengeParticipants = new();
        private IChallenge _currentChallenge;

        #region Initialization

        public ChallengesView(VisualTreeAsset challengeEntryTemplate, VisualTreeAsset challengeParticipantTemplate)
        {
            _challengeEntryTemplate = challengeEntryTemplate;
            _challengeParticipantTemplate = challengeParticipantTemplate;
        }

        public void SetController(ChallengesController controller)
        {
            _controller = controller;
        }

        public void Initialize(VisualElement rootElement)
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
            _myChallengesTab.RegisterCallback<ClickEvent>(evt => _controller?.SwitchToMyChallengesTab());
        }

        private void InitializeButtons(VisualElement rootElement)
        {
            _createButton = rootElement.Q<Button>("challenge-create");
            _createButton.RegisterCallback<ClickEvent>(_ => ShowCreateModal());

            _joinButton = rootElement.Q<Button>("challenge-join");
            _joinButton.RegisterCallback<ClickEvent>(_ => _controller?.JoinChallenge());

            _leaveButton = rootElement.Q<Button>("challenge-leave");
            _leaveButton.RegisterCallback<ClickEvent>(_ => _controller?.LeaveChallenge());

            _claimRewardsButton = rootElement.Q<Button>("challenge-claim");
            _claimRewardsButton.RegisterCallback<ClickEvent>(_ => _controller?.ClaimChallenge());

            _submitScoreButton = rootElement.Q<Button>("challenge-submit-score");
            _submitScoreButton.RegisterCallback<ClickEvent>(_ => ShowSubmitScoreModal());

            _inviteButton = rootElement.Q<Button>("challenge-invite");
            _inviteButton.RegisterCallback<ClickEvent>(_ => ShowInviteModal());

            _refreshButton = rootElement.Q<Button>("challenges-refresh");
            _refreshButton.RegisterCallback<ClickEvent>(_ => _controller?.RefreshChallenges());
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
            // Challenge participants list
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

            // Challenges list
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
                (item.userData as ChallengeView)?.SetChallenge(_challenges[index]);
            };
            _challengesList.itemsSource = _challenges;
            _challengesList.selectionChanged += objects =>
            {
                if (_challengesList.selectedItem is IChallenge challenge)
                {
                    _controller?.OnChallengeSelected(challenge);
                }
            };

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
            _modalTemplateDropdown.RegisterValueChangedCallback(_ => _controller?.OnTemplateChanged(_modalTemplateDropdown.index));
            
            _modalNameField = rootElement.Q<TextField>("create-modal-name");
            _modalMaxParticipantsField = rootElement.Q<IntegerField>("create-modal-max-participants");
            _modalMaxParticipantsField.RegisterCallback<FocusOutEvent>(_ => _controller?.OnMaxParticipantsChanged(_modalTemplateDropdown.index));
            
            _modalInvitees = rootElement.Q<TextField>("create-modal-invitees");
            _modalOpenToggle = rootElement.Q<Toggle>("create-modal-open");

            _modalChallengeDelay = rootElement.Q<SliderInt>("create-modal-delay");
            _modalChallengeDelayLabel = rootElement.Q<Label>("create-modal-delay-value");
            _modalChallengeDelay.RegisterValueChangedCallback(evt =>
            {
                _modalChallengeDelayLabel.text = $"{evt.newValue}s";
            });
            _modalChallengeDelayLabel.text = $"{_modalChallengeDelay.value}s";

            _modalChallengeDuration = rootElement.Q<SliderInt>("create-modal-duration");
            _modalChallengeDurationLabel = rootElement.Q<Label>("create-modal-duration-value");
            _modalChallengeDuration.RegisterValueChangedCallback(evt =>
            {
                _modalChallengeDurationLabel.text = $"{evt.newValue}s";
            });
            _modalChallengeDurationLabel.text = $"{_modalChallengeDuration.value}s";

            _modalCreateButton = rootElement.Q<Button>("create-modal-create");
            _modalCreateButton.RegisterCallback<ClickEvent>(_ => 
            {
                _controller?.CreateChallenge(
                    _modalTemplateDropdown.index,
                    _modalNameField.value,
                    _modalMaxParticipantsField.value,
                    _modalInvitees.value,
                    _modalChallengeDelay.value,
                    _modalChallengeDuration.value,
                    _modalOpenToggle.value
                );
            });
            
            _modalCloseButton = rootElement.Q<Button>("create-modal-close");
            _modalCloseButton.RegisterCallback<ClickEvent>(_ => HideCreateModal());
        }

        private void InitializeSubmitScoreModal(VisualElement rootElement)
        {
            _submitScoreModal = rootElement.Q<VisualElement>("submit-score-modal");
            _scoreField = rootElement.Q<IntegerField>("submit-score-score");
            _subScoreField = rootElement.Q<IntegerField>("submit-score-subscore");
            _scoreMetadataField = rootElement.Q<TextField>("submit-score-metadata");
            
            _submitScoreModalButton = rootElement.Q<Button>("submit-score-modal-submit");
            _submitScoreModalButton.RegisterCallback<ClickEvent>(_ => 
            {
                _controller?.SubmitScore(
                    _scoreField.value,
                    _subScoreField.value,
                    _scoreMetadataField.value
                );
            });
            
            _submitScoreModalCloseButton = rootElement.Q<Button>("submit-score-modal-close");
            _submitScoreModalCloseButton.RegisterCallback<ClickEvent>(_ => HideSubmitScoreModal());
        }

        private void InitializeInviteModal(VisualElement rootElement)
        {
            _inviteModal = rootElement.Q<VisualElement>("invite-modal");
            _inviteModalInvitees = rootElement.Q<TextField>("invite-modal-invitees");
            
            _inviteModalButton = rootElement.Q<Button>("invite-modal-invite");
            _inviteModalButton.RegisterCallback<ClickEvent>(_ => 
            {
                _controller?.InviteToChallenge(_inviteModalInvitees.value);
            });
            
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

        #endregion

        #region Challenge List Management

        public void SetChallenges(IEnumerable<IChallenge> challenges)
        {
            _challenges.Clear();
            _challenges.AddRange(challenges);
            _challengesList.RefreshItems();
        }

        public void ClearChallengeSelection()
        {
            _challengesList.ClearSelection();
        }

        public void SelectChallenge(int index)
        {
            _challengesList.SetSelection(index);
        }

        #endregion

        #region Selected Challenge Panel

        public void SetSelectedChallengeParticipants(IChallenge challenge, IEnumerable<IChallengeScore> participants)
        {
            _currentChallenge = challenge;
            _selectedChallengeParticipants.Clear();
            _selectedChallengeParticipants.AddRange(participants);
            _challengeParticipantsList.RefreshItems();
        }

        public void ShowSelectedChallengePanel(IChallenge challenge)
        {
            _selectedChallengeNameLabel.text = challenge.Name;
            _selectedChallengeDescriptionLabel.text = string.IsNullOrEmpty(challenge.Description)
                ? "No description set."
                : challenge.Description;

            var now = DateTimeOffset.Now;
            var startTime = DateTimeOffset.FromUnixTimeSeconds(challenge.StartTimeSec);
            var endTime = DateTimeOffset.FromUnixTimeSeconds(challenge.EndTimeSec);
            var difference = startTime - now;
            
            if (difference.Seconds > 0)
            {
                _selectedChallengeStatusLabel.text = $"Starting in {difference.Days}d, {difference.Hours}h, {difference.Minutes}m";
                _selectedChallengeStatusLabel.style.color = new StyleColor(Color.orange);
            }
            else
            {
                _selectedChallengeStatusLabel.text = challenge.IsActive ? "Active" : "Ended";
                _selectedChallengeStatusLabel.style.color = challenge.IsActive ? new StyleColor(Color.green) : new StyleColor(Color.red);
            }
            
            _selectedChallengeEndTimeLabel.text = endTime.LocalDateTime.ToString("MMM dd, yyyy HH:mm");
            _selectedChallengePanel.style.display = DisplayStyle.Flex;
        }

        public void HideSelectedChallengePanel()
        {
            _selectedChallengePanel.style.display = DisplayStyle.None;
        }

        public void UpdateChallengeButtons(bool showJoin, bool showLeave, bool showSubmitScore, 
            string submitScoreText, bool showInvite, bool showClaimRewards)
        {
            _joinButton.style.display = showJoin ? DisplayStyle.Flex : DisplayStyle.None;
            _leaveButton.style.display = showLeave ? DisplayStyle.Flex : DisplayStyle.None;
            _submitScoreButton.style.display = showSubmitScore ? DisplayStyle.Flex : DisplayStyle.None;
            _submitScoreButton.text = submitScoreText;
            _inviteButton.style.display = showInvite ? DisplayStyle.Flex : DisplayStyle.None;
            _claimRewardsButton.style.display = showClaimRewards ? DisplayStyle.Flex : DisplayStyle.None;
        }

        #endregion

        #region Modals

        // Create Modal
        public void ShowCreateModal()
        {
            ResetCreateModalInputs();
            _createModal.style.display = DisplayStyle.Flex;
        }

        public void HideCreateModal()
        {
            _createModal.style.display = DisplayStyle.None;
        }

        private void ResetCreateModalInputs()
        {
            _modalNameField.value = string.Empty;
            _modalMaxParticipantsField.value = 100;
            _modalInvitees.value = string.Empty;
            _modalChallengeDelay.value = 0;
            _modalChallengeDuration.value = 2000;
            _modalOpenToggle.value = false;

            if (_modalTemplateDropdown.choices.Count > 0)
            {
                _modalTemplateDropdown.index = 0;
            }
        }

        public void SetTemplateChoices(List<string> choices)
        {
            _modalTemplateDropdown.choices = choices;
        }

        public void UpdateCreateModalLimits(IChallengeTemplate template)
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

        // Submit Score Modal
        public void ShowSubmitScoreModal()
        {
            _scoreField.value = 0;
            _subScoreField.value = 0;
            _scoreMetadataField.value = string.Empty;
            _submitScoreModal.style.display = DisplayStyle.Flex;
        }

        public void HideSubmitScoreModal()
        {
            _submitScoreModal.style.display = DisplayStyle.None;
        }

        // Invite Modal
        public void ShowInviteModal()
        {
            _inviteModalInvitees.value = string.Empty;
            _inviteModal.style.display = DisplayStyle.Flex;
        }

        public void HideInviteModal()
        {
            _inviteModal.style.display = DisplayStyle.None;
        }

        // Error Popup
        public void ShowError(string message)
        {
            _errorMessage.text = message;
            _errorPopup.style.display = DisplayStyle.Flex;
        }

        public void HideErrorPopup()
        {
            _errorPopup.style.display = DisplayStyle.None;
        }

        public void HideAllModals()
        {
            HideCreateModal();
            HideSubmitScoreModal();
            HideInviteModal();
        }

        #endregion

        #region Wallet & Tabs

        public void StartObservingWallet()
        {
            _walletDisplay.StartObserving();
        }

        public void SetMyChallengesTabSelected(bool selected)
        {
            if (selected)
            {
                _myChallengesTab.AddToClassList("selected");
            }
            else
            {
                _myChallengesTab.RemoveFromClassList("selected");
            }
        }

        #endregion
    }
}