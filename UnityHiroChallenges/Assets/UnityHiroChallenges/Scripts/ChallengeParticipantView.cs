using Hiro;
using Nakama;
using UnityEngine.UIElements;

namespace SampleProjects.Challenges
{
    public class ChallengeParticipantView
    {
        private Label usernameLabel;
        private Label scoreLabel;
        private Label rankLabel;

        private string userId;
        private HiroChallengesController challengesController;

        public void SetVisualElement(VisualElement visualElement, HiroChallengesController controller)
        {
            challengesController = controller;

            usernameLabel = visualElement.Q<Label>("username");
            scoreLabel = visualElement.Q<Label>("score");
            rankLabel = visualElement.Q<Label>("rank");
        }

        public void SetChallengeParticipant(IChallengeScore participantScore)
        {
            userId = participantScore.Id;
            usernameLabel.text = participantScore.Username ?? "Unknown User";
            scoreLabel.text = participantScore.Score.ToString();
            rankLabel.text = $"#{participantScore.Rank}";
        }
    }
}