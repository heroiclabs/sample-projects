using System;
using System.Linq;
using System.Threading.Tasks;
using Hiro;
using HeroicUI;
using UnityEngine;
using UnityEngine.UIElements;

namespace HiroInventory
{
    public sealed class InventoryView
    {
        private readonly InventoryController _controller;
        private readonly HiroInventoryCoordinator _coordinator;
        private readonly VisualTreeAsset _inventoryItemTemplate;
        private readonly Sprite _defaultIcon;

        private WalletDisplay _walletDisplay;

        private Button _grantButton;
        private Button _consumeButton;
        private Button _removeButton;
        private Button _refreshButton;
        private DropdownField _grantItemDropdown;

        private VisualElement _inventoryGrid;
        private Color _itemBorderDefaultColor;
        private VisualElement _itemDetailsPanel;
        private Label _detailsNameLabel;
        private Label _detailsDescriptionLabel;
        private Label _detailsCategoryLabel;
        private Label _detailsQuantityLabel;
        private Label _detailsStackableLabel;
        private Label _detailsConsumableLabel;
        private Label _detailsMaxCountLabel;
        private Label _stringPropertiesTitle;
        private VisualElement _stringPropertiesList;
        private Label _numericPropertiesTitle;
        private VisualElement _numericPropertiesList;

        private VisualElement _grantModal;
        private IntegerField _grantQuantityField;
        private Button _grantModalButton;
        private Button _grantModalCloseButton;

        private VisualElement _consumeModal;
        private IntegerField _consumeQuantityField;
        private Toggle _consumeOverconsumeToggle;
        private Button _consumeModalButton;
        private Button _consumeModalCloseButton;

        private VisualElement _removeModal;
        private IntegerField _removeQuantityField;
        private Button _removeModalButton;
        private Button _removeModalCloseButton;

        private VisualElement _errorPopup;
        private Button _errorCloseButton;
        private Label _errorMessage;

        private VisualElement _inventoryFullModal;
        private Button _inventoryFullModalOkButton;
        private Button _inventoryFullModalCloseButton;

        private VisualElement _maxCountModal;
        private Label _maxCountMessage;
        private Button _maxCountModalOkButton;
        private Button _maxCountModalCloseButton;

        private VisualElement _selectedSlot;
        private UIDocument _uiDocument;

        public InventoryView(InventoryController controller, HiroInventoryCoordinator coordinator,
            VisualTreeAsset inventoryItemTemplate, Sprite defaultIcon)
        {
            _controller = controller;
            _coordinator = coordinator;
            _inventoryItemTemplate = inventoryItemTemplate;
            _defaultIcon = defaultIcon;

            InitializeUI();
        }

        #region UI Initialization

