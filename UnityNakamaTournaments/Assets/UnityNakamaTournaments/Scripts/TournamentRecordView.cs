using Nakama;
using UnityEngine.UIElements;

namespace UnityNakamaTournaments
{
    public class TournamentRecordView
    {
        private Label rankLabel;
        private Label usernameLabel;
        private Label scoreLabel;
        private Label subScoreLabel;
    
        private NakamaTournamentsController tournamentsController;
    
        public void SetVisualElement(VisualElement visualElement, NakamaTournamentsController controller)
        {
            tournamentsController = controller;
    
            rankLabel = visualElement.Q<Label>("rank");
            usernameLabel = visualElement.Q<Label>("username");
            scoreLabel = visualElement.Q<Label>("score");
            subScoreLabel = visualElement.Q<Label>("sub-score");
        }
    
        public void SetTournamentRecord(IApiLeaderboardRecord record)
        {
            if (int.TryParse(record.Rank, out var rank))
            {
                switch (rank)
                {
                    // Only ranks 1, 2, and 3 have medals ...
                    case 1:
                    case 2:
                    case 3:
                        rankLabel.text = string.Empty;
                        rankLabel.style.backgroundImage = new StyleBackground(tournamentsController.RankMedals[rank - 1]);
                        break;
                    // ... otherwise use a regular label.
                    default:
                        rankLabel.style.backgroundImage = null;
                        rankLabel.text = record.Rank;
                        break;
                }
            }
            else
            {
                // Invalid rank returned.
                rankLabel.style.backgroundImage = null;
                rankLabel.text = "???";
            }
    
            // Set up remaining data.
            usernameLabel.text = record.Username;
            scoreLabel.text = record.Score ?? "0";
            subScoreLabel.text = record.Subscore ?? "0";
        }
    }
}
