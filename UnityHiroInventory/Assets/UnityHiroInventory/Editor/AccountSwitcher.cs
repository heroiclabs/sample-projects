using System.Text;
using Nakama;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace HiroInventory.Editor
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

            if (AccountSwitcher.NakamaSystem != null)
            {
                _env = AccountSwitcher.CurrentEnv;
                UpdateUsernameLabels();
                OnAccountSwitcherInitialized();
            }
            else
            {
                AccountSwitcher.Initialized += OnAccountSwitcherInitialized;
            }
        }

        private async void OnAccountSwitcherInitialized()
        {
            AccountSwitcher.Initialized -= OnAccountSwitcherInitialized;

            _env = AccountSwitcher.CurrentEnv;
            var nakamaSystem = AccountSwitcher.NakamaSystem;

            await AccountSwitcher.EnsureAccountsExistAsync(nakamaSystem, _env);
            // Switch back to account 0 after ensuring all accounts exist
            await AccountSwitcher.SwitchAccountAsync(nakamaSystem, _env, 0);
            accountDropdown.index = 0;
            UpdateUsernameLabels();
        }

        private async void SwitchAccount(ChangeEvent<string> changeEvt)
        {
            if (!EditorApplication.isPlaying) return;

            var nakamaSystem = AccountSwitcher.NakamaSystem;
            if (nakamaSystem == null) return;

            Debug.Log($"[Editor] SwitchAccount triggered: dropdown index={accountDropdown.index}, dropdown value={changeEvt.newValue}, env={_env}");

            try
            {
                Debug.Log($"[Editor] Calling SwitchAccountAsync with index={accountDropdown.index}");
                var session = await AccountSwitcher.SwitchAccountAsync(
                    nakamaSystem,
                    _env,
                    accountDropdown.index);

                Debug.Log($"[Editor] Switch complete: index={accountDropdown.index}, user={session.Username}, userId={session.UserId}");
            }
            catch (ApiResponseException e)
            {
                Debug.LogWarning($"Error authenticating with Device ID: {e.Message}");
                return;
            }

            UpdateUsernameLabels();
        }

        private void UpdateUsernameLabels()
        {
            if (string.IsNullOrEmpty(_env) || usernamesLabel == null)
                return;

            var accounts = AccountSwitcher.GetAllAccounts();
            var sb = new StringBuilder();

            Debug.Log($"[Editor] UpdateUsernameLabels: env={_env}, total accounts in cache={accounts.Count}");

            // Filter and sort by index for current environment
            for (var i = 0; i < 4; i++)
            {
                var key = $"{_env}_{i}";
                if (accounts.TryGetValue(key, out var account))
                {
                    Debug.Log($"[Editor] Display: {i + 1}: {account.Username} (userId={account.UserId})");
                    sb.Append(i + 1);
                    sb.Append(": ");
                    sb.Append(account.Username);
                    sb.AppendLine();
                }
                else
                {
                    Debug.Log($"[Editor] No account for key={key}");
                }
            }

            usernamesLabel.text = sb.ToString();
        }
    }
}
