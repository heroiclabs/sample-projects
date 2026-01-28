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

namespace HiroChallenges
{
    /// <summary>
    /// Controller for the Challenges system.
    /// Handles business logic and coordinates between the View and Hiro systems.
    /// </summary>
    [RequireComponent(typeof(UIDocument))]
    public class ChallengesController : MonoBehaviour
    {
        private const int DefaultMaxParticipants = 100;
        private const int DefaultDelaySeconds = 0;
        private const int DefaultDurationSeconds = 2000;

        [Header("UI Templates")]
        [SerializeField] private VisualTreeAsset _challengeEntryTemplate;
        [SerializeField] private VisualTreeAsset _challengeParticipantTemplate;

        private readonly Dictionary<string, IChallengeTemplate> _challengeTemplates = new();

        private IChallengesSystem _challengesSystem;
        private IEconomySystem _economySystem;
        private NakamaSystem _nakamaSystem;
        public string CurrentUserId;

        private string _selectedChallengeId;

        public List<IChallenge> Challenges { get; } = new();
        public IChallenge SelectedChallenge { get; private set; }


        public bool IsInitialized { get; private set; }
        public Exception InitializationError { get; private set; }

        private void Start()
        {
            var coordinator = HiroCoordinator.Instance as HiroChallengesCoordinator;
            if (coordinator == null)
            {
                Debug.LogError("HiroChallengesCoordinator not found");
                return;
            }

            // View manages itself and observes systems directly
            _ = new ChallengesView(
                this,
                GetComponent<UIDocument>().rootVisualElement,
                _challengeEntryTemplate,
                _challengeParticipantTemplate
            );

            coordinator.ReceivedStartError += HandleStartError;
            coordinator.ReceivedStartSuccess += HandleStartSuccess;
        }

        private void HandleStartError(Exception e)
        {
            InitializationError = e;
            Debug.LogException(e);
        }

        private void HandleStartSuccess()
        {
            InitializeSystems();
            IsInitialized = true;
        }

        private void InitializeSystems()
        {
            _nakamaSystem = this.GetSystem<NakamaSystem>();
            _challengesSystem = this.GetSystem<ChallengesSystem>();
            _economySystem = this.GetSystem<EconomySystem>();

            if (_nakamaSystem == null)
                throw new InvalidOperationException("NakamaSystem not available");
            if (_challengesSystem == null)
                throw new InvalidOperationException("ChallengesSystem not available");
            if (_economySystem == null)
                throw new InvalidOperationException("EconomySystem not available");

            CurrentUserId = _nakamaSystem.UserId;
        }

        public async Task SwitchCompleteAsync()
        {
            SelectedChallenge = null;
            _selectedChallengeId = string.Empty;
            CurrentUserId = _nakamaSystem.UserId;
            await _economySystem.RefreshAsync();
        }

        public async Task<List<string>> LoadChallengeTemplatesAsync()
        {
            _challengeTemplates.Clear();
            var loadedTemplates = (await _challengesSystem.GetTemplatesAsync()).Templates;
            var challengeTemplateNames = new List<string>();

            foreach (var template in loadedTemplates)
            {
                _challengeTemplates[template.Key] = template.Value;

                var displayName = template.Value.AdditionalProperties.TryGetValue("display_name", out var name)
                    ? name
                    : template.Key;

                challengeTemplateNames.Add(displayName);
            }

            return challengeTemplateNames;
        }

        public IChallengeTemplate GetTemplate(int templateIndex)
        {
            if (templateIndex < 0 || templateIndex >= _challengeTemplates.Count)
                return null;

            return _challengeTemplates.ElementAt(templateIndex).Value;
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
                return null;
            }

            _selectedChallengeId = challengeId;
            SelectedChallenge = await _challengesSystem.GetChallengeAsync(challengeId, true);
            return SelectedChallenge.Scores.ToList();
        }

        public IChallengeScore GetCurrentParticipant(IReadOnlyList<IChallengeScore> participants)
        {
            return participants.FirstOrDefault(p => p.Id == CurrentUserId);
        }

        public async Task<IChallenge> CreateChallengeAsync(
            int templateIndex,
            string challengeName,
            int maxParticipants,
            string[] inviteeIds,
            int delaySeconds,
            int durationSeconds,
            bool isOpen)
        {
            if (templateIndex < 0 || templateIndex >= _challengeTemplates.Count)
                throw new ArgumentException("Please select a valid Challenge template.", nameof(templateIndex));

            var selectedTemplate = _challengeTemplates.ElementAt(templateIndex);

            selectedTemplate.Value.AdditionalProperties.TryGetValue("description", out var description);
            selectedTemplate.Value.AdditionalProperties.TryGetValue("category", out var category);

            var newChallenge = await _challengesSystem.CreateChallengeAsync(
                selectedTemplate.Key,
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

    #region Data Transfer Objects

    public class ChallengeRefreshResult
    {
        public int SelectedChallengeIndex { get; set; }
        public List<IChallengeScore> Participants { get; set; }
    }

    public class ChallengeCreationDefaults
    {
        public int MaxParticipants { get; set; }
        public int DelaySeconds { get; set; }
        public int DurationSeconds { get; set; }
    }

    #endregion
}
