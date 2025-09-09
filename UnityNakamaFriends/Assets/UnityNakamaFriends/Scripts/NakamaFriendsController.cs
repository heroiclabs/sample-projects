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
using Nakama;
using UnityEngine;
using UnityEngine.UIElements;

namespace UnityNakamaFriends.Scripts
{
    [RequireComponent(typeof(UIDocument))]
    public class NakamaFriendsController : MonoBehaviour
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

        public event Action<ISession, NakamaFriendsController> OnInitialized;

        private Label _tabTitle;
        private Button _friendsTab;
        private Button _sentTab;
        private Button _receivedTab;
        private Button _blockedTab;
        private Button _refreshButton;
        private VisualElement _friendsNotification;
        private VisualElement _receivedNotification;
        private Button _inviteFriendButton;
        private TextField _friendToInviteField;
        private ListView _recordsList;
        private ScrollView _scrollView;

        private VisualElement _errorPopup;
        private Button _errorCloseButton;
        private Label _errorMessage;

        private FriendState _selectedState = FriendState.Friend;
        private readonly List<IApiFriend> _friendRecords = new();

        #region Initialization

        private void Start()
        {
            InitializeUI();
            NakamaSingleton.Instance.ReceivedStartError += e =>
            {
                Debug.LogException(e);
                _errorPopup.style.display = DisplayStyle.Flex;
                _errorMessage.text = e.Message;
            };
            NakamaSingleton.Instance.Socket.ReceivedNotification += OnReceivedNotification;
            NakamaSingleton.Instance.ReceivedStartSuccess += session => OnInitialized?.Invoke(session, this);

            // Load friends by default.
            _ = UpdateFriendsList(FriendState.Friend);
        }

        private void OnReceivedNotification(IApiNotification notification)
        {
            switch (notification.Code)
            {
                case -2: // Incoming friend request received.
                    if (_selectedState == FriendState.InviteReceived)
                    {
                        _ = UpdateFriendsList(_selectedState);
                        _receivedNotification.style.display = DisplayStyle.Flex;
                    }

                    break;
                case -3: // Outgoing friend request accepted.
                    if (_selectedState == FriendState.Friend)
                    {
                        _ = UpdateFriendsList(_selectedState);
                        _friendsNotification.style.display = DisplayStyle.Flex;
                    }

                    break;
            }
        }

        public void SwitchComplete(ISession newSession)
        {
            // For use with the account switcher editor tool.
            (NakamaSingleton.Instance.Session as Session)?.Update(newSession.AuthToken, newSession.RefreshToken);
            _ = UpdateFriendsList(_selectedState);
            _friendsNotification.style.display = DisplayStyle.None;
            _receivedNotification.style.display = DisplayStyle.None;
        }

        #endregion

        #region UI Binding

