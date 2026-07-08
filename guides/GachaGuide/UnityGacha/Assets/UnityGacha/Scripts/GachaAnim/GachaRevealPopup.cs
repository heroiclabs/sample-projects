using System;
using System.Collections.Generic;
using System.Linq;
using Hiro;
using UnityEngine;
using UnityEngine.UIElements;

namespace UnityGacha.GachaAnim
{
    public sealed class GachaRevealPopup : IDisposable
    {
        public event Action OnContinueClicked;

        private readonly VisualElement _overlay;
        private readonly VisualElement _ticketIcon;
        private readonly VisualElement _particleContainer;
        private readonly VisualElement _wonItemContainer;
        private readonly VisualElement _wonItemCard;
        private readonly VisualElement _wonItemIcon;
        private readonly Label _wonItemName;
        private readonly Label _wonItemRarity;
        private readonly Button _continueButton;
        private readonly Dictionary<string, Sprite> _iconDictionary;
        private readonly Sprite _defaultIcon;
        private readonly List<IVisualElementScheduledItem> _activeAnimations = new();

        public GachaRevealPopup(
            VisualElement root,
            Dictionary<string, Sprite> iconDictionary,
            Sprite defaultIcon)
        {
            _iconDictionary = iconDictionary;
            _defaultIcon = defaultIcon;

            _overlay           = root.Q<VisualElement>("gacha-reveal-popup");
            _ticketIcon        = root.Q<VisualElement>("reveal-ticket-icon");
            _particleContainer = root.Q<VisualElement>("reveal-particle-container");
            _wonItemContainer  = root.Q<VisualElement>("reveal-won-item-container");
            _wonItemCard       = root.Q<VisualElement>("reveal-won-item-card");
            _wonItemIcon       = root.Q<VisualElement>("reveal-won-item-icon");
            _wonItemName       = root.Q<Label>("reveal-won-item-name");
            _wonItemRarity     = root.Q<Label>("reveal-won-item-rarity");
            _continueButton    = root.Q<Button>("reveal-continue-button");

            _continueButton.RegisterCallback<ClickEvent>(_ =>
            {
                Hide();
                OnContinueClicked?.Invoke();
            });

            Hide();
        }

        public void Show(IInventoryItem wonItem, Sprite ticketSprite)
        {
            if (_overlay.style.display == DisplayStyle.Flex) return;

            SetIcon(_ticketIcon, ticketSprite);

            SetIcon(_wonItemIcon, GetSprite(wonItem.Id));
            _wonItemName.text = wonItem.Name;
            wonItem.NumericProperties.TryGetValue("star_rarity", out var starRarity);
            _wonItemRarity.text = $"{starRarity}★";
            _wonItemRarity.style.color = new StyleColor(GetRarityColor(starRarity));
            _wonItemCard.style.backgroundColor = new StyleColor(GetRarityColor(starRarity));

            _ticketIcon.style.opacity = 1f;
            _ticketIcon.style.display = DisplayStyle.Flex;
            _wonItemContainer.style.opacity = 0f;
            _continueButton.style.display = DisplayStyle.None;
            _continueButton.style.opacity = 0f;
            _particleContainer.Clear();

            _overlay.style.display = DisplayStyle.Flex;

            PlayAnimation();
        }

        public void Hide()
        {
            PauseAllAnimations();
            _overlay.style.display = DisplayStyle.None;
        }

        public void Dispose() => PauseAllAnimations();

        private void PauseAllAnimations()
        {
            foreach (var a in _activeAnimations) a.Pause();
            _activeAnimations.Clear();
        }

        private void SetIcon(VisualElement container, Sprite sprite)
        {
            container.style.backgroundImage = sprite != null
                ? new StyleBackground(sprite)
                : StyleKeyword.Null;
        }

        private Sprite GetSprite(string itemId)
        {
            if (_iconDictionary != null &&
                _iconDictionary.TryGetValue(itemId, out var s) && s != null)
                return s;
            return _defaultIcon;
        }

        private static Color GetRarityColor(double rarity) => rarity switch
        {
            4 => new Color(0.580f, 0.322f, 0.980f),
            5 => new Color(1.000f, 0.733f, 0.012f),
            6 => new Color(0.996f, 0.353f, 0.000f),
            _ => new Color(0.745f, 0.722f, 0.855f)
        };

