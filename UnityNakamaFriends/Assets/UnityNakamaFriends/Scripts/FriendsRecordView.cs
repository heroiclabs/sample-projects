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

using Nakama;
using UnityEngine;
using UnityEngine.UIElements;

namespace UnityNakamaFriends.Scripts
{
    public class FriendsRecordView
    {
        private Label _usernameLabel;
        private Button _addButton;
        private Button _removeButton;
        private Button _blockButton;
        private Button _unblockButton;

        private NakamaFriendsController _friendsController;

        public void SetVisualElement(VisualElement visualElement, NakamaFriendsController controller)
        {
            _friendsController = controller;

            _usernameLabel = visualElement.Q<Label>("username");

            _addButton = visualElement.Q<Button>("add");
            _addButton.RegisterCallback<ClickEvent>(AddFriend);

            _removeButton = visualElement.Q<Button>("remove");
            _removeButton.RegisterCallback<ClickEvent>(DeleteFriend);

            _blockButton = visualElement.Q<Button>("block");
            _blockButton.RegisterCallback<ClickEvent>(BlockFriend);

            // To unblock a user, we just need to reset their friend state, so we can just unfriend them, which will remove the block.
            _unblockButton = visualElement.Q<Button>("unblock");
            _unblockButton.RegisterCallback<ClickEvent>(DeleteFriend);
        }

        public void SetFriend(IApiFriend record)
        {
            _usernameLabel.text = $"{record.User.Username}";
            var state = (NakamaFriendsController.FriendState)record.State;

            switch (state)
            {
                case NakamaFriendsController.FriendState.Friend:
                    _addButton.style.display = DisplayStyle.None;
                    _removeButton.style.display = DisplayStyle.Flex;
                    _blockButton.style.display = DisplayStyle.Flex;
                    _unblockButton.style.display = DisplayStyle.None;
                    break;
                case NakamaFriendsController.FriendState.InviteSent:
                    _addButton.style.display = DisplayStyle.None;
                    _removeButton.style.display = DisplayStyle.Flex;
                    _blockButton.style.display = DisplayStyle.None;
                    _unblockButton.style.display = DisplayStyle.None;
                    break;
                case NakamaFriendsController.FriendState.InviteReceived:
                    _addButton.style.display = DisplayStyle.Flex;
                    _removeButton.style.display = DisplayStyle.Flex;
                    _blockButton.style.display = DisplayStyle.Flex;
                    _unblockButton.style.display = DisplayStyle.None;
                    break;
                case NakamaFriendsController.FriendState.Blocked:
                    _addButton.style.display = DisplayStyle.None;
                    _removeButton.style.display = DisplayStyle.None;
                    _blockButton.style.display = DisplayStyle.None;
                    _unblockButton.style.display = DisplayStyle.Flex;
                    break;
                default:
                    Debug.LogError("Invalid friend state!");
                    break;
            }
        }

        private void AddFriend(ClickEvent ce) => _ = _friendsController.AddFriend(_usernameLabel.text);

        private void DeleteFriend(ClickEvent ce) => _ = _friendsController.DeleteFriend(_usernameLabel.text);

        private void BlockFriend(ClickEvent ce) => _ = _friendsController.BlockFriend(_usernameLabel.text);
    }
}
