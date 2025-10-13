using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Nakama;
using UnityEngine;
using UnityEngine.UIElements;

namespace NakamaGroups
{
    public enum GroupUserState
    {
        None = -1,
        SuperAdmin = 0,
        Admin = 1,
        Member = 2,
        JoinRequest = 3
    }

    [Serializable]
    public struct AvatarData
    {
        public int IconIndex;
        public int BackgroundIndex;
    }

    [RequireComponent(typeof(UIDocument))]
    public class NakamaGroupsController : MonoBehaviour
    {
        [Header("Group Settings")] [SerializeField]
        private int groupEntriesLimit = 100;
        [SerializeField]
        private int groupUserEntriesLimit = 100;

        [Header("References")] [SerializeField]
        private VisualTreeAsset groupEntryTemplate;
        [SerializeField]
        private VisualTreeAsset groupUserTemplate;
        [field: SerializeField]
        public Texture2D[] AvatarIcons { get; private set; }
        [field: SerializeField]
        public Texture2D[] AvatarBackgrounds { get; private set; }

        public event Action<ISession, NakamaGroupsController> OnInitialized;

        private Button allTab;
        private Button myGroupsTab;
        private Button createButton;
        private Button deleteButton;
        private Button joinButton;
        private Button leaveButton;
        private VisualElement selectedGroupAvatarIcon;
        private VisualElement selectedGroupAvatarBackground;
        private VisualElement selectedGroupPanel;
        private Label selectedGroupNameLabel;
        private Label selectedGroupDescriptionLabel;
        private ListView groupUsersList;
        private ListView groupsList;
        private ScrollView groupsScrollView;
        private ScrollView groupUsersScrollView;

        private VisualElement createModal;
        private TextField modalNameField;
        private TextField modalDescriptionField;
        private IntegerField modalMaxCountField;
        private Toggle modalPublicToggle;
        private VisualElement modalAvatarBackground;
        private VisualElement modalAvatarIcon;
        private VisualElement modalPreviousBackgroundButton;
        private VisualElement modalNextBackgroundButton;
        private VisualElement modalPreviousIconButton;
        private VisualElement modalNextIconButton;
        private Button modalCreateButton;
        private Button modalCloseButton;

        private VisualElement errorPopup;
        private Button errorCloseButton;
        private Label errorMessage;

