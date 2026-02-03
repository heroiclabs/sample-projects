using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Hiro;
using Hiro.Unity;
using HeroicUI;
using Nakama;
using UnityEngine;
using UnityEngine.UIElements;

namespace HiroAchievements
{
    [Serializable]
    public class AchievementIconMapping
    {
        public string achievementId;
        public Sprite icon;
    }

    /// <summary>
    /// MonoBehaviour that manages the Achievements UI and lifecycle.
    /// Creates and owns the Controller, handles Unity lifecycle events.
    /// </summary>
    [RequireComponent(typeof(UIDocument))]
    public sealed class AchievementsView : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private VisualTreeAsset achievementItemTemplate;
        [SerializeField] private VisualTreeAsset subAchievementItemTemplate;
        [SerializeField] private AchievementIconMapping[] achievementIconMappings;
        [SerializeField] private Sprite defaultIcon;

        private AchievementsController _controller;
        private HiroAchievementsCoordinator _coordinator;
        private CancellationTokenSource _cts;

        private Dictionary<string, Sprite> _iconDictionary;
        private WalletDisplay _walletDisplay;

        private Button _dailiesTabButton;
        private Button _questsTabButton;
        private Button _achievementsTabButton;
        private Button _refreshButton;
        private Label _resetTimeLabel;
        private CountdownTimer _timer;

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
        private readonly Dictionary<VisualElement, SubAchievementItemView> _subAchievementViews = new();

        public AchievementsController Controller => _controller;
        public event Action<ISession, AchievementsController> OnInitialized;

        private void Start()
        {
            _cts = new CancellationTokenSource();

            BuildIconDictionary();

            _coordinator = HiroCoordinator.Instance as HiroAchievementsCoordinator;
            if (_coordinator == null)
            {
                Debug.LogError("HiroAchievementsCoordinator not found");
                return;
            }

            _coordinator.ReceivedStartError += HandleStartError;
            _coordinator.ReceivedStartSuccess += HandleStartSuccess;
        }

        private void OnDestroy()
        {
            if (_coordinator != null)
            {
                _coordinator.ReceivedStartError -= HandleStartError;
                _coordinator.ReceivedStartSuccess -= HandleStartSuccess;
            }

            AccountSwitcher.AccountSwitched -= OnAccountSwitched;

            _cts?.Cancel();
            _cts?.Dispose();

            _walletDisplay?.Dispose();
        }

        private void BuildIconDictionary()
        {
            _iconDictionary = new Dictionary<string, Sprite>();
            if (achievementIconMappings != null)
            {
                foreach (var mapping in achievementIconMappings)
                {
                    if (!string.IsNullOrEmpty(mapping.achievementId) && mapping.icon != null)
                    {
                        _iconDictionary[mapping.achievementId] = mapping.icon;
                    }
                }
            }
        }

        private void HandleStartError(Exception e)
        {
            Debug.LogException(e);
            ShowError(e.Message);
        }

