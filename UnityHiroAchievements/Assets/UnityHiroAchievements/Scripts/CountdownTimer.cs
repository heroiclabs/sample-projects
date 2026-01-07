using UnityEngine;
using System;
using UnityEngine.UIElements;
using System.Threading; // Required for CancellationToken
using System.Threading.Tasks;

namespace HiroAchievements
{
    public class CountdownTimer
    {
        private float _timeRemaining = 0f;
        private Label _resetTimeLabel;
        private AchievementsView _achievementsView;
        
        private CancellationTokenSource _cts;

        public CountdownTimer(AchievementsView achievementsView, Label resetTimeLabel)
        {
            _resetTimeLabel = resetTimeLabel;
            _achievementsView = achievementsView;
            _resetTimeLabel.text = "";
        }

        public async void start(float timeRemaining)
        {
            stop();

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
                Debug.Log("Timer task was cancelled.");
            }
            catch (Exception e)
            {
                Debug.LogError($"Timer Error: {e.Message}");
            }
        }

        public void stop()
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
            stop();
            _ = _achievementsView.RefreshAchievementsList();
            Debug.Log("Timer Reached Zero - Refreshing List");
        }

        void UpdateTimerDisplay(float seconds)
        {
            float displaySeconds = Math.Max(0, seconds);
            TimeSpan time = TimeSpan.FromSeconds(displaySeconds);
            _resetTimeLabel.text = string.Format("{0:D2}:{1:D2}:{2:D2}", 
                time.Hours, time.Minutes, time.Seconds);
        }
    }
}