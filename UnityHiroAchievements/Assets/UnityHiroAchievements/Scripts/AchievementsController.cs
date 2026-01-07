using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Hiro;
using Hiro.Unity;
using Nakama;
using UnityEngine;
using UnityEngine.UIElements;

namespace HiroAchievements
{
    [System.Serializable]
    public class AchievementIconMapping
    {
        public string achievementId;
        public Sprite icon;
    }

    [RequireComponent(typeof(UIDocument))]
    public class AchievementsController : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private VisualTreeAsset achievementItemTemplate;
        [SerializeField] private VisualTreeAsset subAchievementItemTemplate;
        [SerializeField] private AchievementIconMapping[] achievementIconMappings;
        [SerializeField] private Sprite defaultIcon;

        private NakamaSystem _nakamaSystem;
        private IAchievementsSystem _achievementsSystem;
        private IEconomySystem _economySystem;
        private IAchievement _selectedAchievement;
        private ISubAchievement _selectedSubAchievement;
        private IAchievement _parentAchievement; // Track parent when sub is selected
        private string _selectedSubAchievementKey; // Track the key in the parent's dictionary

        private AchievementsView _view;

        public string CurrentUserId => _nakamaSystem.UserId;
        public List<IAchievement> AllAchievements { get; } = new();
        public List<IAchievement> RepeatAchievements { get; } = new();
        public Dictionary<string, Sprite> IconDictionary { get; private set; }

        // Tab filtering
        private string _currentCategory = "all";

        public event Action<ISession, AchievementsController> OnInitialized;

        #region Initialization

        private void Start()
        {
            // Build the icon dictionary from the mappings
            IconDictionary = new Dictionary<string, Sprite>();
            if (achievementIconMappings != null)
            {
                foreach (var mapping in achievementIconMappings)
                {
                    if (!string.IsNullOrEmpty(mapping.achievementId) && mapping.icon != null)
                    {
                        IconDictionary[mapping.achievementId] = mapping.icon;
                    }
                }
            }

            var achievementsCoordinator = HiroCoordinator.Instance as HiroAchievementsCoordinator;
            if (achievementsCoordinator == null) return;

            achievementsCoordinator.ReceivedStartError += HandleStartError;
            achievementsCoordinator.ReceivedStartSuccess += HandleStartSuccess;

            _view = new AchievementsView(this, achievementsCoordinator, achievementItemTemplate, subAchievementItemTemplate, defaultIcon);
        }

        private void HandleStartError(Exception e)
        {
            Debug.LogException(e);
            _view.ShowError(e.Message);
        }

        private async void HandleStartSuccess(ISession session)
        {
            // Cache Hiro systems
            _nakamaSystem = this.GetSystem<NakamaSystem>();
            _achievementsSystem = this.GetSystem<AchievementsSystem>();
            _economySystem = this.GetSystem<EconomySystem>();

            _view.StartObservingWallet();
            _currentCategory = "quest";

            await LoadAchievements();
            await _view.RefreshAchievementsList();

            OnInitialized?.Invoke(session, this);
        }

        public void SwitchComplete()
        {
            _view.HideAllModals();
            _ = _view.RefreshAchievementsList();
            _ = _economySystem.RefreshAsync();
        }

        #endregion

        #region Achievement Operations

        public async Task LoadAchievements()
        {
            AllAchievements.Clear();
            RepeatAchievements.Clear();

            await _achievementsSystem.RefreshAsync();

            // Get all achievements (including locked ones)
            var achievements = GetAllAchievements();
            AllAchievements.AddRange(achievements);

            // Get repeatable achievements (including locked ones)
            var repeatAchievements = _achievementsSystem.GetRepeatAchievements();
            RepeatAchievements.AddRange(repeatAchievements);

            Debug.Log($"Loaded {AllAchievements.Count} achievements, {RepeatAchievements.Count} repeatable");
        }

        public async Task<List<IAchievement>> RefreshAchievements()
        {
            await _achievementsSystem.RefreshAsync();
            await _economySystem.RefreshAsync();

            AllAchievements.Clear();
            RepeatAchievements.Clear();

            // Get all achievements (including locked)
            Debug.Log(_currentCategory);
            var achievements = _achievementsSystem.GetAchievements(_currentCategory);
            AllAchievements.AddRange(achievements);

            // Get all repeatable achievements (including locked)
            var repeatAchievements = _achievementsSystem.GetRepeatAchievements(_currentCategory);
            RepeatAchievements.AddRange(repeatAchievements);

            return GetFilteredAchievements();
        }

        public List<IAchievement> GetFilteredAchievements()
        {
          Debug.Log(_currentCategory);
          var result = GetAllAchievements(_currentCategory);
          Debug.Log(result.Count());
          return result.ToList();
        }

