using Hiro;
using UnityEngine;
using UnityEngine.UIElements;

namespace XPGuide
{
    public class LevelMilestoneItemView
    {
        private VisualElement _container;
        private Label _nameLabel;
        private Label _statusLabel;
        private Label _rewardsLabel;
        private VisualElement _statusBadge;
        private VisualElement _progressFill;
        private Label _progressText;

        public void SetVisualElement(VisualElement visualElement)
        {
            _container = visualElement.Q<VisualElement>("sub-achievement-item-container");
            _nameLabel = visualElement.Q<Label>("sub-achievement-name");
            _statusLabel = visualElement.Q<Label>("sub-status-text");
            _statusBadge = visualElement.Q<VisualElement>("sub-status-badge");
            _rewardsLabel = visualElement.Q<Label>("sub-achievement-reward-text");
            _progressFill = visualElement.Q<VisualElement>("sub-achievement-progress-fill");
            _progressText = visualElement.Q<Label>("sub-achievement-progress-text");
        }

        public VisualElement GetContainer() => _container;

        public void SetSubAchievement(ISubAchievement sub)
        {
            _nameLabel.text = sub.Name;

            float pct = XPProgressHelper.CalculateSubAchievementPercent(sub);
            _progressFill.style.width = Length.Percent(Mathf.Clamp(pct, 0f, 100f));
            _progressText.text = string.Format(XPUIConstants.SubProgressFormat, sub.Count, sub.MaxCount, pct);

            if (sub.AvailableRewards?.Guaranteed?.Currencies != null &&
                sub.AvailableRewards.Guaranteed.Currencies.TryGetValue("gems", out var reward))
            {
                _rewardsLabel.text = $"{reward.Count.Min} Gems";
            }
            else
            {
                _rewardsLabel.text = sub.ClaimTimeSec > 0 ? "Claimed" : "No reward";
            }

            bool achieved = XPProgressHelper.IsLevelAchieved(sub);
            bool inProgress = XPProgressHelper.IsLevelInProgress(sub);

            if (achieved)
            {
                _statusLabel.text = XPUIConstants.StatusAchieved;
                _statusBadge.style.backgroundColor = XPUIConstants.StatusAchievedColor;
            }
            else if (inProgress)
            {
                _statusLabel.text = XPUIConstants.StatusInProgress;
                _statusBadge.style.backgroundColor = XPUIConstants.StatusInProgressColor;
            }
            else
            {
                _statusLabel.text = XPUIConstants.StatusLocked;
                _statusBadge.style.backgroundColor = XPUIConstants.StatusLockedColor;
            }
        }

        public void SetSelected(bool selected)
        {
            _container.style.backgroundColor = selected
                ? new Color(0.85f, 0.9f, 1f, 1f)
                : new Color(0.95f, 0.95f, 0.95f, 1f);
        }

        public void SetHovered(bool hovered)
        {
            if (hovered)
                _container.style.backgroundColor = new Color(0.9f, 0.9f, 1f, 1f);
        }
    }
}
