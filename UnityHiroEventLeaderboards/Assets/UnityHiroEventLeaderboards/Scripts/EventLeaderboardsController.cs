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
using System.Linq;
using System.Threading.Tasks;
using Hiro;
using Hiro.Unity;
using Nakama;
using UnityEngine;
using UnityEngine.UIElements;

namespace HiroEventLeaderboards
{
    [RequireComponent(typeof(UIDocument))]
    public class EventLeaderboardsController : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private VisualTreeAsset eventLeaderboardEntryTemplate;
        [SerializeField] private VisualTreeAsset eventLeaderboardRecordTemplate;
        [SerializeField] private VisualTreeAsset eventLeaderboardZoneTemplate;

        private IEventLeaderboardsSystem _eventLeaderboardsSystem;
        private IEconomySystem _economySystem;
        private NakamaSystem _nakamaSystem;
        private IEventLeaderboard _selectedEventLeaderboard;
        private string _selectedLeaderboardId;

        private EventLeaderboardsView _view;

        public string CurrentUserId => _nakamaSystem.UserId;
        public string CurrentUsername => _nakamaSystem.Session?.Username;
        public List<IEventLeaderboard> EventLeaderboards { get; } = new();
        public List<IEventLeaderboardScore> SelectedEventLeaderboardRecords { get; } = new();
        public List<LeaderboardDisplayItem> DisplayItems { get; } = new();

        public event Action<ISession, EventLeaderboardsController> OnInitialized;

        #region Initialization

        private void Start()
        {
            var coordinator = HiroCoordinator.Instance as HiroEventLeaderboardsCoordinator;
            if (coordinator == null) return;

            coordinator.ReceivedStartError += HandleStartError;
            coordinator.ReceivedStartSuccess += HandleStartSuccess;

            _view = new EventLeaderboardsView(this, coordinator, eventLeaderboardEntryTemplate,
                eventLeaderboardRecordTemplate, eventLeaderboardZoneTemplate);
        }

        private static void HandleStartError(Exception e)
        {
            Debug.LogException(e);
        }

        private async void HandleStartSuccess(ISession session)
        {
            // Cache Hiro systems.
            _nakamaSystem = this.GetSystem<NakamaSystem>();
            _eventLeaderboardsSystem = this.GetSystem<EventLeaderboardsSystem>();
            _economySystem = this.GetSystem<EconomySystem>();

            await _view.RefreshEventLeaderboards();

            OnInitialized?.Invoke(session, this);
        }

        public void SwitchComplete()
        {
            _ = _view.RefreshEventLeaderboards();
            _ = _economySystem.RefreshAsync();
        }

        #endregion

        #region Event Leaderboard List Operations

        public async Task<Tuple<int, List<IEventLeaderboardScore>>> RefreshEventLeaderboards()
        {
            EventLeaderboards.Clear();

            var eventLeaderboards = await _eventLeaderboardsSystem.ListEventLeaderboardsAsync(null, true);

            // Sort by time remaining (soonest ending first), then by ID alphabetically
            var sortedLeaderboards = eventLeaderboards.Leaderboards
                .OrderBy(lb =>
                {
                    var timeRemaining = EventLeaderboardTimeUtility.GetTimeRemaining(lb);
                    // If event has ended (timeRemaining is zero), sort to bottom
                    if (timeRemaining == TimeSpan.Zero)
                    {
                        return long.MaxValue;
                    }
                    return (long)timeRemaining.TotalSeconds;
                })
                .ThenBy(lb => lb.Id); // Secondary sort by ID alphabetically

            EventLeaderboards.AddRange(sortedLeaderboards);

            // If we have an event leaderboard selected, try to reselect it
            foreach (var eventLeaderboard in EventLeaderboards)
            {
                if (eventLeaderboard.Id != _selectedLeaderboardId) continue;

                var records = await SelectEventLeaderboard(eventLeaderboard);
                return new Tuple<int, List<IEventLeaderboardScore>>(EventLeaderboards.IndexOf(eventLeaderboard), records);
            }

            return null;
        }

        public async Task<List<IEventLeaderboardScore>> SelectEventLeaderboard(IEventLeaderboard eventLeaderboard)
        {
            if (eventLeaderboard == null)
            {
                _selectedLeaderboardId = string.Empty;
                return null;
            }

            _selectedEventLeaderboard = eventLeaderboard;
            _selectedLeaderboardId = _selectedEventLeaderboard.Id;

            // Get detailed event leaderboard info with scores
            var detailedEventLeaderboard = await _eventLeaderboardsSystem.GetEventLeaderboardAsync(_selectedEventLeaderboard.Id);
            var scores = detailedEventLeaderboard.Scores.ToList();

            // Calculate zone boundaries and create display list
            var boundaries = EventLeaderboardZoneCalculator.CalculateZones(detailedEventLeaderboard);
            DisplayItems.Clear();
            DisplayItems.AddRange(EventLeaderboardZoneCalculator.CreateDisplayList(scores, boundaries));

            return scores;
        }

        #endregion

        #region Event Leaderboard Operations

        public async Task SubmitScore(int score, int subScore)
        {
            if (_selectedEventLeaderboard == null) return;

            await _eventLeaderboardsSystem.UpdateEventLeaderboardAsync(
                _selectedEventLeaderboard.Id,
                score,
                subScore
            );
        }

        public async Task ClaimRewards()
        {
            if (_selectedEventLeaderboard == null) return;

            await _eventLeaderboardsSystem.ClaimEventLeaderboardAsync(_selectedEventLeaderboard.Id);
            await _economySystem.RefreshAsync();
        }

        public async Task RollEventLeaderboard()
        {
            if (_selectedEventLeaderboard == null) return;

            await _eventLeaderboardsSystem.RollEventLeaderboardAsync(_selectedEventLeaderboard.Id);
        }

        #endregion

        #region Debug Operations

        public async Task DebugFillEventLeaderboard(int targetCount)
        {
            if (_selectedEventLeaderboard == null) return;

            await _eventLeaderboardsSystem.DebugFillAsync(_selectedEventLeaderboard.Id, targetCount);
        }

        public async Task DebugRandomScores(int minScore, int maxScore, ApiOperator @operator, int subscoreMin, int subscoreMax)
        {
            if (_selectedEventLeaderboard == null) return;

            await _eventLeaderboardsSystem.DebugRandomScoresAsync(
                _selectedEventLeaderboard.Id,
                minScore,
                maxScore,
                @operator,
                subscoreMin,
                subscoreMax
            );
        }

        #endregion
    }
}
