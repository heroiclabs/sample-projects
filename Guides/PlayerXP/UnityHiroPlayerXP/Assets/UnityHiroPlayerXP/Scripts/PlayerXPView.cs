using System;
using System.Threading;
using System.Threading.Tasks;
using Hiro;
using UnityEngine;
using UnityEngine.UIElements;

namespace PlayerXP
{
    public sealed class PlayerXPView : IDisposable
    {
        private readonly XPController _controller;
        private readonly VisualTreeAsset _questItemTemplate;

        private readonly CancellationTokenSource _cts = new();
        private readonly object _disposeLock = new();
        private volatile bool _disposed;

        // Status bar
        private Label _totalXpLabel;
        private Label _levelLabel;
        private Label _xpRequiredLabel;
        private VisualElement _levelProgressFill;
        private Label _coinsLabel;
        private Label _gemsLabel;

        // Tabs
        private Button _tabDirect;
        private Button _tabReward;
        private VisualElement _contentDirect;
        private VisualElement _contentReward;

        // Direct XP tab
        private Button _gainXpButton;
        private TextField _xpAmountField;

        // Quest tab
        private VisualElement _questList;
        private VisualElement _questDetailPlaceholder;
        private VisualElement _questDetailCard;
        private Label _questDetailName;
        private Label _questDetailDescription;
        private VisualElement _questDetailRewards;
        private Button _questCompleteButton;
        private Button _questClaimButton;
        private VisualElement _questBonusNote;
        private Label _questBonusNoteText;
        private VisualElement _selectedQuestItem;
        private string _selectedQuestId;
        private ISubAchievement _selectedQuest;

        // Reset button
        private Button _resetButton;

        // XP Booster
        private VisualElement _xpBoosterBadge;
        private Label _xpBoosterLabel;
        private long _boosterEndTimeSec;

        // Error popup
        private VisualElement _errorPopup;
        private Label _errorMessage;
        private Button _errorCloseButton;

        public PlayerXPView(XPController controller, VisualElement rootElement, VisualTreeAsset questItemTemplate)
        {
            _controller = controller;
            _questItemTemplate = questItemTemplate;
            Initialize(rootElement);
            _ = InitializeAsync();
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
        }

        private void ThrowIfDisposedOrCancelled()
        {
            if (_disposed) throw new OperationCanceledException();
            _cts.Token.ThrowIfCancellationRequested();
        }

        private void Initialize(VisualElement rootElement)
        {
            // Status bar
            _totalXpLabel = rootElement.Q<Label>("total-xp-label");
            _levelLabel = rootElement.Q<Label>("level-label");
            _xpRequiredLabel = rootElement.Q<Label>("xp-required-label");
            _levelProgressFill = rootElement.Q<VisualElement>("level-progress-fill");
            _xpBoosterBadge = rootElement.Q<VisualElement>("xp-booster-badge");
            _xpBoosterLabel = rootElement.Q<Label>("xp-booster-label");
            _coinsLabel = rootElement.Q<Label>("coins-label");
            _gemsLabel = rootElement.Q<Label>("gems-label");

            // Reset button
            _resetButton = rootElement.Q<Button>("reset-button");
            _resetButton.RegisterCallback<ClickEvent>(_ => OnResetClicked());

            // Tabs
            _tabDirect = rootElement.Q<Button>("tab-direct");
            _tabReward = rootElement.Q<Button>("tab-reward");
            _contentDirect = rootElement.Q<VisualElement>("content-direct");
            _contentReward = rootElement.Q<VisualElement>("content-reward");

            _tabDirect.RegisterCallback<ClickEvent>(_ => SelectTab(0));
            _tabReward.RegisterCallback<ClickEvent>(_ => SelectTab(1));

            // Direct XP tab
            _gainXpButton = rootElement.Q<Button>("gain-xp-button");
            _xpAmountField = rootElement.Q<TextField>("direct-xp-amount");
            _gainXpButton.RegisterCallback<ClickEvent>(_ => OnGainXPClicked());

            // Quest tab
            _questList = rootElement.Q<VisualElement>("quest-list");
            _questDetailPlaceholder = rootElement.Q<VisualElement>("quest-detail-placeholder");
            _questDetailCard = rootElement.Q<VisualElement>("quest-detail-card");
            _questDetailName = rootElement.Q<Label>("quest-detail-name");
            _questDetailDescription = rootElement.Q<Label>("quest-detail-description");
            _questDetailRewards = rootElement.Q<VisualElement>("quest-detail-rewards");
            _questCompleteButton = rootElement.Q<Button>("quest-complete-button");
            _questClaimButton = rootElement.Q<Button>("quest-claim-button");
            _questBonusNote = rootElement.Q<VisualElement>("quest-bonus-note");
            _questBonusNoteText = rootElement.Q<Label>("quest-bonus-note-text");
            _questCompleteButton.RegisterCallback<ClickEvent>(_ => OnQuestCompleteClicked());
            _questClaimButton.RegisterCallback<ClickEvent>(_ => OnQuestClaimClicked());

            // Error popup
            _errorPopup = rootElement.Q<VisualElement>("error-popup");
            _errorMessage = rootElement.Q<Label>("error-message");
            _errorCloseButton = rootElement.Q<Button>("error-close");
            _errorCloseButton.RegisterCallback<ClickEvent>(_ => _errorPopup.style.display = DisplayStyle.None);

            SelectTab(1);
        }

