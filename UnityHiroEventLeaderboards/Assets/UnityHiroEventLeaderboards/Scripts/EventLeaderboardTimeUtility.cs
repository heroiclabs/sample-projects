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
    /// Utility class for consistent time-related calculations across event leaderboards.
    /// </summary>
    public static class EventLeaderboardTimeUtility
    {
        /// <summary>
        /// Calculates the time remaining for an event leaderboard.
        /// </summary>
        /// <param name="eventLeaderboard">The event leaderboard</param>
        /// <returns>TimeSpan representing time remaining, or TimeSpan.Zero if ended</returns>
        public static TimeSpan GetTimeRemaining(IEventLeaderboard eventLeaderboard)
        {
            var currentTime = DateTimeOffset.FromUnixTimeSeconds(eventLeaderboard.CurrentTimeSec);
            var endTime = DateTimeOffset.FromUnixTimeSeconds(eventLeaderboard.EndTimeSec);
            var timeRemaining = endTime - currentTime;

            // Return zero if event has ended or is not active
            if (!eventLeaderboard.IsActive || timeRemaining.TotalSeconds <= 0)
            {
                return TimeSpan.Zero;
            }

            return timeRemaining;
        }

        /// <summary>
        /// Formats a time duration conditionally showing only relevant units.
        /// Examples: "3h 25m", "2d 5h", "45m"
        /// </summary>
        /// <param name="duration">The duration to format</param>
        /// <returns>Formatted string representation of the duration</returns>
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

        /// <summary>
        /// Gets the current time for an event leaderboard.
        /// </summary>
        public static DateTimeOffset GetCurrentTime(IEventLeaderboard eventLeaderboard)
        {
            return DateTimeOffset.FromUnixTimeSeconds(eventLeaderboard.CurrentTimeSec);
        }

        /// <summary>
        /// Gets the start time for an event leaderboard.
        /// </summary>
        public static DateTimeOffset GetStartTime(IEventLeaderboard eventLeaderboard)
        {
            return DateTimeOffset.FromUnixTimeSeconds(eventLeaderboard.StartTimeSec);
        }

        /// <summary>
        /// Gets the end time for an event leaderboard.
        /// </summary>
        public static DateTimeOffset GetEndTime(IEventLeaderboard eventLeaderboard)
        {
            return DateTimeOffset.FromUnixTimeSeconds(eventLeaderboard.EndTimeSec);
        }

        /// <summary>
        /// Checks if an event has started.
        /// </summary>
        public static bool HasStarted(IEventLeaderboard eventLeaderboard)
        {
            var currentTime = GetCurrentTime(eventLeaderboard);
            var startTime = GetStartTime(eventLeaderboard);
            return currentTime >= startTime;
        }
    }
}
