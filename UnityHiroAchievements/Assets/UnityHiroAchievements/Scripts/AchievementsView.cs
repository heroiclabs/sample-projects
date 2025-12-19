using System;
using System.Linq;
using System.Threading.Tasks;
using Hiro;
using HeroicUI;
using UnityEngine;
using UnityEngine.UIElements;

namespace HiroAchievements
{
    public sealed class AchievementsView
    {
        private readonly AchievementsController _controller;
        private readonly HiroAchievementsCoordinator _coordinator;
        private readonly VisualTreeAsset _achievementItemTemplate;
        private readonly VisualTreeAsset _subAchievementItemTemplate;
        private readonly Sprite _defaultIcon;

        private WalletDisplay _walletDisplay;

        private Button _dailiesTabButton;
        private Button _questsTabButton;
        private Button _achievementsTabButton;
        private Button _refreshButton;

        private VisualElement _achievementsList;
        private VisualElement _achievementDetailsPanel;
        private Label _detailsNameLabel;
        private Label _detailsDescriptionLabel;
        private Label _detailsCategoryLabel;
        private Label _detailsProgressLabel;
        private VisualElement _detailsProgressBar;
        private VisualElement _detailsProgressFill;
        private VisualElement _detailsSubAchievementsContainer;
        private VisualElement _detailsRewardsContainer;
        private Button _progressButton;
        private Button _claimButton;

        private VisualElement _progressModal;
        private IntegerField _progressQuantityField;
        private Button _progressModalButton;
        private Button _progressModalCloseButton;

        private VisualElement _errorPopup;
        private Button _errorCloseButton;
        private Label _errorMessage;

        private VisualElement _selectedAchievementElement;
        private VisualElement _selectedSubAchievementElement;
        private SubAchievementItemView _selectedSubAchievementView;
        private System.Collections.Generic.Dictionary<VisualElement, SubAchievementItemView> _subAchievementViews = new System.Collections.Generic.Dictionary<VisualElement, SubAchievementItemView>();
        private UIDocument _uiDocument;

        public AchievementsView(AchievementsController controller, HiroAchievementsCoordinator coordinator,
            VisualTreeAsset achievementItemTemplate, VisualTreeAsset subAchievementItemTemplate, Sprite defaultIcon)
        {
            _controller = controller;
            _coordinator = coordinator;
            _achievementItemTemplate = achievementItemTemplate;
            _subAchievementItemTemplate = subAchievementItemTemplate;
            _defaultIcon = defaultIcon;

            _ = InitializeUI();
        }

        #region UI Initialization

