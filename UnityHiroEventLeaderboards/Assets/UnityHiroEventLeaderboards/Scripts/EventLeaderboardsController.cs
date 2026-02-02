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
using System.Collections.Generic;
using System.Threading.Tasks;
using Hiro;
using Nakama;

namespace HiroEventLeaderboards
{
    /// <summary>
    /// Controller/Presenter for the Event Leaderboards system.
    /// Handles business logic and coordinates with Hiro systems.
    /// Plain C# class for testability - no MonoBehaviour inheritance.
    /// </summary>
    public class EventLeaderboardsController
    {
        private readonly NakamaSystem _nakamaSystem;
        private readonly IEventLeaderboardsSystem _eventLeaderboardsSystem;
        private readonly IEconomySystem _economySystem;

        private IEventLeaderboard _selectedEventLeaderboard;
        private string _selectedLeaderboardId = string.Empty;

        private readonly List<IEventLeaderboard> _eventLeaderboards = new();
        private readonly List<IEventLeaderboardScore> _selectedEventLeaderboardRecords = new();
        private readonly List<LeaderboardDisplayItem> _displayItems = new();

        public string CurrentUserId => _nakamaSystem.UserId;
        public string CurrentUsername => _nakamaSystem.Session?.Username;
        public IReadOnlyList<IEventLeaderboard> EventLeaderboards => _eventLeaderboards;
        public IReadOnlyList<IEventLeaderboardScore> SelectedEventLeaderboardRecords => _selectedEventLeaderboardRecords;
        public IReadOnlyList<LeaderboardDisplayItem> DisplayItems => _displayItems;

        public EventLeaderboardsController(
            NakamaSystem nakamaSystem,
            IEventLeaderboardsSystem eventLeaderboardsSystem,
            IEconomySystem economySystem)
        {
            _nakamaSystem = nakamaSystem ?? throw new ArgumentNullException(nameof(nakamaSystem));
            _eventLeaderboardsSystem = eventLeaderboardsSystem ?? throw new ArgumentNullException(nameof(eventLeaderboardsSystem));
            _economySystem = economySystem ?? throw new ArgumentNullException(nameof(economySystem));
        }

        #region Event Leaderboard List Operations

        public async Task<(int index, List<IEventLeaderboardScore> records)?> RefreshEventLeaderboardsAsync()
        {
            _eventLeaderboards.Clear();

            var eventLeaderboards = await _eventLeaderboardsSystem.ListEventLeaderboardsAsync(null, true);

            // Sort by time remaining (soonest ending first), then by ID alphabetically
            var sortedLeaderboards = new List<IEventLeaderboard>(eventLeaderboards.Leaderboards);
            sortedLeaderboards.Sort((a, b) =>
            {
                var timeA = EventLeaderboardTimeUtility.GetTimeRemaining(a);
                var timeB = EventLeaderboardTimeUtility.GetTimeRemaining(b);

                // If event has ended (timeRemaining is zero), sort to bottom
                var secondsA = timeA == TimeSpan.Zero ? long.MaxValue : (long)timeA.TotalSeconds;
                var secondsB = timeB == TimeSpan.Zero ? long.MaxValue : (long)timeB.TotalSeconds;

                var timeComparison = secondsA.CompareTo(secondsB);
                if (timeComparison != 0)
                    return timeComparison;

                return string.Compare(a.Id, b.Id, StringComparison.Ordinal);
            });

            _eventLeaderboards.AddRange(sortedLeaderboards);

            // If we have an event leaderboard selected, try to reselect it
            for (var i = 0; i < _eventLeaderboards.Count; i++)
            {
                var eventLeaderboard = _eventLeaderboards[i];
                if (eventLeaderboard.Id != _selectedLeaderboardId)
                    continue;

                var records = await SelectEventLeaderboardAsync(eventLeaderboard);
                return (i, records);
            }

            return null;
        }

        public async Task<List<IEventLeaderboardScore>> SelectEventLeaderboardAsync(IEventLeaderboard eventLeaderboard)
        {
            if (eventLeaderboard == null)
            {
                _selectedLeaderboardId = string.Empty;
                _selectedEventLeaderboardRecords.Clear();
                _displayItems.Clear();
                return null;
            }

            _selectedEventLeaderboard = eventLeaderboard;
            _selectedLeaderboardId = _selectedEventLeaderboard.Id;

            // Get detailed event leaderboard info with scores
            var detailedEventLeaderboard = await _eventLeaderboardsSystem.GetEventLeaderboardAsync(_selectedEventLeaderboard.Id);

            _selectedEventLeaderboardRecords.Clear();
            foreach (var score in detailedEventLeaderboard.Scores)
            {
                _selectedEventLeaderboardRecords.Add(score);
            }

            // Calculate zone boundaries and create display list
            var boundaries = EventLeaderboardZoneCalculator.CalculateZones(detailedEventLeaderboard);
            _displayItems.Clear();
            _displayItems.AddRange(EventLeaderboardZoneCalculator.CreateDisplayList(
                new List<IEventLeaderboardScore>(_selectedEventLeaderboardRecords), boundaries));

            return new List<IEventLeaderboardScore>(_selectedEventLeaderboardRecords);
        }

        #endregion

        #region Event Leaderboard Operations

        public async Task SubmitScoreAsync(int score, int subScore)
        {
            if (_selectedEventLeaderboard == null)
                return;

            await _eventLeaderboardsSystem.UpdateEventLeaderboardAsync(
                _selectedEventLeaderboard.Id,
                score,
                subScore);
        }

        public async Task ClaimRewardsAsync()
        {
            if (_selectedEventLeaderboard == null)
                return;

            await _eventLeaderboardsSystem.ClaimEventLeaderboardAsync(_selectedEventLeaderboard.Id);
            await _economySystem.RefreshAsync();
        }

        public async Task RollEventLeaderboardAsync()
        {
            if (_selectedEventLeaderboard == null)
                return;

            await _eventLeaderboardsSystem.RollEventLeaderboardAsync(_selectedEventLeaderboard.Id);
        }

        public async Task RefreshEconomyAsync()
        {
            await _economySystem.RefreshAsync();
        }

        public async Task SwitchCompleteAsync()
        {
            _selectedEventLeaderboard = null;
            _selectedLeaderboardId = string.Empty;
            _selectedEventLeaderboardRecords.Clear();
            _displayItems.Clear();
            await _economySystem.RefreshAsync();
        }

        #endregion

        #region Debug Operations

        public async Task DebugFillEventLeaderboardAsync(int targetCount)
        {
            if (_selectedEventLeaderboard == null)
                return;

            await _eventLeaderboardsSystem.DebugFillAsync(_selectedEventLeaderboard.Id, targetCount);
        }

        public async Task DebugRandomScoresAsync(int minScore, int maxScore, ApiOperator @operator, int subscoreMin, int subscoreMax)
        {
            if (_selectedEventLeaderboard == null)
                return;

            await _eventLeaderboardsSystem.DebugRandomScoresAsync(
                _selectedEventLeaderboard.Id,
                minScore,
                maxScore,
                @operator,
                subscoreMin,
                subscoreMax);
        }

        #endregion
    }
}
