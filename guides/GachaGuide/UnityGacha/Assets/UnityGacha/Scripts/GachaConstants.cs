using UnityEngine;

namespace UnityGacha
{
    public static class GachaConstants
    {
        // Item property keys
        public const string PropStarRarity   = "star_rarity";
        public const string PropFiveStarPity = "five_star_pity";
        public const string PropSixStarPity  = "six_star_pity";

        // Categories
        public const string CategoryGachaTicket = "gacha_ticket";

        // Rarity labels
        public const string RarityFour      = "Rarity: 4\u2605";
        public const string RarityFive      = "Rarity: 5\u2605";
        public const string RaritySix       = "Rarity: 6\u2605";
        public const string RarityNone      = "-";
        public const string RarityFourShort = "4\u2605";
        public const string RarityFiveShort = "5\u2605";
        public const string RaritySixShort  = "6\u2605";

        // Rarity colors
        public static readonly Color ColorFourStar = new(0.580f, 0.322f, 0.980f, 1.0f);
        public static readonly Color ColorFiveStar = new(1.000f, 0.733f, 0.012f, 1.0f);
        public static readonly Color ColorSixStar  = new(0.996f, 0.353f, 0.000f, 1.0f);
        public static readonly Color ColorDefault  = new(0.745f, 0.722f, 0.855f, 1.0f);

        public static Color GetRarityColor(double rarity) => rarity switch
        {
            4 => ColorFourStar,
            5 => ColorFiveStar,
            6 => ColorSixStar,
            _ => ColorDefault
        };

        public static string GetRarityLabel(double rarity) => rarity switch
        {
            4 => RarityFour,
            5 => RarityFive,
            6 => RaritySix,
            _ => RarityNone
        };

        public static string GetRarityShortLabel(double rarity) => rarity switch
        {
            4 => RarityFourShort,
            5 => RarityFiveShort,
            6 => RaritySixShort,
            _ => string.Empty
        };
    }
}