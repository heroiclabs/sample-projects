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
        private VisualElement _timeRemainingContainer;
        private Label _timeRemainingLabel;
        private VisualElement _iconElement;

        public void SetVisualElement(VisualElement visualElement)
        {
            _nameLabel = visualElement.Q<Label>("name");
            _categoryLabel = visualElement.Q<Label>("category");
            _statusLabel = visualElement.Q<Label>("status");
            _timeRemainingContainer = visualElement.Q<VisualElement>("time-remaining-container");
            _timeRemainingLabel = visualElement.Q<Label>("time-remaining");
            _iconElement = visualElement.Q<VisualElement>("event-icon");
        }

        public void SetEventLeaderboard(IEventLeaderboard eventLeaderboard)
        {
            _nameLabel.text = eventLeaderboard.Name;
            _categoryLabel.text = eventLeaderboard.Category;

            // Set icon dynamically from Resources based on config
            if (_iconElement != null)
            {
                string iconName = "icon_trophy"; // default fallback
                if (eventLeaderboard.AdditionalProperties.TryGetValue("icon", out var iconValue))
                {
                    iconName = iconValue;
                }

                var iconTexture = Resources.Load<Texture2D>($"EventIcons/{iconName}");
                if (iconTexture != null)
                {
                    _iconElement.style.backgroundImage = new StyleBackground(iconTexture);
                }
            }

            // Convert status to readable string based on timing
            var currentTime = EventLeaderboardTimeUtility.GetCurrentTime(eventLeaderboard);
            var startTime = EventLeaderboardTimeUtility.GetStartTime(eventLeaderboard);

            if (eventLeaderboard.IsActive)
            {
                if (EventLeaderboardTimeUtility.HasStarted(eventLeaderboard))
                {
                    _statusLabel.text = "Active";
                    _statusLabel.style.color = new StyleColor(Color.white);
                    // Use #14D3C2 (teal/green) background for active
                    _statusLabel.style.unityBackgroundImageTintColor = new StyleColor(new Color(0.078f, 0.827f, 0.761f, 1f));
                }
                else
                {
                    var difference = startTime - currentTime;
                    _statusLabel.text = $"Starts in {EventLeaderboardTimeUtility.FormatTimeDuration(difference)}";
                    _statusLabel.style.color = new StyleColor(Color.white);
                    // Use yellow/orange tint for upcoming
                    _statusLabel.style.unityBackgroundImageTintColor = new StyleColor(new Color(1f, 0.8f, 0.3f, 1f));
                }
            }
            else
            {
                _statusLabel.text = "Ended";
                _statusLabel.style.color = new StyleColor(Color.white);
                // Use #E13233 (red) tint for ended
                _statusLabel.style.unityBackgroundImageTintColor = new StyleColor(new Color(0.882f, 0.196f, 0.2f, 1f));
            }

            // Display time remaining with conditional formatting, hide when ended
            if (eventLeaderboard.IsActive)
            {
                var timeRemaining = EventLeaderboardTimeUtility.GetTimeRemaining(eventLeaderboard);
                _timeRemainingLabel.text = EventLeaderboardTimeUtility.FormatTimeDuration(timeRemaining);
                if (_timeRemainingContainer != null)
                {
                    _timeRemainingContainer.style.display = DisplayStyle.Flex;
                }
            }
            else
            {
                // Hide the time remaining container when event has ended
                if (_timeRemainingContainer != null)
                {
                    _timeRemainingContainer.style.display = DisplayStyle.None;
                }
            }
        }
    }
}