        private void InitializeUI()
        {
            _uiDocument = _controller.GetComponent<UIDocument>();
            var rootElement = _uiDocument.rootVisualElement;

            _walletDisplay = new WalletDisplay(rootElement.Q<VisualElement>("wallet-display"));

            _grantButton = rootElement.Q<Button>("item-grant");
            _grantButton.RegisterCallback<ClickEvent>(_ => { _grantModal.style.display = DisplayStyle.Flex; });

            _consumeButton = rootElement.Q<Button>("item-consume");
            _consumeButton.RegisterCallback<ClickEvent>(_ =>
            {
                var selectedItem = _controller.GetSelectedItem();
                if (selectedItem == null || !selectedItem.Consumable) return;
                _consumeQuantityField.value = 1;
                _consumeOverconsumeToggle.value = false;
                _consumeModal.style.display = DisplayStyle.Flex;
            });

            _removeButton = rootElement.Q<Button>("item-remove");
            _removeButton.RegisterCallback<ClickEvent>(_ =>
            {
                if (_controller.GetSelectedItem() == null) return;
                _removeQuantityField.value = 1;
                _removeModal.style.display = DisplayStyle.Flex;
            });

            _grantItemDropdown = rootElement.Q<DropdownField>("grant-item-dropdown");

            // Inventory grid
            _inventoryGrid = rootElement.Q<VisualElement>("inventory-grid");
            _itemBorderDefaultColor = new Color(1f, 0.84f, 0f, 0f);

            // Item details panel
            _itemDetailsPanel = rootElement.Q<VisualElement>("item-details");
            _itemDetailsPanel.pickingMode = PickingMode.Ignore;
            _detailsNameLabel = rootElement.Q<Label>("tooltip-name");
            _detailsDescriptionLabel = rootElement.Q<Label>("tooltip-description");
            _detailsCategoryLabel = rootElement.Q<Label>("tooltip-category");
            _detailsQuantityLabel = rootElement.Q<Label>("tooltip-quantity");
            _detailsStackableLabel = rootElement.Q<Label>("tooltip-stackable");
            _detailsConsumableLabel = rootElement.Q<Label>("tooltip-consumable");
            _detailsMaxCountLabel = rootElement.Q<Label>("tooltip-maxcount");
            _stringPropertiesTitle = rootElement.Q<Label>("string-properties-title");
            _stringPropertiesList = rootElement.Q<VisualElement>("string-properties-list");
            _numericPropertiesTitle = rootElement.Q<Label>("numeric-properties-title");
            _numericPropertiesList = rootElement.Q<VisualElement>("numeric-properties-list");
            ShowEmptyState();

            _refreshButton = rootElement.Q<Button>("inventory-refresh");
            _refreshButton.RegisterCallback<ClickEvent>(evt => _ = RefreshInventory());

            // Grant modal
            _grantModal = rootElement.Q<VisualElement>("grant-modal");
            _grantQuantityField = rootElement.Q<IntegerField>("grant-modal-quantity");
            _grantModalButton = rootElement.Q<Button>("grant-modal-grant");
            _grantModalButton.RegisterCallback<ClickEvent>(evt => _ = HandleGrantItem());
            _grantModalCloseButton = rootElement.Q<Button>("grant-modal-close");
            _grantModalCloseButton.RegisterCallback<ClickEvent>(_ =>
                _grantModal.style.display = DisplayStyle.None);

            // Consume modal
            _consumeModal = rootElement.Q<VisualElement>("consume-modal");
            _consumeQuantityField = rootElement.Q<IntegerField>("consume-modal-quantity");
            _consumeOverconsumeToggle = rootElement.Q<Toggle>("consume-modal-overconsume");
            _consumeModalButton = rootElement.Q<Button>("consume-modal-consume");
            _consumeModalButton.RegisterCallback<ClickEvent>(evt => _ = HandleConsumeItem());
            _consumeModalCloseButton = rootElement.Q<Button>("consume-modal-close");
            _consumeModalCloseButton.RegisterCallback<ClickEvent>(_ =>
                _consumeModal.style.display = DisplayStyle.None);

            // Remove modal
            _removeModal = rootElement.Q<VisualElement>("remove-modal");
            _removeQuantityField = rootElement.Q<IntegerField>("remove-modal-quantity");
            _removeModalButton = rootElement.Q<Button>("remove-modal-remove");
            _removeModalButton.RegisterCallback<ClickEvent>(evt => _ = HandleRemoveItem());
            _removeModalCloseButton = rootElement.Q<Button>("remove-modal-close");
            _removeModalCloseButton.RegisterCallback<ClickEvent>(_ =>
                _removeModal.style.display = DisplayStyle.None);

            // Error popup
            _errorPopup = rootElement.Q<VisualElement>("error-popup");
            _errorMessage = rootElement.Q<Label>("error-message");
            _errorCloseButton = rootElement.Q<Button>("error-close");
            _errorCloseButton.RegisterCallback<ClickEvent>(_ => _errorPopup.style.display = DisplayStyle.None);

            // Inventory full modal
            _inventoryFullModal = rootElement.Q<VisualElement>("inventory-full-modal");
            _inventoryFullModalOkButton = rootElement.Q<Button>("inventory-full-modal-ok");
            _inventoryFullModalOkButton.RegisterCallback<ClickEvent>(_ => _inventoryFullModal.style.display = DisplayStyle.None);
            _inventoryFullModalCloseButton = rootElement.Q<Button>("inventory-full-modal-close");
            _inventoryFullModalCloseButton.RegisterCallback<ClickEvent>(_ => _inventoryFullModal.style.display = DisplayStyle.None);

            // Max count modal
            _maxCountModal = rootElement.Q<VisualElement>("max-count-modal");
            _maxCountMessage = rootElement.Q<Label>("max-count-message");
            _maxCountModalOkButton = rootElement.Q<Button>("max-count-modal-ok");
            _maxCountModalOkButton.RegisterCallback<ClickEvent>(_ => _maxCountModal.style.display = DisplayStyle.None);
            _maxCountModalCloseButton = rootElement.Q<Button>("max-count-modal-close");
            _maxCountModalCloseButton.RegisterCallback<ClickEvent>(_ => _maxCountModal.style.display = DisplayStyle.None);

            UpdateActionButtons();
        }

        public void StartObservingWallet()
        {
            _walletDisplay.StartObserving();
        }

        public void HideAllModals()
        {
            _grantModal.style.display = DisplayStyle.None;
            _consumeModal.style.display = DisplayStyle.None;
            _removeModal.style.display = DisplayStyle.None;
            _inventoryFullModal.style.display = DisplayStyle.None;
            _maxCountModal.style.display = DisplayStyle.None;
            ShowEmptyState();
        }

        #endregion

        #region Inventory Display

