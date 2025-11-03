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
using Hiro.Unity;
using System.Linq;
using System.Threading.Tasks;
using HeroicUI;
using Nakama;

namespace HiroChallenges
{
    [RequireComponent(typeof(UIDocument))]
    public class HiroChallengesController : MonoBehaviour
    {
        [Header("References")] [SerializeField]
        private VisualTreeAsset challengeEntryTemplate;
        [SerializeField]
        private VisualTreeAsset challengeParticipantTemplate;

        public event Action<ISession, HiroChallengesController> OnInitialized;

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

        private int _selectedTabIndex;
        private string _selectedChallengeId;
        private IChallenge _selectedChallenge;
        private NakamaSystem _nakamaSystem;
        private IChallengesSystem _challengesSystem;
        private IEconomySystem _economySystem;
        private readonly Dictionary<string, IChallengeTemplate> _challengeTemplates = new();
        private readonly List<IChallenge> _challenges = new();
        private readonly List<IChallengeScore> _selectedChallengeParticipants = new();

        #region Initialization

        private void Start()
        {
            InitializeUI();
            var challengesCoordinator = HiroCoordinator.Instance as HiroChallengesCoordinator;
            if (challengesCoordinator == null) return;

            challengesCoordinator.ReceivedStartError += e =>
            {
                Debug.LogException(e);
                _errorPopup.style.display = DisplayStyle.Flex;
                _errorMessage.text = e.Message;
            };
            challengesCoordinator.ReceivedStartSuccess += session =>
            {
                OnInitialized?.Invoke(session, this);

                // Cache Hiro systems since we'll be accessing them often.
                _nakamaSystem = this.GetSystem<NakamaSystem>();
                _challengesSystem = this.GetSystem<ChallengesSystem>();
                _economySystem = this.GetSystem<EconomySystem>();

                // Let our wallet display know that Hiro has been initialized successfully.
                _walletDisplay.StartObserving();

                // Load your challenges, and all challenge templates, ready for starting new challenges.
                _ = UpdateChallenges();
                _ = LoadChallengeTemplates();
            };
        }

        public void SwitchComplete()
        {
            // Hide all modals when switching account.
            _createModal.style.display = DisplayStyle.None;
            _submitScoreModal.style.display = DisplayStyle.None;
            _inviteModal.style.display = DisplayStyle.None;

            // Refresh UI for the new user.
            _ = UpdateChallenges();
            _economySystem.RefreshAsync();
        }

        #endregion

        #region UI Binding

