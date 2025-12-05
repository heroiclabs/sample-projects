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
        private Button _tabDeals;
        private Button _tabFeatured;
        private Button _tabResources;
        
        private VisualElement _featuredContainer;
        private VisualElement _featuredItem;
        private VisualElement _storeGrid;

        // Featured Item Elements
        private VisualElement _featuredIcon;
        private Label _featuredName;
        private Label _featuredBadge;
        private Button _featuredPurchaseButton;
        private Label _featuredPrice;
        private VisualElement _featuredCurrencyIcon;

        // Lootbox Item Elements
        private VisualElement _lootboxItem;
        private VisualElement _lootboxIcon;
        private Label _lootboxName;
        private Label _lootboxBadge;
        private Button _lootboxPurchaseButton;
        private Label _lootboxPrice;
        private VisualElement _lootboxCostIcon;

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
            _tabDeals = root.Q<Button>("tab-deals");
            _tabDeals.RegisterCallback<ClickEvent>(_ => SwitchTab(StoreController.StoreTab.Deals));

            _tabFeatured = root.Q<Button>("tab-featured");
            _tabFeatured.RegisterCallback<ClickEvent>(_ => SwitchTab(StoreController.StoreTab.Featured));

            _tabResources = root.Q<Button>("tab-resources");
            _tabResources.RegisterCallback<ClickEvent>(_ => SwitchTab(StoreController.StoreTab.Resources));

            // Store Grid
            _storeGrid = root.Q<VisualElement>("store-grid");
            
            // Featured Item
            _featuredContainer = root.Q<VisualElement>("featured-container");
            _featuredItem = root.Q<VisualElement>("featured-item");
            _featuredIcon = root.Q<VisualElement>("featured-icon");
            _featuredName = root.Q<Label>("featured-name");
            _featuredBadge = root.Q<Label>("featured-badge");
            _featuredPurchaseButton = root.Q<Button>("featured-purchase-button");
            _featuredPrice = root.Q<Label>("featured-price");
            _featuredCurrencyIcon = root.Q<VisualElement>("featured-currency-icon");

            _featuredPurchaseButton.RegisterCallback<ClickEvent>(_ => 
            {
                var featured = _controller.GetFeaturedItem();
                if (featured != null) ShowPurchaseModal(featured);
            });

            // Lootbox Item
            _lootboxItem = root.Q<VisualElement>("lootbox-item");
            _lootboxIcon = root.Q<VisualElement>("lootbox-icon");
            _lootboxName = root.Q<Label>("lootbox-name");
            _lootboxBadge = root.Q<Label>("lootbox-badge");
            _lootboxPurchaseButton = root.Q<Button>("lootbox-purchase-button");
            _lootboxPrice = root.Q<Label>("lootbox-price");
            _lootboxCostIcon = root.Q<VisualElement>("lootbox-cost-icon");

            _lootboxPurchaseButton.RegisterCallback<ClickEvent>(_ => 
            {
                var lootbox = _controller.GetLootboxItem();
                if (lootbox != null) ShowPurchaseModal(lootbox);
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

        public async Task RefreshStoreDisplay()
        {
            UpdateFeaturedAndLootboxDisplay();
            PopulateStoreGrid();
            UpdateTabButtons();
        }

        private void UpdateFeaturedAndLootboxDisplay()
        {
            var currentTab = _controller.GetCurrentTab();
            
            if (currentTab == StoreController.StoreTab.Deals)
            {
                // Show lootbox, hide featured
                _featuredItem.style.display = DisplayStyle.None;
                _lootboxItem.style.display = DisplayStyle.Flex;
                
                PopulateLootboxItem();
            }
            else if (currentTab == StoreController.StoreTab.Featured)
            {
                // Show featured, hide lootbox
                _featuredItem.style.display = DisplayStyle.Flex;
                _lootboxItem.style.display = DisplayStyle.None;
                
                PopulateFeaturedItem();
            }
            else
            {
                // Hide both for Resources tab
                _featuredItem.style.display = DisplayStyle.None;
                _lootboxItem.style.display = DisplayStyle.None;
            }
        }

        private void PopulateFeaturedItem()
        {
            var featured = _controller.GetFeaturedItem();
            
            if (featured == null)
            {
                _featuredItem.style.display = DisplayStyle.None;
                return;
            }

            _featuredItem.style.display = DisplayStyle.Flex;

            // Set icon
            var featuredIcon = _controller.GetItemIcon(featured.Id);
            if (featuredIcon != null)
            {
                _featuredIcon.style.backgroundImage = new StyleBackground(featuredIcon);
            }

            // Set name
            _featuredName.text = featured.Name;

            // Set price based on cost type
            SetFeaturedPrice(featured);

            // Set badge if applicable
            if (featured.AdditionalProperties != null && featured.AdditionalProperties.ContainsKey("badge"))
            {
                _featuredBadge.text = featured.AdditionalProperties["badge"];
                _featuredBadge.style.display = DisplayStyle.Flex;
            }
            else
            {
                _featuredBadge.style.display = DisplayStyle.None;
            }
        }

        private void PopulateLootboxItem()
        {
            var lootbox = _controller.GetLootboxItem();
            
            if (lootbox == null)
            {
                _lootboxItem.style.display = DisplayStyle.None;
                return;
            }

            _lootboxItem.style.display = DisplayStyle.Flex;

            // Set icon
            var lootboxIcon = _controller.GetItemIcon(lootbox.Id);
            if (lootboxIcon != null)
            {
                _lootboxIcon.style.backgroundImage = new StyleBackground(lootboxIcon);
            }

            // Set name
            _lootboxName.text = lootbox.Name;

            // Set badge if applicable
            if (lootbox.AdditionalProperties != null && lootbox.AdditionalProperties.ContainsKey("badge"))
            {
                _lootboxBadge.text = lootbox.AdditionalProperties["badge"];
            }
            else
            {
                _lootboxBadge.text = "MYSTERY BOX";
            }

            // Set price
            SetLootboxPrice(lootbox);

            // Update affordability
            bool canAfford = _controller.CanAffordItem(lootbox);
            _lootboxPurchaseButton.SetEnabled(canAfford);
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

        private void SetLootboxPrice(EconomyListStoreItem lootbox)
        {
            // Real money purchase
            if (!string.IsNullOrEmpty(lootbox.Cost.Sku))
            {
                _lootboxPrice.text = lootbox.Cost.Sku;
                _lootboxCostIcon.style.display = DisplayStyle.None;
            }
            // Soft currency purchase
            else if (lootbox.Cost.Currencies.Count > 0)
            {
                var primaryCurrency = _controller.GetPrimaryCurrency(lootbox);
                var amount = _controller.GetPrimaryCurrencyAmount(lootbox);
                
                // Set currency icon if available
                var currencyIcon = _controller.GetCurrencyIcon(primaryCurrency);
                if (currencyIcon != null)
                {
                    _lootboxCostIcon.style.backgroundImage = new StyleBackground(currencyIcon);
                    _lootboxCostIcon.style.display = DisplayStyle.Flex;
                }
                else
                {
                    _lootboxCostIcon.style.display = DisplayStyle.None;
                }
                
                _lootboxPrice.text = amount.ToString();
            }
            // Free
            else
            {
                _lootboxPrice.text = "FREE";
                _lootboxCostIcon.style.display = DisplayStyle.None;
            }
        }

        private void PopulateStoreGrid()
        {
            _storeGrid.Clear();

            var items = _controller.GetItemsForCurrentTab();

            foreach (var item in items)
            {
                // Skip the featured item in the grid
                if (item == _controller.GetFeaturedItem())
                    continue;

                // Skip the lootbox item in the grid
                if (item == _controller.GetLootboxItem())
                    continue;

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
            var activeColor = new Color(132f / 255f, 154f / 255f, 255f / 255f, 1f);
            var inactiveColor = new Color(132f / 255f, 154f / 255f, 255f / 255f, 0.2f);

            _tabDeals.style.backgroundColor = currentTab == StoreController.StoreTab.Deals 
                ? new StyleColor(activeColor) : new StyleColor(inactiveColor);
            _tabFeatured.style.backgroundColor = currentTab == StoreController.StoreTab.Featured 
                ? new StyleColor(activeColor) : new StyleColor(inactiveColor);
            _tabResources.style.backgroundColor = currentTab == StoreController.StoreTab.Resources 
                ? new StyleColor(activeColor) : new StyleColor(inactiveColor);
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

            try
            {
                var result = await _controller.PurchaseItem(_pendingPurchaseItem);
                
                HidePurchaseModal();
                
                if (result != null)
                {
                    ShowRewardModal(result.Reward);
                }

                await RefreshStoreDisplay();
            }
            catch (Exception e)
            {
                ShowError(e.Message);
            }
        }

        #endregion

        #region Reward Modal

        private void ShowRewardModal(IReward reward)
        {
            _rewardList.Clear();

            if (reward == null)
            {
                _rewardModal.style.display = DisplayStyle.Flex;
                return;
            }

            // Display currency rewards
            if (reward.Currencies != null)
            {
                foreach (var currency in reward.Currencies)
                {
                    var rewardElement = CreateRewardElement(
                        _controller.GetCurrencyIcon(currency.Key),
                        $"{currency.Key}: {currency.Value}"
                    );
                    _rewardList.Add(rewardElement);
                }
            }

            // Display item rewards
            if (reward.Items != null)
            {
                foreach (var item in reward.Items)
                {
                    var rewardElement = CreateRewardElement(
                        _controller.GetItemIcon(item.Key),
                        $"{item.Key} x{item.Value}"
                    );
                    _rewardList.Add(rewardElement);
                }
            }

            _rewardModal.style.display = DisplayStyle.Flex;
        }

        private VisualElement CreateRewardElement(Sprite icon, string text)
        {
            var rewardElement = new VisualElement();
            rewardElement.style.flexDirection = FlexDirection.Row;
            rewardElement.style.alignItems = Align.Center;
            rewardElement.style.marginBottom = 10;
            rewardElement.style.paddingTop = 10;
            rewardElement.style.backgroundColor = new Color(0.9f, 0.9f, 0.9f, 1f);
            rewardElement.style.borderTopLeftRadius = 10;

            var iconElement = new VisualElement();
            iconElement.style.width = 50;
            iconElement.style.height = 50;
            iconElement.style.marginRight = 15;
            iconElement.style.borderTopLeftRadius = 10;
            
            if (icon != null)
            {
                iconElement.style.backgroundImage = new StyleBackground(icon);
            }

            var label = new Label(text);
            label.style.fontSize = 20;
            label.style.color = new Color(0.2f, 0.2f, 0.2f, 1f);

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