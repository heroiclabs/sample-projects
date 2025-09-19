using Nakama;
using UnityEngine.UIElements;

namespace SampleProjects.Groups
{
    public class GroupUserView
    {
        private Label usernameLabel;
        private Label roleLabel;
        private Button acceptButton;
        private Button declineButton;
        private Button promoteButton;
        private Button demoteButton;
        private Button kickButton;
        private Button banButton;

        private string userId;
        private NakamaGroupsController groupsController;

        public void SetVisualElement(VisualElement visualElement, NakamaGroupsController controller)
        {
            groupsController = controller;

            usernameLabel = visualElement.Q<Label>("username");
            roleLabel = visualElement.Q<Label>("role");

            acceptButton = visualElement.Q<Button>("accept");
            acceptButton.RegisterCallback<ClickEvent>(AcceptUser);

            declineButton = visualElement.Q<Button>("decline");
            declineButton.RegisterCallback<ClickEvent>(KickUser); // Kicking is also used to decline invites.

            promoteButton = visualElement.Q<Button>("promote");
            promoteButton.RegisterCallback<ClickEvent>(PromoteUser);

            demoteButton = visualElement.Q<Button>("demote");
            demoteButton.RegisterCallback<ClickEvent>(DemoteUser);

            kickButton = visualElement.Q<Button>("kick");
            kickButton.RegisterCallback<ClickEvent>(KickUser);

            banButton = visualElement.Q<Button>("ban");
            banButton.RegisterCallback<ClickEvent>(BanUser);
        }

        public void SetGroupUser(GroupUserState viewerState, IGroupUserListGroupUser groupUser)
        {
            var userState = (GroupUserState)groupUser.State;

            userId = groupUser.User.Id;
            usernameLabel.text = groupUser.User.Username;
            roleLabel.text = userState.ToString();

            // Hide if user is self.
            if (groupsController.Session.UserId == groupUser.User.Id)
            {
                acceptButton.style.display = DisplayStyle.None;
                declineButton.style.display = DisplayStyle.None;
                promoteButton.style.display = DisplayStyle.None;
                demoteButton.style.display = DisplayStyle.None;
                kickButton.style.display = DisplayStyle.None;
                banButton.style.display = DisplayStyle.None;
                return;
            }

            switch (viewerState)
            {
                // We don't have permissions to manage the group.
                case GroupUserState.NONE:
                case GroupUserState.JOINREQUEST:
                case GroupUserState.MEMBER:
                    acceptButton.style.display = DisplayStyle.None;
                    declineButton.style.display = DisplayStyle.None;
                    promoteButton.style.display = DisplayStyle.None;
                    demoteButton.style.display = DisplayStyle.None;
                    kickButton.style.display = DisplayStyle.None;
                    banButton.style.display = DisplayStyle.None;
                    break;
                // We can manage non-ADMIN or non-SUPERADMIN users, including accepting join requests.
                case GroupUserState.ADMIN:
                    acceptButton.style.display =
                        userState == GroupUserState.JOINREQUEST ? DisplayStyle.Flex : DisplayStyle.None;
                    declineButton.style.display =
                        userState == GroupUserState.JOINREQUEST ? DisplayStyle.Flex : DisplayStyle.None;
                    promoteButton.style.display =
                        userState == GroupUserState.MEMBER ? DisplayStyle.Flex : DisplayStyle.None;
                    demoteButton.style.display =
                        userState == GroupUserState.ADMIN ? DisplayStyle.Flex : DisplayStyle.None;
                    kickButton.style.display =
                        userState == GroupUserState.MEMBER ? DisplayStyle.Flex : DisplayStyle.None;
                    banButton.style.display = userState is GroupUserState.JOINREQUEST or GroupUserState.MEMBER
                        ? DisplayStyle.Flex
                        : DisplayStyle.None;
                    break;
                // We have all possible privileges.
                case GroupUserState.SUPERADMIN:
                    acceptButton.style.display =
                        userState == GroupUserState.JOINREQUEST ? DisplayStyle.Flex : DisplayStyle.None;
                    declineButton.style.display =
                        userState == GroupUserState.JOINREQUEST ? DisplayStyle.Flex : DisplayStyle.None;
                    promoteButton.style.display = userState is GroupUserState.MEMBER or GroupUserState.ADMIN
                        ? DisplayStyle.Flex
                        : DisplayStyle.None;
                    demoteButton.style.display = userState is GroupUserState.SUPERADMIN or GroupUserState.ADMIN
                        ? DisplayStyle.Flex
                        : DisplayStyle.None;
                    kickButton.style.display =
                        userState is GroupUserState.MEMBER or GroupUserState.ADMIN or GroupUserState.SUPERADMIN
                            ? DisplayStyle.Flex
                            : DisplayStyle.None;
                    banButton.style.display =
                        userState is GroupUserState.JOINREQUEST or GroupUserState.MEMBER or GroupUserState.ADMIN
                            or GroupUserState.SUPERADMIN
                            ? DisplayStyle.Flex
                            : DisplayStyle.None;
                    break;
            }
        }

        private void AcceptUser(ClickEvent _)
        {
            groupsController.GroupAccept(userId);
        }

        private void PromoteUser(ClickEvent _)
        {
            groupsController.GroupPromote(userId);
        }

        private void DemoteUser(ClickEvent _)
        {
            groupsController.GroupDemote(userId);
        }

        private void KickUser(ClickEvent _)
        {
            groupsController.GroupKick(userId);
        }

        private void BanUser(ClickEvent _)
        {
            groupsController.GroupBan(userId);
        }
    }
}
