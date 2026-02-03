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
using Hiro;
using Hiro.Unity;
using Nakama;
using UnityEngine;
using UnityEngine.UIElements;

namespace HiroEventLeaderboards
{
    /// <summary>
    /// MonoBehaviour that manages the lifecycle of the EventLeaderboards Controller and View.
    /// Handles Unity lifecycle events and coordinates system initialization.
    /// </summary>
    [RequireComponent(typeof(UIDocument))]
    public class EventLeaderboardsViewBehaviour : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private VisualTreeAsset eventLeaderboardEntryTemplate;
        [SerializeField] private VisualTreeAsset eventLeaderboardRecordTemplate;
        [SerializeField] private VisualTreeAsset eventLeaderboardZoneTemplate;

        private EventLeaderboardsController _controller;
        private EventLeaderboardsView _view;
        private HiroEventLeaderboardsCoordinator _coordinator;
        private NakamaSystem _nakamaSystem;

        /// <summary>
        /// Fired when the controller is initialized and ready to use.
        /// </summary>
        public event Action<ISession, EventLeaderboardsController> OnInitialized;

        /// <summary>
        /// The controller instance. Available after initialization.
        /// </summary>
        public EventLeaderboardsController Controller => _controller;

        private void Start()
        {
            _coordinator = HiroCoordinator.Instance as HiroEventLeaderboardsCoordinator;
            if (_coordinator == null)
            {
                Debug.LogError("HiroEventLeaderboardsCoordinator not found");
                return;
            }

            _coordinator.ReceivedStartError += HandleStartError;
            _coordinator.ReceivedStartSuccess += HandleStartSuccess;
        }

        private void OnDestroy()
        {
            if (_coordinator != null)
            {
                _coordinator.ReceivedStartError -= HandleStartError;
                _coordinator.ReceivedStartSuccess -= HandleStartSuccess;
            }

            _view?.Dispose();
        }

        private static void HandleStartError(Exception e)
        {
            Debug.LogException(e);
        }

        private void HandleStartSuccess()
        {
            _nakamaSystem = _coordinator.GetSystem<NakamaSystem>();
            var eventLeaderboardsSystem = _coordinator.GetSystem<EventLeaderboardsSystem>();
            var economySystem = _coordinator.GetSystem<EconomySystem>();

            _controller = new EventLeaderboardsController(_nakamaSystem, eventLeaderboardsSystem, economySystem);

            var env = _coordinator.IsLocalHost ? "local" : "heroiclabs";
            AccountSwitcher.Initialize(_nakamaSystem, env);

            var rootElement = GetComponent<UIDocument>().rootVisualElement;
            _view = new EventLeaderboardsView(
                _controller,
                rootElement,
                eventLeaderboardEntryTemplate,
                eventLeaderboardRecordTemplate,
                eventLeaderboardZoneTemplate);

            _view.Initialize();

            OnInitialized?.Invoke(_nakamaSystem.Session, _controller);
        }

    }
}
