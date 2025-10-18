using Hiro;
using UnityEngine.UIElements;

namespace HiroChallenges
{
    public class ChallengeParticipantView
    {
        private Label usernameLabel;
        private Label scoreLabel;
        private Label rankLabel;

        public void SetVisualElement(VisualElement visualElement)
        {
            usernameLabel = visualElement.Q<Label>("username");
            scoreLabel = visualElement.Q<Label>("score");
            rankLabel = visualElement.Q<Label>("rank");
        }

        public void SetChallengeParticipant(IChallengeScore participantScore)
        {
            usernameLabel.text = participantScore.Username;
            scoreLabel.text = participantScore.Score.ToString();
            rankLabel.text = participantScore.Rank > 0 ? $"#{participantScore.Rank}" : "-";
        }
    }
}