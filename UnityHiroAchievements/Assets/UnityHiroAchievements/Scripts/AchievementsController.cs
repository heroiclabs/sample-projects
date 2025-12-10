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

            _view = new AchievementsView(this, achievementsCoordinator, achievementItemTemplate, defaultIcon);
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

            // Get all achievements
            var achievements = _achievementsSystem.GetAchievements();
            AllAchievements.AddRange(achievements);

            // Get repeatable achievements
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

            var achievements = _achievementsSystem.GetAchievements();
            AllAchievements.AddRange(achievements);

            var repeatAchievements = _achievementsSystem.GetRepeatAchievements();
            RepeatAchievements.AddRange(repeatAchievements);

            return GetFilteredAchievements();
        }

        public List<IAchievement> GetFilteredAchievements()
        {
          var result = _achievementsSystem.GetAchievements();
          Debug.Log(result.Count());
          return _achievementsSystem.GetAchievements().ToList();
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
                return;
            }

            // Otherwise update the main achievement
            if (_selectedAchievement == null)
                throw new Exception("No achievement selected.");

            Debug.Log($"Updating MAIN ACHIEVEMENT: {_selectedAchievement.Name} (ID: {_selectedAchievement.Id})");
            await UpdateAchievementProgress(_selectedAchievement.Id, progress);
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
            // Can claim if achievement has reward and is completed
            return (achievement.HasAvailableReward() || achievement.HasAvailableTotalReward()) && !achievement.IsClaimed() && achievement.Count >= achievement.MaxCount;
        }

        public bool CanClaimReward(ISubAchievement subAchievement)
        {
            // Can claim if sub-achievement has reward and is completed
            return subAchievement.HasAvailableReward() && !(subAchievement.ClaimTimeSec > 0) && subAchievement.Count >= subAchievement.MaxCount;
        }

        public bool IsAchievementCompleted(IAchievement achievement)
        {
            return (achievement.HasAvailableReward() || achievement.HasAvailableTotalReward()) && achievement.IsClaimed();
        }

        public bool IsAchievementCompleted(ISubAchievement subAchievement)
        {
            return subAchievement.HasAvailableReward() && subAchievement.ClaimTimeSec > 0;
        }

        public bool IsAchievementLocked(IAchievement achievement)
        {
            // Check if achievement has preconditions that aren't met
            // You can customize this logic based on your achievement system
            return false;
        }

        #endregion

        #region Category Helpers

        public List<string> GetAllCategories()
        {
            var categories = new HashSet<string> { "all", "dailies", "quests" };

            foreach (var achievement in AllAchievements)
            {
                if (!string.IsNullOrEmpty(achievement.Category))
                {
                    categories.Add(achievement.Category);
                }
            }

            return categories.ToList();
        }

        #endregion
    }
}