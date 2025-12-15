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
using UnityEngine;
using UnityEngine.UIElements;

namespace HiroTeams
{
    public class TeamView
    {
        private VisualElement _avatarBackground;
        private VisualElement _avatarIcon;
        private Label _nameLabel;
        private Label _countLabel;

        private HiroTeamsController _controller;

        public void SetVisualElement(HiroTeamsController parent, VisualElement visualElement)
        {
            _controller = parent;

            _avatarBackground = visualElement.Q<VisualElement>("avatar-background");
            _avatarIcon = visualElement.Q<VisualElement>("avatar-icon");
            _nameLabel = visualElement.Q<Label>("name");
            _countLabel = visualElement.Q<Label>("count");
        }

        public void SetTeam(ITeam team)
        {
            // Parse the AvatarUrl string into a JSON object containing the separate parts that make up a Team's avatar
            try
            {
                var avatarData = JsonUtility.FromJson<AvatarData>(team.AvatarUrl);
                if (avatarData.iconIndex >= 0 && avatarData.iconIndex < _controller.AvatarIcons.Length)
                {
                    _avatarIcon.style.backgroundImage = _controller.AvatarIcons[avatarData.iconIndex];
                }
                if (avatarData.backgroundIndex >= 0 && avatarData.backgroundIndex < _controller.AvatarBackgrounds.Length)
                {
                    _avatarBackground.style.backgroundImage = _controller.AvatarBackgrounds[avatarData.backgroundIndex];
                }
            }
            catch
            {
                // Avatar URL might not be valid JSON, use defaults
            }

            _nameLabel.text = team.Name;
            _countLabel.text = $"{team.EdgeCount}/{team.MaxCount}";
        }
    }
}
