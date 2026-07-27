using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Nakama;
using Nakama.TinyJson;
using UnityEngine;
using UnityEngine.UIElements;

namespace FriendCodes
{
    [RequireComponent(typeof(UIDocument))]
    public class FriendCodesController : MonoBehaviour
    {
        public enum FriendState
        {
            Friend = 0,
            InviteSent = 1,
            InviteReceived = 2,
            Blocked = 3
        }

        [Header("Friend List Settings")] [SerializeField]
        private int friendRecordsLimit = 1000;

        [Header("References")] [SerializeField]
        private VisualTreeAsset listRecordTemplate;

        public event Action<ISession, FriendCodesController> OnInitialized;

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

        private Button generateFriendCodeButton;
        private TextField friendCodeField;
        private Button claimFriendCodeButton;
        
        private VisualElement errorPopup;
        private Button errorCloseButton;
        private Label errorMessage;

        private FriendState selectedState = FriendState.Friend;
        private readonly List<IApiFriend> friendRecords = new();

        #region Initialization

        private void OnEnable()
        {
            if (DeepLinkManager.Instance != null)
            {
                DeepLinkManager.Instance.OnInviteCodeReceived += HandleInviteCodeReceived;
            }
        }

        private void OnDisable()
        {
            if (DeepLinkManager.Instance != null)
            {
                DeepLinkManager.Instance.OnInviteCodeReceived -= HandleInviteCodeReceived;
            }
        }

        private void Start()
        {
            InitializeUI();
            NakamaSingleton.Instance.ReceivedStartError += e =>
            {
                Debug.LogException(e);
                errorPopup.style.display = DisplayStyle.Flex;
                errorMessage.text = e.Message;
            };
            NakamaSingleton.Instance.Socket.ReceivedNotification += OnReceivedNotification;
            NakamaSingleton.Instance.ReceivedStartSuccess += session =>
            {
                OnInitialized?.Invoke(session, this);

                // Load friends by default.
                _ = UpdateFriendsList(FriendState.Friend);

                // If a friend code was already waiting before we were ready, try to add them.
                var pending = DeepLinkManager.Instance != null ? DeepLinkManager.Instance.PendingInviteCode : null;
                if (!string.IsNullOrEmpty(pending))
                {
                    _ = ClaimFriendCode(pending);
                }
            };
        }

        private void OnReceivedNotification(IApiNotification notification)
        {
            switch (notification.Code)
            {
                case -2: // Incoming friend request received.
                    if (selectedState == FriendState.InviteReceived)
                    {
                        _ = UpdateFriendsList(selectedState);
                    }
                    else
                    {
                        receivedNotification.style.display = DisplayStyle.Flex;
                    }
                    break;
                case -3: // Outgoing friend request accepted.
                    if (selectedState == FriendState.Friend)
                    {
                        _ = UpdateFriendsList(selectedState);
                    }
                    else
                    {
                        friendsNotification.style.display = DisplayStyle.Flex;
                    }
                    break;
            }
        }

        public void SwitchComplete(ISession newSession)
        {
            // For use with the account switcher editor tool.
            (NakamaSingleton.Instance.Session as Session)?.Update(newSession.AuthToken, newSession.RefreshToken);
            _ = UpdateFriendsList(selectedState);
            friendsNotification.style.display = DisplayStyle.None;
            receivedNotification.style.display = DisplayStyle.None;
            friendCodeField.SetValueWithoutNotify(string.Empty);
        }
        #endregion

