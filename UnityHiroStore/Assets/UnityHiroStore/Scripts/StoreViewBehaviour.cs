using System;
using System.Collections.Generic;
using Hiro;
using Hiro.Unity;
using Nakama;
using UnityEngine;
using UnityEngine.UIElements;

namespace HiroStore
{
    [Serializable]
    public class StoreItemIconMapping
    {
        public string itemId;
        public Sprite icon;
    }

    [Serializable]
    public class CurrencyIconMapping
    {
        public string currencyCode;
        public Sprite icon;
    }

    /// <summary>
    /// MonoBehaviour that manages the Store MVP components.
    /// Creates Controller and View, handles lifecycle per Unity MVP pattern.
    /// </summary>
    [RequireComponent(typeof(UIDocument))]
    public class StoreViewBehaviour : MonoBehaviour
    {
        [Header("UI Templates")]
        [SerializeField] private VisualTreeAsset _storeItemTemplate;

        [Header("Icons")]
        [SerializeField] private StoreItemIconMapping[] _itemIconMappings;
        [SerializeField] private CurrencyIconMapping[] _currencyIconMappings;
        [SerializeField] private Sprite _defaultItemIcon;

        private HiroStoreCoordinator _coordinator;
        private StoreView _view;

        public StoreController Controller { get; private set; }

        public event Action<ISession, StoreController> OnInitialized;

        private void Start()
        {
            _coordinator = HiroCoordinator.Instance as HiroStoreCoordinator;
            if (_coordinator == null)
            {
                Debug.LogError("HiroStoreCoordinator not found");
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
            var economySystem = _coordinator.GetSystem<EconomySystem>();

            Controller = new StoreController(nakamaSystem, economySystem, BuildItemIconDictionary(), BuildCurrencyIconDictionary(), _defaultItemIcon);

            var env = _coordinator.IsLocalHost ? "local" : "heroiclabs";
            AccountSwitcher.Initialize(nakamaSystem, env);

            var uiDocument = GetComponent<UIDocument>();
            _view = new StoreView(
                Controller,
                uiDocument.rootVisualElement,
                _storeItemTemplate);

            _view.OnInitialized += () => OnInitialized?.Invoke(session, Controller);
        }

        private Dictionary<string, Sprite> BuildItemIconDictionary()
        {
            var iconDictionary = new Dictionary<string, Sprite>();
            if (_itemIconMappings != null)
            {
                foreach (var mapping in _itemIconMappings)
                {
                    if (!string.IsNullOrEmpty(mapping.itemId) && mapping.icon != null)
                    {
                        iconDictionary[mapping.itemId] = mapping.icon;
                    }
                }
            }
            return iconDictionary;
        }

        private Dictionary<string, Sprite> BuildCurrencyIconDictionary()
        {
            var iconDictionary = new Dictionary<string, Sprite>();
            if (_currencyIconMappings != null)
            {
                foreach (var mapping in _currencyIconMappings)
                {
                    if (!string.IsNullOrEmpty(mapping.currencyCode) && mapping.icon != null)
                    {
                        iconDictionary[mapping.currencyCode] = mapping.icon;
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