        public async Task RefreshInventory()
        {
            try
            {
                var items = await _controller.RefreshInventory();
                
                // Populate grant dropdown with codex items
                var itemNames = _controller.CodexItems.Select(item => item.Name).ToList();
                _grantItemDropdown.choices = itemNames;

                PopulateInventoryGrid();

                // Clear selection after refresh
                if(_controller.GetSelectedItem() == null)
                {
                    _controller.SelectItem(null);
                    _selectedSlot = null;
                    ShowEmptyState();
                }
                UpdateActionButtons();
            }
            catch (Exception e)
            {
                ShowError(e.Message);
            }
        }

        private void PopulateInventoryGrid()
        {
            _inventoryGrid.Clear();

            // Sort items by owned time descending (newest items first)
            var sortedItems = _controller.InventoryItems
                .OrderByDescending(item => item.OwnedTimeSec)
                .ToList();

            foreach (var item in sortedItems)
            {
                // Instantiate the template
                var itemSlot = _inventoryItemTemplate.Instantiate();
                var slotRoot = itemSlot.Q<VisualElement>("inventory-slot");

                // Store reference to item for selection tracking
                slotRoot.userData = item;

                // Set item icon
                var iconContainer = slotRoot.Q<VisualElement>("item-icon");
                SetItemIcon(iconContainer, item.Id);

                // Update quantity label
                var quantityLabel = slotRoot.Q<Label>("item-quantity");
                quantityLabel.text = $"{item.Count}";

                // Get rarity and set background color
                var rarity = item.StringProperties.ContainsKey("rarity")
                    ? item.StringProperties["rarity"].ToLower()
                    : "common";

                Color rarityColor = GetRarityColor(rarity);
                slotRoot.style.backgroundColor = new StyleColor(rarityColor);

                // Click to select
                slotRoot.RegisterCallback<ClickEvent>(evt => { SelectItemSlot(item, slotRoot); });

                _inventoryGrid.Add(slotRoot);
            }
        }

        private void SelectItemSlot(IInventoryItem item, VisualElement slot)
        {
            // Deselect previous slot - restore its original rarity color
            if (_selectedSlot != null && _selectedSlot != slot)
            {
                var previousItem = _selectedSlot.userData as IInventoryItem;
                if (previousItem != null)
                {
                    var rarity = previousItem.StringProperties.ContainsKey("rarity")
                        ? previousItem.StringProperties["rarity"].ToLower()
                        : "common";
                    Color rarityColor = GetRarityColor(rarity);

                    _selectedSlot.style.backgroundColor = new StyleColor(rarityColor);
                    SetBorderColor(_selectedSlot, _itemBorderDefaultColor);
                }
            }

            _controller.SelectItem(item);
            _selectedSlot = slot;
            ShowItemDetails(item, Vector2.zero);
            // Highlight selected slot with turquoise border
            SetBorderColor(slot, new Color(0.467f, 0.984f, 0.937f));

            UpdateActionButtons();
        }

        private void UpdateActionButtons()
        {
            var selectedItem = _controller.GetSelectedItem();
            var hasSelection = selectedItem != null;
            var isConsumable = hasSelection && selectedItem.Consumable;

            _consumeButton.SetEnabled(isConsumable);
            _removeButton.SetEnabled(hasSelection);
        }

        #endregion

        #region Visual Helpers

        private void SetItemIcon(VisualElement iconContainer, string itemId)
        {
            if (_controller.IconDictionary != null && 
                _controller.IconDictionary.TryGetValue(itemId, out Sprite icon) && icon != null)
            {
                iconContainer.style.backgroundImage = new StyleBackground(icon);
            }
            else if (_defaultIcon != null)
            {
                iconContainer.style.backgroundImage = new StyleBackground(_defaultIcon);
            }
            else
            {
                iconContainer.style.backgroundImage = StyleKeyword.Null;
                Debug.LogWarning($"No icon mapping found for item ID: {itemId}");
            }
        }

        private Color GetRarityColor(string rarity)
        {
            return rarity switch
            {
                "common" => new Color(0.541f, 0.714f, 0.965f, 1.0f),
                "uncommon" => new Color(0.784f, 0.875f, 0.478f, 1.0f),
                "rare" => new Color(0.647f, 0.533f, 0.965f, 1.0f),
                "epic" => new Color(0.992f, 0.878f, 0.373f, 1.0f),
                "legendary" => new Color(1f, 0.584f, 0.573f, 1.0f),
                _ => new Color(0.745f, 0.722f, 0.855f, 1.0f)
            };
        }

        private void SetBorderColor(VisualElement element, Color color)
        {
            element.style.borderTopColor = new StyleColor(color);
            element.style.borderBottomColor = new StyleColor(color);
            element.style.borderLeftColor = new StyleColor(color);
            element.style.borderRightColor = new StyleColor(color);
        }