        private async void HandleStartSuccess(ISession session)
        {
            var nakamaSystem = _coordinator.GetSystem<NakamaSystem>();
            var achievementsSystem = _coordinator.GetSystem<AchievementsSystem>();
            var economySystem = _coordinator.GetSystem<EconomySystem>();

            _controller = new AchievementsController(nakamaSystem, achievementsSystem, economySystem);

            var env = _coordinator.IsLocalHost ? "local" : "heroiclabs";
            AccountSwitcher.Initialize(nakamaSystem, env);
            AccountSwitcher.AccountSwitched += OnAccountSwitched;

            InitializeUI();

            _walletDisplay.StartObserving();
            _controller.SetCurrentCategory("quest");

            try
            {
                await _controller.LoadAchievementsAsync();
                await RefreshAchievementsListAsync();
                OnInitialized?.Invoke(session, _controller);
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

        private void InitializeUI()
        {
            var rootElement = GetComponent<UIDocument>().rootVisualElement;

            _walletDisplay = new WalletDisplay(rootElement.Q<VisualElement>("wallet-display"));

            // Tab buttons
            _dailiesTabButton = rootElement.Q<Button>("tab-dailies");
            _questsTabButton = rootElement.Q<Button>("tab-quests");
            _achievementsTabButton = rootElement.Q<Button>("tab-achievements");

            _dailiesTabButton.RegisterCallback<ClickEvent>(_ => SwitchTab("daily"));
            _questsTabButton.RegisterCallback<ClickEvent>(_ => SwitchTab("quest"));
            _achievementsTabButton.RegisterCallback<ClickEvent>(_ => SwitchTab("achievement"));
            _resetTimeLabel = rootElement.Q<Label>("reset-time");
            _timer = new CountdownTimer(this, _resetTimeLabel);

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
            _progressButton.RegisterCallback<ClickEvent>(_ => OnProgressButtonClicked());

            _claimButton = rootElement.Q<Button>("claim-button");
            _claimButton.RegisterCallback<ClickEvent>(_ => OnClaimButtonClicked());

            ShowEmptyState();

            _refreshButton = rootElement.Q<Button>("achievements-refresh");
            _refreshButton.RegisterCallback<ClickEvent>(_ => OnRefreshClicked());

            // Progress modal
            _progressModal = rootElement.Q<VisualElement>("progress-modal");
            _progressQuantityField = rootElement.Q<IntegerField>("progress-modal-quantity");
            _progressModalButton = rootElement.Q<Button>("progress-modal-update");
            _progressModalButton.RegisterCallback<ClickEvent>(_ => OnProgressModalSubmitClicked());
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

        #endregion

        #region Tab Management

        private async void SwitchTab(string category)
        {
            try
            {
                _cts.Token.ThrowIfCancellationRequested();
                _controller.SetCurrentCategory(category);
                UpdateTabButtons();
                await RefreshAchievementsListAsync();
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

        private void UpdateTabButtons()
        {
            var currentCategory = _controller.GetCurrentCategory();

            _dailiesTabButton.RemoveFromClassList("selected");
            _questsTabButton.RemoveFromClassList("selected");
            _achievementsTabButton.RemoveFromClassList("selected");

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

        #region Event Handlers

        private async void OnRefreshClicked()
        {
            try
            {
                _cts.Token.ThrowIfCancellationRequested();
                await RefreshAchievementsListAsync();
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

        private async void OnAccountSwitched()
        {
            try
            {
                _cts.Token.ThrowIfCancellationRequested();
                HideAllModals();
                ShowEmptyState();
                await _controller.SwitchCompleteAsync();
                await RefreshAchievementsListAsync();
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

        private void OnProgressButtonClicked()
        {
            if (_controller.GetSelectedAchievement() == null) return;
            _progressQuantityField.value = 1;

            _progressModal.style.display = DisplayStyle.Flex;
        }

        private async void OnClaimButtonClicked()
        {
            try
            {
                _cts.Token.ThrowIfCancellationRequested();
                await _controller.ClaimSelectedAchievementRewardAsync(claimTotal: true);
                await RefreshAchievementsListAsync();

                var selectedAchievement = _controller.GetSelectedAchievement();
                if (selectedAchievement != null)
                {
                    var updatedAchievement = FindUpdatedAchievement(selectedAchievement.Id);
                    if (updatedAchievement != null)
                    {
                        ShowAchievementDetails(updatedAchievement);
                    }
                }

                UpdateActionButtons();
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

        private async void OnProgressModalSubmitClicked()
        {
            try
            {
                _cts.Token.ThrowIfCancellationRequested();
                await _controller.UpdateSelectedAchievementProgressAsync(_progressQuantityField.value);
                _progressModal.style.display = DisplayStyle.None;
                await RefreshAchievementsListAsync();

                var selectedAchievement = _controller.GetSelectedAchievement();
                if (selectedAchievement != null)
                {
                    var updatedAchievement = FindUpdatedAchievement(selectedAchievement.Id);
                    if (updatedAchievement != null)
                    {
                        ShowAchievementDetails(updatedAchievement);
                    }
                }

                UpdateActionButtons();
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

        private IAchievement FindUpdatedAchievement(string achievementId)
        {
            foreach (var a in _controller.AllAchievements)
            {
                if (a.Id == achievementId)
                {
                    return a;
                }
            }

            foreach (var a in _controller.RepeatAchievements)
            {
                if (a.Id == achievementId)
                {
                    return a;
                }
            }

            return null;
        }

        #endregion

        #region Achievement List Display

        /// <summary>
        /// Public refresh method for external callers (e.g., CountdownTimer).
        /// </summary>
        public async void RefreshAchievementsList()
        {
            try
            {
                _cts.Token.ThrowIfCancellationRequested();
                await RefreshAchievementsListAsync();
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

        private async Task RefreshAchievementsListAsync()
        {
            UpdateTabButtons();
            var achievements = await _controller.RefreshAchievementsAsync();
            PopulateAchievementsList(achievements);
            _timer.start(_controller.GetResetTime());
        }

        private void PopulateAchievementsList(List<IAchievement> achievements)
        {
            _achievementsList.Clear();

            foreach (var achievement in achievements)
            {
                var achievementElement = achievementItemTemplate.Instantiate();
                var container = achievementElement.Q<VisualElement>("achievement-item-container");

                var iconContainer = container.Q<VisualElement>("achievement-icon");
                SetAchievementIcon(iconContainer, achievement.Id);

                var nameLabel = container.Q<Label>("achievement-name");
                nameLabel.text = achievement.Name;

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
                    var incompletePrereqs = _controller.GetIncompletePrerequisiteNames(achievement);
                    int incompleteCount = incompletePrereqs.Count;

                    if (incompleteCount > 0)
                    {
                        statusLabel.text = $"{AchievementsUIConstants.StatusLocked} ({incompleteCount})";
                    }
                    else
                    {
                        statusLabel.text = AchievementsUIConstants.StatusLocked;
                    }
                    statusBadge.style.backgroundColor = AchievementsUIConstants.StatusLockedColor;

                }
                else if (_controller.IsAchievementClaimable(achievement))
                {
                    statusLabel.text = AchievementsUIConstants.StatusToClaim;
                    statusBadge.style.backgroundColor = AchievementsUIConstants.StatusToClaimColor;
                }
                else
                {
                    statusLabel.text = AchievementsUIConstants.StatusInProgress;
                    statusBadge.style.backgroundColor = AchievementsUIConstants.StatusInProgressColor;
                }

                var progressBar = container.Q<VisualElement>("achievement-progress-bar");
                var progressFill = container.Q<VisualElement>("achievement-progress-fill");
                float progressPercent = AchievementProgressHelper.CalculateProgressPercent(achievement);
                progressFill.style.width = Length.Percent(Mathf.Clamp(progressPercent, 0f, 100f));

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
            if (_selectedAchievementElement != null)
            {
                _selectedAchievementElement.RemoveFromClassList("achievement-item--selected");
            }

            _controller.SelectAchievement(achievement);
            _selectedAchievementElement = element;
            _selectedAchievementElement.AddToClassList("achievement-item--selected");

            ShowAchievementDetails(achievement);
            UpdateActionButtons();
        }

        private void SetAchievementIcon(VisualElement iconContainer, string achievementId)
        {
            if (_iconDictionary.TryGetValue(achievementId, out var icon))
            {
                iconContainer.style.backgroundImage = new StyleBackground(icon);
            }
            else if (defaultIcon != null)
            {
                iconContainer.style.backgroundImage = new StyleBackground(defaultIcon);
            }
            else
            {
                iconContainer.style.backgroundImage = StyleKeyword.Null;
                Debug.LogWarning($"No icon mapping found for achievement ID: {achievementId}");
            }
        }

        private static readonly Dictionary<string, string> ItemRarities = new()
        {
            { "coins", "common" },
            { "gems", "rare" },
            { "iron_sword", "common" },
            { "mana_potion", "uncommon" },
            { "golden_key", "rare" },
            { "lucky_charm", "epic" }
        };

        private static string GetRarity(string itemId)
        {
            return ItemRarities.TryGetValue(itemId.ToLower(), out var rarity) ? rarity : "common";
        }

        private VisualElement CreateRewardTile(string rewardType, long amount)
        {
            var tile = new VisualElement();
            tile.AddToClassList("reward-tile");

            var rarity = GetRarity(rewardType);
            tile.AddToClassList($"reward-tile--{rarity}");

            var iconContainer = new VisualElement();
            iconContainer.AddToClassList("reward-tile__icon-container");

            var iconImage = new VisualElement();
            iconImage.AddToClassList("reward-tile__icon");

            if (_iconDictionary.TryGetValue(rewardType, out var rewardIcon))
            {
                iconImage.style.backgroundImage = new StyleBackground(rewardIcon);
            }
            else if (defaultIcon != null)
            {
                iconImage.style.backgroundImage = new StyleBackground(defaultIcon);
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

        private void ShowAchievementDetails(IAchievement achievement)
        {
            bool isLocked = _controller.IsAchievementLocked(achievement);

            _detailsNameLabel.text = achievement.Name;

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
                _detailsDescriptionLabel.style.color = new StyleColor(StyleKeyword.Null);
            }

            _detailsCategoryLabel.text = achievement.Category ?? "Uncategorized";

            var (currentProgress, maxProgress) = AchievementProgressHelper.GetProgressValues(achievement);
            float progressPercent = AchievementProgressHelper.CalculateProgressPercent(achievement);

            _detailsProgressLabel.text = string.Format(AchievementsUIConstants.ProgressFormat,
                currentProgress, maxProgress, progressPercent);
            _detailsProgressFill.style.width = Length.Percent(Mathf.Clamp(progressPercent, 0f, 100f));

            if (_detailsSubAchievementsContainer != null)
            {
                _detailsSubAchievementsContainer.Clear();
                _subAchievementViews.Clear();

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

            _detailsRewardsContainer.Clear();
            if (achievement.HasAvailableReward() && achievement.AvailableRewards != null)
            {
                if (achievement.AvailableRewards.Guaranteed?.Currencies != null)
                {
                    foreach (var currencyPair in achievement.AvailableRewards.Guaranteed.Currencies)
                    {
                        var rewardTile = CreateRewardTile(currencyPair.Key, currencyPair.Value.Count.Min);
                        _detailsRewardsContainer.Add(rewardTile);
                    }
                }

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
        }

        private VisualElement CreateSubAchievementElement(ISubAchievement subAchievement, IAchievement parent, string key)
        {
            var subAchievementElement = subAchievementItemTemplate.Instantiate();

            var subAchievementView = new SubAchievementItemView();
            subAchievementView.SetVisualElement(subAchievementElement);
            subAchievementView.SetSubAchievement(subAchievement);

            var container = subAchievementView.GetContainer();

            _subAchievementViews[container] = subAchievementView;

            container.RegisterCallback<MouseEnterEvent>(_ =>
            {
                if (_selectedSubAchievementElement != container)
                {
                    subAchievementView.SetHovered(true);
                }
            });
            container.RegisterCallback<MouseLeaveEvent>(_ =>
            {
                if (_selectedSubAchievementElement != container)
                {
                    subAchievementView.SetHovered(false);
                    subAchievementView.SetSelected(false);
                }
            });

            container.RegisterCallback<ClickEvent>(evt =>
            {
                SelectSubAchievement(subAchievement, parent, key, container);
                evt.StopPropagation();
            });

            return subAchievementElement;
        }

        private void SelectSubAchievement(ISubAchievement subAchievement, IAchievement parent, string key, VisualElement element)
        {
            if (subAchievement == null)
            {
                Debug.LogError("SelectSubAchievement called with NULL sub-achievement!");
                return;
            }

            if (_selectedSubAchievementView != null && _selectedSubAchievementElement != null)
            {
                _selectedSubAchievementView.SetSelected(false);
            }

            _controller.SelectSubAchievement(subAchievement, parent, key);
            _selectedSubAchievementElement = element;

            if (_subAchievementViews.TryGetValue(element, out var view))
            {
                _selectedSubAchievementView = view;
                _selectedSubAchievementView.SetSelected(true);
            }

            UpdateActionButtons();
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

        /// <summary>
        /// Updates the action buttons based on current selection state.
        /// Does NOT refresh data from the server - just updates UI based on current controller state.
        /// </summary>
        private void UpdateActionButtons()
        {
            var selectedAchievement = _controller.GetSelectedAchievement();
            var selectedSubAchievement = _controller.GetSelectedSubAchievement();

            if (selectedAchievement == null)
            {
                _progressButton.SetEnabled(false);
                _claimButton.SetEnabled(false);
                return;
            }

            bool isLocked = _controller.IsAchievementLocked(selectedAchievement);
            if (isLocked)
            {
                _progressButton.SetEnabled(false);
                _claimButton.SetEnabled(false);
                return;
            }

            if (selectedSubAchievement != null)
            {
                bool isCompleted = _controller.IsAchievementCompleted(selectedSubAchievement);
                _progressButton.SetEnabled(!isCompleted);

                bool canClaim = _controller.CanClaimReward(selectedSubAchievement);
                _claimButton.SetEnabled(canClaim);
            }
            else
            {
                bool isCompleted = _controller.IsAchievementCompleted(selectedAchievement);
                _progressButton.SetEnabled(!isCompleted);

                bool canClaim = _controller.CanClaimReward(selectedAchievement);
                _claimButton.SetEnabled(canClaim);
            }
        }

        #endregion

        #region Modal Management

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
