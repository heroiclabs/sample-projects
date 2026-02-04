using System;
using System.Collections.Generic;
using Hiro;
using Hiro.Unity;
using Nakama;
using UnityEngine;
using UnityEngine.UIElements;

namespace HiroAchievements
{
    [Serializable]
    public class AchievementIconMapping
    {
        public string achievementId;
        public Sprite icon;
    }

    /// <summary>
    /// MonoBehaviour that manages the Achievements MVP components.
    /// Creates Controller and View, handles lifecycle per Unity MVP pattern.
    /// </summary>
    [RequireComponent(typeof(UIDocument))]
    public class AchievementsViewBehaviour : MonoBehaviour
    {
        [Header("UI Templates")]
        [SerializeField] private VisualTreeAsset _achievementItemTemplate;
        [SerializeField] private VisualTreeAsset _subAchievementItemTemplate;

        [Header("Icons")]
        [SerializeField] private AchievementIconMapping[] _achievementIconMappings;
        [SerializeField] private Sprite _defaultIcon;

        private HiroAchievementsCoordinator _coordinator;
        private AchievementsView _view;

        public AchievementsController Controller { get; private set; }

        public event Action<ISession, AchievementsController> OnInitialized;

        private void Start()
        {
            _coordinator = HiroCoordinator.Instance as HiroAchievementsCoordinator;
            if (_coordinator == null)
            {
                Debug.LogError("HiroAchievementsCoordinator not found");
                return;
            }

            _coordinator.ReceivedStartSuccess += OnCoordinatorReady;
            _coordinator.ReceivedStartError += OnCoordinatorError;
        }

        private void OnCoordinatorError(Exception e)
        {
            Debug.LogException(e);
        }

        private void OnCoordinatorReady(ISession session)
        {
            if (_coordinator == null) return;

            _coordinator.ReceivedStartSuccess -= OnCoordinatorReady;
            _coordinator.ReceivedStartError -= OnCoordinatorError;

            var nakamaSystem = _coordinator.GetSystem<NakamaSystem>();
            var achievementsSystem = _coordinator.GetSystem<AchievementsSystem>();
            var economySystem = _coordinator.GetSystem<EconomySystem>();

            Controller = new AchievementsController(nakamaSystem, achievementsSystem, economySystem);

            var env = _coordinator.IsLocalHost ? "local" : "heroiclabs";
            AccountSwitcher.Initialize(nakamaSystem, env);

            var uiDocument = GetComponent<UIDocument>();
            _view = new AchievementsView(
                Controller,
                uiDocument.rootVisualElement,
                _achievementItemTemplate,
                _subAchievementItemTemplate,
                BuildIconDictionary(),
                _defaultIcon);

            _view.OnInitialized += () => OnInitialized?.Invoke(session, Controller);
        }

        private Dictionary<string, Sprite> BuildIconDictionary()
        {
            var iconDictionary = new Dictionary<string, Sprite>();
            if (_achievementIconMappings != null)
            {
                foreach (var mapping in _achievementIconMappings)
                {
                    if (!string.IsNullOrEmpty(mapping.achievementId) && mapping.icon != null)
                    {
                        iconDictionary[mapping.achievementId] = mapping.icon;
                    }
                }
            }
            return iconDictionary;
        }

        private void OnDestroy()
        {
            if (_coordinator != null)
            {
                _coordinator.ReceivedStartSuccess -= OnCoordinatorReady;
                _coordinator.ReceivedStartError -= OnCoordinatorError;
            }

            _view?.Dispose();
        }
    }
}
