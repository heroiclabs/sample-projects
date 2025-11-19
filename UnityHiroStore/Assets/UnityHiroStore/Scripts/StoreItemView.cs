using Hiro;
using UnityEngine;
using UnityEngine.UIElements;

namespace HiroInventory
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
        
        private Button _currencyPurchaseButton;
        private Button _moneyPurchaseButton;
        private Button _freeButton;
        
        private VisualElement _currencyIcon;
        private Label _currencyAmount;
        private Label _priceLabel;

        public IEconomyListStoreItem Item { get; private set; }

        public void SetVisualElement(VisualElement visualElement)
        {
            _root = visualElement;
            _icon = visualElement.Q<VisualElement>("item-icon");
            _amount = visualElement.Q<Label>("item-amount");
            _itemBadge = visualElement.Q<Label>("item-badge");
            _bonusBadge = visualElement.Q<Label>("bonus-badge");
            
            _currencyPurchaseButton = visualElement.Q<Button>("currency-purchase-button");
            _moneyPurchaseButton = visualElement.Q<Button>("money-purchase-button");
            _freeButton = visualElement.Q<Button>("free-button");
            
            _currencyIcon = visualElement.Q<VisualElement>("currency-icon");
            _currencyAmount = visualElement.Q<Label>("currency-amount");
            _priceLabel = visualElement.Q<Label>("price-label");
        }

        public void SetStoreItem(IEconomyListStoreItem item, Sprite itemIcon, Sprite currencyIcon, bool canAfford)
        {
            Item = item;

            // Set item icon
            if (itemIcon != null)
            {
                _icon.style.backgroundImage = new StyleBackground(itemIcon);
            }

            // Set amount - show currency amount or item name
            if (item.Category == "currency")
            {
                // For currency, show the amount
                if (item.AvailableRewards?.Guaranteed?.Currencies != null &&
                    item.AvailableRewards.Guaranteed.Currencies.TryGetValue(item.Name, out var currencyReward))
                {
                    _amount.text = currencyReward.Count.ToString();
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

            // Setup badges
            SetupBadges(item);

            // Setup purchase button
            SetupPurchaseButton(item, currencyIcon, canAfford);
        }

        private void SetupBadges(IEconomyListStoreItem item)
        {
            _itemBadge.style.display = DisplayStyle.None;
            _bonusBadge.style.display = DisplayStyle.None;

            if (item.AdditionalProperties != null)
            {
                if (item.AdditionalProperties.ContainsKey("badge"))
                {
                    _itemBadge.text = item.AdditionalProperties["badge"];
                    _itemBadge.style.display = DisplayStyle.Flex;
                }

                if (item.AdditionalProperties.ContainsKey("bonus"))
                {
                    _bonusBadge.text = item.AdditionalProperties["bonus"];
                    _bonusBadge.style.display = DisplayStyle.Flex;
                }
            }

            // Check if free
            if (IsFreeItem(item))
            {
                _itemBadge.text = "FREE";
                _itemBadge.style.display = DisplayStyle.Flex;
            }

            // Check for first purchase multiplier
            if (item.AdditionalProperties != null && item.AdditionalProperties.Count > 0)
            {
                _bonusBadge.text = "1st Purchase x2";
                _bonusBadge.style.display = DisplayStyle.Flex;
            }
        }

        private void SetupPurchaseButton(IEconomyListStoreItem item, Sprite currencyIcon, bool canAfford)
        {
            // Hide all buttons first
            _currencyPurchaseButton.style.display = DisplayStyle.None;
            _moneyPurchaseButton.style.display = DisplayStyle.None;
            _freeButton.style.display = DisplayStyle.None;

            if (IsFreeItem(item))
            {
                _freeButton.style.display = DisplayStyle.Flex;
                return;
            }

            if (IsRealMoneyItem(item))
            {
                _moneyPurchaseButton.style.display = DisplayStyle.Flex;
                _priceLabel.text = item.Cost.Sku;
                return;
            }

            // Soft currency purchase
            _currencyPurchaseButton.style.display = DisplayStyle.Flex;
            
            if (currencyIcon != null)
            {
                _currencyIcon.style.backgroundImage = new StyleBackground(currencyIcon);
            }

            var firstCost = item.Cost.Currencies.GetEnumerator();
            if (firstCost.MoveNext())
            {
                _currencyAmount.text = firstCost.Current.Value.ToString();
            }

            // Visual feedback for affordability
            if (!canAfford)
            {
                _currencyPurchaseButton.SetEnabled(false);
                _currencyPurchaseButton.style.backgroundColor = new StyleColor(new Color(0.5f, 0.5f, 0.5f, 1f));
            }
            else
            {
                _currencyPurchaseButton.SetEnabled(true);
                _currencyPurchaseButton.style.backgroundColor = new StyleColor(new Color(132f/255f, 154f/255f, 1f, 1f));
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
            _currencyPurchaseButton?.RegisterCallback(callback);
            _moneyPurchaseButton?.RegisterCallback(callback);
            _freeButton?.RegisterCallback(callback);
        }

        public void SetHighlight(bool highlighted)
        {
            if (highlighted)
            {
                var highlightColor = new Color(64f/255f, 224f/255f, 208f/255f, 1f);
                _root.style.borderTopColor = new StyleColor(highlightColor);
                _root.style.borderBottomColor = new StyleColor(highlightColor);
                _root.style.borderLeftColor = new StyleColor(highlightColor);
                _root.style.borderRightColor = new StyleColor(highlightColor);
                _root.style.borderTopWidth = 3;
                _root.style.borderBottomWidth = 3;
                _root.style.borderLeftWidth = 3;
                _root.style.borderRightWidth = 3;
            }
            else
            {
                _root.style.borderTopColor = new StyleColor(Color.clear);
                _root.style.borderBottomColor = new StyleColor(Color.clear);
                _root.style.borderLeftColor = new StyleColor(Color.clear);
                _root.style.borderRightColor = new StyleColor(Color.clear);
                _root.style.borderTopWidth = 0;
                _root.style.borderBottomWidth = 0;
                _root.style.borderLeftWidth = 0;
                _root.style.borderRightWidth = 0;
            }
        }
    }
}