        private void InitializeUI()
        {
            var rootElement = GetComponent<UIDocument>().rootVisualElement;

            _walletDisplay = new WalletDisplay(rootElement.Q<VisualElement>("wallet-display"));

            _myChallengesTab = rootElement.Q<Button>("my-challenges-tab");
            _myChallengesTab.RegisterCallback<ClickEvent>(evt =>
            {
                if (_selectedTabIndex == 0) return;
                _selectedTabIndex = 0;
                _myChallengesTab.AddToClassList("selected");
                _ = UpdateChallenges();
            });

            _createButton = rootElement.Q<Button>("challenge-create");
            _createButton.RegisterCallback<ClickEvent>(_ =>
            {
                // Reset Challenge create inputs.
                _modalNameField.value = string.Empty;
                _modalMaxParticipantsField.value = 100;
                _modalInvitees.value = string.Empty;
                _modalChallengeDelay.value = 0;
                _modalChallengeDuration.value = 2000;
                _modalOpenToggle.value = false;

                // Reset template dropdown to the first item if available.
                if (_modalTemplateDropdown.choices.Count > 0)
                {
                    _modalTemplateDropdown.index = 0;
                }

                _createModal.style.display = DisplayStyle.Flex;
            });

            _joinButton = rootElement.Q<Button>("challenge-join");
            _joinButton.RegisterCallback<ClickEvent>(evt => _ = ChallengeJoin());

            _leaveButton = rootElement.Q<Button>("challenge-leave");
            _leaveButton.RegisterCallback<ClickEvent>(evt => _ = ChallengeLeave());

            _claimRewardsButton = rootElement.Q<Button>("challenge-claim");
            _claimRewardsButton.RegisterCallback<ClickEvent>(evt => _ = ChallengeClaim());

            _submitScoreButton = rootElement.Q<Button>("challenge-submit-score");
            _submitScoreButton.RegisterCallback<ClickEvent>(_ =>
            {
                // Reset score inputs.
                _scoreField.value = 0;
                _subScoreField.value = 0;
                _scoreMetadataField.value = string.Empty;
                _submitScoreModal.style.display = DisplayStyle.Flex;
            });

            _inviteButton = rootElement.Q<Button>("challenge-invite");
            _inviteButton.RegisterCallback<ClickEvent>(_ =>
            {
                _inviteModalInvitees.value = string.Empty;
                _inviteModal.style.display = DisplayStyle.Flex;
            });

            _selectedChallengePanel = rootElement.Q<VisualElement>("selected-challenge-panel");
            _selectedChallengeNameLabel = rootElement.Q<Label>("selected-challenge-name");
            _selectedChallengeDescriptionLabel = rootElement.Q<Label>("selected-challenge-description");
            _selectedChallengeStatusLabel = rootElement.Q<Label>("selected-challenge-status");
            _selectedChallengeEndTimeLabel = rootElement.Q<Label>("selected-challenge-end-time");

            // Challenge participants list.
            _challengeParticipantsList = rootElement.Q<ListView>("challenge-participants-list");
            _challengeParticipantsList.makeItem = () =>
            {
                var newListEntry = challengeParticipantTemplate.Instantiate();
                var newListEntryLogic = new ChallengeParticipantView();
                newListEntry.userData = newListEntryLogic;
                newListEntryLogic.SetVisualElement(newListEntry);
                return newListEntry;
            };
            _challengeParticipantsList.bindItem = (item, index) =>
            {
                (item.userData as ChallengeParticipantView)?.SetChallengeParticipant(_selectedChallenge,
                    _selectedChallengeParticipants[index]);
            };
            _challengeParticipantsList.itemsSource = _selectedChallengeParticipants;

            _challengeParticipantsScrollView = _challengeParticipantsList.Q<ScrollView>();
            _challengeParticipantsScrollView.verticalScrollerVisibility = ScrollerVisibility.AlwaysVisible;

            // Challenges list.
            _challengesList = rootElement.Q<ListView>("challenges-list");
            _challengesList.makeItem = () =>
            {
                var newListEntry = challengeEntryTemplate.Instantiate();
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
                if (_challengesList.selectedItem is IChallenge)
                {
                    _ = OnChallengeSelected(_challengesList.selectedItem as IChallenge);
                }
            };

            _challengesScrollView = _challengesList.Q<ScrollView>();
            _challengesScrollView.verticalScrollerVisibility = ScrollerVisibility.AlwaysVisible;

            _refreshButton = rootElement.Q<Button>("challenges-refresh");
            _refreshButton.RegisterCallback<ClickEvent>(evt => _ = UpdateChallenges());

            // Create modal.
            _createModal = rootElement.Q<VisualElement>("create-modal");
            _modalTemplateDropdown = rootElement.Q<DropdownField>("create-modal-template");
            _modalTemplateDropdown.RegisterValueChangedCallback(_ => { UpdateCreateModalLimits(); });
            _modalNameField = rootElement.Q<TextField>("create-modal-name");
            _modalMaxParticipantsField = rootElement.Q<IntegerField>("create-modal-max-participants");
            _modalMaxParticipantsField.RegisterCallback<FocusOutEvent>(_ =>
            {
                var template = _challengeTemplates.ElementAt(_modalTemplateDropdown.index);
                _modalMaxParticipantsField.value = (int)Mathf.Clamp(_modalMaxParticipantsField.value,
                    template.Value.Players.Min, template.Value.Players.Max);
            });
            _modalInvitees = rootElement.Q<TextField>("create-modal-invitees");
            _modalOpenToggle = rootElement.Q<Toggle>("create-modal-open");

            // Challenge delay slider.
            _modalChallengeDelay = rootElement.Q<SliderInt>("create-modal-delay");
            _modalChallengeDelayLabel = rootElement.Q<Label>("create-modal-delay-value");
            _modalChallengeDelay.RegisterValueChangedCallback(evt =>
            {
                _modalChallengeDelayLabel.text = $"{evt.newValue}s";
            });
            _modalChallengeDelayLabel.text = $"{_modalChallengeDelay.value}s";

            // Challenge duration slider.
            _modalChallengeDuration = rootElement.Q<SliderInt>("create-modal-duration");
            _modalChallengeDurationLabel = rootElement.Q<Label>("create-modal-duration-value");
            _modalChallengeDuration.RegisterValueChangedCallback(evt =>
            {
                _modalChallengeDurationLabel.text = $"{evt.newValue}s";
            });
            _modalChallengeDurationLabel.text = $"{_modalChallengeDuration.value}s";

            _modalCreateButton = rootElement.Q<Button>("create-modal-create");
            _modalCreateButton.RegisterCallback<ClickEvent>(evt => _ = ChallengeCreate());
            _modalCloseButton = rootElement.Q<Button>("create-modal-close");
            _modalCloseButton.RegisterCallback<ClickEvent>(_ => _createModal.style.display = DisplayStyle.None);

            // Submit Score modal.
            _submitScoreModal = rootElement.Q<VisualElement>("submit-score-modal");
            _scoreField = rootElement.Q<IntegerField>("submit-score-score");
            _subScoreField = rootElement.Q<IntegerField>("submit-score-subscore");
            _scoreMetadataField = rootElement.Q<TextField>("submit-score-metadata");
            _submitScoreModalButton = rootElement.Q<Button>("submit-score-modal-submit");
            _submitScoreModalButton.RegisterCallback<ClickEvent>(evt => _ = ChallengeSubmitScore());
            _submitScoreModalCloseButton = rootElement.Q<Button>("submit-score-modal-close");
            _submitScoreModalCloseButton.RegisterCallback<ClickEvent>(_ =>
                _submitScoreModal.style.display = DisplayStyle.None);

            // Invite modal.
            _inviteModal = rootElement.Q<VisualElement>("invite-modal");
            _inviteModalInvitees = rootElement.Q<TextField>("invite-modal-invitees");
            _inviteModalButton = rootElement.Q<Button>("invite-modal-invite");
            _inviteModalButton.RegisterCallback<ClickEvent>(evt => _ = ChallengeInvite());
            _inviteModalCloseButton = rootElement.Q<Button>("invite-modal-close");
            _inviteModalCloseButton.RegisterCallback<ClickEvent>(_ => _inviteModal.style.display = DisplayStyle.None);

            // Error popup.
            _errorPopup = rootElement.Q<VisualElement>("error-popup");
            _errorMessage = rootElement.Q<Label>("error-message");
            _errorCloseButton = rootElement.Q<Button>("error-close");
            _errorCloseButton.RegisterCallback<ClickEvent>(_ => _errorPopup.style.display = DisplayStyle.None);

            _ = OnChallengeSelected(null);
        }

