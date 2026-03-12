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
using HeroicUtils;
using Nakama;
using UnityEngine;
using UnityEngine.UIElements;

namespace HiroTeams
{
    /// <summary>
    /// MonoBehaviour that manages the lifecycle of the Teams Controller and View.
    /// Handles Unity lifecycle events and coordinates system initialization.
    /// </summary>
    [RequireComponent(typeof(UIDocument))]
    public class TeamsViewBehaviour : MonoBehaviour
    {
        [Header("Team Settings")]
        [SerializeField] private int teamEntriesLimit = 100;

        [Header("Avatar Assets")]
        [SerializeField] private Texture2D[] avatarIcons;
        [SerializeField] private Texture2D[] avatarBackgrounds;

        private TeamsController _controller;
        private TeamsView _view;
        private HiroTeamsCoordinator _coordinator;
        private NakamaSystem _nakamaSystem;

        /// <summary>
        /// Fired when the controller is initialized and ready to use.
        /// </summary>
        public event Action<ISession, TeamsController> OnInitialized;

        /// <summary>
        /// The controller instance. Available after initialization.
        /// </summary>
        public TeamsController Controller => _controller;

        private void Start()
        {
            _coordinator = HiroCoordinator.Instance as HiroTeamsCoordinator;
            if (_coordinator == null)
            {
                Debug.LogError("HiroTeamsCoordinator not found");
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

        private void HandleStartSuccess(ISession session)
        {
            _nakamaSystem = _coordinator.GetSystem<NakamaSystem>();
            var teamsSystem = _coordinator.GetSystem<TeamsSystem>();

            _controller = new TeamsController(
                _nakamaSystem,
                teamsSystem,
                teamEntriesLimit,
                avatarIcons,
                avatarBackgrounds);

            var env = _coordinator.IsLocalHost ? "local" : "heroiclabs";
            AccountSwitcher.Initialize(_nakamaSystem, env);

            var teamEntryTemplate = Resources.Load<VisualTreeAsset>("Team");
            var teamMemberTemplate = Resources.Load<VisualTreeAsset>("TeamMember");
            var mailboxEntryTemplate = Resources.Load<VisualTreeAsset>("MailboxEntry");

            var rootElement = GetComponent<UIDocument>().rootVisualElement;
            _view = new TeamsView(
                _controller,
                _nakamaSystem,
                rootElement,
                teamEntryTemplate,
                teamMemberTemplate,
                mailboxEntryTemplate);

            _view.Initialize();

            OnInitialized?.Invoke(_nakamaSystem.Session, _controller);
        }
    }
}
