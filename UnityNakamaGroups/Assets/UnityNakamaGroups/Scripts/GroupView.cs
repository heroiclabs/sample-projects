using Nakama;
using UnityEngine;
using UnityEngine.UIElements;

namespace SampleProjects.Groups
{
    public class GroupView
    {
        private VisualElement avatarBackground;
        private VisualElement avatarIcon;
        private Label nameLabel;
        private Label countLabel;

        private NakamaGroupsController controller;

        public void SetVisualElement(NakamaGroupsController parent, VisualElement visualElement)
        {
            controller = parent;

            avatarBackground = visualElement.Q<VisualElement>("avatar-background");
            avatarIcon = visualElement.Q<VisualElement>("avatar-icon");
            nameLabel = visualElement.Q<Label>("name");
            countLabel = visualElement.Q<Label>("count");
        }

        public void SetGroup(IApiGroup group)
        {
            // Parse the AvatarUrl string into a JSON object containing the separate parts that make up a Group's avatar.
            var avatarData = JsonUtility.FromJson<AvatarData>(group.AvatarUrl);
            if (avatarData.IconIndex >= 0 && avatarData.IconIndex < controller.AvatarIcons.Length)
            {
                avatarIcon.style.backgroundImage = controller.AvatarIcons[avatarData.IconIndex];
            }
            if (avatarData.BackgroundIndex >= 0 && avatarData.BackgroundIndex < controller.AvatarBackgrounds.Length)
            {
                avatarBackground.style.backgroundImage = controller.AvatarBackgrounds[avatarData.BackgroundIndex];
            }
            nameLabel.text = group.Name;
            countLabel.text = $"{group.EdgeCount}/{group.MaxCount}";
        }
    }
}
