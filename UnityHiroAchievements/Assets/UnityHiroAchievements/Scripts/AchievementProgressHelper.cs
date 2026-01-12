using Hiro;

namespace HiroAchievements
{
    /// <summary>
    /// Helper class for calculating achievement progress.
    /// Centralizes logic for both main achievements and sub-achievements.
    /// </summary>
    public static class AchievementProgressHelper
    {
        /// <summary>
        /// Calculates the progress percentage for an achievement.
        /// For achievements with sub-achievements, calculates based on completed sub-achievements.
        /// For normal achievements, calculates based on count/maxCount.
        /// </summary>
        public static float CalculateProgressPercent(IAchievement achievement)
        {
            if (achievement.SubAchievements != null && achievement.SubAchievements.Count > 0)
            {
                // Calculate progress based on completed sub-achievements
                int completedCount = CountCompletedSubAchievements(achievement);
                return (float)completedCount / achievement.SubAchievements.Count * 100f;
            }
            else
            {
                // Use normal count/maxCount for achievements without sub-achievements
                return achievement.MaxCount > 0 
                    ? (float)achievement.Count / achievement.MaxCount * 100f 
                    : 0f;
            }
        }

        /// <summary>
        /// Counts how many sub-achievements are completed for a given achievement.
        /// </summary>
        public static int CountCompletedSubAchievements(IAchievement achievement)
        {
            if (achievement.SubAchievements == null || achievement.SubAchievements.Count == 0)
                return 0;

            int completedCount = 0;
            foreach (var subAchievement in achievement.SubAchievements)
            {
                if (subAchievement.Value.Count >= subAchievement.Value.MaxCount)
                {
                    completedCount++;
                }
            }
            return completedCount;
        }

        /// <summary>
        /// Gets the current and max progress values for display.
        /// For achievements with sub-achievements, returns completed/total sub-achievements.
        /// For normal achievements, returns count/maxCount.
        /// </summary>
        public static (int current, int max) GetProgressValues(IAchievement achievement)
        {
            if (achievement.SubAchievements != null && achievement.SubAchievements.Count > 0)
            {
                int completedCount = CountCompletedSubAchievements(achievement);
                return (completedCount, achievement.SubAchievements.Count);
            }
            else
            {
                return ((int)achievement.Count, (int)achievement.MaxCount);
            }
        }

        /// <summary>
        /// Checks if all sub-achievements are completed for a given achievement.
        /// Returns false if achievement has no sub-achievements.
        /// </summary>
        public static bool AreAllSubAchievementsCompleted(IAchievement achievement)
        {
            if (achievement.SubAchievements == null || achievement.SubAchievements.Count == 0)
                return false;

            foreach (var subAchievement in achievement.SubAchievements)
            {
                if (subAchievement.Value.Count < subAchievement.Value.MaxCount)
                {
                    return false;
                }
            }
            return true;
        }

        /// <summary>
        /// Checks if an achievement has sub-achievements.
        /// </summary>
        public static bool HasSubAchievements(IAchievement achievement)
        {
            return achievement.SubAchievements != null && achievement.SubAchievements.Count > 0;
        }
    }
}