using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Nakama;
using UnityEngine;
using UnityEngine.UIElements;

namespace UnityNakamaLeaderboards
{
    [RequireComponent(typeof(UIDocument))]
    public class NakamaLeaderboardsController : MonoBehaviour
    {
        [Header("Leaderboard Settings")] [SerializeField]
        private int recordsLimit = 20;
        
        [Header("References")] [SerializeField]
        private VisualTreeAsset listRecordTemplate;
        [field: SerializeField]
        public Texture2D[] RankMedals { get; private set; }

        public event Action<ISession, NakamaLeaderboardsController> OnInitialized;

        private Button weeklyTab;
        private Button globalTab;
        private Button submitButton;
        private Button deleteButton;
        private Button refreshButton;
        private Button previousButton;
        private Button nextButton;
        private EnumField operatorField;
        private LongField scoreField;
        private LongField subScoreField;
        private ListView recordsList;
        private ScrollView scrollView;

        private VisualElement errorPopup;
        private Button errorCloseButton;
        private Label errorMessage;

        private VisualElement ownerRecordElement;
        private LeaderboardRecordView ownerRecordView;

        private string prevCursor;
        private string nextCursor;
        private string selectedLeaderboardId;
        private readonly List<IApiLeaderboardRecord> leaderboardRecords = new();