        private async Task InitializeUI()
        {
            _uiDocument = _controller.GetComponent<UIDocument>();
            var rootElement = _uiDocument.rootVisualElement;

            _walletDisplay = new WalletDisplay(rootElement.Q<VisualElement>("wallet-display"));

            // Tab buttons
            _dailiesTabButton = rootElement.Q<Button>("tab-dailies");
            _questsTabButton = rootElement.Q<Button>("tab-quests");
            _achievementsTabButton = rootElement.Q<Button>("tab-achievements");

            _dailiesTabButton.RegisterCallback<ClickEvent>(_ => SwitchTab("daily"));
            _questsTabButton.RegisterCallback<ClickEvent>(_ => SwitchTab("quest"));
            _achievementsTabButton.RegisterCallback<ClickEvent>(_ => SwitchTab("achievement"));

            // Achievements list
            _achievementsList = rootElement.Q<VisualElement>("achievements-list");

            // Achievement details panel
            _achievementDetailsPanel = rootElement.Q<VisualElement>("achievement-details");
            _detailsNameLabel = rootElement.Q<Label>("details-name");
            _detailsDescriptionLabel = rootElement.Q<Label>("details-description");
            _detailsCategoryLabel = rootElement.Q<Label>("details-category");
            _detailsProgressLabel = rootElement.Q<Label>("details-progress");
            _detailsProgressBar = rootElement.Q<VisualElement>("details-progress-bar");
            _detailsProgressFill = rootElement.Q<VisualElement>("details-progress-fill");
            _detailsSubAchievementsContainer = rootElement.Q<VisualElement>("details-sub-achievements");
            _detailsRewardsContainer = rootElement.Q<VisualElement>("details-rewards");

            _progressButton = rootElement.Q<Button>("progress-button");
            _progressButton.RegisterCallback<ClickEvent>(_ =>
            {
                if (_controller.GetSelectedAchievement() == null) return;
                _progressQuantityField.value = 1;
                
                // Show which achievement will be updated in debug log
                var subAch = _controller.GetSelectedSubAchievement();
                if (subAch != null)
                {
                    Debug.Log($"Opening progress modal for sub-achievement: {subAch.Name}");
                }
                else
                {
                    Debug.Log($"Opening progress modal for achievement: {_controller.GetSelectedAchievement().Name}");
                }
                
                _progressModal.style.display = DisplayStyle.Flex;
            });

            _claimButton = rootElement.Q<Button>("claim-button");
            _claimButton.RegisterCallback<ClickEvent>(evt => _ = HandleClaimReward());

            ShowEmptyState();

            _refreshButton = rootElement.Q<Button>("achievements-refresh");
            _refreshButton.RegisterCallback<ClickEvent>(evt => _ = RefreshAchievementsList());

            // Progress modal
            _progressModal = rootElement.Q<VisualElement>("progress-modal");
            _progressQuantityField = rootElement.Q<IntegerField>("progress-modal-quantity");
            _progressModalButton = rootElement.Q<Button>("progress-modal-update");
            _progressModalButton.RegisterCallback<ClickEvent>(evt => _ = HandleUpdateProgress());
            _progressModalCloseButton = rootElement.Q<Button>("progress-modal-close");
            _progressModalCloseButton.RegisterCallback<ClickEvent>(_ =>
                _progressModal.style.display = DisplayStyle.None);

            // Error popup
            _errorPopup = rootElement.Q<VisualElement>("error-popup");
            _errorMessage = rootElement.Q<Label>("error-message");
            _errorCloseButton = rootElement.Q<Button>("error-close");
            _errorCloseButton.RegisterCallback<ClickEvent>(_ => _errorPopup.style.display = DisplayStyle.None);

            UpdateTabButtons();
            await UpdateActionButtons();
        }

        public void StartObservingWallet()
        {
          _walletDisplay.StartObserving();
        }

        #endregion

        #region Tab Management

        private async void SwitchTab(string category)
        {
            _controller.SetCurrentCategory(category);
            UpdateTabButtons();
            await RefreshAchievementsList();
        }

        private void UpdateTabButtons()
        {
            var currentCategory = _controller.GetCurrentCategory();

            // Remove selected class from all tabs
            _dailiesTabButton.RemoveFromClassList("selected");
            _questsTabButton.RemoveFromClassList("selected");
            _achievementsTabButton.RemoveFromClassList("selected");

            // Add selected class to current tab
            switch (currentCategory)
            {
                case "daily":
                    _dailiesTabButton.AddToClassList("selected");
                    break;
                case "quest":
                    _questsTabButton.AddToClassList("selected");
                    break;
                default:
                    _achievementsTabButton.AddToClassList("selected");
                    break;
            }
        }

        #endregion

        #region Achievement List Display

        public async Task RefreshAchievementsList()
        {
            try
            {
                UpdateTabButtons();
                var achievements = await _controller.RefreshAchievements();
                PopulateAchievementsList(achievements);
            }
            catch (Exception e)
            {
                Debug.LogException(e);
                ShowError(e.Message);
            }
        }

