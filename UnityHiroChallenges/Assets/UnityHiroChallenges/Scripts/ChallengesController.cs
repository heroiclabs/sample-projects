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
        #region Constants
        
        private const int DefaultMaxParticipants = 100;
        private const int DefaultDelaySeconds = 0;
        private const int DefaultDurationSeconds = 2000;
        
        #endregion

        #region Serialized Fields
        
        [Header("References")]
        [SerializeField] private VisualTreeAsset _challengeEntryTemplate;
        [SerializeField] private VisualTreeAsset _challengeParticipantTemplate;

        #endregion

        #region Private Fields
        
        private readonly Dictionary<string, IChallengeTemplate> _challengeTemplates = new();
        
        // Hiro systems - injected via Initialize
        private IChallengesSystem _challengesSystem;
        private IEconomySystem _economySystem;
        private NakamaSystem _nakamaSystem;
        
        // Selected challenge state
        private IChallenge _selectedChallenge;
        private string _selectedChallengeId;
        
        // UI state
        private ChallengesView _view;
    
        private string CurrentUserId => _nakamaSystem?.UserId;
        #endregion

        #region Public Properties

        public List<IChallenge> Challenges { get; } = new();
        public ChallengeViewState ViewState { get; } = new();

        #endregion

        #region Events
        
        public event Action<ISession, ChallengesController> OnInitialized;

        #endregion

        #region Unity Lifecycle

        private void Start()
        {
            var challengesCoordinator = HiroCoordinator.Instance as HiroChallengesCoordinator;
            if (challengesCoordinator == null)
            {
                Debug.LogError("HiroChallengesCoordinator not found");
                return;
            }

            // Create view first, before subscribing to events
            _view = new ChallengesView(
                this,
                challengesCoordinator,
                _challengeEntryTemplate,
                _challengeParticipantTemplate
            );

            // Now subscribe to coordinator events
            challengesCoordinator.ReceivedStartError += HandleStartError;
            challengesCoordinator.ReceivedStartSuccess += HandleStartSuccess;
        }

        #endregion

        #region Initialization

        private static void HandleStartError(Exception e)
        {
            Debug.LogException(e);
        }

        private async void HandleStartSuccess(ISession session)
        {
            // Initialize Hiro systems from coordinator
            InitializeSystems();

            await _view.RefreshChallengesAsync();

            OnInitialized?.Invoke(session, this);
        }

        /// <summary>
        /// Initialize all required Hiro systems from the coordinator.
        /// </summary>
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
        }

        /// <summary>
        /// Called when switching between accounts using the account switcher.
        /// </summary>
        public async Task SwitchComplete()
        {
            await _view.RefreshChallengesAsync();
            await _economySystem.RefreshAsync();
        }

        #endregion

        #region Challenge Templates

        /// <summary>
        /// Loads available challenge templates from the system and returns their display names.
        /// </summary>
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

        #endregion

        #region Challenge List Operations

        /// <summary>
        /// Refreshes the list of challenges and updates view state.
        /// Returns the index of the previously selected challenge if it still exists.
        /// </summary>
        public async Task<ChallengeRefreshResult> RefreshChallengesAsync()
        {
            Challenges.Clear();

            var userChallengesResult = await _challengesSystem.ListChallengesAsync(null);
            Challenges.AddRange(userChallengesResult.Challenges);

            // Try to reselect the previously selected challenge if it still exists
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

        /// <summary>
        /// Selects a challenge and loads its participants.
        /// Updates view state with button visibility logic.
        /// </summary>
        public async Task<List<IChallengeScore>> SelectChallengeAsync(IChallenge challenge)
        {
            if (challenge == null)
            {
                _selectedChallengeId = string.Empty;
                _selectedChallenge = null;
                ViewState.Reset();
                return null;
            }

            _selectedChallenge = challenge;
            _selectedChallengeId = challenge.Id;

            var detailedChallenge = await _challengesSystem.GetChallengeAsync(_selectedChallenge.Id, true);
            var participants = detailedChallenge.Scores.ToList();
            
            // Update view state based on challenge and participant status
            UpdateViewState(challenge, participants);
            
            return participants;
        }

        /// <summary>
        /// Updates the view state based on challenge status and user participation.
        /// This determines what UI elements should be visible.
        /// </summary>
        private void UpdateViewState(IChallenge challenge, List<IChallengeScore> participants)
        {
            var foundParticipant = participants.FirstOrDefault(p => p.Id == CurrentUserId);
            var isActive = challenge.IsActive;
            var canClaim = challenge.CanClaim;

            ViewState.ShowJoinButton = isActive && foundParticipant == null;
            ViewState.ShowLeaveButton = !isActive && foundParticipant != null && !canClaim;
            ViewState.ShowSubmitScoreButton = isActive && 
                                                foundParticipant != null &&
                                                foundParticipant.NumScores < challenge.MaxNumScore;
            ViewState.ShowInviteButton = isActive && 
                                          foundParticipant != null &&
                                          foundParticipant.Id == challenge.OwnerId &&
                                          challenge.Size < challenge.MaxSize;
            ViewState.ShowClaimRewardsButton = !isActive && foundParticipant != null && canClaim;
            
            ViewState.SubmitScoreText = foundParticipant != null
                ? $"Submit Score ({foundParticipant.NumScores}/{challenge.MaxNumScore})"
                : "Submit Score";
        }

        #endregion

        #region Challenge Lifecycle Operations

        /// <summary>
        /// Creates a new challenge with the specified parameters.
        /// </summary>
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

            // Parse and validate invitees
            var inviteeIDs = await ParseAndValidateInviteesAsync(inviteesInput);

            // Get template metadata
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

        #endregion

        #region Challenge Participation Operations

        public async Task SubmitScoreAsync(int score, int subScore)
        {
            if (_selectedChallenge == null)
                throw new InvalidOperationException("No challenge selected");

            await _challengesSystem.SubmitChallengeScoreAsync(
                _selectedChallenge.Id,
                score,
                subScore,
                null, // metadata can be personalized according to the needs of the game
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

        #endregion

        #region Helper Methods

        /// <summary>
        /// Parses a comma-separated list of usernames and converts them to user IDs.
        /// Validates that all usernames were found.
        /// </summary>
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

            // Convert usernames to user IDs via Nakama API
            var invitees = await _nakamaSystem.Client.GetUsersAsync(
                _nakamaSystem.Session,
                usernames: inviteeUsernames,
                ids: null
            );

            var inviteeIDs = invitees.Users.Select(user => user.Id).ToArray();

            // Verify all usernames were found
            if (inviteeIDs.Length != inviteeUsernames.Count)
            {
                throw new ArgumentException(
                    $"Could not find all users. Requested: {inviteeUsernames.Count}, Found: {inviteeIDs.Length}");
            }

            return inviteeIDs;
        }

        #endregion

        #region Default Values

        /// <summary>
        /// Gets default values for challenge creation modal.
        /// </summary>
        public ChallengeCreationDefaults GetCreationDefaults()
        {
            return new ChallengeCreationDefaults
            {
                MaxParticipants = DefaultMaxParticipants,
                DelaySeconds = DefaultDelaySeconds,
                DurationSeconds = DefaultDurationSeconds
            };
        }

        #endregion
    }

    #region Data Transfer Objects

    /// <summary>
    /// Represents the UI state for challenge action buttons.
    /// Controller owns this state and View reads from it.
    /// </summary>
    public class ChallengeViewState
    {
        public bool ShowJoinButton { get; set; }
        public bool ShowLeaveButton { get; set; }
        public bool ShowSubmitScoreButton { get; set; }
        public bool ShowInviteButton { get; set; }
        public bool ShowClaimRewardsButton { get; set; }
        public string SubmitScoreText { get; set; } = "Submit Score";

        public void Reset()
        {
            ShowJoinButton = false;
            ShowLeaveButton = false;
            ShowSubmitScoreButton = false;
            ShowInviteButton = false;
            ShowClaimRewardsButton = false;
            SubmitScoreText = "Submit Score";
        }
    }

    /// <summary>
    /// Result of refreshing the challenges list.
    /// </summary>
    public class ChallengeRefreshResult
    {
        public int SelectedChallengeIndex { get; set; }
        public List<IChallengeScore> Participants { get; set; }
    }

    /// <summary>
    /// Default values for challenge creation.
    /// </summary>
    public class ChallengeCreationDefaults
    {
        public int MaxParticipants { get; set; }
        public int DelaySeconds { get; set; }
        public int DurationSeconds { get; set; }
    }

    #endregion
}