using System;
using System.Collections.Generic;
using Hiro;
using UnityEngine;
using UnityEngine.UIElements;
using Hiro.Unity;
using System.Linq;
using System.Threading.Tasks;
using HeroicUI;
using Nakama;

namespace HiroInventory
{
    [System.Serializable]
    public class ItemIconMapping
    {
        public string itemId;
        public Sprite icon;
    }

    [RequireComponent(typeof(UIDocument))]
    public class HiroInventoryController : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private VisualTreeAsset inventoryItemTemplate;
        [SerializeField] private ItemIconMapping[] itemIconMappings;
        [SerializeField] private Sprite defaultIcon; // Optional: fallback icon

        public event Action<ISession, HiroInventoryController> OnInitialized;

        private WalletDisplay walletDisplay;

        private Button grantButton;
        private Button consumeButton;
        private Button removeButton;
        private Button refreshButton;
        private DropdownField grantItemDropdown;

        private VisualElement inventoryGrid;
        private Color itemBoarderDefaultColor;
        private VisualElement itemTooltip;
        private Label tooltipNameLabel;
        private Label tooltipDescriptionLabel;
        private Label tooltipCategoryLabel;
        private Label tooltipQuantityLabel;
        private Label tooltipPropertiesLabel;

        private VisualElement grantModal;
        private IntegerField grantQuantityField;
        private Button grantModalButton;
        private Button grantModalCloseButton;

        private VisualElement consumeModal;
        private IntegerField consumeQuantityField;
        private Toggle consumeOverconsumeToggle;
        private Button consumeModalButton;
        private Button consumeModalCloseButton;

        private VisualElement removeModal;
        private IntegerField removeQuantityField;
        private Button removeModalButton;
        private Button removeModalCloseButton;

        private VisualElement errorPopup;
        private Button errorCloseButton;
        private Label errorMessage;

        private VisualElement selectedSlot;
        private IInventoryItem selectedItem;
        private NakamaSystem nakamaSystem;
        private IInventorySystem inventorySystem;
        private IEconomySystem economySystem;

        private readonly List<IInventoryItem> inventoryItems = new();
        private readonly List<IInventoryItem> codexItems = new();
        private readonly Dictionary<string, IInventoryItem> codexLookup = new();
        private Dictionary<string, Sprite> iconDictionary;

        #region Initialization

        private void Start()
        {
            // Build the icon dictionary from the mappings
            iconDictionary = new Dictionary<string, Sprite>();
            if (itemIconMappings != null)
            {
                foreach (var mapping in itemIconMappings)
                {
                    if (!string.IsNullOrEmpty(mapping.itemId) && mapping.icon != null)
                    {
                        iconDictionary[mapping.itemId] = mapping.icon;
                    }
                }
            }

            InitializeUI();
            var inventoryCoordinator = HiroCoordinator.Instance as HiroInventoryCoordinator;
            if (inventoryCoordinator == null) return;

            inventoryCoordinator.ReceivedStartError += e =>
            {
                Debug.LogException(e);
                errorPopup.style.display = DisplayStyle.Flex;
                errorMessage.text = e.Message;
            };
            inventoryCoordinator.ReceivedStartSuccess += session =>
            {
                OnInitialized?.Invoke(session, this);

                nakamaSystem = this.GetSystem<NakamaSystem>();
                inventorySystem = this.GetSystem<InventorySystem>();
                economySystem = this.GetSystem<EconomySystem>();

                walletDisplay.StartObserving();

                _ = UpdateInventory();
                _ = LoadItemCodex();
            };
        }

        public void SwitchComplete()
        {
            grantModal.style.display = DisplayStyle.None;
            consumeModal.style.display = DisplayStyle.None;
            removeModal.style.display = DisplayStyle.None;
            itemTooltip.style.display = DisplayStyle.None;

            _ = UpdateInventory();
            economySystem.RefreshAsync();
        }

        #endregion

        #region UI Binding