        private void PopulateAchievementsList(System.Collections.Generic.List<IAchievement> achievements)
        {
            _achievementsList.Clear();
            Debug.Log(achievements.Count);
            foreach (var achievement in achievements)
            {
              Debug.Log("Adding Achievement");
                var achievementElement = _achievementItemTemplate.Instantiate();
                var container = achievementElement.Q<VisualElement>("achievement-item-container");

                // Set achievement icon
                var iconContainer = container.Q<VisualElement>("achievement-icon");
                SetAchievementIcon(iconContainer, achievement.Id);

                // Set achievement name
                var nameLabel = container.Q<Label>("achievement-name");
                nameLabel.text = achievement.Name;

                // Set sub-achievements count
                var subAchievementsLabel = container.Q<Label>("sub-achievements-text");
                if (subAchievementsLabel != null)
                {
                    if (achievement.SubAchievements != null && achievement.SubAchievements.Count > 0)
                    {
                        int completedCount = 0;
                        foreach (var subAchievement in achievement.SubAchievements)
                        {
                            if (subAchievement.Value.Count >= subAchievement.Value.MaxCount)
                            {
                                completedCount++;
                            }
                        }
                        subAchievementsLabel.text = $"{completedCount}/{achievement.SubAchievements.Count} Objectives";
                        subAchievementsLabel.style.display = DisplayStyle.Flex;
                    }
                    else
                    {
                        subAchievementsLabel.style.display = DisplayStyle.None;
                    }
                }

                // Set status badge
                var statusBadge = container.Q<VisualElement>("status-badge");
                var statusLabel = statusBadge.Q<Label>("status-text");
                bool isLocked = _controller.IsAchievementLocked(achievement);

                if (_controller.IsAchievementCompleted(achievement))
                {
                    statusLabel.text = AchievementsUIConstants.StatusComplete;
                    statusBadge.style.backgroundColor = AchievementsUIConstants.StatusCompleteColor;
                }
                else if (isLocked)
                {
                    // Get incomplete prerequisites count
                    var incompletePrereqs = _controller.GetIncompletePrerequisiteNames(achievement);
                    int incompleteCount = incompletePrereqs.Count;

                    // Show locked with count of remaining prerequisites
                    if (incompleteCount > 0)
                    {
                        statusLabel.text = $"{AchievementsUIConstants.StatusLocked} ({incompleteCount})";
                    }
                    else
                    {
                        statusLabel.text = AchievementsUIConstants.StatusLocked;
                    }
                    statusBadge.style.backgroundColor = AchievementsUIConstants.StatusLockedColor;

                    // Locked achievements are clickable to view prerequisites
                    if (incompleteCount > 0)
                    {
                        var prerequisitesText = string.Join(", ", incompletePrereqs);
                        Debug.Log($"Locked achievement '{achievement.Name}' requires: {prerequisitesText}");
                    }
                }
                else if(_controller.IsAchievementClaimable(achievement))
                {
                    statusLabel.text = AchievementsUIConstants.StatusToClaim;
                    statusBadge.style.backgroundColor = AchievementsUIConstants.StatusToClaimColor;
                }
                else
                {
                    statusLabel.text = AchievementsUIConstants.StatusInProgress;
                    statusBadge.style.backgroundColor = AchievementsUIConstants.StatusInProgressColor;
                }

                // Set progress using helper
                var progressBar = container.Q<VisualElement>("achievement-progress-bar");
                var progressFill = container.Q<VisualElement>("achievement-progress-fill");
                float progressPercent = AchievementProgressHelper.CalculateProgressPercent(achievement);
                progressFill.style.width = Length.Percent(Mathf.Clamp(progressPercent, 0f, 100f));

                // Register click event for ALL achievements (including locked)
                // Locked achievements can be viewed to see prerequisites, just not progressed
                container.RegisterCallback<ClickEvent>(async evt =>
                {
                    await SelectAchievement(achievement, container);
                    evt.StopPropagation();
                });

                _achievementsList.Add(achievementElement);
            }
        }

        private async Task SelectAchievement(IAchievement achievement, VisualElement element)
        {
            // Deselect previous
            if (_selectedAchievementElement != null)
            {
                _selectedAchievementElement.RemoveFromClassList("achievement-item--selected");
            }

            // Select new
            _controller.SelectAchievement(achievement);
            _selectedAchievementElement = element;
            _selectedAchievementElement.AddToClassList("achievement-item--selected");

            await ShowAchievementDetailsAsync(achievement);
            await UpdateActionButtons();
        }

