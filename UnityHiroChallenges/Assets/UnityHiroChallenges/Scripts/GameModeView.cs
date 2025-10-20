using System.Collections.Generic;
using Hiro;
using UnityEngine;
using UnityEngine.UIElements;

namespace HiroChallenges
{
    public class GameModeView
    {
        private VisualElement root;
        private VisualElement iconElement;
        private Label nameLabel;
        private Label difficultyLabel;
        private VisualElement difficultyBadge;
        private Label descriptionLabel;
        private Label playersLabel;
        private Label categoryLabel;

        // Icon mapping based on category
        private readonly Dictionary<string, string> categoryIcons = new()
        {
            { "arcade", "icon_star" },
            { "team_pvp", "icon_shield" },
            { "survival", "icon_fire" },
            { "racing", "icon_trophy" },
            { "default", "icon_trophy" }
        };

        // Difficulty colors
        private readonly Dictionary<string, Color> difficultyColors = new()
        {
            { "easy", new Color(0.3f, 0.69f, 0.31f) },      // Green
            { "medium", new Color(1f, 0.6f, 0f) },           // Orange
            { "hard", new Color(0.96f, 0.26f, 0.21f) },      // Red
            { "expert", new Color(0.61f, 0.15f, 0.69f) }     // Purple
        };

        public void SetVisualElement(VisualElement visualElement)
        {
            Debug.Log("GameModeView.SetVisualElement called");
            root = visualElement;
            iconElement = visualElement.Q<VisualElement>("mode-icon");
            nameLabel = visualElement.Q<Label>("mode-name");
            difficultyLabel = visualElement.Q<Label>("mode-difficulty");
            difficultyBadge = visualElement.Q<VisualElement>("difficulty-badge");
            descriptionLabel = visualElement.Q<Label>("mode-description");
            playersLabel = visualElement.Q<Label>("mode-players");
            categoryLabel = visualElement.Q<Label>("mode-category");

            // Verify all elements were found
            if (nameLabel == null) Debug.LogError("mode-name not found!");
            if (difficultyLabel == null) Debug.LogError("mode-difficulty not found!");
            if (descriptionLabel == null) Debug.LogError("mode-description not found!");
        }

        public void SetGameMode(string templateId, IChallengeTemplate template)
        {
            Debug.LogFormat("GameModeView.SetGameMode called for template: {0}", templateId);

            // Set name - convert template ID to display name (speed_runner -> Speed Runner)
            nameLabel.text = FormatTemplateName(templateId);
            Debug.LogFormat("Set name to: {0}", nameLabel.text);

            // Set description from additional properties
            if (template.AdditionalProperties.TryGetValue("description", out var description))
            {
                descriptionLabel.text = description;
            }
            else
            {
                descriptionLabel.text = "No description available.";
            }

            // Set difficulty
            if (template.AdditionalProperties.TryGetValue("difficulty", out var difficulty))
            {
                difficultyLabel.text = difficulty.ToUpper();

                // Apply difficulty color
                var difficultyKey = difficulty.ToLower();
                if (difficultyColors.TryGetValue(difficultyKey, out var color))
                {
                    difficultyBadge.style.backgroundColor = color;
                }
            }
            else
            {
                difficultyLabel.text = "MEDIUM";
                difficultyBadge.style.backgroundColor = difficultyColors["medium"];
            }

            // Set player range
            playersLabel.text = $"{template.Players.Min}-{template.Players.Max} Players";

            // Set category
            if (template.AdditionalProperties.TryGetValue("category", out var category))
            {
                categoryLabel.text = $"Category: {FormatCategoryName(category)}";

                // Set icon based on category
                SetIconForCategory(category);
            }
            else
            {
                categoryLabel.text = "Category: General";
                SetIconForCategory("default");
            }
        }

        private string FormatTemplateName(string templateId)
        {
            // Convert snake_case to Title Case (speed_runner -> Speed Runner)
            var words = templateId.Split('_');
            for (int i = 0; i < words.Length; i++)
            {
                if (words[i].Length > 0)
                {
                    words[i] = char.ToUpper(words[i][0]) + words[i].Substring(1);
                }
            }
            return string.Join(" ", words);
        }

        private string FormatCategoryName(string category)
        {
            // Convert to readable format
            return category.Replace("_", " ")
                .Replace("pvp", "PvP")
                .Replace("pve", "PvE");
        }

        private void SetIconForCategory(string category)
        {
            var categoryKey = category.ToLower();
            var iconName = categoryIcons.ContainsKey(categoryKey)
                ? categoryIcons[categoryKey]
                : categoryIcons["default"];

            // Map icon names to their asset GUIDs (you may need to adjust these)
            var iconPath = $"project://database/Assets/UnityHiroChallenges/HeroicUI/IconPictogram/128/{iconName}.png";

            // Note: The actual GUID-based path would need to be set in the UXML or via StyleSheet
            // For now, we'll apply a tint color based on category
            var tintColor = new Color(0.52f, 0.6f, 1f); // Default blue tint

            iconElement.style.unityBackgroundImageTintColor = tintColor;
        }
    }
}