        private async Task InitializeAsync()
        {
            try
            {
                ThrowIfDisposedOrCancelled();
                await _controller.LoadAsync();
                UpdateStatusBar();
                await RefreshQuestListAsync();
            }
            catch (OperationCanceledException) { }
            catch (Exception e)
            {
                ShowError(e.Message);
                Debug.LogException(e);
            }
        }

        private async Task RefreshQuestListAsync()
        {
            await _controller.RefreshAsync();
            UpdateStatusBar();
            PopulateQuestList();
        }

        // ── Status bar ────────────────────────────────────────────────────────────

        private void UpdateStatusBar()
        {
            long xp = _controller.GetCurrentXP();
            int level = _controller.GetCurrentLevel();
            var currentLevelSub = _controller.GetCurrentLevelSub();

            _totalXpLabel.text = $"Total XP:  {xp} pts";
            _coinsLabel.text = _controller.GetCoins().ToString();
            _gemsLabel.text = _controller.GetGems().ToString();
            _levelLabel.text = $"Level {level}:";

            if (currentLevelSub != null)
            {
                float pct = XPProgressHelper.CalculateSubAchievementPercent(currentLevelSub);
                _levelProgressFill.style.width = Length.Percent(Mathf.Clamp(pct, 0f, 100f));
                _xpRequiredLabel.text = $"{currentLevelSub.MaxCount - currentLevelSub.Count} XP to next level";
            }
            else
            {
                _levelProgressFill.style.width = Length.Percent(100f);
                _xpRequiredLabel.text = "Max level reached";
            }

            UpdateBoosterDisplay();
        }

        // ── Reset ─────────────────────────────────────────────────────────────────

        private async void OnResetClicked()
        {
            try
            {
                ThrowIfDisposedOrCancelled();
                _resetButton.SetEnabled(false);
                await _controller.ResetAsync();
                UpdateStatusBar();
                await RefreshQuestListAsync();
            }
            catch (OperationCanceledException) { }
            catch (Exception e)
            {
                ShowError(e.Message);
                Debug.LogException(e);
            }
            finally
            {
                _resetButton.SetEnabled(true);
            }
        }

