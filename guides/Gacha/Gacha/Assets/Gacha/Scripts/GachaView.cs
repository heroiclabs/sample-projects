using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Gacha.GachaAnim;
using Hiro;
using Hiro.Unity;
using UnityEngine;
using UnityEngine.UIElements;

namespace Gacha
{
    public sealed class GachaView : IDisposable
    {
        #region Constants
 
        private static readonly Color ColorPropertyText = new(0.314f, 0.314f, 0.314f, 1.0f);
 
        private static readonly Color ColorSlotDefault  = new(0.95f, 0.95f, 0.98f, 1.0f);
        private static readonly Color ColorSlotSelected = new(0.0f,  0.8f,  0.8f,  1.0f);

        private const int RewardSlotSize        = 100;
        private const int RewardSlotMargin      = 5;
        private const int RewardFontSize    = 14;
        private const int RewardLabelBottomOffset = 5;

        private const int   PropertyLabelMarginBottom  = 4;
        private const float PropertyLabelHeightPercent = 25f;
 
        #endregion

        private readonly GachaController _controller;
        private readonly VisualTreeAsset _inventoryItemTemplate;
        private readonly VisualTreeAsset _gachaTicketTemplate;
        private readonly Dictionary<string, Sprite> _iconDictionary;
        private readonly Sprite _defaultIcon;

        private readonly VisualElement _gachaTicketList;
        private VisualElement _gachaSelectedSlot;
        private string _pendingRefreshTicketId;

        private readonly VisualElement _gachaItemDetailsPanel;
        private readonly Label _gachaDetailsNameLabel;
        private readonly Label _gachaDetailsDescriptionLabel;
        private readonly Label _gachaDetailsCategoryLabel;
        private readonly Label _gachaDetailsQuantityLabel;
        private readonly Label _gachaDetailsStackableLabel;
        private readonly Label _gachaDetailsConsumableLabel;
        private readonly Label _gachaDetailsMaxCountLabel;
        private readonly Label _gachaDetailsRarityLabel;
        private readonly VisualElement _gachaDetailsRarityLabelBackground;
        private readonly VisualElement _gachaDetailsIcon;
        private readonly Label _gachaStringPropertiesTitle;
        private readonly VisualElement _gachaStringPropertiesList;
        private readonly Label _gachaNumericPropertiesTitle;
        private readonly VisualElement _gachaNumericPropertiesList;
        private readonly VisualElement _gachaItemRewards;

        private readonly Button _gachaConsumeButton;
        private readonly Button _gachaConsumeTenButton;

        private readonly GachaRevealPopup _revealPopup;
        private readonly GachaTenRevealPopup _tenRevealPopup;

        private readonly CancellationTokenSource _cts = new();
        private readonly object _disposeLock = new();
        private volatile bool _disposed;

