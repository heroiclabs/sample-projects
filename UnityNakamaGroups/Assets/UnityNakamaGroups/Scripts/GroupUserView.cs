using System.Threading.Tasks;
using Nakama;
using UnityEngine.UIElements;

namespace NakamaGroups
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
            acceptButton.RegisterCallback<ClickEvent>(evt => _ = AcceptUser());

            declineButton = visualElement.Q<Button>("decline");
            declineButton.RegisterCallback<ClickEvent>(evt => _ = KickUser()); // Kicking is also used to decline invites.

            promoteButton = visualElement.Q<Button>("promote");
            promoteButton.RegisterCallback<ClickEvent>(evt => _ = PromoteUser());

            demoteButton = visualElement.Q<Button>("demote");
            demoteButton.RegisterCallback<ClickEvent>(evt => _ = DemoteUser());

            kickButton = visualElement.Q<Button>("kick");
            kickButton.RegisterCallback<ClickEvent>(evt => _ = KickUser());

            banButton = visualElement.Q<Button>("ban");
            banButton.RegisterCallback<ClickEvent>(evt => _ = BanUser());
        }

        public void SetGroupUser(GroupUserState viewerState, IGroupUserListGroupUser groupUser)
        {
            var userState = (GroupUserState)groupUser.State;

            userId = groupUser.User.Id;
            usernameLabel.text = groupUser.User.Username;
            roleLabel.text = userState.ToString();

            // Hide if user is self.
            var session = NakamaSingleton.Instance.Session;
            if (session.UserId == groupUser.User.Id)
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
                case GroupUserState.None:
                case GroupUserState.JoinRequest:
                case GroupUserState.Member:
                    acceptButton.style.display = DisplayStyle.None;
                    declineButton.style.display = DisplayStyle.None;
                    promoteButton.style.display = DisplayStyle.None;
                    demoteButton.style.display = DisplayStyle.None;
                    kickButton.style.display = DisplayStyle.None;
                    banButton.style.display = DisplayStyle.None;
                    break;
                // We can manage non-ADMIN or non-SUPERADMIN users, including accepting join requests.
                case GroupUserState.Admin:
                    acceptButton.style.display =
                        userState == GroupUserState.JoinRequest ? DisplayStyle.Flex : DisplayStyle.None;
                    declineButton.style.display =
                        userState == GroupUserState.JoinRequest ? DisplayStyle.Flex : DisplayStyle.None;
                    promoteButton.style.display =
                        userState == GroupUserState.Member ? DisplayStyle.Flex : DisplayStyle.None;
                    demoteButton.style.display =
                        userState == GroupUserState.Admin ? DisplayStyle.Flex : DisplayStyle.None;
                    kickButton.style.display =
                        userState == GroupUserState.Member ? DisplayStyle.Flex : DisplayStyle.None;
                    banButton.style.display = userState is GroupUserState.JoinRequest or GroupUserState.Member
                        ? DisplayStyle.Flex
                        : DisplayStyle.None;
                    break;
                // We have all possible privileges.
                case GroupUserState.SuperAdmin:
                    acceptButton.style.display =
                        userState == GroupUserState.JoinRequest ? DisplayStyle.Flex : DisplayStyle.None;
                    declineButton.style.display =
                        userState == GroupUserState.JoinRequest ? DisplayStyle.Flex : DisplayStyle.None;
                    promoteButton.style.display = userState is GroupUserState.Member or GroupUserState.Admin
                        ? DisplayStyle.Flex
                        : DisplayStyle.None;
                    demoteButton.style.display = userState is GroupUserState.SuperAdmin or GroupUserState.Admin
                        ? DisplayStyle.Flex
                        : DisplayStyle.None;
                    kickButton.style.display =
                        userState is GroupUserState.Member or GroupUserState.Admin or GroupUserState.SuperAdmin
                            ? DisplayStyle.Flex
                            : DisplayStyle.None;
                    banButton.style.display =
                        userState is GroupUserState.JoinRequest or GroupUserState.Member or GroupUserState.Admin
                            or GroupUserState.SuperAdmin
                            ? DisplayStyle.Flex
                            : DisplayStyle.None;
                    break;
            }
        }

        private Task AcceptUser() => groupsController.GroupAccept(userId);
        private Task PromoteUser() => groupsController.GroupPromote(userId);
        private Task DemoteUser() => groupsController.GroupDemote(userId);
        private Task KickUser() => groupsController.GroupKick(userId);
        private Task BanUser() => groupsController.GroupBan(userId);
    }
}
