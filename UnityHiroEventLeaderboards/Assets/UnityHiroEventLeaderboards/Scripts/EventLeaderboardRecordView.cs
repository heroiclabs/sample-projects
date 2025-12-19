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

using Hiro;
using UnityEngine;
using UnityEngine.UIElements;

namespace HiroEventLeaderboards
{
    public sealed class EventLeaderboardRecordView
    {
        private VisualElement _recordContainer;
        private Label _usernameLabel;
        private Label _scoreLabel;
        private Label _subScoreLabel;
        private Label _rankLabel;
        private VisualElement _medalGold;
        private VisualElement _medalSilver;
        private VisualElement _medalBronze;

        public void SetVisualElement(VisualElement visualElement)
        {
            _recordContainer = visualElement.Q<VisualElement>("record-container");
            _usernameLabel = visualElement.Q<Label>("username");
            _scoreLabel = visualElement.Q<Label>("score");
            _subScoreLabel = visualElement.Q<Label>("sub-score");
            _rankLabel = visualElement.Q<Label>("rank");
            _medalGold = visualElement.Q<VisualElement>("medal-gold");
            _medalSilver = visualElement.Q<VisualElement>("medal-silver");
            _medalBronze = visualElement.Q<VisualElement>("medal-bronze");
        }

        public void SetEventLeaderboardRecord(IEventLeaderboardScore record)
        {
            SetEventLeaderboardRecord(record, null, null);
        }

        public void SetEventLeaderboardRecord(IEventLeaderboardScore record, IEventLeaderboard eventLeaderboard, string currentUsername)
        {
            _usernameLabel.text = FormatUsername(record.Username);
            _scoreLabel.text = record.Score.ToString();
            _subScoreLabel.text = record.Subscore.ToString();

            // A rank of 0 would mean that you are yet to submit your first score.
            _rankLabel.text = record.Rank > 0 ? $"#{record.Rank}" : "-";

            // Highlight current player's record
            if (!string.IsNullOrEmpty(currentUsername) && record.Username == currentUsername)
            {
                _recordContainer.style.unityBackgroundImageTintColor = new StyleColor(new Color(0.9f, 0.95f, 1f)); // Light blue tint
                _usernameLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
            }
            else
            {
                _recordContainer.style.unityBackgroundImageTintColor = new StyleColor(new Color(0.933f, 0.933f, 0.933f)); // Default gray
                _usernameLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
            }

            // Show medal icon based on reward tiers
            if (eventLeaderboard != null && record.Rank > 0)
            {
                var medalType = GetMedalTypeForRank(record.Rank, eventLeaderboard);
                SetMedalIcon(medalType);
            }
            else
            {
                SetMedalIcon(MedalType.None);
            }
        }

        /// Formats debug usernames (UUIDs) to be more readable.
        /// Converts long UUID strings to "DebugPlayerX" format.
        private static string FormatUsername(string username)
        {
            if (string.IsNullOrEmpty(username))
            {
                return username;
            }

            // Check if username looks like a UUID (contains hyphens and is long enough)
            // UUIDs are in format: xxxxxxxx-xxxx-xxxx-xxxx-xxxxxxxxxxxx (36 chars)
            if (username.Length >= 36 && username.IndexOf('-') > 0)
            {
                // Extract first 3 characters for the ID
                var shortId = username.Substring(0, 3);
                return $"DebugPlayer{shortId}";
            }

            return username;
        }

        private enum MedalType
        {
            None,
            Gold,
            Silver,
            Bronze
        }

        /// <summary>
        /// Determines which medal type a player gets based on their rank and the event's reward tiers.
        /// Assumes 3 reward tiers per tier level with consistent rank ranges.
        /// </summary>
        private static MedalType GetMedalTypeForRank(long rank, IEventLeaderboard eventLeaderboard)
        {
            // Get reward tiers for the current tier
            var tierKey = eventLeaderboard.Tier.ToString();
            if (eventLeaderboard.RewardTiers == null || !eventLeaderboard.RewardTiers.TryGetValue(tierKey, out var rewardTiers))
            {
                return MedalType.None;
            }

            var tiers = rewardTiers.RewardTiers;
            if (tiers.Count < 3)
            {
                return MedalType.None;
            }

            // Sort tiers by RankMin manually
            var sortedTiers = new System.Collections.Generic.List<IEventLeaderboardRewardTier>(tiers);
            sortedTiers.Sort((a, b) => a.RankMin.CompareTo(b.RankMin));

            // Top tier (1-5) = Gold
            if (rank >= sortedTiers[0].RankMin && rank <= sortedTiers[0].RankMax)
            {
                return MedalType.Gold;
            }

            // Second tier (6-15) = Silver
            if (rank >= sortedTiers[1].RankMin && rank <= sortedTiers[1].RankMax)
            {
                return MedalType.Silver;
            }

            // Third tier (16-50/100) = Bronze
            if (rank >= sortedTiers[2].RankMin && rank <= sortedTiers[2].RankMax)
            {
                return MedalType.Bronze;
            }

            return MedalType.None;
        }

        private void SetMedalIcon(MedalType medalType)
        {
            // Hide all medals first
            if (_medalGold != null) _medalGold.style.display = DisplayStyle.None;
            if (_medalSilver != null) _medalSilver.style.display = DisplayStyle.None;
            if (_medalBronze != null) _medalBronze.style.display = DisplayStyle.None;

            // Show the appropriate medal
            switch (medalType)
            {
                case MedalType.Gold:
                    if (_medalGold != null) _medalGold.style.display = DisplayStyle.Flex;
                    break;
                case MedalType.Silver:
                    if (_medalSilver != null) _medalSilver.style.display = DisplayStyle.Flex;
                    break;
                case MedalType.Bronze:
                    if (_medalBronze != null) _medalBronze.style.display = DisplayStyle.Flex;
                    break;
            }
        }
    }
}
