using System;
using System.Threading;
using System.Threading.Tasks;
using Hiro;
using HeroicUI;
using HeroicUtils;
using UnityEngine;
using UnityEngine.UIElements;

namespace HiroStore
{
    /// <summary>
    /// View for the Store system.
    /// Manages UI presentation and user interactions, delegates all business logic to Controller.
    /// </summary>
    public sealed class StoreView : IDisposable
    {
        private readonly StoreController _controller;
        private readonly VisualTreeAsset _storeItemTemplate;

        private readonly CancellationTokenSource _cts = new();
        private readonly object _disposeLock = new();
        private volatile bool _disposed;

        private WalletDisplay _walletDisplay;

        // UI Elements
        private Button _refreshButton;
        private Button _tabCurrency;
        private Button _tabItems;
        private VisualElement _storeGrid;

        // Featured Item Elements
        private VisualElement _featuredItem;
        private VisualElement _featuredIcon;
        private Label _featuredName;
        private Label _featuredBadge;
        private VisualElement _featuredRewardsContainer;
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

        // Toast
        private VisualElement _toast;
        private Label _toastMessage;
        private CancellationTokenSource _toastCts;

        private IEconomyListStoreItem _pendingPurchaseItem;

        private LoadingSpinner _storeListSpinner;

        public event Action OnInitialized;

        public StoreView(
            StoreController controller,
            VisualElement rootElement,
            VisualTreeAsset storeItemTemplate)
        {
            _controller = controller;
            _storeItemTemplate = storeItemTemplate;

            Initialize(rootElement);

            _walletDisplay.StartObserving();
            AccountSwitcher.AccountSwitched += OnAccountSwitched;

            _ = InitializeAsync();
        }

        public void Dispose()
        {
            lock (_disposeLock)
            {
                if (_disposed) return;
                _disposed = true;
            }

            _toastCts?.Cancel();
            _toastCts?.Dispose();
            _cts.Cancel();
            _cts.Dispose();
            AccountSwitcher.AccountSwitched -= OnAccountSwitched;
            _walletDisplay?.Dispose();
            _storeListSpinner?.Dispose();
        }

        private void ThrowIfDisposedOrCancelled()
        {
            if (_disposed)
                throw new OperationCanceledException();
            _cts.Token.ThrowIfCancellationRequested();
        }

        private async Task InitializeAsync()
        {
            try
            {
                ThrowIfDisposedOrCancelled();
                await _controller.RefreshStoreAsync();
                await RefreshStoreDisplayAsync();
                OnInitialized?.Invoke();
            }
            catch (OperationCanceledException)
            {
                // Expected on dispose
            }
            catch (Exception e)
            {
                ShowError(e.Message);
                Debug.LogException(e);
            }
        }

        #region UI Initialization