        private void SetAchievementIcon(VisualElement iconContainer, string achievementId)
        {
            if (_controller.IconDictionary.TryGetValue(achievementId, out var icon))
            {
                iconContainer.style.backgroundImage = new StyleBackground(icon);
            }
            else if (_defaultIcon != null)
            {
                iconContainer.style.backgroundImage = new StyleBackground(_defaultIcon);
            }
            else
            {
                iconContainer.style.backgroundImage = StyleKeyword.Null;
                Debug.LogWarning($"No icon mapping found for achievement ID: {achievementId}");
            }
        }

        private static readonly System.Collections.Generic.Dictionary<string, string> ItemRarities = new()
        {
            // Currencies
            { "coins", "common" },
            { "gems", "rare" },
            // Items from inventory
            { "iron_sword", "common" },
            { "mana_potion", "uncommon" },
            { "golden_key", "rare" },
            { "lucky_charm", "epic" }
        };

        private string GetRarity(string itemId)
        {
            return ItemRarities.TryGetValue(itemId.ToLower(), out var rarity) ? rarity : "common";
        }

        private VisualElement CreateRewardTile(string rewardType, long amount)
        {
            var tile = new VisualElement();
            tile.AddToClassList("reward-tile");

            var rarity = GetRarity(rewardType);
            tile.AddToClassList($"reward-tile--{rarity}");

            // Icon container (colored background based on rarity)
            var iconContainer = new VisualElement();
            iconContainer.AddToClassList("reward-tile__icon-container");

            // Icon image (smaller, inside container)
            var iconImage = new VisualElement();
            iconImage.AddToClassList("reward-tile__icon");

            // Set icon based on reward type
            if (_controller.IconDictionary.TryGetValue(rewardType, out var rewardIcon))
            {
                iconImage.style.backgroundImage = new StyleBackground(rewardIcon);
            }
            else if (_defaultIcon != null)
            {
                iconImage.style.backgroundImage = new StyleBackground(_defaultIcon);
            }
            iconContainer.Add(iconImage);
            tile.Add(iconContainer);

            var amountLabel = new Label(amount.ToString());
            amountLabel.AddToClassList("reward-tile__amount");
            tile.Add(amountLabel);

            return tile;
        }

        #endregion

        #region Achievement Details

        private async Task ShowAchievementDetailsAsync(IAchievement achievement)
        {
            bool isLocked = _controller.IsAchievementLocked(achievement);
            
            _detailsNameLabel.text = achievement.Name;
            
            // Add locked indicator to description if achievement is locked
            if (isLocked)
            {
                var prerequisites = _controller.GetPrerequisiteAchievements(achievement);
                string prerequisitesText = PrerequisiteDisplayHelper.FormatPrerequisitesList(prerequisites, _controller);

                _detailsDescriptionLabel.text = AchievementsUIConstants.LockedDescriptionPrefix +
                    (string.IsNullOrEmpty(achievement.Description)
                        ? "No description available."
                        : achievement.Description) +
                    prerequisitesText;
                _detailsDescriptionLabel.style.color = AchievementsUIConstants.StatusLockedColor;
            }
            else
            {
                _detailsDescriptionLabel.text = string.IsNullOrEmpty(achievement.Description)
                    ? "No description available."
                    : achievement.Description;
                _detailsDescriptionLabel.style.color = new StyleColor(StyleKeyword.Null); // Reset to default
            }
            
            _detailsCategoryLabel.text = achievement.Category ?? "Uncategorized";

            // Progress - use helper to calculate
            var (currentProgress, maxProgress) = AchievementProgressHelper.GetProgressValues(achievement);
            float progressPercent = AchievementProgressHelper.CalculateProgressPercent(achievement);
            
            _detailsProgressLabel.text = string.Format(AchievementsUIConstants.ProgressFormat, 
                currentProgress, maxProgress, progressPercent);
            _detailsProgressFill.style.width = Length.Percent(Mathf.Clamp(progressPercent, 0f, 100f));

            // Sub-Achievements
            if (_detailsSubAchievementsContainer != null)
            {
                _detailsSubAchievementsContainer.Clear();
                _subAchievementViews.Clear(); // Clear the views dictionary
                
                if (achievement.SubAchievements != null && achievement.SubAchievements.Count > 0)
                {
                    _detailsSubAchievementsContainer.style.display = DisplayStyle.Flex;
                    
                    foreach (var subAchievementPair in achievement.SubAchievements)
                    {
                        var subAchievementElement = CreateSubAchievementElement(subAchievementPair.Value, achievement, subAchievementPair.Key);
                        _detailsSubAchievementsContainer.Add(subAchievementElement);
                    }
                }
                else
                {
                    _detailsSubAchievementsContainer.style.display = DisplayStyle.None;
                }
            }

            // Rewards - displayed as colored tile icons
            _detailsRewardsContainer.Clear();
            if (achievement.HasAvailableReward() && achievement.AvailableRewards != null)
            {
                // Display currency rewards as colored tiles
                if (achievement.AvailableRewards.Guaranteed?.Currencies != null)
                {
                    foreach (var currencyPair in achievement.AvailableRewards.Guaranteed.Currencies)
                    {
                        var rewardTile = CreateRewardTile(currencyPair.Key, currencyPair.Value.Count.Min);
                        _detailsRewardsContainer.Add(rewardTile);
                    }
                }

                // Display item rewards if any
                if (achievement.AvailableRewards.Guaranteed?.Items != null)
                {
                    foreach (var itemPair in achievement.AvailableRewards.Guaranteed.Items)
                    {
                        var rewardTile = CreateRewardTile(itemPair.Key, itemPair.Value.Count.Min);
                        _detailsRewardsContainer.Add(rewardTile);
                    }
                }
            }
            else
            {
                var noRewardLabel = new Label("No rewards available");
                noRewardLabel.style.fontSize = 18;
                noRewardLabel.style.color = new Color(0.5f, 0.5f, 0.5f);
                _detailsRewardsContainer.Add(noRewardLabel);
            }

            _achievementDetailsPanel.style.display = DisplayStyle.Flex;
            await UpdateActionButtons();
        }

