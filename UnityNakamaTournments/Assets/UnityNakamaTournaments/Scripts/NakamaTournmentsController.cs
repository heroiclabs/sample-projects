using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Nakama;
using UnityEngine;
using UnityEngine.UIElements;

namespace UnityNakamaTournaments
{
    [RequireComponent(typeof(UIDocument))]
    public class NakamaTournamentsController : MonoBehaviour
    {
        [Header("Tournament Settings")] [SerializeField]
        private int tournamentEntriesLimit = 20;
        [SerializeField]
        private int tournamentRecordEntriesLimit = 20;
        
        [Header("References")] [SerializeField]
        private VisualTreeAsset tournamentTemplate;
        [SerializeField]
        private VisualTreeAsset tournamentRecordTemplate;
        [field: SerializeField]
        public Texture2D[] RankMedals { get; private set; }

        public event Action<ISession, NakamaTournamentsController> OnInitialized;

        private VisualElement joinedControls;
        private VisualElement notJoinedControls;
        private Button tournamentJoinButton;
        private Button scoreSubmitButton;
        private Button scoreDeleteButton;
        private Button refreshButton;
        private Button previousButton;
        private Button nextButton;
        private EnumField operatorField;
        private LongField scoreField;
        private LongField subScoreField;
        private VisualElement selectedTournamentPanel;
        private ListView tournamentRecordsList;
        private ListView tournamentsList;
        private ScrollView recordsScrollView;
        private ScrollView tournamentsScrollView;
        private Label tournamentTitle;
        private Label tournamentDescription;

        private VisualElement errorPopup;
        private Button errorCloseButton;
        private Label errorMessage;

        private VisualElement ownerRecordElement;
        private TournamentRecordView ownerRecordView;

        private string prevCursor;
        private string nextCursor;
        private string selectedTournamentId;
        private IApiTournament selectedTournament;
        private readonly List<IApiTournament> tournaments = new();
        private readonly List<IApiLeaderboardRecord> tournamentRecords = new();

        #region Initialization
        private void Start()
        {
            InitializeUI();
            NakamaSingleton.Instance.ReceivedStartError += e =>
            {
                Debug.LogException(e);
                errorPopup.style.display = DisplayStyle.Flex;
                errorMessage.text = e.Message;
            };
            NakamaSingleton.Instance.ReceivedStartSuccess += session =>
            {
                OnInitialized?.Invoke(session, this);
                _ = UpdateTournamentsEntries();
            };
        }

        public void SwitchComplete(ISession newSession)
        {
            // For use with the account switcher editor tool.
            (NakamaSingleton.Instance.Session as Session)?.Update(newSession.AuthToken, newSession.RefreshToken);
            selectedTournament = null;
            selectedTournamentId = string.Empty;
            selectedTournamentPanel.style.display = DisplayStyle.None;
            tournamentsList.ClearSelection();
            _ = UpdateTournamentsEntries();
        }
        #endregion

