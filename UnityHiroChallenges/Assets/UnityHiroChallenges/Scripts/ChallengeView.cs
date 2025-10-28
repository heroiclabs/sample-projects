using System;
using Hiro;
using UnityEngine;
using UnityEngine.UIElements;

namespace HiroChallenges
{
    public class ChallengeView
    {
        private Label nameLabel;
        private Label categoryLabel;
        private Label statusLabel;
        private Label participantsLabel;
        private Label endTimeLabel;

        public void SetVisualElement(VisualElement visualElement)
        {
            nameLabel = visualElement.Q<Label>("name");
            categoryLabel = visualElement.Q<Label>("category");
            statusLabel = visualElement.Q<Label>("status");
            participantsLabel = visualElement.Q<Label>("participants");
            endTimeLabel = visualElement.Q<Label>("end-time");
        }

        public void SetChallenge(IChallenge challenge)
        {
            nameLabel.text = challenge.Name;
            categoryLabel.text = challenge.Category;

            // Convert status to readable string.
            var now = DateTimeOffset.Now;
            var startTime = DateTimeOffset.FromUnixTimeSeconds(challenge.StartTimeSec);
            var difference = startTime - now;
            if (difference.Seconds > 0)
            {
                statusLabel.text = $"Starting in {difference.Days}d, {difference.Hours}h, {difference.Minutes}m";
                statusLabel.style.color = new StyleColor(Color.orange);
            }
            else
            {
                statusLabel.text = challenge.IsActive ? "Active" : "Ended";
                statusLabel.style.color = challenge.IsActive ? new StyleColor(Color.green) : new StyleColor(Color.red);
            }

            participantsLabel.text = $"{challenge.Size}/{challenge.MaxSize}";

            // Format end time to display for local time.
            var endTime = DateTimeOffset.FromUnixTimeSeconds(challenge.EndTimeSec).LocalDateTime;
            endTimeLabel.text = endTime.ToString("MMM dd, HH:mm");
        }
    }
}