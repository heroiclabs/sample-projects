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

namespace HiroChallenges
{
    /// <summary>
    /// Controller/Presenter for the Challenges system.
    /// Handles business logic and coordinates with Hiro systems.
    /// </summary>
    public class ChallengesController
    {
        private const int DefaultMaxParticipants = 100;
        private const int DefaultDelaySeconds = 0;
        private const int DefaultDurationSeconds = 2000;

        private readonly Dictionary<string, IChallengeTemplate> _challengeTemplates = new();

        private readonly IChallengesSystem _challengesSystem;
        private readonly IEconomySystem _economySystem;
        private readonly NakamaSystem _nakamaSystem;

        private string _selectedChallengeId;

        public string CurrentUserId { get; private set; }
        public List<IChallenge> Challenges { get; } = new();
        public IChallenge SelectedChallenge { get; private set; }

        public ChallengesController(NakamaSystem nakamaSystem, IChallengesSystem challengesSystem, IEconomySystem economySystem)
        {
            _nakamaSystem = nakamaSystem ?? throw new ArgumentNullException(nameof(nakamaSystem));
            _challengesSystem = challengesSystem ?? throw new ArgumentNullException(nameof(challengesSystem));
            _economySystem = economySystem ?? throw new ArgumentNullException(nameof(economySystem));

            CurrentUserId = _nakamaSystem.UserId;
        }

        public async Task SwitchCompleteAsync()
        {
            SelectedChallenge = null;
            _selectedChallengeId = string.Empty;
            CurrentUserId = _nakamaSystem.UserId;
            await _economySystem.RefreshAsync();
        }

        public async Task<List<ChallengeTemplateOption>> LoadChallengeTemplatesAsync()
        {
            _challengeTemplates.Clear();
            var loadedTemplates = (await _challengesSystem.GetTemplatesAsync()).Templates;
            var challengeTemplateOptions = new List<ChallengeTemplateOption>();
            var orderedTemplates = new List<(string Key, IChallengeTemplate Template, string DisplayName)>();

            foreach (var template in loadedTemplates)
            {
                var displayName = template.Value.AdditionalProperties.TryGetValue("display_name", out var name)
                    ? name
                    : template.Key;

                orderedTemplates.Add((template.Key, template.Value, displayName));
            }

            orderedTemplates.Sort((a, b) => string.Compare(a.DisplayName, b.DisplayName, StringComparison.OrdinalIgnoreCase));

            foreach (var template in orderedTemplates)
            {
                _challengeTemplates[template.Key] = template.Template;
                challengeTemplateOptions.Add(new ChallengeTemplateOption
                {
                    Id = template.Key,
                    DisplayName = template.DisplayName
                });
            }

            return challengeTemplateOptions;
        }

        public IChallengeTemplate GetTemplate(string templateId)
        {
            if (string.IsNullOrEmpty(templateId))
                return null;

            return _challengeTemplates.TryGetValue(templateId, out var template)
                ? template
                : null;
        }

        public async Task<ChallengeRefreshResult> RefreshChallengesAsync()
        {
            Challenges.Clear();

            var userChallengesResult = await _challengesSystem.ListChallengesAsync(null);
            Challenges.AddRange(userChallengesResult.Challenges);

            foreach (var challenge in Challenges)
            {
                if (challenge.Id != _selectedChallengeId)
                    continue;

                var participants = await SelectChallengeAsync(challenge.Id);
                return new ChallengeRefreshResult
                {
                    SelectedChallengeIndex = Challenges.IndexOf(challenge),
                    Participants = participants
                };
            }

            return null;
        }

        public async Task<List<IChallengeScore>> SelectChallengeAsync(string challengeId)
        {
            if (string.IsNullOrEmpty(challengeId))
            {
                _selectedChallengeId = string.Empty;
                SelectedChallenge = null;
                return new List<IChallengeScore>();
            }

            _selectedChallengeId = challengeId;
            SelectedChallenge = await _challengesSystem.GetChallengeAsync(challengeId, true);
            return new List<IChallengeScore>(SelectedChallenge.Scores);
        }

        public IChallengeScore GetCurrentParticipant(IReadOnlyList<IChallengeScore> participants)
        {
            foreach (var participant in participants)
            {
                if (participant.Id == CurrentUserId)
                    return participant;
            }
            return null;
        }

        public async Task<IChallenge> CreateChallengeAsync(
            string templateId,
            string challengeName,
            int maxParticipants,
            string[] inviteeIds,
            int delaySeconds,
            int durationSeconds,
            bool isOpen)
        {
            if (string.IsNullOrEmpty(templateId) || !_challengeTemplates.ContainsKey(templateId))
                throw new ArgumentException("Please select a valid Challenge template.", nameof(templateId));

            var template = _challengeTemplates[templateId];

            template.AdditionalProperties.TryGetValue("description", out var description);
            template.AdditionalProperties.TryGetValue("category", out var category);

            var newChallenge = await _challengesSystem.CreateChallengeAsync(
                templateId,
                challengeName,
                description ?? "Missing description.",
                inviteeIds,
                isOpen,
                delaySeconds,
                durationSeconds,
                maxParticipants,
                category ?? "Missing category",
                new Dictionary<string, string>()
            );

            _selectedChallengeId = newChallenge.Id;
            SelectedChallenge = newChallenge;

            return newChallenge;
        }

        public async Task JoinChallengeAsync()
        {
            if (SelectedChallenge == null)
                throw new InvalidOperationException("No challenge selected");

            await _challengesSystem.JoinChallengeAsync(SelectedChallenge.Id);
        }

        public async Task LeaveChallengeAsync()
        {
            if (SelectedChallenge == null)
                throw new InvalidOperationException("No challenge selected");

            await _challengesSystem.LeaveChallengeAsync(SelectedChallenge.Id);
        }

        public async Task ClaimChallengeAsync()
        {
            if (SelectedChallenge == null)
                throw new InvalidOperationException("No challenge selected");

            await _challengesSystem.ClaimChallengeAsync(SelectedChallenge.Id);
            await _economySystem.RefreshAsync();
        }

        public async Task SubmitScoreAsync(int score, int subScore)
        {
            if (SelectedChallenge == null)
                throw new InvalidOperationException("No challenge selected");

            await _challengesSystem.SubmitChallengeScoreAsync(
                SelectedChallenge.Id,
                score,
                subScore,
                null,
                true
            );
        }

        public async Task InviteToChallengeAsync(string[] inviteeIds)
        {
            if (SelectedChallenge == null)
                throw new InvalidOperationException("No challenge selected");

            await _challengesSystem.InviteChallengeAsync(
                SelectedChallenge.Id,
                inviteeIds
            );
        }

        public ChallengeCreationDefaults GetCreationDefaults()
        {
            return new ChallengeCreationDefaults
            {
                MaxParticipants = DefaultMaxParticipants,
                DelaySeconds = DefaultDelaySeconds,
                DurationSeconds = DefaultDurationSeconds
            };
        }
    }

    public class ChallengeRefreshResult
    {
        public int SelectedChallengeIndex { get; set; }
        public List<IChallengeScore> Participants { get; set; }
    }

    public class ChallengeTemplateOption
    {
        public string Id { get; set; }
        public string DisplayName { get; set; }
    }

    public class ChallengeCreationDefaults
    {
        public int MaxParticipants { get; set; }
        public int DelaySeconds { get; set; }
        public int DurationSeconds { get; set; }
    }
}
