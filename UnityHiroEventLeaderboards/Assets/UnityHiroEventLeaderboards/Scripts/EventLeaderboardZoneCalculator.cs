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

using System.Collections.Generic;
using Hiro;

namespace HiroEventLeaderboards
{
    /// <summary>
    /// Helper class to calculate promotion and demotion zone boundaries for event leaderboards.
    /// </summary>
    public static class EventLeaderboardZoneCalculator
    {
        public class ZoneBoundaries
        {
            /// <summary>
            /// The last rank in the promotion zone (0 if no promotion zone).
            /// </summary>
            public long PromotionCutoff { get; set; }

            /// <summary>
            /// The first rank in the demotion zone (0 if no demotion zone).
            /// </summary>
            public long DemotionCutoff { get; set; }

            /// <summary>
            /// Whether change zones are being used (true) or reward tiers (false).
            /// </summary>
            public bool UsingChangeZones { get; set; }
        }

        /// <summary>
        /// Calculates the zone boundaries for a given event leaderboard.
        /// Priority: Change zones first, then reward tiers as fallback.
        /// </summary>
        public static ZoneBoundaries CalculateZones(IEventLeaderboard eventLeaderboard)
        {
            if (eventLeaderboard == null)
            {
                return new ZoneBoundaries();
            }

            var tierKey = eventLeaderboard.Tier.ToString();
            var totalPlayers = eventLeaderboard.Count;

            // Check for changeZones and that they are properly configured
            if (eventLeaderboard.ChangeZones.TryGetValue(tierKey, out var changeZone) &&
                (changeZone.Promotion > 0 || changeZone.Demotion > 0))
            {
                return CalculateFromChangeZones(changeZone, totalPlayers);
            }

            // Use rewardTiers if changeZones are not configured
            if ( eventLeaderboard.RewardTiers.TryGetValue(tierKey, out var rewardTiers))
            {
                return CalculateFromRewardTiers(rewardTiers);
            }

            return new ZoneBoundaries();
        }

        private static ZoneBoundaries CalculateFromChangeZones(IEventLeaderboardChangeZone changeZone, long totalPlayers)
        {
            var boundaries = new ZoneBoundaries
            {
                UsingChangeZones = true
            };

            if (totalPlayers == 0)
            {
                return boundaries;
            }

            // Calculate promotion cutoff (top X%)
            if (changeZone.Promotion > 0)
            {
                boundaries.PromotionCutoff = (long)(totalPlayers * changeZone.Promotion);
                // Ensure at least 1 player if a percentage is set
                if (boundaries.PromotionCutoff == 0 && changeZone.Promotion > 0)
                {
                    boundaries.PromotionCutoff = 1;
                }
            }

            // Calculate demotion cutoff (bottom Y%)
            if (changeZone.Demotion > 0)
            {
                var demotionCount = (long)(totalPlayers * changeZone.Demotion);
                // Ensure at least 1 player if a percentage is set
                if (demotionCount == 0 && changeZone.Demotion > 0)
                {
                    demotionCount = 1;
                }
                boundaries.DemotionCutoff = totalPlayers - demotionCount + 1;
            }

            return boundaries;
        }

        private static ZoneBoundaries CalculateFromRewardTiers(IEventLeaderboardRewardTiers rewardTiers)
        {
            var boundaries = new ZoneBoundaries
            {
                UsingChangeZones = false
            };

            if (rewardTiers.RewardTiers.Count == 0)
            {
                return boundaries;
            }

            long lastPromotionRank = 0;
            long firstDemotionRank = 0;

            foreach (var tier in rewardTiers.RewardTiers)
            {
                // Find the last rank with positive tier_change (promotion)
                if (tier.TierChange > 0 && tier.RankMax > lastPromotionRank)
                {
                    lastPromotionRank = tier.RankMax;
                }

                // Find the first rank with negative tier_change (demotion)
                if (tier.TierChange < 0)
                {
                    if (firstDemotionRank == 0 || tier.RankMin < firstDemotionRank)
                    {
                        firstDemotionRank = tier.RankMin;
                    }
                }
            }

            boundaries.PromotionCutoff = lastPromotionRank;
            boundaries.DemotionCutoff = firstDemotionRank;

            return boundaries;
        }

        /// <summary>
        /// Creates a display list with zone indicators inserted at the appropriate positions.
        /// Promotion zone indicator appears AFTER the last promoted player (all players above it are promoted).
        /// Demotion zone indicator appears BEFORE the first demoted player (all players below it are demoted).
        /// </summary>
        public static List<LeaderboardDisplayItem> CreateDisplayList(
            List<IEventLeaderboardScore> records,
            ZoneBoundaries boundaries)
        {
            var displayList = new List<LeaderboardDisplayItem>();

            if (records == null || records.Count == 0)
            {
                return displayList;
            }

            var promotionZoneAdded = false;
            var demotionZoneAdded = false;

            for (var i = 0; i < records.Count; i++)
            {
                var record = records[i];
                var nextRecord = i < records.Count - 1 ? records[i + 1] : null;

                // Add the player record first
                displayList.Add(LeaderboardDisplayItem.CreatePlayerRecord(record));

                // Add promotion zone indicator AFTER the last player in the promotion zone
                // This happens when:
                // 1. Current player is in a promotion zone (rank <= cutoff)
                // 2. Next player is outside a promotion zone (rank > cutoff) OR there is no next player
                if (!promotionZoneAdded && boundaries.PromotionCutoff > 0)
                {
                    if (record.Rank <= boundaries.PromotionCutoff &&
                        (nextRecord == null || nextRecord.Rank > boundaries.PromotionCutoff))
                    {
                        displayList.Add(LeaderboardDisplayItem.CreateZoneIndicator(EventLeaderboardZoneView.ZoneType.Promotion));
                        promotionZoneAdded = true;
                    }
                }

                // Add demotion zone indicator BEFORE the first player in the demotion zone
                // This happens when the next player enters the demotion zone
                if (demotionZoneAdded || boundaries.DemotionCutoff <= 0) continue;
                if (nextRecord == null || nextRecord.Rank < boundaries.DemotionCutoff) continue;
                displayList.Add(LeaderboardDisplayItem.CreateZoneIndicator(EventLeaderboardZoneView.ZoneType.Demotion));
                demotionZoneAdded = true;
            }

            return displayList;
        }
    }

    /// <summary>
    /// Wrapper class that represents an item in the leaderboard display.
    /// Can be either a player record or a zone indicator.
    /// </summary>
    public class LeaderboardDisplayItem
    {
        public enum ItemType
        {
            PlayerRecord,
            ZoneIndicator
        }

        public ItemType Type { get; }
        public IEventLeaderboardScore PlayerRecord { get; }
        public EventLeaderboardZoneView.ZoneType ZoneType { get; }

        private LeaderboardDisplayItem(ItemType type, IEventLeaderboardScore record = null, EventLeaderboardZoneView.ZoneType zoneType = EventLeaderboardZoneView.ZoneType.Promotion)
        {
            Type = type;
            PlayerRecord = record;
            ZoneType = zoneType;
        }

        public static LeaderboardDisplayItem CreatePlayerRecord(IEventLeaderboardScore record)
        {
            return new LeaderboardDisplayItem(ItemType.PlayerRecord, record);
        }

        public static LeaderboardDisplayItem CreateZoneIndicator(EventLeaderboardZoneView.ZoneType zoneType)
        {
            return new LeaderboardDisplayItem(ItemType.ZoneIndicator, null, zoneType);
        }
    }
}
