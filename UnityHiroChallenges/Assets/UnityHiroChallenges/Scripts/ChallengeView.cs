// Copyright 2025 The Nakama Authors
//
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//
// http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.

using System;
using Hiro;
using UnityEngine;
using UnityEngine.UIElements;

namespace HiroChallenges
{
    /// <summary>
    /// View component for displaying a single challenge in a list.
    /// </summary>
    public sealed class ChallengeView
    {
        private Label _nameLabel;
        private Label _categoryLabel;
        private Label _statusLabel;
        private Label _participantsLabel;
        private Label _endTimeLabel;

        public void SetVisualElement(VisualElement visualElement)
        {
            _nameLabel = visualElement.Q<Label>("name");
            _categoryLabel = visualElement.Q<Label>("category");
            _statusLabel = visualElement.Q<Label>("status");
            _participantsLabel = visualElement.Q<Label>("participants");
            _endTimeLabel = visualElement.Q<Label>("end-time");
        }

        public void SetChallenge(IChallenge challenge)
        {
            _nameLabel.text = challenge.Name;
            _categoryLabel.text = challenge.Category;

            // Convert status to readable string
            var now = DateTimeOffset.Now;
            var startTime = DateTimeOffset.FromUnixTimeSeconds(challenge.StartTimeSec);
            var difference = startTime - now;
            
            if (difference.TotalSeconds > 0)
            {
                _statusLabel.text = $"Starting in {difference.Days}d, {difference.Hours}h, {difference.Minutes}m";
                _statusLabel.style.color = new StyleColor(Color.yellow);
            }
            else
            {
                _statusLabel.text = challenge.IsActive ? "Active" : "Ended";
                _statusLabel.style.color = challenge.IsActive
                    ? new StyleColor(Color.green)
                    : new StyleColor(Color.red);
            }

            _participantsLabel.text = $"{challenge.Size}/{challenge.MaxSize}";

            // Format end time to display for local time
            var endTime = DateTimeOffset.FromUnixTimeSeconds(challenge.EndTimeSec).LocalDateTime;
            _endTimeLabel.text = endTime.ToString("MMM dd, HH:mm");
        }
    }
}
