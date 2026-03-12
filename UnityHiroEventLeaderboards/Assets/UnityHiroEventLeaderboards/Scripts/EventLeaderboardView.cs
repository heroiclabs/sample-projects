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
        private static readonly Color ActiveTint = new Color(0.078f, 0.827f, 0.761f, 1f);
        private static readonly Color JoinTint = new Color(0f, 0.675f, 0.957f, 1f);
        private static readonly Color WarningTint = new Color(1f, 0.8f, 0.3f, 1f);
        private static readonly Color EndedTint = new Color(0.882f, 0.196f, 0.2f, 1f);

        private Label _nameLabel;
        private Label _categoryLabel;
        private Label _statusLabel;
        private VisualElement _timeRemainingContainer;
        private Label _timeRemainingPrefixLabel;
        private Label _timeRemainingLabel;
        private VisualElement _iconElement;

        public void SetVisualElement(VisualElement visualElement)
        {
            _nameLabel = visualElement.Q<Label>("name");
            _categoryLabel = visualElement.Q<Label>("category");
            _statusLabel = visualElement.Q<Label>("status");
            _timeRemainingContainer = visualElement.Q<VisualElement>("time-remaining-container");
            _timeRemainingPrefixLabel = visualElement.Q<Label>("time-remaining-label");
            _timeRemainingLabel = visualElement.Q<Label>("time-remaining");
            _iconElement = visualElement.Q<VisualElement>("event-icon");
        }

        public void SetEventLeaderboard(IEventLeaderboard eventLeaderboard)
        {
            _nameLabel.text = eventLeaderboard.Name;
            _categoryLabel.text = eventLeaderboard.Category;

            SetIcon(eventLeaderboard);
            SetStatus(eventLeaderboard);
            SetTimeRemaining(eventLeaderboard);
        }

        private void SetIcon(IEventLeaderboard eventLeaderboard)
        {
            if (_iconElement == null) return;

            var iconName = "icon_trophy";
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

        private void SetStatus(IEventLeaderboard eventLeaderboard)
        {
            if (eventLeaderboard.CanClaim)
            {
                SetStatusLabel("Claim reward", WarningTint);
            }
            else if (eventLeaderboard.CanRoll)
            {
                SetStatusLabel("Can join", JoinTint);
            }
            else if (eventLeaderboard.IsActive)
            {
                SetStatusLabel("Active", ActiveTint);
            }
            else
            {
                SetStatusLabel("Ended", EndedTint);
            }
        }

        private void SetStatusLabel(string text, Color tintColor)
        {
            _statusLabel.text = text;
            _statusLabel.style.color = new StyleColor(Color.white);
            _statusLabel.style.unityBackgroundImageTintColor = new StyleColor(tintColor);
        }

        private void SetTimeRemaining(IEventLeaderboard eventLeaderboard)
        {
            
            if (eventLeaderboard.IsActive)
            {
                if (_timeRemainingPrefixLabel != null)
                    _timeRemainingPrefixLabel.text = "Time Left:";

                var timeRemaining = EventLeaderboardTimeUtility.GetTimeRemaining(eventLeaderboard);
                _timeRemainingLabel.text = EventLeaderboardTimeUtility.FormatTimeDuration(timeRemaining);
                _timeRemainingContainer?.Show();
                return;
            }

            var timeUntilNextStart = EventLeaderboardTimeUtility.GetTimeUntilNextStart(eventLeaderboard);
            if (timeUntilNextStart > TimeSpan.Zero)
            {
                if (_timeRemainingPrefixLabel != null)
                    _timeRemainingPrefixLabel.text = "Begins in:";

                _timeRemainingLabel.text = EventLeaderboardTimeUtility.FormatTimeDuration(timeUntilNextStart);
                _timeRemainingContainer?.Show();
            }
            else
            {
                _timeRemainingContainer?.Hide();
            }
        }
    }
}
