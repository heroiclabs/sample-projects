using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Nakama;
using UnityEngine;
using UnityEngine.UIElements;

namespace SampleProjects.NakamaFriends
{
    [RequireComponent(typeof(UIDocument))]
    public class NakamaFriendsController : MonoBehaviour
    {
        public enum FriendState
        {
            FRIEND = 0,
            INVITE_SENT = 1,
            INVITE_RECEIVED = 2,
            BLOCKED = 3
        }

        [Header("Nakama Settings")]
        [SerializeField] private string scheme = "http";
        [SerializeField] private string host = "127.0.0.1";
        [SerializeField] private int port = 7350;
        [SerializeField] private string serverKey = "defaultkey";

        [Header("Friend List Settings")]
        [SerializeField] private int friendRecordsLimit = 1000;

        [Header("References")]
        [SerializeField] private VisualTreeAsset listRecordTemplate;

        public event Action<ISession, NakamaFriendsController> OnInitialized;

        private Label tabTitle;
        private Button friendsTab;
        private Button sentTab;
        private Button receivedTab;
        private Button blockedTab;
        private Button refreshButton;
        private VisualElement friendsNotification;
        private VisualElement receivedNotification;
        private Button inviteFriendButton;
        private TextField friendToInviteField;
        private ListView recordsList;
        private ScrollView scrollView;

        private VisualElement errorPopup;
        private Button errorCloseButton;
        private Label errorMessage;

        public Client Client { get; private set; }
        public ISession Session { get; private set; }
        public ISocket MainSocket { get; private set; }

        private FriendState selectedState = FriendState.FRIEND;
        private readonly List<IApiFriend> friendRecords = new();

        #region Initialization
        private async void Start()
        {
            InitializeUI();

            await AuthenticateWithDevice();

            OnInitialized?.Invoke(Session, this);

            // Load friends by default.
            UpdateFriendsList(FriendState.FRIEND);
        }

        private async Task AuthenticateWithDevice()
        {
            Client = new Client(scheme, host, port, serverKey, UnityWebRequestAdapter.Instance);

            // If the user's device ID is already stored, grab that - alternatively get the System's unique device identifier.
            var deviceId = PlayerPrefs.GetString("deviceId", SystemInfo.deviceUniqueIdentifier);

            // If the device identifier is invalid then let's generate a unique one.
            if (deviceId == SystemInfo.unsupportedIdentifier)
            {
                deviceId = Guid.NewGuid().ToString();
            }

            // Save the user's device ID to PlayerPrefs, so it can be retrieved during a later play session for re-authenticating.
            PlayerPrefs.SetString("deviceId", deviceId);

            try
            {
                Session = await Client.AuthenticateDeviceAsync($"{deviceId}_0");
                Debug.Log($"Authenticated {Session.Username} with Device ID");

                // Sockets are not required to use the Nakama Friends feature.
                // However, they can be useful to update UI in response to friend requests beging received/accepted.
                MainSocket = Client.NewSocket(true);
                await MainSocket.ConnectAsync(Session, true);
                MainSocket.ReceivedNotification += OnReceivedNotification;
            }
            catch (ApiResponseException ex)
            {
                errorPopup.style.display = DisplayStyle.Flex;
                errorMessage.text = ex.Message;
            }
        }

        private void OnReceivedNotification(IApiNotification notification)
        {
            switch (notification.Code)
            {
                case -2: // Incoming friend request received.
                    if (selectedState == FriendState.INVITE_RECEIVED)
                    {
                        UpdateFriendsList(selectedState);
                        receivedNotification.style.display = DisplayStyle.Flex;
                    }
                    break;
                case -3: // Outgoing friend request accepted.
                    if (selectedState == FriendState.FRIEND)
                    {
                        UpdateFriendsList(selectedState);
                        friendsNotification.style.display = DisplayStyle.Flex;
                    }
                    break;
            }
        }

        public void SwitchComplete(ISession newSession)
        {
            // For use with the account switcher editor tool.
            Session = newSession;
            UpdateFriendsList(selectedState);
            friendsNotification.style.display = DisplayStyle.None;
            receivedNotification.style.display = DisplayStyle.None;
        }
        #endregion

