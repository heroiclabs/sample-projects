using Hiro;
using UnityEngine;
using UnityEngine.UIElements;

namespace HiroStore
{
    /// <summary>
    /// View class for individual store items in the grid
    /// Handles the visual representation and user interaction for a single store item
    /// </summary>
    public class StoreItemView
    {
        private VisualElement _root;
        private VisualElement _icon;
        private Label _amount;
        private Label _itemBadge;
        private Label _bonusBadge;

        private Button _purchaseButton;
        private VisualElement _currencyIcon;
        private Label _priceLabel;

        private StoreController _controller;

        public IEconomyListStoreItem Item { get; private set; }

        public StoreItemView(StoreController controller)
        {
            _controller = controller;
        }

        public void SetVisualElement(VisualElement visualElement)
        {
            _root = visualElement;
            _icon = visualElement.Q<VisualElement>("item-icon");
            _amount = visualElement.Q<Label>("item-amount");
            _itemBadge = visualElement.Q<Label>("item-badge");
            _bonusBadge = visualElement.Q<Label>("bonus-badge");

            _purchaseButton = visualElement.Q<Button>("purchase-button");
            _currencyIcon = visualElement.Q<VisualElement>("currency-icon");
            _priceLabel = visualElement.Q<Label>("price-label");
        }

        public void SetStoreItem(IEconomyListStoreItem item)
        {
            Item = item;

            SetItemIcon(item);
            SetItemAmount(item);
            SetupBadges(item);
            SetupPurchaseButton(item);
        }

        private void SetItemIcon(IEconomyListStoreItem item)
        {
            var itemIcon = _controller.GetItemIcon(item.Id);
            if (itemIcon != null)
            {
                _icon.style.backgroundImage = new StyleBackground(itemIcon);
            }
        }

        private void SetItemAmount(IEconomyListStoreItem item)
        {
            // Set amount - show currency amount or item name
            if (item.Category == "currency")
            {
                // For currency, show the amount
                if (item.AvailableRewards?.Guaranteed?.Currencies != null &&
                    item.AvailableRewards.Guaranteed.Currencies.TryGetValue(item.Name, out var currencyReward))
                {
                    _amount.text = currencyReward.Count.Min.ToString();
                }
                else
                {
                    _amount.text = "0";
                }
            }
            else
            {
                // For items, show the item name
                _amount.text = item.Name;
            }
        }

        private void SetupBadges(IEconomyListStoreItem item)
        {
            _itemBadge.style.display = DisplayStyle.None;
            _bonusBadge.style.display = DisplayStyle.None;

            if (item.AdditionalProperties != null)
            {
                // Check for custom badge
                if (item.AdditionalProperties.ContainsKey("badge"))
                {
                    _itemBadge.text = item.AdditionalProperties["badge"];
                    _itemBadge.style.display = DisplayStyle.Flex;
                }

                // Check for bonus badge
                if (item.AdditionalProperties.ContainsKey("bonus"))
                {
                    _bonusBadge.text = item.AdditionalProperties["bonus"];
                    _bonusBadge.style.display = DisplayStyle.Flex;
                }
            }
        }

        private void SetupPurchaseButton(IEconomyListStoreItem item)
        {
            bool isFree = IsFreeItem(item);

            // Toggle the --free modifier class
            _purchaseButton.EnableInClassList("store-item-button--free", isFree);

            if (isFree)
            {
                _currencyIcon.style.display = DisplayStyle.None;
                _priceLabel.text = "FREE";
            }
            else if (IsRealMoneyItem(item))
            {
                _currencyIcon.style.display = DisplayStyle.None;
                _priceLabel.text = item.Cost.Sku;
            }
            else
            {
                // Soft currency purchase
                var primaryCurrency = _controller.GetPrimaryCurrency((EconomyListStoreItem)item);
                var amount = _controller.GetPrimaryCurrencyAmount(item);

                var currencyIcon = _controller.GetCurrencyIcon(primaryCurrency);
                if (currencyIcon != null)
                {
                    _currencyIcon.style.backgroundImage = new StyleBackground(currencyIcon);
                    _currencyIcon.style.display = DisplayStyle.Flex;
                }
                else
                {
                    _currencyIcon.style.display = DisplayStyle.None;
                }

                _priceLabel.text = amount.ToString();
            }
        }

        private bool IsFreeItem(IEconomyListStoreItem item)
        {
            return item.Cost.Currencies.Count == 0 && string.IsNullOrEmpty(item.Cost.Sku);
        }

        private bool IsRealMoneyItem(IEconomyListStoreItem item)
        {
            return !string.IsNullOrEmpty(item.Cost.Sku);
        }

        public void RegisterPurchaseCallback(EventCallback<ClickEvent> callback)
        {
            _purchaseButton?.RegisterCallback(callback);
        }
    }
}