        #region UI Binding
        private void InitializeUI()
        {
            var rootElement = GetComponent<UIDocument>().rootVisualElement;

            tabTitle = rootElement.Q<Label>("tab-title");

            friendsTab = rootElement.Q<Button>("friends-tab");
            friendsTab.RegisterCallback<ClickEvent>(evt =>
            {
                if (selectedState == FriendState.Friend) return;
                friendsTab.AddToClassList("selected");
                sentTab.RemoveFromClassList("selected");
                receivedTab.RemoveFromClassList("selected");
                blockedTab.RemoveFromClassList("selected");
                _ = UpdateFriendsList(FriendState.Friend);
                friendsNotification.style.display = DisplayStyle.None;
            });
            friendsNotification = friendsTab.Q<VisualElement>("red-dot");

            sentTab = rootElement.Q<Button>("sent-tab");
            sentTab.RegisterCallback<ClickEvent>(evt =>
            {
                if (selectedState == FriendState.InviteSent) return;
                sentTab.AddToClassList("selected");
                friendsTab.RemoveFromClassList("selected");
                receivedTab.RemoveFromClassList("selected");
                blockedTab.RemoveFromClassList("selected");
                _ = UpdateFriendsList(FriendState.InviteSent);
            });

            receivedTab = rootElement.Q<Button>("received-tab");
            receivedTab.RegisterCallback<ClickEvent>(evt =>
            {
                if (selectedState == FriendState.InviteReceived) return;
                receivedTab.AddToClassList("selected");
                friendsTab.RemoveFromClassList("selected");
                sentTab.RemoveFromClassList("selected");
                blockedTab.RemoveFromClassList("selected");
                _ = UpdateFriendsList(FriendState.InviteReceived);
                receivedNotification.style.display = DisplayStyle.None;
            });
            receivedNotification = receivedTab.Q<VisualElement>("red-dot");

            blockedTab = rootElement.Q<Button>("blocked-tab");
            blockedTab.RegisterCallback<ClickEvent>(evt =>
            {
                if (selectedState == FriendState.Blocked) return;
                blockedTab.AddToClassList("selected");
                friendsTab.RemoveFromClassList("selected");
                sentTab.RemoveFromClassList("selected");
                receivedTab.RemoveFromClassList("selected");
                _ = UpdateFriendsList(FriendState.Blocked);
            });

            refreshButton = rootElement.Q<Button>("refresh");
            refreshButton.RegisterCallback<ClickEvent>(evt => _ = UpdateFriendsList(selectedState));

            friendToInviteField = rootElement.Q<TextField>("friend-to-invite");

            inviteFriendButton = rootElement.Q<Button>("invite-friend");
            inviteFriendButton.RegisterCallback<ClickEvent>(evt =>
            {
                _ = AddFriend(friendToInviteField.text);
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

            generateFriendCodeButton = rootElement.Q<Button>("generate-friend-code");
            generateFriendCodeButton.RegisterCallback<ClickEvent>(evt => _ = GenerateFriendCode());

            friendCodeField = rootElement.Q<TextField>("friend-code-input");

            claimFriendCodeButton = rootElement.Q<Button>("claim-friend-code");
            claimFriendCodeButton.RegisterCallback<ClickEvent>(evt => _ = ClaimFriendCode(friendCodeField.text));

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
        [Serializable]
        private class FriendCodeData
        {
            public string code;
        }
 
        [Serializable]
        private class ClaimCodeResponse
        {
            public bool success;
        }

        private async void HandleInviteCodeReceived(string code)
        {
            await ClaimFriendCode(code);
        }

        private async Task ClaimFriendCode(string code)
        {
            var payload = JsonUtility.ToJson(new FriendCodeData { code = code });

            try
            {
                var session = NakamaSingleton.Instance.Session;
                var result = await NakamaSingleton.Instance.Client.RpcAsync(session, "claim_friend_code", payload);
                var response = JsonUtility.FromJson<ClaimCodeResponse>(result.Payload);

                if (!response.success)
                {
                    Debug.LogWarning("Friend code claim failed.");
                    return;
                }
                Debug.Log($"Code claimed successfully.");
            }
            catch (Exception e)
            {
                Debug.LogWarning($"Friend code claim failed: {e.Message}");
                return;
            }

            // After successfully adding the friend, update the friends list.
            _ = UpdateFriendsList(selectedState);
        }

        private async Task GenerateFriendCode()
        {
            try
            {
                var session = NakamaSingleton.Instance.Session;
                var result = await NakamaSingleton.Instance.Client.RpcAsync(session, "generate_friend_code");
                var response = JsonUtility.FromJson<FriendCodeData>(result.Payload);

                Debug.Log($"Code generated successfully.");

                // Display code on UI and copy to clipboard.
                friendCodeField.SetValueWithoutNotify(response.code);
                GUIUtility.systemCopyBuffer = response.code;
            }
            catch (Exception e)
            {
                Debug.LogWarning($"Generate friend code failed: {e.Message}");
            }
        }

        private async Task UpdateFriendsList(FriendState friendState)
        {
            // Store the current friend state, so we know which to fetch when refreshing.
            selectedState = friendState;

            try
            {
                // Fetch the specified records based on the selected friend state.
                var session = NakamaSingleton.Instance.Session;
                var result = await NakamaSingleton.Instance.Client.ListFriendsAsync(session, (int)friendState,
                    friendRecordsLimit, string.Empty);

                // After successfully fetching the desired records, replace the currently cached records with the new records.
                friendRecords.Clear();
                friendRecords.AddRange(result.Friends);
            }
            catch (Exception e)
            {
                errorPopup.style.display = DisplayStyle.Flex;
                errorMessage.text = e.Message;
                return;
            }

            // We then update the display of the Friends records list, and move the scroller back to the top.
            recordsList.RefreshItems();
            scrollView.scrollOffset = Vector2.zero;

            tabTitle.text = friendState switch
            {
                FriendState.Friend => "My Friends",
                FriendState.InviteSent => "Pending Invites",
                FriendState.InviteReceived => "Friend Requests",
                FriendState.Blocked => "Blocked Users",
                _ => tabTitle.text
            };
        }

        public async Task AddFriend(string friendToAdd)
        {
            var friendsToAdd = new[] { friendToAdd };

            try
            {
                // Add the selected user. This will accept a friend request if the friendToAdd has already sent a request to the user.
                var session = NakamaSingleton.Instance.Session;
                await NakamaSingleton.Instance.Client.AddFriendsAsync(session, null, friendsToAdd);
            }
            catch (Exception e)
            {
                errorPopup.style.display = DisplayStyle.Flex;
                errorMessage.text = e.Message;
                return;
            }

            // After successfully adding the friend, update the friends list.
            _ = UpdateFriendsList(selectedState);
        }

        public async Task DeleteFriend(string targetFriend)
        {
            var friendToDelete = new[] { targetFriend };

            try
            {
                // Remove the selected friend. This will also unblock the user, if they are blocked.
                var session = NakamaSingleton.Instance.Session;
                await NakamaSingleton.Instance.Client.DeleteFriendsAsync(session, null, friendToDelete);
            }
            catch (Exception e)
            {
                errorPopup.style.display = DisplayStyle.Flex;
                errorMessage.text = e.Message;
                return;
            }

            // After successfully removing the friend, update the friends list.
            _ = UpdateFriendsList(selectedState);
        }

        public async Task BlockFriend(string targetFriend)
        {
            var friendToBlock = new[] { targetFriend };

            try
            {
                // Block the selected user.
                var session = NakamaSingleton.Instance.Session;
                await NakamaSingleton.Instance.Client.BlockFriendsAsync(session, null, friendToBlock);
            }
            catch (Exception e)
            {
                errorPopup.style.display = DisplayStyle.Flex;
                errorMessage.text = e.Message;
                return;
            }

            // After successfully blocking the user, update the friends list.
            _ = UpdateFriendsList(selectedState);
        }
        #endregion
    }
}