        public GachaView(
            VisualElement rootElement,
            GachaController controller,
            VisualTreeAsset inventoryItemTemplate,
            VisualTreeAsset gachaTicketTemplate,
            Dictionary<string, Sprite> iconDictionary,
            Sprite defaultIcon)
        {
            _controller            = controller;
            _inventoryItemTemplate = inventoryItemTemplate;
            _gachaTicketTemplate   = gachaTicketTemplate;
            _iconDictionary        = iconDictionary;
            _defaultIcon           = defaultIcon;

            _gachaTicketList               = rootElement.Q<VisualElement>("gacha-ticket-list");
            _gachaItemDetailsPanel         = rootElement.Q<VisualElement>("gacha-item-details");
            _gachaDetailsNameLabel         = rootElement.Q<Label>("gacha-tooltip-name");
            _gachaDetailsDescriptionLabel  = rootElement.Q<Label>("gacha-tooltip-description");
            _gachaDetailsCategoryLabel     = rootElement.Q<Label>("gacha-tooltip-category");
            _gachaDetailsQuantityLabel     = rootElement.Q<Label>("gacha-tooltip-quantity");
            _gachaDetailsStackableLabel    = rootElement.Q<Label>("gacha-tooltip-stackable");
            _gachaDetailsConsumableLabel   = rootElement.Q<Label>("gacha-tooltip-consumable");
            _gachaDetailsMaxCountLabel     = rootElement.Q<Label>("gacha-tooltip-maxcount");
            _gachaDetailsRarityLabel       = rootElement.Q<Label>("gacha-tooltip-rarity");
            _gachaDetailsRarityLabelBackground = rootElement.Q<VisualElement>("gacha-tooltip-rarity-background");
            _gachaDetailsIcon              = rootElement.Q<VisualElement>("gacha-tooltip-item-icon");
            _gachaStringPropertiesTitle    = rootElement.Q<Label>("gacha-string-properties-title");
            _gachaStringPropertiesList     = rootElement.Q<VisualElement>("gacha-string-properties-list");
            _gachaNumericPropertiesTitle   = rootElement.Q<Label>("gacha-numeric-properties-title");
            _gachaNumericPropertiesList    = rootElement.Q<VisualElement>("gacha-numeric-properties-list");
            _gachaItemRewards              = rootElement.Q<VisualElement>("gacha-item-rewards");

            _gachaConsumeButton = rootElement.Q<Button>("gacha-item-consume");
            _gachaConsumeButton.RegisterCallback<ClickEvent>(_ => OnGachaConsumeClicked());
            _gachaConsumeTenButton = rootElement.Q<Button>("gacha-item-consume-10");
            _gachaConsumeTenButton.RegisterCallback<ClickEvent>(_ => OnGachaConsumeTenClicked());

            _revealPopup = new GachaRevealPopup(rootElement, _iconDictionary, _defaultIcon);
            _revealPopup.OnContinueClicked += OnRevealContinueClicked;
            _tenRevealPopup = new GachaTenRevealPopup(rootElement, _iconDictionary, _defaultIcon);
            _tenRevealPopup.OnContinueClicked += OnTenRevealContinueClicked;

            ShowEmptyState();
        }

        public void Dispose()
        {
            lock (_disposeLock)
            {
                if (_disposed) return;
                _disposed = true;
            }

            _cts.Cancel();
            _cts.Dispose();
            _revealPopup?.Dispose();
            _tenRevealPopup?.Dispose();
        }

        private void ThrowIfDisposedOrCancelled()
        {
            if (_disposed)
                throw new OperationCanceledException();
            _cts.Token.ThrowIfCancellationRequested();
        }

        public async void Activate(string selectItemId = null)
        {
            try
            {
                ThrowIfDisposedOrCancelled();
                await _controller.RefreshSystemsAsync();
                PopulateTicketList(selectItemId);
            }
            catch (OperationCanceledException) { }
            catch (Exception e)
            {
                ShowError(e.Message);
                Debug.LogException(e);
            }
        }

        private void PopulateTicketList(string selectItemId = null)
        {
            _gachaTicketList.Clear();
            _gachaSelectedSlot = null;

            var ticketItems = _controller.CodexItems.FindAll(x => x.Category == GachaConstants.CategoryGachaTicket);

            VisualElement firstSlotRoot = null;
            IInventoryItem firstItem = null;
            VisualElement targetSlotRoot = null;
            IInventoryItem targetItem = null;

            foreach (var item in ticketItems)
            {
                var itemSlot = _gachaTicketTemplate.Instantiate();
                var slotRoot = itemSlot.Q<VisualElement>("inventory-slot");

                slotRoot.userData = item;

                SetItemIcon(slotRoot.Q<VisualElement>("item-icon"), item.Id);

                var nameLabel = slotRoot.Q<Label>("item-name");
                if (nameLabel != null)
                    nameLabel.text = item.Name;

                var quantityLabel = slotRoot.Q<Label>("item-quantity");
                if (quantityLabel != null)
                {
                    var foundItem = _controller.InventoryItems.Find(x => x.Id == item.Id);
                    quantityLabel.text = $"x{foundItem?.Count ?? 0}";
                }

                slotRoot.style.backgroundColor = new StyleColor(ColorSlotDefault);
                slotRoot.RegisterCallback<ClickEvent>(_ => SelectGachaTicketSlot(item, slotRoot));

                _gachaTicketList.Add(itemSlot);

                if (firstSlotRoot == null)
                {
                    firstSlotRoot = slotRoot;
                    firstItem = item;
                }

                if (selectItemId != null && item.Id == selectItemId)
                {
                    targetSlotRoot = slotRoot;
                    targetItem = item;
                }
            }

            if (targetSlotRoot != null)
                SelectGachaTicketSlot(targetItem, targetSlotRoot);
            else if (firstSlotRoot != null)
                SelectGachaTicketSlot(firstItem, firstSlotRoot);
            else
                ShowEmptyState();

            UpdateConsumeButtons();
        }

