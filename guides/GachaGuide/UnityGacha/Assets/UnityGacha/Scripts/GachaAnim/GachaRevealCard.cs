using System.Collections.Generic;
using Hiro;
using UnityEngine;
using UnityEngine.UIElements;

namespace UnityGacha.GachaAnim
{
    public sealed class GachaRevealCard : VisualElement
    {
        private const int CardMargin    = 5;
        private const int CardRadius    = 8;
        private const int CardPadding   = 6;
        private const int IconSize      = 60;
        private const int IconMarginBot = 4;
        private const int NameFontSize  = 16;
        private const int RarityFontSize = 14;

        public GachaRevealCard(
            IInventoryItem item,
            Dictionary<string, Sprite> iconDictionary,
            Sprite defaultIcon)
        {
            item.NumericProperties.TryGetValue(GachaConstants.PropStarRarity, out var starRarity);

            style.width                   = Length.Percent(8);
            style.height                  = Length.Percent(80);
            style.marginTop               = CardMargin;
            style.marginBottom            = CardMargin;
            style.marginLeft              = CardMargin;
            style.marginRight             = CardMargin;
            style.alignItems              = Align.Center;
            style.justifyContent          = Justify.Center;
            style.backgroundColor         = new StyleColor(GachaConstants.GetRarityColor(starRarity));
            style.borderTopLeftRadius     = CardRadius;
            style.borderTopRightRadius    = CardRadius;
            style.borderBottomLeftRadius  = CardRadius;
            style.borderBottomRightRadius = CardRadius;
            style.paddingTop              = CardPadding;
            style.paddingBottom           = CardPadding;

            var icon = new VisualElement
            {
                style =
                {
                    width           = IconSize,
                    height          = IconSize,
                    marginBottom    = IconMarginBot,
                    backgroundSize  = new BackgroundSize(BackgroundSizeType.Cover),
                    backgroundImage = GetSprite(item.Id, iconDictionary, defaultIcon) is { } sprite
                        ? new StyleBackground(sprite)
                        : StyleKeyword.Null
                }
            };

            var nameLabel = new Label(!string.IsNullOrEmpty(item.Name) ? item.Name : "Unknown Item")
            {
                style =
                {
                    unityFontStyleAndWeight = FontStyle.Bold,
                    fontSize       = NameFontSize,
                    color          = new StyleColor(Color.white),
                    unityTextAlign = TextAnchor.MiddleCenter,
                    whiteSpace     = WhiteSpace.Normal
                }
            };

            var rarityLabel = new Label($"{starRarity}\u2605")
            {
                style =
                {
                    fontSize       = RarityFontSize,
                    color          = new StyleColor(Color.white),
                    unityTextAlign = TextAnchor.MiddleCenter
                }
            };

            Add(icon);
            Add(nameLabel);
            Add(rarityLabel);
        }

        private static Sprite GetSprite(
            string itemId,
            Dictionary<string, Sprite> iconDictionary,
            Sprite defaultIcon)
        {
            if (iconDictionary != null && iconDictionary.TryGetValue(itemId, out var icon) && icon != null)
                return icon;
            return defaultIcon;
        }
    }
}
