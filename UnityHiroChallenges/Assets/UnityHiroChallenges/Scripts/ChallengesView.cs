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
using Hiro.System;
using Hiro.Unity;
using UnityEngine;
using UnityEngine.UIElements;
using HeroicUI;

namespace HiroChallenges
{
    /// <summary>
    /// View for the Challenges system.
    /// Manages UI presentation and user interactions, delegates all business logic to Controller.
    /// Observes Hiro systems directly for updates.
    /// </summary>
    public sealed class ChallengesView : IDisposable
    {
        private const int DefaultTabIndex = 0;

        private readonly ChallengesController _controller;
        private readonly VisualTreeAsset _challengeEntryTemplate;
        private readonly VisualTreeAsset _challengeParticipantTemplate;

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
        private Button _submitScoreModalButton;
        private Button _submitScoreModalCloseButton;

        private VisualElement _inviteModal;
        private TextField _inviteModalInvitees;
        private Button _inviteModalButton;
        private Button _inviteModalCloseButton;

        private VisualElement _errorPopup;
        private Button _errorCloseButton;
        private Label _errorMessage;

        private readonly List<IChallengeScore> _selectedChallengeParticipants = new();
        private IChallenge _currentChallenge;
        private int _selectedTabIndex = DefaultTabIndex;

        private IDisposable _challengesSystemObserver;
        private IDisposable _nakamaSystemObserver;

        public ChallengesView(
            ChallengesController controller,
            VisualElement rootElement,
            VisualTreeAsset challengeEntryTemplate,
            VisualTreeAsset challengeParticipantTemplate)
        {
            _controller = controller;
            _challengeEntryTemplate = challengeEntryTemplate;
            _challengeParticipantTemplate = challengeParticipantTemplate;

            Initialize(rootElement);
            HideSelectedChallengePanel();

            var coordinator = HiroCoordinator.Instance as HiroChallengesCoordinator;
            coordinator.ReceivedStartSuccess += OnCoordinatorReady;
        }

        private async void OnCoordinatorReady()
        {
            var coordinator = HiroCoordinator.Instance as HiroChallengesCoordinator;
            coordinator.ReceivedStartSuccess -= OnCoordinatorReady;

            // Wait for controller to initialize its systems
            while (!_controller.IsInitialized)
                await Task.Yield();

            _walletDisplay.StartObserving();

            var challengesSystem = coordinator.GetSystem<ChallengesSystem>();
            var nakamaSystem = coordinator.GetSystem<NakamaSystem>();

            _challengesSystemObserver = SystemObserver<ChallengesSystem>.Create(challengesSystem, OnChallengesSystemUpdated);
            _nakamaSystemObserver = SystemObserver<NakamaSystem>.Create(nakamaSystem, OnNakamaSystemUpdated);

            await LoadTemplatesAsync();
            await RefreshChallengesAsync();
        }

        private void OnChallengesSystemUpdated(ChallengesSystem system)
        {
            _ = RefreshChallengesAsync();
        }

        private void OnNakamaSystemUpdated(NakamaSystem system)
        {
            _ = RefreshChallengesAsync();
        }

