using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Hiro;
using Nakama;
using UnityEngine;

namespace HiroAchievements
{
    /// <summary>
    /// Controller for the Achievements system.
    /// Handles business logic and coordinates with Hiro systems.
    /// Plain C# class for testability - no MonoBehaviour inheritance.
    /// </summary>
    public class AchievementsController
    {
        private readonly NakamaSystem _nakamaSystem;
        private readonly IAchievementsSystem _achievementsSystem;
        private readonly IEconomySystem _economySystem;

        private IAchievement _selectedAchievement;
        private ISubAchievement _selectedSubAchievement;
        private IAchievement _parentAchievement;
        private string _selectedSubAchievementKey;
        private string _currentCategory = "quest";

        public List<IAchievement> AllAchievements { get; } = new();
        public List<IAchievement> RepeatAchievements { get; } = new();

        public string CurrentUserId => _nakamaSystem.UserId;

        public AchievementsController(
            NakamaSystem nakamaSystem,
            IAchievementsSystem achievementsSystem,
            IEconomySystem economySystem)
        {
            _nakamaSystem = nakamaSystem ?? throw new ArgumentNullException(nameof(nakamaSystem));
            _achievementsSystem = achievementsSystem ?? throw new ArgumentNullException(nameof(achievementsSystem));
            _economySystem = economySystem ?? throw new ArgumentNullException(nameof(economySystem));
        }

        #region Category Management

        public void SetCurrentCategory(string category)
        {
            _currentCategory = category;
        }

        public string GetCurrentCategory()
        {
            return _currentCategory;
        }

        #endregion

        #region Achievement Selection

        public void SelectAchievement(IAchievement achievement)
        {
            _selectedAchievement = achievement;
            _selectedSubAchievement = null;
            _parentAchievement = null;
            _selectedSubAchievementKey = null;
        }

        public IAchievement GetSelectedAchievement()
        {
            return _selectedAchievement;
        }

        public void SelectSubAchievement(ISubAchievement subAchievement, IAchievement parent, string subAchievementKey)
        {
            _selectedSubAchievement = subAchievement;
            _parentAchievement = parent;
            _selectedSubAchievementKey = subAchievementKey;
        }

        public ISubAchievement GetSelectedSubAchievement()
        {
            return _selectedSubAchievement;
        }

        public IAchievement GetParentAchievement()
        {
            return _parentAchievement;
        }

        public string GetSelectedSubAchievementKey()
        {
            return _selectedSubAchievementKey;
        }

        public void ClearSubAchievementSelection()
        {
            _selectedSubAchievement = null;
            _parentAchievement = null;
            _selectedSubAchievementKey = null;
        }

        #endregion

        #region Achievement Operations

        public async Task LoadAchievementsAsync()
        {
            AllAchievements.Clear();
            RepeatAchievements.Clear();

            await _achievementsSystem.RefreshAsync();

            var achievements = GetAllAchievements();
            AllAchievements.AddRange(achievements);

            var repeatAchievements = _achievementsSystem.GetRepeatAchievements();
            RepeatAchievements.AddRange(repeatAchievements);
        }

        public async Task<List<IAchievement>> RefreshAchievementsAsync()
        {
            await _achievementsSystem.RefreshAsync();

            AllAchievements.Clear();
            RepeatAchievements.Clear();

            var achievements = _achievementsSystem.GetAchievements(_currentCategory);
            AllAchievements.AddRange(achievements);

            var repeatAchievements = _achievementsSystem.GetRepeatAchievements(_currentCategory);
            RepeatAchievements.AddRange(repeatAchievements);

            return GetFilteredAchievements();
        }

        public List<IAchievement> GetFilteredAchievements()
        {
            var result = new List<IAchievement>();
            result.AddRange(GetAllAchievements(_currentCategory));
            return result;
        }

