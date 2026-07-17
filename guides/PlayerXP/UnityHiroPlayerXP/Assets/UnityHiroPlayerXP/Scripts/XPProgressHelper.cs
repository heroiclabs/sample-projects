using Hiro;

namespace PlayerXP
{
    public static class XPProgressHelper
    {
        public static float CalculateSubAchievementPercent(ISubAchievement sub)
        {
            return sub.MaxCount > 0 ? (float)sub.Count / sub.MaxCount * 100f : 0f;
        }

        public static bool IsLevelAchieved(ISubAchievement sub)
        {
            return sub.ClaimTimeSec > 0;
        }

        public static bool IsLevelInProgress(ISubAchievement sub)
        {
            return sub.ClaimTimeSec <= 0 && sub.Count > 0;
        }
    }
}
