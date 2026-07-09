using System;
using Hiro;
using Hiro.Unity;
using Nakama;
using UnityEngine;
using UnityEngine.UIElements;

namespace XPGuide
{
    [RequireComponent(typeof(UIDocument))]
    public class XPGuideViewBehaviour : MonoBehaviour
    {
        private XPGuideCoordinator _coordinator;
        private XPGuideView _view;

        private void Start()
        {
            _coordinator = HiroCoordinator.Instance as XPGuideCoordinator;
            if (_coordinator == null)
            {
                Debug.LogError("XPGuideCoordinator not found");
                return;
            }

            _coordinator.ReceivedStartSuccess += OnCoordinatorReady;
            _coordinator.ReceivedStartError += OnCoordinatorError;
        }

        private void OnCoordinatorError(Exception e)
        {
            Debug.LogException(e);
        }

        private void OnCoordinatorReady(ISession session)
        {
            if (_coordinator == null) return;

            _coordinator.ReceivedStartSuccess -= OnCoordinatorReady;
            _coordinator.ReceivedStartError -= OnCoordinatorError;

            var nakamaSystem = _coordinator.GetSystem<NakamaSystem>();
            var achievementsSystem = _coordinator.GetSystem<AchievementsSystem>();
            var economySystem = _coordinator.GetSystem<EconomySystem>();

            var controller = new XPController(
                nakamaSystem,
                achievementsSystem,
                economySystem,
                _coordinator.NakamaClient,
                session);

            var questItemTemplate = Resources.Load<VisualTreeAsset>("QuestItemTemplate");
            var uiDocument = GetComponent<UIDocument>();
            _view = new XPGuideView(controller, uiDocument.rootVisualElement, questItemTemplate);
        }

        private void Update()
        {
            _view?.Tick();
        }

        private void OnDestroy()
        {
            if (_coordinator != null)
            {
                _coordinator.ReceivedStartSuccess -= OnCoordinatorReady;
                _coordinator.ReceivedStartError -= OnCoordinatorError;
            }

            _view?.Dispose();
        }
    }
}