        private void UpdateBoosterDisplay()
        {
            _boosterEndTimeSec = _controller.GetActiveXPBoosterEndTimeSec();
            long remaining = _boosterEndTimeSec > 0
                ? _boosterEndTimeSec - DateTimeOffset.UtcNow.ToUnixTimeSeconds()
                : 0;

            if (remaining > 0)
            {
                _xpBoosterBadge.style.backgroundColor = new Color(1f, 0.745f, 0.157f);
                _xpBoosterLabel.style.color = Color.white;
                _xpBoosterLabel.text = FormatCountdown(remaining);
            }
            else
            {
                _boosterEndTimeSec = 0;
                SetBoosterOff();
            }
        }

        // Called every frame from PlayerXPViewBehaviour.Update().
        // Updates the countdown label locally without any server calls.
        public void Tick()
        {
            if (_boosterEndTimeSec <= 0) return;

            long remaining = _boosterEndTimeSec - DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            if (remaining <= 0)
            {
                _boosterEndTimeSec = 0;
                SetBoosterOff();
                return;
            }

            _xpBoosterLabel.text = FormatCountdown(remaining);
        }

        private void SetBoosterOff()
        {
            _xpBoosterBadge.style.backgroundColor = new Color(1f, 1f, 1f, 0.2f);
            _xpBoosterLabel.style.color = new Color(1f, 1f, 1f, 0.6f);
            _xpBoosterLabel.text = "OFF";
        }

        // ── Tab switching ─────────────────────────────────────────────────────────

        private void SelectTab(int tabIndex)
        {
            _tabDirect.RemoveFromClassList("selected");
            _tabReward.RemoveFromClassList("selected");

            _contentDirect.style.display = DisplayStyle.None;
            _contentReward.style.display = DisplayStyle.None;

            switch (tabIndex)
            {
                case 0:
                    _tabDirect.AddToClassList("selected");
                    _contentDirect.style.display = DisplayStyle.Flex;
                    break;
                case 1:
                    _tabReward.AddToClassList("selected");
                    _contentReward.style.display = DisplayStyle.Flex;
                    break;
            }
        }

        // ── Direct XP tab ─────────────────────────────────────────────────────────

        private async void OnGainXPClicked()
        {
            try
            {
                ThrowIfDisposedOrCancelled();

                if (!long.TryParse(_xpAmountField.value, out long amount) || amount <= 0)
                {
                    ShowError("Please enter a valid positive XP amount.");
                    return;
                }

                await _controller.GrantXPAsync(amount);
                UpdateStatusBar();
            }
            catch (OperationCanceledException) { }
            catch (Exception e)
            {
                ShowError(e.Message);
                Debug.LogException(e);
            }
        }

        // ── Quest tab ─────────────────────────────────────────────────────────────

        private void PopulateQuestList()
        {
            var previousSelectedId = _selectedQuestId;

            _questList.Clear();
            _selectedQuestItem = null;
            _selectedQuestId = null;
            _selectedQuest = null;

            var quests = _controller.GetOrderedQuests();
            foreach (var (key, sub) in quests)
            {
                var element = _questItemTemplate.Instantiate();
                var container = element.Q<VisualElement>("quest-item-container");

                container.Q<Label>("quest-name").text = sub.Name;

                var fill = container.Q<VisualElement>("quest-progress-fill");
                fill.style.width = sub.Count >= sub.MaxCount ? Length.Percent(100f) : Length.Percent(0f);

                var badge = container.Q<VisualElement>("quest-status-badge");
                var badgeLabel = container.Q<Label>("quest-status-text");
                bool claimed = sub.ClaimTimeSec > 0;
                bool completed = sub.Count >= sub.MaxCount;

                if (claimed)
                {
                    badgeLabel.text = "Claimed";
                    badge.style.backgroundColor = XPUIConstants.StatusAchievedColor;
                }
                else if (completed)
                {
                    badgeLabel.text = "Completed";
                    badge.style.backgroundColor = XPUIConstants.StatusInProgressColor;
                }
                else
                {
                    badgeLabel.text = "Not Started";
                    badge.style.backgroundColor = XPUIConstants.StatusLockedColor;
                }

                var capturedKey = key;
                var capturedSub = sub;
                container.RegisterCallback<ClickEvent>(_ => SelectQuest(capturedKey, capturedSub, container));

                _questList.Add(element);
            }

            if (quests.Count > 0)
            {
                var restoreIndex = quests.FindIndex(q => q.key == previousSelectedId);
                var selectIndex = restoreIndex >= 0 ? restoreIndex : 0;
                var (selectKey, selectSub) = quests[selectIndex];
                var selectContainer = _questList[selectIndex].Q<VisualElement>("quest-item-container");
                SelectQuest(selectKey, selectSub, selectContainer);
            }
        }

