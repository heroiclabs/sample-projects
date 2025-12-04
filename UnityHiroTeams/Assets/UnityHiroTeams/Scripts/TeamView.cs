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
        private VisualElement avatarBackground;
        private VisualElement avatarIcon;
        private Label nameLabel;
        private Label countLabel;

        private HiroTeamsController controller;

        public void SetVisualElement(HiroTeamsController parent, VisualElement visualElement)
        {
            controller = parent;

            avatarBackground = visualElement.Q<VisualElement>("avatar-background");
            avatarIcon = visualElement.Q<VisualElement>("avatar-icon");
            nameLabel = visualElement.Q<Label>("name");
            countLabel = visualElement.Q<Label>("count");
        }

        public void SetTeam(ITeam team)
        {
            // Parse the AvatarUrl string into a JSON object containing the separate parts that make up a Team's avatar
            try
            {
                var avatarData = JsonUtility.FromJson<AvatarData>(team.AvatarUrl);
                if (avatarData.IconIndex >= 0 && avatarData.IconIndex < controller.AvatarIcons.Length)
                {
                    avatarIcon.style.backgroundImage = controller.AvatarIcons[avatarData.IconIndex];
                }
                if (avatarData.BackgroundIndex >= 0 && avatarData.BackgroundIndex < controller.AvatarBackgrounds.Length)
                {
                    avatarBackground.style.backgroundImage = controller.AvatarBackgrounds[avatarData.BackgroundIndex];
                }
            }
            catch
            {
                // Avatar URL might not be valid JSON, use defaults
            }

            nameLabel.text = team.Name;
            countLabel.text = $"{team.EdgeCount}/{team.MaxCount}";
        }
    }
}
