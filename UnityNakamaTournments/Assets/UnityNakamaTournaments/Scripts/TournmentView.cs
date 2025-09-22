using System;
using Nakama;
using UnityEngine.UIElements;

namespace SampleProjects.NakamaTournaments
{
    public class TournamentView
    {
        private Label titleLabel;
        private Label sizeLabel;
        private Label remainingTimeLabel;

        public void SetVisualElement(VisualElement visualElement)
        {
            titleLabel = visualElement.Q<Label>("title");
            sizeLabel = visualElement.Q<Label>("size");
            remainingTimeLabel = visualElement.Q<Label>("remaining-time");
        }

        public void SetTournament(IApiTournament tournament)
        {
            titleLabel.text = tournament.Title;
            sizeLabel.text = tournament.MaxSize == int.MaxValue ? "Unlimited" : $"{tournament.Size}/{tournament.MaxSize}";
            var resetTime = DateTimeOffset.FromUnixTimeSeconds(tournament.NextReset);
            var remainingTime = resetTime - DateTimeOffset.Now;
            remainingTimeLabel.text = $"{remainingTime:dd}d {remainingTime:hh}h {remainingTime:mm}m";
        }
    }
}
