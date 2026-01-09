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

namespace HiroEventLeaderboards
{
    /// <summary>
    /// Utility class for time-related calculations across event leaderboards.
    /// </summary>
    public static class EventLeaderboardTimeUtility
    {
        public static TimeSpan GetTimeRemaining(IEventLeaderboard eventLeaderboard)
        {
            // Return zero if event is not active
            if (!eventLeaderboard.IsActive)
            {
                return TimeSpan.Zero;
            }

            var nowUnix = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            var endUnix = eventLeaderboard.EndTimeSec;

            var secondsRemaining = System.Math.Max(0, endUnix - nowUnix);

            return TimeSpan.FromSeconds(secondsRemaining);
        }

        /// <summary>
        /// Formats a time duration conditionally showing only relevant units.
        /// Examples: "3h 25m", "2d 5h", "45m"
        /// </summary>
        public static string FormatTimeDuration(TimeSpan duration)
        {
            if (duration.TotalMinutes < 1)
            {
                return "< 1m";
            }

            if (duration.Days > 0)
            {
                return $"{duration.Days}d {duration.Hours}h";
            }

            if (duration.Hours > 0)
            {
                return $"{duration.Hours}h {duration.Minutes}m";
            }

            return $"{duration.Minutes}m";
        }
        
        public static DateTimeOffset GetStartTime(IEventLeaderboard eventLeaderboard)
        {
            return DateTimeOffset.FromUnixTimeSeconds(eventLeaderboard.StartTimeSec);
        }

        public static DateTimeOffset GetEndTime(IEventLeaderboard eventLeaderboard)
        {
            return DateTimeOffset.FromUnixTimeSeconds(eventLeaderboard.EndTimeSec);
        }

        public static bool HasStarted(IEventLeaderboard eventLeaderboard)
        {
            var nowUnix = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            var startUnix = eventLeaderboard.StartTimeSec;
            return nowUnix >= startUnix;
        }

        /// <summary>
        /// Calculates the time until the next start of an event leaderboard.
        /// </summary>
        public static TimeSpan GetTimeUntilNextStart(IEventLeaderboard eventLeaderboard)
        {
            // ExpiryTimeSec is 0 when there is no next iteration (series has ended)
            if (eventLeaderboard.ExpiryTimeSec == 0)
            {
                return TimeSpan.Zero;
            }

            var nowUnix = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            var expiryUnix = eventLeaderboard.ExpiryTimeSec;

            var secondsUntilNext = System.Math.Max(0, expiryUnix - nowUnix);

            return TimeSpan.FromSeconds(secondsUntilNext);
        }
    }
}