        #region UI Binding
        private void InitializeUI()
        {
            var rootElement = GetComponent<UIDocument>().rootVisualElement;

            tabTitle = rootElement.Q<Label>("tab-title");

            friendsTab = rootElement.Q<Button>("friends-tab");
            friendsTab.RegisterCallback<ClickEvent>(_ =>
            {
                if (selectedState == FriendState.FRIEND) return;
                friendsTab.AddToClassList("selected");
                sentTab.RemoveFromClassList("selected");
                receivedTab.RemoveFromClassList("selected");
                blockedTab.RemoveFromClassList("selected");
                UpdateFriendsList(FriendState.FRIEND);
                friendsNotification.style.display = DisplayStyle.None;
            });
            friendsNotification = friendsTab.Q<VisualElement>("red-dot");

            sentTab = rootElement.Q<Button>("sent-tab");
            sentTab.RegisterCallback<ClickEvent>(_ =>
            {
                if (selectedState == FriendState.INVITE_SENT) return;
                sentTab.AddToClassList("selected");
                friendsTab.RemoveFromClassList("selected");
                receivedTab.RemoveFromClassList("selected");
                blockedTab.RemoveFromClassList("selected");
                UpdateFriendsList(FriendState.INVITE_SENT);
            });

            receivedTab = rootElement.Q<Button>("received-tab");
            receivedTab.RegisterCallback<ClickEvent>(_ =>
            {
                if (selectedState == FriendState.INVITE_RECEIVED) return;
                receivedTab.AddToClassList("selected");
                friendsTab.RemoveFromClassList("selected");
                sentTab.RemoveFromClassList("selected");
                blockedTab.RemoveFromClassList("selected");
                UpdateFriendsList(FriendState.INVITE_RECEIVED);
                receivedNotification.style.display = DisplayStyle.None;
            });
            receivedNotification = receivedTab.Q<VisualElement>("red-dot");

            blockedTab = rootElement.Q<Button>("blocked-tab");
            blockedTab.RegisterCallback<ClickEvent>(_ =>
            {
                if (selectedState == FriendState.BLOCKED) return;
                blockedTab.AddToClassList("selected");
                friendsTab.RemoveFromClassList("selected");
                sentTab.RemoveFromClassList("selected");
                receivedTab.RemoveFromClassList("selected");
                UpdateFriendsList(FriendState.BLOCKED);
            });

            refreshButton = rootElement.Q<Button>("refresh");
            refreshButton.RegisterCallback<ClickEvent>(_ => UpdateFriendsList(selectedState));

            friendToInviteField = rootElement.Q<TextField>("friend-to-invite");

            inviteFriendButton = rootElement.Q<Button>("invite-friend");
            inviteFriendButton.RegisterCallback<ClickEvent>(_ =>
            {
                AddFriend(friendToInviteField.text);
                friendToInviteField.value = string.Empty;
            });

            recordsList = rootElement.Q<ListView>("records-list");
            recordsList.makeItem = CreateFriendRecord;
            recordsList.bindItem = (item, index) =>
            {
                (item.userData as FriendsRecordView)?.SetFriend(friendRecords[index]);
            };
            recordsList.itemsSource = friendRecords;

            scrollView = recordsList.Q<ScrollView>();
            scrollView.verticalScrollerVisibility = ScrollerVisibility.AlwaysVisible;

            errorPopup = rootElement.Q<VisualElement>("error-popup");
            errorMessage = rootElement.Q<Label>("error-message");
            errorCloseButton = rootElement.Q<Button>("error-close");
            errorCloseButton.RegisterCallback<ClickEvent>(_ => errorPopup.style.display = DisplayStyle.None);
        }

        private VisualElement CreateFriendRecord()
        {
            var newListRecord = listRecordTemplate.Instantiate();
            var newListRecordLogic = new FriendsRecordView();
            newListRecord.userData = newListRecordLogic;
            newListRecordLogic.SetVisualElement(newListRecord, this);
            return newListRecord;
        }
        #endregion

        #region Friends
        private async void UpdateFriendsList(FriendState friendState)
        {
            if (Session == null) return;

            // Store the current friend state, so we know which to fetch when refreshing.
            selectedState = friendState;

            try
            {
                // Fetch the specified records based on the selected friend state.
                var result = await Client.ListFriendsAsync(
                    Session,
                    (int)friendState,
                    friendRecordsLimit,
                    string.Empty
                );

                // After successfully fetching the desired records, replace the currently cached records with the new records.
                friendRecords.Clear();
                friendRecords.AddRange(result.Friends);
            }
            catch (Exception ex)
            {
                errorPopup.style.display = DisplayStyle.Flex;
                errorMessage.text = ex.Message;
                return;
            }

            // We then update the display of the Friends records list, and move the scroller back to the top.
            recordsList.RefreshItems();
            scrollView.scrollOffset = Vector2.zero;

            tabTitle.text = friendState switch
            {
                FriendState.FRIEND => "My Friends",
                FriendState.INVITE_SENT => "Pending Invites",
                FriendState.INVITE_RECEIVED => "Friend Requests",
                FriendState.BLOCKED => "Blocked Users",
                _ => tabTitle.text
            };
        }

        public async void AddFriend(string friendToAdd)
        {
            if (Session == null) return;

            var friendsToAdd = new[] { friendToAdd };

            try
            {
                // Add the selected user. This will accept a friend request if the friendToAdd has already sent a request to the user.
                await Client.AddFriendsAsync(
                    Session,
                    null,
                    friendsToAdd
                );
            }
            catch (Exception ex)
            {
                errorPopup.style.display = DisplayStyle.Flex;
                errorMessage.text = ex.Message;
                return;
            }

            // After successfully adding the friend, update the friends list.
            UpdateFriendsList(selectedState);
        }

        public async void DeleteFriend(string targetFriend)
        {
            if (Session == null) return;

            var friendToDelete = new[] { targetFriend };

            try
            {
                // Remove the selected friend. This will also unblock the user, if they are blocked.
                await Client.DeleteFriendsAsync(
                    Session,
                    null,
                    friendToDelete
                );
            }
            catch (Exception ex)
            {
                errorPopup.style.display = DisplayStyle.Flex;
                errorMessage.text = ex.Message;
                return;
            }

            // After successfully removing the friend, update the friends list.
            UpdateFriendsList(selectedState);
        }

        public async void BlockFriend(string targetFriend)
        {
            if (Session == null) return;

            var friendToBlock = new[] { targetFriend };

            try
            {
                // Block the selected user.
                await Client.BlockFriendsAsync(
                    Session,
                    null,
                    friendToBlock
                );
            }
            catch (Exception ex)
            {
                errorPopup.style.display = DisplayStyle.Flex;
                errorMessage.text = ex.Message;
                return;
            }

            // After successfully blocking the user, update the friends list.
            UpdateFriendsList(selectedState);
        }
        #endregion
    }
}