        public void Deactivate()
        {
            _controller.SelectItem(null);
            _gachaSelectedSlot = null;
            ShowEmptyState();
        }

        #region Selection

        private void SelectGachaTicketSlot(IInventoryItem item, VisualElement slotRoot)
        {
            if (_gachaSelectedSlot != null)
            {
                _gachaSelectedSlot.style.backgroundColor = new StyleColor(ColorSlotDefault);
                SetBorderColor(_gachaSelectedSlot, Color.clear);
            }

            _gachaSelectedSlot = slotRoot;
            _controller.SelectItem(item);

            SetBorderColor(slotRoot, ColorSlotSelected);

            ShowTicketDetails(item);
            UpdateConsumeButtons();
        }

        #endregion

        #region Consume

        private async void OnGachaConsumeClicked()
        {
            _gachaConsumeButton.SetEnabled(false);
            _gachaConsumeTenButton.SetEnabled(false);
            try
            {
                ThrowIfDisposedOrCancelled();

                var selectedItem = _controller.GetSelectedItem();
                _pendingRefreshTicketId = selectedItem.Id;

                Sprite ticketSprite = null;
                _iconDictionary?.TryGetValue(selectedItem.Id, out ticketSprite);
                ticketSprite ??= _defaultIcon;

                var wonItemId = await _controller.ConsumeItemAsync(1, false);

                IInventoryItem wonItem = null;
                if (wonItemId != null)
                    _controller.CodexLookup.TryGetValue(wonItemId, out wonItem);

                if (wonItem == null)
                {
                    await RefreshAndReselectAsync(_pendingRefreshTicketId);
                    return;
                }

                _revealPopup.Show(wonItem, ticketSprite);
            }
            catch (OperationCanceledException) { }
            catch (Exception e)
            {
                UpdateConsumeButtons();
                ShowError(e.Message);
                Debug.LogException(e);
            }
        }

        private async void OnGachaConsumeTenClicked()
        {
            _gachaConsumeButton.SetEnabled(false);
            _gachaConsumeTenButton.SetEnabled(false);
            try
            {
                ThrowIfDisposedOrCancelled();

                var selectedItem = _controller.GetSelectedItem();
                _pendingRefreshTicketId = selectedItem.Id;

                Sprite ticketSprite = null;
                _iconDictionary?.TryGetValue(selectedItem.Id, out ticketSprite);
                ticketSprite ??= _defaultIcon;

                var wonItemIds = await _controller.ConsumeTenItemsAsync();

                var wonItems = new List<IInventoryItem>();
                foreach (var id in wonItemIds)
                {
                    if (_controller.CodexLookup.TryGetValue(id, out var item))
                        wonItems.Add(item);
                }

                if (wonItems.Count == 0)
                {
                    await RefreshAndReselectAsync(_pendingRefreshTicketId);
                    return;
                }

                _tenRevealPopup.Show(wonItems, ticketSprite);
            }
            catch (OperationCanceledException) { }
            catch (Exception e)
            {
                UpdateConsumeButtons();
                ShowError(e.Message);
                Debug.LogException(e);
            }
        }

        private async void OnRevealContinueClicked()
        {
            try
            {
                ThrowIfDisposedOrCancelled();
                await RefreshAndReselectAsync(_pendingRefreshTicketId);
            }
            catch (OperationCanceledException) { }
            catch (Exception e)
            {
                UpdateConsumeButtons();
                ShowError(e.Message);
                Debug.LogException(e);
            }
        }

        private async void OnTenRevealContinueClicked()
        {
            try
            {
                ThrowIfDisposedOrCancelled();
                await RefreshAndReselectAsync(_pendingRefreshTicketId);
            }
            catch (OperationCanceledException) { }
            catch (Exception e)
            {
                UpdateConsumeButtons();
                ShowError(e.Message);
                Debug.LogException(e);
            }
        }

        private async Task RefreshAndReselectAsync(string ticketId)
        {
            await _controller.RefreshSystemsAsync();
            PopulateTicketList(ticketId);
        }

