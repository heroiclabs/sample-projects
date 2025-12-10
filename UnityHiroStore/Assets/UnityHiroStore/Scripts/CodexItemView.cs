using Hiro;
using UnityEngine.UIElements;

namespace HiroInventory
{
    public class CodexItemView
    {
        private Label nameLabel;
        private Label descriptionLabel;
        private Label categoryLabel;
        private Label consumableLabel;

        public void SetVisualElement(VisualElement visualElement)
        {
            nameLabel = visualElement.Q<Label>("codex-item-name");
            descriptionLabel = visualElement.Q<Label>("codex-item-description");
            categoryLabel = visualElement.Q<Label>("codex-item-category");
            consumableLabel = visualElement.Q<Label>("codex-item-consumable");
        }

        public void SetCodexItem(IInventoryItem item)
        {
            nameLabel.text = item.Name;
            descriptionLabel.text = string.IsNullOrEmpty(item.Description) 
                ? "No description available." 
                : item.Description;
            categoryLabel.text = item.Category ?? "Uncategorized";
            consumableLabel.text = item.Consumable ? "Consumable" : "Non-Consumable";
        }
    }
}