        private void InitializeUI()
        {
            var rootElement = GetComponent<UIDocument>().rootVisualElement;

            walletDisplay = new WalletDisplay(rootElement.Q<VisualElement>("wallet-display"));

            grantButton = rootElement.Q<Button>("item-grant");
            grantButton.RegisterCallback<ClickEvent>(_ =>
            {
                grantModal.style.display = DisplayStyle.Flex;
            });

            consumeButton = rootElement.Q<Button>("item-consume");
            consumeButton.RegisterCallback<ClickEvent>(_ =>
            {
                if (selectedItem == null || !selectedItem.Consumable) return;
                consumeQuantityField.value = 1;
                consumeOverconsumeToggle.value = false;
                consumeModal.style.display = DisplayStyle.Flex;
            });

            removeButton = rootElement.Q<Button>("item-remove");
            removeButton.RegisterCallback<ClickEvent>(_ =>
            {
                if (selectedItem == null) return;
                removeQuantityField.value = 1;
                removeModal.style.display = DisplayStyle.Flex;
            });

            grantItemDropdown = rootElement.Q<DropdownField>("grant-item-dropdown");

            // Inventory grid
            inventoryGrid = rootElement.Q<VisualElement>("inventory-grid");
            itemBoarderDefaultColor = new Color(1f, 0.84f, 0f, 0f);

            // Item tooltip
            itemTooltip = rootElement.Q<VisualElement>("item-tooltip");
            tooltipNameLabel = rootElement.Q<Label>("tooltip-name");
            tooltipDescriptionLabel = rootElement.Q<Label>("tooltip-description");
            tooltipCategoryLabel = rootElement.Q<Label>("tooltip-category");
            tooltipQuantityLabel = rootElement.Q<Label>("tooltip-quantity");
            tooltipPropertiesLabel = rootElement.Q<Label>("tooltip-properties");
            itemTooltip.style.display = DisplayStyle.None;

            refreshButton = rootElement.Q<Button>("inventory-refresh");
            refreshButton.RegisterCallback<ClickEvent>(evt => _ = UpdateInventory());

            // Grant modal
            grantModal = rootElement.Q<VisualElement>("grant-modal");
            grantQuantityField = rootElement.Q<IntegerField>("grant-modal-quantity");
            grantModalButton = rootElement.Q<Button>("grant-modal-grant");
            grantModalButton.RegisterCallback<ClickEvent>(evt => _ = GrantItem());
            grantModalCloseButton = rootElement.Q<Button>("grant-modal-close");
            grantModalCloseButton.RegisterCallback<ClickEvent>(_ => grantModal.style.display = DisplayStyle.None);

            // Consume modal
            consumeModal = rootElement.Q<VisualElement>("consume-modal");
            consumeQuantityField = rootElement.Q<IntegerField>("consume-modal-quantity");
            consumeOverconsumeToggle = rootElement.Q<Toggle>("consume-modal-overconsume");
            consumeModalButton = rootElement.Q<Button>("consume-modal-consume");
            consumeModalButton.RegisterCallback<ClickEvent>(evt => _ = ConsumeItem());
            consumeModalCloseButton = rootElement.Q<Button>("consume-modal-close");
            consumeModalCloseButton.RegisterCallback<ClickEvent>(_ => consumeModal.style.display = DisplayStyle.None);

            // Remove modal
            removeModal = rootElement.Q<VisualElement>("remove-modal");
            removeQuantityField = rootElement.Q<IntegerField>("remove-modal-quantity");
            removeModalButton = rootElement.Q<Button>("remove-modal-remove");
            removeModalButton.RegisterCallback<ClickEvent>(evt => _ = RemoveItem());
            removeModalCloseButton = rootElement.Q<Button>("remove-modal-close");
            removeModalCloseButton.RegisterCallback<ClickEvent>(_ => removeModal.style.display = DisplayStyle.None);

            // Error popup
            errorPopup = rootElement.Q<VisualElement>("error-popup");
            errorMessage = rootElement.Q<Label>("error-message");
            errorCloseButton = rootElement.Q<Button>("error-close");
            errorCloseButton.RegisterCallback<ClickEvent>(_ => errorPopup.style.display = DisplayStyle.None);

            UpdateActionButtons();
        }

