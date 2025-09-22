using System;
using System.Collections.Generic;
using System.Text;
using Nakama;
using SampleProjects.Groups;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

namespace SampleProjects.NakamaGroups.Editor
{
    public class AccountSwitcherEditor : EditorWindow
    {
        [SerializeField] private VisualTreeAsset tree;

        private DropdownField accountDropdown;
        private Label usernamesLabel;

        private readonly SortedDictionary<string, string> accountUsernames = new();

        private const string ACCOUNT_USERNAMES_KEY = "AccountSwitcher_Usernames";

        [MenuItem("Tools/Nakama/Account Switcher")]
        public static void ShowWindow()
        {
            var inspectorType = typeof(UnityEditor.Editor).Assembly.GetType("UnityEditor.InspectorWindow");
            var window = GetWindow<AccountSwitcherEditor>("Account Switcher", inspectorType);

            window.Focus();
        }
        
        [MenuItem("Tools/Nakama/Clear Test Accounts")]
        public static void ClearSavedAccounts()
        {
            EditorPrefs.DeleteKey(ACCOUNT_USERNAMES_KEY);
            Debug.Log("Cleared all saved account usernames");
    
            // Refresh any open Account Switcher windows
            var windows = Resources.FindObjectsOfTypeAll<AccountSwitcherEditor>();
            foreach (var window in windows)
            {
                window.accountUsernames.Clear();
                window.UpdateUsernameLabels();
            }
        }

        private void CreateGUI()
        {
            tree.CloneTree(rootVisualElement);

            accountDropdown = rootVisualElement.Q<DropdownField>("account-dropdown");
            accountDropdown.RegisterValueChangedCallback(SwitchAccount);

            usernamesLabel = rootVisualElement.Q<Label>("usernames");
            
            // Load saved usernames on startup
            LoadAccountUsernames();
            UpdateUsernameLabels();

            if (!EditorApplication.isPlaying) return;

            var rootGameObjects = SceneManager.GetActiveScene().GetRootGameObjects();
            foreach (var rootGameObject in rootGameObjects)
            {
                if (!rootGameObject.TryGetComponent<NakamaGroupsController>(out var GroupsController)) continue;

                if (GroupsController.Session != null)
                {
                    OnControllerInitialized(GroupsController.Session);
                }
                else
                {
                    GroupsController.OnInitialized += OnControllerInitialized;
                }
            }
        }

        private void OnControllerInitialized(ISession session, NakamaGroupsController controller = null)
        {
            accountUsernames[accountDropdown.choices[0]] = session.Username;
            UpdateUsernameLabels();

            if (controller != null)
            {
                controller.OnInitialized -= OnControllerInitialized;
            }
        }

        private void LoadAccountUsernames()
        {
            var savedUsernames = EditorPrefs.GetString(ACCOUNT_USERNAMES_KEY, "");
            if (string.IsNullOrEmpty(savedUsernames)) return;

            try
            {
                var usernameData = JsonUtility.FromJson<SerializableStringDictionary>(savedUsernames);
                accountUsernames.Clear();
                
                foreach (var item in usernameData.items)
                {
                    accountUsernames[item.key] = item.value;
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning($"Failed to load saved account usernames: {e.Message}");
            }
        }

        private void SaveAccountUsernames()
        {
            try
            {
                var usernameData = new SerializableStringDictionary();
                foreach (var kvp in accountUsernames)
                {
                    usernameData.items.Add(new SerializableKeyValuePair { key = kvp.Key, value = kvp.Value });
                }

                var json = JsonUtility.ToJson(usernameData);
                EditorPrefs.SetString(ACCOUNT_USERNAMES_KEY, json);
            }
            catch (Exception e)
            {
                Debug.LogWarning($"Failed to save account usernames: {e.Message}");
            }
        }

        private async void SwitchAccount(ChangeEvent<string> changeEvt)
        {
            if (!EditorApplication.isPlaying) return;

            var previousValue = changeEvt.previousValue;
            var newValue = changeEvt.newValue;

            var deviceId = PlayerPrefs.GetString("deviceId", SystemInfo.deviceUniqueIdentifier);
            if (deviceId == SystemInfo.unsupportedIdentifier)
            {
                deviceId = Guid.NewGuid().ToString();
            }
            PlayerPrefs.SetString("deviceId", deviceId);

            var rootGameObjects = SceneManager.GetActiveScene().GetRootGameObjects();
            foreach (var rootGameObject in rootGameObjects)
            {
                if (!rootGameObject.TryGetComponent<NakamaGroupsController>(out var GroupsController)) continue;

                // Save username before switching
                if (!string.IsNullOrEmpty(previousValue) && GroupsController.Session != null)
                {
                    accountUsernames[previousValue] = GroupsController.Session.Username;
                }
                
                if (GroupsController.Session != null)
                {
                    await GroupsController.Client.SessionLogoutAsync(GroupsController.Session);
                }

                try
                {
                    var newSession = await GroupsController.Client.AuthenticateDeviceAsync($"{deviceId}_{accountDropdown.index}");
                    accountUsernames[newValue] = newSession.Username;
                    GroupsController.SwitchComplete(newSession);
                    
                    // Save usernames after successful authentication
                    SaveAccountUsernames();
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
            var sb = new StringBuilder();
            var index = 1;
    
            foreach (var kvp in accountUsernames)
            {
                sb.Append(index);
                sb.Append(": ");
                sb.Append(kvp.Value);
                sb.AppendLine();
                index++;
            }

            usernamesLabel.text = sb.ToString();
        }

        [Serializable]
        private class SerializableStringDictionary
        {
            public List<SerializableKeyValuePair> items = new();
        }

        [Serializable]
        private class SerializableKeyValuePair
        {
            public string key;
            public string value;
        }
    }
}