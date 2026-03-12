using UnityEngine;
using System;
using UnityEngine.UIElements;
using System.Threading;
using System.Threading.Tasks;

namespace HiroAchievements
{
    public class CountdownTimer
    {
        private float _timeRemaining;
        private readonly Label _resetTimeLabel;
        private readonly Action _onComplete;

        private CancellationTokenSource _cts;

        public CountdownTimer(Label resetTimeLabel, Action onComplete)
        {
            _resetTimeLabel = resetTimeLabel;
            _onComplete = onComplete;
            _resetTimeLabel.text = "";
        }

        public async void Start(float timeRemaining)
        {
            Stop();

            // Don't start if no valid reset time
            if (timeRemaining <= 0)
            {
                _resetTimeLabel.text = "";
                return;
            }

            _cts = new CancellationTokenSource();
            var token = _cts.Token;

            _timeRemaining = timeRemaining;

            try
            {
                while (_timeRemaining > 0)
                {
                    if (token.IsCancellationRequested) return;

                    UpdateTimerDisplay(_timeRemaining);
                    await Task.Delay(1000, token);

                    _timeRemaining -= 1f;
                }

                Complete();
            }
            catch (OperationCanceledException)
            {
                // Expected when timer is stopped
            }
            catch (Exception e)
            {
                Debug.LogException(e);
            }
        }

        public void Stop()
        {
            if (_cts != null)
            {
                _cts.Cancel();
                _cts.Dispose();
                _cts = null;
            }

            _resetTimeLabel.text = "";
        }

        private void Complete()
        {
            Stop();
            _onComplete?.Invoke();
        }

        private void UpdateTimerDisplay(float seconds)
        {
            float displaySeconds = Math.Max(0, seconds);
            TimeSpan time = TimeSpan.FromSeconds(displaySeconds);
            _resetTimeLabel.text = string.Format("Dailies reset in: {0:D2}:{1:D2}:{2:D2}",
                time.Hours, time.Minutes, time.Seconds);
        }
    }
}
