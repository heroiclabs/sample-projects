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
        /// <summary>
        /// The Satori feature flag the server-side SatoriPersonalizer merges onto the economy config.
        /// </summary>
        public const string EconomyFlagName = "Hiro-Economy";

        /// <summary>
        /// Store item additional property naming the Satori live event the item belongs to.
        /// Items carrying this property only exist while their live event is active.
        /// </summary>
        public const string SeasonalEventProperty = "event_id";

        /// <summary>
        /// Store item additional property carrying the pre-discount price, set on flag variants
        /// so the UI can render a was/now price.
        /// </summary>
        public const string OriginalCostProperty = "original_cost";

        private readonly NakamaSystem _nakamaSystem;
        private readonly IEconomySystem _economySystem;
        private readonly ISatoriSystem _satoriSystem;
        private readonly Dictionary<string, Sprite> _itemIconDictionary;
        private readonly Dictionary<string, Sprite> _currencyIconDictionary;
        private readonly Sprite _defaultItemIcon;

        private EconomyListStoreItem _selectedItem;

        public string CurrentUserId => _nakamaSystem.UserId;
        public List<IEconomyListStoreItem> StoreItems { get; } = new();
        public IReadOnlyDictionary<string, long> Wallet => _economySystem.Wallet;

        /// <summary>
        /// True when the last store refresh detected the player moved into or out of a Satori
        /// audience for the economy flag, meaning the offers on display changed for this player.
        /// </summary>
        public bool OffersChanged { get; private set; }

        /// <summary>
        /// Returns whether offers changed on the last refresh and clears the flag, so the UI
        /// notifies the player once per change rather than on every redraw.
        /// </summary>
        public bool ConsumeOffersChanged()
        {
            var changed = OffersChanged;
            OffersChanged = false;
            return changed;
        }

        public enum StoreTab
        {
            Currency,
            Items,
            Seasonal
        }

        private StoreTab _currentTab = StoreTab.Currency;

        public event Action<IEconomyListStoreItem> OnItemSelected;

        public StoreController(
            NakamaSystem nakamaSystem,
            IEconomySystem economySystem,
            Dictionary<string, Sprite> itemIconDictionary,
            Dictionary<string, Sprite> currencyIconDictionary,
            Sprite defaultItemIcon,
            ISatoriSystem satoriSystem = null)
        {
            _nakamaSystem = nakamaSystem ?? throw new ArgumentNullException(nameof(nakamaSystem));
            _economySystem = economySystem ?? throw new ArgumentNullException(nameof(economySystem));
            _satoriSystem = satoriSystem;
            _itemIconDictionary = itemIconDictionary ?? new Dictionary<string, Sprite>();
            _currencyIconDictionary = currencyIconDictionary ?? new Dictionary<string, Sprite>();
            _defaultItemIcon = defaultItemIcon;
        }

        #region Store Operations

        public async Task RefreshStoreAsync()
        {
            // Pull the latest Satori flag state first: the server personalizes the store from the
            // same flag, and ConditionChanged tells us whether this player's offers just changed.
            OffersChanged = await CheckOffersChangedAsync();

            await _economySystem.RefreshStoreAsync();
            await _economySystem.RefreshAsync();

            StoreItems.Clear();
            StoreItems.AddRange(_economySystem.StoreItems);

            LogStoreItems();
        }

        /// <summary>
        /// Logs the store catalog the server returned. With the SatoriPersonalizer active this is
        /// the merged result of the base economy config and the player's Hiro-Economy flag value,
        /// so it can include items (and categories) the store UI doesn't render.
        /// </summary>
        private void LogStoreItems()
        {
            var lines = new System.Text.StringBuilder();
            lines.AppendLine($"Store returned {StoreItems.Count} items:");
            foreach (var item in StoreItems)
            {
                lines.AppendLine($"- {item.Id} [{item.Category}] '{item.Name}'");
            }
            Debug.Log(lines.ToString());
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
                StoreTab.Seasonal => "seasonal",
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

        #region Satori Operations

        /// <summary>
        /// Updates the player's Satori properties, recomputes their audience memberships, and
        /// refreshes the store so the server returns the catalog personalized for the new segments.
        /// </summary>
        public async Task UpdateSegmentPropertiesAsync(
            Dictionary<string, string> defaultProperties,
            Dictionary<string, string> customProperties = null)
        {
            if (_satoriSystem == null)
            {
                Debug.LogWarning("Satori system unavailable, skipping property update");
                return;
            }

            await _satoriSystem.UpdatePropertiesAsync(
                defaultProperties ?? new Dictionary<string, string>(),
                customProperties ?? new Dictionary<string, string>(),
                recompute: true);

            await RefreshStoreAsync();
        }

        /// <summary>
        /// Fetches the economy flag and reports whether the player's audience membership for it
        /// changed since the last fetch (they gained or lost an offer).
        /// </summary>
        private async Task<bool> CheckOffersChangedAsync()
        {
            if (_satoriSystem == null) return false;

            try
            {
                var flagList = await _satoriSystem.GetFlagsAsync(
                    new[] { EconomyFlagName }, Array.Empty<string>());

                if (flagList?.Flags == null) return false;

                foreach (var flag in flagList.Flags)
                {
                    if (flag.Name == EconomyFlagName)
                    {
                        return flag.ConditionChanged;
                    }
                }
            }
            catch (Exception e)
            {
                // Flag state is a UI nicety; never let it break the store refresh.
                Debug.LogWarning($"Failed to fetch Satori flags: {e.Message}");
            }

            return false;
        }

        public bool HasSeasonalItems()
        {
            foreach (var item in StoreItems)
            {
                if (item.Category == "seasonal") return true;
            }
            return false;
        }

        /// <summary>
        /// Returns the live event ID advertised by the seasonal items currently in the store,
        /// or null when no seasonal offer is running. By convention the Satori live event is
        /// named after this value, so it can be looked up for scheduling details.
        /// </summary>
        public string GetSeasonalEventId()
        {
            foreach (var item in StoreItems)
            {
                if (item.AdditionalProperties != null &&
                    item.AdditionalProperties.TryGetValue(SeasonalEventProperty, out var eventId) &&
                    !string.IsNullOrEmpty(eventId))
                {
                    return eventId;
                }
            }
            return null;
        }

        /// <summary>
        /// Returns the end time (Unix seconds) of the active run of the named Satori live event,
        /// or 0 when the event can't be found. Used to drive the seasonal countdown.
        /// </summary>
        public async Task<long> GetLiveEventEndTimeAsync(string liveEventName)
        {
            if (_satoriSystem == null || string.IsNullOrEmpty(liveEventName)) return 0;

            try
            {
                var eventList = await _satoriSystem.GetLiveEventsAsync(
                    new[] { liveEventName }, Array.Empty<string>());

                if (eventList?.LiveEvents == null) return 0;

                foreach (var liveEvent in eventList.LiveEvents)
                {
                    if (liveEvent.Name == liveEventName &&
                        long.TryParse(liveEvent.ActiveEndTimeSec, out var endTimeSec))
                    {
                        return endTimeSec;
                    }
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning($"Failed to fetch Satori live events: {e.Message}");
            }

            return 0;
        }

        /// <summary>
        /// Returns the pre-discount price a flag variant recorded on the item, or 0 when the item
        /// isn't discounted. Only prices higher than the current cost count as a discount.
        /// </summary>
        public long GetOriginalCost(IEconomyListStoreItem item)
        {
            if (item?.AdditionalProperties != null &&
                item.AdditionalProperties.TryGetValue(OriginalCostProperty, out var value) &&
                long.TryParse(value, out var originalCost) &&
                originalCost > GetPrimaryCurrencyAmount(item))
            {
                return originalCost;
            }
            return 0;
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