        public void Dispose()
        {
            _challengesSystemObserver?.Dispose();
            _nakamaSystemObserver?.Dispose();
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
            _createModal = rootElement.RequireElement<VisualElement>("create-modal");

            _modalTemplateDropdown = _createModal.RequireElement<DropdownField>("create-modal-template");
            _modalTemplateDropdown.RegisterValueChangedCallback(evt =>
            {
                var template = _controller.GetTemplate(evt.newValue != null ? _modalTemplateDropdown.index : 0);
                if (template != null)
                    UpdateCreateModalLimits(template);
            });

            _modalNameField = _createModal.RequireElement<TextField>("create-modal-name");
            _modalMaxParticipantsField = _createModal.RequireElement<IntegerField>("create-modal-max-participants");

            _modalInvitees = _createModal.RequireElement<TextField>("create-modal-invitees");
            RegisterAutocompleteHandler(_modalInvitees);

            _modalChallengeDelay = _createModal.RequireElement<SliderInt>("create-modal-delay");
            _modalChallengeDelayLabel = _createModal.RequireElement<Label>("create-modal-delay-value");
            _modalChallengeDelay.RegisterValueChangedCallback(evt =>
            {
                _modalChallengeDelayLabel.text = $"{evt.newValue}s";
            });

            _modalChallengeDuration = _createModal.RequireElement<SliderInt>("create-modal-duration");
            _modalChallengeDurationLabel = _createModal.RequireElement<Label>("create-modal-duration-value");
            _modalChallengeDuration.RegisterValueChangedCallback(evt =>
            {
                _modalChallengeDurationLabel.text = $"{evt.newValue}s";
            });

            _modalOpenToggle = _createModal.RequireElement<Toggle>("create-modal-open");

            _modalCreateButton = _createModal.RequireElement<Button>("create-modal-create");
            _modalCreateButton.RegisterCallback<ClickEvent>(_ => OnCreateConfirmClicked());

            _modalCloseButton = _createModal.RequireElement<Button>("create-modal-close");
            _modalCloseButton.RegisterCallback<ClickEvent>(_ => HideCreateModal());
        }

        private async void OnCreateConfirmClicked()
        {
            await CreateChallengeAsync();
        }

        private void InitializeSubmitScoreModal(VisualElement rootElement)
        {
            _submitScoreModal = rootElement.RequireElement<VisualElement>("submit-score-modal");

            _scoreField = _submitScoreModal.RequireElement<IntegerField>("submit-score-score");
            _subScoreField = _submitScoreModal.RequireElement<IntegerField>("submit-score-subscore");

            _submitScoreModalButton = _submitScoreModal.RequireElement<Button>("submit-score-modal-submit");
            _submitScoreModalButton.RegisterCallback<ClickEvent>(_ => SubmitScoreAsync());

            _submitScoreModalCloseButton = _submitScoreModal.RequireElement<Button>("submit-score-modal-close");
            _submitScoreModalCloseButton.RegisterCallback<ClickEvent>(_ => HideSubmitScoreModal());
        }

        private void InitializeInviteModal(VisualElement rootElement)
        {
            _inviteModal = rootElement.RequireElement<VisualElement>("invite-modal");

            _inviteModalInvitees = _inviteModal.RequireElement<TextField>("invite-modal-invitees");
            RegisterAutocompleteHandler(_inviteModalInvitees);

            _inviteModalButton = _inviteModal.RequireElement<Button>("invite-modal-invite");
            _inviteModalButton.RegisterCallback<ClickEvent>(_ => OnInviteConfirmClicked());

            _inviteModalCloseButton = _inviteModal.RequireElement<Button>("invite-modal-close");
            _inviteModalCloseButton.RegisterCallback<ClickEvent>(_ => HideInviteModal());
        }

        private List<string> GetAutocompleteCandidates(TextField textField)
        {
            var usernames = new List<string>(_controller.GetKnownUsernames());

            // For invite modal, also exclude current participants
            if (textField == _inviteModalInvitees)
            {
                var excludeSet = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                foreach (var p in _selectedChallengeParticipants)
                {
                    if (!string.IsNullOrEmpty(p.Username))
                        excludeSet.Add(p.Username);
                }
                usernames.RemoveAll(u => excludeSet.Contains(u));
            }

            return usernames;
        }

        private void UpdateUsernamePlaceholder(TextField textField)
        {
            var usernames = GetAutocompleteCandidates(textField);
            textField.textEdition.placeholder = usernames.Count > 0
                ? string.Join(", ", usernames)
                : string.Empty;
        }

