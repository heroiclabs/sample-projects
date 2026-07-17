using System;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Hiro;
using Hiro.System;
using Hiro.Unity;
using UnityEngine;
using UnityEngine.UIElements;

namespace HeroicUI
{
    public class WalletDisplay : IDisposable
    {
        private Label _coinsLabel;
        private Label _gemsLabel;
        private Label _xpLevelLabel;
        private Label _xpProgressText;
        private VisualElement _xpBarFill;

        private CancellationTokenSource _coinsCanceller;
        private CancellationTokenSource _gemsCanceller;

        private IDisposable _economyDisposer;
        private IDisposable _achievementsDisposer;

        private const string CoinsId = "coins";
        private const string GemsId = "gems";
        private const string XPId = "xp";
        private const string PlayerLevelsId = "player_levels";
        private const float CurrencyLerpTime = 0.6f;

        public WalletDisplay(VisualElement topElement)
        {
            _coinsLabel = topElement.Q<Label>("coins");
            _gemsLabel = topElement.Q<Label>("gems");
            _xpLevelLabel = topElement.Q<Label>("xp-level");
            _xpProgressText = topElement.Q<Label>("xp-progress-text");
            _xpBarFill = topElement.Q<VisualElement>("xp-bar-fill");
        }

        public void StartObserving()
        {
            var economySystem = HiroCoordinator.Instance.Systems.GetSystem<EconomySystem>();
            _economyDisposer = SystemObserver<EconomySystem>.Create(economySystem, OnEconomyUpdated);
            economySystem.RefreshAsync();

            var achievementsSystem = HiroCoordinator.Instance.Systems.GetSystem<AchievementsSystem>();
            _achievementsDisposer = SystemObserver<AchievementsSystem>.Create(achievementsSystem, OnAchievementsUpdated);
        }

        private void OnEconomyUpdated(EconomySystem system)
        {
            _ = HandleWalletUpdatedAsync(system);
            UpdateXPBar(system);
        }

        private void OnAchievementsUpdated(AchievementsSystem system)
        {
            var economySystem = HiroCoordinator.Instance.Systems.GetSystem<EconomySystem>();
            UpdateXPBar(economySystem);
        }

        private async Task HandleWalletUpdatedAsync(EconomySystem system)
        {
            float startCoins = float.Parse(_coinsLabel?.text ?? "0");
            system.Wallet.TryGetValue(CoinsId, out long coinsValue);
            float endCoins = coinsValue;

            _coinsCanceller?.Cancel();
            _coinsCanceller = new CancellationTokenSource();

            float startGems = float.Parse(_gemsLabel?.text ?? "0");
            system.Wallet.TryGetValue(GemsId, out long gemsValue);
            float endGems = gemsValue;

            _gemsCanceller?.Cancel();
            _gemsCanceller = new CancellationTokenSource();

            await Task.WhenAll(
                LerpRoutine(_coinsLabel, startCoins, endCoins, _coinsCanceller.Token),
                LerpRoutine(_gemsLabel, startGems, endGems, _gemsCanceller.Token));
        }

        private void UpdateXPBar(EconomySystem economySystem)
        {
            if (_xpBarFill == null) return;

            var achievementsSystem = HiroCoordinator.Instance.Systems.GetSystem<AchievementsSystem>();
            var playerLevels = achievementsSystem
                .GetAchievements("progression")
                .FirstOrDefault(a => a.Id == PlayerLevelsId);

            int currentLevel = 0;
            long xpCurrent = 0;
            long xpMax = 0;

            if (playerLevels?.SubAchievements != null)
            {
                var ordered = playerLevels.SubAchievements
                    .OrderBy(kv => ExtractLevelNumber(kv.Key))
                    .ToList();

                foreach (var kv in ordered)
                {
                    if (kv.Value.ClaimTimeSec > 0)
                        currentLevel = ExtractLevelNumber(kv.Key);
                }

                // Find the active (first unclaimed) level for the progress bar.
                var active = ordered.FirstOrDefault(kv => kv.Value.ClaimTimeSec <= 0);
                if (active.Value != null)
                {
                    xpCurrent = active.Value.Count;
                    xpMax = active.Value.MaxCount;
                }
                else if (ordered.Count > 0)
                {
                    // All levels complete: show the last level as full.
                    var last = ordered[ordered.Count - 1];
                    xpCurrent = last.Value.MaxCount;
                    xpMax = last.Value.MaxCount;
                }
            }

            if (_xpLevelLabel != null)
                _xpLevelLabel.text = currentLevel.ToString();

            if (_xpProgressText != null)
                _xpProgressText.text = xpMax > 0 ? $"{xpCurrent} / {xpMax}" : "MAX";

            float pct = xpMax > 0 ? Mathf.Clamp01((float)xpCurrent / xpMax) * 100f : 100f;
            _xpBarFill.style.width = Length.Percent(pct);
        }

        private static async Task LerpRoutine(Label label, float startValue, float endValue, CancellationToken ct)
        {
            if (label == null) return;

            var lerpValue = startValue;
            var t = 0f;
            label.text = string.Empty;

            while (Mathf.Abs(endValue - lerpValue) > 0.05f)
            {
                if (ct.IsCancellationRequested) return;

                t += Time.deltaTime / CurrencyLerpTime;
                lerpValue = Mathf.Lerp(startValue, endValue, t);
                label.text = lerpValue.ToString("0");
                await Task.Delay(TimeSpan.FromSeconds(Time.deltaTime), ct);
            }

            label.text = endValue.ToString(CultureInfo.InvariantCulture);
        }

        private static int ExtractLevelNumber(string key)
        {
            if (int.TryParse(key.Replace("level_", ""), out int num))
                return num;
            return 0;
        }

        public void Dispose()
        {
            _economyDisposer?.Dispose();
            _achievementsDisposer?.Dispose();
        }
    }
}
