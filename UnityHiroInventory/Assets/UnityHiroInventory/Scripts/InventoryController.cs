using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Hiro;
using Hiro.Unity;
using Nakama;
using UnityEngine;
using UnityEngine.UIElements;

namespace HiroInventory
{
    [System.Serializable]
    public class ItemIconMapping
    {
        public string itemId;
        public Sprite icon;
    }

    [RequireComponent(typeof(UIDocument))]
    public class InventoryController : MonoBehaviour
    {

        [Header("References")]
        [SerializeField] private VisualTreeAsset inventoryItemTemplate;
        [SerializeField] private ItemIconMapping[] itemIconMappings;
        [SerializeField] private Sprite defaultIcon;

        private NakamaSystem _nakamaSystem;
        private IInventorySystem _inventorySystem;
        private IEconomySystem _economySystem;
        private IInventoryItem _selectedItem;

        private InventoryView _view;

        public string CurrentUserId => _nakamaSystem.UserId;
        public List<IInventoryItem> InventoryItems { get; } = new();
        public List<IInventoryItem> CodexItems { get; } = new();
        public Dictionary<string, IInventoryItem> CodexLookup { get; } = new();
        public Dictionary<string, Sprite> IconDictionary { get; private set; }
        private const int MaxInventorySize = 12;

        public event Action<ISession, InventoryController> OnInitialized;

        #region Initialization

        private void Start()
        {
            // Build the icon dictionary from the mappings
            IconDictionary = new Dictionary<string, Sprite>();
            if (itemIconMappings != null)
            {
                foreach (var mapping in itemIconMappings)
                {
                    if (!string.IsNullOrEmpty(mapping.itemId) && mapping.icon != null)
                    {
                        IconDictionary[mapping.itemId] = mapping.icon;
                    }
                }
            }

            var inventoryCoordinator = HiroCoordinator.Instance as HiroInventoryCoordinator;
            if (inventoryCoordinator == null) return;

            inventoryCoordinator.ReceivedStartError += HandleStartError;
            inventoryCoordinator.ReceivedStartSuccess += HandleStartSuccess;

            _view = new InventoryView(this, inventoryCoordinator, inventoryItemTemplate, defaultIcon);
        }

        private void HandleStartError(Exception e)
        {
            Debug.LogException(e);
            _view.ShowError(e.Message);
        }

        private async void HandleStartSuccess(ISession session)
        {
            // Cache Hiro systems
            _nakamaSystem = this.GetSystem<NakamaSystem>();
            _inventorySystem = this.GetSystem<InventorySystem>();
            _economySystem = this.GetSystem<EconomySystem>();

            _view.StartObservingWallet();

            await LoadItemCodex();
            await _view.RefreshInventory();

            OnInitialized?.Invoke(session, this);
        }

        public void SwitchComplete()
        {
            _view.HideAllModals();
            _ = _view.RefreshInventory();
            _ = _economySystem.RefreshAsync();
        }

        #endregion

        #region Item Codex

        public async Task LoadItemCodex()
        {
            CodexItems.Clear();
            CodexLookup.Clear();

            var items = await _inventorySystem.GetItemCodexAsync();

            CodexItems.AddRange(items);

            foreach (var item in items)
            {
                CodexLookup[item.Id] = item;
            }
        }

        #endregion

        #region Inventory Operations

        public async Task<List<IInventoryItem>> RefreshInventory()
        {
            InventoryItems.Clear();

            await _inventorySystem.RefreshAsync();
            await _economySystem.RefreshAsync();

            foreach (var item in _inventorySystem.Items)
            {
                Debug.Log($"Item {item.Id}: {item.Name} - {item.Description} - {item.Count}");
            }

            Debug.Log("Item Inventory Count: " + _inventorySystem.Items.Count);
            InventoryItems.AddRange(_inventorySystem.Items);

            return InventoryItems;
        }

        public void SelectItem(IInventoryItem item)
        {
            _selectedItem = item;
            Debug.Log($"Selected item: {item?.Name ?? "None"}");
        }