        private void SelectQuest(string key, ISubAchievement sub, VisualElement itemContainer)
        {
            if (_selectedQuestItem != null)
            {
                _selectedQuestItem.RemoveFromClassList("achievement-item--selected");
                _selectedQuestItem.style.backgroundColor = Color.white;
            }

            _selectedQuestId = key;
            _selectedQuest = sub;
            _selectedQuestItem = itemContainer;
            _selectedQuestItem.AddToClassList("achievement-item--selected");
            _selectedQuestItem.style.backgroundColor = new Color(0.91f, 0.90f, 1f);

            ShowQuestDetail(sub);
        }

        private void ShowQuestDetail(ISubAchievement sub)
        {
            _questDetailPlaceholder.style.display = DisplayStyle.None;
            _questDetailCard.style.display = DisplayStyle.Flex;

            _questDetailName.text = sub.Name;
            _questDetailDescription.text = sub.Description;

            bool hasModifier = sub.AvailableRewards?.Guaranteed?.RewardModifiers?.Count > 0;
            _questBonusNote.style.display = hasModifier ? DisplayStyle.Flex : DisplayStyle.None;
            if (hasModifier)
            {
                IAvailableRewardsRewardModifier firstMod = null;
                foreach (var m in sub.AvailableRewards.Guaranteed.RewardModifiers) { firstMod = m; break; }
                long durationSec = (long)(firstMod.DurationSec?.Min ?? 0);
                _questBonusNoteText.text = $"Completion triggers XP booster: double gains for {FormatDuration(durationSec)}.";
            }

            _questDetailRewards.Clear();
            if (sub.AvailableRewards?.Guaranteed?.Currencies != null)
            {
                foreach (var curr in sub.AvailableRewards.Guaranteed.Currencies)
                    _questDetailRewards.Add(CreateCurrencyTile(curr.Key, curr.Value.Count.Min));
            }

            if (sub.AvailableRewards?.Guaranteed?.RewardModifiers != null)
            {
                foreach (var mod in sub.AvailableRewards.Guaranteed.RewardModifiers)
                    _questDetailRewards.Add(CreateModifierTile(mod));
            }

            if (_questDetailRewards.childCount == 0 && sub.ClaimTimeSec > 0)
            {
                var label = new Label("Reward claimed");
                label.style.fontSize = 18;
                label.style.color = new Color(0.5f, 0.5f, 0.5f);
                _questDetailRewards.Add(label);
            }

            bool claimed = sub.ClaimTimeSec > 0;
            bool completed = sub.Count >= sub.MaxCount;

            _questCompleteButton.SetEnabled(!completed && !claimed);
            _questClaimButton.SetEnabled(completed && !claimed);
        }

        private async void OnQuestCompleteClicked()
        {
            if (_selectedQuestId == null) return;
            try
            {
                ThrowIfDisposedOrCancelled();
                await _controller.CompleteSubQuestAsync(_selectedQuestId);
                await RefreshQuestListAsync();
            }
            catch (OperationCanceledException) { }
            catch (Exception e) { ShowError(e.Message); Debug.LogException(e); }
        }

        private async void OnQuestClaimClicked()
        {
            if (_selectedQuestId == null) return;
            try
            {
                ThrowIfDisposedOrCancelled();
                await _controller.ClaimSubQuestAsync(_selectedQuestId);
                await RefreshQuestListAsync();
            }
            catch (OperationCanceledException) { }
            catch (Exception e) { ShowError(e.Message); Debug.LogException(e); }
        }

