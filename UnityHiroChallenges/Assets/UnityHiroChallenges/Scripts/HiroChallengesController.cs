using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Nakama;
using Hiro;
using UnityEngine;
using UnityEngine.UIElements;
using Hiro.Unity;
using Hiro.System;
using System.Linq;

namespace SampleProjects.Challenges
{
    [RequireComponent(typeof(UIDocument))]
    public class HiroChallengesController : HiroCoordinator
    {
        [Header("Nakama Settings")] [SerializeField]
        private string scheme = "http";

        [SerializeField] private string host = "127.0.0.1";
        [SerializeField] private int port = 7350;
        [SerializeField] private string serverKey = "defaultkey";

        [Header("Hiro Settings")] [SerializeField]
        private const string PlayerPrefsAuthToken = "hiro.AuthToken";
        private const string PlayerPrefsRefreshToken = "hiro.RefreshToken";
        private const string PlayerPrefsDeviceId = "hiro.DeviceId";

        [Header("Challenge Settings")] [SerializeField]
        private int challengeEntriesLimit = 100;

        [SerializeField]
        private int challengeParticipantsLimit = 100;

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
        private TextField modalNameField;
        private TextField modalDescriptionField;
        private IntegerField modalMaxParticipantsField;
        private TextField modalInvitees;
        private Toggle modalOpenToggle;
        private Button modalCreateButton;
        private Button modalCloseButton;

        private VisualElement submitScoreModal;
        private IntegerField scoreField;
        private IntegerField subscoreField;
        private TextField scoreMetadataField;
        private Button submitScoreModalButton;
        private Button submitScoreModalCloseButton;

        private VisualElement errorPopup;
        private Button errorCloseButton;
        private Label errorMessage;

        public IClient Client { get; private set; }
        public ISession Session { get; private set; }
        private IChallengesSystem challengesSystem;

        private int selectedTabIndex;
        private string selectedChallengeId;
        private IChallenge selectedChallenge;
        private readonly List<IChallenge> challenges = new();
        private readonly List<IChallengeScore> selectedChallengeParticipants = new();

        #region Initialization

        public async Task SwitchComplete(ISession newSession)
        {
            Session = this.GetSystem<NakamaSystem>().Session;
            
            UpdateChallenges();
            OnInitialized?.Invoke(Session, this);
            
            //Debug.LogFormat("Account switch complete. New user: {0}", newSession.Username);
        }


        #endregion

        #region UI Binding

