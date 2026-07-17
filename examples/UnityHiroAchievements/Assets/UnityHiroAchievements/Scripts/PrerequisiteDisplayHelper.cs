using System.Collections.Generic;
using System.Text;
using Hiro;

namespace HiroAchievements
{
    /// <summary>
    /// Helper class for formatting prerequisite achievement information.
    /// </summary>
    public static class PrerequisiteDisplayHelper
    {
        /// <summary>
        /// Formats a list of prerequisite achievements with completion status.
        /// Returns formatted string with checkmarks for completed and X for incomplete.
        /// </summary>
        public static string FormatPrerequisitesList(List<IAchievement> prerequisites, AchievementsController controller)
        {
            if (prerequisites == null || prerequisites.Count == 0)
                return string.Empty;

            var sb = new StringBuilder();
            sb.AppendLine("\n\n📋 Required Achievements:");

            foreach (var prereq in prerequisites)
            {
                bool isComplete = controller.IsAchievementCompleted(prereq);
                string statusIcon = isComplete ? "✓" : "✗";
                string statusColor = isComplete ? "[Complete]" : "[Incomplete]";
                sb.AppendLine($"{statusIcon} {prereq.Name} {statusColor}");
            }

            return sb.ToString();
        }

        /// <summary>
        /// Gets a short summary of incomplete prerequisites (e.g., "2 achievements required").
        /// </summary>
        public static string GetPrerequisitesSummary(int incompleteCount, int totalCount)
        {
            if (totalCount == 0)
                return string.Empty;

            if (incompleteCount == 0)
                return "All prerequisites complete";

            return incompleteCount == 1 
                ? "1 achievement required" 
                : $"{incompleteCount} achievements required";
        }

        /// <summary>
        /// Creates a detailed tooltip text for locked achievements.
        /// </summary>
        public static string CreateTooltipText(List<IAchievement> prerequisites, AchievementsController controller)
        {
            if (prerequisites == null || prerequisites.Count == 0)
                return "This achievement is locked.";

            var sb = new StringBuilder();
            sb.AppendLine("Complete these achievements to unlock:");

            foreach (var prereq in prerequisites)
            {
                bool isComplete = controller.IsAchievementCompleted(prereq);
                if (!isComplete)
                {
                    sb.AppendLine($"• {prereq.Name}");
                }
            }

            return sb.ToString();
        }

        /// <summary>
        /// Checks if all prerequisites are completed.
        /// </summary>
        public static bool AreAllPrerequisitesComplete(List<IAchievement> prerequisites, AchievementsController controller)
        {
            if (prerequisites == null || prerequisites.Count == 0)
                return true;

            foreach (var prereq in prerequisites)
            {
                if (!controller.IsAchievementCompleted(prereq))
                    return false;
            }

            return true;
        }

        /// <summary>
        /// Gets the completion percentage of prerequisites (0-100).
        /// </summary>
        public static float GetPrerequisitesCompletionPercent(List<IAchievement> prerequisites, AchievementsController controller)
        {
            if (prerequisites == null || prerequisites.Count == 0)
                return 100f;

            int completedCount = 0;
            foreach (var prereq in prerequisites)
            {
                if (controller.IsAchievementCompleted(prereq))
                    completedCount++;
            }

            return (float)completedCount / prerequisites.Count * 100f;
        }
    }
}