        private VisualElement CreateCurrencyTile(string currencyId, long amount)
        {
            var tile = new VisualElement();
            tile.AddToClassList("reward-tile");
            tile.AddToClassList($"reward-tile--{GetRarity(currencyId)}");

            var iconContainer = new VisualElement();
            iconContainer.AddToClassList("reward-tile__icon-container");

            if (currencyId == "xp")
            {
                var xpLabel = new Label("XP");
                xpLabel.AddToClassList("reward-tile__icon--xp-label");
                iconContainer.Add(xpLabel);
            }
            else
            {
                var icon = new VisualElement();
                icon.AddToClassList("reward-tile__icon");
                icon.AddToClassList($"reward-tile__icon--{currencyId}");
                iconContainer.Add(icon);
            }

            tile.Add(iconContainer);

            var amountLabel = new Label(amount.ToString());
            amountLabel.AddToClassList("reward-tile__amount");
            tile.Add(amountLabel);

            return tile;
        }

        private VisualElement CreateModifierTile(IAvailableRewardsRewardModifier modifier)
        {
            var tile = new VisualElement();
            tile.AddToClassList("reward-tile");
            tile.AddToClassList("reward-tile--rare");

            if (modifier.Id == "xp")
            {
                long durationSec = (long)(modifier.DurationSec?.Min ?? 0);
                long h = durationSec / 3600;
                long m = (durationSec % 3600) / 60;
                string durationText = h >= 1 ? $"{h}h{m}min" : $"{m}min";

                var iconContainer = new VisualElement();
                iconContainer.AddToClassList("reward-tile__icon-container");
                var durationLabel = new Label(durationText);
                durationLabel.AddToClassList("reward-tile__icon--xp-label");
                durationLabel.style.fontSize = 24;
                iconContainer.Add(durationLabel);
                tile.Add(iconContainer);

                var nameLabel = new Label("XP Booster");
                nameLabel.AddToClassList("reward-tile__amount");
                tile.Add(nameLabel);
            }
            else
            {
                long hours = (long)(modifier.DurationSec?.Min ?? 0) / 3600;
                string desc = modifier.Operator == "multiplier"
                    ? $"Double {modifier.Id.ToUpper()} gains\nfor {hours}h"
                    : $"{modifier.Id} modifier\n({modifier.Operator})";

                var label = new Label(desc);
                label.style.whiteSpace = WhiteSpace.Normal;
                label.style.fontSize = 16;
                label.style.unityTextAlign = TextAnchor.MiddleCenter;
                label.style.paddingLeft = 6;
                label.style.paddingRight = 6;
                label.style.color = Color.white;
                tile.Add(label);
            }

            return tile;
        }

        private static string GetRarity(string currencyId) => currencyId switch
        {
            "gems" => "rare",
            _ => "common"
        };

        private static string FormatCountdown(long seconds)
        {
            if (seconds <= 0) return "0s";
            long h = seconds / 3600;
            long m = (seconds % 3600) / 60;
            long s = seconds % 60;
            if (h >= 1) return $"{h}h {m}m";
            if (m >= 1) return $"{m}m {s}s";
            return $"{s}s";
        }

        private static string FormatDuration(long seconds)
        {
            long hours = seconds / 3600;
            long minutes = (seconds % 3600) / 60;

            if (hours >= 1 && minutes == 0)
                return $"{hours} {(hours == 1 ? "hour" : "hours")}";
            if (hours >= 1)
                return $"{hours} {(hours == 1 ? "hour" : "hours")} {minutes} {(minutes == 1 ? "minute" : "minutes")}";
            return $"{minutes} {(minutes == 1 ? "minute" : "minutes")}";
        }

        // ── Error popup ───────────────────────────────────────────────────────────

        public void ShowError(string message)
        {
            _errorPopup.style.display = DisplayStyle.Flex;
            _errorMessage.text = message;
        }
    }
}
