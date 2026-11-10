using System.Globalization;
using System.Text.RegularExpressions;

namespace IncreaseHardMod.Quests;

/// <summary>
/// Quest titles in the player's own game language. The host builds the English title and syncs it; the Russian one is
/// rebuilt on every client from the quest's objective, target and parameter (which every client already has), so the
/// network format is unchanged and each player sees their own language.
/// </summary>
internal static class QuestText
{
    internal static bool Russian => Localization.GameLanguage.Russian;

    internal static string Title(ActiveQuest q)
    {
        if (q == null) return "";
        if (!Russian) return q.Title ?? "";
        string ru = Ru(q);
        if (string.IsNullOrEmpty(ru)) return q.Title ?? "";
        return q.Special ? "КОНТРАКТ: " + ru : ru;
    }

    /// <summary>1 монстр, 2 монстра, 5 монстров.</summary>
    internal static string Plural(int n, string one, string few, string many)
    {
        int m10 = System.Math.Abs(n) % 10, m100 = System.Math.Abs(n) % 100;
        return m10 == 1 && m100 != 11 ? one : m10 >= 2 && m10 <= 4 && (m100 < 12 || m100 > 14) ? few : many;
    }

    internal static string Stat(StatKind s) => !Russian ? StatKindInfo.Label(s) : s switch
    {
        StatKind.Health => "Здоровье", StatKind.Stamina => "Выносливость", StatKind.SprintSpeed => "Скорость бега",
        StatKind.GrabStrength => "Сила захвата", StatKind.GrabRange => "Дальность захвата", StatKind.ThrowStrength => "Сила броска",
        StatKind.ExtraJump => "Доп. прыжок", StatKind.TumbleLaunch => "Бросок кувырком", _ => s.ToString(),
    };

    internal static string Tier(UpgradeTier t) => !Russian ? UpgradeTierInfo.Label(t) : t switch
    {
        UpgradeTier.Useless => "базовое", UpgradeTier.Mid => "обычное", UpgradeTier.Useful => "редкое", UpgradeTier.Power => "мощное", _ => t.ToString(),
    };

    internal static string PerkName(int perk)
    {
        var node = Skills.SkillCatalog.QuestPerk(perk);
        return node != null ? Skills.SkillText.Name(node) : Russian ? "перк контракта" : "contract perk";
    }

    private static string Clock(int seconds) => $"{seconds / 60}:{seconds % 60:00}";
    private static string K(int dollars) => "$" + (dollars / 1000f).ToString("0.#", CultureInfo.InvariantCulture) + "K";

    private static string Ru(ActiveQuest q)
    {
        int n = q.Target;
        string monsters = Plural(n, "монстра", "монстров", "монстров");
        string valuables = Plural(n, "ценность", "ценности", "ценностей");
        switch (q.Objective)
        {
            case ObjectiveType.KillAny: return $"Убейте {n} {monsters}";
            case ObjectiveType.MeleeKill: return $"Убейте {n} {monsters} в ближнем бою";
            case ObjectiveType.GunKill: return $"Застрелите {n} {monsters}";
            case ObjectiveType.HandKill: return $"Убейте {n} {monsters} голыми руками";
            case ObjectiveType.KillType: return $"Убейте {n} {monsters} вида «{Localization.EnemyText.Name(q.Param)}»";
            case ObjectiveType.KillTier:
            {
                int tier = int.TryParse(q.Param, out int t) ? t : -1;
                string kind = tier switch { 0 => "слабых", 1 => "средних", 2 => "сильных", _ => $"уровня {tier + 1}" };
                return $"Убейте {n} {kind} {monsters}";
            }
            case ObjectiveType.PitKill: return $"Столкните {n} {monsters} в пропасть";
            case ObjectiveType.StunEnemies: return $"Оглушите {n} {monsters}";
            case ObjectiveType.NoGunKills: return "Пройдите уровень, никого не застрелив";
            case ObjectiveType.NoDamage: return "Пройдите уровень без урона";
            case ObjectiveType.NoDeaths: return n <= 0 ? "Пройдите уровень без смертей" : $"Пройдите уровень. Допустимо смертей: {n}";
            case ObjectiveType.ReachHaul: return $"Сдайте добычу на сумму {K(n)}";
            case ObjectiveType.IntactExtract: return $"Сдайте {n} {valuables} без повреждений";
            case ObjectiveType.ExtractCount: return $"Сдайте {n} {valuables}";
            case ObjectiveType.HighValueExtract:
                return int.TryParse(q.Param, out int minimum)
                    ? $"Сдайте {n} {valuables} стоимостью от {K(minimum)} за штуку"
                    : $"Сдайте {n} {Plural(n, "дорогую ценность", "дорогие ценности", "дорогих ценностей")}";
            case ObjectiveType.SoulExtract: return $"Сдайте {n} {Plural(n, "душу", "души", "душ")} монстров";
            case ObjectiveType.Revive: return n == 1 ? "Воскресите павшего напарника" : $"Воскресите {n} {Plural(n, "павшего напарника", "павших напарников", "павших напарников")}";
            case ObjectiveType.MoonRitual: return n == 1 ? "Луна судит: заслужите её одобрение у точки сдачи" : $"Луна судит: заслужите её одобрение у {n} точек сдачи";
            case ObjectiveType.ExtractionRush: return $"Закройте все точки сдачи за {Clock(n)}";
            case ObjectiveType.HatTrick:
                return int.TryParse(q.Param, out int window) ? $"Хет-трик: убейте {n} {monsters} за {window} с" : $"Хет-трик: убейте {n} {monsters} подряд";
            case ObjectiveType.CrownJewel:
            {
                // The jewel's name and value only travel inside the English title: "... extract the NAME ($12K) ...".
                var m = Regex.Match(q.Title ?? "", @"extract the (.+) \((\$[0-9.]+K)\)");
                string jewel = m.Success ? $"{m.Groups[1].Value} ({m.Groups[2].Value})" : "самую ценную вещь";
                return q.Param == "100" ? $"Жемчужина: сдайте {jewel} без единой царапины" : $"Жемчужина: сдайте {jewel}, сохранив 75% стоимости";
            }
            case ObjectiveType.FastOpener: return $"Быстрый старт: закройте первую точку сдачи за {Clock(n)}";
            case ObjectiveType.LightsOut: return $"Без света: исследуйте уровень без фонарей в общей сложности {Clock(n)}";
            case ObjectiveType.Oops: return $"Упс! Разбейте ценности на сумму {K(n)}";
            case ObjectiveType.RagdollOlympics: return $"Олимпиада кувырков: кувыркнитесь {n} {Plural(n, "раз", "раза", "раз")}";
            case ObjectiveType.TalkItOut:
                return (SemiFunc.PlayerGetList()?.Count ?? 1) <= 1
                    ? $"Выговоритесь: поговорите сами с собой {n} с (это совершенно нормально)"
                    : $"Выговоритесь: наберите {n} с разговора в голосовом чате";
            case ObjectiveType.Ballerina: return "Балерина: покружитесь 5 раз за 6 секунд";
            case ObjectiveType.PayRespects: return "Почтите память: присядьте на 3 с на месте гибели монстра";
            case ObjectiveType.MonsterHugger: return $"Обнимашки с монстром: постойте вплотную к живому монстру {n} с";
        }
        return "";
    }
}
