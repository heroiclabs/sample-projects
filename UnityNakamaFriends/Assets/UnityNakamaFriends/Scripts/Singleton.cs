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

using UnityEngine;

namespace UnityNakamaFriends.Scripts
{
    // ReSharper disable StaticMemberInGenericType

    /// <summary>
    /// A generic and thread-safe base class for <c>MonoBehaviour</c> which applies the singleton pattern.
    /// </summary>
    /// <typeparam name="T">The type of the singleton.</typeparam>
    public abstract class Singleton<T> : MonoBehaviour where T : MonoBehaviour
    {
        private static T _instance;
        private static readonly object Lock = new();
        private static bool _isQuitting;

        /// <summary>
        /// The instance of type <c>T</c> for this singleton.
        /// </summary>
        public static T Instance
        {
            get
            {
                if (_isQuitting)
                {
                    // Prevent object left in the Editor scene when the application is stopped.
                    return null;
                }

                lock (Lock)
                {
                    if (_instance == null)
                    {
                        _instance = FindFirstObjectByType<T>();
                    }

                    if (_instance == null)
                    {
                        var singletonObject = new GameObject();
                        _instance = singletonObject.AddComponent<T>();
                        singletonObject.name = typeof(T).Name;
                    }
                }
                return _instance;
            }
        }

        protected virtual void OnApplicationQuit() => _isQuitting = true;
    }

    /// <summary>
    /// A persistent version of the <c>Singleton</c> type which is retained across scenes.
    /// </summary>
    /// <typeparam name="T">The type of the singleton.</typeparam>
    public abstract class PersistentSingleton<T> : Singleton<T> where T : MonoBehaviour
    {
        protected virtual void Awake()
        {
            if (Instance != null && Instance != this as T)
            {
                Destroy(gameObject);
                return;
            }

            DontDestroyOnLoad(gameObject);
        }
    }
}
