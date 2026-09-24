using HarmonyLib;
using RimWorld;
using Verse;

namespace HSKMoreHardcore
{
    // SkyAI (SkyMind.AdvancedAI_TendUtility.MedicineDef) выдаёт вражескому медику медицину
    // по тех-уровню фракции: Industrial/Spacer -> MedicineIndustrial, Ultra/Archo -> MedicineUltratech.
    // Медицина создаётся в рантайме (ThingMaker.MakeThing), не лежит ни в одном def, поэтому
    // XML/конфигом её тип не поменять — единственный путь — Harmony. Понижаем всё выше травяной
    // до HerbMedicine, чтобы рейдеры (включая служителей Хорекса) не таскали индустриалку/глиттер.
    [StaticConstructorOnStartup]
    public static class RaidMedicHerbal
    {
        private static ThingDef herbal;

        static RaidMedicHerbal()
        {
            var type = AccessTools.TypeByName("SkyMind.AdvancedAI_TendUtility");
            if (type == null)
            {
                Log.Warning("[HSKMoreHardcore] RaidMedicHerbal: SkyMind.AdvancedAI_TendUtility not found.");
                return;
            }

            var medicineDef = AccessTools.Method(type, "MedicineDef");
            if (medicineDef == null)
            {
                Log.Warning("[HSKMoreHardcore] RaidMedicHerbal: MedicineDef not found.");
                return;
            }

            var harmony = new Harmony("linya.hskmorehardcore.raidmedic");
            harmony.Patch(medicineDef,
                postfix: new HarmonyMethod(typeof(RaidMedicHerbal), nameof(Postfix)));
            Log.Message("[HSKMoreHardcore] RaidMedicHerbal applied.");
        }

        public static void Postfix(ref ThingDef __result)
        {
            if (__result == null)
                return;

            // Травяную/средневековую не трогаем — понижаем только индустриалку и ультратех.
            if (__result.defName != "MedicineIndustrial" && __result.defName != "MedicineUltratech")
                return;

            if (herbal == null)
                herbal = DefDatabase<ThingDef>.GetNamedSilentFail("HerbMedicine")
                      ?? DefDatabase<ThingDef>.GetNamedSilentFail("MedicineHerbal");

            if (herbal != null)
                __result = herbal;
        }
    }
}
