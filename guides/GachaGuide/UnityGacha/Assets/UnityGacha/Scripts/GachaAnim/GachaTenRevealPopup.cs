using System;
using System.Collections.Generic;
using System.Linq;
using Hiro;
using UnityEngine;
using UnityEngine.UIElements;

namespace UnityGacha.GachaAnim
{
    public sealed class GachaTenRevealPopup : IDisposable
    {
        #region Constants

        // Shake animation
        private const long  ShakeIntervalMs = 60;
        private const long  ShakeDurationMs = 1000;
        private const float ShakeMagnitude  = 12f;

        // Fade timings
        private const long FadeOutDurationMs      = 200;
        private const long GridFadeInDurationMs   = 400;
        private const long GridFadeInDelayMs      = 100;
        private const long ButtonFadeInDurationMs = 200;
        private const long ButtonFadeInDelayMs    = 1000;
        private const long FadeIntervalMs         = 16;

        // Particles
        private const int   ParticleCount        = 24;
        private const float ParticleBaseRadius   = 60f;
        private const float ParticleRadiusJitter = 40f;
        private const float ParticleAngleJitter  = 7f;
        private const long  ParticleDurationMs   = 400;
        private const int   ParticleSize         = 8;
        private const int   ParticleRadius       = 4;

        #endregion

        public event Action OnContinueClicked;

        private readonly VisualElement _overlay;
        private readonly VisualElement _ticketIcon;
        private readonly VisualElement _particleContainer;
        private readonly VisualElement _grid;
        private readonly Button _continueButton;
        private readonly Dictionary<string, Sprite> _iconDictionary;
        private readonly Sprite _defaultIcon;
        private readonly List<IVisualElementScheduledItem> _activeAnimations = new();
        private readonly EventCallback<ClickEvent> _continueClickHandler;

        private IReadOnlyList<IInventoryItem> _wonItems;

        public GachaTenRevealPopup(
            VisualElement root,
            Dictionary<string, Sprite> iconDictionary,
            Sprite defaultIcon)
        {
            _iconDictionary = iconDictionary;
            _defaultIcon    = defaultIcon;

            _overlay           = root.Q<VisualElement>("gacha-ten-reveal-popup");
            _ticketIcon        = root.Q<VisualElement>("reveal-ten-ticket-icon");
            _particleContainer = root.Q<VisualElement>("reveal-ten-particle-container");
            _grid              = root.Q<VisualElement>("reveal-ten-grid");
            _continueButton    = root.Q<Button>("reveal-ten-continue-button");

            _continueClickHandler = _ =>
            {
                Hide();
                OnContinueClicked?.Invoke();
            };
            _continueButton.RegisterCallback(_continueClickHandler);

            Hide();
        }

        public void Show(IReadOnlyList<IInventoryItem> wonItems, Sprite ticketSprite)
        {
            if (_overlay.style.display == DisplayStyle.Flex) return;

            _wonItems = wonItems;

            SetIcon(_ticketIcon, ticketSprite);

            _ticketIcon.style.opacity     = 1f;
            _ticketIcon.style.display     = DisplayStyle.Flex;
            _grid.style.opacity           = 0f;
            _continueButton.style.display = DisplayStyle.None;
            _continueButton.style.opacity = 0f;

            _grid.Clear();
            _particleContainer.Clear();

            _overlay.style.display = DisplayStyle.Flex;

            PlayShakeAnimation();
        }

        public void Hide()
        {
            PauseAllAnimations();
            _overlay.style.display = DisplayStyle.None;
            _wonItems = null;
        }

        public void Dispose()
        {
            _continueButton?.UnregisterCallback(_continueClickHandler);
            OnContinueClicked = null;
            PauseAllAnimations();
        }

        private void PauseAllAnimations()
        {
            foreach (var a in _activeAnimations)
                a.Pause();
            _activeAnimations.Clear();
        }

        #region Animation

        private void PlayShakeAnimation()
        {
            long elapsed  = 0;
            var  shakeDir = 1f;

            IVisualElementScheduledItem anim = null;
            anim = _ticketIcon.schedule.Execute(() =>
            {
                elapsed += ShakeIntervalMs;
                _ticketIcon.style.translate = new Translate(ShakeMagnitude * shakeDir, 0f);
                shakeDir = -shakeDir;

                if (elapsed < ShakeDurationMs) return;
                anim.Pause();
                _ticketIcon.style.translate = new Translate(0f, 0f);
                StartPostShakePhase();
            }).Every(ShakeIntervalMs);
            _activeAnimations.Add(anim);
        }

