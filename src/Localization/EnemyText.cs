namespace IncreaseHardMod.Localization;

internal static class EnemyText
{
    internal static string Name(string english, EnemyParent enemy = null)
    {
        if (!GameLanguage.Russian || string.IsNullOrEmpty(english)) return english;
        if (english == "Cleanup Crew") return "Уборщики";
        if (english == "Monster") return "Монстр";

        if (enemy == null && EnemyDirector.instance != null && EnemyDirector.instance.enemiesSpawned != null)
        {
            foreach (var candidate in EnemyDirector.instance.enemiesSpawned)
            {
                if (candidate == null || candidate.enemyName != english) continue;
                enemy = candidate;
                break;
            }
        }

        var localized = enemy != null && enemy.enemyName == english ? enemy.enemyNameLocalized?.GetLocalizedString() : null;
        return string.IsNullOrWhiteSpace(localized) ? english : localized;
    }
}