        private VisualElement CreateSubAchievementElement(ISubAchievement subAchievement, IAchievement parent, string key)
        {
            // Instantiate the template
            var subAchievementElement = _subAchievementItemTemplate.Instantiate();
            
            // Create view and set data
            var subAchievementView = new SubAchievementItemView();
            subAchievementView.SetVisualElement(subAchievementElement);
            subAchievementView.SetSubAchievement(subAchievement);
            
            var container = subAchievementView.GetContainer();
            
            // Store view for later access
            _subAchievementViews[container] = subAchievementView;

            // Add hover effect
            container.RegisterCallback<MouseEnterEvent>(_ =>
            {
                if (_selectedSubAchievementElement != container)
                {
                    subAchievementView.SetHovered(true);
                }
            });
            container.RegisterCallback<MouseLeaveEvent>(_ =>
            {
                // Only reset if not selected
                if (_selectedSubAchievementElement != container)
                {
                    subAchievementView.SetHovered(false);
                    subAchievementView.SetSelected(false);
                }
            });

            // Make clickable - pass sub-achievement, parent, and key
            container.RegisterCallback<ClickEvent>(async evt =>
            {
                await SelectSubAchievement(subAchievement, parent, key, container);
                evt.StopPropagation();
            });

            return subAchievementElement;
        }

        private async Task SelectSubAchievement(ISubAchievement subAchievement, IAchievement parent, string key, VisualElement element)
        {
            if (subAchievement == null)
            {
                Debug.LogError("SelectSubAchievement called with NULL sub-achievement!");
                return;
            }

            Debug.Log($"VIEW: SelectSubAchievement called with: {subAchievement.Name} (ID: {subAchievement.Id})");

            // Deselect previous sub-achievement
            if (_selectedSubAchievementView != null && _selectedSubAchievementElement != null)
            {
                _selectedSubAchievementView.SetSelected(false);
            }

            // Select new sub-achievement
            _controller.SelectSubAchievement(subAchievement, parent, key);
            _selectedSubAchievementElement = element;
            
            // Get the view from dictionary and mark as selected
            if (_subAchievementViews.TryGetValue(element, out var view))
            {
                _selectedSubAchievementView = view;
                _selectedSubAchievementView.SetSelected(true);
            }

            Debug.Log($"VIEW: Sub-achievement UI updated");
            await UpdateActionButtons();
        }