        public float GetResetTime()
        {
            foreach (var achievement in GetAllAchievements(_currentCategory))
            {
                if (achievement.ResetTimeSec > 0)
                {
                    return achievement.ResetTimeSec - achievement.CurrentTimeSec;
                }
            }
            return -1;
        }

        #endregion

        #region Achievement Actions

        public async Task UpdateAchievementProgressAsync(string achievementId, long progress)
        {
            if (string.IsNullOrEmpty(achievementId))
                throw new Exception("Invalid achievement ID.");

            if (progress <= 0)
                throw new Exception("Progress must be greater than 0.");

            var updates = new Dictionary<string, long>
            {
                { achievementId, progress }
            };

            await _achievementsSystem.UpdateAchievementsAsync(updates);
        }

        public async Task UpdateSelectedAchievementProgressAsync(long progress)
        {
            if (_selectedSubAchievement != null)
            {
                await UpdateAchievementProgressAsync(_selectedSubAchievement.Id, progress);

                if (_parentAchievement != null)
                {
                    await CheckAndProgressParentAchievementAsync(_parentAchievement);
                }
                return;
            }

            if (_selectedAchievement == null)
                throw new Exception("No achievement selected.");

            await UpdateAchievementProgressAsync(_selectedAchievement.Id, progress);
        }

        private async Task CheckAndProgressParentAchievementAsync(IAchievement parentAchievement)
        {
            if (!AchievementProgressHelper.HasSubAchievements(parentAchievement))
                return;

            await RefreshAchievementsAsync();

            IAchievement updatedParent = null;
            foreach (var a in AllAchievements)
            {
                if (a.Id == parentAchievement.Id)
                {
                    updatedParent = a;
                    break;
                }
            }

            if (updatedParent == null)
                return;

            bool allCompleted = AchievementProgressHelper.AreAllSubAchievementsCompleted(updatedParent);

            if (allCompleted && updatedParent.Count == 0)
            {
                await UpdateAchievementProgressAsync(updatedParent.Id, 1);
            }
        }

        public async Task ClaimAchievementRewardAsync(string achievementId, bool claimTotal = true)
        {
            if (string.IsNullOrEmpty(achievementId))
                throw new Exception("Invalid achievement ID.");

            await _achievementsSystem.ClaimAchievementsAsync(new[] { achievementId }, claimTotal);
            await _economySystem.RefreshAsync();
        }

        public async Task ClaimSelectedAchievementRewardAsync(bool claimTotal = true)
        {
            if (_selectedSubAchievement != null)
            {
                await ClaimAchievementRewardAsync(_selectedSubAchievement.Id, claimTotal);
                return;
            }

            if (_selectedAchievement == null)
                throw new Exception("No achievement selected.");

            await ClaimAchievementRewardAsync(_selectedAchievement.Id, claimTotal);
        }

        public async Task RefreshEconomyAsync()
        {
            await _economySystem.RefreshAsync();
        }

        public async Task SwitchCompleteAsync()
        {
            _selectedAchievement = null;
            _selectedSubAchievement = null;
            _parentAchievement = null;
            _selectedSubAchievementKey = null;
            await _economySystem.RefreshAsync();
        }

        #endregion

        #region Achievement State Checks

        public bool CanClaimReward(IAchievement achievement)
        {
            if (AchievementProgressHelper.HasSubAchievements(achievement))
            {
                return (achievement.HasAvailableReward() || achievement.HasAvailableTotalReward()) &&
                       !achievement.IsClaimed() && achievement.Count >= 1;
            }
            else
            {
                return (achievement.HasAvailableReward() || achievement.HasAvailableTotalReward()) &&
                       !achievement.IsClaimed() && achievement.Count >= achievement.MaxCount;
            }
        }

        public bool CanClaimReward(ISubAchievement subAchievement)
        {
            return subAchievement.HasAvailableReward() &&
                   !(subAchievement.ClaimTimeSec > 0) &&
                   subAchievement.Count >= subAchievement.MaxCount;
        }