        #endregion

        #region Item Details

        private void ShowItemDetails(IInventoryItem item, Vector2 mousePosition)
        {
            _detailsNameLabel.text = item.Name;
            _detailsDescriptionLabel.text = string.IsNullOrEmpty(item.Description)
                ? "No description available."
                : item.Description;
            _detailsCategoryLabel.text = item.Category ?? "Uncategorized";
            _detailsQuantityLabel.text = $"Quantity: {item.Count}";

            // Item Attributes (always present)
            _detailsStackableLabel.text = $"Stackable: {(item.Stackable ? "Yes" : "No")}";
            _detailsConsumableLabel.text = $"Consumable: {(item.Consumable ? "Yes" : "No")}";
            _detailsMaxCountLabel.text = $"Max Stack: {item.MaxCount}";

            // Clear previous properties
            _stringPropertiesList.Clear();
            _numericPropertiesList.Clear();

            // String Properties
            if (item.StringProperties.Count > 0)
            {
                _stringPropertiesTitle.style.display = DisplayStyle.Flex;
                foreach (var prop in item.StringProperties)
                {
                    var label = new Label($"• {prop.Key}: {prop.Value}");
                    label.style.marginBottom = 4;
                    label.style.color = new StyleColor(new Color(0.314f, 0.314f, 0.314f));
                    _stringPropertiesList.Add(label);
                }
            }
            else
            {
                _stringPropertiesTitle.style.display = DisplayStyle.None;
            }

            // Numeric Properties
            if (item.NumericProperties.Count > 0)
            {
                _numericPropertiesTitle.style.display = DisplayStyle.Flex;
                foreach (var prop in item.NumericProperties)
                {
                    var label = new Label($"• {prop.Key}: {prop.Value}");
                    label.style.marginBottom = 4;
                    label.style.color = new StyleColor(new Color(0.314f, 0.314f, 0.314f));
                    _numericPropertiesList.Add(label);
                }
            }
            else
            {
                _numericPropertiesTitle.style.display = DisplayStyle.None;
            }

            _itemDetailsPanel.style.display = DisplayStyle.Flex;
        }

        private void HideItemDetails()
        {
            ShowEmptyState();
        }

        private void ShowEmptyState()
        {
            _detailsNameLabel.text = "No Item Selected";
            _detailsDescriptionLabel.text = "Select an item from the inventory to view details.";
            _detailsCategoryLabel.text = "";
            _detailsQuantityLabel.text = "";
            _detailsStackableLabel.text = "";
            _detailsConsumableLabel.text = "";
            _detailsMaxCountLabel.text = "";

            // Clear and hide optional properties
            _stringPropertiesList.Clear();
            _numericPropertiesList.Clear();
            _stringPropertiesTitle.style.display = DisplayStyle.None;
            _numericPropertiesTitle.style.display = DisplayStyle.None;

            _itemDetailsPanel.style.display = DisplayStyle.Flex;
        }

        #endregion

        #region Action Handlers

        private async Task HandleGrantItem()
        {
            try
            {
                await _controller.GrantItem(_grantItemDropdown.index, _grantQuantityField.value);
                _grantModal.style.display = DisplayStyle.None;
                await RefreshInventory();
            }
            catch (InvalidOperationException e) when (e.Message == "INVENTORY_FULL")
            {
                _grantModal.style.display = DisplayStyle.None;
                _inventoryFullModal.style.display = DisplayStyle.Flex;
            }
            catch (Exception e)
            {
                ShowError(e.Message);
            }
        }

        /// <summary>
        /// Displays the max count modal with details from the exception message.
        /// </summary>
        private void ShowMaxCountReachedModal(string exceptionMessage)
        {
            _maxCountMessage.text = $"You've reached the maximum limit for this item.";
            _maxCountModal.style.display = DisplayStyle.Flex;
        }

        private async Task HandleConsumeItem()
        {
            try
            {
                await _controller.ConsumeItem(_consumeQuantityField.value, _consumeOverconsumeToggle.value);
                _consumeModal.style.display = DisplayStyle.None;
                await RefreshInventory();
            }
            catch (Exception e)
            {
                ShowError(e.Message);
                Debug.LogError(e);
            }
        }

        private async Task HandleRemoveItem()
        {
            try
            {
                await _controller.RemoveItem(_removeQuantityField.value);
                _removeModal.style.display = DisplayStyle.None;
                await RefreshInventory();
            }
            catch (Exception e)
            {
                ShowError(e.Message);
            }
        }

        public void ShowError(string message)
        {
            _errorPopup.style.display = DisplayStyle.Flex;
            _errorMessage.text = message;
        }

        #endregion
    }
}