        public IInventoryItem GetSelectedItem()
        {
            return _selectedItem;
        }

        #endregion

        #region Item Actions

        public async Task GrantItem(int codexIndex, int quantity)
        {
            if (codexIndex < 0 || codexIndex >= CodexItems.Count)
                throw new Exception("Please select a valid item.");

            if (quantity == 0)
                throw new Exception("Quantity cannot be 0.");

            var selectedItem = CodexItems[codexIndex];

            // Only validate limits when adding items (positive quantity)
            if (quantity > 0)
            {
                ValidateItemMaxCount(selectedItem, quantity);
                ValidateInventorySlots(selectedItem, quantity);
            }

            var items = new Dictionary<string, long>
            {
                { selectedItem.Id, quantity }
            };

            await _inventorySystem.GrantItemsAsync(items);
        }

        /// <summary>
        /// Validates that granting items won't exceed the item's MaxCount limit.
        /// Throws InvalidOperationException with "MAX_COUNT_REACHED" if limit would be exceeded.
        /// </summary>
        private void ValidateItemMaxCount(IInventoryItem item, long quantityToAdd)
        {
            // Items with MaxCount of 0 have unlimited capacity
            if (item.MaxCount == 0)
                return;

            if (item.Stackable)
            {
                // For stackable items, check if adding to existing stack exceeds max
                var existingStack = InventoryItems.FirstOrDefault(i => i.Id == item.Id);
                if (existingStack != null)
                {
                    var newTotal = existingStack.Count + quantityToAdd;
                    if (newTotal > item.MaxCount)
                    {
                        throw new InvalidOperationException($"MAX_COUNT_REACHED:{item.Name}:{item.MaxCount}");
                    }
                }
            }
            else
            {
                // For non-stackable items, count total instances
                var currentInstanceCount = InventoryItems.Count(i => i.Id == item.Id);
                var newInstanceCount = currentInstanceCount + quantityToAdd;

                if (newInstanceCount > item.MaxCount)
                {
                    throw new InvalidOperationException($"MAX_COUNT_REACHED");
                }
            }
        }

        /// <summary>
        /// Validates that granting items won't exceed the inventory's max limit.
        /// Throws InvalidOperationException with "INVENTORY_FULL" if inventory is full.
        /// </summary>
        private void ValidateInventorySlots(IInventoryItem item, long quantityToAdd)
        {
            var existingItem = InventoryItems.FirstOrDefault(i => i.Id == item.Id);

            if (item.Stackable)
            {
                // Stackable items only need a slot if this is a new item type
                var needsNewSlot = existingItem == null;
                if (needsNewSlot && InventoryItems.Count >= MaxInventorySize)
                {
                    throw new InvalidOperationException("INVENTORY_FULL");
                }
            }
            else
            {
                // Non-stackable items: each item needs its own slot
                var slotsNeeded = (int)quantityToAdd;
                var slotsAvailable = MaxInventorySize - InventoryItems.Count;

                if (slotsNeeded > slotsAvailable)
                {
                    throw new InvalidOperationException("INVENTORY_FULL");
                }
            }
        }

        public async Task ConsumeItem(int quantity, bool overconsume)
        {
            if (_selectedItem == null) return;

            if (quantity <= 0)
                throw new Exception("Quantity must be greater than 0.");

            var items = new Dictionary<string, long>
            {
                { _selectedItem.Id, quantity }
            };
            var instances = new Dictionary<string, long> { };

            await _inventorySystem.ConsumeItemsAsync(items, instances, overconsume);
            await _economySystem.RefreshAsync();

            _selectedItem = null;
        }

        public async Task RemoveItem(int quantity)
        {
            if (_selectedItem == null) return;

            if (quantity <= 0)
                throw new Exception("Quantity must be greater than 0.");

            // Remove items using negative grant
            var items = new Dictionary<string, long>
            {
                { _selectedItem.Id, -quantity }
            };

            await _inventorySystem.GrantItemsAsync(items);

            _selectedItem = null;
        }

        #endregion
    }
}