        private void UpdateConsumeButtons()
        {
            var selectedItem = _controller.GetSelectedItem();
            if (selectedItem == null)
            {
                _gachaConsumeButton.SetEnabled(false);
                _gachaConsumeTenButton.SetEnabled(false);
                return;
            }

            var ticketCount = _controller.InventoryItems
                .Where(item => item.Id == selectedItem.Id)
                .Sum(item => item.Count);

            _gachaConsumeButton.SetEnabled(ticketCount >= 1);
            _gachaConsumeTenButton.SetEnabled(ticketCount >= 10);
        }

        #endregion

        #region Detail Panel

        private void ShowTicketDetails(IInventoryItem item)
        {
            _gachaDetailsNameLabel.text = item.Name;
            _gachaDetailsDescriptionLabel.text = string.IsNullOrEmpty(item.Description)
                ? "No description available."
                : item.Description;
            _gachaDetailsCategoryLabel.text = !string.IsNullOrEmpty(item.Category)
                ? item.Category
                : "Uncategorized";

            var foundItem = _controller.InventoryItems.Find(x => x.Id == item.Id);
            _gachaDetailsQuantityLabel.text = $"Quantity: {foundItem?.Count ?? 0}";

            _gachaDetailsStackableLabel.text  = "";
            _gachaDetailsConsumableLabel.text = "";
            _gachaDetailsMaxCountLabel.text   = "";

            item.NumericProperties.TryGetValue(GachaConstants.PropStarRarity, out var itemStarRarity);
            _gachaDetailsRarityLabel.text = GachaConstants.GetRarityLabel(itemStarRarity);
            _gachaDetailsRarityLabelBackground.style.unityBackgroundImageTintColor = GachaConstants.GetRarityColor(itemStarRarity);
            SetItemIcon(_gachaDetailsIcon, item.Id);

            _gachaStringPropertiesList.Clear();
            _gachaNumericPropertiesList.Clear();

            PopulateStringProperties(item);
            PopulatePityProperties(item);
            PopulateRewardsGrid(item);

            _gachaItemDetailsPanel.style.display = DisplayStyle.Flex;
        }

        private void PopulateStringProperties(IInventoryItem item)
        {
            if (item.StringProperties.Count == 0)
            {
                _gachaStringPropertiesTitle.style.display = DisplayStyle.None;
                return;
            }

            _gachaStringPropertiesTitle.style.display = DisplayStyle.Flex;
            foreach (var prop in item.StringProperties)
            {
                _gachaStringPropertiesList.Add(new Label($"\u2022 {prop.Key}: {prop.Value}")
                {
                    style =
                    {
                        marginBottom = PropertyLabelMarginBottom,
                        color = new StyleColor(ColorPropertyText)
                    }
                });
            }
        }

        private void PopulatePityProperties(IInventoryItem item)
        {
            if (item.NumericProperties.Count == 0)
            {
                _gachaNumericPropertiesTitle.style.display = DisplayStyle.None;
                return;
            }

            var statsSystem = HiroCoordinator.Instance.GetSystem<StatsSystem>();
            statsSystem.PrivateStats.TryGetValue($"{item.Id}_{GachaConstants.PropFiveStarPity}", out var fiveStarPityStat);
            statsSystem.PrivateStats.TryGetValue($"{item.Id}_{GachaConstants.PropSixStarPity}", out var sixStarPityStat);

            _gachaNumericPropertiesTitle.style.display = DisplayStyle.Flex;
            foreach (var prop in item.NumericProperties)
            {
                var label = prop.Key switch
                {
                    GachaConstants.PropFiveStarPity => new Label($"- 5\u2605 guaranteed in: {prop.Value - (fiveStarPityStat?.Value ?? 0)}"),
                    GachaConstants.PropSixStarPity  => new Label($"- 6\u2605 guaranteed in: {prop.Value - (sixStarPityStat?.Value ?? 0)}"),
                    _                           => new Label()
                };
                label.style.marginBottom = PropertyLabelMarginBottom;
                label.style.color        = new StyleColor(ColorPropertyText);
                label.style.height       = new StyleLength(Length.Percent(PropertyLabelHeightPercent));
                _gachaNumericPropertiesList.Add(label);
            }
        }

