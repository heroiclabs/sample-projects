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
using UnityEngine.UIElements;

namespace HiroEventLeaderboards
{
    public sealed class EventLeaderboardRecordView
    {
        private Label _usernameLabel;
        private Label _scoreLabel;
        private Label _subScoreLabel;
        private Label _rankLabel;

        public void SetVisualElement(VisualElement visualElement)
        {
            _usernameLabel = visualElement.Q<Label>("username");
            _scoreLabel = visualElement.Q<Label>("score");
            _subScoreLabel = visualElement.Q<Label>("sub-score");
            _rankLabel = visualElement.Q<Label>("rank");
        }

        public void SetEventLeaderboardRecord(IEventLeaderboardScore record)
        {
            _usernameLabel.text = record.Username;
            _scoreLabel.text = record.Score.ToString();
            _subScoreLabel.text = record.Subscore.ToString();

            // A rank of 0 would mean that you are yet to submit your first score.
            _rankLabel.text = record.Rank > 0 ? $"#{record.Rank}" : "-";
        }
    }
}
