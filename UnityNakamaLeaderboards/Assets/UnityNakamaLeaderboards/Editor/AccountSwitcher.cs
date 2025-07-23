using System;
using System.Collections.Generic;
using System.Text;
using Nakama;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

namespace SampleProjects.Leaderboards.Editor
{
        public class AccountSwitcherEditor : EditorWindow
    {
        [SerializeField] private VisualTreeAsset tree;

        private DropdownField accountDropdown;
        private Label usernamesLabel;

        private readonly SortedDictionary<string, string> accountUsernames = new();

        private const string FIRST_OPEN_KEY = "AccountSwitcher_FirstOpen";

        [MenuItem("Tools/Nakama/Account Switcher")]
        public static void ShowWindow()
        {
            var inspectorType = typeof(UnityEditor.Editor).Assembly.GetType("UnityEditor.InspectorWindow");
            var window = GetWindow<AccountSwitcherEditor>("Account Switcher", inspectorType);

            window.Focus();
        }

        [InitializeOnLoadMethod]
        private static void OnProjectLoadedInEditor()
        {
            if (!EditorPrefs.GetBool(FIRST_OPEN_KEY, true)) return;

            EditorPrefs.SetBool(FIRST_OPEN_KEY, false);
            EditorApplication.delayCall += ShowWindow;
        }

        private void CreateGUI()
        {
            tree.CloneTree(rootVisualElement);

            accountDropdown = rootVisualElement.Q<DropdownField>("account-dropdown");
            accountDropdown.RegisterValueChangedCallback(SwitchAccount);

            usernamesLabel = rootVisualElement.Q<Label>("usernames");
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
                if (!rootGameObject.TryGetComponent<NakamaLeaderboardsController>(out var leaderboardsController)) continue;

                accountUsernames[previousValue] = leaderboardsController.Session.Username;
                await leaderboardsController.Client.SessionLogoutAsync(leaderboardsController.Session);

                try
                {
                    var newSession = await leaderboardsController.Client.AuthenticateDeviceAsync($"{deviceId}_{accountDropdown.index}");
                    accountUsernames[newValue] = newSession.Username;
                    Debug.Log($"Authenticated {newSession.Username} with Device ID");
                    leaderboardsController.SwitchComplete(newSession);
                    break;
                }
                catch (ApiResponseException ex)
                {
                    Debug.LogFormat("Error authenticating with Device ID: {0}", ex.Message);
                    return;
                }
            }

            UpdateUsernameLabels();
        }

        private void UpdateUsernameLabels()
        {
            var sb = new StringBuilder();
            foreach (var kvp in accountUsernames)
            {
                sb.Append(kvp.Key);
                sb.Append(": ");
                sb.Append(kvp.Value);
                sb.AppendLine();
            }

            usernamesLabel.text = sb.ToString();
        }
    }
}
