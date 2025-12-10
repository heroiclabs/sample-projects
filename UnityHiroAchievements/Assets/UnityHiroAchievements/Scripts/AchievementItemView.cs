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
                if (achievement.SubAchievements != null && achievement.SubAchievements.Count > 0)
                {
                    int completedCount = 0;
                    foreach (var subAchievement in achievement.SubAchievements)
                    {
                        if (subAchievement.Value.Count >= subAchievement.Value.MaxCount)
                        {
                            completedCount++;
                        }
                    }
                    subAchievementsLabel.text = $"{completedCount}/{achievement.SubAchievements.Count} Objectives";
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
                statusLabel.text = achievement.ClaimTimeSec > 0 ? "Claimed" : "Complete";
                statusBadge.style.backgroundColor = achievement.ClaimTimeSec > 0 
                    ? new UnityEngine.Color(0.6f, 0.6f, 0.6f, 1f) 
                    : new UnityEngine.Color(0.4f, 0.8f, 0.4f, 1f);
            }
            else if (isLocked)
            {
                statusLabel.text = "Locked";
                statusBadge.style.backgroundColor = new UnityEngine.Color(0.5f, 0.5f, 0.5f, 1f);
            }
            else
            {
                statusLabel.text = "In Progress";
                statusBadge.style.backgroundColor = new UnityEngine.Color(0.5f, 0.6f, 1f, 1f);
            }

            // Set progress
            float progressPercent = achievement.MaxCount > 0 
                ? (float)achievement.Count / achievement.MaxCount * 100f 
                : 0f;
            progressFill.style.width = Length.Percent(UnityEngine.Mathf.Clamp(progressPercent, 0f, 100f));
        }
    }
}