        private void UpdateCreateModalLimits()
        {
            var template = _challengeTemplates.ElementAt(_modalTemplateDropdown.index).Value;
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
            _modalMaxParticipantsField.value = (int)Mathf.Clamp(_modalMaxParticipantsField.value, template.Players.Min,
                template.Players.Max);
        }

        private async Task LoadChallengeTemplates()
        {
            try
            {
                _challengeTemplates.Clear();
                var loadedTemplates = (await _challengesSystem.GetTemplatesAsync()).Templates;
                var challengeTemplateNames = new List<string>();
                foreach (var template in loadedTemplates)
                {
                    _challengeTemplates[template.Key] = template.Value;
                    challengeTemplateNames.Add(
                        template.Value.AdditionalProperties.TryGetValue("display_name", out var displayName)
                            ? displayName
                            : template.Key);
                }

                // Populate dropdown with template names
                _modalTemplateDropdown.choices = challengeTemplateNames;
            }
            catch (Exception e)
            {
                _errorPopup.style.display = DisplayStyle.Flex;
                _errorMessage.text = $"Failed to load challenge templates: {e.Message}";
            }
        }

        private async Task OnChallengeSelected(IChallenge challenge)
        {
            if (challenge == null)
            {
                _selectedChallengeId = string.Empty;
                _selectedChallengePanel.style.display = DisplayStyle.None;
                return;
            }

            _selectedChallenge = challenge;
            _selectedChallengeId = _selectedChallenge.Id;

            _selectedChallengeNameLabel.text = _selectedChallenge.Name;
            _selectedChallengeDescriptionLabel.text = string.IsNullOrEmpty(_selectedChallenge.Description)
                ? "No description set."
                : _selectedChallenge.Description;
            var now = DateTimeOffset.Now;
            var startTime = DateTimeOffset.FromUnixTimeSeconds(challenge.StartTimeSec);
            var endTime = DateTimeOffset.FromUnixTimeSeconds(_selectedChallenge.EndTimeSec);
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

            // Get detailed challenge info with scores
            try
            {
                var detailedChallenge = await _challengesSystem.GetChallengeAsync(_selectedChallenge.Id, true);
                _selectedChallengeParticipants.Clear();
                _selectedChallengeParticipants.AddRange(detailedChallenge.Scores);
                _challengeParticipantsList.RefreshItems();
            }
            catch (Exception e)
            {
                _errorPopup.style.display = DisplayStyle.Flex;
                _errorMessage.text = e.Message;
            }

            // Update button visibility based on challenge status and user participation
            UpdateChallengeButtons();

            _selectedChallengePanel.style.display = DisplayStyle.Flex;
        }

