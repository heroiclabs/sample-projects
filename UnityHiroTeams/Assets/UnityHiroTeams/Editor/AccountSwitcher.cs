// Copyright 2025 The Nakama Authors
//
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//
// http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.

using System;
using System.Collections.Generic;
using System.Text;
using Nakama;
using Hiro;
using Hiro.Unity;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

namespace HiroTeams.Editor
{
    public class AccountSwitcherEditor : EditorWindow
    {
        [SerializeField] private VisualTreeAsset tree;

        private DropdownField accountDropdown;
        private Label usernamesLabel;

        private readonly SortedDictionary<string, string> accountUsernames = new();

        private const string AccountUsernamesKey = "AccountSwitcher_Usernames";

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
            EditorPrefs.DeleteKey(AccountUsernamesKey);
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
                if (!rootGameObject.TryGetComponent<HiroTeamsController>(out var teamsController)) continue;

                if (HiroCoordinator.Instance.GetSystem<NakamaSystem>().Session is Session session)
                {
                    OnControllerInitialized(session);
                }
                else
                {
                    teamsController.OnInitialized += OnControllerInitialized;
                }
            }
        }

        private void OnControllerInitialized(ISession session, HiroTeamsController teamsController = null)
        {
            accountUsernames[accountDropdown.choices[0]] = session.Username;
            UpdateUsernameLabels();

            if (teamsController != null)
            {
                teamsController.OnInitialized -= OnControllerInitialized;
            }
        }

        private void LoadAccountUsernames()
        {
            var savedUsernames = EditorPrefs.GetString(AccountUsernamesKey, "");
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
                EditorPrefs.SetString(AccountUsernamesKey, json);
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

            var rootGameObjects = SceneManager.GetActiveScene().GetRootGameObjects();
            foreach (var rootGameObject in rootGameObjects)
            {
                if (!rootGameObject.TryGetComponent<HiroTeamsController>(out var teamsController)) continue;

                var coordinator = HiroCoordinator.Instance as HiroTeamsCoordinator;
                if (coordinator == null) return;
                var nakamaSystem = coordinator.GetSystem<NakamaSystem>();

                // Save username before switching
                if (!string.IsNullOrEmpty(previousValue))
                {
                    accountUsernames[previousValue] = nakamaSystem.Session.Username;
                }

                try
                {
                    var newSession = await HiroTeamsCoordinator.NakamaAuthorizerFunc(accountDropdown.index)
                        .Invoke(nakamaSystem.Client);
                    (nakamaSystem.Session as Session)?.Update(newSession.AuthToken, newSession.RefreshToken);
                    await nakamaSystem.RefreshAsync();
                    accountUsernames[newValue] = newSession.Username;
                    teamsController.SwitchComplete(newSession);

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