        private void PlayAnimation()
        {
            const long shakeIntervalMs = 60;
            const long shakeDurationMs = 1000;
            const float shakeMagnitude = 12f;
            long shakeElapsed = 0;
            var shakeDir = 1f;

            IVisualElementScheduledItem shakeAnim = null;
            shakeAnim = _ticketIcon.schedule.Execute(() =>
            {
                shakeElapsed += shakeIntervalMs;
                _ticketIcon.style.translate = new Translate(shakeMagnitude * shakeDir, 0f);
                shakeDir = -shakeDir;

                if (shakeElapsed < shakeDurationMs) return;
                shakeAnim.Pause();
                _ticketIcon.style.translate = new Translate(0f, 0f);
                StartPostShakePhase();
            }).Every(shakeIntervalMs);
            _activeAnimations.Add(shakeAnim);
        }

        private void StartPostShakePhase()
        {
            long elapsed = 0;
            const long duration = 200;
            const long interval = 16;
            IVisualElementScheduledItem fadeOut = null;
            fadeOut = _ticketIcon.schedule.Execute(() =>
            {
                elapsed += interval;
                _ticketIcon.style.opacity = 1f - Mathf.Clamp01((float)elapsed / duration);
                if (elapsed < duration) return;
                fadeOut.Pause();
                _ticketIcon.style.display = DisplayStyle.None;
            }).Every(interval);
            _activeAnimations.Add(fadeOut);

            SpawnParticles();

            _activeAnimations.Add(_overlay.schedule.Execute(StartWonItemFadeIn).StartingIn(200));
            _activeAnimations.Add(_overlay.schedule.Execute(ShowContinueButton).StartingIn(1000));
        }

        private void SpawnParticles()
        {
            const int count = 24;
            const float baseRadius = 60f;
            const float radiusJitter = 40f;
            const float angleJitterDeg = 7f;
            const long duration = 400;
            const long interval = 16;

            var random = new System.Random();
            var particleColor = _wonItemRarity.style.color.value;

            var particles = new List<(VisualElement el, float dx, float dy)>(count);

            for (var i = 0; i < count; i++)
            {
                var angleDeg = 360f / count * i
                               + (float)(random.NextDouble() * angleJitterDeg * 2 - angleJitterDeg);
                var rad = angleDeg * Mathf.Deg2Rad;
                var radius = baseRadius + (float)(random.NextDouble() * radiusJitter);
                var dx = Mathf.Cos(rad) * radius;
                var dy = Mathf.Sin(rad) * radius;

                var p = new VisualElement();
                p.style.position = Position.Absolute;
                p.style.width = 8;
                p.style.height = 8;
                p.style.borderTopLeftRadius = 4;
                p.style.borderTopRightRadius = 4;
                p.style.borderBottomLeftRadius = 4;
                p.style.borderBottomRightRadius = 4;
                p.style.backgroundColor = new StyleColor(particleColor);
                p.style.left = Length.Percent(50);
                p.style.top = Length.Percent(50);
                p.style.translate = new Translate(-4f, -4f);
                p.style.opacity = 1f;

                _particleContainer.Add(p);
                particles.Add((p, dx, dy));
            }

            long elapsed = 0;
            IVisualElementScheduledItem particleAnim = null;
            particleAnim = _particleContainer.schedule.Execute(() =>
            {
                elapsed += interval;
                var t = Mathf.Clamp01((float)elapsed / duration);

                foreach (var (el, dx, dy) in particles)
                {
                    el.style.translate = new Translate(-4f + dx * t, -4f + dy * t);
                    el.style.opacity = 1f - t;
                }

                if (elapsed < duration) return;
                particleAnim.Pause();
                _particleContainer.Clear();
            }).Every(interval);
            _activeAnimations.Add(particleAnim);
        }

        private void StartWonItemFadeIn()
        {
            long elapsed = 0;
            const long duration = 400;
            const long interval = 16;
            IVisualElementScheduledItem anim = null;
            anim = _wonItemContainer.schedule.Execute(() =>
            {
                elapsed += interval;
                _wonItemContainer.style.opacity = Mathf.Clamp01((float)elapsed / duration);
                if (elapsed < duration) return;
                anim.Pause();
                _wonItemContainer.style.opacity = 1f;
            }).Every(interval);
            _activeAnimations.Add(anim);
        }

        private void ShowContinueButton()
        {
            _continueButton.style.display = DisplayStyle.Flex;
            long elapsed = 0;
            const long duration = 200;
            const long interval = 16;
            IVisualElementScheduledItem anim = null;
            anim = _continueButton.schedule.Execute(() =>
            {
                elapsed += interval;
                _continueButton.style.opacity = Mathf.Clamp01((float)elapsed / duration);
                if (elapsed < duration) return;
                anim.Pause();
                _continueButton.style.opacity = 1f;
            }).Every(interval);
            _activeAnimations.Add(anim);
        }
    }
}
