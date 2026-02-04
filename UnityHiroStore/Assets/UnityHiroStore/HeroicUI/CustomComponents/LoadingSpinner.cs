using System;
using UnityEngine.UIElements;

namespace HeroicUI
{
    public class LoadingSpinner : IDisposable
    {
        private readonly VisualElement _spinner;
        private IVisualElementScheduledItem _animation;
        private float _currentAngle;

        private const float DegreesPerFrame = 6f;
        private const long FrameIntervalMs = 16; // ~60fps

        public LoadingSpinner(VisualElement spinner)
        {
            _spinner = spinner;
            Hide();
        }

        public void Show()
        {
            _spinner.style.display = DisplayStyle.Flex;
            StartSpinning();
        }

        public void Hide()
        {
            StopSpinning();
            _spinner.style.display = DisplayStyle.None;
        }

        private void StartSpinning()
        {
            _animation?.Pause();
            _currentAngle = 0f;

            _animation = _spinner.schedule.Execute(() =>
            {
                _currentAngle = (_currentAngle + DegreesPerFrame) % 360f;
                _spinner.style.rotate = new Rotate(Angle.Degrees(_currentAngle));
            }).Every(FrameIntervalMs);
        }

        private void StopSpinning()
        {
            _animation?.Pause();
            _animation = null;
        }

        public void Dispose()
        {
            StopSpinning();
        }
    }
}
