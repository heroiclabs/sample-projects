using System;
using Hiro;
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
            
            // Convert status enum to readable string
            statusLabel.text = challenge.IsActive ? "Active" : "Ended";
            
            participantsLabel.text = $"{challenge.Size}/{challenge.MaxSize}";
            
            // Format end time (assuming UnixTime conversion)
            var endTime = DateTimeOffset.FromUnixTimeSeconds(challenge.EndTimeSec).DateTime;
            endTimeLabel.text = endTime.ToString("MMM dd, HH:mm");
        }
    }
}