        private const string WeeklyLeaderboardId = "weekly_leaderboard";
        private const string GlobalLeaderboardId = "global_leaderboard";
        
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
                // Load the Weekly Leaderboard by default.
                _ = UpdateLeaderboardRecords(WeeklyLeaderboardId);
            };
        }

        public void SwitchComplete(ISession newSession)
        {
            // For use with the account switcher editor tool.
            (NakamaSingleton.Instance.Session as Session)?.Update(newSession.AuthToken, newSession.RefreshToken);
            _ = UpdateLeaderboardRecords(selectedLeaderboardId);
        }
        #endregion

        #region UI Binding
        private void InitializeUI()
        {
            var rootElement = GetComponent<UIDocument>().rootVisualElement;

            weeklyTab = rootElement.Q<Button>("weekly-tab");
            weeklyTab.RegisterCallback<ClickEvent>(evt =>
            {
                if (selectedLeaderboardId == WeeklyLeaderboardId) return;
                weeklyTab.AddToClassList("selected");
                globalTab.RemoveFromClassList("selected");
                _ = UpdateLeaderboardRecords(WeeklyLeaderboardId);
            });

            globalTab = rootElement.Q<Button>("global-tab");
            globalTab.RegisterCallback<ClickEvent>(evt =>
            {
                if (selectedLeaderboardId == GlobalLeaderboardId) return;
                globalTab.AddToClassList("selected");
                weeklyTab.RemoveFromClassList("selected");
                _ = UpdateLeaderboardRecords(GlobalLeaderboardId);
            });

            submitButton = rootElement.Q<Button>("leaderboard-submit");
            submitButton.RegisterCallback<ClickEvent>(evt => _ = LeaderboardSubmit());

            deleteButton = rootElement.Q<Button>("leaderboard-delete");
            deleteButton.RegisterCallback<ClickEvent>(evt => _ = LeaderboardDelete());

            refreshButton = rootElement.Q<Button>("refresh");
            refreshButton.RegisterCallback<ClickEvent>(evt => _ = UpdateLeaderboardRecords(selectedLeaderboardId));

            previousButton = rootElement.Q<Button>("previous-page");
            previousButton.RegisterCallback<ClickEvent>(evt => _ = UpdateLeaderboardRecords(selectedLeaderboardId, prevCursor));

            nextButton = rootElement.Q<Button>("next-page");
            nextButton.RegisterCallback<ClickEvent>(evt => _ = UpdateLeaderboardRecords(selectedLeaderboardId, nextCursor));

            operatorField = rootElement.Q<EnumField>("operator-field");
            scoreField = rootElement.Q<LongField>("score-field");
            subScoreField = rootElement.Q<LongField>("sub-score-field");

            recordsList = rootElement.Q<ListView>("records-list");
            recordsList.makeItem = CreateLeaderboardRecord;
            recordsList.bindItem = (item, index) =>
            {
                (item.userData as LeaderboardRecordView)?.SetLeaderboardRecord(leaderboardRecords[index]);
            };
            recordsList.itemsSource = leaderboardRecords;

            scrollView = recordsList.Q<ScrollView>();
            scrollView.verticalScrollerVisibility = ScrollerVisibility.AlwaysVisible;

            ownerRecordElement = rootElement.Q<VisualElement>("owner-record");
            ownerRecordView = new LeaderboardRecordView();
            ownerRecordElement.userData = ownerRecordView;
            ownerRecordView.SetVisualElement(ownerRecordElement, this);

            errorPopup = rootElement.Q<VisualElement>("error-popup");
            errorMessage = rootElement.Q<Label>("error-message");
            errorCloseButton = rootElement.Q<Button>("error-close");
            errorCloseButton.RegisterCallback<ClickEvent>(_ => errorPopup.style.display = DisplayStyle.None);
        }

        private VisualElement CreateLeaderboardRecord()
        {
            var newListRecord = listRecordTemplate.Instantiate();
            var newListRecordLogic = new LeaderboardRecordView();
            newListRecord.userData = newListRecordLogic;
            newListRecordLogic.SetVisualElement(newListRecord, this);
            return newListRecord;
        }
        #endregion

        #region Leaderboards

        private async Task UpdateLeaderboardRecords(string leaderboardId, string cursor = null)
        {
            // Store the current Leaderboard, so we know which to fetch when refreshing.
            selectedLeaderboardId = leaderboardId;

            try
            {
                // Fetch the specified range of records, as well as the session user's record, if there is one.
                var session = NakamaSingleton.Instance.Session;
                var result = await NakamaSingleton.Instance.Client.ListLeaderboardRecordsAsync(
                    session, leaderboardId, new [] { session.UserId }, null, recordsLimit, cursor);

                // Store previous and next cursors to allow for pagination.
                prevCursor = result.PrevCursor;
                nextCursor = result.NextCursor;

                using var enumerator = result.OwnerRecords.GetEnumerator();
                if (enumerator.MoveNext())
                {
                    // Display the session user's record in the Leaderboard separately at all times,
                    // regardless of which "page" we are on.
                    ownerRecordView.SetLeaderboardRecord(enumerator.Current);
                    ownerRecordElement.style.display = DisplayStyle.Flex;
                }
                else
                {
                    // The session user may not have a record in the Leaderboard yet, in which case
                    // we simply hide the element.
                    ownerRecordElement.style.display = DisplayStyle.None;
                }

                // After successfully fetching the desired records, replace the currently cached records with the new records.
                leaderboardRecords.Clear();
                leaderboardRecords.AddRange(result.Records);
                
            }
            catch (Exception e)
            {
                errorPopup.style.display = DisplayStyle.Flex;
                errorMessage.text = e.Message;
                return;
            }

            // Update the UI elements accordingly. The buttons will only be enabled if there are records to paginate to.
            // We then update the display of the Leaderboard records list, and move the scroller back to the top.
            previousButton.SetEnabled(prevCursor != null);
            nextButton.SetEnabled(nextCursor != null);
            recordsList.RefreshItems();
            scrollView.scrollOffset = Vector2.zero;
        }

        private async Task LeaderboardSubmit()
        {
            try
            {
                // Write the inputted score and sub-score to the server, using the specified operator.
                var session = NakamaSingleton.Instance.Session;
                await NakamaSingleton.Instance.Client.WriteLeaderboardRecordAsync(
                    session,
                    selectedLeaderboardId,
                    scoreField.value,
                    subScoreField.value,
                    null,
                    (ApiOperator)operatorField.value);
            }
            catch (Exception e)
            {
                errorPopup.style.display = DisplayStyle.Flex;
                errorMessage.text = e.Message;
                return;
            }

            // After successfully writing the new scores, update the records list.
            _ = UpdateLeaderboardRecords(selectedLeaderboardId);
        }

        private async Task LeaderboardDelete()
        {
            try
            {
                // Delete the session user's record.
                var session = NakamaSingleton.Instance.Session;
                await NakamaSingleton.Instance.Client.DeleteLeaderboardRecordAsync(
                    session,
                    selectedLeaderboardId);
            }
            catch (Exception e)
            {
                errorPopup.style.display = DisplayStyle.Flex;
                errorMessage.text = e.Message;
                return;
            }

            // After successfully deleting the record, update the records list.
            _ = UpdateLeaderboardRecords(selectedLeaderboardId);
        }
        #endregion
    }
}