        private void UpdateChallengeButtons()
        {
            if (_selectedChallenge == null) return;

            var isActive = _selectedChallenge.IsActive;
            IChallengeScore foundParticipant = null;
            foreach (var participant in _selectedChallengeParticipants)
            {
                if (participant.Id != _nakamaSystem.UserId || participant.State != ChallengeState.Joined) continue;
                foundParticipant = participant;
                break;
            }

            var canClaim = _selectedChallenge.CanClaim;

            // Join button: show if challenge is active/pending, open, and user is not a participant
            _joinButton.style.display = isActive && foundParticipant == null ? DisplayStyle.Flex : DisplayStyle.None;

            // Leave button: show if user is participant and challenge is not ended
            _leaveButton.style.display = !isActive && foundParticipant != null && !canClaim
                ? DisplayStyle.Flex
                : DisplayStyle.None;

            // Submit score button: show if user is participant and challenge is active
            _submitScoreButton.style.display =
                isActive && foundParticipant != null && foundParticipant.NumScores < _selectedChallenge.MaxNumScore
                    ? DisplayStyle.Flex
                    : DisplayStyle.None;
            _submitScoreButton.text = $"Submit Score ({foundParticipant?.NumScores}/{_selectedChallenge.MaxNumScore})";

            // Invite button: show if user is participant and challenge is active
            _inviteButton.style.display =
                isActive && foundParticipant != null && foundParticipant.Id == _selectedChallenge.OwnerId &&
                _selectedChallenge.Size < _selectedChallenge.MaxSize
                    ? DisplayStyle.Flex
                    : DisplayStyle.None;

            // Claim rewards button: show if challenge is ended and user can claim
            _claimRewardsButton.style.display = !isActive && foundParticipant != null && canClaim
                ? DisplayStyle.Flex
                : DisplayStyle.None;
        }