        private async Task LoadItemCodex()
        {
            try
            {
                codexItems.Clear();
                codexLookup.Clear();
                
                var items = await inventorySystem.GetItemCodexAsync();
                
                codexItems.AddRange(items);
                
                foreach (var item in items)
                {
                    codexLookup[item.Id] = item;
                }

                // Populate grant dropdown
                var itemNames = codexItems.Select(item => item.Name).ToList();
                grantItemDropdown.choices = itemNames;
            }
            catch (Exception e)
            {
                errorPopup.style.display = DisplayStyle.Flex;
                errorMessage.text = $"Failed to load item codex: {e.Message}";
            }
        }

        private void UpdateActionButtons()
        {
            var hasSelection = selectedItem != null;
            var isConsumable = hasSelection && selectedItem.Consumable;

            consumeButton.SetEnabled(isConsumable);
            removeButton.SetEnabled(hasSelection);
        }

        private void PopulateInventoryGrid()
        {
            inventoryGrid.Clear();
            Debug.Log(inventoryItems[0]);
            foreach (var item in inventoryItems)
            {
                // Instantiate the template
                var itemSlot = inventoryItemTemplate.Instantiate();
                var slotRoot = itemSlot.Q<VisualElement>("inventory-slot");

                // Store reference to item for selection tracking
                slotRoot.userData = item;

                // Set item icon
                var iconContainer = slotRoot.Q<VisualElement>("item-icon");
                SetItemIcon(iconContainer, item.Id);

                // Update quantity label
                var quantityLabel = slotRoot.Q<Label>("item-quantity");
                quantityLabel.text = $"x{item.Count}";

                // Get rarity and set background color
                var rarity = item.StringProperties.ContainsKey("rarity") 
                    ? item.StringProperties["rarity"].ToLower() 
                    : "common";
                
                Color rarityColor = GetRarityColor(rarity);
                slotRoot.style.backgroundColor = new StyleColor(rarityColor);

                Debug.Log(rarity);

                // Click to select
                slotRoot.RegisterCallback<ClickEvent>(evt =>
                {
                    SelectItem(item, slotRoot);
                });

                // Hover effects
                slotRoot.RegisterCallback<MouseEnterEvent>(evt =>
                {
                    if (selectedItem != item)
                    {
                        Color hoverColor = new Color(rarityColor.r, rarityColor.g, rarityColor.b, 0.8f);
                        slotRoot.style.backgroundColor = new StyleColor(hoverColor);
                        slotRoot.style.borderTopColor = new StyleColor(Color.white);
                        slotRoot.style.borderBottomColor = new StyleColor(Color.white);
                        slotRoot.style.borderLeftColor = new StyleColor(Color.white);
                        slotRoot.style.borderRightColor = new StyleColor(Color.white);
                    }
                    ShowTooltip(item, evt.mousePosition);
                });

                slotRoot.RegisterCallback<MouseLeaveEvent>(evt =>
                {
                    if (selectedItem != item)
                    {
                        slotRoot.style.backgroundColor = new StyleColor(rarityColor);
                        slotRoot.style.borderTopColor = new StyleColor(itemBoarderDefaultColor);
                        slotRoot.style.borderBottomColor = new StyleColor(itemBoarderDefaultColor);
                        slotRoot.style.borderLeftColor = new StyleColor(itemBoarderDefaultColor);
                        slotRoot.style.borderRightColor = new StyleColor(itemBoarderDefaultColor);
                    }
                    HideTooltip();
                });

                inventoryGrid.Add(slotRoot);
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
                _ => new Color(0.745f, 0.722f, 0.855f, 1.0f) // Default color
            };
        }
        