        #region UI Binding
        private void InitializeUI()
        {
            var rootElement = GetComponent<UIDocument>().rootVisualElement;

            joinedControls = rootElement.Q<VisualElement>("joined-controls");
            notJoinedControls = rootElement.Q<VisualElement>("not-joined-controls");
    
            tournamentJoinButton = rootElement.Q<Button>("tournament-join");
            tournamentJoinButton.RegisterCallback<ClickEvent>(evt => _ = TournamentJoin());

            scoreSubmitButton = rootElement.Q<Button>("tournament-submit");
            scoreSubmitButton.RegisterCallback<ClickEvent>(evt => _ = TournamentSubmit());

            scoreDeleteButton = rootElement.Q<Button>("tournament-delete");
            scoreDeleteButton.RegisterCallback<ClickEvent>(evt => _ = TournamentDelete());

            operatorField = rootElement.Q<EnumField>("operator-field");
            scoreField = rootElement.Q<LongField>("score-field");
            subScoreField = rootElement.Q<LongField>("sub-score-field");

            selectedTournamentPanel = rootElement.Q<VisualElement>("selected-tournament-panel");

            refreshButton = rootElement.Q<Button>("refresh");
            refreshButton.RegisterCallback<ClickEvent>(evt => _ = OnTournamentSelected());

            previousButton = rootElement.Q<Button>("previous-page");
            previousButton.RegisterCallback<ClickEvent>(evt => _ = OnTournamentSelected(prevCursor));

            nextButton = rootElement.Q<Button>("next-page");
            nextButton.RegisterCallback<ClickEvent>(evt => _ = OnTournamentSelected(nextCursor));

            tournamentRecordsList = rootElement.Q<ListView>("tournament-records-list");
            tournamentRecordsList.makeItem = () =>
            {
                var newListEntry = tournamentRecordTemplate.Instantiate();
                var newListEntryLogic = new TournamentRecordView();
                newListEntry.userData = newListEntryLogic;
                newListEntryLogic.SetVisualElement(newListEntry, this);
                return newListEntry;
            };
            tournamentRecordsList.bindItem = (item, index) =>
            {
                (item.userData as TournamentRecordView)?.SetTournamentRecord(tournamentRecords[index]);
            };
            tournamentRecordsList.itemsSource = tournamentRecords;

            recordsScrollView = tournamentRecordsList.Q<ScrollView>();
            recordsScrollView.verticalScrollerVisibility = ScrollerVisibility.AlwaysVisible;

            tournamentsList = rootElement.Q<ListView>("tournaments-list");
            tournamentsList.makeItem = () =>
            {
                var newListEntry = tournamentTemplate.Instantiate();
                var newListEntryLogic = new TournamentView();
                newListEntry.userData = newListEntryLogic;
                newListEntryLogic.SetVisualElement(newListEntry);
                return newListEntry;
            };
            tournamentsList.bindItem = (item, index) =>
            {
                (item.userData as TournamentView)?.SetTournament(tournaments[index]);
            };
            tournamentsList.itemsSource = tournaments;
            tournamentsList.selectionChanged += objects => _ = OnTournamentSelected();

            tournamentsScrollView = tournamentsList.Q<ScrollView>();
            tournamentsScrollView.verticalScrollerVisibility = ScrollerVisibility.AlwaysVisible;

            errorPopup = rootElement.Q<VisualElement>("error-popup");
            errorMessage = rootElement.Q<Label>("error-message");
            errorCloseButton = rootElement.Q<Button>("error-close");
            errorCloseButton.RegisterCallback<ClickEvent>(_ => errorPopup.style.display = DisplayStyle.None);

            tournamentTitle = rootElement.Q<Label>("tournament-title");
            tournamentDescription = rootElement.Q<Label>("tournament-description");

            ownerRecordElement = rootElement.Q<VisualElement>("owner-record");
            ownerRecordView = new TournamentRecordView();
            ownerRecordElement.userData = ownerRecordView;
            ownerRecordView.SetVisualElement(ownerRecordElement, this);

            _ = OnTournamentSelected();
        }

        private async Task OnTournamentSelected(string cursor = null)
        {
            // Store the current Tournament, so we know which to fetch when refreshing.
            selectedTournament = tournamentsList.selectedItem as IApiTournament;

            if (selectedTournament == null)
            {
                selectedTournamentId = string.Empty;
                selectedTournamentPanel.style.display = DisplayStyle.None;
                return;
            }

            selectedTournamentId = selectedTournament.Id;

            try
            {
                // Fetch the specified range of records, as well as the session user's record, if there is one.
                var session = NakamaSingleton.Instance.Session;
                var result = await NakamaSingleton.Instance.Client.ListTournamentRecordsAsync(session,
                    selectedTournament.Id, new[] { session.UserId }, null, tournamentRecordEntriesLimit, cursor);

                // Store previous and next cursors to allow for pagination.
                prevCursor = result.PrevCursor;
                nextCursor = result.NextCursor;

                using var enumerator = result.OwnerRecords.GetEnumerator();
                if (enumerator.MoveNext())
                {
                    // Update the submit button to reflect the number of submissions and whether more scores can be submitted.
                    scoreSubmitButton.text = $"Submit ({enumerator.Current.NumScore}/{selectedTournament.MaxNumScore})";
                    scoreSubmitButton.SetEnabled(enumerator.Current.NumScore < selectedTournament.MaxNumScore);

                    // Display the session user's record in the Tournament separately at all times,
                    // regardless of which "page" we are on.
                    ownerRecordView.SetTournamentRecord(enumerator.Current);
                    ownerRecordElement.style.display = DisplayStyle.Flex;
                }
                else
                {
                    // If the user has no records, then they must be able to submit scores.
                    scoreSubmitButton.text = $"Submit (0/{selectedTournament.MaxNumScore})";
                    scoreSubmitButton.SetEnabled(true);
                    ownerRecordElement.style.display = DisplayStyle.None;
                }

                // After successfully fetching the desired records, replace the currently cached records with the new records.
                tournamentRecords.Clear();
                tournamentRecords.AddRange(result.Records);
            }
            catch (Exception e)
            {
                errorPopup.style.display = DisplayStyle.Flex;
                errorMessage.text = e.Message;
                return;
            }


            // Update the UI elements accordingly. The buttons will only be enabled if there are records to paginate to.
            // We then update the display of the Tournament records list, and move the scroller back to the top.
            tournamentRecordsList.RefreshItems();
            previousButton.SetEnabled(prevCursor != null);
            nextButton.SetEnabled(nextCursor != null);
            tournamentTitle.text = selectedTournament.Title;
            tournamentDescription.text = selectedTournament.Description;
            joinedControls.style.display = selectedTournament.CanEnter ? DisplayStyle.Flex : DisplayStyle.None; // TODO: Use new property
            notJoinedControls.style.display = !selectedTournament.CanEnter ? DisplayStyle.Flex : DisplayStyle.None; // TODO: Use new property
            selectedTournamentPanel.style.display = DisplayStyle.Flex;
            recordsScrollView.scrollOffset = Vector2.zero;
        }
        #endregion