        private void RegisterAutocompleteHandler(TextField textField)
        {
            textField.RegisterCallback<KeyDownEvent>(evt =>
            {
                if (evt.keyCode != KeyCode.Tab)
                    return;

                evt.PreventDefault();
                evt.StopPropagation();

                var candidates = GetAutocompleteCandidates(textField);
                var completed = UsernameAutocomplete.Complete(textField.value, candidates);

                if (completed != textField.value)
                {
                    textField.value = completed;
                    textField.SelectRange(completed.Length, completed.Length);
                }
            }, TrickleDown.TrickleDown);
        }

        private async void OnInviteConfirmClicked()
        {
            await InviteUsersAsync();
        }

        private void InitializeErrorPopup(VisualElement rootElement)
        {
            _errorPopup = rootElement.RequireElement<VisualElement>("error-popup");
            _errorMessage = _errorPopup.RequireElement<Label>("error-message");
            _errorCloseButton = _errorPopup.RequireElement<Button>("error-close");
            _errorCloseButton.RegisterCallback<ClickEvent>(_ => HideErrorPopup());
        }

        public async Task RefreshChallengesAsync()
        {
            try
            {
                var refreshResult = await _controller.RefreshChallengesAsync();
                _challengesList.RefreshItems();

                if (refreshResult != null)
                {
                    _challengesList.selectedIndex = refreshResult.SelectedChallengeIndex;
                    UpdateSelectedChallengePanel(
                        _controller.Challenges[refreshResult.SelectedChallengeIndex],
                        refreshResult.Participants);
                }
                else
                {
                    _challengesList.ClearSelection();
                    HideSelectedChallengePanel();
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

        private void UpdateSelectedChallengePanel(IChallenge challenge, List<IChallengeScore> participants)
        {
            _currentChallenge = challenge;
            _selectedChallengeParticipants.Clear();
            _selectedChallengeParticipants.AddRange(participants);

            _selectedChallengeNameLabel.text = challenge.Name;
            _selectedChallengeDescriptionLabel.text = challenge.Description;

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

            UpdateButtonStates();
        }

        private void UpdateButtonStates()
        {
            var permissions = _controller.GetPermissions(_currentChallenge, _selectedChallengeParticipants);

            _joinButton.SetDisplay(permissions.CanJoin);
            _leaveButton.SetDisplay(permissions.CanLeave);
            _submitScoreButton.SetDisplay(permissions.CanSubmitScore);
            _submitScoreButton.text = $"Submit Score ({permissions.ScoresSubmitted}/{permissions.MaxScores})";
            _inviteButton.SetDisplay(permissions.CanInvite);
            _claimRewardsButton.SetDisplay(permissions.CanClaim);
        }

        private void ShowSelectedChallengePanel()
        {
            _selectedChallengePanel.Show();
        }

        private void HideSelectedChallengePanel()
        {
            _selectedChallengePanel.Hide();
        }

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
            UpdateUsernamePlaceholder(_modalInvitees);
            _createModal.Show();
        }

        private void HideCreateModal()
        {
            _createModal.Hide();
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

        private void ShowSubmitScoreModal()
        {
            _scoreField.value = 0;
            _subScoreField.value = 0;
            _submitScoreModal.Show();
        }

        private void HideSubmitScoreModal()
        {
            _submitScoreModal.Hide();
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

        private void ShowInviteModal()
        {
            _inviteModalInvitees.value = string.Empty;
            UpdateUsernamePlaceholder(_inviteModalInvitees);
            _inviteModal.Show();
        }

        private void HideInviteModal()
        {
            _inviteModal.Hide();
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

        public void ShowError(string message)
        {
            _errorMessage.text = message;
            _errorPopup.Show();
        }

        private void HideErrorPopup()
        {
            _errorPopup.Hide();
        }
    }

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

        public static T RequireElement<T>(this VisualElement parent, string name) where T : VisualElement
        {
            var element = parent.Q<T>(name);
            if (element == null)
                throw new InvalidOperationException($"Required UI element '{name}' of type {typeof(T).Name} not found");
            return element;
        }
    }
}
