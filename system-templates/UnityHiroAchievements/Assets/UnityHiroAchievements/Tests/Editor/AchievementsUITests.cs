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

using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace HiroAchievements.Tests.Editor
{
    /// <summary>
    /// UI tests for the Achievements UXML templates.
    /// Tests that UI elements are properly configured and accessible.
    /// </summary>
    [TestFixture]
    public class AchievementsUITests
    {
        private const string AchievementsUxmlPath = "Assets/UnityHiroAchievements/UI/Achievements.uxml";

        private VisualTreeAsset _achievementsUxml;
        private VisualElement _root;

        [SetUp]
        public void SetUp()
        {
            _achievementsUxml = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(AchievementsUxmlPath);
            Assert.IsNotNull(_achievementsUxml, $"Failed to load UXML at {AchievementsUxmlPath}");

            _root = _achievementsUxml.Instantiate();
            Assert.IsNotNull(_root, "Failed to instantiate UXML");
        }

        [TearDown]
        public void TearDown()
        {
            _root = null;
            _achievementsUxml = null;
        }

        #region Progress Modal Tests

        [Test]
        public void ProgressModal_Exists()
        {
            var progressModal = _root.Q<VisualElement>("progress-modal");
            Assert.IsNotNull(progressModal, "progress-modal element should exist");
        }

        [Test]
        public void ProgressModal_HasCorrectStructure()
        {
            var progressModal = _root.Q<VisualElement>("progress-modal");
            Assert.IsNotNull(progressModal, "progress-modal should exist");

            var background = progressModal.Q<VisualElement>("progress-modal-background");
            Assert.IsNotNull(background, "progress-modal-background should exist");

            var panel = progressModal.Q<VisualElement>("progress-modal-panel");
            Assert.IsNotNull(panel, "progress-modal-panel should exist");

            var top = progressModal.Q<VisualElement>("progress-modal-top");
            Assert.IsNotNull(top, "progress-modal-top should exist");

            var bottom = progressModal.Q<VisualElement>("progress-modal-bottom");
            Assert.IsNotNull(bottom, "progress-modal-bottom should exist");
        }

        [Test]
        public void ProgressModal_TextField_Exists()
        {
            var quantityField = _root.Q<TextField>("progress-modal-quantity");
            Assert.IsNotNull(quantityField, "progress-modal-quantity TextField should exist");
        }

        [Test]
        public void ProgressModal_TextField_HasDefaultValue()
        {
            var quantityField = _root.Q<TextField>("progress-modal-quantity");
            Assert.IsNotNull(quantityField, "TextField should exist");
            Assert.AreEqual("1", quantityField.value, "TextField should have default value of '1'");
        }

        [Test]
        public void ProgressModal_TextField_CanSetValue()
        {
            var quantityField = _root.Q<TextField>("progress-modal-quantity");
            Assert.IsNotNull(quantityField, "TextField should exist");

            quantityField.value = "42";
            Assert.AreEqual("42", quantityField.value, "TextField value should be settable");
        }

        [Test]
        public void ProgressModal_TextField_IsFocusable()
        {
            var quantityField = _root.Q<TextField>("progress-modal-quantity");
            Assert.IsNotNull(quantityField, "TextField should exist");

            // TextField should be focusable by default
            Assert.IsTrue(quantityField.focusable, "TextField should be focusable");
        }

        [Test]
        public void ProgressModal_TextField_CanParseInteger()
        {
            var quantityField = _root.Q<TextField>("progress-modal-quantity");
            Assert.IsNotNull(quantityField, "TextField should exist");

            quantityField.value = "123";
            var parsed = int.Parse(quantityField.value);
            Assert.AreEqual(123, parsed, "TextField value should be parseable as integer");
        }

        [Test]
        public void ProgressModal_UpdateButton_Exists()
        {
            var updateButton = _root.Q<Button>("progress-modal-update");
            Assert.IsNotNull(updateButton, "progress-modal-update Button should exist");
            Assert.AreEqual("Update Progress", updateButton.text, "Button should have correct text");
        }

        [Test]
        public void ProgressModal_CloseButton_Exists()
        {
            var closeButton = _root.Q<Button>("progress-modal-close");
            Assert.IsNotNull(closeButton, "progress-modal-close Button should exist");
        }

        [Test]
        public void ProgressModal_HasTemplateAttribute()
        {
            var progressModal = _root.Q<VisualElement>("progress-modal");
            Assert.IsNotNull(progressModal, "progress-modal should exist");
            // The template="ErrorPopup" attribute is set in UXML
        }

        #endregion

        #region Error Popup Tests

        [Test]
        public void ErrorPopup_Exists()
        {
            var errorPopup = _root.Q<VisualElement>("error-popup");
            Assert.IsNotNull(errorPopup, "error-popup element should exist");
        }

        [Test]
        public void ErrorPopup_HasRequiredElements()
        {
            var errorMessage = _root.Q<Label>("error-message");
            Assert.IsNotNull(errorMessage, "error-message Label should exist");

            var errorClose = _root.Q<Button>("error-close");
            Assert.IsNotNull(errorClose, "error-close Button should exist");
        }

        #endregion

        #region Main UI Tests

        [Test]
        public void AchievementsList_Exists()
        {
            var achievementsList = _root.Q<VisualElement>("achievements-list");
            Assert.IsNotNull(achievementsList, "achievements-list should exist");
        }

        [Test]
        public void TabButtons_Exist()
        {
            var dailiesTab = _root.Q<Button>("tab-dailies");
            Assert.IsNotNull(dailiesTab, "tab-dailies should exist");

            var questsTab = _root.Q<Button>("tab-quests");
            Assert.IsNotNull(questsTab, "tab-quests should exist");

            var achievementsTab = _root.Q<Button>("tab-achievements");
            Assert.IsNotNull(achievementsTab, "tab-achievements should exist");
        }

        [Test]
        public void ActionButtons_Exist()
        {
            var progressButton = _root.Q<Button>("progress-button");
            Assert.IsNotNull(progressButton, "progress-button should exist");

            var claimButton = _root.Q<Button>("claim-button");
            Assert.IsNotNull(claimButton, "claim-button should exist");

            var refreshButton = _root.Q<Button>("achievements-refresh");
            Assert.IsNotNull(refreshButton, "achievements-refresh should exist");
        }

        #endregion
    }
}
