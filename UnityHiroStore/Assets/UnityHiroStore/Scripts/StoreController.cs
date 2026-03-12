using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Hiro;
using UnityEngine;

namespace HiroStore
{
    /// <summary>
    /// Controller for the Store system.
    /// Handles business logic and coordinates with Hiro systems.
    /// Plain C# class for testability - no MonoBehaviour inheritance.
    /// </summary>
    public class StoreController
    {
        private readonly NakamaSystem _nakamaSystem;
        private readonly IEconomySystem _economySystem;
        private readonly Dictionary<string, Sprite> _itemIconDictionary;
        private readonly Dictionary<string, Sprite> _currencyIconDictionary;
        private readonly Sprite _defaultItemIcon;

        private EconomyListStoreItem _selectedItem;

        public string CurrentUserId => _nakamaSystem.UserId;
        public List<IEconomyListStoreItem> StoreItems { get; } = new();
        public IReadOnlyDictionary<string, long> Wallet => _economySystem.Wallet;

        public enum StoreTab
        {
            Currency,
            Items
        }

        private StoreTab _currentTab = StoreTab.Currency;

        public event Action<IEconomyListStoreItem> OnItemSelected;

        public StoreController(
            NakamaSystem nakamaSystem,
            IEconomySystem economySystem,
            Dictionary<string, Sprite> itemIconDictionary,
            Dictionary<string, Sprite> currencyIconDictionary,
            Sprite defaultItemIcon)
        {
            _nakamaSystem = nakamaSystem ?? throw new ArgumentNullException(nameof(nakamaSystem));
            _economySystem = economySystem ?? throw new ArgumentNullException(nameof(economySystem));
            _itemIconDictionary = itemIconDictionary ?? new Dictionary<string, Sprite>();
            _currencyIconDictionary = currencyIconDictionary ?? new Dictionary<string, Sprite>();
            _defaultItemIcon = defaultItemIcon;
        }

        #region Store Operations

        public async Task RefreshStoreAsync()
        {
            await _economySystem.RefreshStoreAsync();
            await _economySystem.RefreshAsync();

            StoreItems.Clear();
            StoreItems.AddRange(_economySystem.StoreItems);
        }

        public async Task SwitchCompleteAsync()
        {
            _selectedItem = null;
            await RefreshStoreAsync();
        }

        public List<IEconomyListStoreItem> GetItemsForCategory(string category)
        {
            var result = new List<IEconomyListStoreItem>();
            foreach (var item in StoreItems)
            {
                if (item.Category == category && !IsFeaturedItem(item))
                {
                    result.Add(item);
                }
            }

            // Sort by item prefix then by primary currency amount
            result.Sort((a, b) =>
            {
                var aPrefix = a.Id.Split('_')[0];
                var bPrefix = b.Id.Split('_')[0];
                var prefixCompare = string.Compare(aPrefix, bPrefix, StringComparison.Ordinal);
                if (prefixCompare != 0) return prefixCompare;
                return GetPrimaryCurrencyAmount(a).CompareTo(GetPrimaryCurrencyAmount(b));
            });

            return result;
        }

        private bool IsFeaturedItem(IEconomyListStoreItem item)
        {
            return item.AdditionalProperties.TryGetValue("featured", out var value) &&
                   value.Equals("true", StringComparison.OrdinalIgnoreCase);
        }

        public void SwitchTab(StoreTab tab)
        {
            _currentTab = tab;
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
            foreach (var item in StoreItems)
            {
                if (item.Category == category && IsFeaturedItem(item))
                {
                    return (EconomyListStoreItem)item;
                }
            }
            return null;
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
        }

        public EconomyListStoreItem GetSelectedItem() => _selectedItem;

        public async Task<IEconomyPurchaseAck> PurchaseItemAsync(IEconomyListStoreItem item)
        {
            if (item == null)
                throw new Exception("No item selected");

            var result = await _economySystem.PurchaseStoreItemAsync(item.Id);
            return result;
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
            foreach (var currency in item.Cost.Currencies)
            {
                return currency.Key;
            }
            return "";
        }

        public long GetPrimaryCurrencyAmount(IEconomyListStoreItem item)
        {
            foreach (var currency in item.Cost.Currencies)
            {
                if (!string.IsNullOrEmpty(currency.Value) && long.TryParse(currency.Value, out long amount))
                {
                    return amount;
                }
                break;
            }
            return 0;
        }

        public Sprite GetItemIcon(string itemId)
        {
            if (_itemIconDictionary != null && _itemIconDictionary.TryGetValue(itemId, out Sprite icon))
                return icon;
            return _defaultItemIcon;
        }

        public Sprite GetCurrencyIcon(string currencyCode)
        {
            if (_currencyIconDictionary != null && _currencyIconDictionary.TryGetValue(currencyCode, out Sprite icon))
                return icon;
            return null;
        }

        #endregion
    }
}