        private void ShowEmptyState()
        {
            _gachaDetailsNameLabel.text        = "No Ticket Selected";
            _gachaDetailsDescriptionLabel.text = "Select a gacha ticket to view details.";
            _gachaDetailsCategoryLabel.text    = "";
            _gachaDetailsQuantityLabel.text    = "";
            _gachaDetailsStackableLabel.text   = "";
            _gachaDetailsConsumableLabel.text  = "";
            _gachaDetailsMaxCountLabel.text    = "";
            _gachaDetailsRarityLabel.text      = "";

            _gachaStringPropertiesList.Clear();
            _gachaNumericPropertiesList.Clear();
            _gachaStringPropertiesTitle.style.display  = DisplayStyle.None;
            _gachaNumericPropertiesTitle.style.display = DisplayStyle.None;
            _gachaItemRewards.Clear();

            _gachaItemDetailsPanel.style.display = DisplayStyle.Flex;
        }

        private void PopulateRewardsGrid(IInventoryItem ticket)
        {
            _gachaItemRewards.Clear();

            var itemSets = ticket.ConsumeAvailableRewards.Weighted
                .SelectMany(x => x.ItemSets)
                .SelectMany(y => y.Set);

            var rewardItems = _controller.CodexItems
                .FindAll(rewardItem => rewardItem.ItemSets.Any(itemSets.Contains))
                .OrderByDescending(rewardItem => rewardItem.NumericProperties.TryGetValue(GachaConstants.PropStarRarity, out var rarity)
                    ? rarity
                    : default);

            foreach (var rewardItem in rewardItems)
            {
                var rewardSlot = _inventoryItemTemplate.Instantiate();
                var slotRoot   = rewardSlot.Q<VisualElement>("inventory-slot");

                slotRoot.style.width        = RewardSlotSize;
                slotRoot.style.height       = RewardSlotSize;
                slotRoot.style.marginTop    = RewardSlotMargin;
                slotRoot.style.marginRight  = RewardSlotMargin;
                slotRoot.style.marginBottom = RewardSlotMargin;
                slotRoot.style.marginLeft   = RewardSlotMargin;

                rewardItem.NumericProperties.TryGetValue(GachaConstants.PropStarRarity, out var starRarity);
                slotRoot.style.backgroundColor = new StyleColor(GachaConstants.GetRarityColor(starRarity));

                var iconContainer = slotRoot.Q<VisualElement>("item-icon");
                SetItemIcon(iconContainer, rewardItem.Id);
                iconContainer.style.marginBottom = RewardSlotMargin;

                var quantityLabel = slotRoot.Q<Label>("item-quantity");
                if (quantityLabel != null)
                {
                    quantityLabel.text                  = GachaConstants.GetRarityShortLabel(starRarity);
                    quantityLabel.style.fontSize        = RewardFontSize;
                    quantityLabel.style.color           = new StyleColor(Color.white);
                    quantityLabel.style.unityTextAlign  = TextAnchor.LowerCenter;
                    quantityLabel.style.bottom          = RewardLabelBottomOffset;
                    quantityLabel.style.right           = StyleKeyword.Auto;
                    quantityLabel.style.left            = 0;
                    quantityLabel.style.width           = new StyleLength(Length.Percent(100));
                }

                _gachaItemRewards.Add(rewardSlot);
            }
        }

        #endregion

        #region Visual Helpers

        private void SetItemIcon(VisualElement iconContainer, string itemId)
        {
            if (_iconDictionary != null && _iconDictionary.TryGetValue(itemId, out var icon) && icon != null)
                iconContainer.style.backgroundImage = new StyleBackground(icon);
            else if (_defaultIcon != null)
                iconContainer.style.backgroundImage = new StyleBackground(_defaultIcon);
            else
            {
                iconContainer.style.backgroundImage = StyleKeyword.Null;
                Debug.LogWarning($"No icon mapping found for item ID: {itemId}");
            }
        }

        private static void SetBorderColor(VisualElement element, Color color)
        {
            element.style.borderTopColor    = new StyleColor(color);
            element.style.borderBottomColor = new StyleColor(color);
            element.style.borderLeftColor   = new StyleColor(color);
            element.style.borderRightColor  = new StyleColor(color);
        }

        private void ShowError(string message) =>
            Debug.LogError($"[GachaView] {message}");

        #endregion
    }
}
