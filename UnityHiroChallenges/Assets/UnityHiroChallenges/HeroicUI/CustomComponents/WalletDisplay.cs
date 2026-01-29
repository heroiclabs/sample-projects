using System;
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
        private const string CoinsId = "coins";
        private const string GemsId = "gems";
        private const float CurrencyLerpTime = 0.6f;
        private const float FrameDelaySeconds = 0.016f; // ~60fps fixed frame time

        private readonly Label _coinsLabel;
        private readonly Label _gemsLabel;

        private CancellationTokenSource _gemsCanceller;
        private CancellationTokenSource _coinsCanceller;
        private IDisposable _economyDisposer;

        public WalletDisplay(VisualElement topElement)
        {
            _coinsLabel = topElement.Q<Label>("coins");
            _gemsLabel = topElement.Q<Label>("gems");
        }

        public void StartObserving()
        {
            var economySystem = HiroCoordinator.Instance.Systems.GetSystem<EconomySystem>();
            _economyDisposer = SystemObserver<EconomySystem>.Create(economySystem, OnEconomyUpdated);
        }

        private async void OnEconomyUpdated(EconomySystem system)
        {
            try
            {
                await HandleWalletUpdatedAsync(system);
            }
            catch (OperationCanceledException)
            {
                // Expected when cancellation is requested
            }
            catch (Exception e)
            {
                Debug.LogException(e);
            }
        }

        private async Task HandleWalletUpdatedAsync(EconomySystem system)
        {
            float.TryParse(_coinsLabel.text, NumberStyles.Number, CultureInfo.InvariantCulture, out var startCoinsValue);
            system.Wallet.TryGetValue(CoinsId, out var endCoinsValue);

            _coinsCanceller?.Cancel();
            _coinsCanceller?.Dispose();
            _coinsCanceller = new CancellationTokenSource();

            float.TryParse(_gemsLabel.text, NumberStyles.Number, CultureInfo.InvariantCulture, out var startGemsValue);
            system.Wallet.TryGetValue(GemsId, out var endGemsValue);

            _gemsCanceller?.Cancel();
            _gemsCanceller?.Dispose();
            _gemsCanceller = new CancellationTokenSource();

            await Task.WhenAll(
                LerpRoutine(_coinsLabel, startCoinsValue, endCoinsValue, _coinsCanceller.Token),
                LerpRoutine(_gemsLabel, startGemsValue, endGemsValue, _gemsCanceller.Token));
        }

        private static async Task LerpRoutine(Label label, float startValue, float endValue, CancellationToken ct)
        {
            var t = 0f;

            while (Mathf.Abs(endValue - Mathf.Lerp(startValue, endValue, t)) > 0.05f)
            {
                ct.ThrowIfCancellationRequested();

                t += FrameDelaySeconds / CurrencyLerpTime;
                var lerpValue = Mathf.Lerp(startValue, endValue, t);
                label.text = lerpValue.ToString("0");

                await Task.Delay(TimeSpan.FromSeconds(FrameDelaySeconds), ct);
            }

            label.text = endValue.ToString("0", CultureInfo.InvariantCulture);
        }

        public void Dispose()
        {
            _coinsCanceller?.Cancel();
            _coinsCanceller?.Dispose();
            _gemsCanceller?.Cancel();
            _gemsCanceller?.Dispose();
            _economyDisposer?.Dispose();
        }
    }
}
