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

using Hiro;
using Hiro.Unity;
using HeroicUtils;
using UnityEngine;
using UnityEngine.UIElements;

namespace HiroChallenges
{
    /// <summary>
    /// MonoBehaviour that manages the Challenges MVP components.
    /// Creates Controller and View, handles lifecycle per Unity MVP pattern.
    /// </summary>
    [RequireComponent(typeof(UIDocument))]
    public class ChallengesViewBehaviour : MonoBehaviour
    {
        private HiroChallengesCoordinator _coordinator;
        private ChallengesView _view;

        public ChallengesController Controller { get; private set; }

        private void Start()
        {
            _coordinator = HiroCoordinator.Instance as HiroChallengesCoordinator;
            if (_coordinator == null)
            {
                Debug.LogError("HiroChallengesCoordinator not found");
                return;
            }

            _coordinator.ReceivedStartSuccess += OnCoordinatorReady;
        }

        private void OnCoordinatorReady()
        {
            if (_coordinator == null)
                return;

            _coordinator.ReceivedStartSuccess -= OnCoordinatorReady;

            var nakamaSystem = _coordinator.GetSystem<NakamaSystem>();
            var challengesSystem = _coordinator.GetSystem<ChallengesSystem>();
            var economySystem = _coordinator.GetSystem<EconomySystem>();

            Controller = new ChallengesController(nakamaSystem, challengesSystem, economySystem);

            var env = _coordinator.IsLocalHost ? "local" : "heroiclabs";
            AccountSwitcher.Initialize(nakamaSystem, env);

            var challengeEntryTemplate = Resources.Load<VisualTreeAsset>("Challenge");
            var challengeParticipantTemplate = Resources.Load<VisualTreeAsset>("ChallengeParticipant");

            var uiDocument = GetComponent<UIDocument>();
            _view = new ChallengesView(
                Controller,
                uiDocument.rootVisualElement,
                challengeEntryTemplate,
                challengeParticipantTemplate);
        }

        private void OnDestroy()
        {
            if (_coordinator != null)
                _coordinator.ReceivedStartSuccess -= OnCoordinatorReady;

            _view?.Dispose();
        }
    }
}