        private void InitializeUI()
        {
            var rootElement = GetComponent<UIDocument>().rootVisualElement;

            allTab = rootElement.Q<Button>("all-tab");
            allTab.RegisterCallback<ClickEvent>(_ =>
            {
                if (selectedTabIndex == 0) return;
                selectedTabIndex = 0;
                allTab.AddToClassList("selected");
                myChallengesTab.RemoveFromClassList("selected");
                UpdateChallenges();
            });

            myChallengesTab = rootElement.Q<Button>("my-challenges-tab");
            myChallengesTab.RegisterCallback<ClickEvent>(_ =>
            {
                if (selectedTabIndex == 1) return;
                selectedTabIndex = 1;
                myChallengesTab.AddToClassList("selected");
                allTab.RemoveFromClassList("selected");
                UpdateChallenges();
            });

            createButton = rootElement.Q<Button>("challenge-create");
            createButton.RegisterCallback<ClickEvent>(_ =>
            {
                modalNameField.value = string.Empty;
                modalDescriptionField.value = string.Empty;
                modalMaxParticipantsField.value = 100;
                modalInvitees.value = string.Empty;
                modalOpenToggle.value = true;
                createModal.style.display = DisplayStyle.Flex;
            });

            joinButton = rootElement.Q<Button>("challenge-join");
            joinButton.RegisterCallback<ClickEvent>(ChallengeJoin);

            leaveButton = rootElement.Q<Button>("challenge-leave");
            leaveButton.RegisterCallback<ClickEvent>(ChallengeLeave);

            claimRewardsButton = rootElement.Q<Button>("challenge-claim");
            claimRewardsButton.RegisterCallback<ClickEvent>(ChallengeClaim);

            submitScoreButton = rootElement.Q<Button>("challenge-submit-score");
            submitScoreButton.RegisterCallback<ClickEvent>(_ =>
            {
                scoreField.value = 0;
                subscoreField.value = 0;
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
                newListEntryLogic.SetVisualElement(newListEntry, this);
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
                newListEntryLogic.SetVisualElement(this, newListEntry);
                return newListEntry;
            };
            challengesList.bindItem = (item, index) => { (item.userData as ChallengeView)?.SetChallenge(challenges[index]); };
            challengesList.itemsSource = challenges;
            challengesList.selectionChanged += _ =>
            {
                Debug.LogFormat("Challenge Selected");
                if (challengesList.selectedItem is IChallenge)
                {
                    Debug.LogFormat("Challenge Selected is IChallenge");
                    OnChallengeSelected(challengesList.selectedItem as IChallenge);
                }
            };

            challengesScrollView = challengesList.Q<ScrollView>();
            challengesScrollView.verticalScrollerVisibility = ScrollerVisibility.AlwaysVisible;

            // Create Modal
            createModal = rootElement.Q<VisualElement>("create-modal");
            modalNameField = rootElement.Q<TextField>("create-modal-name");
            modalDescriptionField = rootElement.Q<TextField>("create-modal-description");
            modalMaxParticipantsField = rootElement.Q<IntegerField>("create-modal-max-participants");
            modalInvitees = rootElement.Q<TextField>("create-modal-invitees");
            modalOpenToggle = rootElement.Q<Toggle>("create-modal-open");
            modalCreateButton = rootElement.Q<Button>("create-modal-create");
            modalCreateButton.RegisterCallback<ClickEvent>(ChallengeCreate);
            modalCloseButton = rootElement.Q<Button>("create-modal-close");
            modalCloseButton.RegisterCallback<ClickEvent>(_ => createModal.style.display = DisplayStyle.None);

            // Submit Score Modal
            submitScoreModal = rootElement.Q<VisualElement>("submit-score-modal");
            scoreField = rootElement.Q<IntegerField>("submit-score-score");
            subscoreField = rootElement.Q<IntegerField>("submit-score-subscore");
            scoreMetadataField = rootElement.Q<TextField>("submit-score-metadata");
            submitScoreModalButton = rootElement.Q<Button>("submit-score-modal-submit");
            submitScoreModalButton.RegisterCallback<ClickEvent>(ChallengeSubmitScore);
            submitScoreModalCloseButton = rootElement.Q<Button>("submit-score-modal-close");
            submitScoreModalCloseButton.RegisterCallback<ClickEvent>(_ => submitScoreModal.style.display = DisplayStyle.None);

            errorPopup = rootElement.Q<VisualElement>("error-popup");
            errorMessage = rootElement.Q<Label>("error-message");
            errorCloseButton = rootElement.Q<Button>("error-close");
            errorCloseButton.RegisterCallback<ClickEvent>(_ => errorPopup.style.display = DisplayStyle.None);

            OnChallengeSelected(null);
        }

        private async void OnChallengeSelected(IChallenge challenge)
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
            selectedChallengeDescriptionLabel.text = selectedChallenge.Description ?? "No description set.";
            selectedChallengeStatusLabel.text = GetStatusString(selectedChallenge.IsActive);
            
            var endTime = UnixTimeToDateTime(selectedChallenge.EndTimeSec);
            selectedChallengeEndTimeLabel.text = endTime.ToString("MMM dd, yyyy HH:mm");

            // Get detailed challenge info with scores
            try
            {
                var detailedChallenge = await challengesSystem.GetChallengeAsync(selectedChallenge.Id, true);
                selectedChallengeParticipants.Clear();
                if (detailedChallenge.Scores != null)
                {
                    selectedChallengeParticipants.AddRange(detailedChallenge.Scores);
                }
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
            joinButton.style.display = isActive && selectedChallenge.Open && !isParticipant ? DisplayStyle.Flex : DisplayStyle.None;

            // Leave button: show if user is participant and challenge is not ended
            leaveButton.style.display = isParticipant && !isActive && !canClaim ? DisplayStyle.Flex : DisplayStyle.None;

            // Submit score button: show if user is participant and challenge is active
            submitScoreButton.style.display = isParticipant && isActive ? DisplayStyle.Flex : DisplayStyle.None;

            // Claim rewards button: show if challenge is ended and user can claim
            claimRewardsButton.style.display = !isActive && isParticipant && canClaim ? DisplayStyle.Flex : DisplayStyle.None;
        }

        private bool IsUserParticipant()
        {
            var nakamaSystem = this.GetSystem<NakamaSystem>();
            foreach (var participant in selectedChallengeParticipants)
            {
                if (participant.Id == nakamaSystem.UserId) return true;
            }
            return false;
        }

        #endregion

        #region Challenges

        private async void UpdateChallenges()
        {
            challenges.Clear();

            switch (selectedTabIndex)
            {
                case 0:
                    try
                    {
                        challengesSystem = this.GetSystem<ChallengesSystem>();

                        // List all Challenges.
                        var challengesResult = await challengesSystem.ListChallengesAsync(null);
                        challenges.AddRange(challengesResult.Challenges);
                    }
                    catch (Exception e)
                    {
                        Debug.LogFormat("ERROR: '{0}'", e);
                        errorPopup.style.display = DisplayStyle.Flex;
                        errorMessage.text = e.Message;
                        return;
                    }
                    break;
                case 1:
                    try
                    {
                        // List Challenges that the user has joined (we'll need to filter from all challenges)
                        var allChallengesResult = await challengesSystem.ListChallengesAsync(null);
                        var nakamaSystem = this.GetSystem<NakamaSystem>();
                        foreach (var challenge in allChallengesResult.Challenges)
                        {
                            // Check if user is participant
                            var detailedChallenge = await challengesSystem.GetChallengeAsync(challenge.Id, true);
                            await challengesSystem.GetTemplatesAsync();
                            if (detailedChallenge.Scores != null)
                            {
                                foreach (var score in detailedChallenge.Scores)
                                {
                                    if (score.Id == nakamaSystem.UserId)
                                    {
                                        challenges.Add(challenge);
                                        break;
                                    }
                                }
                            }
                        }
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
                
                OnChallengeSelected(challenge);
                challengesList.SetSelection(challenges.IndexOf(challenge));
                return;
            }

            // If we don't find the previously selected Challenge, hide the selected Challenge panel.
            selectedChallengePanel.style.display = DisplayStyle.None;
        }

        private async void ChallengeCreate(ClickEvent _)
        {
            try
            {
                var templateId = "speed_runner";
                var metadata = new Dictionary<string, string>();
                var nakamaSystem = this.GetSystem<NakamaSystem>();
                Debug.LogFormat("UserID: '{0}'", nakamaSystem.UserId);
                var invitee = await nakamaSystem.Client.GetUsersAsync(session: nakamaSystem.Session, usernames: new List<string> { modalInvitees.value }, ids: null);
                var inviteeID = invitee.Users.ElementAt(0).Id;
                Debug.LogFormat("Invitee: '{0}'", inviteeID);

                await challengesSystem.CreateChallengeAsync(
                    templateId,
                    modalNameField.value,
                    modalDescriptionField.value,
                    new[] { inviteeID },
                    modalOpenToggle.value,
                    10, // max score submissions
                    0, // challenge delay
                    2000, // challenge duration
                    modalMaxParticipantsField.value,
                    "race", // challenge category
                    metadata
                );
            }
            catch (Exception e)
            {
                errorPopup.style.display = DisplayStyle.Flex;
                errorMessage.text = e.Message;
                Debug.LogFormat("ERROR: '{0}'", e);
                return;
            }

            createModal.style.display = DisplayStyle.None;
            UpdateChallenges();
        }

        private async void ChallengeJoin(ClickEvent _)
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

            UpdateChallenges();
        }

        private async void ChallengeLeave(ClickEvent _)
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

            UpdateChallenges();
        }

