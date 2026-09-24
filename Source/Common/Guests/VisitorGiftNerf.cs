using System.Collections.Generic;
using HarmonyLib;
using RimWorld;
using Verse;

namespace HSKMoreHardcore
{
    /// <summary>
    /// Заменяем медикаменты в подарках гостей на MedicineHerbal.
    /// Postfix на VisitorGiftForPlayerUtility.GenerateGifts.
    /// </summary>
    [StaticConstructorOnStartup]
    public static class VisitorGiftNerf
    {
        private static ThingDef herbalDef;

        static VisitorGiftNerf()
        {
            var harmony = new Harmony("linya.hskmorehardcore.visitorgiftnerf");
            var target = AccessTools.Method(typeof(VisitorGiftForPlayerUtility), nameof(VisitorGiftForPlayerUtility.GenerateGifts));
            if (target != null)
            {
                harmony.Patch(target,
                    postfix: new HarmonyMethod(typeof(VisitorGiftNerf), nameof(Postfix)));
                Log.Message("[HSKMoreHardcore] VisitorGiftNerf applied.");
            }
        }

        public static void Postfix(ref List<Thing> __result)
        {
            if (__result == null)
                return;

            if (herbalDef == null)
                herbalDef = DefDatabase<ThingDef>.GetNamedSilentFail("MedicineHerbal");

            if (herbalDef == null)
                return;

            for (int i = __result.Count - 1; i >= 0; i--)
            {
                var thing = __result[i];
                if (thing.def.IsMedicine && thing.def != herbalDef)
                {
                    int count = thing.stackCount;
                    thing.Destroy();
                    var replacement = ThingMaker.MakeThing(herbalDef);
                    replacement.stackCount = count;
                    __result[i] = replacement;
                }
            }
        }
    }
}