        public void SetCurrentCategory(string category)
        {
            _currentCategory = category;
        }

        public string GetCurrentCategory()
        {
            return _currentCategory;
        }

        public void SelectAchievement(IAchievement achievement)
        {
            _selectedAchievement = achievement;
            _selectedSubAchievement = null; // Clear sub-achievement when main achievement is selected
            Debug.Log($"Selected achievement: {achievement?.Name ?? "None"}");
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
            Debug.Log($"✓ SelectSubAchievement CALLED - Selected sub-achievement: {subAchievement?.Name ?? "None"} (ID: {subAchievement?.Id ?? "null"}, Key: {subAchievementKey})");
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

        #region Achievement Actions

        public async Task UpdateAchievementProgress(string achievementId, long progress)
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
            await RefreshAchievements();
        }

        public async Task UpdateAchievementProgress(long progress)
        {
            // If a sub-achievement is selected, update that
            if (_selectedSubAchievement != null)
            {
                Debug.Log($"Updating SUB-ACHIEVEMENT: {_selectedSubAchievement.Name} (ID: {_selectedSubAchievement.Id})");
                await UpdateAchievementProgress(_selectedSubAchievement.Id, progress);
                
                // After updating sub-achievement, check if all sub-achievements are complete
                if (_parentAchievement != null)
                {
                    await CheckAndProgressParentAchievement(_parentAchievement);
                }
                return;
            }

            // Otherwise update the main achievement
            if (_selectedAchievement == null)
                throw new Exception("No achievement selected.");

            Debug.Log($"Updating MAIN ACHIEVEMENT: {_selectedAchievement.Name} (ID: {_selectedAchievement.Id})");
            await UpdateAchievementProgress(_selectedAchievement.Id, progress);
        }

        private async Task CheckAndProgressParentAchievement(IAchievement parentAchievement)
        {
            if (!AchievementProgressHelper.HasSubAchievements(parentAchievement))
                return;

            // Refresh to get latest sub-achievement data
            await RefreshAchievements();
            
            // Re-fetch the parent achievement to get updated sub-achievement counts
            var updatedParent = AllAchievements.FirstOrDefault(a => a.Id == parentAchievement.Id);
            if (updatedParent == null)
                return;

            // Check if all sub-achievements are completed
            bool allCompleted = AchievementProgressHelper.AreAllSubAchievementsCompleted(updatedParent);

            // If all sub-achievements are complete and parent count is 0, progress parent by 1
            if (allCompleted && updatedParent.Count == 0)
            {
                Debug.Log($"✓ All sub-achievements completed! Progressing parent achievement: {updatedParent.Name} (ID: {updatedParent.Id})");
                await UpdateAchievementProgress(updatedParent.Id, 1);
            }
        }

        public async Task ClaimAchievementReward(string achievementId, bool claimTotal = true)
        {
            if (string.IsNullOrEmpty(achievementId))
                throw new Exception("Invalid achievement ID.");

            await _achievementsSystem.ClaimAchievementsAsync(new[] { achievementId }, claimTotal);

            // Refresh economy after claiming rewards
            await _economySystem.RefreshAsync();
        }

        public async Task ClaimAchievementReward(bool claimTotal = true)
        {
            // If a sub-achievement is selected, claim that
            if (_selectedSubAchievement != null)
            {
                await ClaimAchievementReward(_selectedSubAchievement.Id, claimTotal);
                return;
            }

            // Otherwise claim the main achievement
            if (_selectedAchievement == null)
                throw new Exception("No achievement selected.");

            await ClaimAchievementReward(_selectedAchievement.Id, claimTotal);
        }

        public bool CanClaimReward(IAchievement achievement)
        {
            // Check if achievement has sub-achievements
            if (AchievementProgressHelper.HasSubAchievements(achievement))
            {
                // For achievements with sub-achievements, the parent count will be 1 when all subs are complete
                // This is because we auto-progress the parent when all sub-achievements finish
                return (achievement.HasAvailableReward() || achievement.HasAvailableTotalReward()) && !achievement.IsClaimed() && achievement.Count >= 1;
            }
            else
            {
                // For normal achievements without sub-achievements, use count/maxCount
                return (achievement.HasAvailableReward() || achievement.HasAvailableTotalReward()) && !achievement.IsClaimed() && achievement.Count >= achievement.MaxCount;
            }
        }

        public bool CanClaimReward(ISubAchievement subAchievement)
        {
            // Can claim if sub-achievement has reward and is completed
            return subAchievement.HasAvailableReward() && !(subAchievement.ClaimTimeSec > 0) && subAchievement.Count >= subAchievement.MaxCount;
        }

