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
using HiroChallenges.Tools;
using UnityEngine;

namespace HiroChallenges
{
    /// <summary>
    /// Controller/Presenter for the Challenges system.
    /// Handles business logic and coordinates with Hiro systems.
    /// </summary>
    public class ChallengesController : IDisposable
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

        public ChallengesController(NakamaSystem nakamaSystem, IChallengesSystem challengesSystem,
            IEconomySystem economySystem)
        {
            _nakamaSystem = nakamaSystem ?? throw new ArgumentNullException(nameof(nakamaSystem));
            _challengesSystem = challengesSystem ?? throw new ArgumentNullException(nameof(challengesSystem));
            _economySystem = economySystem ?? throw new ArgumentNullException(nameof(economySystem));

            CurrentUserId = _nakamaSystem.UserId;

            AccountSwitcher.AccountSwitched += OnAccountSwitched;
        }

        private async void OnAccountSwitched()
        {
            SelectedChallenge = null;
            _selectedChallengeId = string.Empty;
            CurrentUserId = _nakamaSystem.UserId;

            try
            {
                await _economySystem.RefreshAsync();
            }
            catch (Exception e)
            {
                Debug.LogException(e);
            }
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

            orderedTemplates.Sort((a, b) =>
                string.Compare(a.DisplayName, b.DisplayName, StringComparison.OrdinalIgnoreCase));

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
            return string.IsNullOrEmpty(templateId) ? null : _challengeTemplates.GetValueOrDefault(templateId);
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
            var challenge = await _challengesSystem.GetChallengeAsync(challengeId, true);

            return SelectChallenge(challenge);
        }

        public List<IChallengeScore> SelectChallenge(IChallenge challenge)
        {
            if (challenge == null)
            {
                _selectedChallengeId = string.Empty;
                SelectedChallenge = null;
                return new List<IChallengeScore>();
            }

            _selectedChallengeId = challenge.Id;
            SelectedChallenge = challenge;
            return new List<IChallengeScore>(SelectedChallenge.Scores);
        }


        public IChallengeScore GetCurrentParticipant(IReadOnlyList<IChallengeScore> participants)
        {
            foreach (var participant in participants)
                if (participant.Id == CurrentUserId)
                    return participant;
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
            if (string.IsNullOrEmpty(templateId) || !_challengeTemplates.TryGetValue(templateId, out var template))
                throw new ArgumentException("Please select a valid Challenge template.", nameof(templateId));

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

        public async Task<IChallenge> JoinChallengeAsync()
        {
            if (SelectedChallenge == null)
                throw new InvalidOperationException("No challenge selected");

            return await _challengesSystem.JoinChallengeAsync(SelectedChallenge.Id);
        }

        public async Task<IChallenge> LeaveChallengeAsync()
        {
            if (SelectedChallenge == null)
                throw new InvalidOperationException("No challenge selected");

            return await _challengesSystem.LeaveChallengeAsync(SelectedChallenge.Id);
        }

        public async Task<IChallenge> ClaimChallengeAsync()
        {
            if (SelectedChallenge == null)
                throw new InvalidOperationException("No challenge selected");

            var claimedChallenge = await _challengesSystem.ClaimChallengeAsync(SelectedChallenge.Id);

            var refreshTasks = new List<Task>();

            if (claimedChallenge.Reward == null) return claimedChallenge;

            if (claimedChallenge.Reward.Currencies.Count > 0)
                refreshTasks.Add(_economySystem.RefreshAsync());

            // Extra refreshes if using more Hiro systems:
            //
            // if (claimedChallenge.Reward.Items.Count > 0 || claimedChallenge.Reward.ItemInstances.Count > 0)
            //     refreshTasks.Add(_inventorySystem.RefreshAsync());
            //
            // if (claimedChallenge.Reward.Energies.Count > 0 || claimedChallenge.Reward.EnergyModifiers.Count > 0)
            //     refreshTasks.Add(_energiesSystem.RefreshAsync());

            await Task.WhenAll(refreshTasks);

            return claimedChallenge;
        }

        public async Task<IChallenge> SubmitScoreAsync(int score, int subScore)
        {
            if (SelectedChallenge == null)
                throw new InvalidOperationException("No challenge selected");

            return await _challengesSystem.SubmitChallengeScoreAsync(
                SelectedChallenge.Id,
                score,
                subScore,
                null!,
                true
            );
        }

        public async Task<IChallenge> InviteToChallengeAsync(string[] inviteeIds)
        {
            if (SelectedChallenge == null)
                throw new InvalidOperationException("No challenge selected");

            return await _challengesSystem.InviteChallengeAsync(
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

        public void Dispose()
        {
            AccountSwitcher.AccountSwitched -= OnAccountSwitched;
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