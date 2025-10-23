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
        [Header("Challenge Settings")] [SerializeField]
        private int challengeEntriesLimit = 100;

        [Header("References")] [SerializeField]
        private VisualTreeAsset challengeEntryTemplate;
        [SerializeField]
        private VisualTreeAsset challengeParticipantTemplate;

        public event Action<ISession, HiroChallengesController> OnInitialized;

        private WalletDisplay walletDisplay;

        private Button myChallengesTab;
        private Button createButton;
        private Button joinButton;
        private Button leaveButton;
        private Button claimRewardsButton;
        private Button submitScoreButton;
        private Button inviteButton;
        private VisualElement selectedChallengePanel;
        private Label selectedChallengeNameLabel;
        private Label selectedChallengeDescriptionLabel;
        private Label selectedChallengeStatusLabel;
        private Label selectedChallengeEndTimeLabel;
        private ListView challengeParticipantsList;
        private ListView challengesList;
        private ScrollView challengesScrollView;
        private ScrollView challengeParticipantsScrollView;

        private VisualElement createModal;
        private DropdownField modalTemplateDropdown;
        private TextField modalNameField;
        private IntegerField modalMaxParticipantsField;
        private TextField modalInvitees;
        private SliderInt modalChallengeDelay;
        private Label modalChallengeDelayLabel;
        private SliderInt modalChallengeDuration;
        private Label modalChallengeDurationLabel;
        private Toggle modalOpenToggle;
        private Button modalCreateButton;
        private Button modalCloseButton;

        private VisualElement submitScoreModal;
        private IntegerField scoreField;
        private IntegerField subScoreField;
        private TextField scoreMetadataField;
        private Button submitScoreModalButton;
        private Button submitScoreModalCloseButton;

        private VisualElement inviteModal;
        private TextField inviteModalInvitees;
        private Button inviteModalButton;
        private Button inviteModalCloseButton;

        private VisualElement errorPopup;
        private Button errorCloseButton;
        private Label errorMessage;

        private int selectedTabIndex;
        private string selectedChallengeId;
        private IChallenge selectedChallenge;
        private NakamaSystem nakamaSystem;
        private IChallengesSystem challengesSystem;
        private IEconomySystem economySystem;
        private readonly Dictionary<string, IChallengeTemplate> challengeTemplates = new();
        private readonly List<IChallenge> challenges = new();
        private readonly List<IChallengeScore> selectedChallengeParticipants = new();

        #region Initialization

        private void Start()
        {
            InitializeUI();
            var challengesCoordinator = HiroCoordinator.Instance as HiroChallengesCoordinator;
            if (challengesCoordinator == null) return;

            challengesCoordinator.ReceivedStartError += e =>
            {
                Debug.LogException(e);
                errorPopup.style.display = DisplayStyle.Flex;
                errorMessage.text = e.Message;
            };
            challengesCoordinator.ReceivedStartSuccess += session =>
            {
                OnInitialized?.Invoke(session, this);

                // Cache Hiro systems since we'll be accessing them often.
                nakamaSystem = this.GetSystem<NakamaSystem>();
                challengesSystem = this.GetSystem<ChallengesSystem>();
                economySystem = this.GetSystem<EconomySystem>();

                // Let our wallet display know that Hiro has been initialized successfully.
                walletDisplay.StartObserving();

                // Load your challenges, and all challenge templates, ready for starting new challenges.
                _ = UpdateChallenges();
                _ = LoadChallengeTemplates();
            };
        }

        public void SwitchComplete()
        {
            createModal.style.display = DisplayStyle.None;
            submitScoreModal.style.display = DisplayStyle.None;
            inviteModal.style.display = DisplayStyle.None;
            _ = UpdateChallenges();
            economySystem.RefreshAsync();
        }

        #endregion

        #region UI Binding

        private void InitializeUI()
        {
            var rootElement = GetComponent<UIDocument>().rootVisualElement;

            walletDisplay = new WalletDisplay(rootElement.Q<VisualElement>("wallet-display"));

            myChallengesTab = rootElement.Q<Button>("my-challenges-tab");
            myChallengesTab.RegisterCallback<ClickEvent>(evt =>
            {
                if (selectedTabIndex == 0) return;
                selectedTabIndex = 0;
                myChallengesTab.AddToClassList("selected");
                _ = UpdateChallenges();
            });

            createButton = rootElement.Q<Button>("challenge-create");
            createButton.RegisterCallback<ClickEvent>(_ =>
            {
                modalNameField.value = string.Empty;
                modalMaxParticipantsField.value = 100;
                modalInvitees.value = string.Empty;
                modalChallengeDelay.value = 0;
                modalChallengeDuration.value = 2000;
                modalOpenToggle.value = false;

                // Reset template dropdown to the first item if available.
                if (modalTemplateDropdown.choices.Count > 0)
                {
                    modalTemplateDropdown.index = 0;
                }

                createModal.style.display = DisplayStyle.Flex;
            });

            joinButton = rootElement.Q<Button>("challenge-join");
            joinButton.RegisterCallback<ClickEvent>(evt => _ = ChallengeJoin());

            leaveButton = rootElement.Q<Button>("challenge-leave");
            leaveButton.RegisterCallback<ClickEvent>(evt => _ = ChallengeLeave());

            claimRewardsButton = rootElement.Q<Button>("challenge-claim");
            claimRewardsButton.RegisterCallback<ClickEvent>(evt => _ = ChallengeClaim());

            submitScoreButton = rootElement.Q<Button>("challenge-submit-score");
            submitScoreButton.RegisterCallback<ClickEvent>(_ =>
            {
                scoreField.value = 0;
                subScoreField.value = 0;
                scoreMetadataField.value = string.Empty;
                submitScoreModal.style.display = DisplayStyle.Flex;
            });

            inviteButton = rootElement.Q<Button>("challenge-invite");
            inviteButton.RegisterCallback<ClickEvent>(_ =>
            {
                inviteModalInvitees.value = string.Empty;
                inviteModal.style.display = DisplayStyle.Flex;
            });

            selectedChallengePanel = rootElement.Q<VisualElement>("selected-challenge-panel");
            selectedChallengeNameLabel = rootElement.Q<Label>("selected-challenge-name");
            selectedChallengeDescriptionLabel = rootElement.Q<Label>("selected-challenge-description");
            selectedChallengeStatusLabel = rootElement.Q<Label>("selected-challenge-status");
            selectedChallengeEndTimeLabel = rootElement.Q<Label>("selected-challenge-end-time");

            challengeParticipantsList = rootElement.Q<ListView>("challenge-participants-list");
            challengeParticipantsList.makeItem = () =>
            {
                var newListEntry = challengeParticipantTemplate.Instantiate();
                var newListEntryLogic = new ChallengeParticipantView();
                newListEntry.userData = newListEntryLogic;
                newListEntryLogic.SetVisualElement(newListEntry);
                return newListEntry;
            };
            challengeParticipantsList.bindItem = (item, index) =>
            {
                (item.userData as ChallengeParticipantView)?.SetChallengeParticipant(selectedChallenge,
                    selectedChallengeParticipants[index]);
            };
            challengeParticipantsList.itemsSource = selectedChallengeParticipants;

            challengeParticipantsScrollView = challengeParticipantsList.Q<ScrollView>();
            challengeParticipantsScrollView.verticalScrollerVisibility = ScrollerVisibility.AlwaysVisible;

            challengesList = rootElement.Q<ListView>("challenges-list");
            challengesList.makeItem = () =>
            {
                var newListEntry = challengeEntryTemplate.Instantiate();
                var newListEntryLogic = new ChallengeView();
                newListEntry.userData = newListEntryLogic;
                newListEntryLogic.SetVisualElement(newListEntry);
                return newListEntry;
            };
            challengesList.bindItem = (item, index) =>
            {
                (item.userData as ChallengeView)?.SetChallenge(challenges[index]);
            };
            challengesList.itemsSource = challenges;
            challengesList.selectionChanged += objects =>
            {
                if (challengesList.selectedItem is IChallenge)
                {
                    _ = OnChallengeSelected(challengesList.selectedItem as IChallenge);
                }
            };

            challengesScrollView = challengesList.Q<ScrollView>();
            challengesScrollView.verticalScrollerVisibility = ScrollerVisibility.AlwaysVisible;

            // Create Modal
            createModal = rootElement.Q<VisualElement>("create-modal");
            modalTemplateDropdown = rootElement.Q<DropdownField>("create-modal-template");
            modalTemplateDropdown.RegisterValueChangedCallback(_ => { UpdateCreateModalLimits(); });
            modalNameField = rootElement.Q<TextField>("create-modal-name");
            modalMaxParticipantsField = rootElement.Q<IntegerField>("create-modal-max-participants");
            modalMaxParticipantsField.RegisterCallback<FocusOutEvent>(_ =>
            {
                var template = challengeTemplates.ElementAt(modalTemplateDropdown.index);
                modalMaxParticipantsField.value = (int)Mathf.Clamp(modalMaxParticipantsField.value,
                    template.Value.Players.Min, template.Value.Players.Max);
            });
            modalInvitees = rootElement.Q<TextField>("create-modal-invitees");
            modalOpenToggle = rootElement.Q<Toggle>("create-modal-open");

            // Challenge Delay Slider
            modalChallengeDelay = rootElement.Q<SliderInt>("create-modal-delay");
            modalChallengeDelayLabel = rootElement.Q<Label>("create-modal-delay-value");
            modalChallengeDelay.RegisterValueChangedCallback(evt =>
            {
                modalChallengeDelayLabel.text = $"{evt.newValue}s";
            });
            modalChallengeDelayLabel.text = $"{modalChallengeDelay.value}s";

            // Challenge Duration Slider
            modalChallengeDuration = rootElement.Q<SliderInt>("create-modal-duration");
            modalChallengeDurationLabel = rootElement.Q<Label>("create-modal-duration-value");
            modalChallengeDuration.RegisterValueChangedCallback(evt =>
            {
                modalChallengeDurationLabel.text = $"{evt.newValue}s";
            });
            modalChallengeDurationLabel.text = $"{modalChallengeDuration.value}s";

            modalCreateButton = rootElement.Q<Button>("create-modal-create");
            modalCreateButton.RegisterCallback<ClickEvent>(evt => _ = ChallengeCreate());
            modalCloseButton = rootElement.Q<Button>("create-modal-close");
            modalCloseButton.RegisterCallback<ClickEvent>(_ => createModal.style.display = DisplayStyle.None);

            // Submit Score Modal
            submitScoreModal = rootElement.Q<VisualElement>("submit-score-modal");
            scoreField = rootElement.Q<IntegerField>("submit-score-score");
            subScoreField = rootElement.Q<IntegerField>("submit-score-subscore");
            scoreMetadataField = rootElement.Q<TextField>("submit-score-metadata");
            submitScoreModalButton = rootElement.Q<Button>("submit-score-modal-submit");
            submitScoreModalButton.RegisterCallback<ClickEvent>(evt => _ = ChallengeSubmitScore());
            submitScoreModalCloseButton = rootElement.Q<Button>("submit-score-modal-close");
            submitScoreModalCloseButton.RegisterCallback<ClickEvent>(_ =>
                submitScoreModal.style.display = DisplayStyle.None);

            // Invite Modal
            inviteModal = rootElement.Q<VisualElement>("invite-modal");
            inviteModalInvitees = rootElement.Q<TextField>("invite-modal-invitees");
            inviteModalButton = rootElement.Q<Button>("invite-modal-invite");
            inviteModalButton.RegisterCallback<ClickEvent>(evt => _ = ChallengeInvite());
            inviteModalCloseButton = rootElement.Q<Button>("invite-modal-close");
            inviteModalCloseButton.RegisterCallback<ClickEvent>(_ => inviteModal.style.display = DisplayStyle.None);

            errorPopup = rootElement.Q<VisualElement>("error-popup");
            errorMessage = rootElement.Q<Label>("error-message");
            errorCloseButton = rootElement.Q<Button>("error-close");
            errorCloseButton.RegisterCallback<ClickEvent>(_ => errorPopup.style.display = DisplayStyle.None);

            _ = OnChallengeSelected(null);
        }

        private void UpdateCreateModalLimits()
        {
            var template = challengeTemplates.ElementAt(modalTemplateDropdown.index).Value;
            var maxDelay = template.StartDelayMax;

            modalChallengeDelay.highValue = (int)maxDelay;

            modalChallengeDelay.value = (int)Mathf.Clamp(modalChallengeDelay.value, 0, maxDelay);
            modalChallengeDelayLabel.text = $"{modalChallengeDelay.value}s";

            var minDuration = template.Duration.MinSec;
            var maxDuration = template.Duration.MaxSec;

            modalChallengeDuration.lowValue = (int)minDuration;
            modalChallengeDuration.highValue = (int)maxDuration;
            modalChallengeDuration.value = (int)Mathf.Clamp(modalChallengeDuration.value, minDuration, maxDuration);
            modalChallengeDurationLabel.text = $"{modalChallengeDuration.value}s";
            modalMaxParticipantsField.value = (int)Mathf.Clamp(modalMaxParticipantsField.value, template.Players.Min,
                template.Players.Max);
        }

        private async Task LoadChallengeTemplates()
        {
            try
            {
                challengeTemplates.Clear();
                var loadedTemplates = (await challengesSystem.GetTemplatesAsync()).Templates;
                var challengeTemplateNames = new List<string>();
                foreach (var template in loadedTemplates)
                {
                    challengeTemplates[template.Key] = template.Value;
                    challengeTemplateNames.Add(
                        template.Value.AdditionalProperties.TryGetValue("display_name", out var displayName)
                            ? displayName
                            : template.Key);
                }

                // Populate dropdown with template names
                modalTemplateDropdown.choices = challengeTemplateNames;
            }
            catch (Exception e)
            {
                errorPopup.style.display = DisplayStyle.Flex;
                errorMessage.text = $"Failed to load challenge templates: {e.Message}";
            }
        }

        private async Task OnChallengeSelected(IChallenge challenge)
        {
            if (challenge == null)
            {
                selectedChallengeId = string.Empty;
                selectedChallengePanel.style.display = DisplayStyle.None;
                return;
            }

            selectedChallenge = challenge;
            selectedChallengeId = selectedChallenge.Id;

            selectedChallengeNameLabel.text = selectedChallenge.Name;
            selectedChallengeDescriptionLabel.text = string.IsNullOrEmpty(selectedChallenge.Description)
                ? "No description set."
                : selectedChallenge.Description;
            selectedChallengeStatusLabel.text = selectedChallenge.IsActive ? "Active" : "Ended";
            selectedChallengeStatusLabel.style.color =
                challenge.IsActive ? new StyleColor(Color.green) : new StyleColor(Color.red);

            var endTime = DateTimeOffset.FromUnixTimeSeconds(selectedChallenge.EndTimeSec).LocalDateTime;
            selectedChallengeEndTimeLabel.text = endTime.ToString("MMM dd, yyyy HH:mm");

            // Get detailed challenge info with scores
            try
            {
                var detailedChallenge = await challengesSystem.GetChallengeAsync(selectedChallenge.Id, true);
                selectedChallengeParticipants.Clear();
                selectedChallengeParticipants.AddRange(detailedChallenge.Scores);
                challengeParticipantsList.RefreshItems();
            }
            catch (Exception e)
            {
                errorPopup.style.display = DisplayStyle.Flex;
                errorMessage.text = e.Message;
            }

            // Update button visibility based on challenge status and user participation
            UpdateChallengeButtons();

            selectedChallengePanel.style.display = DisplayStyle.Flex;
        }

        private void UpdateChallengeButtons()
        {
            if (selectedChallenge == null) return;

            var isActive = selectedChallenge.IsActive;
            IChallengeScore foundParticipant = null;
            foreach (var participant in selectedChallengeParticipants)
            {
                if (participant.Id != nakamaSystem.UserId || participant.State != ChallengeState.Joined) continue;
                foundParticipant = participant;
                break;
            }

            var canClaim = selectedChallenge.CanClaim;

            // Join button: show if challenge is active/pending, open, and user is not a participant
            joinButton.style.display = isActive && foundParticipant == null ? DisplayStyle.Flex : DisplayStyle.None;

            // Leave button: show if user is participant and challenge is not ended
            leaveButton.style.display = !isActive && foundParticipant != null && !canClaim
                ? DisplayStyle.Flex
                : DisplayStyle.None;

            // Submit score button: show if user is participant and challenge is active
            submitScoreButton.style.display =
                isActive && foundParticipant != null && foundParticipant.NumScores < selectedChallenge.MaxNumScore
                    ? DisplayStyle.Flex
                    : DisplayStyle.None;
            submitScoreButton.text = $"Submit Score ({foundParticipant?.NumScores}/{selectedChallenge.MaxNumScore})";

            // Invite button: show if user is participant and challenge is active
            inviteButton.style.display =
                isActive && foundParticipant != null && foundParticipant.Id == selectedChallenge.OwnerId
                    ? DisplayStyle.Flex
                    : DisplayStyle.None;

            // Claim rewards button: show if challenge is ended and user can claim
            claimRewardsButton.style.display = !isActive && foundParticipant != null && canClaim
                ? DisplayStyle.Flex
                : DisplayStyle.None;
        }

        #endregion

        #region Challenges

        private async Task UpdateChallenges()
        {
            challenges.Clear();

            try
            {
                // List Challenges that the user has joined (we'll need to filter from all challenges)
                var userChallengesResult = await challengesSystem.ListChallengesAsync(null);
                challenges.AddRange(userChallengesResult.Challenges);
            }
            catch (Exception e)
            {
                errorPopup.style.display = DisplayStyle.Flex;
                errorMessage.text = e.Message;
                return;
            }

            challengesList.RefreshItems();
            challengesList.ClearSelection();

            // If we have a Challenge selected, then update Challenges, try to select that Challenge, if it still exists.
            foreach (var challenge in challenges)
            {
                if (challenge.Id != selectedChallengeId) continue;

                _ = OnChallengeSelected(challenge);
                challengesList.SetSelection(challenges.IndexOf(challenge));
                return;
            }

            // If we don't find the previously selected Challenge, hide the selected Challenge panel.
            selectedChallengePanel.style.display = DisplayStyle.None;
        }

        private async Task ChallengeCreate()
        {
            try
            {
                // Get the selected template ID
                if (modalTemplateDropdown.index < 0 || modalTemplateDropdown.index >= challengeTemplates.Count)
                {
                    throw new Exception("Please select a valid challenge template.");
                }

                var selectedTemplate = challengeTemplates.ElementAt(modalTemplateDropdown.index);

                var metadata = new Dictionary<string, string>();

                if (string.IsNullOrEmpty(modalInvitees.value))
                {
                    throw new Exception("Invitees field cannot be empty. Please enter at least one username.");
                }

                // Split the input by comma and trim whitespace
                var inviteeUsernames = modalInvitees.value
                    .Split(',')
                    .Select(username => username.Trim())
                    .Where(username => !string.IsNullOrEmpty(username))
                    .ToList();

                if (inviteeUsernames.Count == 0)
                {
                    throw new Exception("No valid usernames found. Please enter at least one username.");
                }

                var invitees = await nakamaSystem.Client.GetUsersAsync(
                    session: nakamaSystem.Session,
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

                selectedTemplate.Value.AdditionalProperties.TryGetValue("description", out var description);
                selectedTemplate.Value.AdditionalProperties.TryGetValue("category", out var category);

                await challengesSystem.CreateChallengeAsync(
                    selectedTemplate.Key,
                    modalNameField.value,
                    description ?? "Missing description.",
                    inviteeIDs,
                    modalOpenToggle.value,
                    selectedTemplate.Value.MaxNumScore,
                    modalChallengeDelay.value,
                    modalChallengeDuration.value,
                    modalMaxParticipantsField.value,
                    category ?? "Missing category",
                    metadata
                );
            }
            catch (Exception e)
            {
                errorPopup.style.display = DisplayStyle.Flex;
                errorMessage.text = e.Message;
                return;
            }

            createModal.style.display = DisplayStyle.None;
            _ = UpdateChallenges();
        }

        private async Task ChallengeJoin()
        {
            if (selectedChallenge == null) return;

            try
            {
                await challengesSystem.JoinChallengeAsync(selectedChallenge.Id);
            }
            catch (Exception e)
            {
                errorPopup.style.display = DisplayStyle.Flex;
                errorMessage.text = e.Message;
                return;
            }

            _ = UpdateChallenges();
        }

        private async Task ChallengeLeave()
        {
            if (selectedChallenge == null) return;

            try
            {
                await challengesSystem.LeaveChallengeAsync(selectedChallenge.Id);
                challengesList.ClearSelection();
            }
            catch (Exception e)
            {
                errorPopup.style.display = DisplayStyle.Flex;
                errorMessage.text = e.Message;
                return;
            }

            _ = UpdateChallenges();
        }

        private async Task ChallengeClaim()
        {
            if (selectedChallenge == null) return;

            try
            {
                await challengesSystem.ClaimChallengeAsync(selectedChallenge.Id);
                await economySystem.RefreshAsync();
            }
            catch (Exception e)
            {
                errorPopup.style.display = DisplayStyle.Flex;
                errorMessage.text = e.Message;
                return;
            }

            _ = UpdateChallenges();
        }

        private async Task ChallengeSubmitScore()
        {
            if (selectedChallenge == null) return;

            try
            {
                await challengesSystem.SubmitChallengeScoreAsync(
                    selectedChallenge.Id,
                    scoreField.value,
                    subScoreField.value,
                    scoreMetadataField.value,
                    true
                );
            }
            catch (Exception e)
            {
                errorPopup.style.display = DisplayStyle.Flex;
                errorMessage.text = e.Message;
                return;
            }

            submitScoreModal.style.display = DisplayStyle.None;
            _ = UpdateChallenges();
        }

        private async Task ChallengeInvite()
        {
            if (selectedChallenge == null) return;

            try
            {
                if (string.IsNullOrEmpty(inviteModalInvitees.value))
                {
                    throw new Exception("Invitees field cannot be empty. Please enter at least one username.");
                }

                // Split the input by comma and trim whitespace
                var inviteeUsernames = inviteModalInvitees.value
                    .Split(',')
                    .Select(username => username.Trim())
                    .Where(username => !string.IsNullOrEmpty(username))
                    .ToList();

                if (inviteeUsernames.Count == 0)
                {
                    throw new Exception("No valid usernames found. Please enter at least one username.");
                }

                var invitees = await nakamaSystem.Client.GetUsersAsync(
                    session: nakamaSystem.Session,
                    usernames: inviteeUsernames,
                    ids: null
                );

                var inviteeIDs = invitees.Users.Select(user => user.Id).ToArray();

                Debug.LogFormat("Inviting {0} players to challenge {1}: {2}",
                    inviteeIDs.Length,
                    selectedChallenge.Id,
                    string.Join(", ", inviteeIDs)
                );

                // Validate that we found all requested users
                if (inviteeIDs.Length != inviteeUsernames.Count)
                {
                    throw new Exception(
                        $"Could not find all users. Requested: {inviteeUsernames.Count}, Found: {inviteeIDs.Length}");
                }

                await challengesSystem.InviteChallengeAsync(
                    challengeId: selectedChallenge.Id,
                    userIds: inviteeIDs
                );
            }
            catch (Exception e)
            {
                errorPopup.style.display = DisplayStyle.Flex;
                errorMessage.text = e.Message;
                return;
            }

            inviteModal.style.display = DisplayStyle.None;
            _ = UpdateChallenges();
        }

        #endregion
    }
}