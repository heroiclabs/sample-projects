using UnityEngine;

namespace HiroAchievements
{
    /// <summary>
    /// Constants for achievements UI styling and configuration.
    /// Centralizes colors, sizes, and other magic values.
    /// </summary>
    public static class AchievementsUIConstants
    {
        // Status Badge Colors
        public static readonly Color StatusClaimedColor = new Color(0.6f, 0.6f, 0.6f, 1f);
        public static readonly Color StatusToClaimColor = new Color(1f, 0.84f, 0f, 1f);
        public static readonly Color StatusCompleteColor = new Color(0.4f, 0.8f, 0.4f, 1f);
        public static readonly Color StatusLockedColor = new Color(0.5f, 0.5f, 0.5f, 1f);
        public static readonly Color StatusInProgressColor = new Color(0.5f, 0.6f, 1f, 1f);

        // Selection Colors
        public static readonly Color SelectionBorderColor = new Color(0.5f, 0.6f, 1f, 1f);
        public static readonly Color SelectionBackgroundColor = new Color(0.85f, 0.9f, 1f, 1f);
        public static readonly Color HoverBackgroundColor = new Color(0.9f, 0.9f, 1f, 1f);
        public static readonly Color DefaultBorderColor = new Color(0.7f, 0.7f, 0.7f, 1f);
        public static readonly Color DefaultBackgroundColor = new Color(0.95f, 0.95f, 0.95f, 1f);

        // Achievement Selection (for list items)
        public static readonly Color AchievementSelectionBorder = new Color(0.5f, 0.6f, 1f, 1f);
        public static readonly Color AchievementSelectionBackground = new Color(0.9f, 0.92f, 1f, 0.3f);
        public static readonly Color AchievementDefaultBorder = new Color(0.776f, 0.765f, 0.894f, 0.5f);

        // Status Text
        public const string StatusClaimed = "CLAIMED";
        public const string StatusToClaim = "CAN CLAIM";
        public const string StatusComplete = "COMPLETE";
        public const string StatusLocked = "🔒 LOCKED";
        public const string StatusInProgress = "IN PROGRESS";
        public const string StatusCheckmark = "✓";

        // Locked Achievement Text
        public const string LockedDescriptionPrefix = "🔒 LOCKED - Complete required achievements to unlock.\n\n";
        public const string LockedClickToViewHint = "Click to view required achievements";

        // UI Text Templates
        public const string ObjectivesFormat = "{0}/{1} Objectives";
        public const string ProgressFormat = "Progress: {0} / {1} ({2:F0}%)";
        public const string SubAchievementProgressFormat = "{0} / {1} ({2:F0}%)";
    }
}