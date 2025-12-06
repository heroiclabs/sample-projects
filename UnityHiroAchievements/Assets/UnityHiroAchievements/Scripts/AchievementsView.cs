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
        private UIDocument _uiDocument;

        public AchievementsView(AchievementsController controller, HiroAchievementsCoordinator coordinator,
            VisualTreeAsset achievementItemTemplate, Sprite defaultIcon)
        {
            _controller = controller;
            _coordinator = coordinator;
            _achievementItemTemplate = achievementItemTemplate;
            _defaultIcon = defaultIcon;

            InitializeUI();
        }

        #region UI Initialization

        private void InitializeUI()
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
            _detailsRewardsContainer = rootElement.Q<VisualElement>("details-rewards");

            _progressButton = rootElement.Q<Button>("progress-button");
            _progressButton.RegisterCallback<ClickEvent>(_ =>
            {
                if (_controller.GetSelectedAchievement() == null) return;
                _progressQuantityField.value = 1;
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
            UpdateActionButtons();
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

            foreach (var achievement in achievements)
            {
                var achievementElement = _achievementItemTemplate.CloneTree();
                var container = achievementElement.Q<VisualElement>("achievement-item-container");

                // Set achievement icon
                var iconContainer = container.Q<VisualElement>("achievement-icon");
                SetAchievementIcon(iconContainer, achievement.Id);

                // Set achievement name
                var nameLabel = container.Q<Label>("achievement-name");
                nameLabel.text = achievement.Name;

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

                // Set progress bar
                var progressBar = container.Q<VisualElement>("achievement-progress-bar");
                var progressFill = progressBar.Q<VisualElement>("achievement-progress-fill");
                float progressPercent = achievement.MaxCount > 0 
                    ? (float)achievement.Count / achievement.MaxCount * 100f 
                    : 0f;
                progressFill.style.width = Length.Percent(Mathf.Clamp(progressPercent, 0f, 100f));

                // Click handler
                container.RegisterCallback<ClickEvent>(evt =>
                {
                    SelectAchievement(achievement, container);
                    evt.StopPropagation();
                });

                _achievementsList.Add(achievementElement);
            }
        }

        private void SelectAchievement(IAchievement achievement, VisualElement element)
        {
            // Deselect previous
            if (_selectedAchievementElement != null)
            {
                _selectedAchievementElement.RemoveFromClassList("selected-achievement");
            }

            // Select new
            _controller.SelectAchievement(achievement);
            _selectedAchievementElement = element;
            _selectedAchievementElement.AddToClassList("selected-achievement");

            ShowAchievementDetails(achievement);
            UpdateActionButtons();
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

        private void ShowAchievementDetails(IAchievement achievement)
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
        }

        private void ShowEmptyState()
        {
            _detailsNameLabel.text = "No Achievement Selected";
            _detailsDescriptionLabel.text = "Select an achievement from the list to view details.";
            _detailsCategoryLabel.text = "";
            _detailsProgressLabel.text = "";
            _detailsProgressFill.style.width = Length.Percent(0);
            _detailsRewardsContainer.Clear();

            _achievementDetailsPanel.style.display = DisplayStyle.Flex;
        }

        #endregion

        #region Action Buttons

        private void UpdateActionButtons()
        {
            var selectedAchievement = _controller.GetSelectedAchievement();

            if (selectedAchievement == null)
            {
                _progressButton.SetEnabled(false);
                _claimButton.SetEnabled(false);
                return;
            }

            // Progress button: enabled if not completed
            bool isCompleted = _controller.IsAchievementCompleted(selectedAchievement);
            _progressButton.SetEnabled(!isCompleted);

            // Claim button: enabled if can claim reward
            bool canClaim = _controller.CanClaimReward(selectedAchievement);
            _claimButton.SetEnabled(canClaim);
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
                        ShowAchievementDetails(updatedAchievement);
                    }
                }

                UpdateActionButtons();
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
                        ShowAchievementDetails(updatedAchievement);
                    }
                }

                UpdateActionButtons();
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