        public bool IsAchievementCompleted(IAchievement achievement)
        {
            // Check if achievement has sub-achievements
            if (AchievementProgressHelper.HasSubAchievements(achievement))
            {
                // For achievements with sub-achievements, check if parent count is 1 and claimed
                // Parent count will be 1 when all sub-achievements are complete
                return (achievement.HasAvailableReward() || achievement.HasAvailableTotalReward()) && achievement.IsClaimed() && achievement.Count >= 1;
            }
            else
            {
                // For normal achievements
                return (achievement.HasAvailableReward() || achievement.HasAvailableTotalReward()) && achievement.IsClaimed();
            }
        }

        public bool IsAchievementCompleted(ISubAchievement subAchievement)
        {
            return subAchievement.HasAvailableReward() && subAchievement.ClaimTimeSec > 0;
        }

        public bool IsAchievementClaimable(IAchievement achievement)
        {
            return (achievement.HasAvailableReward() || achievement.HasAvailableTotalReward()) && !achievement.IsClaimed() && achievement.Count >= achievement.MaxCount;
        }

        public bool IsAchievementClaimable(ISubAchievement subAchievement)
        {
            return subAchievement.HasAvailableReward() && subAchievement.ClaimTimeSec <= 0 && subAchievement.Count >= subAchievement.MaxCount;
        }

        public bool IsAchievementLocked(IAchievement achievement)
        {
            // Get available achievements (those that are unlocked)
            var availableAchievements = _achievementsSystem.GetAvailableAchievements();
            var availableRepeatAchievements = _achievementsSystem.GetAvailableRepeatAchievements();
            
            // Check if this achievement is in the available list
            bool isAvailable = availableAchievements.Any(a => a.Id == achievement.Id) || 
                               availableRepeatAchievements.Any(a => a.Id == achievement.Id);
            
            // If not in available list, it's locked
            return !isAvailable;
        }

        /// <summary>
        /// Gets the prerequisite achievements for a locked achievement.
        /// Returns empty list if achievement has no preconditions or if achievement is not locked.
        /// </summary>
        public List<IAchievement> GetPrerequisiteAchievements(IAchievement achievement)
        {
            var prerequisites = new List<IAchievement>();

            // Check if achievement has preconditions
            if (achievement.PreconditionIds == null || achievement.PreconditionIds.Count == 0)
                return prerequisites;

            // Find each prerequisite achievement in our lists
            foreach (var preconditionId in achievement.PreconditionIds)
            {
                // Search in both normal and repeatable achievements
                var prerequisite = AllAchievements.FirstOrDefault(a => a.Id == preconditionId) ??
                                   RepeatAchievements.FirstOrDefault(a => a.Id == preconditionId);

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

        /// <summary>
        /// Gets the names of incomplete prerequisite achievements.
        /// Returns empty list if all prerequisites are completed.
        /// </summary>
        public List<string> GetIncompletePrerequisiteNames(IAchievement achievement)
        {
            var incompleteNames = new List<string>();
            var prerequisites = GetPrerequisiteAchievements(achievement);

            foreach (var prerequisite in prerequisites)
            {
                // Check if prerequisite is NOT completed
                if (!IsAchievementCompleted(prerequisite))
                {
                    incompleteNames.Add(prerequisite.Name);
                }
            }

            return incompleteNames;
        }

        #endregion

        #region Helpers

        public String GetResetTime()
        {
            foreach(var achievement in GetAllAchievements(_currentCategory))
            {
                if(achievement.ResetTimeSec > 0)
                {
                    TimeSpan time = TimeSpan.FromSeconds(achievement.ResetTimeSec - achievement.CurrentTimeSec);
                    return string.Format("{0:D2}:{1:D2}:{2:D2}", 
                         time.Hours, 
                         time.Minutes, 
                         time.Seconds);
                }
            }
            return "";
        }

        List<IAchievement> GetAllAchievements(string category)
        {
            var achievements = _achievementsSystem.GetAchievements(category);
            var repeatedAchievements = _achievementsSystem.GetRepeatAchievements(category);
            List<IAchievement> result = new List<IAchievement>();
            result.AddRange(achievements);
            result.AddRange(repeatedAchievements);

            return result;
        }

        List<IAchievement> GetAllAchievements()
        {
            var achievements = _achievementsSystem.GetAchievements();
            var repeatedAchievements = _achievementsSystem.GetRepeatAchievements();
            List<IAchievement> result = new List<IAchievement>();
            result.AddRange(achievements);
            result.AddRange(repeatedAchievements);

            return result;
        }

        #endregion
    }
}