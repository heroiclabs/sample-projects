using System;
using System.Collections.Generic;
using Hiro;
using Hiro.Unity;
using Nakama;
using UnityEngine;
using UnityEngine.UIElements;

namespace HiroInventory
{
    [Serializable]
    public class ItemIconMapping
    {
        public string itemId;
        public Sprite icon;
    }

    /// <summary>
    /// MonoBehaviour that manages the Inventory MVP components.
    /// Creates Controller and View, handles lifecycle per Unity MVP pattern.
    /// </summary>
    [RequireComponent(typeof(UIDocument))]
    public class InventoryViewBehaviour : MonoBehaviour
    {
        [Header("UI Templates")]
        [SerializeField] private VisualTreeAsset _inventoryItemTemplate;

        [Header("Icons")]
        [SerializeField] private ItemIconMapping[] _itemIconMappings;
        [SerializeField] private Sprite _defaultIcon;

        private HiroInventoryCoordinator _coordinator;
        private InventoryView _view;

        public InventoryController Controller { get; private set; }

        public event Action<ISession, InventoryController> OnInitialized;

        private void Start()
        {
            _coordinator = HiroCoordinator.Instance as HiroInventoryCoordinator;
            if (_coordinator == null)
            {
                Debug.LogError("HiroInventoryCoordinator not found");
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
            var inventorySystem = _coordinator.GetSystem<InventorySystem>();
            var economySystem = _coordinator.GetSystem<EconomySystem>();

            Controller = new InventoryController(nakamaSystem, inventorySystem, economySystem);

            var env = _coordinator.IsLocalHost ? "local" : "heroiclabs";
            AccountSwitcher.Initialize(nakamaSystem, env);

            var uiDocument = GetComponent<UIDocument>();
            _view = new InventoryView(
                Controller,
                uiDocument.rootVisualElement,
                _inventoryItemTemplate,
                BuildIconDictionary(),
                _defaultIcon);

            _view.OnInitialized += () => OnInitialized?.Invoke(session, Controller);
        }

        private Dictionary<string, Sprite> BuildIconDictionary()
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