        private void InitializeUI()
        {
            var rootElement = GetComponent<UIDocument>().rootVisualElement;

            _tabTitle = rootElement.Q<Label>("tab-title");

            _friendsTab = rootElement.Q<Button>("friends-tab");
            _friendsTab.RegisterCallback<ClickEvent>(ce =>
            {
                if (_selectedState == FriendState.Friend) return;
                _friendsTab.AddToClassList("selected");
                _sentTab.RemoveFromClassList("selected");
                _receivedTab.RemoveFromClassList("selected");
                _blockedTab.RemoveFromClassList("selected");
                _ = UpdateFriendsList(FriendState.Friend);
                _friendsNotification.style.display = DisplayStyle.None;
            });
            _friendsNotification = _friendsTab.Q<VisualElement>("red-dot");

            _sentTab = rootElement.Q<Button>("sent-tab");
            _sentTab.RegisterCallback<ClickEvent>(ce =>
            {
                if (_selectedState == FriendState.InviteSent) return;
                _sentTab.AddToClassList("selected");
                _friendsTab.RemoveFromClassList("selected");
                _receivedTab.RemoveFromClassList("selected");
                _blockedTab.RemoveFromClassList("selected");
                _ = UpdateFriendsList(FriendState.InviteSent);
            });

            _receivedTab = rootElement.Q<Button>("received-tab");
            _receivedTab.RegisterCallback<ClickEvent>(ce =>
            {
                if (_selectedState == FriendState.InviteReceived) return;
                _receivedTab.AddToClassList("selected");
                _friendsTab.RemoveFromClassList("selected");
                _sentTab.RemoveFromClassList("selected");
                _blockedTab.RemoveFromClassList("selected");
                _ = UpdateFriendsList(FriendState.InviteReceived);
                _receivedNotification.style.display = DisplayStyle.None;
            });
            _receivedNotification = _receivedTab.Q<VisualElement>("red-dot");

            _blockedTab = rootElement.Q<Button>("blocked-tab");
            _blockedTab.RegisterCallback<ClickEvent>(ce =>
            {
                if (_selectedState == FriendState.Blocked) return;
                _blockedTab.AddToClassList("selected");
                _friendsTab.RemoveFromClassList("selected");
                _sentTab.RemoveFromClassList("selected");
                _receivedTab.RemoveFromClassList("selected");
                _ = UpdateFriendsList(FriendState.Blocked);
            });

            _refreshButton = rootElement.Q<Button>("refresh");
            _refreshButton.RegisterCallback<ClickEvent>(ce => _ = UpdateFriendsList(_selectedState));

            _friendToInviteField = rootElement.Q<TextField>("friend-to-invite");

            _inviteFriendButton = rootElement.Q<Button>("invite-friend");
            _inviteFriendButton.RegisterCallback<ClickEvent>(ce =>
            {
                _ = AddFriend(_friendToInviteField.text);
                _friendToInviteField.value = string.Empty;
            });

            _recordsList = rootElement.Q<ListView>("records-list");
            _recordsList.makeItem = CreateFriendRecord;
            _recordsList.bindItem = (item, index) =>
            {
                (item.userData as FriendsRecordView)?.SetFriend(_friendRecords[index]);
            };
            _recordsList.itemsSource = _friendRecords;

            _scrollView = _recordsList.Q<ScrollView>();
            _scrollView.verticalScrollerVisibility = ScrollerVisibility.AlwaysVisible;

            _errorPopup = rootElement.Q<VisualElement>("error-popup");
            _errorMessage = rootElement.Q<Label>("error-message");
            _errorCloseButton = rootElement.Q<Button>("error-close");
            _errorCloseButton.RegisterCallback<ClickEvent>(_ => _errorPopup.style.display = DisplayStyle.None);
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

        private async Task UpdateFriendsList(FriendState friendState)
        {
            // Store the current friend state, so we know which to fetch when refreshing.
            _selectedState = friendState;

            try
            {
                // Fetch the specified records based on the selected friend state.
                var session = NakamaSingleton.Instance.Session;
                var result = await NakamaSingleton.Instance.Client.ListFriendsAsync(session, (int)friendState,
                    friendRecordsLimit, string.Empty);

                // After successfully fetching the desired records, replace the currently cached records with the new records.
                _friendRecords.Clear();
                _friendRecords.AddRange(result.Friends);
            }
            catch (Exception e)
            {
                _errorPopup.style.display = DisplayStyle.Flex;
                _errorMessage.text = e.Message;
                return;
            }

            // We then update the display of the Friends records list, and move the scroller back to the top.
            _recordsList.RefreshItems();
            _scrollView.scrollOffset = Vector2.zero;

            _tabTitle.text = friendState switch
            {
                FriendState.Friend => "My Friends",
                FriendState.InviteSent => "Pending Invites",
                FriendState.InviteReceived => "Friend Requests",
                FriendState.Blocked => "Blocked Users",
                _ => _tabTitle.text
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
                _errorPopup.style.display = DisplayStyle.Flex;
                _errorMessage.text = e.Message;
                return;
            }

            // After successfully adding the friend, update the friends list.
            _ = UpdateFriendsList(_selectedState);
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
            catch (Exception ex)
            {
                _errorPopup.style.display = DisplayStyle.Flex;
                _errorMessage.text = ex.Message;
                return;
            }

            // After successfully removing the friend, update the friends list.
            _ = UpdateFriendsList(_selectedState);
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
                _errorPopup.style.display = DisplayStyle.Flex;
                _errorMessage.text = e.Message;
                return;
            }

            // After successfully blocking the user, update the friends list.
            _ = UpdateFriendsList(_selectedState);
        }

        #endregion
    }
}