        private int selectedTabIndex;
        private int selectedAvatarBackgroundIndex;
        private int selectedAvatarIconIndex;
        private string selectedGroupId;
        private IApiGroup selectedGroup;
        private readonly List<IApiGroup> groups = new();
        private readonly List<IGroupUserListGroupUser> selectedGroupUsers = new();

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
                // Load existing groups.
                _ = UpdateGroups();
            };
        }

        public void SwitchComplete(ISession newSession)
        {
            // For use with the account switcher editor tool.
            (NakamaSingleton.Instance.Session as Session)?.Update(newSession.AuthToken, newSession.RefreshToken);
            _ = UpdateGroups();
        }

        #endregion

        #region UI Binding

        private void InitializeUI()
        {
            var rootElement = GetComponent<UIDocument>().rootVisualElement;

            allTab = rootElement.Q<Button>("all-tab");
            allTab.RegisterCallback<ClickEvent>(evt =>
            {
                if (selectedTabIndex == 0) return;
                selectedTabIndex = 0;
                allTab.AddToClassList("selected");
                myGroupsTab.RemoveFromClassList("selected");
                _ = UpdateGroups();
            });

            myGroupsTab = rootElement.Q<Button>("my-groups-tab");
            myGroupsTab.RegisterCallback<ClickEvent>(evt =>
            {
                if (selectedTabIndex == 1) return;
                selectedTabIndex = 1;
                myGroupsTab.AddToClassList("selected");
                allTab.RemoveFromClassList("selected");
                _ = UpdateGroups();
            });

            createButton = rootElement.Q<Button>("group-create");
            createButton.RegisterCallback<ClickEvent>(_ =>
            {
                selectedAvatarBackgroundIndex = 0;
                selectedAvatarIconIndex = 0;
                modalNameField.value = string.Empty;
                modalDescriptionField.value = string.Empty;
                modalMaxCountField.value = 20;
                modalPublicToggle.value = false;
                UpdateCreateModal();
                createModal.style.display = DisplayStyle.Flex;
            });

            deleteButton = rootElement.Q<Button>("group-delete");
            deleteButton.RegisterCallback<ClickEvent>(evt => _ = GroupDelete());

            joinButton = rootElement.Q<Button>("group-join");
            joinButton.RegisterCallback<ClickEvent>(evt => _ = GroupJoin());

            leaveButton = rootElement.Q<Button>("group-leave");
            leaveButton.RegisterCallback<ClickEvent>(evt => _ = GroupLeave());

            selectedGroupPanel = rootElement.Q<VisualElement>("selected-group-panel");
            selectedGroupAvatarIcon = rootElement.Q<VisualElement>("selected-group-avatar-icon");
            selectedGroupAvatarBackground = rootElement.Q<VisualElement>("selected-group-avatar-background");
            selectedGroupNameLabel = rootElement.Q<Label>("selected-group-name");
            selectedGroupDescriptionLabel = rootElement.Q<Label>("selected-group-description");

            groupUsersList = rootElement.Q<ListView>("group-users-list");
            groupUsersList.makeItem = () =>
            {
                var newListEntry = groupUserTemplate.Instantiate();
                var newListEntryLogic = new GroupUserView();
                newListEntry.userData = newListEntryLogic;
                newListEntryLogic.SetVisualElement(newListEntry, this);
                return newListEntry;
            };
            groupUsersList.bindItem = (item, index) =>
            {
                var session = NakamaSingleton.Instance.Session;
                var viewerUser = default(IGroupUserListGroupUser);
                foreach (var user in selectedGroupUsers)
                {
                    if (user.User.Id != session.UserId) continue;
                    viewerUser = user;
                    break;
                }
            
                var viewerState = GroupUserState.None;
                if (viewerUser != null)
                {
                    viewerState = (GroupUserState)viewerUser.State;
                }
            
                (item.userData as GroupUserView)?.SetGroupUser(viewerState, selectedGroupUsers[index]);
            };
            groupUsersList.itemsSource = selectedGroupUsers;

            groupUsersScrollView = groupUsersList.Q<ScrollView>();
            groupUsersScrollView.verticalScrollerVisibility = ScrollerVisibility.AlwaysVisible;

            groupsList = rootElement.Q<ListView>("groups-list");
            groupsList.makeItem = () =>
            {
                var newListEntry = groupEntryTemplate.Instantiate();
                var newListEntryLogic = new GroupView();
                newListEntry.userData = newListEntryLogic;
                newListEntryLogic.SetVisualElement(this, newListEntry);
                return newListEntry;
            };
            groupsList.bindItem = (item, index) => { (item.userData as GroupView)?.SetGroup(groups[index]); };
            groupsList.itemsSource = groups;
            groupsList.selectionChanged += objects =>
            {
                if (groupsList.selectedItem is IApiGroup)
                {
                    _ = OnGroupSelected(groupsList.selectedItem as IApiGroup);
                }
            };

            groupsScrollView = groupsList.Q<ScrollView>();
            groupsScrollView.verticalScrollerVisibility = ScrollerVisibility.AlwaysVisible;

            createModal = rootElement.Q<VisualElement>("create-modal");
            modalNameField = rootElement.Q<TextField>("create-modal-name");
            modalDescriptionField = rootElement.Q<TextField>("create-modal-description");
            modalMaxCountField = rootElement.Q<IntegerField>("create-modal-max-count");
            modalPublicToggle = rootElement.Q<Toggle>("create-modal-public");
            modalAvatarBackground = rootElement.Q<VisualElement>("create-modal-avatar-background");
            modalAvatarIcon = rootElement.Q<VisualElement>("create-modal-avatar-icon");
            modalPreviousBackgroundButton = rootElement.Q<VisualElement>("create-modal-previous-background");
            modalPreviousBackgroundButton.RegisterCallback<ClickEvent>(_ =>
            {
                selectedAvatarBackgroundIndex--;
                if (selectedAvatarBackgroundIndex < 0)
                {
                    selectedAvatarBackgroundIndex = AvatarBackgrounds.Length - 1;
                }
                UpdateCreateModal();
            });
            modalNextBackgroundButton = rootElement.Q<VisualElement>("create-modal-next-background");
            modalNextBackgroundButton.RegisterCallback<ClickEvent>(_ =>
            {
                selectedAvatarBackgroundIndex++;
                if (selectedAvatarBackgroundIndex == AvatarBackgrounds.Length)
                {
                    selectedAvatarBackgroundIndex = 0;
                }
                UpdateCreateModal();
            });
            modalPreviousIconButton = rootElement.Q<VisualElement>("create-modal-previous-icon");
            modalPreviousIconButton.RegisterCallback<ClickEvent>(_ =>
            {
                selectedAvatarIconIndex--;
                if (selectedAvatarIconIndex < 0)
                {
                    selectedAvatarIconIndex = AvatarIcons.Length - 1;
                }
                UpdateCreateModal();
            });
            modalNextIconButton = rootElement.Q<VisualElement>("create-modal-next-icon");
            modalNextIconButton.RegisterCallback<ClickEvent>(_ =>
            {
                selectedAvatarIconIndex++;
                if (selectedAvatarIconIndex == AvatarIcons.Length)
                {
                    selectedAvatarIconIndex = 0;
                }
                UpdateCreateModal();
            });
            modalCreateButton = rootElement.Q<Button>("create-modal-create");
            modalCreateButton.RegisterCallback<ClickEvent>(evt => _ = GroupCreate());
            modalCloseButton = rootElement.Q<Button>("create-modal-close");
            modalCloseButton.RegisterCallback<ClickEvent>(_ => createModal.style.display = DisplayStyle.None);

            errorPopup = rootElement.Q<VisualElement>("error-popup");
            errorMessage = rootElement.Q<Label>("error-message");
            errorCloseButton = rootElement.Q<Button>("error-close");
            errorCloseButton.RegisterCallback<ClickEvent>(_ => errorPopup.style.display = DisplayStyle.None);

            _ = OnGroupSelected(null);
        }

        private async Task OnGroupSelected(IApiGroup group)
        {
            if (group == null)
            {
                selectedGroupId = string.Empty;
                selectedGroupPanel.style.display = DisplayStyle.None;
                return;
            }

            selectedGroup = group;
            selectedGroupId = selectedGroup.Id;

            var avatarData = JsonUtility.FromJson<AvatarData>(selectedGroup.AvatarUrl);
            if (avatarData.IconIndex >= 0 && avatarData.IconIndex < AvatarIcons.Length)
            {
                selectedGroupAvatarIcon.style.backgroundImage = AvatarIcons[avatarData.IconIndex];
            }
            if (avatarData.BackgroundIndex >= 0 && avatarData.BackgroundIndex < AvatarBackgrounds.Length)
            {
                selectedGroupAvatarBackground.style.backgroundImage = AvatarBackgrounds[avatarData.BackgroundIndex];
            }

            selectedGroupNameLabel.text = selectedGroup.Name;
            selectedGroupDescriptionLabel.text = selectedGroup.Description ?? "No description set.";

            var session = NakamaSingleton.Instance.Session;
            var groupUsers = await NakamaSingleton.Instance.Client.ListGroupUsersAsync(session, selectedGroup.Id, null,
                groupUserEntriesLimit, string.Empty);
            selectedGroupUsers.Clear();
            selectedGroupUsers.AddRange(groupUsers.GroupUsers);
            groupUsersList.RefreshItems();
            var viewerUser = default(IGroupUserListGroupUser);
            foreach (var user in selectedGroupUsers)
            {
                if (user.User.Id != session.UserId) continue;
                viewerUser = user;
                break;
            }

            if (selectedGroup.EdgeCount < selectedGroup.MaxCount && viewerUser == null)
            {
                joinButton.style.display = DisplayStyle.Flex;
            }
            else
            {
                joinButton.style.display = DisplayStyle.None;
            }

            if (viewerUser != null)
            {
                leaveButton.style.display = DisplayStyle.Flex;
                deleteButton.style.display = viewerUser.State == (int)GroupUserState.SuperAdmin
                    ? DisplayStyle.Flex
                    : DisplayStyle.None;
            }
            else
            {
                leaveButton.style.display = DisplayStyle.None;
                deleteButton.style.display = DisplayStyle.None;
            }

            selectedGroupPanel.style.display = DisplayStyle.Flex;
        }

        #endregion

        #region Groups

        private async Task UpdateGroups()
        {
            groups.Clear();

            var session = NakamaSingleton.Instance.Session;
            switch (selectedTabIndex)
            {
                case 0:
                    try
                    {
                        // List all Groups.
                        var groupsResult =
                            await NakamaSingleton.Instance.Client.ListGroupsAsync(session, null, groupEntriesLimit);
                        groups.AddRange(groupsResult.Groups);
                    }
                    catch (Exception e)
                    {
                        errorPopup.style.display = DisplayStyle.Flex;
                        errorMessage.text = e.Message;
                        return;
                    }
                    break;
                case 1:
                    try
                    {
                        // List Groups that the user has created/joined.
                        var userGroupsResult =
                            await NakamaSingleton.Instance.Client.ListUserGroupsAsync(session, null, groupEntriesLimit,
                                string.Empty);
                        foreach (var userGroup in userGroupsResult.UserGroups)
                        {
                            groups.Add(userGroup.Group);
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

            groupsList.RefreshItems();
            groupsList.ClearSelection();

            // If we have a Group selected, then update Groups, try to select that Group, if it still exists.
            foreach (var group in groups)
            {
                if (group.Id != selectedGroupId) continue;
                
                _ = OnGroupSelected(group);
                groupsList.SetSelection(groups.IndexOf(group));
                return;
            }

            // If we don't find the previously selected Group, hide the selected Group panel.
            selectedGroupPanel.style.display = DisplayStyle.None;
        }

        private void UpdateCreateModal()    
        {
            // Update the preview for the Group's avatar logo, which is made up of a background, and an icon.
            if (selectedAvatarBackgroundIndex >= 0 && selectedAvatarBackgroundIndex < AvatarBackgrounds.Length)
            {
                modalAvatarBackground.style.backgroundImage = AvatarBackgrounds[selectedAvatarBackgroundIndex];
            }
            if (selectedAvatarIconIndex >= 0 && selectedAvatarIconIndex < AvatarIcons.Length)
            {
                modalAvatarIcon.style.backgroundImage = AvatarIcons[selectedAvatarIconIndex];
            }
        }

        private async Task GroupCreate()
        {
            try
            {
                // Take the selected avatar icon and background, and convert it into a JSON object.
                var avatarDataJson = JsonUtility.ToJson(new AvatarData
                {
                    BackgroundIndex = selectedAvatarBackgroundIndex,
                    IconIndex = selectedAvatarIconIndex
                });

                // Use the user defined parameters to create a new Group.
                var session = NakamaSingleton.Instance.Session;
                await NakamaSingleton.Instance.Client.CreateGroupAsync(session, modalNameField.value,
                    modalDescriptionField.value, avatarDataJson, null, modalPublicToggle.value,
                    modalMaxCountField.value);
            }
            catch (ApiResponseException e)
            {
                errorPopup.style.display = DisplayStyle.Flex;
                errorMessage.text = e.Message;
                return;
            }

            // After successfully creating the Group, hide the create modal, and update the Groups list.
            createModal.style.display = DisplayStyle.None;
            _ = UpdateGroups();
        }

        private async Task GroupDelete()
        {
            if (selectedGroup == null) return;

            try
            {
                // Attempt to delete the selected Group, only works if the user is a SUPERADMIN.
                var session = NakamaSingleton.Instance.Session;
                await NakamaSingleton.Instance.Client.DeleteGroupAsync(session, selectedGroup.Id);
                groupsList.ClearSelection();
            }
            catch (ApiResponseException e)
            {
                errorPopup.style.display = DisplayStyle.Flex;
                errorMessage.text = e.Message;
                return;
            }

            // After successfully deleting the Group, update the Groups list.
            _ = UpdateGroups();
        }

        private async Task GroupJoin()
        {
            if (selectedGroup == null) return;

            try
            {
                // Attempt to join the selected Group, if the Group is private, the user will need to be accepted before
                // being able to access the Group.
                var session = NakamaSingleton.Instance.Session;
                await NakamaSingleton.Instance.Client.JoinGroupAsync(session, selectedGroup.Id);
            }
            catch (ApiResponseException e)
            {
                errorPopup.style.display = DisplayStyle.Flex;
                errorMessage.text = e.Message;
                return;
            }

            // After successfully joining, or requesting to join the Group, update the Groups list.
            _ = UpdateGroups();
        }

        private async Task GroupLeave()
        {
            if (selectedGroup == null) return;

            try
            {
                // Attempt to leave the selected Group.
                var session = NakamaSingleton.Instance.Session;
                await NakamaSingleton.Instance.Client.LeaveGroupAsync(session, selectedGroup.Id);
                groupsList.ClearSelection();
            }
            catch (ApiResponseException e)
            {
                errorPopup.style.display = DisplayStyle.Flex;
                errorMessage.text = e.Message;
                return;
            }

            // After successfully leaving, update the Groups list.
            _ = UpdateGroups();
        }

        public async Task GroupAccept(string userId)
        {
            if (selectedGroup == null) return;

            try
            {
                // Attempt to accept the selected users join request.
                var session = NakamaSingleton.Instance.Session;
                await NakamaSingleton.Instance.Client.AddGroupUsersAsync(session, selectedGroup.Id, new[] { userId });
            }
            catch (ApiResponseException e)
            {
                errorPopup.style.display = DisplayStyle.Flex;
                errorMessage.text = e.Message;
                return;
            }

            // After successfully accepting the user, update the Groups list.
            _ = UpdateGroups();
        }

        public async Task GroupPromote(string userId)
        {
            if (selectedGroup == null) return;

            try
            {
                // Attempt to promote the selected user up to the next rank with more privileges.
                var session = NakamaSingleton.Instance.Session;
                await NakamaSingleton.Instance.Client.PromoteGroupUsersAsync(session, selectedGroup.Id,
                    new[] { userId });
            }
            catch (ApiResponseException e)
            {
                errorPopup.style.display = DisplayStyle.Flex;
                errorMessage.text = e.Message;
                return;
            }

            // After successfully promoting the user, update the Groups list.
            _ = UpdateGroups();
        }

        public async Task GroupDemote(string userId)
        {
            if (selectedGroup == null) return;

            try
            {
                // Attempt to demote the selected user down to the previous rank with fewer privileges.
                var session = NakamaSingleton.Instance.Session;
                await NakamaSingleton.Instance.Client.DemoteGroupUsersAsync(session, selectedGroup.Id,
                    new[] { userId });
            }
            catch (ApiResponseException e)
            {
                errorPopup.style.display = DisplayStyle.Flex;
                errorMessage.text = e.Message;
                return;
            }

            // After successfully demoting the user, update the Groups list.
            _ = UpdateGroups();
        }

        public async Task GroupKick(string userId)
        {
            if (selectedGroup == null) return;

            try
            {
                // Attempt to kick the selected user from the Group, removing them, but still allowing them to rejoin.
                // This is also used to decline a user's join request.
                var session = NakamaSingleton.Instance.Session;
                await NakamaSingleton.Instance.Client.KickGroupUsersAsync(session, selectedGroup.Id, new[] { userId });
            }
            catch (ApiResponseException e)
            {
                errorPopup.style.display = DisplayStyle.Flex;
                errorMessage.text = e.Message;
                return;
            }

            // After successfully kicking the user, update the Groups list.
            _ = UpdateGroups();
        }

        public async Task GroupBan(string userId)
        {
            if (selectedGroup == null) return;

            try
            {
                // Attempt to ban the selected user from the Group, removing them, and removing their ability to join again.
                var session = NakamaSingleton.Instance.Session;
                await NakamaSingleton.Instance.Client.BanGroupUsersAsync(session, selectedGroup.Id, new[] { userId });
            }
            catch (ApiResponseException e)
            {
                errorPopup.style.display = DisplayStyle.Flex;
                errorMessage.text = e.Message;
                return;
            }

            // After successfully banning the user, update the Groups list.
            _ = UpdateGroups();
        }

        #endregion
    }
}