        #region Tournaments
        private async Task UpdateTournamentsEntries()
        {
            try
            {
                // Fetch the Tournaments that meet the specified requirements.
                var session = NakamaSingleton.Instance.Session;
                var result =
                    await NakamaSingleton.Instance.Client.ListTournamentsAsync(session, 1, 1, null, null,
                        tournamentEntriesLimit);

                // After successfully fetching the desired Tournaments, replace the currently cached Tournaments with the new Tournaments.
                tournaments.Clear();
                tournaments.AddRange(result.Tournaments);
            }
            catch (Exception e)
            {
                errorPopup.style.display = DisplayStyle.Flex;
                errorMessage.text = e.Message;
                return;
            }


            // Update the UI elements accordingly. Update the selected Tournament view if it still exists.
            // We then update the display of the Tournaments list, and move the scroller back to the top.
            tournamentsList.RefreshItems();

            var found = false;
            foreach (var tournament in tournaments)
            {
                if (tournament.Id != selectedTournamentId) continue;
                found = true;
                break;
            }
            if (found)
            {
                _ = OnTournamentSelected();
                return;
            }

            selectedTournamentPanel.style.display = DisplayStyle.None;
            tournamentsScrollView.scrollOffset = Vector2.zero;
        }

        private async Task TournamentJoin()
        {
            if (selectedTournament is not { CanEnter: true }) return;

            try
            {
                // Attempt to join the selected Tournament.
                var session = NakamaSingleton.Instance.Session;
                await NakamaSingleton.Instance.Client.JoinTournamentAsync(session, selectedTournamentId);
            }
            catch (Exception e)
            {
                errorPopup.style.display = DisplayStyle.Flex;
                errorMessage.text = e.Message;
                return;
            }

            // After successfully joining the Tournament, update the Tournaments list.
            _ = UpdateTournamentsEntries();
        }

        private async Task TournamentSubmit()
        {
            if (selectedTournament == null) return;

            try
            {
                // Write the inputted score and sub-score to the server, using the specified operator.
                var session = NakamaSingleton.Instance.Session;
                await NakamaSingleton.Instance.Client.WriteTournamentRecordAsync(session, selectedTournamentId,
                    scoreField.value, subScoreField.value, null, (ApiOperator)operatorField.value);
            }
            catch (Exception e)
            {
                errorPopup.style.display = DisplayStyle.Flex;
                errorMessage.text = e.Message;
                return;
            }

            // After successfully writing the new scores, update the Tournaments list.
            _ = UpdateTournamentsEntries();
        }

        private async Task TournamentDelete()
        {
            if (selectedTournament == null) return;

            try
            {
                // Delete the session user's records.
                var session = NakamaSingleton.Instance.Session;
                await NakamaSingleton.Instance.Client.DeleteTournamentRecordAsync(session, selectedTournamentId);
            }
            catch (Exception e)
            {
                errorPopup.style.display = DisplayStyle.Flex;
                errorMessage.text = e.Message;
                return;
            }

            // After successfully deleting the record, update the Tournaments list.
            _ = UpdateTournamentsEntries();
        }
        #endregion
    }
}
