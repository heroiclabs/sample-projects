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
using Nakama;
using UnityEngine;

namespace NakamaTournaments
{
    /// <summary>
    /// Sets up and manages the Nakama Client.
    /// </summary>
    public class NakamaSingleton : MonoBehaviour
    {
        [Header("Nakama Settings")]
        [SerializeField]
        private string scheme = "http";

        [SerializeField]
        private string host = "127.0.0.1";
        [SerializeField]
        private int port = 7350;
        [SerializeField]
        private string serverKey = "defaultkey";

        private static NakamaSingleton instance;
        private static readonly object instanceLock = new();
        private static bool isQuitting;

        public Client Client { get; private set; }
        public ISession Session { get; private set; }

        public event Action<Exception> ReceivedStartError;
        public event Action<ISession> ReceivedStartSuccess;

        public static NakamaSingleton Instance
        {
            get
            {
                if (isQuitting)
                {
                    // Prevent object left in the Editor scene when the application is stopped.
                    return null;
                }

                lock (instanceLock)
                {
                    if (instance == null)
                    {
                        instance = FindFirstObjectByType<NakamaSingleton>();
                    }

                    if (instance != null) return instance;

                    var singletonObject = new GameObject();
                    instance = singletonObject.AddComponent<NakamaSingleton>();
                    singletonObject.name = nameof(NakamaSingleton);
                }
                return instance;
            }
        }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            DontDestroyOnLoad(gameObject);

            Client = new Client(scheme, host, port, serverKey, UnityWebRequestAdapter.Instance);
            Client.ReceivedSessionUpdated += session =>
            {
                PlayerPrefs.SetString("authToken", session.AuthToken);
                PlayerPrefs.SetString("refreshToken", session.RefreshToken);
            };
        }

        private async void Start()
        {
            // If we've seen a previous play session, restore the ID first.
            var deviceId= PlayerPrefs.GetString("deviceId", SystemInfo.deviceUniqueIdentifier);
            if (deviceId == SystemInfo.unsupportedIdentifier)
            {
                // Use a generated ID if "deviceUniqueIdentifier" is unsupported.
                deviceId = Guid.NewGuid().ToString();
            }
            PlayerPrefs.SetString("deviceId", deviceId);

            try
            {
                Session = await Client.AuthenticateDeviceAsync($"{deviceId}_0");
                Debug.Log($"Authenticated {Session.Username} with Device ID");
                ReceivedStartSuccess?.Invoke(Session);
            }
            catch (Exception e)
            {
                ReceivedStartError?.Invoke(e);
            }
        }

        protected void OnApplicationQuit() => isQuitting = true;
    }
}