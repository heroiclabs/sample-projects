using System;
using System.Text;
using Nakama;
using Hiro;
using Hiro.Unity;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

namespace HiroChallenges.Editor
{
    public class AccountSwitcherEditor : EditorWindow
    {
        [SerializeField] private VisualTreeAsset tree;

        private DropdownField accountDropdown;
        private Label usernamesLabel;
        private string _env;

        [MenuItem("Tools/Nakama/Account Switcher")]
        public static void ShowWindow()
        {
            var inspectorType = typeof(UnityEditor.Editor).Assembly.GetType("UnityEditor.InspectorWindow");
            var window = GetWindow<AccountSwitcherEditor>("Account Switcher", inspectorType);

            window.Focus();
        }

        [MenuItem("Tools/Nakama/Clear Saved Accounts")]
        public static void ClearSavedAccounts()
        {
            AccountSwitcher.ClearAccounts();
            Debug.Log("Cleared all saved accounts");

            // Refresh any open Account Switcher windows
            var windows = Resources.FindObjectsOfTypeAll<AccountSwitcherEditor>();
            foreach (var window in windows)
            {
                window.UpdateUsernameLabels();
            }
        }

        private void CreateGUI()
        {
            tree.CloneTree(rootVisualElement);

            accountDropdown = rootVisualElement.Q<DropdownField>("account-dropdown");
            accountDropdown.RegisterValueChangedCallback(SwitchAccount);

            usernamesLabel = rootVisualElement.Q<Label>("usernames");

            if (!EditorApplication.isPlaying)
            {
                usernamesLabel.text = "Enter Play Mode to see accounts";
                return;
            }

            var coordinator = HiroCoordinator.Instance as HiroChallengesCoordinator;
            if (coordinator == null) return;

            _env = coordinator.IsLocalHost ? "local" : "heroiclabs";
            UpdateUsernameLabels();

            var nakamaSystem = coordinator.GetSystem<NakamaSystem>();
            if (nakamaSystem?.Session != null)
            {
                OnCoordinatorInitialized();
            }
            else
            {
                coordinator.ReceivedStartSuccess += OnCoordinatorInitialized;
            }
        }

        private async void OnCoordinatorInitialized()
        {
            var coordinator = HiroCoordinator.Instance as HiroChallengesCoordinator;
            if (coordinator == null)
            {
                Debug.LogError("HiroChallengesCoordinator not found");
                return;
            }

            coordinator.ReceivedStartSuccess -= OnCoordinatorInitialized;

            var nakamaSystem = coordinator.GetSystem<NakamaSystem>();
            var rootGameObjects = SceneManager.GetActiveScene().GetRootGameObjects();
            foreach (var rootGameObject in rootGameObjects)
            {
                if (!rootGameObject.TryGetComponent<ChallengesViewBehaviour>(out var viewBehaviour)) continue;
                if (viewBehaviour.Controller == null) continue;

                await AccountSwitcher.EnsureAccountsExistAsync(nakamaSystem, viewBehaviour.Controller, _env);
                // Switch back to account 0 after ensuring all accounts exist
                await AccountSwitcher.SwitchAccountAsync(nakamaSystem, viewBehaviour.Controller, _env, 0);
                accountDropdown.index = 0;
                UpdateUsernameLabels();
                return;
            }
        }

        private async void SwitchAccount(ChangeEvent<string> changeEvt)
        {
            if (!EditorApplication.isPlaying) return;

            var rootGameObjects = SceneManager.GetActiveScene().GetRootGameObjects();
            foreach (var rootGameObject in rootGameObjects)
            {
                if (!rootGameObject.TryGetComponent<ChallengesViewBehaviour>(out var viewBehaviour)) continue;
                if (viewBehaviour.Controller == null) continue;

                var coordinator = HiroCoordinator.Instance as HiroChallengesCoordinator;
                if (coordinator == null) return;
                var nakamaSystem = coordinator.GetSystem<NakamaSystem>();
                if (nakamaSystem == null) return;

                try
                {
                    await AccountSwitcher.SwitchAccountAsync(
                        nakamaSystem,
                        viewBehaviour.Controller,
                        _env,
                        accountDropdown.index);

                    Debug.Log($"Switch to account index: {accountDropdown.index}, env: {_env}");
                    break;
                }
                catch (ApiResponseException e)
                {
                    Debug.LogWarning($"Error authenticating with Device ID: {e.Message}");
                    return;
                }
            }

            UpdateUsernameLabels();
        }

        private void UpdateUsernameLabels()
        {
            var accounts = AccountSwitcher.GetAllAccounts();
            var sb = new StringBuilder();

            // Filter and sort by index for current environment
            for (var i = 0; i < 4; i++)
            {
                var key = $"{_env}_{i}";
                if (accounts.TryGetValue(key, out var account))
                {
                    sb.Append(i + 1);
                    sb.Append(": ");
                    sb.Append(account.Username);
                    sb.AppendLine();
                }
            }

            usernamesLabel.text = sb.ToString();
        }
    }
}