        private void Initialize(VisualElement root)
        {
            // Wallet
            _walletDisplay = new WalletDisplay(root.Q<VisualElement>("wallet-display"));

            // Refresh
            _refreshButton = root.Q<Button>("refresh-button");
            _refreshButton.RegisterCallback<ClickEvent>(_ => OnRefreshClicked());

            // Tabs
            _tabCurrency = root.Q<Button>("tab-currency");
            _tabCurrency.RegisterCallback<ClickEvent>(_ => SwitchTab(StoreController.StoreTab.Currency));

            _tabItems = root.Q<Button>("tab-items");
            _tabItems.RegisterCallback<ClickEvent>(_ => SwitchTab(StoreController.StoreTab.Items));

            // Store Grid
            _storeGrid = root.Q<VisualElement>("store-grid");

            // Spinner
            _storeListSpinner = new LoadingSpinner(root.Q<VisualElement>("store-list-spinner"));

            // Featured Item
            _featuredItem = root.Q<VisualElement>("featured-item");
            _featuredIcon = root.Q<VisualElement>("featured-icon");
            _featuredName = root.Q<Label>("featured-name");
            _featuredBadge = root.Q<Label>("featured-badge");
            _featuredRewardsContainer = root.Q<VisualElement>("featured-rewards-container");
            _featuredPurchaseButton = root.Q<Button>("featured-purchase-button");
            _featuredPrice = root.Q<Label>("featured-price");
            _featuredCurrencyIcon = root.Q<VisualElement>("featured-currency-icon");

            _featuredItem.RegisterCallback<ClickEvent>(_ =>
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

            _purchaseModalConfirm.RegisterCallback<ClickEvent>(_ => OnPurchaseConfirmClicked());
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

            // Toast
            _toast = root.Q<VisualElement>("toast");
            _toastMessage = root.Q<Label>("toast-message");

            UpdateTabButtons();
        }

        #endregion

        #region Event Handlers

        private async void OnRefreshClicked()
        {
            _storeListSpinner?.Show();
            try
            {
                ThrowIfDisposedOrCancelled();
                await _controller.RefreshStoreAsync();
                await RefreshStoreDisplayAsync();
                ShowToast("Store and wallet synced");
            }
            catch (OperationCanceledException)
            {
                // Expected on dispose
            }
            catch (Exception e)
            {
                ShowError(e.Message);
                Debug.LogException(e);
            }
            finally
            {
                _storeListSpinner?.Hide();
            }
        }

        private async void OnAccountSwitched()
        {
            try
            {
                ThrowIfDisposedOrCancelled();
                HideAllModals();
                await _controller.SwitchCompleteAsync();
                await RefreshStoreDisplayAsync();
            }
            catch (OperationCanceledException)
            {
                // Expected on dispose
            }
            catch (Exception e)
            {
                ShowError(e.Message);
                Debug.LogException(e);
            }
        }

        private async void OnPurchaseConfirmClicked()
        {
            if (_pendingPurchaseItem == null) return;

            var purchasedItem = _pendingPurchaseItem;

            try
            {
                ThrowIfDisposedOrCancelled();
                var result = await _controller.PurchaseItemAsync(_pendingPurchaseItem);

                HidePurchaseModal();
                ShowRewardModal(purchasedItem, result?.Reward);

                await RefreshStoreDisplayAsync();
            }
            catch (OperationCanceledException)
            {
                // Expected on dispose
            }
            catch (Exception e)
            {
                ShowError(e.Message);
                Debug.LogException(e);
            }
        }

        #endregion

        #region Store Display

        public Task RefreshStoreDisplayAsync()
        {
            PopulateFeaturedItem();
            PopulateStoreGrid();
            UpdateTabButtons();
            return Task.CompletedTask;
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

            ApplyFeaturedTheme(featured);

            var featuredIcon = _controller.GetItemIcon(featured.Id);
            if (featuredIcon != null)
            {
                _featuredIcon.style.backgroundImage = new StyleBackground(featuredIcon);
            }

            _featuredName.text = featured.Name;
            SetFeaturedRewardValue(featured);
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

            if (!string.IsNullOrEmpty(_currentTheme) && _currentTheme != newTheme)
            {
                _featuredItem.RemoveFromClassList($"featured-item--{_currentTheme}");
            }

            if (_currentTheme != newTheme)
            {
                _featuredItem.AddToClassList($"featured-item--{newTheme}");
                _currentTheme = newTheme;
            }
        }

        private void SetFeaturedRewardValue(IEconomyListStoreItem featured)
        {
            _featuredRewardsContainer.Clear();

            var availableRewards = featured.AvailableRewards;

            // Add guaranteed currency rewards
            if (availableRewards.Guaranteed?.Currencies != null)
            {
                foreach (var currency in availableRewards.Guaranteed.Currencies)
                {
                    var icon = _controller.GetCurrencyIcon(currency.Key);
                    var amount = currency.Value.Count.Min;
                    _featuredRewardsContainer.Add(CreateFeaturedRewardElement(icon, amount.ToString()));
                }
            }

            // Add guaranteed item rewards
            if (availableRewards.Guaranteed?.Items != null)
            {
                foreach (var item in availableRewards.Guaranteed.Items)
                {
                    var icon = _controller.GetItemIcon(item.Key);
                    var amount = item.Value.Count.Min;
                    _featuredRewardsContainer.Add(CreateFeaturedRewardElement(icon, $"x{amount}"));
                }
            }

            // Add weighted rewards (potential lootbox rewards)
            if (availableRewards.Weighted != null)
            {
                foreach (var weightedReward in availableRewards.Weighted)
                {
                    // Add weighted currency rewards
                    if (weightedReward.Currencies != null)
                    {
                        foreach (var currency in weightedReward.Currencies)
                        {
                            var icon = _controller.GetCurrencyIcon(currency.Key);
                            _featuredRewardsContainer.Add(CreateFeaturedRewardElement(icon, null));
                        }
                    }

                    // Add weighted item rewards
                    if (weightedReward.Items != null)
                    {
                        foreach (var item in weightedReward.Items)
                        {
                            var icon = _controller.GetItemIcon(item.Key);
                            _featuredRewardsContainer.Add(CreateFeaturedRewardElement(icon, null));
                        }
                    }
                }
            }
        }

        private VisualElement CreateFeaturedRewardElement(Sprite icon, string amountText)
        {
            var container = new VisualElement();
            container.AddToClassList("featured-reward");

            var iconElement = new VisualElement();
            iconElement.AddToClassList("featured-reward__icon");
            if (icon != null)
            {
                iconElement.style.backgroundImage = new StyleBackground(icon);
            }
            container.Add(iconElement);

            if (string.IsNullOrEmpty(amountText)) return container;
            var label = new Label(amountText);
            label.AddToClassList("featured-value-amount");
            container.Add(label);

            return container;
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
            _ = RefreshStoreDisplayAsync();
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
                var primaryCurrency = GetFirstCurrencyKey(item);
                var amount = _controller.GetPrimaryCurrencyAmount(item);

                _modalCostAmount.text = amount.ToString();

                var currencyIcon = _controller.GetCurrencyIcon(primaryCurrency);
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

        private string GetFirstCurrencyKey(IEconomyListStoreItem item)
        {
            foreach (var currency in item.Cost.Currencies)
            {
                return currency.Key;
            }
            return "";
        }

        private void HidePurchaseModal()
        {
            _purchaseModal.style.display = DisplayStyle.None;
            _pendingPurchaseItem = null;
        }

        #endregion

        #region Reward Modal

        private void ShowRewardModal(IEconomyListStoreItem purchasedItem, IReward reward)
        {
            _rewardList.Clear();

            // Display rewards from the purchase response
            if (reward != null)
            {
                if (reward.Currencies?.Count > 0)
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

                if (reward.Items?.Count > 0)
                {
                    foreach (var item in reward.Items)
                    {
                        var count = long.Parse(item.Value);
                        var rewardElement = CreateRewardElement(
                            _controller.GetItemIcon(item.Key),
                            $"{purchasedItem.Name} x{count}"
                        );
                        _rewardList.Add(rewardElement);
                    }
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

        private void HideAllModals()
        {
            _purchaseModal.style.display = DisplayStyle.None;
            _rewardModal.style.display = DisplayStyle.None;
            _errorPopup.style.display = DisplayStyle.None;
            _pendingPurchaseItem = null;
        }

        #endregion

        #region Error Handling

        public void ShowError(string message)
        {
            _errorPopup.style.display = DisplayStyle.Flex;
            _errorMessage.text = message;
        }

        #endregion

        #region Toast

        private async void ShowToast(string message)
        {
            _toastCts?.Cancel();
            _toastCts = new CancellationTokenSource();
            var token = _toastCts.Token;

            _toastMessage.text = message;
            _toast.style.display = DisplayStyle.Flex;

            try
            {
                await Task.Delay(2500, token);
                _toast.style.display = DisplayStyle.None;
            }
            catch (OperationCanceledException)
            {
                // Toast was replaced by another, ignore
            }
        }

        #endregion
    }
}
