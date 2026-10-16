using System.Reflection;
using HarmonyLib;
using RimWorld;
using Verse;

namespace HSKMoreHardcore
{
    /// <summary>
    /// Подсказка у курсора с ресурсом и количеством в клетке залежи, когда
    /// выбран добытчик (SK.Building_Extractor / Building_AdvancedExtractor) —
    /// как у буровой и сканера. Ваниль в DeepResourceGrid.DeepResourcesOnGUI
    /// показывает её только для вещей с CompDeepDrill/CompDeepScanner, у
    /// SK-добытчиков этих компов нет. Условие то же ванильное: на карте есть
    /// включённый сканер. Сетку залежей добытчикам даёт XML-флаг
    /// drawPlaceWorkersWhileSelected (Patches/Core_SK/ExtractorRadius.xml).
    /// </summary>
    [StaticConstructorOnStartup]
    public static class ExtractorDeepResourceInfo
    {
        private static readonly System.Type extractorType =
            AccessTools.TypeByName("SK.Building_Extractor");
        private static readonly System.Type advExtractorType =
            AccessTools.TypeByName("SK.Building_AdvancedExtractor");
        private static readonly MethodInfo renderMouseAttachments =
            AccessTools.Method(typeof(DeepResourceGrid), "RenderMouseAttachments");

        static ExtractorDeepResourceInfo()
        {
            if (extractorType == null || renderMouseAttachments == null)
            {
                Log.Warning("[HSKMoreHardcore] ExtractorDeepResourceInfo: SK.Building_Extractor или RenderMouseAttachments не найдены — подсказка у курсора не добавлена.");
                return;
            }

            var target = AccessTools.Method(typeof(DeepResourceGrid), nameof(DeepResourceGrid.DeepResourcesOnGUI));
            new Harmony("linya.hskmorehardcore.extractordeepinfo").Patch(target,
                postfix: new HarmonyMethod(typeof(ExtractorDeepResourceInfo), nameof(Postfix)));
        }

        public static void Postfix(DeepResourceGrid __instance)
        {
            Thing sel = Find.Selector.SingleSelectedThing;
            if (sel == null)
                return;
            if (!extractorType.IsInstanceOfType(sel)
                && (advExtractorType == null || !advExtractorType.IsInstanceOfType(sel)))
                return;
            // У вещей с компами бура/сканера ваниль подсказку уже нарисовала
            if (sel.TryGetComp<CompDeepDrill>() != null || sel.TryGetComp<CompDeepScanner>() != null)
                return;
            if (!__instance.AnyActiveDeepScannersOnMap())
                return;

            renderMouseAttachments.Invoke(__instance, null);
        }
    }
}
