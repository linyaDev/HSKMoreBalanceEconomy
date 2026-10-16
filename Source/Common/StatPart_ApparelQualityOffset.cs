using RimWorld;
using Verse;

namespace HSKMoreHardcore
{
    // Общий расчёт добавки к офсету одежды по качеству. Мёртвая зона Normal..Good = 0;
    // ниже — отрицательные тиры, выше — положительные. improvement: +1 лучше, -1 хуже, 0 без изменений.
    public static class ApparelQualityOffsetUtil
    {
        public static float Delta(string statDefName, QualityCategory q, out int improvement)
        {
            improvement = 0;
            if (!NerfSettings.apparelQualityStatStep.TryGetValue(statDefName, out float step))
                return 0f;

            int qi = (int)q;
            int eff;
            if (qi < NerfSettings.apparelQualityBaselineLow)
                eff = qi - NerfSettings.apparelQualityBaselineLow;
            else if (qi > NerfSettings.apparelQualityBaselineHigh)
                eff = qi - NerfSettings.apparelQualityBaselineHigh;
            else
                eff = 0;

            improvement = eff > 0 ? 1 : (eff < 0 ? -1 : 0);
            int dir = NerfSettings.apparelQualityLowerIsBetter.Contains(statDefName) ? -1 : 1;
            return dir * eff * step;
        }
    }

    // Делает бонус одежды носителю зависимым от качества (аддитивно).
    // Работает за счёт того, что StatWorker.StatOffsetFromGear прогоняет StatPart'ы стата с запросом
    // по самой вещи (StatRequest.For(gear)) — и только при ненулевом офсете, т.е. лишь для вещей,
    // которые этот стат уже дают.
    public class StatPart_ApparelQualityOffset : StatPart
    {
        public override void TransformValue(StatRequest req, ref float val)
        {
            if (TryGetDelta(req, out float delta))
                val += delta;
        }

        public override string ExplanationPart(StatRequest req)
        {
            if (TryGetDelta(req, out float delta) && delta != 0f)
                return "HSKMoreHardcore_QualityAdj".Translate() + ": "
                     + delta.ToStringByStyle(parentStat.toStringStyle, ToStringNumberSense.Offset);
            return null;
        }

        private bool TryGetDelta(StatRequest req, out float delta)
        {
            delta = 0f;
            if (parentStat == null || !(req.HasThing && req.Thing is Apparel ap))
                return false;
            if (!ap.TryGetQuality(out QualityCategory q))
                return false;
            if (!NerfSettings.apparelQualityStatStep.ContainsKey(parentStat.defName))
                return false;

            delta = ApparelQualityOffsetUtil.Delta(parentStat.defName, q, out _);
            return true;
        }
    }
}
