using System;
using System.Collections.Generic;
using System.Text;
using Nakama;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

namespace SampleProjects.NakamaTournaments.Editor
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
                if (!rootGameObject.TryGetComponent<NakamaTournamentsController>(out var tournamentsController)) continue;

                if (tournamentsController.Session != null)
                {
                    OnControllerInitialized(tournamentsController.Session);
                }
                else
                {
                    tournamentsController.OnInitialized += OnControllerInitialized;
                }
            }
        }

        private void OnControllerInitialized(ISession session, NakamaTournamentsController controller = null)
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
            catch (Exception ex)
            {
                Debug.LogWarning($"Failed to load saved account usernames: {ex.Message}");
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
            catch (Exception ex)
            {
                Debug.LogWarning($"Failed to save account usernames: {ex.Message}");
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
                if (!rootGameObject.TryGetComponent<NakamaTournamentsController>(out var tournamentsController)) continue;

                // Save username before switching
                if (!string.IsNullOrEmpty(previousValue) && tournamentsController.Session != null)
                {
                    accountUsernames[previousValue] = tournamentsController.Session.Username;
                }
                
                if (tournamentsController.Session != null)
                {
                    await tournamentsController.Client.SessionLogoutAsync(tournamentsController.Session);
                }

                try
                {
                    var newSession = await tournamentsController.Client.AuthenticateDeviceAsync($"{deviceId}_{accountDropdown.index}");
                    accountUsernames[newValue] = newSession.Username;
                    tournamentsController.SwitchComplete(newSession);
                    
                    // Save usernames after successful authentication
                    SaveAccountUsernames();
                    break;
                }
                catch (ApiResponseException ex)
                {
                    Debug.LogWarning($"Error authenticating with Device ID: {ex.Message}");
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