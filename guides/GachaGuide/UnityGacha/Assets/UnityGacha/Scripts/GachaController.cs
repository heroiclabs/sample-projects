using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Hiro;

namespace UnityGacha
{
    public class GachaController
    {
        private const int MaxInventorySize = 999;

        private readonly NakamaSystem _nakamaSystem;
        private readonly IInventorySystem _inventorySystem;
        private readonly IEconomySystem _economySystem;
        private readonly IStatsSystem _statsSystem;

        private IInventoryItem _selectedItem;

        public string CurrentUserId => _nakamaSystem.UserId;
        public List<IInventoryItem> InventoryItems { get; } = new();
        public List<IInventoryItem> CodexItems { get; } = new();
        public Dictionary<string, IInventoryItem> CodexLookup { get; } = new();

        public GachaController(
            NakamaSystem nakamaSystem,
            IInventorySystem inventorySystem,
            IEconomySystem economySystem,
            IStatsSystem statsSystem)
        {
            _nakamaSystem = nakamaSystem ?? throw new ArgumentNullException(nameof(nakamaSystem));
            _inventorySystem = inventorySystem ?? throw new ArgumentNullException(nameof(inventorySystem));
            _economySystem = economySystem ?? throw new ArgumentNullException(nameof(economySystem));
            _statsSystem = statsSystem ?? throw new ArgumentNullException(nameof(statsSystem));
        }

        #region Item Codex

        public async Task LoadItemCodexAsync()
        {
            CodexItems.Clear();
            CodexLookup.Clear();

            var items = await _inventorySystem.GetItemCodexAsync();

            CodexItems.AddRange(items);

            foreach (var item in items)
                CodexLookup[item.Id] = item;
        }

        #endregion

        #region Inventory Operations

        public async Task RefreshInventoryAsync()
        {
            await _inventorySystem.RefreshAsync();

            InventoryItems.Clear();
            InventoryItems.AddRange(_inventorySystem.Items);
        }

        public async Task RefreshSystemsAsync()
        {
            await Task.WhenAll(
                _inventorySystem.RefreshAsync(),
                _economySystem.RefreshAsync(),
                _statsSystem.RefreshAsync());

            InventoryItems.Clear();
            InventoryItems.AddRange(_inventorySystem.Items);
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
            if (item.MaxCount == 0)
                return;

            if (item.Stackable)
            {
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
                        throw new InvalidOperationException($"You've reached the maximum amount of {item.Name} of {item.MaxCount}");
                }
            }
            else
            {
                int currentInstanceCount = 0;
                foreach (var i in InventoryItems)
                {
                    if (i.Id == item.Id)
                        currentInstanceCount++;
                }

                var newInstanceCount = currentInstanceCount + quantityToAdd;

                if (newInstanceCount > item.MaxCount)
                    throw new InvalidOperationException($"You've reached the maximum amount of {item.Name} of {item.MaxCount}");
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
                var needsNewSlot = existingItem == null;
                if (needsNewSlot && InventoryItems.Count >= MaxInventorySize)
                    throw new InvalidOperationException("INVENTORY_FULL");
            }
            else
            {
                var slotsNeeded = (int)quantityToAdd;
                var slotsAvailable = MaxInventorySize - InventoryItems.Count;

                if (slotsNeeded > slotsAvailable)
                    throw new InvalidOperationException("INVENTORY_FULL");
            }
        }

        /// <summary>
        /// Consumes the selected item. Returns the won reward item ID, or null if no item is
        /// selected or the consume yielded no reward.
        /// Call RefreshSystemsAsync after this to update inventory, economy, and stats.
        /// </summary>
        public async Task<string> ConsumeItemAsync(int quantity, bool overConsume)
        {
            if (_selectedItem == null) return null;

            if (quantity <= 0)
                throw new Exception("Quantity must be greater than 0.");

            var items = new Dictionary<string, long>
            {
                { _selectedItem.Id, quantity }
            };

            var result = await _inventorySystem.ConsumeItemsAsync(items, new Dictionary<string, long>(), overConsume);

            _selectedItem = null;

            return result.Values.FirstOrDefault()?.Rewards.FirstOrDefault()?.Items.Keys.FirstOrDefault();
        }

        /// <summary>
        /// Consumes 10 of the selected ticket item. Returns the list of won reward item IDs.
        /// Returns an empty list if no item is selected or the consume yielded no rewards.
        /// Call RefreshSystemsAsync after this to update inventory, economy, and stats.
        /// </summary>
        public async Task<string[]> ConsumeTenItemsAsync()
        {
            if (_selectedItem == null) return Array.Empty<string>();

            var items = new Dictionary<string, long>
            {
                { _selectedItem.Id, 10 }
            };

            var result = await _inventorySystem.ConsumeItemsAsync(items, new Dictionary<string, long>(), false);

            _selectedItem = null;

            return result.Values
                .FirstOrDefault()
                ?.Rewards.FirstOrDefault()
                ?.Items
                .SelectMany(kvp => Enumerable.Repeat(kvp.Key, int.Parse(kvp.Value)))
                .OrderBy(_ => Guid.NewGuid())
                .ToArray();
        }

        public async Task RemoveItemAsync(int quantity)
        {
            if (_selectedItem == null) return;

            if (quantity <= 0)
                throw new Exception("Quantity must be greater than 0.");

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