        private void SetItemIcon(VisualElement iconContainer, string itemId)
        {
            if (iconDictionary != null && iconDictionary.TryGetValue(itemId, out Sprite icon) && icon != null)
            {
                iconContainer.style.backgroundImage = new StyleBackground(icon);
            }
            else if (defaultIcon != null)
            {
                // Use default icon as fallback
                iconContainer.style.backgroundImage = new StyleBackground(defaultIcon);
            }
            else
            {
                // No icon found and no default - clear background
                iconContainer.style.backgroundImage = StyleKeyword.Null;
                Debug.LogWarning($"No icon mapping found for item ID: {itemId}");
            }
        }

        private void SelectItem(IInventoryItem item, VisualElement slot)
        {
            // Deselect previous slot - restore its original rarity color
            if (selectedSlot != null && selectedSlot != slot)
            {
                // Get the original item to retrieve its rarity
                var previousItem = selectedSlot.userData as IInventoryItem;
                if (previousItem != null)
                {
                    var rarity = previousItem.StringProperties.ContainsKey("rarity") 
                        ? previousItem.StringProperties["rarity"].ToLower() 
                        : "common";
                    Color rarityColor = GetRarityColor(rarity);
                    
                    // Restore original rarity color and remove border
                    selectedSlot.style.backgroundColor = new StyleColor(rarityColor);
                    selectedSlot.style.borderTopColor = new StyleColor(itemBoarderDefaultColor);
                    selectedSlot.style.borderBottomColor = new StyleColor(itemBoarderDefaultColor);
                    selectedSlot.style.borderLeftColor = new StyleColor(itemBoarderDefaultColor);
                    selectedSlot.style.borderRightColor = new StyleColor(itemBoarderDefaultColor);
                }
            }

            selectedItem = item;
            selectedSlot = slot;
            
            // Highlight selected slot with turquoise border (keeping the rarity background)
            slot.style.borderTopColor = new StyleColor(new Color(0.467f, 0.984f, 0.937f));
            slot.style.borderBottomColor = new StyleColor(new Color(0.467f, 0.984f, 0.937f));
            slot.style.borderLeftColor = new StyleColor(new Color(0.467f, 0.984f, 0.937f));
            slot.style.borderRightColor = new StyleColor(new Color(0.467f, 0.984f, 0.937f));
            
            UpdateActionButtons();
            
            Debug.Log($"Selected item: {item.Name}");
        }

        private void ShowTooltip(IInventoryItem item, Vector2 mousePosition)
        {
            tooltipNameLabel.text = item.Name;
            tooltipDescriptionLabel.text = string.IsNullOrEmpty(item.Description)
                ? "No description available."
                : item.Description;
            tooltipCategoryLabel.text = $"Category: {item.Category ?? "Uncategorized"}";
            tooltipQuantityLabel.text = $"Quantity: {item.Count}";

            // Display properties if any
            var propertiesText = "";
            if (item.StringProperties != null && item.StringProperties.Count > 0)
            {
                propertiesText += "String Properties:\n";
                foreach (var prop in item.StringProperties)
                {
                    propertiesText += $"  • {prop.Key}: {prop.Value}\n";
                }
            }
            if (item.NumericProperties != null && item.NumericProperties.Count > 0)
            {
                if (propertiesText.Length > 0) propertiesText += "\n";
                propertiesText += "Numeric Properties:\n";
                foreach (var prop in item.NumericProperties)
                {
                    propertiesText += $"  • {prop.Key}: {prop.Value}\n";
                }
            }
            tooltipPropertiesLabel.text = string.IsNullOrEmpty(propertiesText) 
                ? "No custom properties." 
                : propertiesText;

            // Position tooltip near the mouse cursor, offset to the right
            var offsetX = 20f;
            var offsetY = -20f;
            
            itemTooltip.style.left = mousePosition.x + offsetX;
            itemTooltip.style.top = mousePosition.y + offsetY;
            
            // Ensure tooltip stays within screen bounds
            var rootElement = GetComponent<UIDocument>().rootVisualElement;
            var tooltipWidth = 400f; // Updated to match new width
            var tooltipHeight = 450f; // Increased for larger content
            
            // Adjust if tooltip would go off right edge
            if (mousePosition.x + offsetX + tooltipWidth > rootElement.resolvedStyle.width)
            {
                itemTooltip.style.left = mousePosition.x - tooltipWidth - 20f;
            }
            
            // Adjust if tooltip would go off bottom edge
            if (mousePosition.y + offsetY + tooltipHeight > rootElement.resolvedStyle.height)
            {
                itemTooltip.style.top = rootElement.resolvedStyle.height - tooltipHeight - 20f;
            }

            itemTooltip.style.display = DisplayStyle.Flex;
        }

