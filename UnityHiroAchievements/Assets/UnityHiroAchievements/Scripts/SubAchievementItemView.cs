using Hiro;
using UnityEngine.UIElements;

namespace HiroAchievements
{
    /// <summary>
    /// View class for individual sub-achievement items.
    /// Handles the display of sub-achievement data in the template.
    /// </summary>
    public class SubAchievementItemView
    {
        private VisualElement container;
        private Label nameLabel;
        private Label statusLabel;
        private VisualElement statusBadge;
        private VisualElement progressBar;
        private VisualElement progressFill;
        private Label progressText;

        public void SetVisualElement(VisualElement visualElement)
        {
            container = visualElement.Q<VisualElement>("sub-achievement-item-container");
            nameLabel = visualElement.Q<Label>("sub-achievement-name");
            statusLabel = visualElement.Q<Label>("sub-status-text");
            statusBadge = visualElement.Q<VisualElement>("sub-status-badge");
            progressBar = visualElement.Q<VisualElement>("sub-achievement-progress-bar");
            progressFill = visualElement.Q<VisualElement>("sub-achievement-progress-fill");
            progressText = visualElement.Q<Label>("sub-achievement-progress-text");
        }

        public VisualElement GetContainer()
        {
            return container;
        }

        public void SetSubAchievement(ISubAchievement subAchievement)
        {
            nameLabel.text = subAchievement.Name;

            // Calculate progress
            float progressPercent = subAchievement.MaxCount > 0 
                ? (float)subAchievement.Count / subAchievement.MaxCount * 100f 
                : 0f;

            // Set progress bar
            progressFill.style.width = Length.Percent(UnityEngine.Mathf.Clamp(progressPercent, 0f, 100f));

            // Set progress text using constant format
            progressText.text = string.Format(AchievementsUIConstants.SubAchievementProgressFormat, 
                subAchievement.Count, subAchievement.MaxCount, progressPercent);

            // Set status badge
            bool isCompleted = subAchievement.Count >= subAchievement.MaxCount;
            if (isCompleted && subAchievement.ClaimTimeSec > 0)
            {
                statusLabel.text = AchievementsUIConstants.StatusCheckmark;
                statusBadge.style.backgroundColor = AchievementsUIConstants.StatusCompleteColor;
            }
            else if (isCompleted && subAchievement.ClaimTimeSec <= 0)
            {
                statusLabel.text = AchievementsUIConstants.StatusToClaim;
                statusBadge.style.backgroundColor = AchievementsUIConstants.StatusToClaimColor;
            }
            else
            {
                statusLabel.text = AchievementsUIConstants.StatusInProgress;
                statusBadge.style.backgroundColor = AchievementsUIConstants.StatusInProgressColor;
            }
        }

        public void SetSelected(bool selected)
        {
            if (selected)
            {
                container.style.backgroundColor = AchievementsUIConstants.SelectionBackgroundColor;
                container.style.borderLeftColor = AchievementsUIConstants.SelectionBorderColor;
                container.style.borderRightColor = AchievementsUIConstants.SelectionBorderColor;
                container.style.borderTopColor = AchievementsUIConstants.SelectionBorderColor;
                container.style.borderBottomColor = AchievementsUIConstants.SelectionBorderColor;
            }
            else
            {
                container.style.backgroundColor = AchievementsUIConstants.DefaultBackgroundColor;
                container.style.borderLeftColor = AchievementsUIConstants.DefaultBorderColor;
                container.style.borderRightColor = AchievementsUIConstants.DefaultBorderColor;
                container.style.borderTopColor = AchievementsUIConstants.DefaultBorderColor;
                container.style.borderBottomColor = AchievementsUIConstants.DefaultBorderColor;
            }
        }

        public void SetHovered(bool hovered)
        {
            if (hovered && container.style.backgroundColor.value != AchievementsUIConstants.SelectionBackgroundColor)
            {
                container.style.backgroundColor = AchievementsUIConstants.HoverBackgroundColor;
            }
        }
    }
}