        #endregion

        #region Challenges

        private async Task UpdateChallenges()
        {
            _challenges.Clear();

            try
            {
                // List all Challenges that the user has either joined, or is invited to.
                var userChallengesResult = await _challengesSystem.ListChallengesAsync(null);
                _challenges.AddRange(userChallengesResult.Challenges);
            }
            catch (Exception e)
            {
                _errorPopup.style.display = DisplayStyle.Flex;
                _errorMessage.text = e.Message;
                return;
            }

            _challengesList.RefreshItems();
            _challengesList.ClearSelection();

            // If we have a Challenge selected, update Challenges, then try to select that Challenge, if it still exists.
            foreach (var challenge in _challenges)
            {
                if (challenge.Id != _selectedChallengeId) continue;

                _ = OnChallengeSelected(challenge);
                _challengesList.SetSelection(_challenges.IndexOf(challenge));
                return;
            }

            // If we don't find the previously selected Challenge, hide the selected Challenge panel.
            _selectedChallengePanel.style.display = DisplayStyle.None;
        }

        private async Task ChallengeCreate()
        {
            try
            {
                // Get the selected template ID.
                if (_modalTemplateDropdown.index < 0 || _modalTemplateDropdown.index >= _challengeTemplates.Count)
                {
                    throw new Exception("Please select a valid Challenge template.");
                }

                var selectedTemplate = _challengeTemplates.ElementAt(_modalTemplateDropdown.index);

                if (string.IsNullOrEmpty(_modalInvitees.value))
                {
                    throw new Exception("Invitees field cannot be empty. Please enter at least one username.");
                }

                // Split the input by comma and trim whitespace.
                var inviteeUsernames = _modalInvitees.value
                    .Split(',')
                    .Select(username => username.Trim())
                    .Where(username => !string.IsNullOrEmpty(username))
                    .ToList();

                if (inviteeUsernames.Count == 0)
                {
                    throw new Exception("No valid usernames found. Please enter at least one username.");
                }

                var invitees = await _nakamaSystem.Client.GetUsersAsync(
                    session: _nakamaSystem.Session,
                    usernames: inviteeUsernames,
                    ids: null
                );

                var inviteeIDs = invitees.Users.Select(user => user.Id).ToArray();

                // Validate that we found all requested users
                if (inviteeIDs.Length != inviteeUsernames.Count)
                {
                    throw new Exception(
                        $"Could not find all users. Requested: {inviteeUsernames.Count}, Found: {inviteeIDs.Length}");
                }

                // Read from additional properties.
                selectedTemplate.Value.AdditionalProperties.TryGetValue("description", out var description);
                selectedTemplate.Value.AdditionalProperties.TryGetValue("category", out var category);

                var newChallenge = await _challengesSystem.CreateChallengeAsync(
                    selectedTemplate.Key,
                    _modalNameField.value,
                    description ?? "Missing description.",
                    inviteeIDs,
                    _modalOpenToggle.value,
                    selectedTemplate.Value.MaxNumScore,
                    _modalChallengeDelay.value,
                    _modalChallengeDuration.value,
                    _modalMaxParticipantsField.value,
                    category ?? "Missing category",
                    new Dictionary<string, string>()
                );

                _selectedChallengeId = newChallenge.Id;
                _selectedChallenge = newChallenge;
            }
            catch (Exception e)
            {
                _errorPopup.style.display = DisplayStyle.Flex;
                _errorMessage.text = e.Message;
                return;
            }

            _createModal.style.display = DisplayStyle.None;
            _ = UpdateChallenges();
        }

