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
    [RequireComponent(typeof(UIDocument))]
    public class ChallengesController : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private VisualTreeAsset challengeEntryTemplate;
        [SerializeField] private VisualTreeAsset challengeParticipantTemplate;

        private readonly Dictionary<string, IChallengeTemplate> _challengeTemplates = new();
        private IChallengesSystem _challengesSystem;
        private IEconomySystem _economySystem;
        private NakamaSystem _nakamaSystem;
        private IChallenge _selectedChallenge;
        private string _selectedChallengeId;

        private ChallengesView _view;

        public string CurrentUserId => _nakamaSystem.UserId;
        public List<IChallenge> Challenges { get; } = new();

        public event Action<ISession, ChallengesController> OnInitialized;

        #region Initialization

        private void Start()
        {
            var challengesCoordinator = HiroCoordinator.Instance as HiroChallengesCoordinator;
            if (challengesCoordinator == null) return;

            challengesCoordinator.ReceivedStartError += HandleStartError;
            challengesCoordinator.ReceivedStartSuccess += HandleStartSuccess;

            _view = new ChallengesView(this, challengesCoordinator, challengeEntryTemplate,
                challengeParticipantTemplate);
        }

        private static void HandleStartError(Exception e)
        {
            Debug.LogException(e);
        }

        private async void HandleStartSuccess(ISession session)
        {
            // Initialize and cache Hiro system references
            _nakamaSystem = this.GetSystem<NakamaSystem>();
            _challengesSystem = this.GetSystem<ChallengesSystem>();
            _economySystem = this.GetSystem<EconomySystem>();

            await _view.RefreshChallenges();

            OnInitialized?.Invoke(session, this);
        }

        // Called when switching between accounts using the account switcher.
        public void SwitchComplete()
        {
            _ = _view.RefreshChallenges();
            _ = _economySystem.RefreshAsync();
        }

        #endregion

        #region Challenge Templates

        // Loads available challenge templates from the system and returns their display names
        public async Task<List<string>> LoadChallengeTemplates()
        {
            _challengeTemplates.Clear();
            var loadedTemplates = (await _challengesSystem.GetTemplatesAsync()).Templates;
            var challengeTemplateNames = new List<string>();

            foreach (var template in loadedTemplates)
            {
                _challengeTemplates[template.Key] = template.Value;
                challengeTemplateNames.Add(
                    template.Value.AdditionalProperties.TryGetValue("display_name", out var displayName)
                        ? displayName
                        : template.Key);
            }

            return challengeTemplateNames;
        }

        public IChallengeTemplate GetTemplate(int templateIndex)
        {
            if (templateIndex < 0 || templateIndex >= _challengeTemplates.Count) return null;
            return _challengeTemplates.ElementAt(templateIndex).Value;
        }

        #endregion

        #region Challenge List Operations

        public async Task<Tuple<int, List<IChallengeScore>>> RefreshChallenges()
        {
            Challenges.Clear();

            var userChallengesResult = await _challengesSystem.ListChallengesAsync(null);
            Challenges.AddRange(userChallengesResult.Challenges);

            // Try to reselect the previously selected challenge if it still exists
            foreach (var challenge in Challenges)
            {
                if (challenge.Id != _selectedChallengeId) continue;

                var participants = await SelectChallenge(challenge);
                return new Tuple<int, List<IChallengeScore>>(Challenges.IndexOf(challenge), participants);
            }

            return null;
        }

        public async Task<List<IChallengeScore>> SelectChallenge(IChallenge challenge)
        {
            if (challenge == null)
            {
                _selectedChallengeId = string.Empty;
                return null;
            }

            _selectedChallenge = challenge;
            _selectedChallengeId = _selectedChallenge.Id;

            var detailedChallenge = await _challengesSystem.GetChallengeAsync(_selectedChallenge.Id, true);
            return detailedChallenge.Scores.ToList();
        }

        #endregion

        #region Challenge Lifecycle Operations
        public async Task CreateChallenge(int templateIndex, string challengeName, int maxParticipants,
            string inviteesInput,
            int delay, int duration, bool isOpen)
        {
            if (templateIndex < 0 || templateIndex >= _challengeTemplates.Count)
                throw new Exception("Please select a valid Challenge template.");

            var selectedTemplate = _challengeTemplates.ElementAt(templateIndex);

            // Validate and parse invitee usernames from a string of usernames separated by a comma.
            if (string.IsNullOrEmpty(inviteesInput))
                throw new Exception("Invitees field cannot be empty. Please enter at least one username.");

            var inviteeUsernames = inviteesInput
                .Split(',')
                .Select(username => username.Trim())
                .Where(username => !string.IsNullOrEmpty(username))
                .ToList();

            if (inviteeUsernames.Count == 0)
                throw new Exception("No valid usernames found. Please enter at least one username.");

            // Convert usernames to user IDs via Nakama API
            var invitees = await _nakamaSystem.Client.GetUsersAsync(
                _nakamaSystem.Session,
                usernames: inviteeUsernames,
                ids: null
            );

            var inviteeIDs = invitees.Users.Select(user => user.Id).ToArray();

            // Verify all usernames were found
            if (inviteeIDs.Length != inviteeUsernames.Count)
                throw new Exception(
                    $"Could not find all users. Requested: {inviteeUsernames.Count}, Found: {inviteeIDs.Length}");

            selectedTemplate.Value.AdditionalProperties.TryGetValue("description", out var description);
            selectedTemplate.Value.AdditionalProperties.TryGetValue("category", out var category);

            var newChallenge = await _challengesSystem.CreateChallengeAsync(
                selectedTemplate.Key,
                challengeName,
                description ?? "Missing description.",
                inviteeIDs,
                isOpen,
                selectedTemplate.Value.MaxNumScore,
                delay,
                duration,
                maxParticipants,
                category ?? "Missing category",
                new Dictionary<string, string>()
            );

            _selectedChallengeId = newChallenge.Id;
            _selectedChallenge = newChallenge;
        }

        public async Task JoinChallenge()
        {
            if (_selectedChallenge == null) return;

            await _challengesSystem.JoinChallengeAsync(_selectedChallenge.Id);
        }

        public async Task LeaveChallenge()
        {
            if (_selectedChallenge == null) return;

            await _challengesSystem.LeaveChallengeAsync(_selectedChallenge.Id);
        }

        public async Task ClaimChallenge()
        {
            if (_selectedChallenge == null) return;

            await _challengesSystem.ClaimChallengeAsync(_selectedChallenge.Id);
            await _economySystem.RefreshAsync();
        }

        #endregion

        #region Challenge Participation Operations
        public async Task SubmitScore(int score, int subScore, string metadata)
        {
            if (_selectedChallenge == null) return;

            await _challengesSystem.SubmitChallengeScoreAsync(
                _selectedChallenge.Id,
                score,
                subScore,
                metadata,
                true
            );
        }

        public async Task InviteToChallenge(string inviteesInput)
        {
            if (_selectedChallenge == null) return;

            // Validate and parse invitee usernames from a string of usernames separated by a comma.
            if (string.IsNullOrEmpty(inviteesInput))
                throw new Exception("Invitees field cannot be empty. Please enter at least one username.");

            var inviteeUsernames = inviteesInput
                .Split(',')
                .Select(username => username.Trim())
                .Where(username => !string.IsNullOrEmpty(username))
                .ToList();

            if (inviteeUsernames.Count == 0)
                throw new Exception("No valid usernames found. Please enter at least one username.");

            // Convert usernames to user IDs via Nakama API
            var invitees = await _nakamaSystem.Client.GetUsersAsync(
                _nakamaSystem.Session,
                usernames: inviteeUsernames,
                ids: null
            );

            var inviteeIDs = invitees.Users.Select(user => user.Id).ToArray();

            // Verify all usernames were found
            if (inviteeIDs.Length != inviteeUsernames.Count)
                throw new Exception(
                    $"Could not find all users. Requested: {inviteeUsernames.Count}, Found: {inviteeIDs.Length}");

            await _challengesSystem.InviteChallengeAsync(
                _selectedChallenge.Id,
                inviteeIDs
            );
        }

        #endregion
    }
}