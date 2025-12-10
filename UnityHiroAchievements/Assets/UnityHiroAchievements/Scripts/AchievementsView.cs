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
        private UIDocument _uiDocument;

        public AchievementsView(AchievementsController controller, HiroAchievementsCoordinator coordinator,
            VisualTreeAsset achievementItemTemplate, Sprite defaultIcon)
        {
            _controller = controller;
            _coordinator = coordinator;
            _achievementItemTemplate = achievementItemTemplate;
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

            _dailiesTabButton.RegisterCallback<ClickEvent>(_ => SwitchTab("dailies"));
            _questsTabButton.RegisterCallback<ClickEvent>(_ => SwitchTab("quests"));
            _achievementsTabButton.RegisterCallback<ClickEvent>(_ => SwitchTab("all"));

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

            // Remove active class from all tabs
            _dailiesTabButton.RemoveFromClassList("active-tab");
            _questsTabButton.RemoveFromClassList("active-tab");
            _achievementsTabButton.RemoveFromClassList("active-tab");

            // Add active class to current tab
            switch (currentCategory)
            {
                case "dailies":
                    _dailiesTabButton.AddToClassList("active-tab");
                    break;
                case "quests":
                    _questsTabButton.AddToClassList("active-tab");
                    break;
                default:
                    _achievementsTabButton.AddToClassList("active-tab");
                    break;
            }
        }

        #endregion

        #region Achievement List Display

        public async Task RefreshAchievementsList()
        {
            try
            {
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

                if (_controller.IsAchievementCompleted(achievement))
                {
                    statusLabel.text = achievement.ClaimTimeSec > 0 ? "Claimed" : "Complete";
                    statusBadge.style.backgroundColor = achievement.ClaimTimeSec > 0
                        ? new Color(0.6f, 0.6f, 0.6f, 1f)
                        : new Color(0.4f, 0.8f, 0.4f, 1f);
                }
                else if (_controller.IsAchievementLocked(achievement))
                {
                    statusLabel.text = "Locked";
                    statusBadge.style.backgroundColor = new Color(0.5f, 0.5f, 0.5f, 1f);
                }
                else
                {
                    statusLabel.text = "In Progress";
                    statusBadge.style.backgroundColor = new Color(0.5f, 0.6f, 1f, 1f);
                }

                // Set progress
                var progressBar = container.Q<VisualElement>("achievement-progress-bar");
                var progressFill = container.Q<VisualElement>("achievement-progress-fill");
                float progressPercent = achievement.MaxCount > 0
                    ? (float)achievement.Count / achievement.MaxCount * 100f
                    : 0f;
                progressFill.style.width = Length.Percent(Mathf.Clamp(progressPercent, 0f, 100f));

                // Register click event
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
                _selectedAchievementElement.RemoveFromClassList("selected-achievement");
                // Reset visual style
                _selectedAchievementElement.style.borderTopColor = new StyleColor(new Color(0.776f, 0.765f, 0.894f, 0.5f));
                _selectedAchievementElement.style.borderBottomColor = new StyleColor(new Color(0.776f, 0.765f, 0.894f, 0.5f));
                _selectedAchievementElement.style.borderLeftColor = new StyleColor(new Color(0.776f, 0.765f, 0.894f, 0.5f));
                _selectedAchievementElement.style.borderRightColor = new StyleColor(new Color(0.776f, 0.765f, 0.894f, 0.5f));
                _selectedAchievementElement.style.backgroundColor = new StyleColor(StyleKeyword.Null);
            }

            // Select new
            _controller.SelectAchievement(achievement);
            _selectedAchievementElement = element;
            _selectedAchievementElement.AddToClassList("selected-achievement");
            // Add visual highlight
            _selectedAchievementElement.style.borderTopColor = new Color(0.5f, 0.6f, 1f, 1f);
            _selectedAchievementElement.style.borderBottomColor = new Color(0.5f, 0.6f, 1f, 1f);
            _selectedAchievementElement.style.borderLeftColor = new Color(0.5f, 0.6f, 1f, 1f);
            _selectedAchievementElement.style.borderRightColor = new Color(0.5f, 0.6f, 1f, 1f);
            _selectedAchievementElement.style.backgroundColor = new Color(0.9f, 0.92f, 1f, 0.3f);

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

        #endregion

        #region Achievement Details

        private async Task ShowAchievementDetailsAsync(IAchievement achievement)
        {
            _detailsNameLabel.text = achievement.Name;
            _detailsDescriptionLabel.text = string.IsNullOrEmpty(achievement.Description)
                ? "No description available."
                : achievement.Description;
            _detailsCategoryLabel.text = achievement.Category ?? "Uncategorized";

            // Progress
            float progressPercent = achievement.MaxCount > 0 
                ? (float)achievement.Count / achievement.MaxCount * 100f 
                : 0f;
            _detailsProgressLabel.text = $"Progress: {achievement.Count} / {achievement.MaxCount} ({progressPercent:F0}%)";
            _detailsProgressFill.style.width = Length.Percent(Mathf.Clamp(progressPercent, 0f, 100f));

            // Sub-Achievements
            if (_detailsSubAchievementsContainer != null)
            {
                _detailsSubAchievementsContainer.Clear();
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

            // Rewards
            _detailsRewardsContainer.Clear();
            if (achievement.HasReward())
            {
                // Display rewards (you'll need to customize this based on your reward structure)
                var rewardLabel = new Label("Rewards Available");
                rewardLabel.style.fontSize = 20;
                rewardLabel.style.color = new Color(0.8f, 0.6f, 0.2f);
                rewardLabel.style.marginBottom = 10;
                _detailsRewardsContainer.Add(rewardLabel);

                // Example reward display - customize based on your reward data structure
                if (achievement.Reward != null)
                {
                    var rewardText = new Label($"• Reward: {achievement.Reward}");
                    rewardText.style.fontSize = 18;
                    _detailsRewardsContainer.Add(rewardText);
                }
            }
            else
            {
                var noRewardLabel = new Label("No rewards for this achievement");
                noRewardLabel.style.fontSize = 18;
                noRewardLabel.style.color = new Color(0.5f, 0.5f, 0.5f);
                _detailsRewardsContainer.Add(noRewardLabel);
            }

            _achievementDetailsPanel.style.display = DisplayStyle.Flex;
            await UpdateActionButtons();
        }

        private VisualElement CreateSubAchievementElement(ISubAchievement subAchievement, IAchievement parent, string key)
        {
            // Container for the sub-achievement
            var container = new VisualElement();
            container.style.marginBottom = 10;
            container.style.paddingTop = 10;
            container.style.paddingBottom = 10;
            container.style.paddingLeft = 10;
            container.style.paddingRight = 10;
            container.style.borderTopLeftRadius = 8;
            container.style.borderTopRightRadius = 8;
            container.style.borderBottomLeftRadius = 8;
            container.style.borderBottomRightRadius = 8;
            container.style.borderLeftWidth = 2;
            container.style.borderRightWidth = 2;
            container.style.borderTopWidth = 2;
            container.style.borderBottomWidth = 2;
            container.style.borderLeftColor = new Color(0.7f, 0.7f, 0.7f, 1f);
            container.style.borderRightColor = new Color(0.7f, 0.7f, 0.7f, 1f);
            container.style.borderTopColor = new Color(0.7f, 0.7f, 0.7f, 1f);
            container.style.borderBottomColor = new Color(0.7f, 0.7f, 0.7f, 1f);
            container.style.backgroundColor = new Color(0.95f, 0.95f, 0.95f, 1f);

            // Add hover effect
            container.RegisterCallback<MouseEnterEvent>(_ =>
            {
                if (_selectedSubAchievementElement != container)
                {
                    container.style.backgroundColor = new Color(0.9f, 0.9f, 1f, 1f);
                }
            });
            container.RegisterCallback<MouseLeaveEvent>(_ =>
            {
                // Only reset if not selected
                if (_selectedSubAchievementElement != container)
                {
                    container.style.backgroundColor = new Color(0.95f, 0.95f, 0.95f, 1f);
                }
            });

            // Make clickable - pass sub-achievement, parent, and key
            container.RegisterCallback<ClickEvent>(async evt =>
            {
                await SelectSubAchievement(subAchievement, parent, key, container);
                evt.StopPropagation();
            });

            // Top row with name and status
            var topRow = new VisualElement();
            topRow.style.flexDirection = FlexDirection.Row;
            topRow.style.justifyContent = Justify.SpaceBetween;
            topRow.style.alignItems = Align.Center;
            topRow.style.marginBottom = 8;

            var nameLabel = new Label(subAchievement.Name);
            nameLabel.style.fontSize = 18;
            nameLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
            nameLabel.style.color = new Color(0, 0, 0, 1f);
            topRow.Add(nameLabel);

            // Status badge with progress or checkmark
            var statusBadge = new VisualElement();
            statusBadge.style.paddingTop = 4;
            statusBadge.style.paddingBottom = 4;
            statusBadge.style.paddingLeft = 12;
            statusBadge.style.paddingRight = 12;
            statusBadge.style.borderTopLeftRadius = 12;
            statusBadge.style.borderTopRightRadius = 12;
            statusBadge.style.borderBottomLeftRadius = 12;
            statusBadge.style.borderBottomRightRadius = 12;

            bool isCompleted = subAchievement.Count >= subAchievement.MaxCount;
            if (isCompleted)
            {
                statusBadge.style.backgroundColor = new Color(0.4f, 0.8f, 0.4f, 1f);
                var checkLabel = new Label("✓");
                checkLabel.style.fontSize = 18;
                checkLabel.style.color = new Color(1, 1, 1, 1f);
                checkLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
                statusBadge.Add(checkLabel);
            }
            else
            {
                statusBadge.style.backgroundColor = new Color(0.5f, 0.6f, 1f, 1f);
                var progressInnerLabel = new Label("Progress");
                progressInnerLabel.style.fontSize = 14;
                progressInnerLabel.style.color = new Color(1, 1, 1, 1f);
                progressInnerLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
                statusBadge.Add(progressInnerLabel);
            }
            topRow.Add(statusBadge);

            container.Add(topRow);

            // Progress bar
            var progressBarContainer = new VisualElement();
            progressBarContainer.style.height = 18;
            progressBarContainer.style.backgroundColor = new Color(0.85f, 0.85f, 0.85f, 1f);
            progressBarContainer.style.borderTopLeftRadius = 9;
            progressBarContainer.style.borderTopRightRadius = 9;
            progressBarContainer.style.borderBottomLeftRadius = 9;
            progressBarContainer.style.borderBottomRightRadius = 9;
            progressBarContainer.style.overflow = Overflow.Hidden;

            var progressFill = new VisualElement();
            float progressPercent = subAchievement.MaxCount > 0
                ? (float)subAchievement.Count / subAchievement.MaxCount * 100f
                : 0f;
            progressFill.style.width = Length.Percent(Mathf.Clamp(progressPercent, 0f, 100f));
            progressFill.style.height = Length.Percent(100);
            progressFill.style.backgroundColor = new Color(0.5f, 0.7f, 1f, 1f);

            progressBarContainer.Add(progressFill);
            container.Add(progressBarContainer);

            // Progress text label
            var progressLabel = new Label($"{subAchievement.Count} / {subAchievement.MaxCount} ({progressPercent:F0}%)");
            progressLabel.style.fontSize = 14;
            progressLabel.style.color = new Color(0.4f, 0.4f, 0.4f, 1f);
            progressLabel.style.marginTop = 4;
            progressLabel.style.unityTextAlign = TextAnchor.MiddleCenter;
            container.Add(progressLabel);

            return container;
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
            if (_selectedSubAchievementElement != null)
            {
                _selectedSubAchievementElement.style.backgroundColor = new Color(0.95f, 0.95f, 0.95f, 1f);
                _selectedSubAchievementElement.style.borderLeftColor = new Color(0.7f, 0.7f, 0.7f, 1f);
                _selectedSubAchievementElement.style.borderRightColor = new Color(0.7f, 0.7f, 0.7f, 1f);
                _selectedSubAchievementElement.style.borderTopColor = new Color(0.7f, 0.7f, 0.7f, 1f);
                _selectedSubAchievementElement.style.borderBottomColor = new Color(0.7f, 0.7f, 0.7f, 1f);
            }

            // Select new sub-achievement
            _controller.SelectSubAchievement(subAchievement, parent, key);
            _selectedSubAchievementElement = element;
            _selectedSubAchievementElement.style.backgroundColor = new Color(0.85f, 0.9f, 1f, 1f);
            _selectedSubAchievementElement.style.borderLeftColor = new Color(0.5f, 0.6f, 1f, 1f);
            _selectedSubAchievementElement.style.borderRightColor = new Color(0.5f, 0.6f, 1f, 1f);
            _selectedSubAchievementElement.style.borderTopColor = new Color(0.5f, 0.6f, 1f, 1f);
            _selectedSubAchievementElement.style.borderBottomColor = new Color(0.5f, 0.6f, 1f, 1f);

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