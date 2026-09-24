using System.Text;
using RimWorld;
using UnityEngine;
using Verse;

namespace HSKMoreHardcore
{
    // Снижает рыночную цену помеченных вещей (CompWornByEnemy) — одежды и оружия.
    // Множители применяются мультипликативно:
    //  - метка износа/стирки (только одежда): washed / wornByEnemy / worn (берём самый жёсткий из применимых)
    //  - починка (одежда и оружие): repairValueMult ^ repairCount
    // С ванильным «снято с трупа» (x0.1) не складываемся: там уже более жёсткий штраф.
    public class StatPart_WornByEnemy : StatPart
    {
        public override void TransformValue(StatRequest req, ref float val)
        {
            var comp = GetComp(req);
            if (comp == null)
                return;
            if (TryMarkMult(comp, out float markMult, out _))
                val *= markMult;
            val *= RepairMult(comp);
        }

        public override string ExplanationPart(StatRequest req)
        {
            var comp = GetComp(req);
            if (comp == null)
                return null;

            var sb = new StringBuilder();
            if (TryMarkMult(comp, out float markMult, out string labelKey))
                sb.AppendLine(labelKey.Translate() + ": x" + markMult.ToStringPercent());
            float repairMult = RepairMult(comp);
            if (repairMult < 1f)
                sb.AppendLine("HSKMoreHardcore_Repaired".Translate() + ": x" + repairMult.ToStringPercent());

            return sb.Length > 0 ? sb.ToString().TrimEndNewlines() : null;
        }

        // Множитель за метку износа/стирки (одежда). 1, если меток нет.
        private static bool TryMarkMult(CompWornByEnemy comp, out float mult, out string labelKey)
        {
            mult = 1f;
            labelKey = null;

            // «Постирано» имеет приоритет (стирка снимает износ)
            if (comp.washed)
            {
                mult = NerfSettings.washedValueMult;
                labelKey = "HSKMoreHardcore_Washed";
                return true;
            }

            bool any = false;
            if (comp.wornByEnemy)
            {
                mult = NerfSettings.wornByEnemyValueMult;
                labelKey = "HSKMoreHardcore_WornByEnemy";
                any = true;
            }
            if (comp.worn && (!any || NerfSettings.wornValueMult < mult))
            {
                mult = NerfSettings.wornValueMult;
                labelKey = "HSKMoreHardcore_Worn";
                any = true;
            }
            return any;
        }

        // Множитель за починки: repairValueMult ^ repairCount (1, если не чинили).
        private static float RepairMult(CompWornByEnemy comp)
        {
            if (comp.repairCount <= 0)
                return 1f;
            return Mathf.Pow(NerfSettings.repairValueMult, comp.repairCount);
        }

        private static CompWornByEnemy GetComp(StatRequest req)
        {
            if (!req.HasThing || req.Thing == null)
                return null;
            var t = req.Thing;
            if (t is Apparel ap)
            {
                // Не складываемся с ванильным штрафом трупной одежды (x0.1) — он жёстче
                if (ap.WornByCorpse)
                    return null;
                return ap.TryGetComp<CompWornByEnemy>();
            }
            if (t.def != null && t.def.IsWeapon)
                return t.TryGetComp<CompWornByEnemy>();
            return null;
        }
    }
}
