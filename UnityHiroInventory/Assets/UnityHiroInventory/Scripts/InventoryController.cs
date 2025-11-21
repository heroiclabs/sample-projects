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

            var selectedCodexItem = CodexItems[codexIndex];

            // Check if granting would exceed inventory limit 
            if (quantity > 0)
            {
                var existingItem = InventoryItems.FirstOrDefault(i => i.Id == selectedCodexItem.Id);

                // For non-stackable items: Each item creates a new instance
                // For stackable items: Grant increases the count on a single instance
                if (!selectedCodexItem.Stackable)
                {
                    // Non-stackable: Each quantity = new slot
                    int newSlotsNeeded = (int)quantity;
                    if (InventoryItems.Count + newSlotsNeeded > MaxInventorySize)
                    {
                        throw new InvalidOperationException("INVENTORY_FULL");
                    }
                }
                else
                {
                    // Stackable: Only need new slot if item doesn't exist
                    if (existingItem == null && InventoryItems.Count >= MaxInventorySize)
                    {
                        throw new InvalidOperationException("INVENTORY_FULL");
                    }
                }
            }

            var items = new Dictionary<string, long>
            {
                { selectedCodexItem.Id, quantity }
            };

            await _inventorySystem.GrantItemsAsync(items);
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