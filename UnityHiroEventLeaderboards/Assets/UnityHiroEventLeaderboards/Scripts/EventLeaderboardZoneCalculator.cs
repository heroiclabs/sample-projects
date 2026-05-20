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
    /// Helper class to build a display list with promotion and demotion zone indicators
    /// inserted at the correct positions, using each score's TierDelta field.
    /// </summary>
    public static class EventLeaderboardZoneCalculator
    {
        /// <summary>
        /// Creates a display list with zone indicators inserted at the appropriate positions,
        /// derived from each score's TierDelta value as computed by the server.
        /// TierDelta > 0: moving up at least one tier = promotion
        /// TierDelta == 0: staying in current tier
        /// TierDelta < 0: moving down at least one tier = demotion
        /// </summary>
        public static List<LeaderboardDisplayItem> CreateDisplayList(List<IEventLeaderboardScore> records)
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

                // Add promotion zone indicator AFTER the last player with promotion.
                // This happens when:
                // 1. Current player has TierDelta > 0 (will be promoted).
                // 2. Next player has TierDelta <= 0, or there is no next player
                if (!promotionZoneAdded && record.TierDelta > 0 &&
                    (nextRecord == null || nextRecord.TierDelta <= 0))
                {
                    displayList.Add(LeaderboardDisplayItem.CreateZoneIndicator(EventLeaderboardZoneView.ZoneType.Promotion));
                    promotionZoneAdded = true;
                }

                // Add demotion zone indicator between the last neutral player and the first demoting player.
                // This happens when:
                // 1. Current player is not demoting (staying or promoting).
                // 2. Next player is demoting, i.e. TierDelta < 0
                if (!demotionZoneAdded && record.TierDelta >= 0 &&
                    nextRecord != null && nextRecord.TierDelta < 0)
                {
                    displayList.Add(LeaderboardDisplayItem.CreateZoneIndicator(EventLeaderboardZoneView.ZoneType.Demotion));
                    demotionZoneAdded = true;
                }
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
