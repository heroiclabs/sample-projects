using Hiro;
using UnityEngine.UIElements;

namespace HiroAchievements
{
    /// <summary>
    /// View class for individual achievement items in the list.
    /// Handles the display of achievement data in the list template.
    /// </summary>
    public class AchievementItemView
    {
        private VisualElement iconContainer;
        private Label nameLabel;
        private Label subAchievementsLabel;
        private Label statusLabel;
        private VisualElement statusBadge;
        private VisualElement progressBar;
        private VisualElement progressFill;

        public void SetVisualElement(VisualElement visualElement)
        {
            iconContainer = visualElement.Q<VisualElement>("achievement-icon");
            nameLabel = visualElement.Q<Label>("achievement-name");
            subAchievementsLabel = visualElement.Q<Label>("sub-achievements-text");
            statusLabel = visualElement.Q<Label>("status-text");
            statusBadge = visualElement.Q<VisualElement>("status-badge");
            progressBar = visualElement.Q<VisualElement>("achievement-progress-bar");
            progressFill = visualElement.Q<VisualElement>("achievement-progress-fill");
        }

        public void SetAchievement(IAchievement achievement, bool isCompleted, bool isLocked)
        {
            nameLabel.text = achievement.Name;

            // Show sub-achievements count if available
            if (subAchievementsLabel != null)
            {
                if (AchievementProgressHelper.HasSubAchievements(achievement))
                {
                    int completedCount = AchievementProgressHelper.CountCompletedSubAchievements(achievement);
                    subAchievementsLabel.text = string.Format(AchievementsUIConstants.ObjectivesFormat, 
                        completedCount, achievement.SubAchievements.Count);
                    subAchievementsLabel.style.display = DisplayStyle.Flex;
                }
                else
                {
                    subAchievementsLabel.style.display = DisplayStyle.None;
                }
            }

            // Set status
            if (isCompleted)
            {
                statusLabel.text = achievement.ClaimTimeSec > 0 
                    ? AchievementsUIConstants.StatusClaimed 
                    : AchievementsUIConstants.StatusComplete;
                statusBadge.style.backgroundColor = achievement.ClaimTimeSec > 0 
                    ? AchievementsUIConstants.StatusClaimedColor 
                    : AchievementsUIConstants.StatusCompleteColor;
            }
            else if (isLocked)
            {
                statusLabel.text = AchievementsUIConstants.StatusLocked;
                statusBadge.style.backgroundColor = AchievementsUIConstants.StatusLockedColor;
            }
            else
            {
                statusLabel.text = AchievementsUIConstants.StatusInProgress;
                statusBadge.style.backgroundColor = AchievementsUIConstants.StatusInProgressColor;
            }

            // Set progress using helper
            float progressPercent = AchievementProgressHelper.CalculateProgressPercent(achievement);
            progressFill.style.width = Length.Percent(UnityEngine.Mathf.Clamp(progressPercent, 0f, 100f));
        }
    }
}