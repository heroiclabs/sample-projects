using Hiro;
using UnityEngine.UIElements;

namespace HiroChallenges
{
    public class ChallengeParticipantView
    {
        private Label usernameLabel;
        private Label scoreLabel;
        private Label subScoreLabel;
        private Label rankLabel;

        public void SetVisualElement(VisualElement visualElement)
        {
            usernameLabel = visualElement.Q<Label>("username");
            scoreLabel = visualElement.Q<Label>("score");
            subScoreLabel = visualElement.Q<Label>("sub-score");
            rankLabel = visualElement.Q<Label>("rank");
        }

        public void SetChallengeParticipant(IChallenge challenge, IChallengeScore participantScore)
        {
            // Display username along with remaining score submissions.
            usernameLabel.text =
                $"<color=blue>({participantScore.NumScores}/{challenge.MaxNumScore})</color> {participantScore.Username} ";
            scoreLabel.text = participantScore.Score.ToString();
            subScoreLabel.text = participantScore.Subscore.ToString();

            // A rank of 0 would mean that you are yet to submit your first score.
            rankLabel.text = participantScore.Rank > 0 ? $"#{participantScore.Rank}" : "-";
        }
    }
}