        private async Task ChallengeJoin()
        {
            if (_selectedChallenge == null) return;

            try
            {
                // Attempt to join the selected Challenge.
                await _challengesSystem.JoinChallengeAsync(_selectedChallenge.Id);
            }
            catch (Exception e)
            {
                _errorPopup.style.display = DisplayStyle.Flex;
                _errorMessage.text = e.Message;
                return;
            }

            // After successfully joining the Challenge, update the Challenges list.
            _ = UpdateChallenges();
        }

        private async Task ChallengeLeave()
        {
            if (_selectedChallenge == null) return;

            try
            {
                // Attempt to leave the selected Challenge.
                await _challengesSystem.LeaveChallengeAsync(_selectedChallenge.Id);
                _challengesList.ClearSelection();
            }
            catch (Exception e)
            {
                _errorPopup.style.display = DisplayStyle.Flex;
                _errorMessage.text = e.Message;
                return;
            }

            // After successfully leaving the Challenge, update the Challenges list.
            _ = UpdateChallenges();
        }

        private async Task ChallengeClaim()
        {
            if (_selectedChallenge == null) return;

            try
            {
                // Attempt to claim rewards from the selected Challenge.
                await _challengesSystem.ClaimChallengeAsync(_selectedChallenge.Id);
                // Currency may have been rewarded, so notify UI.
                await _economySystem.RefreshAsync();
            }
            catch (Exception e)
            {
                _errorPopup.style.display = DisplayStyle.Flex;
                _errorMessage.text = e.Message;
                return;
            }

            // After successfully claiming the Challenge rewards, update the Challenges list.
            _ = UpdateChallenges();
        }

        private async Task ChallengeSubmitScore()
        {
            if (_selectedChallenge == null) return;

            try
            {
                // Attempt to submit a score to the selected Challenge.
                await _challengesSystem.SubmitChallengeScoreAsync(
                    _selectedChallenge.Id,
                    _scoreField.value,
                    _subScoreField.value,
                    _scoreMetadataField.value,
                    true
                );
            }
            catch (Exception e)
            {
                _errorPopup.style.display = DisplayStyle.Flex;
                _errorMessage.text = e.Message;
                return;
            }

            _submitScoreModal.style.display = DisplayStyle.None;

            // After successfully submitting a score, update the Challenges list.
            _ = UpdateChallenges();
        }

        private async Task ChallengeInvite()
        {
            if (_selectedChallenge == null) return;

            try
            {
                if (string.IsNullOrEmpty(_inviteModalInvitees.value))
                {
                    throw new Exception("Invitees field cannot be empty. Please enter at least one username.");
                }

                // Split the input by comma and trim whitespace
                var inviteeUsernames = _inviteModalInvitees.value
                    .Split(',')
                    .Select(username => username.Trim())
                    .Where(username => !string.IsNullOrEmpty(username))
                    .ToList();

                if (inviteeUsernames.Count == 0)
                {
                    throw new Exception("No valid usernames found. Please enter at least one username.");
                }

                // Convert from usernames to user IDs.
                var invitees = await _nakamaSystem.Client.GetUsersAsync(
                    session: _nakamaSystem.Session,
                    usernames: inviteeUsernames,
                    ids: null
                );

                var inviteeIDs = invitees.Users.Select(user => user.Id).ToArray();

                // Validate that we found all requested users
                if (inviteeIDs.Length != inviteeUsernames.Count)
                {
                    throw new Exception(
                        $"Could not find all users. Requested: {inviteeUsernames.Count}, Found: {inviteeIDs.Length}");
                }

                // Attempt to invite all listed users to the selected Challenge.
                await _challengesSystem.InviteChallengeAsync(
                    challengeId: _selectedChallenge.Id,
                    userIds: inviteeIDs
                );
            }
            catch (Exception e)
            {
                _errorPopup.style.display = DisplayStyle.Flex;
                _errorMessage.text = e.Message;
                return;
            }

            _inviteModal.style.display = DisplayStyle.None;

            // After successfully inviting the requested users, update the Challenges list.
            _ = UpdateChallenges();
        }

        #endregion
    }
}
