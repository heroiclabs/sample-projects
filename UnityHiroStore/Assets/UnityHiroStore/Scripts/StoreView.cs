using System;
using System.Linq;
using System.Threading.Tasks;
using Hiro;
using HeroicUI;
using UnityEngine;
using UnityEngine.UIElements;

namespace HiroStore
{
    public sealed class StoreView
    {
        private readonly StoreController _controller;
        private readonly HiroStoreCoordinator _coordinator;
        private readonly VisualTreeAsset _storeItemTemplate;
        private readonly Sprite _defaultIcon;

        private WalletDisplay _walletDisplay;
        private UIDocument _uiDocument;

        // UI Elements
        private Button _backButton;
        private Button _refreshButton;
        private Button _tabCurrency;
        private Button _tabItems;
        private VisualElement _storeGrid;

        // Featured Item Elements
        private VisualElement _featuredItem;
        private VisualElement _featuredIcon;
        private Label _featuredName;
        private Label _featuredBadge;
        private VisualElement _featuredValueIcon;
        private Label _featuredValueAmount;
        private Button _featuredPurchaseButton;
        private Label _featuredPrice;
        private VisualElement _featuredCurrencyIcon;
        private string _currentTheme = "";

        // Modals
        private VisualElement _purchaseModal;
        private VisualElement _modalItemIcon;
        private Label _modalItemName;
        private Label _modalItemDescription;
        private VisualElement _modalCostIcon;
        private Label _modalCostAmount;
        private Button _purchaseModalConfirm;
        private Button _purchaseModalCancel;
        private Button _purchaseModalClose;

        private VisualElement _rewardModal;
        private ScrollView _rewardList;
        private Button _rewardModalCloseButton;

        private VisualElement _errorPopup;
        private Label _errorMessage;
        private Button _errorCloseButton;

        private IEconomyListStoreItem _pendingPurchaseItem;

        public StoreView(StoreController controller, HiroStoreCoordinator coordinator,
            VisualTreeAsset storeItemTemplate, Sprite defaultIcon)
        {
            _controller = controller;
            _coordinator = coordinator;
            _storeItemTemplate = storeItemTemplate;
            _defaultIcon = defaultIcon;

            InitializeUI();
        }

        #region UI Initialization

        private void InitializeUI()
        {
            _uiDocument = _controller.GetComponent<UIDocument>();
            var root = _uiDocument.rootVisualElement;

            // Wallet
            _walletDisplay = new WalletDisplay(root.Q<VisualElement>("wallet-display"));

            // Refresh
            _refreshButton = root.Q<Button>("refresh-button");
            _refreshButton.RegisterCallback<ClickEvent>(async evt => await _controller.RefreshStore());

            // Tabs
            _tabCurrency = root.Q<Button>("tab-currency");
            _tabCurrency.RegisterCallback<ClickEvent>(_ => SwitchTab(StoreController.StoreTab.Currency));

            _tabItems = root.Q<Button>("tab-items");
            _tabItems.RegisterCallback<ClickEvent>(_ => SwitchTab(StoreController.StoreTab.Items));

            // Store Grid
            _storeGrid = root.Q<VisualElement>("store-grid");
            
            // Featured Item
            _featuredItem = root.Q<VisualElement>("featured-item");
            _featuredIcon = root.Q<VisualElement>("featured-icon");
            _featuredName = root.Q<Label>("featured-name");
            _featuredBadge = root.Q<Label>("featured-badge");
            _featuredValueIcon = root.Q<VisualElement>("featured-value-icon");
            _featuredValueAmount = root.Q<Label>("featured-value-amount");
            _featuredPurchaseButton = root.Q<Button>("featured-purchase-button");
            _featuredPrice = root.Q<Label>("featured-price");
            _featuredCurrencyIcon = root.Q<VisualElement>("featured-currency-icon");

            _featuredPurchaseButton.RegisterCallback<ClickEvent>(_ =>
            {
                var featured = _controller.GetFeaturedItemForCategory(_controller.GetCurrentCategory());
                if (featured != null) ShowPurchaseModal(featured);
            });

            // Purchase Modal
            _purchaseModal = root.Q<VisualElement>("purchase-modal");
            _modalItemIcon = root.Q<VisualElement>("modal-item-icon");
            _modalItemName = root.Q<Label>("modal-item-name");
            _modalItemDescription = root.Q<Label>("modal-item-description");
            _modalCostIcon = root.Q<VisualElement>("modal-cost-icon");
            _modalCostAmount = root.Q<Label>("modal-cost-amount");
            _purchaseModalConfirm = root.Q<Button>("purchase-modal-confirm");
            _purchaseModalCancel = root.Q<Button>("purchase-modal-cancel");
            _purchaseModalClose = root.Q<Button>("purchase-modal-close");

            _purchaseModalConfirm.RegisterCallback<ClickEvent>(async evt => await HandlePurchaseConfirm());
            _purchaseModalCancel.RegisterCallback<ClickEvent>(_ => HidePurchaseModal());
            _purchaseModalClose.RegisterCallback<ClickEvent>(_ => HidePurchaseModal());

            // Reward Modal
            _rewardModal = root.Q<VisualElement>("reward-modal");
            _rewardList = root.Q<ScrollView>("reward-list");
            _rewardModalCloseButton = root.Q<Button>("reward-modal-close-button");
            _rewardModalCloseButton.RegisterCallback<ClickEvent>(_ => HideRewardModal());

            // Error Popup
            _errorPopup = root.Q<VisualElement>("error-popup");
            _errorMessage = root.Q<Label>("error-message");
            _errorCloseButton = root.Q<Button>("error-close");
            _errorCloseButton.RegisterCallback<ClickEvent>(_ => _errorPopup.style.display = DisplayStyle.None);

            UpdateTabButtons();
        }