        private async void ChallengeClaim(ClickEvent _)
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

            UpdateChallenges();
        }

        private async void ChallengeSubmitScore(ClickEvent _)
        {
            if (selectedChallenge == null) return;

            try
            {
                await challengesSystem.SubmitChallengeScoreAsync(
                    selectedChallenge.Id,
                    scoreField.value,
                    subscoreField.value,
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
            UpdateChallenges();
        }

        private string GetStatusString(bool isActive)
        {
                return isActive ? "Active" : "Ended";
        }

        private System.DateTime UnixTimeToDateTime(long unixTime)
        {
            return System.DateTimeOffset.FromUnixTimeSeconds(unixTime).DateTime;
        }

        #endregion

        #region Hiro System Setup

        protected override Task<Systems> CreateSystemsAsync()
        {
            var logger = new Hiro.Unity.Logger();

            // Set up network connectivity probes.
            var nakamaProbe = new NakamaClientNetworkProbe(TimeSpan.FromSeconds(60));
            var monitor = new NetworkMonitor(InternetReachabilityNetworkProbe.Default, nakamaProbe);
            monitor.ConnectivityChanged += (_, args) =>
            {
                Instance.Logger.InfoFormat($"Network is online: {args.Online}");
            };

            var nakamaSystem = new NakamaSystem(logger, scheme, host, port, serverKey, NakamaAuthorizerFunc(monitor), nakamaProbe);

            nakamaSystem.Client.ReceivedSessionUpdated += session =>
            {
                PlayerPrefs.SetString(PlayerPrefsAuthToken, session.AuthToken);
                PlayerPrefs.SetString(PlayerPrefsRefreshToken, session.RefreshToken);
            };

            // Store references for account switcher
            Client = nakamaSystem.Client;

            // Create our systems container
            var systems = new Systems("HiroSystemsContainer", monitor, logger);
            systems.Add(nakamaSystem);
            challengesSystem = new ChallengesSystem(logger, nakamaSystem);
            systems.Add(challengesSystem);

            return Task.FromResult(systems);
        }

        public Task<Systems> SwitchAccounts(String newDeviceID, int index)
        {
            var logger = new Hiro.Unity.Logger();

            // Set up network connectivity probes.
            var nakamaProbe = new NakamaClientNetworkProbe(TimeSpan.FromSeconds(60));
            var monitor = new NetworkMonitor(InternetReachabilityNetworkProbe.Default, nakamaProbe);
            monitor.ConnectivityChanged += (_, args) =>
            {
                Instance.Logger.InfoFormat($"Network is online: {args.Online}");
            };

            var nakamaSystem = new NakamaSystem(logger, scheme, host, port, serverKey, NakamaAuthorizerFunc(monitor, newDeviceID, index), nakamaProbe);

            nakamaSystem.Client.ReceivedSessionUpdated += session =>
            {
                PlayerPrefs.SetString(PlayerPrefsAuthToken, session.AuthToken);
                PlayerPrefs.SetString(PlayerPrefsRefreshToken, session.RefreshToken);
            };

            // Store references for account switcher
            Client = nakamaSystem.Client;

            // Create our systems container
            var systems = new Systems("HiroSystemsContainer", monitor, logger);
            systems.Add(nakamaSystem);
            challengesSystem = new ChallengesSystem(logger, nakamaSystem);
            systems.Add(challengesSystem);

            return Task.FromResult(systems);
        }

        private NakamaSystem.AuthorizerFunc NakamaAuthorizerFunc(INetworkMonitor monitor, string newDeviceID = null, int index = 0)
        {
            const string playerPrefsAuthToken = "nakama.AuthToken";
            const string playerPrefsRefreshToken = "nakama.RefreshToken";
            const string playerPrefsDeviceId = "nakama.DeviceId";

            return async client =>
            {
                client.ReceivedSessionUpdated += session =>
                {
                    PlayerPrefs.SetString(playerPrefsAuthToken + index.ToString(), session.AuthToken);
                    PlayerPrefs.SetString(playerPrefsRefreshToken + index.ToString(), session.RefreshToken);
                };

                var authToken = PlayerPrefs.GetString(playerPrefsAuthToken + index.ToString());
                var refreshToken = PlayerPrefs.GetString(playerPrefsRefreshToken + index.ToString());
                var session = Nakama.Session.Restore(authToken, refreshToken);
                Debug.Log("Session:" + session);

                // Add an hour, so we check whether the token is within an hour of expiration to refresh it.
                var expiredDate = DateTime.UtcNow.AddHours(1);

                if (session != null && (!monitor.Online || !session.HasRefreshExpired(expiredDate)))
                {
                    return session;
                }

                // Get device ID from PlayerPrefs (set by AccountSwitcher)
                var deviceId = string.IsNullOrEmpty(newDeviceID) ? PlayerPrefs.GetString(playerPrefsDeviceId + index.ToString(), SystemInfo.deviceUniqueIdentifier) : newDeviceID;
                if (deviceId == SystemInfo.unsupportedIdentifier)
                {
                    deviceId = Guid.NewGuid().ToString();
                    PlayerPrefs.SetString(playerPrefsDeviceId + index.ToString(), deviceId);
                }

                Debug.LogFormat("Logged in with device ID: '{0}'", deviceId);

                session = await client.AuthenticateDeviceAsync(deviceId);

                PlayerPrefs.SetString(playerPrefsDeviceId + index.ToString(), newDeviceID);
                PlayerPrefs.SetString(playerPrefsAuthToken + index.ToString(), session.AuthToken);
                PlayerPrefs.SetString(playerPrefsRefreshToken + index.ToString(), session.RefreshToken);

                if (session.Created)
                {
                    Debug.LogFormat("New user account '{0}' created.", session.UserId);
                }

                // Store session and socket for account switcher
                Session = session;

                return session;
            };
        }

        protected override async void SystemsInitializeCompleted()
        {
            Debug.Log("The challenges system is initialized!");
            var nakamaSystem = this.GetSystem<NakamaSystem>();

            InitializeUI();

            OnInitialized?.Invoke(Session, this);

            // Load existing challenges.
            UpdateChallenges();
        }

        protected override void SystemsInitializeFailed(Exception e)
        {
            Debug.LogException(e);
        }

    public object getSession()
    {
        return this.GetSystem<NakamaSystem>().Session;
    }

    #endregion
  }
}