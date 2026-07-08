using System;
using System.Collections.Generic;
using Hiro;
using Hiro.Unity;
using HeroicUtils;
using Nakama;
using UnityEngine;
using UnityEngine.UIElements;

namespace UnityGacha
{
    [Serializable]
    public class ItemIconMapping
    {
        public string itemId;
        public Sprite icon;
    }

    /// <summary>
    /// MonoBehaviour that manages the MVP components.
    /// Creates Controller and Views, owns tab coordination and lifecycle.
    /// Drives initialization order: codex and inventory are loaded before any view is activated.
    /// </summary>
    [RequireComponent(typeof(UIDocument))]
    public class GachaViewBehaviour : MonoBehaviour
    {
        [Header("Icons")]
        [SerializeField] private ItemIconMapping[] _itemIconMappings;
        [SerializeField] private Sprite _defaultIcon;

        private UnityGachaCoordinator _coordinator;
        private GachaController _controller;
        private InventoryView _inventoryView;
        private GachaView _gachaView;

        private enum ActiveTab { Inventory, Gacha }
        private ActiveTab _activeTab = ActiveTab.Gacha;

        private Button _inventoryTabButton;
        private Button _gachaTicketsTabButton;
        private VisualElement _inventoryContainer;
        private VisualElement _gachaContainer;
        private VisualElement _gachaTicketsFocusOverlay;
        private VisualElement _inventoryFocusOverlay;

        public event Action<ISession, GachaController> OnInitialized;

        private void Start()
        {
            _coordinator = HiroCoordinator.Instance as UnityGachaCoordinator;
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

        private async void OnCoordinatorReady(ISession session)
        {
            if (_coordinator == null) return;

            _coordinator.ReceivedStartSuccess -= OnCoordinatorReady;
            _coordinator.ReceivedStartError -= OnCoordinatorError;

            var nakamaSystem = _coordinator.GetSystem<NakamaSystem>();
            var inventorySystem = _coordinator.GetSystem<InventorySystem>();
            var economySystem = _coordinator.GetSystem<EconomySystem>();
            var statsSystem = _coordinator.GetSystem<StatsSystem>();

            _controller = new GachaController(nakamaSystem, inventorySystem, economySystem, statsSystem);

            AccountSwitcher.Initialize(nakamaSystem);

            var inventoryItemTemplate = Resources.Load<VisualTreeAsset>("InventoryItemTemplate");
            var gachaTicketTemplate = Resources.Load<VisualTreeAsset>("InventoryGachaTicketTemplate");
            var iconDictionary = BuildIconDictionary();

            var root = GetComponent<UIDocument>().rootVisualElement;

            _inventoryTabButton = root.Q<Button>("inventory-tab-btn");
            _gachaTicketsTabButton = root.Q<Button>("gacha-tickets-tab-btn");
            _inventoryContainer = root.Q<VisualElement>("inventory-container");
            _gachaContainer = root.Q<VisualElement>("gacha-container");
            _gachaTicketsFocusOverlay = root.Q<VisualElement>("gacha-tickets-tab-focus");
            _inventoryFocusOverlay = root.Q<VisualElement>("inventory-tab-focus");
            _inventoryTabButton.RegisterCallback<ClickEvent>(_ => OnInventoryTabClicked());
            _gachaTicketsTabButton.RegisterCallback<ClickEvent>(_ => OnGachaTabClicked());

            _inventoryView = new InventoryView(
                _controller,
                root,
                inventoryItemTemplate,
                iconDictionary,
                _defaultIcon);

            _gachaView = new GachaView(
                root,
                _controller,
                inventoryItemTemplate,
                gachaTicketTemplate,
                iconDictionary,
                _defaultIcon);

            SetTabVisuals();

            try
            {
                await _controller.LoadItemCodexAsync();
                await _controller.RefreshInventoryAsync();
                _gachaView.Activate();
                OnInitialized?.Invoke(session, _controller);
            }
            catch (Exception e)
            {
                Debug.LogException(e);
            }
        }

        private void OnInventoryTabClicked()
        {
            if (_activeTab == ActiveTab.Inventory) return;
            _activeTab = ActiveTab.Inventory;
            SetTabVisuals();

            _gachaView.Deactivate();
            _inventoryView.Activate();
        }

        private void OnGachaTabClicked()
        {
            if (_activeTab == ActiveTab.Gacha) return;
            _activeTab = ActiveTab.Gacha;
            SetTabVisuals();

            _inventoryView.Deactivate();
            _gachaView.Activate();
        }

        private void SetTabVisuals()
        {
            bool isGacha = _activeTab == ActiveTab.Gacha;
            _inventoryTabButton.style.opacity = 1f;
            _gachaTicketsTabButton.style.opacity = 1f;
            _inventoryContainer.style.display = isGacha ? DisplayStyle.None : DisplayStyle.Flex;
            _gachaContainer.style.display = isGacha ? DisplayStyle.Flex : DisplayStyle.None;
            if (_gachaTicketsFocusOverlay != null)
                _gachaTicketsFocusOverlay.style.display = isGacha ? DisplayStyle.Flex : DisplayStyle.None;
            if (_inventoryFocusOverlay != null)
                _inventoryFocusOverlay.style.display = isGacha ? DisplayStyle.None : DisplayStyle.Flex;
        }

        private Dictionary<string, Sprite> BuildIconDictionary()
        {
            var iconDictionary = new Dictionary<string, Sprite>();
            if (_itemIconMappings != null)
            {
                foreach (var mapping in _itemIconMappings)
                {
                    if (!string.IsNullOrEmpty(mapping.itemId) && mapping.icon != null)
                        iconDictionary[mapping.itemId] = mapping.icon;
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

            _inventoryView?.Dispose();
            _gachaView?.Dispose();
        }
    }
}