        private void ShowEmptyState()
        {
            _detailsNameLabel.text = "No Achievement Selected";
            _detailsDescriptionLabel.text = "Select an achievement from the list to view details.";
            _detailsCategoryLabel.text = "";
            _detailsProgressLabel.text = "";
            _detailsProgressFill.style.width = Length.Percent(0);
            if (_detailsSubAchievementsContainer != null)
            {
                _detailsSubAchievementsContainer.Clear();
                _detailsSubAchievementsContainer.style.display = DisplayStyle.None;
            }
            _detailsRewardsContainer.Clear();

            _achievementDetailsPanel.style.display = DisplayStyle.Flex;
        }

        #endregion

        #region Action Buttons

        private async Task UpdateActionButtons()
        {
            await _controller.RefreshAchievements();
            var selectedAchievement = _controller.GetSelectedAchievement();
            var selectedSubAchievement = _controller.GetSelectedSubAchievement();

            if (selectedAchievement == null)
            {
                _progressButton.SetEnabled(false);
                _claimButton.SetEnabled(false);
                return;
            }

            // Check if main achievement is locked - if so, disable all buttons
            bool isLocked = _controller.IsAchievementLocked(selectedAchievement);
            if (isLocked)
            {
                _progressButton.SetEnabled(false);
                _claimButton.SetEnabled(false);
                Debug.Log("Achievement is locked - buttons disabled");
                return;
            }

            // Check if a sub-achievement is selected
            if (selectedSubAchievement != null)
            {
                // Use sub-achievement overloads
                bool isCompleted = _controller.IsAchievementCompleted(selectedSubAchievement);
                _progressButton.SetEnabled(!isCompleted);

                bool canClaim = _controller.CanClaimReward(selectedSubAchievement);
                Debug.Log($"Sub-achievement can claim: {canClaim}");
                _claimButton.SetEnabled(canClaim);
            }
            else
            {
                // Use main achievement overloads
                bool isCompleted = _controller.IsAchievementCompleted(selectedAchievement);
                _progressButton.SetEnabled(!isCompleted);

                bool canClaim = _controller.CanClaimReward(selectedAchievement);
                Debug.Log($"Main achievement can claim: {canClaim}");
                _claimButton.SetEnabled(canClaim);
            }
        }

        #endregion

        #region Action Handlers

        private async Task HandleUpdateProgress()
        {
            try
            {
                await _controller.UpdateAchievementProgress(_progressQuantityField.value);
                _progressModal.style.display = DisplayStyle.None;
                await RefreshAchievementsList();

                // Re-select and show updated details
                var selectedAchievement = _controller.GetSelectedAchievement();
                if (selectedAchievement != null)
                {
                    // Refresh achievement data
                    var updatedAchievement = _controller.AllAchievements
                        .FirstOrDefault(a => a.Id == selectedAchievement.Id);
                    if (updatedAchievement != null)
                    {
                        await ShowAchievementDetailsAsync(updatedAchievement);
                    }
                }

                await UpdateActionButtons();
            }
            catch (Exception e)
            {
                ShowError(e.Message);
                Debug.LogError(e);
            }
        }

        private async Task HandleClaimReward()
        {
            try
            {
                await _controller.ClaimAchievementReward(claimTotal: true);
                await RefreshAchievementsList();

                // Re-select and show updated details
                var selectedAchievement = _controller.GetSelectedAchievement();
                if (selectedAchievement != null)
                {
                    var updatedAchievement = _controller.AllAchievements
                        .FirstOrDefault(a => a.Id == selectedAchievement.Id);
                    if (updatedAchievement != null)
                    {
                        await ShowAchievementDetailsAsync(updatedAchievement);
                    }
                }

                await UpdateActionButtons();
            }
            catch (Exception e)
            {
                ShowError(e.Message);
                Debug.LogError(e);
            }
        }

        public void ShowError(string message)
        {
            _errorPopup.style.display = DisplayStyle.Flex;
            _errorMessage.text = message;
        }

        public void HideAllModals()
        {
            _progressModal.style.display = DisplayStyle.None;
            _errorPopup.style.display = DisplayStyle.None;
        }

        #endregion
    }
}