        public void StartObservingWallet()
        {
            _walletDisplay.StartObserving();
        }

        #endregion

        #region Store Display

        public Task RefreshStoreDisplay()
        {
            UpdateFeaturedDisplay();
            PopulateStoreGrid();
            UpdateTabButtons();
            return Task.CompletedTask;
        }

        private void UpdateFeaturedDisplay()
        {
            PopulateFeaturedItem();
        }

        private void PopulateFeaturedItem()
        {
            var featured = _controller.GetFeaturedItemForCategory(_controller.GetCurrentCategory());

            if (featured == null)
            {
                _featuredItem.style.display = DisplayStyle.None;
                return;
            }

            _featuredItem.style.display = DisplayStyle.Flex;

            // Apply theme
            ApplyFeaturedTheme(featured);

            // Set icon
            var featuredIcon = _controller.GetItemIcon(featured.Id);
            if (featuredIcon != null)
            {
                _featuredIcon.style.backgroundImage = new StyleBackground(featuredIcon);
            }

            // Set name
            _featuredName.text = featured.Name;

            // Set reward value display (icon + amount)
            SetFeaturedRewardValue(featured);

            // Set price based on cost type
            SetFeaturedPrice(featured);

            // Set badge if applicable
            if (featured.AdditionalProperties.TryGetValue("badge", out var property))
            {
                _featuredBadge.text = property;
                _featuredBadge.style.display = DisplayStyle.Flex;
            }
            else
            {
                _featuredBadge.style.display = DisplayStyle.None;
            }
        }

        private void ApplyFeaturedTheme(IEconomyListStoreItem featured)
        {
            var newTheme = _controller.GetItemTheme(featured);

            // Remove old theme class if different
            if (!string.IsNullOrEmpty(_currentTheme) && _currentTheme != newTheme)
            {
                _featuredItem.RemoveFromClassList($"featured-item--{_currentTheme}");
            }

            // Add new theme class
            if (_currentTheme != newTheme)
            {
                _featuredItem.AddToClassList($"featured-item--{newTheme}");
                _currentTheme = newTheme;
            }
        }

        private void SetFeaturedRewardValue(IEconomyListStoreItem featured)
        {
            // Get the first reward currency
            var rewardCurrencies = featured.AvailableRewards?.Guaranteed?.Currencies;
            if (rewardCurrencies != null && rewardCurrencies.Count > 0)
            {
                var firstReward = rewardCurrencies.First();
                var currencyCode = firstReward.Key;
                var amount = firstReward.Value.Count.Min;

                // Set the reward icon
                var currencyIcon = _controller.GetCurrencyIcon(currencyCode);
                if (currencyIcon != null)
                {
                    _featuredValueIcon.style.backgroundImage = new StyleBackground(currencyIcon);
                }

                // Set the reward amount
                _featuredValueAmount.text = amount.ToString();
            }
        }

        private void SetFeaturedPrice(EconomyListStoreItem featured)
        {
            // Hide currency icon by default
            if (_featuredCurrencyIcon != null)
            {
                _featuredCurrencyIcon.style.display = DisplayStyle.None;
            }
            
            // Soft currency purchase
            if (featured.Cost.Currencies.Count > 0)
            {
                var primaryCurrency = _controller.GetPrimaryCurrency(featured);
                var amount = _controller.GetPrimaryCurrencyAmount(featured);
                
                // Set currency icon if available
                if (_featuredCurrencyIcon != null)
                {
                    var currencyIcon = _controller.GetCurrencyIcon(primaryCurrency);
                    if (currencyIcon != null)
                    {
                        _featuredCurrencyIcon.style.backgroundImage = new StyleBackground(currencyIcon);
                        _featuredCurrencyIcon.style.display = DisplayStyle.Flex;
                    }
                }
                
                _featuredPrice.text = amount.ToString();
            }
            // Free
            else
            {
                _featuredPrice.text = "FREE";
            }
        }

        private void PopulateStoreGrid()
        {
            _storeGrid.Clear();

            var items = _controller.GetItemsForCategory(_controller.GetCurrentCategory());

            foreach (var item in items)
            {
                CreateStoreItemSlot(item);
            }
        }

