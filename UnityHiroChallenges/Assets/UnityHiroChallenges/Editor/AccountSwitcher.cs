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

            UpdateUsernameLabels();

            if (!EditorApplication.isPlaying) return;

            var coordinator = HiroCoordinator.Instance as HiroChallengesCoordinator;
            if (coordinator == null) return;

            _env = coordinator.IsLocalHost ? "local" : "heroiclabs";

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
                throw new InvalidOperationException("HiroChallengesCoordinator not found");

            coordinator.ReceivedStartSuccess -= OnCoordinatorInitialized;

            var nakamaSystem = coordinator.GetSystem<NakamaSystem>();
            var rootGameObjects = SceneManager.GetActiveScene().GetRootGameObjects();
            foreach (var rootGameObject in rootGameObjects)
            {
                if (!rootGameObject.TryGetComponent<ChallengesController>(out var controller)) continue;

                await AccountSwitcher.EnsureAccountsExistAsync(nakamaSystem, controller, _env);
                // Switch back to account 0 after ensuring all accounts exist
                await AccountSwitcher.SwitchAccountAsync(nakamaSystem, controller, _env, 0);
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
                if (!rootGameObject.TryGetComponent<ChallengesController>(out var controller)) continue;

                var coordinator = HiroCoordinator.Instance as HiroChallengesCoordinator;
                if (coordinator == null) return;
                var nakamaSystem = coordinator.GetSystem<NakamaSystem>();
                if (nakamaSystem == null) return;

                try
                {
                    var newSession = await AccountSwitcher.SwitchAccountAsync(
                        nakamaSystem,
                        controller,
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
            var index = 1;

            foreach (var kvp in accounts)
            {
                sb.Append(index);
                sb.Append(": ");
                sb.Append(kvp.Value.Username);
                sb.AppendLine();
                index++;
            }

            usernamesLabel.text = sb.ToString();
        }
    }
}