        private void HideTooltip()
        {
            itemTooltip.style.display = DisplayStyle.None;
        }

        #endregion

        #region Inventory Operations

        private async Task UpdateInventory()
        {
            inventoryItems.Clear();

            try
            {
                await inventorySystem.RefreshAsync();
                await economySystem.RefreshAsync();
                foreach (var item in inventorySystem.Items)
                {
                    Debug.Log($"Item {item.Id}: {item.Name} - {item.Description} - {item.Count}");
                }
                Debug.Log("Item Inventory Count: " + inventorySystem.Items.Count);
                inventoryItems.AddRange(inventorySystem.Items);
            }
            catch (Exception e)
            {
                errorPopup.style.display = DisplayStyle.Flex;
                errorMessage.text = e.Message;
                return;
            }

            PopulateInventoryGrid();
            
            // Clear selection after refresh
            selectedItem = null;
            selectedSlot = null;
            UpdateActionButtons();
        }

        private async Task GrantItem()
        {
            try
            {
                if (grantItemDropdown.index < 0 || grantItemDropdown.index >= codexItems.Count)
                {
                    throw new Exception("Please select a valid item.");
                }

                var selectedCodexItem = codexItems[grantItemDropdown.index];
                var quantity = grantQuantityField.value;

                if (quantity == 0)
                {
                    throw new Exception("Quantity cannot be 0.");
                }

                var items = new Dictionary<string, long>
                {
                    { selectedCodexItem.Id, quantity }
                };

                await inventorySystem.GrantItemsAsync(items);
            }
            catch (Exception e)
            {
                errorPopup.style.display = DisplayStyle.Flex;
                errorMessage.text = e.Message;
                return;
            }

            grantModal.style.display = DisplayStyle.None;
            _ = UpdateInventory();
        }

        private async Task ConsumeItem()
        {
            if (selectedItem == null) return;

            try
            {
                var quantity = consumeQuantityField.value;

                if (quantity <= 0)
                {
                    throw new Exception("Quantity must be greater than 0.");
                }

                var items = new Dictionary<string, long>
                {
                    { selectedItem.Id, quantity }
                };
                var instances = new Dictionary<string, long>{};

                var overconsume = consumeOverconsumeToggle.value;

                await inventorySystem.ConsumeItemsAsync(items, instances, overconsume);
                await economySystem.RefreshAsync();

            }
            catch (Exception e)
            {
                errorPopup.style.display = DisplayStyle.Flex;
                errorMessage.text = e.Message;
                Debug.LogError(e);
                return;
            }

            consumeModal.style.display = DisplayStyle.None;
            selectedItem = null;
            _ = UpdateInventory();
        }

        private async Task RemoveItem()
        {
            if (selectedItem == null) return;

            try
            {
                var quantity = removeQuantityField.value;

                if (quantity <= 0)
                {
                    throw new Exception("Quantity must be greater than 0.");
                }

                // Remove items using negative grant
                var items = new Dictionary<string, long>
                {
                    { selectedItem.Id, -quantity }
                };

                await inventorySystem.GrantItemsAsync(items);
            }
            catch (Exception e)
            {
                errorPopup.style.display = DisplayStyle.Flex;
                errorMessage.text = e.Message;
                return;
            }

            removeModal.style.display = DisplayStyle.None;
            selectedItem = null;
            _ = UpdateInventory();
        }

        #endregion
    }
}