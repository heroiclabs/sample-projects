using Hiro;
using UnityEngine.UIElements;

namespace HiroInventory
{
    // This view class is kept for potential future use with list-based inventory display
    // Currently, the main controller creates inventory slots dynamically in PopulateInventoryGrid()
    public class InventoryItemView
    {
        private Label nameLabel;
        private Label quantityLabel;
        private Label categoryLabel;
        private VisualElement iconContainer;

        public void SetVisualElement(VisualElement visualElement)
        {
            nameLabel = visualElement.Q<Label>("item-name");
            quantityLabel = visualElement.Q<Label>("item-quantity");
            categoryLabel = visualElement.Q<Label>("item-category");
            iconContainer = visualElement.Q<VisualElement>("item-icon");
        }

        public void SetInventoryItem(IInventoryItem item)
        {
            nameLabel.text = item.Name;
            quantityLabel.text = item.Count > 0 ? $"{item.Count}" : "0";
            categoryLabel.text = item.Category ?? "Uncategorized";

            // Add visual indicator for consumable items
            if (item.Consumable)
            {
                categoryLabel.text += " (Consumable)";
            }
        }
    }
}