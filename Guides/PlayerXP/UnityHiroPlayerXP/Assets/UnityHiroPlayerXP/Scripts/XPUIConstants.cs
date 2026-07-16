using UnityEngine;

namespace PlayerXP
{
    public static class XPUIConstants
    {
        public static readonly Color StatusAchievedColor = new Color(0.4f, 0.8f, 0.4f, 1f);
        public static readonly Color StatusInProgressColor = new Color(0.5f, 0.6f, 1f, 1f);
        public static readonly Color StatusLockedColor = new Color(0.5f, 0.5f, 0.5f, 1f);

        public const string StatusAchieved = "ACHIEVED";
        public const string StatusInProgress = "IN PROGRESS";
        public const string StatusLocked = "LOCKED";

        public const string ProgressFormat = "XP: {0} / {1} ({2:F0}%)";
        public const string SubProgressFormat = "{0} / {1} ({2:F0}%)";
    }
}
