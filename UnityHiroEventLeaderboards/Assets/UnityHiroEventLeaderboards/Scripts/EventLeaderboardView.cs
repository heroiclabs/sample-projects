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

namespace HiroEventLeaderboards
{
    public sealed class EventLeaderboardView
    {
        private Label _nameLabel;
        private Label _categoryLabel;
        private Label _statusLabel;
        private Label _tierLabel;
        private Label _endTimeLabel;

        public void SetVisualElement(VisualElement visualElement)
        {
            _nameLabel = visualElement.Q<Label>("name");
            _categoryLabel = visualElement.Q<Label>("category");
            _statusLabel = visualElement.Q<Label>("status");
            _tierLabel = visualElement.Q<Label>("tier");
            _endTimeLabel = visualElement.Q<Label>("end-time");
        }

        public void SetEventLeaderboard(IEventLeaderboard eventLeaderboard)
        {
            _nameLabel.text = eventLeaderboard.Name;
            _categoryLabel.text = eventLeaderboard.Category;

            // Display the user's current tier
            _tierLabel.text = $"Tier {eventLeaderboard.Tier}";

            // Convert status to readable string based on timing
            var currentTime = DateTimeOffset.FromUnixTimeSeconds(eventLeaderboard.CurrentTimeSec);
            var startTime = DateTimeOffset.FromUnixTimeSeconds(eventLeaderboard.StartTimeSec);
            var endTime = DateTimeOffset.FromUnixTimeSeconds(eventLeaderboard.EndTimeSec);

            if (eventLeaderboard.IsActive)
            {
                if (currentTime >= startTime)
                {
                    _statusLabel.text = "Active";
                    _statusLabel.style.color = new StyleColor(Color.green);
                }
                else
                {
                    var difference = startTime - currentTime;
                    _statusLabel.text = $"Starts in {difference.Days}d, {difference.Hours}h, {difference.Minutes}m";
                    _statusLabel.style.color = new StyleColor(Color.yellow);
                }
            }
            else
            {
                _statusLabel.text = "Ended";
                _statusLabel.style.color = new StyleColor(Color.red);
            }

            // Format end time to display for local time.
            _endTimeLabel.text = endTime.LocalDateTime.ToString("MMM dd, HH:mm");
        }
    }
}
