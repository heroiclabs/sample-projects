using System;
using System.Collections.Generic;
using Hiro;
using UnityEngine;
using UnityEngine.UIElements;
using Hiro.Unity;
using System.Linq;
using System.Threading.Tasks;
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

        private Button allTab;
        private Button myChallengesTab;
        private Button createButton;
        private Button joinButton;
        private Button leaveButton;
        private Button claimRewardsButton;
        private Button submitScoreButton;
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
        private TextField modalCategory;
        private SliderInt modalChallengeDelay;
        private Label modalChallengeDelayLabel;
        private SliderInt modalChallengeDuration;
        private Label modalChallengeDurationLabel;
        private IntegerField modalMaxScoreSubmissions;
        private Toggle modalOpenToggle;
        private Button modalCreateButton;
        private Button modalCloseButton;

        private VisualElement submitScoreModal;
        private IntegerField scoreField;
        private IntegerField subScoreField;
        private TextField scoreMetadataField;
        private Button submitScoreModalButton;
        private Button submitScoreModalCloseButton;

        private VisualElement errorPopup;
        private Button errorCloseButton;
        private Label errorMessage;

        private int selectedTabIndex;
        private string selectedChallengeId;
        private IChallenge selectedChallenge;
        private NakamaSystem nakamaSystem;
        private ChallengesSystem challengesSystem;
        private readonly List<IChallenge> challenges = new();
        private readonly List<IChallengeScore> selectedChallengeParticipants = new();
        private readonly List<String> challengeTemplates = new();

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
                nakamaSystem = this.GetSystem<NakamaSystem>();
                challengesSystem = this.GetSystem<ChallengesSystem>();
                _ = UpdateChallenges();
                _ = LoadChallengeTemplates();
            };
        }

        public void SwitchComplete()
        {
            _ = UpdateChallenges();
        }

        #region UI Binding

        private void InitializeUI()
        {
            var rootElement = GetComponent<UIDocument>().rootVisualElement;

            allTab = rootElement.Q<Button>("all-tab");
            allTab.RegisterCallback<ClickEvent>(evt =>
            {
                if (selectedTabIndex == 0) return;
                selectedTabIndex = 0;
                allTab.AddToClassList("selected");
                myChallengesTab.RemoveFromClassList("selected");
                _ = UpdateChallenges();
            });

            myChallengesTab = rootElement.Q<Button>("my-challenges-tab");
            myChallengesTab.RegisterCallback<ClickEvent>(evt =>
            {
                if (selectedTabIndex == 1) return;
                selectedTabIndex = 1;
                myChallengesTab.AddToClassList("selected");
                allTab.RemoveFromClassList("selected");
                _ = UpdateChallenges();
            });

            createButton = rootElement.Q<Button>("challenge-create");
            createButton.RegisterCallback<ClickEvent>(_ =>
            {
                modalNameField.value = string.Empty;
                modalMaxParticipantsField.value = 100;
                modalInvitees.value = string.Empty;
                modalCategory.value = "race";
                modalChallengeDelay.value = 0;
                modalChallengeDuration.value = 2000;
                modalMaxScoreSubmissions.value = 10;
                modalOpenToggle.value = true;

                // Reset template dropdown to first item if available
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
                (item.userData as ChallengeParticipantView)?.SetChallengeParticipant(selectedChallengeParticipants[index]);
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
            challengesList.bindItem = (item, index) => { (item.userData as ChallengeView)?.SetChallenge(challenges[index]); };
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
            modalTemplateDropdown.RegisterValueChangedCallback(evt =>
            {
                UpdateSliderLimitsFromTemplateAsync(evt.newValue);
            });
            modalNameField = rootElement.Q<TextField>("create-modal-name");
            modalMaxParticipantsField = rootElement.Q<IntegerField>("create-modal-max-participants");
            modalInvitees = rootElement.Q<TextField>("create-modal-invitees");
            modalCategory = rootElement.Q<TextField>("create-modal-category");
            modalMaxScoreSubmissions = rootElement.Q<IntegerField>("create-modal-max-submissions");
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
            submitScoreModalCloseButton.RegisterCallback<ClickEvent>(_ => submitScoreModal.style.display = DisplayStyle.None);

            errorPopup = rootElement.Q<VisualElement>("error-popup");
            errorMessage = rootElement.Q<Label>("error-message");
            errorCloseButton = rootElement.Q<Button>("error-close");
            errorCloseButton.RegisterCallback<ClickEvent>(_ => errorPopup.style.display = DisplayStyle.None);

            _ = OnChallengeSelected(null);
        }
        
        private async Task UpdateSliderLimitsFromTemplateAsync(string templateName)
        {
            var templates = await challengesSystem.GetTemplatesAsync();
            IChallengeTemplate template = null;
            templates.Templates.TryGetValue(templateName, out template);
            long maxDelay = template.StartDelayMax;

            modalChallengeDelay.highValue = (int)maxDelay;
            
            modalChallengeDelay.value = (int)Mathf.Clamp(modalChallengeDelay.value, 0, maxDelay);
            modalChallengeDelayLabel.text = $"{modalChallengeDelay.value}s";

            var minDuration = template.Duration.MinSec;
            var maxDuration = template.Duration.MaxSec;

            modalChallengeDuration.lowValue = (int)minDuration;
            modalChallengeDuration.highValue = (int)maxDuration;
            modalChallengeDuration.value = (int)Mathf.Clamp(modalChallengeDuration.value, minDuration, maxDuration);
            modalChallengeDurationLabel.text = $"{modalChallengeDuration.value}s";
        }

        private async Task LoadChallengeTemplates()
        {
            try
            {
                var templates = await challengesSystem.GetTemplatesAsync();
                
                challengeTemplates.Clear();
                challengeTemplates.AddRange(templates.Templates.Keys.ToList());
                
                // Populate dropdown with template names
                modalTemplateDropdown.choices = challengeTemplates;
                
                if (challengeTemplates.Count > 0)
                {
                    modalTemplateDropdown.index = 0;
                    UpdateSliderLimitsFromTemplateAsync(modalTemplateDropdown.value);
                }
                
                Debug.LogFormat("Loaded {0} challenge templates", challengeTemplates.Count);
            }
            catch (Exception e)
            {
                Debug.LogError($"Failed to load challenge templates: {e.Message}");
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
            selectedChallengeDescriptionLabel.text = string.IsNullOrEmpty(selectedChallenge.Description) ? "No description set." : selectedChallenge.Description;
            selectedChallengeStatusLabel.text = selectedChallenge.IsActive ? "Active" : "Ended";
            
            var endTime = DateTimeOffset.FromUnixTimeSeconds(selectedChallenge.EndTimeSec).DateTime;
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
            var isParticipant = IsUserParticipant();
            var canClaim = selectedChallenge.CanClaim;

            // Join button: show if challenge is active/pending, open, and user is not a participant
            joinButton.style.display = isActive && !isParticipant ? DisplayStyle.Flex : DisplayStyle.None;

            // Leave button: show if user is participant and challenge is not ended
            leaveButton.style.display = isParticipant && !isActive && !canClaim ? DisplayStyle.Flex : DisplayStyle.None;

            // Submit score button: show if user is participant and challenge is active
            submitScoreButton.style.display = isParticipant && isActive ? DisplayStyle.Flex : DisplayStyle.None;

            // Claim rewards button: show if challenge is ended and user can claim
            claimRewardsButton.style.display = !isActive && isParticipant && canClaim ? DisplayStyle.Flex : DisplayStyle.None;
        }

        private bool IsUserParticipant()
        {
            foreach (var participant in selectedChallengeParticipants)
            {
                if (participant.Id == nakamaSystem.UserId && participant.State == ChallengeState.Joined) return true;
            }
            return false;
        }

        #endregion

        #region Challenges

        private async Task UpdateChallenges()
        {
            challenges.Clear();

            switch (selectedTabIndex)
            {
                case 0:
                    try
                    {
                        // List all Challenges.
                        var challengesResult =
                            await challengesSystem.SearchChallengesAsync(string.Empty, string.Empty,
                                challengeEntriesLimit);
                        challenges.AddRange(challengesResult.Challenges);
                    }
                    catch (Exception e)
                    {
                        errorPopup.style.display = DisplayStyle.Flex;
                        errorMessage.text = e.Message;
                        return;
                    }
                    break;
                case 1:
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
                    break;
                default:
                    Debug.LogError("Unhandled Tab Index");
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
                
                var selectedTemplate = challengeTemplates[modalTemplateDropdown.index];
                var templateId = selectedTemplate;
                
                var metadata = new Dictionary<string, string>();
                Debug.LogFormat("UserID: '{0}'", nakamaSystem.UserId);
                
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
                
                Debug.LogFormat("Found {0} invitees: {1}", 
                    inviteeIDs.Length, 
                    string.Join(", ", inviteeIDs)
                );
                
                // Validate that we found all requested users
                if (inviteeIDs.Length != inviteeUsernames.Count)
                {
                    throw new Exception($"Could not find all users. Requested: {inviteeUsernames.Count}, Found: {inviteeIDs.Length}");
                }
                
                await challengesSystem.CreateChallengeAsync(
                    templateId,
                    modalNameField.value,
                    "Challenge created via UI", // Hardcoded description, need to get description via additional properties later.
                    inviteeIDs,
                    modalOpenToggle.value,
                    modalMaxScoreSubmissions.value,
                    modalChallengeDelay.value,
                    modalChallengeDuration.value,
                    modalMaxParticipantsField.value,
                    modalCategory.value,
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

        #endregion
    }
}