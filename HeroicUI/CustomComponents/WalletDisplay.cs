using System;
using System.Collections.Generic;
using System.Globalization;
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
        private Label coinsLabel;
        private Label gemsLabel;

        private CancellationTokenSource gemsCanceller;
        private CancellationTokenSource coinsCanceller;

        private IDisposable economyDisposer;

        private const string CoinsId = "coins";
        private const string GemsId = "gems";
        private const float CurrencyLerpTime = 0.6f;

        public WalletDisplay(VisualElement topElement)
        {
            InitializeUI(topElement);
        }

        private void InitializeUI(VisualElement topElement)
        {
            coinsLabel = topElement.Q<Label>("coins");
            gemsLabel = topElement.Q<Label>("gems");
        }

        public void StartObserving()
        {
            var economySystem = HiroCoordinator.Instance.Systems.GetSystem<EconomySystem>();
            economyDisposer = SystemObserver<EconomySystem>.Create(economySystem, OnEconomyUpdated);
            economySystem.RefreshAsync();
        }

        private void OnEconomyUpdated(EconomySystem system)
        {
            _ = HandleWalletUpdatedAsync(system);
        }

        private async Task HandleWalletUpdatedAsync(EconomySystem system)
        {
            var startCoinsValue = float.Parse(coinsLabel.text);
            float endCoinsValue = system.Wallet.GetValueOrDefault(CoinsId, 0);

            coinsCanceller?.Cancel();
            coinsCanceller = new CancellationTokenSource();

            var startGemsValue = float.Parse(gemsLabel.text);
            float endGemsValue = system.Wallet.GetValueOrDefault(CoinsId, 0);
            if (system.Wallet.TryGetValue(GemsId, out var gemsValue))
            {
                endCoinsValue = gemsValue;
            }

            gemsCanceller?.Cancel();
            gemsCanceller = new CancellationTokenSource(); 
            
            await Task.WhenAll(LerpRoutine(coinsLabel, startCoinsValue, endCoinsValue, coinsCanceller.Token),
                LerpRoutine(gemsLabel, startGemsValue, endGemsValue, gemsCanceller.Token));
        }

        private static async Task LerpRoutine(Label label, float startValue, float endValue, CancellationToken ct)
        {
            var lerpValue = startValue;
            var t = 0f;
            label.text = string.Empty;

            while (Mathf.Abs(endValue - lerpValue) > 0.05f)
            {
                if (ct.IsCancellationRequested)
                {
                    return;
                }

                t += Time.deltaTime / CurrencyLerpTime;

                lerpValue = Mathf.Lerp(startValue, endValue, t);
                label.text = lerpValue.ToString("0");
                await Task.Delay(TimeSpan.FromSeconds(Time.deltaTime), ct);
            }

            label.text = endValue.ToString(CultureInfo.InvariantCulture);
        }

        public void Dispose()
        {
            economyDisposer?.Dispose();
        }
    }
}
