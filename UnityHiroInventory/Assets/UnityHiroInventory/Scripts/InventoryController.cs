using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Hiro;

namespace HiroInventory
{
    /// <summary>
    /// Controller for the Inventory system.
    /// Handles business logic and coordinates with Hiro systems.
    /// Plain C# class for testability - no MonoBehaviour inheritance.
    /// </summary>
    public class InventoryController
    {
        private const int MaxInventorySize = 12;

        private readonly NakamaSystem _nakamaSystem;
        private readonly IInventorySystem _inventorySystem;
        private readonly IEconomySystem _economySystem;

        private IInventoryItem _selectedItem;

        public string CurrentUserId => _nakamaSystem.UserId;
        public List<IInventoryItem> InventoryItems { get; } = new();
        public List<IInventoryItem> CodexItems { get; } = new();
        public Dictionary<string, IInventoryItem> CodexLookup { get; } = new();

        public InventoryController(
            NakamaSystem nakamaSystem,
            IInventorySystem inventorySystem,
            IEconomySystem economySystem)
        {
            _nakamaSystem = nakamaSystem ?? throw new ArgumentNullException(nameof(nakamaSystem));
            _inventorySystem = inventorySystem ?? throw new ArgumentNullException(nameof(inventorySystem));
            _economySystem = economySystem ?? throw new ArgumentNullException(nameof(economySystem));
        }

        #region Item Codex

        public async Task LoadItemCodexAsync()
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

        public async Task<List<IInventoryItem>> RefreshInventoryAsync()
        {
            InventoryItems.Clear();

            await _inventorySystem.RefreshAsync();
            await _economySystem.RefreshAsync();

            InventoryItems.AddRange(_inventorySystem.Items);

            return InventoryItems;
        }

        public async Task SwitchCompleteAsync()
        {
            _selectedItem = null;
            await _economySystem.RefreshAsync();
        }

        public void SelectItem(IInventoryItem item)
        {
            _selectedItem = item;
        }

        public IInventoryItem GetSelectedItem()
        {
            return _selectedItem;
        }

        #endregion

        #region Item Actions

        public async Task GrantItemAsync(int codexIndex, int quantity)
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
                IInventoryItem existingStack = null;
                foreach (var i in InventoryItems)
                {
                    if (i.Id == item.Id)
                    {
                        existingStack = i;
                        break;
                    }
                }

                if (existingStack != null)
                {
                    var newTotal = existingStack.Count + quantityToAdd;
                    if (newTotal > item.MaxCount)
                    {
                        throw new InvalidOperationException($"You've reached the maximum amount of {item.Name} of {item.MaxCount}");
                    }
                }
            }
            else
            {
                // For non-stackable items, count total instances
                int currentInstanceCount = 0;
                foreach (var i in InventoryItems)
                {
                    if (i.Id == item.Id)
                    {
                        currentInstanceCount++;
                    }
                }

                var newInstanceCount = currentInstanceCount + quantityToAdd;

                if (newInstanceCount > item.MaxCount)
                {
                    throw new InvalidOperationException($"You've reached the maximum amount of {item.Name} of {item.MaxCount}");
                }
            }
        }

        /// <summary>
        /// Validates that granting items won't exceed the inventory's max limit.
        /// Throws InvalidOperationException with "INVENTORY_FULL" if inventory is full.
        /// </summary>
        private void ValidateInventorySlots(IInventoryItem item, long quantityToAdd)
        {
            IInventoryItem existingItem = null;
            foreach (var i in InventoryItems)
            {
                if (i.Id == item.Id)
                {
                    existingItem = i;
                    break;
                }
            }

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

        public async Task ConsumeItemAsync(int quantity, bool overconsume)
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

        public async Task RemoveItemAsync(int quantity)
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
