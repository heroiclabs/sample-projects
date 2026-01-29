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
        [Header("UI Templates")]
        [SerializeField] private VisualTreeAsset _challengeEntryTemplate;
        [SerializeField] private VisualTreeAsset _challengeParticipantTemplate;

        private ChallengesView _view;

        public ChallengesController Controller { get; private set; }

        private void Start()
        {
            var coordinator = HiroCoordinator.Instance as HiroChallengesCoordinator;
            if (coordinator == null)
            {
                Debug.LogError("HiroChallengesCoordinator not found");
                return;
            }

            coordinator.ReceivedStartSuccess += OnCoordinatorReady;
        }

        private void OnCoordinatorReady()
        {
            var coordinator = HiroCoordinator.Instance as HiroChallengesCoordinator;
            if (coordinator == null)
                return;

            coordinator.ReceivedStartSuccess -= OnCoordinatorReady;

            var nakamaSystem = coordinator.GetSystem<NakamaSystem>();
            var challengesSystem = coordinator.GetSystem<ChallengesSystem>();
            var economySystem = coordinator.GetSystem<EconomySystem>();

            Controller = new ChallengesController(nakamaSystem, challengesSystem, economySystem);

            var uiDocument = GetComponent<UIDocument>();
            _view = new ChallengesView(
                Controller,
                uiDocument.rootVisualElement,
                _challengeEntryTemplate,
                _challengeParticipantTemplate);
        }

        private void OnDestroy()
        {
            var coordinator = HiroCoordinator.Instance as HiroChallengesCoordinator;
            if (coordinator != null)
                coordinator.ReceivedStartSuccess -= OnCoordinatorReady;

            _view?.Dispose();
        }
    }
}