        public bool IsAchievementCompleted(IAchievement achievement)
        {
            if (AchievementProgressHelper.HasSubAchievements(achievement))
            {
                return (achievement.HasAvailableReward() || achievement.HasAvailableTotalReward()) &&
                       achievement.IsClaimed() && achievement.Count >= 1;
            }
            else
            {
                return (achievement.HasAvailableReward() || achievement.HasAvailableTotalReward()) &&
                       achievement.IsClaimed();
            }
        }

        public bool IsAchievementCompleted(ISubAchievement subAchievement)
        {
            return subAchievement.HasAvailableReward() && subAchievement.ClaimTimeSec > 0;
        }

        public bool IsAchievementClaimable(IAchievement achievement)
        {
            return (achievement.HasAvailableReward() || achievement.HasAvailableTotalReward()) &&
                   !achievement.IsClaimed() && achievement.Count >= achievement.MaxCount;
        }

        public bool IsAchievementClaimable(ISubAchievement subAchievement)
        {
            return subAchievement.HasAvailableReward() &&
                   subAchievement.ClaimTimeSec <= 0 &&
                   subAchievement.Count >= subAchievement.MaxCount;
        }

        public bool IsAchievementLocked(IAchievement achievement)
        {
            var availableAchievements = _achievementsSystem.GetAvailableAchievements();
            var availableRepeatAchievements = _achievementsSystem.GetAvailableRepeatAchievements();

            foreach (var a in availableAchievements)
            {
                if (a.Id == achievement.Id)
                    return false;
            }

            foreach (var a in availableRepeatAchievements)
            {
                if (a.Id == achievement.Id)
                    return false;
            }

            return true;
        }

        public List<IAchievement> GetPrerequisiteAchievements(IAchievement achievement)
        {
            var prerequisites = new List<IAchievement>();

            if (achievement.PreconditionIds == null || achievement.PreconditionIds.Count == 0)
                return prerequisites;

            foreach (var preconditionId in achievement.PreconditionIds)
            {
                IAchievement prerequisite = null;

                foreach (var a in AllAchievements)
                {
                    if (a.Id == preconditionId)
                    {
                        prerequisite = a;
                        break;
                    }
                }

                if (prerequisite == null)
                {
                    foreach (var a in RepeatAchievements)
                    {
                        if (a.Id == preconditionId)
                        {
                            prerequisite = a;
                            break;
                        }
                    }
                }

                if (prerequisite != null)
                {
                    prerequisites.Add(prerequisite);
                }
                else
                {
                    Debug.LogWarning($"Prerequisite achievement with ID '{preconditionId}' not found for achievement '{achievement.Name}'");
                }
            }

            return prerequisites;
        }

        public List<string> GetIncompletePrerequisiteNames(IAchievement achievement)
        {
            var incompleteNames = new List<string>();
            var prerequisites = GetPrerequisiteAchievements(achievement);

            foreach (var prerequisite in prerequisites)
            {
                if (!IsAchievementCompleted(prerequisite))
                {
                    incompleteNames.Add(prerequisite.Name);
                }
            }

            return incompleteNames;
        }

        #endregion

        #region Helpers

        private List<IAchievement> GetAllAchievements(string category)
        {
            var achievements = _achievementsSystem.GetAchievements(category);
            var repeatedAchievements = _achievementsSystem.GetRepeatAchievements(category);
            var result = new List<IAchievement>();
            result.AddRange(achievements);
            result.AddRange(repeatedAchievements);
            return result;
        }

        private List<IAchievement> GetAllAchievements()
        {
            var achievements = _achievementsSystem.GetAchievements();
            var repeatedAchievements = _achievementsSystem.GetRepeatAchievements();
            var result = new List<IAchievement>();
            result.AddRange(achievements);
            result.AddRange(repeatedAchievements);
            return result;
        }

        #endregion
    }
}
