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
    public class StoreItemIconMapping
    {
        public string itemId;
        public Sprite icon;
    }

    [System.Serializable]
    public class CurrencyIconMapping
    {
        public string currencyCode;
        public Sprite icon;
    }

    [RequireComponent(typeof(UIDocument))]
    public class StoreController : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private VisualTreeAsset storeItemTemplate;
        [SerializeField] private StoreItemIconMapping[] itemIconMappings;
        [SerializeField] private CurrencyIconMapping[] currencyIconMappings;
        [SerializeField] private Sprite defaultItemIcon;

        private NakamaSystem _nakamaSystem;
        private IEconomySystem _economySystem;
        private IInventorySystem _inventorySystem;

        private StoreView _view;

        public string CurrentUserId => _nakamaSystem.UserId;
        public List<IEconomyListStoreItem> StoreItems { get; } = new();
        public Dictionary<string, Sprite> ItemIconDictionary { get; private set; }
        public Dictionary<string, Sprite> CurrencyIconDictionary { get; private set; }

        public event Action<ISession, StoreController> OnInitialized;
        public event Action<IEconomyListStoreItem> OnItemSelected;

        public enum StoreTab
        {
            Deals,
            Featured,
            Resources
        }

        private StoreTab _currentTab = StoreTab.Featured;
        private EconomyListStoreItem _selectedItem;
        private EconomyListStoreItem _featuredItem;

        #region Initialization

        private void Start()
        {
            BuildIconDictionaries();

            var inventoryCoordinator = HiroCoordinator.Instance as HiroInventoryCoordinator;
            if (inventoryCoordinator == null) return;

            inventoryCoordinator.ReceivedStartError += HandleStartError;
            inventoryCoordinator.ReceivedStartSuccess += HandleStartSuccess;

            _view = new StoreView(this, inventoryCoordinator, storeItemTemplate, defaultItemIcon);
        }

        public void SwitchComplete()
        {
            _ = RefreshStore();
        }

        private void BuildIconDictionaries()
        {
            ItemIconDictionary = new Dictionary<string, Sprite>();
            if (itemIconMappings != null)
            {
                foreach (var mapping in itemIconMappings)
                {
                    if (!string.IsNullOrEmpty(mapping.itemId) && mapping.icon != null)
                    {
                        ItemIconDictionary[mapping.itemId] = mapping.icon;
                    }
                }
            }

            CurrencyIconDictionary = new Dictionary<string, Sprite>();
            if (currencyIconMappings != null)
            {
                foreach (var mapping in currencyIconMappings)
                {
                    if (!string.IsNullOrEmpty(mapping.currencyCode) && mapping.icon != null)
                    {
                        CurrencyIconDictionary[mapping.currencyCode] = mapping.icon;
                    }
                }
            }
        }

        private void HandleStartError(Exception e)
        {
            Debug.LogException(e);
            _view.ShowError(e.Message);
        }

        private async void HandleStartSuccess(ISession session)
        {
            _nakamaSystem = this.GetSystem<NakamaSystem>();
            _economySystem = this.GetSystem<EconomySystem>();
            _inventorySystem = this.GetSystem<InventorySystem>();

            _view.StartObservingWallet();

            await RefreshStore();

            OnInitialized?.Invoke(session, this);
        }

        #endregion

        #region Store Operations

        public async Task RefreshStore()
        {
            try
            {
                await _economySystem.RefreshStoreAsync();
                await _economySystem.RefreshAsync();

                StoreItems.Clear();
                StoreItems.AddRange(_economySystem.StoreItems);

                Debug.Log($"Loaded {StoreItems.Count} store items");

                // Find featured item (you can customize this logic)
                _featuredItem = (EconomyListStoreItem)StoreItems.FirstOrDefault(item => 
                    item.Category == "featured" || item.Name.ToLower().Contains("starter"));

                await _view.RefreshStoreDisplay();
            }
            catch (Exception e)
            {
                Debug.LogException(e);
                _view.ShowError($"Failed to refresh store: {e.Message}");
            }
        }

        public List<IEconomyListStoreItem> GetItemsForCurrentTab()
        {
            return _currentTab switch
            {
                StoreTab.Deals => StoreItems.Where(item => 
                    item.Category == "deals" || item.Cost.Currencies.Count == 0 && string.IsNullOrEmpty(item.Cost.Sku)).ToList(),
                StoreTab.Featured => StoreItems.Where(item => 
                    item.Category == "featured" || item != _featuredItem).ToList(),
                StoreTab.Resources => StoreItems.Where(item => 
                    item.Category == "resources" || item.Category == "currency").ToList(),
                _ => StoreItems.ToList()
            };
        }

        public void SwitchTab(StoreTab tab)
        {
            _currentTab = tab;
            _ = _view.RefreshStoreDisplay();
        }

        public StoreTab GetCurrentTab() => _currentTab;

        public EconomyListStoreItem GetFeaturedItem() => _featuredItem;

        #endregion

        #region Purchase Operations

        public void SelectItem(EconomyListStoreItem item)
        {
            _selectedItem = item;
            OnItemSelected?.Invoke(item);
            Debug.Log($"Selected store item: {item?.Name ?? "None"}");
        }

        public EconomyListStoreItem GetSelectedItem() => _selectedItem;

        public async Task<IEconomyPurchaseAck> PurchaseItem(IEconomyListStoreItem item)
        {
            if (item == null)
                throw new Exception("No item selected");

            try
            {
                IEconomyPurchaseAck result;

                // Check if this is a real money purchase (has SKU)
                if (!string.IsNullOrEmpty(item.Cost.Sku))
                {
                    // This would integrate with Unity IAP or your payment system
                    // For now, we'll make a purchase intent
                    await _economySystem.PurchaseIntentAsync(item);
                    Debug.Log($"Purchase intent created for {item.Name} with SKU: {item.Cost.Sku}");
                    
                    // In a real implementation, you'd handle the IAP flow here
                    // and then call PurchaseStoreItemAsync with the receipt
                    throw new Exception("Real money purchases require IAP integration");
                }
                else
                {
                    // Soft currency purchase
                    result = await _economySystem.PurchaseStoreItemAsync(item.Id);
                    Debug.Log($"Purchased {item.Name} successfully");
                }

                // Refresh economy and inventory
                await _economySystem.RefreshAsync();

                return result;
            }
            catch (Exception e)
            {
                Debug.LogError($"Purchase failed: {e.Message}");
                throw;
            }
        }

        public bool CanAffordItem(IEconomyListStoreItem item)
        {
            if (item == null) return false;

            // Check if it's a real money purchase
            if (!string.IsNullOrEmpty(item.Cost.Sku))
            {
                return true; // Can always attempt real money purchase
            }

            // Check if user has enough of each required currency
            foreach (var cost in item.Cost.Currencies)
            {
                if (!_economySystem.Wallet.TryGetValue(cost.Key, out var userAmount))
                {
                    return false; // User doesn't have this currency
                }

                // Parse the cost value from string to long
                if (!long.TryParse(cost.Value, out long requiredAmount))
                {
                    return false; // Invalid cost value
                }

                if (userAmount < requiredAmount)
                {
                    return false; // Not enough of this currency
                }
            }

            return true;
        }

        #endregion

        #region Helper Methods

        public string GetPrimaryCurrency(EconomyListStoreItem item)
        {
            // Return the first currency code or "USD" for SKU items
            if (!string.IsNullOrEmpty(item.Cost.Sku))
                return "USD";

            return item.Cost.Currencies.FirstOrDefault().Key ?? "";
        }

        public long GetPrimaryCurrencyAmount(IEconomyListStoreItem item)
        {
            if (!string.IsNullOrEmpty(item.Cost.Sku))
                return 0; // Price is in the SKU

            var firstCurrency = item.Cost.Currencies.FirstOrDefault();
            if (string.IsNullOrEmpty(firstCurrency.Value))
                return 0;

            // Parse the string value to long
            if (long.TryParse(firstCurrency.Value, out long amount))
                return amount;

            return 0;
        }

        public Sprite GetItemIcon(string itemId)
        {
            if (ItemIconDictionary != null && ItemIconDictionary.TryGetValue(itemId, out Sprite icon))
                return icon;
            return defaultItemIcon;
        }

        public Sprite GetCurrencyIcon(string currencyCode)
        {
            if (CurrencyIconDictionary != null && CurrencyIconDictionary.TryGetValue(currencyCode, out Sprite icon))
                return icon;
            return null;
        }

        #endregion
    }
}