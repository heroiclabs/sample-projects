using Nakama;
using UnityEngine;
using UnityEngine.UIElements;

namespace SampleProjects.NakamaFriends
{
    public class FriendsRecordView
    {
        private Label usernameLabel;
        private Button addButton;
        private Button removeButton;
        private Button blockButton;
        private Button unblockButton;

        private NakamaFriendsController friendsController;

        public void SetVisualElement(VisualElement visualElement, NakamaFriendsController controller)
        {
            friendsController = controller;

            usernameLabel = visualElement.Q<Label>("username");

            addButton = visualElement.Q<Button>("add");
            addButton.RegisterCallback<ClickEvent>(AddFriend);

            removeButton = visualElement.Q<Button>("remove");
            removeButton.RegisterCallback<ClickEvent>(DeleteFriend);

            blockButton = visualElement.Q<Button>("block");
            blockButton.RegisterCallback<ClickEvent>(BlockFriend);

            // To unblock a user, we just need to reset their friend state, so we can just unfriend them, which will remove the block.
            unblockButton = visualElement.Q<Button>("unblock");
            unblockButton.RegisterCallback<ClickEvent>(DeleteFriend);
        }

        public void SetFriend(IApiFriend record)
        {
            usernameLabel.text = $"{record.User.Username}";
            var state = (NakamaFriendsController.FriendState)record.State;

            switch (state)
            {
                case NakamaFriendsController.FriendState.FRIEND:
                    addButton.style.display = DisplayStyle.None;
                    removeButton.style.display = DisplayStyle.Flex;
                    blockButton.style.display = DisplayStyle.Flex;
                    unblockButton.style.display = DisplayStyle.None;
                    break;
                case NakamaFriendsController.FriendState.INVITE_SENT:
                    addButton.style.display = DisplayStyle.None;
                    removeButton.style.display = DisplayStyle.Flex;
                    blockButton.style.display = DisplayStyle.None;
                    unblockButton.style.display = DisplayStyle.None;
                    break;
                case NakamaFriendsController.FriendState.INVITE_RECEIVED:
                    addButton.style.display = DisplayStyle.Flex;
                    removeButton.style.display = DisplayStyle.Flex;
                    blockButton.style.display = DisplayStyle.Flex;
                    unblockButton.style.display = DisplayStyle.None;
                    break;
                case NakamaFriendsController.FriendState.BLOCKED:
                    addButton.style.display = DisplayStyle.None;
                    removeButton.style.display = DisplayStyle.None;
                    blockButton.style.display = DisplayStyle.None;
                    unblockButton.style.display = DisplayStyle.Flex;
                    break;
                default:
                    Debug.LogError("Invalid friend state!");
                    break;
            }
        }

        private void AddFriend(ClickEvent _)
        {
            friendsController.AddFriend(usernameLabel.text);
        }

        private void DeleteFriend(ClickEvent _)
        {
            friendsController.DeleteFriend(usernameLabel.text);
        }

        private void BlockFriend(ClickEvent _)
        {
            friendsController.BlockFriend(usernameLabel.text);
        }
    }
}
