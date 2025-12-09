using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Hiro;
using Hiro.Unity;
using Nakama;
using UnityEngine;
using UnityEngine.UIElements;

namespace HiroStore
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

        private StoreView _view;

        public string CurrentUserId => _nakamaSystem.UserId;
        private List<IEconomyListStoreItem> StoreItems { get; } = new();
        private Dictionary<string, Sprite> ItemIconDictionary { get; set; }
        private Dictionary<string, Sprite> CurrencyIconDictionary { get; set; }

        public event Action<ISession, StoreController> OnInitialized;
        public event Action<IEconomyListStoreItem> OnItemSelected;

        public enum StoreTab
        {
            Currency,
            Items
        }

        private StoreTab _currentTab = StoreTab.Currency;
        private EconomyListStoreItem _selectedItem;

        #region Initialization

        private void Start()
        {
            BuildIconDictionaries();

            var storeCoordinator = HiroCoordinator.Instance as HiroStoreCoordinator;
            if (!storeCoordinator) return;

            storeCoordinator.ReceivedStartError += HandleStartError;
            storeCoordinator.ReceivedStartSuccess += HandleStartSuccess;

            _view = new StoreView(this, storeCoordinator, storeItemTemplate, defaultItemIcon);
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
                    if (!string.IsNullOrEmpty(mapping.itemId) && mapping.icon)
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
                    if (!string.IsNullOrEmpty(mapping.currencyCode) && mapping.icon)
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

                await _view.RefreshStoreDisplay();
            }
            catch (Exception e)
            {
                Debug.LogException(e);
                _view.ShowError($"Failed to refresh store: {e.Message}");
            }
        }

        public List<IEconomyListStoreItem> GetItemsForCategory(string category)
        {
            return StoreItems.Where(item =>
                item.Category == category && !IsFeaturedItem(item))
                .OrderBy(item => item.Name)
                .ThenBy(GetPrimaryCurrencyAmount)
                .ToList();
        }

        private bool IsFeaturedItem(IEconomyListStoreItem item)
        {
            return item.AdditionalProperties.TryGetValue("featured", out var value) &&
                   value.Equals("true", StringComparison.OrdinalIgnoreCase);
        }

        public void SwitchTab(StoreTab tab)
        {
            _currentTab = tab;
            _ = _view.RefreshStoreDisplay();
        }

        public StoreTab GetCurrentTab() => _currentTab;

        public string GetCurrentCategory()
        {
            return _currentTab switch
            {
                StoreTab.Currency => "currency",
                StoreTab.Items => "items",
                _ => ""
            };
        }

        public EconomyListStoreItem GetFeaturedItemForCategory(string category)
        {
            return (EconomyListStoreItem)StoreItems.FirstOrDefault(item =>
                item.Category == category && IsFeaturedItem(item));
        }

        public string GetItemTheme(IEconomyListStoreItem item)
        {
            if (item?.AdditionalProperties != null &&
                item.AdditionalProperties.TryGetValue("theme", out var theme))
            {
                return theme;
            }
            return "primary";
        }

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
                var result = await _economySystem.PurchaseStoreItemAsync(item.Id);
                Debug.Log($"Purchased {item.Name} successfully");

                // Refresh economy
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
            return item.Cost.Currencies.FirstOrDefault().Key ?? "";
        }

        public long GetPrimaryCurrencyAmount(IEconomyListStoreItem item)
        {
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