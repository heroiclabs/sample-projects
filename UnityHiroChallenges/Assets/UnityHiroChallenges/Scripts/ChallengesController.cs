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
        private string _currentUserId;

        private IChallenge _selectedChallenge;
        private string _selectedChallengeId;

        private ChallengesView _view;

        public List<IChallenge> Challenges { get; } = new();

        public event Action<ISession, ChallengesController> OnInitialized;
        public event Action<Exception> OnInitializationFailed;

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

            _view = new ChallengesView(
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
            OnInitializationFailed?.Invoke(e);
        }

        private async void HandleStartSuccess(ISession session)
        {
            InitializeSystems();
            await _view.InitializeAsync(_economySystem);
            IsInitialized = true;
            OnInitialized?.Invoke(session, this);
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

            _currentUserId = _nakamaSystem.UserId;
        }

        public async Task SwitchCompleteAsync()
        {
            await _view.RefreshChallengesAsync();
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

                var participants = await SelectChallengeAsync(challenge);
                return new ChallengeRefreshResult
                {
                    SelectedChallengeIndex = Challenges.IndexOf(challenge),
                    Participants = participants
                };
            }

            return null;
        }

        public async Task<List<IChallengeScore>> SelectChallengeAsync(IChallenge challenge)
        {
            if (challenge == null)
            {
                _selectedChallengeId = string.Empty;
                _selectedChallenge = null;
                return null;
            }

            _selectedChallenge = challenge;
            _selectedChallengeId = challenge.Id;

            var detailedChallenge = await _challengesSystem.GetChallengeAsync(_selectedChallenge.Id, true);
            return detailedChallenge.Scores.ToList();
        }

        public ChallengePermissions GetPermissions(IChallenge challenge, IReadOnlyList<IChallengeScore> participants)
        {
            var currentParticipant = participants.FirstOrDefault(p => p.Id == _currentUserId);
            return new ChallengePermissions(challenge, currentParticipant);
        }

        public async Task CreateChallengeAsync(
            int templateIndex,
            string challengeName,
            int maxParticipants,
            string inviteesInput,
            int delaySeconds,
            int durationSeconds,
            bool isOpen)
        {
            if (templateIndex < 0 || templateIndex >= _challengeTemplates.Count)
                throw new ArgumentException("Please select a valid Challenge template.", nameof(templateIndex));

            var selectedTemplate = _challengeTemplates.ElementAt(templateIndex);
            var inviteeIDs = await ParseAndValidateInviteesAsync(inviteesInput);

            selectedTemplate.Value.AdditionalProperties.TryGetValue("description", out var description);
            selectedTemplate.Value.AdditionalProperties.TryGetValue("category", out var category);

            var newChallenge = await _challengesSystem.CreateChallengeAsync(
                selectedTemplate.Key,
                challengeName,
                description ?? "Missing description.",
                inviteeIDs,
                isOpen,
                selectedTemplate.Value.MaxNumScore,
                delaySeconds,
                durationSeconds,
                maxParticipants,
                category ?? "Missing category",
                new Dictionary<string, string>()
            );

            _selectedChallengeId = newChallenge.Id;
            _selectedChallenge = newChallenge;
        }

        public async Task JoinChallengeAsync()
        {
            if (_selectedChallenge == null)
                throw new InvalidOperationException("No challenge selected");

            await _challengesSystem.JoinChallengeAsync(_selectedChallenge.Id);
        }

        public async Task LeaveChallengeAsync()
        {
            if (_selectedChallenge == null)
                throw new InvalidOperationException("No challenge selected");

            await _challengesSystem.LeaveChallengeAsync(_selectedChallenge.Id);
        }

        public async Task ClaimChallengeAsync()
        {
            if (_selectedChallenge == null)
                throw new InvalidOperationException("No challenge selected");

            await _challengesSystem.ClaimChallengeAsync(_selectedChallenge.Id);
            await _economySystem.RefreshAsync();
        }

        public async Task SubmitScoreAsync(int score, int subScore)
        {
            if (_selectedChallenge == null)
                throw new InvalidOperationException("No challenge selected");

            await _challengesSystem.SubmitChallengeScoreAsync(
                _selectedChallenge.Id,
                score,
                subScore,
                null,
                true
            );
        }

        public async Task InviteToChallengeAsync(string inviteesInput)
        {
            if (_selectedChallenge == null)
                throw new InvalidOperationException("No challenge selected");

            var inviteeIDs = await ParseAndValidateInviteesAsync(inviteesInput);

            await _challengesSystem.InviteChallengeAsync(
                _selectedChallenge.Id,
                inviteeIDs
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

        public IEnumerable<string> GetKnownUsernames()
        {
            var currentUsername = _nakamaSystem.Session.Username;

#if UNITY_EDITOR
            var usernames = GetEditorKnownUsernames();
#else
            var usernames = Array.Empty<string>();
#endif

            var filtered = new List<string>();
            foreach (var username in usernames)
            {
                if (!string.Equals(username, currentUsername, StringComparison.OrdinalIgnoreCase))
                    filtered.Add(username);
            }
            return filtered;
        }

#if UNITY_EDITOR
        private static IEnumerable<string> GetEditorKnownUsernames()
        {
            const string accountUsernamesKey = "AccountSwitcher_Usernames";
            var savedUsernames = UnityEditor.EditorPrefs.GetString(accountUsernamesKey, "");

            if (string.IsNullOrEmpty(savedUsernames))
                return Array.Empty<string>();

            try
            {
                var data = JsonUtility.FromJson<UsernameStorage>(savedUsernames);
                if (data?.items == null)
                    return Array.Empty<string>();

                var result = new List<string>();
                foreach (var item in data.items)
                {
                    if (!string.IsNullOrEmpty(item.value))
                        result.Add(item.value);
                }
                return result;
            }
            catch
            {
                return Array.Empty<string>();
            }
        }

        [Serializable]
        private class UsernameStorage
        {
            public List<UsernameItem> items;
        }

        [Serializable]
        private class UsernameItem
        {
            public string key;
            public string value;
        }
#endif

        private async Task<string[]> ParseAndValidateInviteesAsync(string inviteesInput)
        {
            if (string.IsNullOrEmpty(inviteesInput))
                throw new ArgumentException("Invitees field cannot be empty. Please enter at least one username.");

            var inviteeUsernames = inviteesInput
                .Split(',')
                .Select(username => username.Trim())
                .Where(username => !string.IsNullOrEmpty(username))
                .ToList();

            if (inviteeUsernames.Count == 0)
                throw new ArgumentException("No valid usernames found. Please enter at least one username.");

            var result = await _nakamaSystem.Client.GetUsersAsync(
                _nakamaSystem.Session,
                usernames: inviteeUsernames,
                ids: null
            );
            var inviteeIDs = result.Users.Select(user => user.Id).ToList();

            if (inviteeIDs.Count != inviteeUsernames.Count)
            {
                throw new ArgumentException(
                    $"Could not find all users. Requested: {inviteeUsernames.Count}, Found: {inviteeIDs.Count}");
            }

            return inviteeIDs.ToArray();
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
