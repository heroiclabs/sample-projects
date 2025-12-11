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

            // Set progress text
            progressText.text = $"{subAchievement.Count} / {subAchievement.MaxCount} ({progressPercent:F0}%)";

            // Set status badge
            bool isCompleted = subAchievement.Count >= subAchievement.MaxCount;
            if (isCompleted)
            {
                statusLabel.text = "✓";
                statusBadge.style.backgroundColor = new UnityEngine.Color(0.4f, 0.8f, 0.4f, 1f);
            }
            else
            {
                statusLabel.text = "Progress";
                statusBadge.style.backgroundColor = new UnityEngine.Color(0.5f, 0.6f, 1f, 1f);
            }
        }

        public void SetSelected(bool selected)
        {
            if (selected)
            {
                container.style.backgroundColor = new UnityEngine.Color(0.85f, 0.9f, 1f, 1f);
                container.style.borderLeftColor = new UnityEngine.Color(0.5f, 0.6f, 1f, 1f);
                container.style.borderRightColor = new UnityEngine.Color(0.5f, 0.6f, 1f, 1f);
                container.style.borderTopColor = new UnityEngine.Color(0.5f, 0.6f, 1f, 1f);
                container.style.borderBottomColor = new UnityEngine.Color(0.5f, 0.6f, 1f, 1f);
            }
            else
            {
                container.style.backgroundColor = new UnityEngine.Color(0.95f, 0.95f, 0.95f, 1f);
                container.style.borderLeftColor = new UnityEngine.Color(0.7f, 0.7f, 0.7f, 1f);
                container.style.borderRightColor = new UnityEngine.Color(0.7f, 0.7f, 0.7f, 1f);
                container.style.borderTopColor = new UnityEngine.Color(0.7f, 0.7f, 0.7f, 1f);
                container.style.borderBottomColor = new UnityEngine.Color(0.7f, 0.7f, 0.7f, 1f);
            }
        }

        public void SetHovered(bool hovered)
        {
            if (hovered && container.style.backgroundColor.value != new UnityEngine.Color(0.85f, 0.9f, 1f, 1f))
            {
                container.style.backgroundColor = new UnityEngine.Color(0.9f, 0.9f, 1f, 1f);
            }
        }
    }
}