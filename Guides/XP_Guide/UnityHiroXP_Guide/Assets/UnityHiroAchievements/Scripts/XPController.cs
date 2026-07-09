using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Hiro;
using Nakama;

namespace XPGuide
{
    public class XPController
    {
        private readonly NakamaSystem _nakamaSystem;
        private readonly IAchievementsSystem _achievementsSystem;
        private readonly IEconomySystem _economySystem;
        private readonly IClient _client;
        private readonly ISession _session;

        private ISubAchievement _selectedLevel;
        private string _selectedLevelKey;

        public string CurrentUserId => _nakamaSystem.UserId;

        public XPController(
            NakamaSystem nakamaSystem,
            IAchievementsSystem achievementsSystem,
            IEconomySystem economySystem,
            IClient client,
            ISession session)
        {
            _nakamaSystem = nakamaSystem ?? throw new ArgumentNullException(nameof(nakamaSystem));
            _achievementsSystem = achievementsSystem ?? throw new ArgumentNullException(nameof(achievementsSystem));
            _economySystem = economySystem ?? throw new ArgumentNullException(nameof(economySystem));
            _client = client ?? throw new ArgumentNullException(nameof(client));
            _session = session ?? throw new ArgumentNullException(nameof(session));
        }

        public async Task LoadAsync()
        {
            await _achievementsSystem.RefreshAsync();
            await _economySystem.RefreshAsync();
        }

        public async Task RefreshAsync()
        {
            await _achievementsSystem.RefreshAsync();
            await _economySystem.RefreshAsync();
        }

        // Calls the server RPC that atomically grants XP currency and advances all level sub-achievements.
        public async Task GrantXPAsync(long amount)
        {
            if (amount <= 0)
                throw new ArgumentException("XP amount must be greater than 0.");

            var payload = $"{{\"amount\":{amount}}}";
            await _client.RpcAsync(_session, "rpc_grant_xp", payload);
            await RefreshAsync();
        }

        public IAchievement GetPlayerLevels()
        {
            return _achievementsSystem.GetAchievements("progression")
                .FirstOrDefault(a => a.Id == "player_levels");
        }

        // Returns level sub-achievements ordered by their level number.
        public List<(string key, ISubAchievement sub)> GetOrderedLevels()
        {
            var playerLevels = GetPlayerLevels();
            if (playerLevels?.SubAchievements == null)
                return new List<(string, ISubAchievement)>();

            return playerLevels.SubAchievements
                .OrderBy(kv => ExtractLevelNumber(kv.Key))
                .Select(kv => (kv.Key, kv.Value))
                .ToList();
        }

        public long GetCurrentXP()
        {
            return _economySystem.Wallet.TryGetValue("xp", out var xp) ? xp : 0;
        }

        public long GetCoins()
        {
            return _economySystem.Wallet.TryGetValue("coins", out var coins) ? coins : 0;
        }

        public long GetGems()
        {
            return _economySystem.Wallet.TryGetValue("gems", out var gems) ? gems : 0;
        }

        // Returns the highest level number whose sub-achievement has been claimed.
        public int GetCurrentLevel()
        {
            int level = 0;
            foreach (var (key, sub) in GetOrderedLevels())
            {
                if (sub.ClaimTimeSec > 0)
                {
                    int num = ExtractLevelNumber(key);
                    if (num > level) level = num;
                }
            }
            return level;
        }

        // Returns the sub-achievement for the next unclaimed level, or null if all levels are completed.
        public ISubAchievement GetCurrentLevelSub()
        {
            foreach (var (_, sub) in GetOrderedLevels())
            {
                if (sub.ClaimTimeSec <= 0)
                    return sub;
            }
            return null;
        }

        // Returns (xpIntoLevel, xpRequiredForLevel) for the next unclaimed level.
        // Returns (currentXP, currentXP) when all levels are completed.
        public (long current, long max) GetProgressToNextLevel()
        {
            foreach (var (_, sub) in GetOrderedLevels())
            {
                if (sub.ClaimTimeSec <= 0)
                    return (sub.Count, sub.MaxCount);
            }
            var xp = GetCurrentXP();
            return (xp, xp);
        }

        public void SelectLevel(string key, ISubAchievement sub)
        {
            _selectedLevelKey = key;
            _selectedLevel = sub;
        }

        public void ClearSelection()
        {
            _selectedLevelKey = null;
            _selectedLevel = null;
        }

        public ISubAchievement GetSelectedLevel() => _selectedLevel;
        public string GetSelectedLevelKey() => _selectedLevelKey;

        public List<(string key, ISubAchievement sub)> GetOrderedQuests()
        {
            var questAchievement = _achievementsSystem.GetAchievements("quests")
                .FirstOrDefault(a => a.Id == "defeat_thunder_world");
            if (questAchievement?.SubAchievements == null)
                return new List<(string, ISubAchievement)>();

            return questAchievement.SubAchievements
                .Select(kv => (kv.Key, kv.Value))
                .ToList();
        }

        public async Task CompleteSubQuestAsync(string subQuestId)
        {
            await _achievementsSystem.UpdateAchievementsAsync(new Dictionary<string, long> { { subQuestId, 1 } });
            await RefreshAsync();
        }

        public async Task ClaimSubQuestAsync(string subQuestId)
        {
            await _achievementsSystem.ClaimAchievementsAsync(new[] { subQuestId });
            await RefreshAsync();
        }

        public bool IsQuestCompleted(string questId)
        {
            return _achievementsSystem.GetAchievements("quests")
                .FirstOrDefault(a => a.Id == questId)
                ?.IsClaimed() == true;
        }

        public async Task UpdateQuestAsync(string questId, long progress)
        {
            await _achievementsSystem.UpdateAchievementsAsync(new Dictionary<string, long> { { questId, progress } });
            await RefreshAsync();
        }

        public async Task ResetAsync()
        {
            await _client.RpcAsync(_session, "rpc_reset_data", "{}");
            await RefreshAsync();
            await _economySystem.RefreshStoreAsync();
        }

        // Returns the end timestamp (unix seconds) of the first active XP reward modifier,
        // or 0 if no unexpired XP booster is present.
        public long GetActiveXPBoosterEndTimeSec()
        {
            long now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            foreach (var mod in _economySystem.RewardModifiers)
            {
                if (mod.Id == "xp" && mod.EndTimeSec > now)
                    return mod.EndTimeSec;
            }
            return 0;
        }

        public async Task SwitchCompleteAsync()
        {
            ClearSelection();
            await RefreshAsync();
        }

        private static int ExtractLevelNumber(string key)
        {
            if (int.TryParse(key.Replace("level_", ""), out int num))
                return num;
            return 0;
        }
    }
}