        private void CreateStoreItemSlot(IEconomyListStoreItem item)
        {
            var itemSlot = _storeItemTemplate.Instantiate();
            var slotRoot = itemSlot.Q<VisualElement>("store-item-slot");

            // Store item reference
            slotRoot.userData = item;

            // Create StoreItemView and let it handle all the setup
            var itemView = new StoreItemView(_controller);
            itemView.SetVisualElement(slotRoot);
            itemView.SetStoreItem(item);

            // Register click event for purchase
            itemView.RegisterPurchaseCallback(_ => ShowPurchaseModal(item));

            _storeGrid.Add(slotRoot);
        }

        #endregion

        #region Tab Management

        private void SwitchTab(StoreController.StoreTab tab)
        {
            _controller.SwitchTab(tab);
            UpdateTabButtons();
        }

        private void UpdateTabButtons()
        {
            var currentTab = _controller.GetCurrentTab();

            // Toggle the 'selected' class on each tab based on current selection
            _tabCurrency.EnableInClassList("selected", currentTab == StoreController.StoreTab.Currency);
            _tabItems.EnableInClassList("selected", currentTab == StoreController.StoreTab.Items);
        }

        #endregion

        #region Purchase Modal

        private void ShowPurchaseModal(IEconomyListStoreItem item)
        {
            _pendingPurchaseItem = item;

            // Set item info
            _modalItemName.text = item.Name;
            _modalItemDescription.text = item.Description ?? "No description available.";

            // Set icon
            var icon = _controller.GetItemIcon(item.Id);
            if (icon != null)
            {
                _modalItemIcon.style.backgroundImage = new StyleBackground(icon);
            }

            // Set cost
            SetModalCost(item);

            // Enable/disable purchase button
            _purchaseModalConfirm.SetEnabled(_controller.CanAffordItem(item));

            _purchaseModal.style.display = DisplayStyle.Flex;
        }

        private void SetModalCost(IEconomyListStoreItem item)
        {
            // Soft currency purchase
            if (item.Cost.Currencies.Count > 0)
            {
                var primaryCurrency = item.Cost.Currencies.FirstOrDefault();
                var amount = _controller.GetPrimaryCurrencyAmount(item);
                
                _modalCostAmount.text = amount.ToString();
                
                var currencyIcon = _controller.GetCurrencyIcon(primaryCurrency.Key);
                if (currencyIcon != null)
                {
                    _modalCostIcon.style.backgroundImage = new StyleBackground(currencyIcon);
                    _modalCostIcon.style.display = DisplayStyle.Flex;
                }
                else
                {
                    _modalCostIcon.style.display = DisplayStyle.None;
                }
            }
            // Free
            else
            {
                _modalCostAmount.text = "FREE";
                _modalCostIcon.style.display = DisplayStyle.None;
            }
        }

        private void HidePurchaseModal()
        {
            _purchaseModal.style.display = DisplayStyle.None;
            _pendingPurchaseItem = null;
        }

        private async Task HandlePurchaseConfirm()
        {
            if (_pendingPurchaseItem == null) return;

            var purchasedItem = _pendingPurchaseItem;

            try
            {
                await _controller.PurchaseItem(_pendingPurchaseItem);

                HidePurchaseModal();
                ShowRewardModal(purchasedItem);

                await RefreshStoreDisplay();
            }
            catch (Exception e)
            {
                ShowError(e.Message);
            }
        }

        #endregion

        #region Reward Modal

        private void ShowRewardModal(IEconomyListStoreItem purchasedItem)
        {
            _rewardList.Clear();

            if (purchasedItem?.AvailableRewards?.Guaranteed == null)
            {
                _rewardModal.style.display = DisplayStyle.Flex;
                return;
            }

            var guaranteed = purchasedItem.AvailableRewards.Guaranteed;

            // Display currency rewards
            if (guaranteed.Currencies != null)
            {
                foreach (var currency in guaranteed.Currencies)
                {
                    var rewardElement = CreateRewardElement(
                        _controller.GetCurrencyIcon(currency.Key),
                        $"{currency.Key}: {currency.Value.Count.Min}"
                    );
                    _rewardList.Add(rewardElement);
                }
            }

            // Display item rewards
            if (guaranteed.Items != null)
            {
                foreach (var item in guaranteed.Items)
                {
                    var rewardElement = CreateRewardElement(
                        _controller.GetItemIcon(item.Key),
                        $"{item.Key} x{item.Value.Count.Min}"
                    );
                    _rewardList.Add(rewardElement);
                }
            }

            _rewardModal.style.display = DisplayStyle.Flex;
        }

        private VisualElement CreateRewardElement(Sprite icon, string text)
        {
            var rewardElement = new VisualElement();
            rewardElement.AddToClassList("reward-item");

            var iconElement = new VisualElement();
            iconElement.AddToClassList("reward-item__icon");
            if (icon)
            {
                iconElement.style.backgroundImage = new StyleBackground(icon);
            }

            var label = new Label(text);
            label.AddToClassList("reward-item__label");

            rewardElement.Add(iconElement);
            rewardElement.Add(label);

            return rewardElement;
        }

        private void HideRewardModal()
        {
            _rewardModal.style.display = DisplayStyle.None;
        }

        #endregion

        #region Error Handling

        public void ShowError(string message)
        {
            _errorPopup.style.display = DisplayStyle.Flex;
            _errorMessage.text = message;
        }

        #endregion
    }
}