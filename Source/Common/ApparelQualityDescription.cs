using System.Text;
using HarmonyLib;
using RimWorld;
using Verse;

namespace HSKMoreHardcore
{
    // Дописывает в подробное описание одежды (Apparel.DescriptionDetailed) секцию с уже
    // скорректированными по качеству бонусами для статов из apparelQualityStatStep.
    // Базовое описание кэшируется на дефе и показывает авторские (базовые) значения; здесь
    // показываем фактические для конкретного качества — через тот же StatWorker.StatOffsetFromGear.
    [StaticConstructorOnStartup]
    public static class ApparelQualityDescription
    {
        static ApparelQualityDescription()
        {
            var getter = AccessTools.PropertyGetter(typeof(Apparel), nameof(Apparel.DescriptionDetailed));
            if (getter == null)
                return;

            var harmony = new Harmony("linya.hskmorehardcore.apparelqualitydesc");
            harmony.Patch(getter, postfix: new HarmonyMethod(typeof(ApparelQualityDescription), nameof(Postfix)));
            Log.Message("[HSKMoreHardcore] ApparelQualityDescription applied.");
        }

        // Цвета строк статов (rich-text hex)
        private const string ImproveColor = "#9BD7FF"; // улучшение
        private const string WorseColor = "#FF6B6B";   // ухудшение
        // без изменений — белый (без тега)

        public static void Postfix(Apparel __instance, ref string __result)
        {
            if (__instance?.def?.equippedStatOffsets == null)
                return;
            if (!__instance.TryGetQuality(out QualityCategory q))
                return;

            StringBuilder sb = null;
            foreach (var mod in __instance.def.equippedStatOffsets)
            {
                if (mod.stat == null || !NerfSettings.apparelQualityStatStep.ContainsKey(mod.stat.defName))
                    continue;

                ApparelQualityOffsetUtil.Delta(mod.stat.defName, q, out int improvement);
                if (improvement == 0)
                    continue; // не меняется при этом качестве — не пишем

                float adjusted = StatWorker.StatOffsetFromGear(__instance, mod.stat);

                if (sb == null)
                {
                    sb = new StringBuilder();
                    sb.Append("\n\n");
                    sb.Append("HSKMoreHardcore_QualityAdj".Translate());
                    sb.Append(":");
                }

                string line = mod.stat.LabelCap + ": " + mod.stat.ValueToString(adjusted, ToStringNumberSense.Offset);
                sb.Append("\n");
                sb.Append(improvement > 0
                    ? $"<color={ImproveColor}>{line}</color>"
                    : $"<color={WorseColor}>{line}</color>");
            }

            if (sb != null)
                __result += sb.ToString();
        }
    }
}
