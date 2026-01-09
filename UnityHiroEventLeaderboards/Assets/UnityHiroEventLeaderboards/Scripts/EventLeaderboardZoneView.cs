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

using UnityEngine;
using UnityEngine.UIElements;

namespace HiroEventLeaderboards
{
    public sealed class EventLeaderboardZoneView
    {
        private Label _zoneLabel;
        private VisualElement _arrowLeft;
        private VisualElement _arrowRight;

        public enum ZoneType
        {
            Promotion,
            Demotion
        }

        public void SetVisualElement(VisualElement visualElement)
        {
            _zoneLabel = visualElement.Q<Label>("zone-label");
            _arrowLeft = visualElement.Q<VisualElement>("arrow-left");
            _arrowRight = visualElement.Q<VisualElement>("arrow-right");
        }

        public void SetZone(ZoneType zoneType)
        {
            if (zoneType == ZoneType.Promotion)
            {
                _zoneLabel.text = "PROMOTION ZONE";
                _zoneLabel.style.color = new StyleColor(new Color(0.075f, 0.827f, 0.761f)); // Green color #13D3C2

                _arrowLeft.style.unityBackgroundImageTintColor = new StyleColor(new Color(0.075f, 0.827f, 0.761f));
                _arrowRight.style.unityBackgroundImageTintColor = new StyleColor(new Color(0.075f, 0.827f, 0.761f));
            }
            else // Demotion
            {
                _zoneLabel.text = "DEMOTION ZONE";
                _zoneLabel.style.color = new StyleColor(new Color(0.890f, 0.200f, 0.192f)); // Red color #E33331

                _arrowLeft.style.rotate = new Rotate(new Angle(180f));
                _arrowRight.style.rotate = new Rotate(new Angle(180f));
                _arrowLeft.style.unityBackgroundImageTintColor = new StyleColor(new Color(0.890f, 0.200f, 0.192f));
                _arrowRight.style.unityBackgroundImageTintColor = new StyleColor(new Color(0.890f, 0.200f, 0.192f));
            }
        }
    }
}
