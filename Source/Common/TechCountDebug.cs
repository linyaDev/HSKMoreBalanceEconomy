using System.Collections.Generic;
using System.Linq;
using System.Text;
using RimWorld;
using Verse;

namespace HSKMoreHardcore
{
    // Временная диагностика: откуда берутся исследования в каждом техуровне.
    // Виджет прогресса считает их как DefDatabase<ResearchProjectDef>, и если
    // число в тире меняется, надо видеть, какой мод их принёс.
    //
    // Пишет в лог один раз при старте: сводку по тирам и разбивку по модам,
    // а для выбранного тира (traceLevel) — полный список дефов.
    // Выключается константой DebugLog.
    [StaticConstructorOnStartup]
    public static class TechCountDebug
    {
        private const bool DebugLog = true;

        // Для какого тира печатать поимённый список
        private const TechLevel TraceLevel = TechLevel.Industrial;

        static TechCountDebug()
        {
            if (!DebugLog)
                return;

            var all = DefDatabase<ResearchProjectDef>.AllDefsListForReading;

            var sb = new StringBuilder();
            sb.AppendLine($"[HSKMoreHardcore] TechCountDebug: всего исследований {all.Count}");
            sb.AppendLine("  настройки IiB: " + DescribeSettings());

            foreach (var group in all.GroupBy(p => p.techLevel).OrderBy(g => (int)g.Key))
            {
                var byMod = group
                    .GroupBy(p => p.modContentPack?.Name ?? "(без мода)")
                    .OrderByDescending(g => g.Count())
                    .Select(g => $"{g.Key} {g.Count()}");
                sb.AppendLine($"  {group.Key}: {group.Count()} — {string.Join(", ", byMod)}");
            }

            Log.Message(sb.ToString().TrimEndNewlines());

            var traced = all.Where(p => p.techLevel == TraceLevel)
                .OrderBy(p => p.modContentPack?.Name ?? "")
                .ThenBy(p => p.defName)
                .Select(p => $"{p.defName} <- {p.modContentPack?.Name ?? "(без мода)"}");

            Log.Message($"[HSKMoreHardcore] TechCountDebug: {TraceLevel} поимённо:\n" + string.Join("\n", traced));
        }

        // Накопительный счёт изученного по тирам — как в IgnoranceBase.GetPlayerTech.
        // Только после загрузки игры: до неё нет менеджера исследований и
        // ResearchProjectDef.IsFinished падает. Зовётся из HardcoreGameComponent.
        public static void LogProgress()
        {
            if (!DebugLog || Current.Game == null || Find.ResearchManager == null)
                return;

            var all = DefDatabase<ResearchProjectDef>.AllDefsListForReading;
            float percent = PercentResearchNeeded();

            var sb = new StringBuilder();
            sb.AppendLine("[HSKMoreHardcore] TechCountDebug: прогресс по тирам, " + DescribeSettings());

            int cum = 0;
            for (int lvl = 7; lvl >= 1; lvl--)
            {
                var tl = (TechLevel)lvl;
                int total = all.Count(p => p.techLevel == tl);
                if (total == 0)
                    continue;
                cum += all.Count(p => p.techLevel == tl && p.IsFinished);
                int need = UnityEngine.Mathf.CeilToInt(percent * total);
                sb.AppendLine($"  {tl}: всего {total}, изучено накопительно {cum}, нужно {need}"
                    + (cum >= need ? "  <- порог взят" : ""));
            }

            Log.Message(sb.ToString().TrimEndNewlines());
        }

        private static float PercentResearchNeeded()
        {
            object settings = IgnoranceSettings();
            if (settings == null)
                return -1f;
            return (float?)HarmonyLib.AccessTools.Field(settings.GetType(), "PercentResearchNeeded")?.GetValue(settings) ?? -1f;
        }

        private static object IgnoranceSettings()
        {
            var helper = HarmonyLib.AccessTools.TypeByName("DIgnoranceIsBliss.SettingsHelper");
            var settingsField = helper == null ? null : HarmonyLib.AccessTools.Field(helper, "LatestVersion");
            return settingsField?.GetValue(null);
        }

        // Порог и режим расчёта Ignorance Is Bliss
        private static string DescribeSettings()
        {
            object settings = IgnoranceSettings();
            if (settings == null)
                return "мод не найден";

            var type = settings.GetType();
            float percent = (float?)HarmonyLib.AccessTools.Field(type, "PercentResearchNeeded")?.GetValue(settings) ?? -1f;
            bool usePercent = (bool?)HarmonyLib.AccessTools.Field(type, "UsePercentResearched")?.GetValue(settings) ?? false;
            bool useHighest = (bool?)HarmonyLib.AccessTools.Field(type, "UseHighestResearched")?.GetValue(settings) ?? false;
            bool useFixed = (bool?)HarmonyLib.AccessTools.Field(type, "UseFixedTechRange")?.GetValue(settings) ?? false;

            return $"порог {percent:0.###}, по проценту {usePercent}, по высшему {useHighest}, фиксированный диапазон {useFixed}";
        }
    }
}
