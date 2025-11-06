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

        public event Action<ISession, ChallengesController> OnInitialized;

        private ChallengesView _view;
        private int _selectedTabIndex;
        private string _selectedChallengeId;
        private IChallenge _selectedChallenge;
        private NakamaSystem _nakamaSystem;
        private IChallengesSystem _challengesSystem;
        private IEconomySystem _economySystem;
        private readonly Dictionary<string, IChallengeTemplate> _challengeTemplates = new();
        private readonly List<IChallenge> _challenges = new();

        #region Initialization

        private void Start()
        {
            InitializeView();
            
            var challengesCoordinator = HiroCoordinator.Instance as HiroChallengesCoordinator;
            if (challengesCoordinator == null) return;

            challengesCoordinator.ReceivedStartError += HandleStartError;
            challengesCoordinator.ReceivedStartSuccess += HandleStartSuccess;
        }

        private void InitializeView()
        {
            var rootElement = GetComponent<UIDocument>().rootVisualElement;
            _view = new ChallengesView(challengeEntryTemplate, challengeParticipantTemplate);
            _view.SetController(this);
            _view.Initialize(rootElement);
            _view.HideSelectedChallengePanel();
        }

        private void HandleStartError(Exception e)
        {
            Debug.LogException(e);
            _view.ShowError(e.Message);
        }

        private void HandleStartSuccess(ISession session)
        {
            OnInitialized?.Invoke(session, this);

            // Cache Hiro systems
            _nakamaSystem = this.GetSystem<NakamaSystem>();
            _challengesSystem = this.GetSystem<ChallengesSystem>();
            _economySystem = this.GetSystem<EconomySystem>();

            _view.StartObservingWallet();

            _ = RefreshChallenges();
            _ = LoadChallengeTemplates();
        }

        public void SwitchComplete()
        {
            _view.HideAllModals();
            _ = RefreshChallenges();
            _economySystem.RefreshAsync();
        }

        #endregion

        #region Public Methods

        public void SwitchToMyChallengesTab()
        {
            if (_selectedTabIndex == 0) return;
            _selectedTabIndex = 0;
            _view.SetMyChallengesTabSelected(true);
            _ = RefreshChallenges();
        }

        public void OnTemplateChanged(int templateIndex)
        {
            UpdateCreateModalLimits(templateIndex);
        }

        public void OnChallengeSelected(IChallenge challenge)
        {
            _ = SelectChallenge(challenge);
        }

        public void OnMaxParticipantsChanged(int templateIndex)
        {
            var template = GetSelectedTemplate(templateIndex);
            if (template != null)
            {
                _view.UpdateCreateModalLimits(template);
            }
        }

        #endregion

        #region Challenge Templates

        private async Task LoadChallengeTemplates()
        {
            try
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

                _view.SetTemplateChoices(challengeTemplateNames);
            }
            catch (Exception e)
            {
                _view.ShowError($"Failed to load challenge templates: {e.Message}");
            }
        }

        public IChallengeTemplate GetSelectedTemplate(int templateIndex)
        {
            if (templateIndex < 0 || templateIndex >= _challengeTemplates.Count)
            {
                return null;
            }
            return _challengeTemplates.ElementAt(templateIndex).Value;
        }

        public void UpdateCreateModalLimits(int templateIndex)
        {
            var template = GetSelectedTemplate(templateIndex);
            if (template != null)
            {
                _view.UpdateCreateModalLimits(template);
            }
        }

        #endregion

        #region Challenge Selection

        public async Task SelectChallenge(IChallenge challenge)
        {
            if (challenge == null)
            {
                _selectedChallengeId = string.Empty;
                _view.HideSelectedChallengePanel();
                return;
            }

            _selectedChallenge = challenge;
            _selectedChallengeId = _selectedChallenge.Id;

            _view.ShowSelectedChallengePanel(_selectedChallenge);

            // Get detailed challenge info with scores
            try
            {
                var detailedChallenge = await _challengesSystem.GetChallengeAsync(_selectedChallenge.Id, true);
                _view.SetSelectedChallengeParticipants(_selectedChallenge, detailedChallenge.Scores);
                UpdateChallengeButtons(detailedChallenge.Scores.ToList());
            }
            catch (Exception e)
            {
                _view.ShowError(e.Message);
            }
        }

        public void UpdateChallengeButtons(List<IChallengeScore> participants)
        {
            if (_selectedChallenge == null) return;

            var isActive = _selectedChallenge.IsActive;
            IChallengeScore foundParticipant = null;
            
            foreach (var participant in participants)
            {
                if (participant.Id != _nakamaSystem.UserId || participant.State != ChallengeState.Joined) continue;
                foundParticipant = participant;
                break;
            }

            var canClaim = _selectedChallenge.CanClaim;

            // Determine button visibility
            var showJoin = isActive && foundParticipant == null;
            var showLeave = !isActive && foundParticipant != null && !canClaim;
            var showSubmitScore = isActive && foundParticipant != null && 
                                  foundParticipant.NumScores < _selectedChallenge.MaxNumScore;
            var submitScoreText = $"Submit Score ({foundParticipant?.NumScores}/{_selectedChallenge.MaxNumScore})";
            var showInvite = isActive && foundParticipant != null && 
                             foundParticipant.Id == _selectedChallenge.OwnerId &&
                             _selectedChallenge.Size < _selectedChallenge.MaxSize;
            var showClaimRewards = !isActive && foundParticipant != null && canClaim;

            _view.UpdateChallengeButtons(showJoin, showLeave, showSubmitScore, submitScoreText, 
                showInvite, showClaimRewards);
        }

        #endregion

        #region Challenge Operations

        public async Task RefreshChallenges()
        {
            _challenges.Clear();

            try
            {
                var userChallengesResult = await _challengesSystem.ListChallengesAsync(null);
                _challenges.AddRange(userChallengesResult.Challenges);
            }
            catch (Exception e)
            {
                _view.ShowError(e.Message);
                return;
            }

            _view.SetChallenges(_challenges);
            _view.ClearChallengeSelection();

            // If we have a challenge selected, try to reselect it
            foreach (var challenge in _challenges)
            {
                if (challenge.Id != _selectedChallengeId) continue;

                _ = SelectChallenge(challenge);
                _view.SelectChallenge(_challenges.IndexOf(challenge));
                return;
            }

            _view.HideSelectedChallengePanel();
        }

        public async Task CreateChallenge(int templateIndex, string name, int maxParticipants, string inviteesInput,
            int delay, int duration, bool isOpen)
        {
            try
            {
                if (templateIndex < 0 || templateIndex >= _challengeTemplates.Count)
                {
                    throw new Exception("Please select a valid Challenge template.");
                }

                var selectedTemplate = _challengeTemplates.ElementAt(templateIndex);

                if (string.IsNullOrEmpty(inviteesInput))
                {
                    throw new Exception("Invitees field cannot be empty. Please enter at least one username.");
                }

                var inviteeUsernames = inviteesInput
                    .Split(',')
                    .Select(username => username.Trim())
                    .Where(username => !string.IsNullOrEmpty(username))
                    .ToList();

                if (inviteeUsernames.Count == 0)
                {
                    throw new Exception("No valid usernames found. Please enter at least one username.");
                }

                var invitees = await _nakamaSystem.Client.GetUsersAsync(
                    session: _nakamaSystem.Session,
                    usernames: inviteeUsernames,
                    ids: null
                );

                var inviteeIDs = invitees.Users.Select(user => user.Id).ToArray();

                if (inviteeIDs.Length != inviteeUsernames.Count)
                {
                    throw new Exception(
                        $"Could not find all users. Requested: {inviteeUsernames.Count}, Found: {inviteeIDs.Length}");
                }

                selectedTemplate.Value.AdditionalProperties.TryGetValue("description", out var description);
                selectedTemplate.Value.AdditionalProperties.TryGetValue("category", out var category);

                var newChallenge = await _challengesSystem.CreateChallengeAsync(
                    selectedTemplate.Key,
                    name,
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
            catch (Exception e)
            {
                _view.ShowError(e.Message);
                return;
            }

            _view.HideCreateModal();
            _ = RefreshChallenges();
        }

        public async Task JoinChallenge()
        {
            if (_selectedChallenge == null) return;

            try
            {
                await _challengesSystem.JoinChallengeAsync(_selectedChallenge.Id);
            }
            catch (Exception e)
            {
                _view.ShowError(e.Message);
                return;
            }

            _ = RefreshChallenges();
        }

        public async Task LeaveChallenge()
        {
            if (_selectedChallenge == null) return;

            try
            {
                await _challengesSystem.LeaveChallengeAsync(_selectedChallenge.Id);
                _view.ClearChallengeSelection();
            }
            catch (Exception e)
            {
                _view.ShowError(e.Message);
                return;
            }

            _ = RefreshChallenges();
        }

        public async Task ClaimChallenge()
        {
            if (_selectedChallenge == null) return;

            try
            {
                await _challengesSystem.ClaimChallengeAsync(_selectedChallenge.Id);
                await _economySystem.RefreshAsync();
            }
            catch (Exception e)
            {
                _view.ShowError(e.Message);
                return;
            }

            _ = RefreshChallenges();
        }

        public async Task SubmitScore(int score, int subScore, string metadata)
        {
            if (_selectedChallenge == null) return;

            try
            {
                await _challengesSystem.SubmitChallengeScoreAsync(
                    _selectedChallenge.Id,
                    score,
                    subScore,
                    metadata,
                    true
                );
            }
            catch (Exception e)
            {
                _view.ShowError(e.Message);
                return;
            }

            _view.HideSubmitScoreModal();
            _ = RefreshChallenges();
        }

        public async Task InviteToChallenge(string inviteesInput)
        {
            if (_selectedChallenge == null) return;

            try
            {
                if (string.IsNullOrEmpty(inviteesInput))
                {
                    throw new Exception("Invitees field cannot be empty. Please enter at least one username.");
                }

                var inviteeUsernames = inviteesInput
                    .Split(',')
                    .Select(username => username.Trim())
                    .Where(username => !string.IsNullOrEmpty(username))
                    .ToList();

                if (inviteeUsernames.Count == 0)
                {
                    throw new Exception("No valid usernames found. Please enter at least one username.");
                }

                var invitees = await _nakamaSystem.Client.GetUsersAsync(
                    session: _nakamaSystem.Session,
                    usernames: inviteeUsernames,
                    ids: null
                );

                var inviteeIDs = invitees.Users.Select(user => user.Id).ToArray();

                if (inviteeIDs.Length != inviteeUsernames.Count)
                {
                    throw new Exception(
                        $"Could not find all users. Requested: {inviteeUsernames.Count}, Found: {inviteeIDs.Length}");
                }

                await _challengesSystem.InviteChallengeAsync(
                    challengeId: _selectedChallenge.Id,
                    userIds: inviteeIDs
                );
            }
            catch (Exception e)
            {
                _view.ShowError(e.Message);
                return;
            }

            _view.HideInviteModal();
            _ = RefreshChallenges();
        }

        #endregion
    }
}