        private void StartPostShakePhase()
        {
            ScheduleFade(
                target:     _ticketIcon,
                from:       1f,
                to:         0f,
                durationMs: FadeOutDurationMs,
                onComplete: () => _ticketIcon.style.display = DisplayStyle.None);

            SpawnParticles();

            _activeAnimations.Add(_overlay.schedule.Execute(BuildAndFadeInGrid).StartingIn(GridFadeInDelayMs));
            _activeAnimations.Add(_overlay.schedule.Execute(ShowContinueButton).StartingIn(ButtonFadeInDelayMs));
        }

        private void SpawnParticles()
        {
            var random        = new System.Random();
            var particleColor = GetHighestRarityColor();
            var particles     = new List<(VisualElement el, float dx, float dy)>(ParticleCount);

            for (var i = 0; i < ParticleCount; i++)
            {
                var angleDeg = 360f / ParticleCount * i
                               + (float)(random.NextDouble() * ParticleAngleJitter * 2 - ParticleAngleJitter);
                var rad    = angleDeg * Mathf.Deg2Rad;
                var radius = ParticleBaseRadius + (float)(random.NextDouble() * ParticleRadiusJitter);
                var dx     = Mathf.Cos(rad) * radius;
                var dy     = Mathf.Sin(rad) * radius;

                var p = new VisualElement
                {
                    style =
                    {
                        position                = Position.Absolute,
                        width                   = ParticleSize,
                        height                  = ParticleSize,
                        borderTopLeftRadius     = ParticleRadius,
                        borderTopRightRadius    = ParticleRadius,
                        borderBottomLeftRadius  = ParticleRadius,
                        borderBottomRightRadius = ParticleRadius,
                        backgroundColor         = new StyleColor(particleColor),
                        left                    = Length.Percent(50),
                        top                     = Length.Percent(50),
                        translate               = new Translate(-ParticleRadius, -ParticleRadius),
                        opacity                 = 1f
                    }
                };

                _particleContainer.Add(p);
                particles.Add((p, dx, dy));
            }

            long elapsed = 0;
            IVisualElementScheduledItem anim = null;
            anim = _particleContainer.schedule.Execute(() =>
            {
                elapsed += FadeIntervalMs;
                var t = Mathf.Clamp01((float)elapsed / ParticleDurationMs);

                foreach (var (el, dx, dy) in particles)
                {
                    el.style.translate = new Translate(-ParticleRadius + dx * t, -ParticleRadius + dy * t);
                    el.style.opacity   = 1f - t;
                }

                if (elapsed < ParticleDurationMs) return;
                anim.Pause();
                _particleContainer.Clear();
            }).Every(FadeIntervalMs);
            _activeAnimations.Add(anim);
        }

        private void BuildAndFadeInGrid()
        {
            _grid.Clear();
            foreach (var item in _wonItems ?? Array.Empty<IInventoryItem>())
                _grid.Add(new GachaRevealCard(item, _iconDictionary, _defaultIcon));

            ScheduleFade(_grid, from: 0f, to: 1f, durationMs: GridFadeInDurationMs);
        }

        private void ShowContinueButton()
        {
            _continueButton.style.display = DisplayStyle.Flex;
            ScheduleFade(_continueButton, from: 0f, to: 1f, durationMs: ButtonFadeInDurationMs);
        }

        private void ScheduleFade(
            VisualElement target,
            float from,
            float to,
            long durationMs,
            Action onComplete = null)
        {
            target.style.opacity = from;
            long elapsed = 0;
            IVisualElementScheduledItem anim = null;
            anim = target.schedule.Execute(() =>
            {
                elapsed += FadeIntervalMs;
                target.style.opacity = Mathf.Lerp(from, to, Mathf.Clamp01((float)elapsed / durationMs));
                if (elapsed < durationMs) return;
                anim.Pause();
                target.style.opacity = to;
                onComplete?.Invoke();
            }).Every(FadeIntervalMs);
            _activeAnimations.Add(anim);
        }

        #endregion

        #region Helpers

        private static void SetIcon(VisualElement container, Sprite sprite)
        {
            container.style.backgroundImage = sprite != null
                ? new StyleBackground(sprite)
                : StyleKeyword.Null;
        }

        private Color GetHighestRarityColor()
        {
            var highestRarity = (_wonItems ?? Array.Empty<IInventoryItem>())
                .Select(item =>
                {
                    item.NumericProperties.TryGetValue(GachaConstants.PropStarRarity, out var r);
                    return r;
                })
                .DefaultIfEmpty(0)
                .Max();

            return GachaConstants.GetRarityColor(highestRarity);
        }

        #endregion
    }
}