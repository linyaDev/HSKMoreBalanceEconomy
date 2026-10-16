using System.Collections.Generic;
using Verse;

namespace HSKMoreHardcore
{
    // Правило множителя цены сделки. Задаётся списком в
    // Defs/Misc/HardcoreSettings.xml (sellPriceRules — продажа игроком,
    // buyPriceRules — покупка), правила проверяются по порядку —
    // срабатывает первое подходящее. Точечные sellPriceOverrides по defName
    // приоритетнее любого правила (для покупки — buyPriceOverrides).
    //
    // Отбор: вещь должна пройти хотя бы один из заданных признаков
    // (categories / thingDefs / requireStat), а затем не попасть под исключения.
    // Правило без единого признака игнорируется — иначе оно накрыло бы всё подряд.
    public class PriceRule
    {
        // Только для читаемости дефа и сообщений в логе
        public string label;

        public float multiplier = 1f;

        // Категории вещи (ThingCategoryDef.defName). Учитывается вложенность:
        // вещь из подкатегории тоже считается принадлежащей родительской.
        public List<string> categories;

        // Конкретные вещи по defName
        public List<string> thingDefs;

        // Деф стата, который должен быть задан в statBases со значением > 0.
        // Так ловится топливо: BurnDurationHours.
        public string requireStat;

        public List<string> excludedCategories;
        public List<string> excludedThingDefs;

        public bool HasAnySelector =>
            (categories != null && categories.Count > 0)
            || (thingDefs != null && thingDefs.Count > 0)
            || !requireStat.NullOrEmpty();

        public bool Matches(ThingDef def)
        {
            if (def == null || !HasAnySelector)
                return false;

            if (!MatchesSelector(def))
                return false;

            if (excludedThingDefs != null && excludedThingDefs.Contains(def.defName))
                return false;

            if (excludedCategories != null && IsInAnyCategory(def, excludedCategories))
                return false;

            return true;
        }

        private bool MatchesSelector(ThingDef def)
        {
            if (thingDefs != null && thingDefs.Contains(def.defName))
                return true;

            if (categories != null && IsInAnyCategory(def, categories))
                return true;

            if (!requireStat.NullOrEmpty() && HasStat(def, requireStat))
                return true;

            return false;
        }

        private static bool IsInAnyCategory(ThingDef def, List<string> categoryNames)
        {
            for (int i = 0; i < categoryNames.Count; i++)
            {
                var cat = DefDatabase<ThingCategoryDef>.GetNamedSilentFail(categoryNames[i]);
                if (cat != null && def.IsWithinCategory(cat))
                    return true;
            }
            return false;
        }

        private static bool HasStat(ThingDef def, string statDefName)
        {
            if (def.statBases == null)
                return false;

            for (int i = 0; i < def.statBases.Count; i++)
            {
                var mod = def.statBases[i];
                if (mod?.stat?.defName == statDefName && mod.value > 0f)
                    return true;
            }
            return false;
        }
    }
}
