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
        [SerializeField]
        private VisualTreeAsset templateTemplate;

        public event Action<ISession, HiroChallengesController> OnInitialized;

        private Button templatesTab;
        private Button myChallengesTab;
        private Button openChallengesTab;
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

        private VisualElement templateDetailPanel;
        private Label templateDetailName;
        private Label templateDetailDescription;
        private Label templateDetailDifficulty;
        private Label templateDetailCategory;
        private Label templateDetailPlayers;
        private Label templateDetailDuration;
        private IntegerField templateMaxParticipants;
        private IntegerField templateMaxScores;
        private Toggle templateOpenToggle;
        private TextField templateInvitees;
        private Button showAdvancedOptionsButton;
        private Button templateStartChallengeButton;

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
        private readonly List<KeyValuePair<string, IChallengeTemplate>> templates = new();
        private IChallengeTemplates challengeTemplates;
        private string selectedTemplateId;
        private IChallengeTemplate selectedTemplate;

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
                _ = RefreshCurrentTab();
                _ = LoadChallengeTemplates();
            };
        }

        public void SwitchComplete()
        {
            _ = RefreshCurrentTab();
        }

        #region UI Binding

        private void InitializeUI()
        {
            var rootElement = GetComponent<UIDocument>().rootVisualElement;

            templatesTab = rootElement.Q<Button>("game-modes-tab");
            templatesTab.RegisterCallback<ClickEvent>(evt =>
            {
                if (selectedTabIndex == 0) return;
                selectedTabIndex = 0;
                templatesTab.AddToClassList("selected");
                myChallengesTab.RemoveFromClassList("selected");
                openChallengesTab.RemoveFromClassList("selected");
                _ = UpdateTemplates();
            });

            myChallengesTab = rootElement.Q<Button>("my-challenges-tab");
            myChallengesTab.RegisterCallback<ClickEvent>(evt =>
            {
                if (selectedTabIndex == 1) return;
                selectedTabIndex = 1;
                myChallengesTab.AddToClassList("selected");
                templatesTab.RemoveFromClassList("selected");
                openChallengesTab.RemoveFromClassList("selected");
                _ = UpdateChallenges();
            });

            openChallengesTab = rootElement.Q<Button>("open-challenges-tab");
            openChallengesTab.RegisterCallback<ClickEvent>(evt =>
            {
                if (selectedTabIndex == 2) return;
                selectedTabIndex = 2;
                openChallengesTab.AddToClassList("selected");
                templatesTab.RemoveFromClassList("selected");
                myChallengesTab.RemoveFromClassList("selected");
                _ = UpdateOpenChallenges();
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
            // makeItem and bindItem will be set dynamically in SetupListViewForCurrentTab
            challengesList.selectionChanged += objects =>
            {
                if (selectedTabIndex == 0)
                {
                    // Game Modes tab - handle template selection
                    if (challengesList.selectedItem is KeyValuePair<string, IChallengeTemplate> selectedMode)
                    {
                        OnTemplateSelected(selectedMode.Key, selectedMode.Value);
                    }
                }
                else if (selectedTabIndex == 1 || selectedTabIndex == 2)
                {
                    // My Challenges tab or Open Challenges tab
                    if (challengesList.selectedItem is IChallenge challenge)
                    {
                        _ = OnChallengeSelected(challenge);
                    }
                }
            };

            challengesScrollView = challengesList.Q<ScrollView>();
            challengesScrollView.verticalScrollerVisibility = ScrollerVisibility.AlwaysVisible;

            // Create Modal
            createModal = rootElement.Q<VisualElement>("create-modal");
            modalTemplateDropdown = rootElement.Q<DropdownField>("create-modal-template");
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
            modalCreateButton.RegisterCallback<ClickEvent>(evt => _ = CreateCustomChallenge());
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

            // Template Detail Panel
            templateDetailPanel = rootElement.Q<VisualElement>("game-mode-detail-panel");
            templateDetailName = rootElement.Q<Label>("game-mode-detail-name");
            templateDetailDescription = rootElement.Q<Label>("game-mode-detail-description");
            templateDetailDifficulty = rootElement.Q<Label>("game-mode-detail-difficulty");
            templateDetailCategory = rootElement.Q<Label>("game-mode-detail-category");
            templateDetailPlayers = rootElement.Q<Label>("game-mode-detail-players");
            templateDetailDuration = rootElement.Q<Label>("game-mode-detail-duration");
            templateMaxParticipants = rootElement.Q<IntegerField>("game-mode-max-participants");
            templateMaxScores = rootElement.Q<IntegerField>("game-mode-max-scores");
            templateOpenToggle = rootElement.Q<Toggle>("game-mode-open-toggle");
            templateInvitees = rootElement.Q<TextField>("game-mode-invitees");

            showAdvancedOptionsButton = rootElement.Q<Button>("advanced-options");
            showAdvancedOptionsButton.RegisterCallback<ClickEvent>(_ =>
            {
                modalNameField.value = string.Empty;
                modalMaxParticipantsField.value = 100;
                modalInvitees.value = string.Empty;
                modalCategory.value = "race";
                modalChallengeDelay.value = 0;
                modalChallengeDuration.value = 2000;
                modalMaxScoreSubmissions.value = 10;
                modalOpenToggle.value = true;

                // Set the template dropdown to the currently selected template
                if (!string.IsNullOrEmpty(selectedTemplateId) && modalTemplateDropdown.choices != null)
                {
                    var templateIndex = modalTemplateDropdown.choices.IndexOf(selectedTemplateId);
                    if (templateIndex >= 0)
                    {
                        modalTemplateDropdown.index = templateIndex;
                    }
                }

                createModal.style.display = DisplayStyle.Flex;
            });

            templateStartChallengeButton = rootElement.Q<Button>("game-mode-start-challenge");
            templateStartChallengeButton.RegisterCallback<ClickEvent>(evt => _ = CreateTemplateChallenge());

            _ = OnChallengeSelected(null);
        }

        private async Task LoadChallengeTemplates()
        {
            try
            {
                challengeTemplates = await challengesSystem.GetTemplatesAsync();

                var templateNames = challengeTemplates.Templates.Keys.ToList();

                // Populate dropdown with template names
                modalTemplateDropdown.choices = templateNames;

                if (templateNames.Count > 0)
                {
                    modalTemplateDropdown.index = 0;
                }
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
                templateDetailPanel.style.display = DisplayStyle.None;
                return;
            }

            selectedChallenge = challenge;
            selectedChallengeId = selectedChallenge.Id;

            // Hide game mode panel, show challenge panel
            templateDetailPanel.style.display = DisplayStyle.None;
            selectedChallengePanel.style.display = DisplayStyle.Flex;

            selectedChallengeNameLabel.text = selectedChallenge.Name;
            selectedChallengeDescriptionLabel.text = string.IsNullOrEmpty(selectedChallenge.Description) ? "No description set." : selectedChallenge.Description;
            selectedChallengeStatusLabel.text = selectedChallenge.IsActive ? "Active" : "Ended";

            var endTime = DateTimeOffset.FromUnixTimeSeconds(selectedChallenge.EndTimeSec).DateTime;
            selectedChallengeEndTimeLabel.text = endTime.ToString("MMM dd, yyyy HH:mm");

            // Get detailed challenge info with scores
            // Note: This only works if the user has a relationship with the challenge
            // (joined, invited, created). For open challenges not yet joined, this will fail.
            try
            {
                var detailedChallenge = await challengesSystem.GetChallengeAsync(selectedChallenge.Id, true);
                selectedChallengeParticipants.Clear();
                selectedChallengeParticipants.AddRange(detailedChallenge.Scores);
                challengeParticipantsList.RefreshItems();
            }
            catch (Exception e)
            {
                // For open challenges the user hasn't joined, GetChallengeAsync will fail
                // because they don't have a challenge pointer record.
                // Clear participants list and continue.
                selectedChallengeParticipants.Clear();
                challengeParticipantsList.RefreshItems();

                Debug.LogWarning($"Could not fetch detailed challenge info: {e.Message}. This is expected for open challenges not yet joined.");
            }

            // Update button visibility based on challenge status and user participation
            UpdateChallengeButtons();
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

        private async Task RefreshCurrentTab()
        {
            // Helper method to refresh whichever tab is currently selected
            switch (selectedTabIndex)
            {
                case 0:
                    await UpdateTemplates();
                    break;
                case 1:
                    await UpdateChallenges();
                    break;
                case 2:
                    await UpdateOpenChallenges();
                    break;
                default:
                    Debug.LogError($"Invalid tab index: {selectedTabIndex}");
                    return;
            }
        }

        private async Task UpdateTemplates()
        {
            templates.Clear();

            try
            {
                // Fetch challenge templates
                challengeTemplates = await challengesSystem.GetTemplatesAsync();

                // Convert to list for binding
                foreach (var template in challengeTemplates.Templates)
                {
                    templates.Add(new KeyValuePair<string, IChallengeTemplate>(template.Key, template.Value));
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"Error fetching game modes: {e.Message}");
                errorPopup.style.display = DisplayStyle.Flex;
                errorMessage.text = e.Message;
                return;
            }

            // Check if template is assigned
            if (templateTemplate == null)
            {
                Debug.LogError("Game Mode Template is not assigned in the Inspector!");
                errorPopup.style.display = DisplayStyle.Flex;
                errorMessage.text = "Game Mode Template is not assigned. Please assign GameMode.uxml in the Inspector.";
                return;
            }

            // Setup ListView for game modes
            SetupListViewForTemplates();

            challengesList.RefreshItems();
            challengesList.ClearSelection();

            // Hide challenge detail panel when viewing game modes
            selectedChallengePanel.style.display = DisplayStyle.None;
        }

        private async Task UpdateChallenges()
        {
            challenges.Clear();

            try
            {
                // List Challenges that the user has joined
                var userChallengesResult = await challengesSystem.ListChallengesAsync(null);
                challenges.AddRange(userChallengesResult.Challenges);
            }
            catch (Exception e)
            {
                errorPopup.style.display = DisplayStyle.Flex;
                errorMessage.text = e.Message;
                return;
            }

            // Setup ListView for challenges
            SetupListViewForChallenges();

            challengesList.RefreshItems();
            challengesList.ClearSelection();

            // If we have a Challenge selected, try to select it if it still exists
            foreach (var challenge in challenges)
            {
                if (challenge.Id != selectedChallengeId) continue;

                _ = OnChallengeSelected(challenge);
                challengesList.SetSelection(challenges.IndexOf(challenge));
                return;
            }

            // If we don't find the previously selected Challenge, hide the selected Challenge panel
            selectedChallengePanel.style.display = DisplayStyle.None;
        }

        private async Task UpdateOpenChallenges()
        {
            challenges.Clear();

            try
            {
                // Get all active challenges (both open and invite-only)
                var searchResult = await challengesSystem.SearchChallengesAsync(
                    null,   // name - no filter
                    null,   // category - no filter
                    challengeEntriesLimit
                );

                challenges.AddRange(searchResult.Challenges);
            }
            catch (Exception e)
            {
                errorPopup.style.display = DisplayStyle.Flex;
                errorMessage.text = e.Message;
                return;
            }

            // Setup ListView for challenges
            SetupListViewForChallenges();

            challengesList.RefreshItems();
            challengesList.ClearSelection();

            // If we have a Challenge selected, try to select it if it still exists
            foreach (var challenge in challenges)
            {
                if (challenge.Id != selectedChallengeId) continue;

                _ = OnChallengeSelected(challenge);
                challengesList.SetSelection(challenges.IndexOf(challenge));
                return;
            }

            // If we don't find the previously selected Challenge, hide the selected Challenge panel
            selectedChallengePanel.style.display = DisplayStyle.None;
        }

        // Unity's ListView caches makeItem/bindItem callbacks and visual elements.
        // When we switch between Templates (TemplateView) and Challenges (ChallengeView),
        // we're displaying completely different data types with different UXML templates.
        //
        // Without clearing and rebuilding, the ListView would try to reuse old visual
        // elements (e.g., TemplateView items) for new data (e.g., IChallenge objects),
        // causing binding errors and visual glitches.
        //
        // The pattern is:
        // 1. Clear itemsSource (prevents binding during rebuild)
        // 2. Rebuild() (clears cached visual elements)
        // 3. Set new makeItem/bindItem (defines how to create/bind items)
        // 4. Set new itemsSource (provides the data)
        // 5. Rebuild() (regenerates visual elements with new callbacks)

        private void SetupListViewForTemplates()
        {
            challengesList.itemsSource = null;
            challengesList.Rebuild();

            challengesList.makeItem = () =>
            {
                var newListEntry = templateTemplate.Instantiate();
                var newListEntryLogic = new TemplateView();
                newListEntry.userData = newListEntryLogic;
                newListEntryLogic.SetVisualElement(newListEntry);
                return newListEntry;
            };
            challengesList.bindItem = (item, index) =>
            {
                var view = item.userData as TemplateView;
                var gameMode = templates[index];
                view?.SetTemplate(gameMode.Key, gameMode.Value);
            };

            challengesList.itemsSource = templates;
            challengesList.Rebuild();
        }

        private void SetupListViewForChallenges()
        {
            challengesList.itemsSource = null;
            challengesList.Rebuild();

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
            challengesList.Rebuild();
        }

        private void OnTemplateSelected(string templateId, IChallengeTemplate template)
        {

            // Store selected game mode
            selectedTemplateId = templateId;
            selectedTemplate = template;

            // Hide challenge panel, show game mode panel
            selectedChallengePanel.style.display = DisplayStyle.None;
            templateDetailPanel.style.display = DisplayStyle.Flex;

            // Populate game mode details
            templateDetailName.text = FormatTemplateName(templateId);

            // Get description from additional properties
            templateDetailDescription.text = template.AdditionalProperties.TryGetValue("description", out var description) ? description : "No description available.";

            // Get difficulty
            if (template.AdditionalProperties.TryGetValue("difficulty", out var difficulty))
            {
                templateDetailDifficulty.text = char.ToUpper(difficulty[0]) + difficulty.Substring(1);
            }
            else
            {
                templateDetailDifficulty.text = "Medium";
            }

            // Get category
            templateDetailCategory.text = template.AdditionalProperties.TryGetValue("category", out var category) ? FormatCategoryName(category) : "General";

            // Player range
            templateDetailPlayers.text = $"{template.Players.Min}-{template.Players.Max}";

            // Duration range
            templateDetailDuration.text = FormatDuration(template.Duration.MinSec, template.Duration.MaxSec);

            // Set default values for customizable fields
            templateMaxParticipants.value = (int)template.Players.Max;

            templateMaxScores.value = (int)template.MaxNumScore;

            // Clear invitees field
            templateInvitees.value = "";
        }

        private string FormatTemplateName(string templateId)
        {
            // Convert snake_case to Title Case (speed_runner -> Speed Runner)
            var words = templateId.Split('_');
            for (int i = 0; i < words.Length; i++)
            {
                if (words[i].Length > 0)
                {
                    words[i] = char.ToUpper(words[i][0]) + words[i].Substring(1);
                }
            }
            return string.Join(" ", words);
        }

        private string FormatCategoryName(string category)
        {
            return category.Replace("_", " ")
                .Replace("pvp", "PvP")
                .Replace("pve", "PvE");
        }

        private string FormatDuration(long minSec, long maxSec)
        {
            string minStr = minSec < 60 ? $"{minSec}s" : $"{minSec / 60}m";
            string maxStr = maxSec < 60 ? $"{maxSec}s" : $"{maxSec / 60}m";
            return $"{minStr} - {maxStr}";
        }

        // Consolidated challenge creation logic shared by both quick-start and advanced options
        private async Task CreateChallenge(
            string templateId,
            string challengeName,
            string description,
            string inviteesText,
            bool isOpen,
            int maxScores,
            long startDelaySec,
            long durationSec,
            int maxParticipants,
            string category,
            Dictionary<string, string> metadata)
        {
            try
            {
                // Parse and validate invitees
                string[] inviteeIDs = Array.Empty<string>();

                if (!string.IsNullOrEmpty(inviteesText))
                {
                    var inviteeUsernames = inviteesText
                        .Split(',')
                        .Select(username => username.Trim())
                        .Where(username => !string.IsNullOrEmpty(username))
                        .ToList();

                    if (inviteeUsernames.Count > 0)
                    {
                        var invitees = await nakamaSystem.Client.GetUsersAsync(
                            session: nakamaSystem.Session,
                            usernames: inviteeUsernames,
                            ids: null
                        );

                        inviteeIDs = invitees.Users.Select(user => user.Id).ToArray();

                        if (inviteeIDs.Length != inviteeUsernames.Count)
                        {
                            throw new Exception($"Could not find all users. Requested: {inviteeUsernames.Count}, Found: {inviteeIDs.Length}");
                        }

                        if (inviteeIDs.Length + 1 > maxParticipants)
                        {
                            throw new Exception($"Too many invitees. Max participants is {maxParticipants} (including you).");
                        }
                    }
                }
                else if (!isOpen)
                {
                    throw new Exception("Invite-only challenges require at least one invitee. Either add invitees or enable 'Open Challenge'.");
                }

                // Create the challenge
                var challenge = await challengesSystem.CreateChallengeAsync(
                    templateId,
                    challengeName,
                    description,
                    inviteeIDs,
                    isOpen,
                    maxScores,
                    startDelaySec,
                    durationSec,
                    maxParticipants,
                    category,
                    metadata
                );

                // Switch to My Challenges tab
                selectedTabIndex = 1;
                templatesTab.RemoveFromClassList("selected");
                myChallengesTab.AddToClassList("selected");
                openChallengesTab.RemoveFromClassList("selected");

                await RefreshCurrentTab();

                errorPopup.style.display = DisplayStyle.Flex;
                errorMessage.text = $"Challenge '{challenge.Name}' created successfully!";
            }
            catch (Exception e)
            {
                errorPopup.style.display = DisplayStyle.Flex;
                errorMessage.text = e.Message;
                throw;
            }
        }

        // Quick-start challenge creation from template panel
        private async Task CreateTemplateChallenge()
        {
            if (selectedTemplate == null || string.IsNullOrEmpty(selectedTemplateId))
            {
                errorPopup.style.display = DisplayStyle.Flex;
                errorMessage.text = "No template selected.";
                return;
            }

            // Calculate default duration from template
            long durationSec = (selectedTemplate.Duration.MinSec + selectedTemplate.Duration.MaxSec) / 2;

            // Get category from template
            string category = "general";
            if (selectedTemplate.AdditionalProperties.TryGetValue("category", out var cat))
            {
                category = cat;
            }

            try
            {
                await CreateChallenge(
                    templateId: selectedTemplateId,
                    challengeName: $"{FormatTemplateName(selectedTemplateId)} Challenge",
                    description: templateDetailDescription.text,
                    inviteesText: templateInvitees.value,
                    isOpen: templateOpenToggle.value,
                    maxScores: templateMaxScores.value,
                    startDelaySec: 0,
                    durationSec: durationSec,
                    maxParticipants: templateMaxParticipants.value,
                    category: category,
                    metadata: new Dictionary<string, string>()
                );
            }
            catch
            {
                // Error already handled in CreateChallenge
            }
        }

        // Advanced challenge creation from modal
        private async Task CreateCustomChallenge()
        {
            // Validate template selection
            if (modalTemplateDropdown.index < 0 || modalTemplateDropdown.index >= modalTemplateDropdown.choices.Count)
            {
                errorPopup.style.display = DisplayStyle.Flex;
                errorMessage.text = "Please select a valid challenge template.";
                return;
            }

            var templateId = modalTemplateDropdown.choices[modalTemplateDropdown.index];

            try
            {
                await CreateChallenge(
                    templateId: templateId,
                    challengeName: modalNameField.value,
                    description: "Challenge created via advanced options",
                    inviteesText: modalInvitees.value,
                    isOpen: modalOpenToggle.value,
                    maxScores: (int)modalMaxScoreSubmissions.value,
                    startDelaySec: modalChallengeDelay.value,
                    durationSec: modalChallengeDuration.value,
                    maxParticipants: modalMaxParticipantsField.value,
                    category: modalCategory.value,
                    metadata: new Dictionary<string, string>()
                );

                createModal.style.display = DisplayStyle.None;
            }
            catch
            {
                // Error already handled in CreateChallenge
            }
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

            _ = RefreshCurrentTab();
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

            _ = RefreshCurrentTab();
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

            _ = RefreshCurrentTab();
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
            _ = RefreshCurrentTab();
        }

        #endregion
    }
}