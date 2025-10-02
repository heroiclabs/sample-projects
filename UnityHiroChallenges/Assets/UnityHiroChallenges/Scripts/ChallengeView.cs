using System;
using Hiro;
using Nakama;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UIElements;

namespace SampleProjects.Challenges
{
    public class ChallengeView
    {
        private Label nameLabel;
        private Label categoryLabel;
        private Label statusLabel;
        private Label participantsLabel;
        private Label endTimeLabel;

        private HiroChallengesController controller;

        public void SetVisualElement(HiroChallengesController parent, VisualElement visualElement)
        {
            controller = parent;

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
            statusLabel.text = GetStatusString(challenge.IsActive);
            
            participantsLabel.text = $"{challenge.Size}/{challenge.MaxSize}";
            
            // Format end time (assuming UnixTime conversion)
            var endTime = UnixTimeToDateTime(challenge.EndTimeSec);
            endTimeLabel.text = endTime.ToString("MMM dd, HH:mm");
        }

        private string GetStatusString(bool isActive)
        {
                return isActive ? "Active" : "Ended";
        }

        private System.DateTime UnixTimeToDateTime(long unixTime)
        {
            return System.DateTimeOffset.FromUnixTimeSeconds(unixTime).DateTime;
        }
    }
}